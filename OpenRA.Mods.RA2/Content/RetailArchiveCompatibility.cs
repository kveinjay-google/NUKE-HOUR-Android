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
using System.Linq;
using OpenRA.Mods.Cnc.FileSystem;

namespace OpenRA.Mods.RA2.Content
{
	public static class RetailArchiveCompatibility
	{
		static readonly IReadOnlyDictionary<string, string[]> ExpectedRoles =
			new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
			{
				{ "ra2.mix", new[] { "local.mix", "conquer.mix" } },
				{ "language.mix", new[] { "audio.mix", "cameo.mix" } },
				{ "ra2md.mix", new[] { "localmd.mix", "conqmd.mix" } },
				{ "langmd.mix", new[] { "audiomd.mix", "cameomd.mix" } },
			};

		static readonly string[] CandidateFilenames = ExpectedRoles.Values
			.SelectMany(names => names)
			.Append("payload.bin")
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToArray();

		public static bool TryValidate(string path, out string reason)
		{
			reason = string.Empty;
			if (!Path.GetExtension(path).Equals(".mix", StringComparison.OrdinalIgnoreCase))
				return true;

			try
			{
				using var archive = new MixLoader.MixFile(File.OpenRead(path), Path.GetFileName(path), CandidateFilenames);
				if (archive.EntryCount == 0)
				{
					reason = $"{Path.GetFileName(path)} is an empty MIX archive.";
					return false;
				}

				if (ExpectedRoles.TryGetValue(Path.GetFileName(path), out var roles) &&
					!roles.Any(archive.ContainsHash))
				{
					reason = $"{Path.GetFileName(path)} does not contain the expected game data.";
					return false;
				}

				return true;
			}
			catch (Exception e)
			{
				reason = $"{Path.GetFileName(path)} is not a readable MIX archive: {e.Message}";
				return false;
			}
		}
	}
}
