#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software. It is
 * made available under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using OpenRA.Mods.RA2.UtilityCommands;

namespace OpenRA.Mods.RA2.Content
{
	/// <summary>
	/// Converts maps from the player's locally imported retail archives into
	/// OpenRA map packages in writable user storage before MapCache starts.
	/// Retail bytes remain user data and are never added to an application build.
	/// </summary>
	public static class RetailMapInstaller
	{
		const string GeneratedStarterMap = "nukehour-starter-v1.oramap";
		const string LegacyMapConversionVersion = "v5";

		static readonly HashSet<string> MultiplayerArchives = new(StringComparer.OrdinalIgnoreCase)
		{
			"multi.mix", "multimd.mix",
		};

		static readonly HashSet<string> LegacyMapExtensions = new(StringComparer.OrdinalIgnoreCase)
		{
			".map", ".mpr", ".yrm",
		};
		static readonly HashSet<string> OpenRaMapPackageExtensions = new(StringComparer.OrdinalIgnoreCase)
		{
			".oramap", ".zip",
		};

		public static bool IsMultiplayerArchive(string name)
			=> MultiplayerArchives.Contains(LeafName(name));

		public static bool IsOpenRaMapPackage(string path)
			=> OpenRaMapPackageExtensions.Contains(Path.GetExtension(path));

		public static int EnsureInstalled(ModData modData, Action<int, int, string> progress = null)
		{
			if (modData == null)
				throw new ArgumentNullException(nameof(modData));

			var destinationRoot = Path.Combine(
				Platform.SupportDir, "maps", modData.Manifest.Id, "nukehour-storage-v1");
			var failureRoot = Path.Combine(
				Platform.SupportDir, "Cache", modData.Manifest.Id + "-retail-map-v1");
			Directory.CreateDirectory(destinationRoot);
			Directory.CreateDirectory(failureRoot);

			var discovered = 0;
			var installed = 0;
			var failures = new List<string>();
			var archives = modData.ModFiles.MountedPackages.Where(p => IsMultiplayerArchive(p.Name)).ToArray();
			var contentRoot = Path.Combine(Platform.SupportDir, "Content", modData.Manifest.Id);
			var roots = new[] { contentRoot, destinationRoot }.Distinct(StringComparer.Ordinal).ToArray();
			var looseMaps = roots.Where(Directory.Exists).SelectMany(EnumerateLooseMapPaths).ToArray();
			var total = archives.Sum(p => p.Contents.Count(n => LegacyMapExtensions.Contains(Path.GetExtension(n)))) +
				looseMaps.Count(p => LegacyMapExtensions.Contains(Path.GetExtension(p)) ||
					(p.StartsWith(contentRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal) && IsOpenRaMapPackage(p)));
			var completed = 0;
			progress?.Invoke(0, total, string.Empty);

			foreach (var package in archives)
			{
				var archiveName = LeafName(package.Name);
				foreach (var mapName in package.Contents
					.Where(name => LegacyMapExtensions.Contains(Path.GetExtension(name)))
					.OrderBy(name => name, StringComparer.OrdinalIgnoreCase))
				{
					discovered++;
					progress?.Invoke(completed, total, "");
					try
					{
						using var source = package.GetStream(mapName) ??
							throw new InvalidDataException("The map entry could not be opened.");
						if (InstallLegacyMap(modData, source, archiveName, mapName, destinationRoot, failureRoot))
							installed++;
					}
					catch (Exception e) when (e is IOException || e is InvalidDataException || e is ArgumentException)
					{
						failures.Add($"{archiveName}:{Path.GetFileName(mapName)} ({e.GetType().Name})");
					}
					finally { progress?.Invoke(++completed, total, string.Empty); }
				}
			}

			foreach (var root in roots)
			{
				if (!Directory.Exists(root))
					continue;

				foreach (var path in looseMaps.Where(p => p.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal)).ToArray())
				{
					var extension = Path.GetExtension(path);
					if (LegacyMapExtensions.Contains(extension))
					{
						discovered++;
					progress?.Invoke(completed, total, "");
						try
						{
							using var source = File.OpenRead(path);
							if (InstallLegacyMap(
								modData, source, "loose", Path.GetFileName(path), destinationRoot, failureRoot))
								installed++;
						}
						catch (Exception e) when (e is IOException || e is InvalidDataException || e is ArgumentException)
						{
							failures.Add($"loose:{Path.GetFileName(path)} ({e.GetType().Name})");
						}
						finally { progress?.Invoke(++completed, total, string.Empty); }
					}
					else if (root == contentRoot && OpenRaMapPackageExtensions.Contains(extension))
					{
						discovered++;
					progress?.Invoke(completed, total, "");
						try
						{
							if (InstallOpenRaMap(path, destinationRoot))
								installed++;
						}
						catch (Exception e) when (e is IOException || e is InvalidDataException)
						{
							failures.Add($"oramap:{Path.GetFileName(path)} ({e.GetType().Name})");
						}
						finally { progress?.Invoke(++completed, total, string.Empty); }
					}
				}
			}

			var available = Directory.EnumerateFiles(destinationRoot, "*.oramap", SearchOption.TopDirectoryOnly)
				.Where(path => !Path.GetFileName(path).Equals(GeneratedStarterMap, StringComparison.OrdinalIgnoreCase))
				.Count(IsValidOpenRaMap);
			Log.Write("debug", $"Retail map import: discovered={discovered}, installed={installed}, available={available}, failed={failures.Count}");
			foreach (var failure in failures.Take(20))
				Log.Write("debug", $"Retail map import skipped {failure}");

			if (discovered > 0 && available == 0)
				throw new InvalidDataException("Imported retail maps could not be converted into playable maps.");

			return available;
		}

		static bool InstallLegacyMap(
			ModData modData, Stream source, string archiveName, string mapName,
			string destinationRoot, string failureRoot)
		{
			using var buffer = new MemoryStream();
			source.CopyTo(buffer);
			var bytes = buffer.ToArray();
			var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant()[..12];
			var prefix = $"retail-{SafeStem(archiveName)}-{SafeStem(mapName)}-";
			var destination = Path.Combine(
				destinationRoot, prefix + hash + "-" + LegacyMapConversionVersion + ".oramap");
			var failureMarker = Path.Combine(
				failureRoot, prefix + hash + "-" + LegacyMapConversionVersion + ".failed");
			if (IsValidOpenRaMap(destination))
				return false;
			if (File.Exists(destination))
				File.Delete(destination);
			if (File.Exists(failureMarker))
				return false;

			var staging = destination + ".staging-" + Guid.NewGuid().ToString("N");
			try
			{
				ImportRA2MapCommand.ConvertMap(modData, new MemoryStream(bytes, writable: false), mapName, staging);
				if (!IsValidOpenRaMap(staging))
					throw new InvalidDataException("The converted map package is invalid.");

				File.Move(staging, destination, true);
				foreach (var stale in Directory.EnumerateFiles(destinationRoot, prefix + "*.oramap", SearchOption.TopDirectoryOnly))
					if (!stale.Equals(destination, StringComparison.Ordinal))
						File.Delete(stale);
				return true;
			}
			catch (Exception e) when (e is InvalidDataException || e is ArgumentException)
			{
				File.WriteAllText(failureMarker, "conversion-failed-" + LegacyMapConversionVersion);
				throw;
			}
			finally
			{
				if (File.Exists(staging))
					File.Delete(staging);
			}
		}

		static bool InstallOpenRaMap(string source, string destinationRoot)
		{
			if (!TryValidateOpenRaMapPackage(source, out var reason))
				throw new InvalidDataException(reason);

			using var input = File.OpenRead(source);
			using var sha = SHA256.Create();
			var hash = Convert.ToHexString(sha.ComputeHash(input)).ToLowerInvariant()[..12];
			var destination = Path.Combine(destinationRoot,
				$"user-{SafeStem(source)}-{hash}.oramap");
			if (Path.GetFullPath(source).Equals(Path.GetFullPath(destination), StringComparison.Ordinal))
				return false;

			if (File.Exists(destination) && FilesEqual(source, destination))
				return false;

			var staging = destination + ".staging-" + Guid.NewGuid().ToString("N");
			try
			{
				File.Copy(source, staging);
				File.Move(staging, destination, true);
				return true;
			}
			finally
			{
				if (File.Exists(staging))
					File.Delete(staging);
			}
		}

		public static bool TryValidateOpenRaMapPackage(string path, out string reason)
		{
			reason = string.Empty;
			if (!File.Exists(path))
			{
				reason = "The selected map package does not exist.";
				return false;
			}

			try
			{
				using var archive = ZipFile.OpenRead(path);
				var entries = archive.Entries.Select(entry => entry.FullName).ToArray();
				var yamlEntry = archive.Entries.FirstOrDefault(entry =>
					entry.FullName.Equals("map.yaml", StringComparison.OrdinalIgnoreCase));
				if (yamlEntry == null ||
					!entries.Contains("map.bin", StringComparer.OrdinalIgnoreCase) ||
					PublicContentSafetyPolicy.ValidateMapPackageEntries(entries) != ContentSafetyViolation.None)
				{
					reason = "The selected map package is missing required map data or contains unsafe overrides.";
					return false;
				}

				using var reader = new StreamReader(yamlEntry.Open());
				var playableSlots = 0;
				var spawnPoints = 0;
				while (reader.ReadLine() is { } line)
				{
					var value = line.Trim();
					if (value.Equals("Playable: True", StringComparison.OrdinalIgnoreCase))
						playableSlots++;
					else if (value.EndsWith(": mpspawn", StringComparison.OrdinalIgnoreCase))
						spawnPoints++;
				}

				if (playableSlots < 2 || spawnPoints < 2)
				{
					reason = "The selected map package needs at least two playable slots and two spawn points.";
					return false;
				}

				return true;
			}
			catch (InvalidDataException e)
			{
				reason = "The selected map package is not a readable ZIP archive: " + e.Message;
				return false;
			}
			catch (IOException e)
			{
				reason = "The selected map package could not be read: " + e.Message;
				return false;
			}
		}

		static bool IsValidOpenRaMap(string path)
			=> TryValidateOpenRaMapPackage(path, out _);

		static IEnumerable<string> EnumerateLooseMapPaths(string root)
		{
			if (!Directory.Exists(root))
				yield break;

			var options = new EnumerationOptions
			{
				RecurseSubdirectories = true,
				AttributesToSkip = FileAttributes.ReparsePoint,
				IgnoreInaccessible = true,
			};
			foreach (var path in Directory.EnumerateFiles(root, "*", options))
			{
				var extension = Path.GetExtension(path);
				if (LegacyMapExtensions.Contains(extension) || OpenRaMapPackageExtensions.Contains(extension))
					yield return path;
			}
		}

		static bool FilesEqual(string left, string right)
		{
			using var leftStream = File.OpenRead(left);
			using var rightStream = File.OpenRead(right);
			if (leftStream.Length != rightStream.Length)
				return false;

			using var leftHash = SHA256.Create();
			using var rightHash = SHA256.Create();
			return leftHash.ComputeHash(leftStream).SequenceEqual(rightHash.ComputeHash(rightStream));
		}

		static string LeafName(string path)
		{
			var normalized = (path ?? string.Empty).Replace('\\', '/');
			var separator = Math.Max(normalized.LastIndexOf('/'), normalized.LastIndexOf('|'));
			return separator >= 0 ? normalized[(separator + 1)..] : normalized;
		}

		static string SafeStem(string name)
		{
			var stem = Path.GetFileNameWithoutExtension(LeafName(name));
			var safe = new string(stem.ToLowerInvariant()
				.Select(character => (character is >= 'a' and <= 'z' or >= '0' and <= '9') ? character : '-')
				.ToArray()).Trim('-');
			return string.IsNullOrEmpty(safe) ? "map" : safe[..Math.Min(safe.Length, 48)];
		}
	}
}
