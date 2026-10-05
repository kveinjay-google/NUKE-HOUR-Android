#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software. It is made
 * available under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using FS = OpenRA.FileSystem.FileSystem;

namespace OpenRA.Mods.Common.Installer
{
	public sealed class MacOSDirectorySourceResolver : ISourceResolver
	{
		const int DefaultMaximumDepth = 6;
		const int DefaultMaximumDirectories = 20000;

		public string FindSourcePath(ModContent.ModSource source)
		{
			if (Platform.CurrentPlatform != PlatformType.OSX)
				return null;

			var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
			return FindSourcePath(source, CommonSearchRoots(home, "/Volumes"));
		}

		public Availability GetAvailability()
		{
			return Platform.CurrentPlatform == PlatformType.OSX ?
				Availability.DigitalInstall : Availability.Unavailable;
		}

		public static IEnumerable<string> CommonSearchRoots(string userProfile, string volumesRoot)
		{
			var roots = new[]
			{
				"/Applications",
				volumesRoot,
				Path.Combine(userProfile, "Applications"),
				Path.Combine(userProfile, "Games"),
				Path.Combine(userProfile, "GOG Games"),
				Path.Combine(userProfile, "Library", "Application Support", "Steam", "steamapps", "common"),
				Path.Combine(userProfile, "Library", "Application Support", "CrossOver", "Bottles"),
				Path.Combine(userProfile, "Library", "Application Support", "com.isaacmarovitz.Whisky", "Bottles"),
				Path.Combine(userProfile, "Library", "Application Support", "Porting Kit"),
				Path.Combine(userProfile, ".wine", "drive_c"),
			};

			return roots
				.Where(path => !string.IsNullOrWhiteSpace(path))
				.Select(Path.GetFullPath)
				.Distinct(StringComparer.OrdinalIgnoreCase);
		}

		public static string FindSourcePath(
			ModContent.ModSource source, IEnumerable<string> roots,
			int maximumDepth = DefaultMaximumDepth, int maximumDirectories = DefaultMaximumDirectories)
		{
			if (source == null || roots == null || source.RequiredFiles.Length == 0 ||
				maximumDepth < 0 || maximumDirectories <= 0)
				return null;

			var pending = new Queue<(string Path, int Depth)>();
			var queuedRoots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			foreach (var root in roots.Where(path => !string.IsNullOrWhiteSpace(path)))
			{
				try
				{
					var fullPath = Path.GetFullPath(root);
					if (queuedRoots.Add(fullPath))
						pending.Enqueue((fullPath, 0));

					if (pending.Count >= maximumDirectories)
						break;
				}
				catch (Exception)
				{
					// Ignore malformed or inaccessible candidate roots.
				}
			}

			var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			while (pending.Count != 0 && visited.Count < maximumDirectories)
			{
				var candidate = pending.Dequeue();
				if (!visited.Add(candidate.Path) || !IsSearchableDirectory(candidate.Path))
					continue;

				if (IsValidSourcePath(candidate.Path, source))
					return candidate.Path;

				if (candidate.Depth >= maximumDepth)
					continue;

				var remainingDirectories = maximumDirectories - visited.Count - pending.Count;
				if (remainingDirectories <= 0)
					continue;

				string[] children;
				try
				{
					children = Directory.EnumerateDirectories(candidate.Path)
						.Take(remainingDirectories)
						.OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
						.ToArray();
				}
				catch (Exception)
				{
					continue;
				}

				foreach (var child in children)
					pending.Enqueue((child, candidate.Depth + 1));
			}

			return null;
		}

		public static bool IsValidSourcePath(string path, ModContent.ModSource source)
		{
			if (source == null || source.RequiredFiles.Length == 0 || !Directory.Exists(path))
				return false;

			try
			{
				return source.RequiredFiles.All(filename =>
					File.Exists(FS.ResolveCaseInsensitivePath(Path.Combine(path, filename))));
			}
			catch (Exception)
			{
				return false;
			}
		}

		static bool IsSearchableDirectory(string path)
		{
			try
			{
				if (!Directory.Exists(path))
					return false;

				return (new DirectoryInfo(path).Attributes & FileAttributes.ReparsePoint) == 0;
			}
			catch (Exception)
			{
				return false;
			}
		}

		public static string ChooseDirectory(string prompt)
		{
			if (Platform.CurrentPlatform != PlatformType.OSX)
				return null;

			const string Script = "on run argv\n" +
				"activate\n" +
				"set selectedFolder to choose folder with prompt (item 1 of argv)\n" +
				"return POSIX path of selectedFolder\n" +
				"end run";
			try
			{
				var startInfo = new ProcessStartInfo("/usr/bin/osascript")
				{
					UseShellExecute = false,
					RedirectStandardOutput = true,
					RedirectStandardError = true,
					CreateNoWindow = true,
				};
				startInfo.ArgumentList.Add("-e");
				startInfo.ArgumentList.Add(Script);
				startInfo.ArgumentList.Add("--");
				startInfo.ArgumentList.Add(prompt ?? string.Empty);

				using var process = Process.Start(startInfo);
				if (process == null)
					return null;

				var output = process.StandardOutput.ReadToEnd();
				process.WaitForExit();
				return process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output) ?
					Path.GetFullPath(output.Trim()) : null;
			}
			catch (Exception)
			{
				return null;
			}
		}
	}
}
