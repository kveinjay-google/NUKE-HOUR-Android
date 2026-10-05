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

namespace OpenRA.Mods.RA2.Content
{
	public sealed class RetailContentCatalog
	{
		const string GeneratedStarterMap = "nukehour-starter-v1.oramap";

		static readonly string[] BaseFiles = { "ra2.mix", "language.mix" };
		static readonly string[] YuriFiles = { "ra2md.mix", "langmd.mix" };
		static readonly string[] CampaignFiles = { "maps01.mix", "maps02.mix" };
		static readonly string[] TrackedFiles =
		{
			"ra2.mix", "language.mix", "ra2md.mix", "langmd.mix", "multi.mix", "multimd.mix",
			"maps01.mix", "maps02.mix", "mapsmd03.mix",
			"movies01.mix", "movies02.mix", "movmd03.mix",
			"theme.mix", "thememd.mix", "wdt.mix",
		};

		// The retail audio.bag/audio.idx files are nested inside audio.mix, which is
		// itself nested inside language.mix (and langmd.mix for the expansion).
		// They are therefore not expected to exist as loose imported files.
		static readonly string[] AudioFiles = { "language.mix" };
		static readonly HashSet<string> MultiplayerMapArchives = new(StringComparer.OrdinalIgnoreCase)
		{
			"multi.mix", "multimd.mix",
		};
		static readonly HashSet<string> MapExtensions = new(StringComparer.OrdinalIgnoreCase)
		{
			// Legacy maps are converted into .oramap packages by RetailMapInstaller
			// before MapCache performs its initial scan.
			".map", ".mpr", ".oramap", ".yrm", ".zip",
		};

		public static IReadOnlyList<string> KnownFiles { get; } = Array.AsReadOnly(TrackedFiles);

		public RetailContentStatus Inspect(string directory, bool hasAdmittedMission = false)
		{
			if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
				return Inspect(Array.Empty<string>(), hasAdmittedMission);

			return Inspect(Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories), hasAdmittedMission);
		}

		public RetailContentStatus Inspect(IEnumerable<string> paths, bool hasAdmittedMission = false)
		{
			if (paths == null)
				throw new ArgumentNullException(nameof(paths));

			var materializedPaths = paths.Where(path => !string.IsNullOrEmpty(path)).ToArray();
			var invalidFiles = materializedPaths
				.Where(File.Exists)
				.Where(path => Path.GetExtension(path).Equals(".mix", StringComparison.OrdinalIgnoreCase))
				.Where(path => !RetailArchiveCompatibility.TryValidate(path, out _))
				.Select(Path.GetFileName)
				.ToArray();
			var validPaths = materializedPaths.Where(path =>
				!invalidFiles.Contains(Path.GetFileName(path), StringComparer.OrdinalIgnoreCase));
			return Inspect(validPaths, hasAdmittedMission, invalidFiles);
		}

		RetailContentStatus Inspect(
			IEnumerable<string> paths, bool hasAdmittedMission, IEnumerable<string> invalidFiles)
		{
			var materializedPaths = paths.Where(path => !string.IsNullOrEmpty(path)).ToArray();
			var names = new HashSet<string>(
				materializedPaths.Select(Path.GetFileName), StringComparer.OrdinalIgnoreCase);

			var missingBase = Missing(names, BaseFiles);
			var missingYuri = Missing(names, YuriFiles);
			var missingAudio = Missing(names, AudioFiles);
			var missingCampaign = Missing(names, CampaignFiles);
			var hasMap = materializedPaths.Any(path =>
				MapExtensions.Contains(Path.GetExtension(path)) &&
				!Path.GetFileName(path).Equals(GeneratedStarterMap, StringComparison.OrdinalIgnoreCase)) ||
				names.Overlaps(MultiplayerMapArchives);
			var profile = Profile(names, missingBase.Length, missingYuri.Length);

			return new RetailContentStatus(
				State(BaseFiles.Length, missingBase.Length),
				State(YuriFiles.Length, missingYuri.Length),
				State(AudioFiles.Length, missingAudio.Length),
				hasMap ? ContentState.Ready : ContentState.Missing,
				State(CampaignFiles.Length, missingCampaign.Length),
				missingBase,
				missingYuri,
				missingAudio,
				missingCampaign,
				hasAdmittedMission,
				TrackedFiles.Select(file => new RetailContentFileStatus(file, names.Contains(file))),
				profile,
				invalidFiles);
		}

		static RetailContentProfile Profile(HashSet<string> names, int missingBaseCount, int missingYuriCount)
		{
			if (TrackedFiles.All(names.Contains))
				return RetailContentProfile.SteamComplete;

			if (missingBaseCount == 0 && missingYuriCount == 0)
				return RetailContentProfile.CombinedCollection;

			return missingBaseCount == 0
				? RetailContentProfile.RedAlert2
				: RetailContentProfile.Incomplete;
		}

		static string[] Missing(HashSet<string> names, IEnumerable<string> required)
		{
			return required.Where(file => !names.Contains(file)).ToArray();
		}

		static ContentState State(int requiredCount, int missingCount)
		{
			if (missingCount == requiredCount)
				return ContentState.Missing;

			return missingCount == 0 ? ContentState.Ready : ContentState.Incomplete;
		}
	}
}
