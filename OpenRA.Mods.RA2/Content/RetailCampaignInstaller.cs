#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using OpenRA.FileSystem;
using OpenRA.Mods.RA2.UtilityCommands;

namespace OpenRA.Mods.RA2.Content
{
	public sealed record RetailCampaignInstallResult(
		int Discovered,
		int Installed,
		IReadOnlyList<string> AvailableMissionIds,
		IReadOnlyList<string> Failures);

	/// <summary>
	/// Converts user-owned RA2 campaign archives into private OpenRA mission
	/// packages. Conversion is transactional per mission: an invalid replacement
	/// can never remove a previously working cached mission.
	/// </summary>
	public static class RetailCampaignInstaller
	{
		const string CacheDirectory = "nukehour-campaign-v1";

		static readonly HashSet<string> CampaignArchives = new(StringComparer.OrdinalIgnoreCase)
		{
			"maps01.mix", "maps02.mix",
		};

		public static bool IsCampaignArchive(string name)
			=> CampaignArchives.Contains(LeafName(name));

		public static string CacheIdentity(string sourceHash, int compatibilityRevision)
		{
			if (string.IsNullOrWhiteSpace(sourceHash))
				throw new ArgumentException("The source hash is required.", nameof(sourceHash));
			if (compatibilityRevision < 1)
				throw new ArgumentOutOfRangeException(nameof(compatibilityRevision));

			return $"{sourceHash}-compat-{compatibilityRevision}-bridges-v4";
		}

		public static RetailCampaignInstallResult EnsureInstalled(ModData modData, Action<int, int, string> progress = null)
		{
			if (modData == null)
				throw new ArgumentNullException(nameof(modData));

			var destinationRoot = Path.Combine(
				Platform.SupportDir, "maps", modData.Manifest.Id, CacheDirectory);
			return EnsureInstalled(modData, modData.ModFiles.MountedPackages, destinationRoot, progress);
		}

		internal static RetailCampaignInstallResult EnsureInstalled(
			ModData modData, IEnumerable<IReadOnlyPackage> packages, string destinationRoot, Action<int, int, string> progress = null)
		{
			if (modData == null)
				throw new ArgumentNullException(nameof(modData));
			if (packages == null)
				throw new ArgumentNullException(nameof(packages));
			if (string.IsNullOrWhiteSpace(destinationRoot))
				throw new ArgumentException("A campaign cache directory is required.", nameof(destinationRoot));

			Directory.CreateDirectory(destinationRoot);
			var discovered = 0;
			var installed = 0;
			var available = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			var failures = new List<string>();

			var archives = packages.Where(p => p != null && IsCampaignArchive(p.Name)).ToArray();
			var total = archives.Sum(p => RetailCampaignCatalog.Missions.Count(m => m.Archive.Equals(LeafName(p.Name), StringComparison.OrdinalIgnoreCase)));
			var completed = 0;
			progress?.Invoke(0, total, string.Empty);
			foreach (var package in archives)
			{
				var archiveName = LeafName(package.Name);
				var packageEntries = package.Contents.ToArray();
				foreach (var mission in RetailCampaignCatalog.Missions.Where(
					m => m.Archive.Equals(archiveName, StringComparison.OrdinalIgnoreCase)))
				{
					var sourceName = mission.SourceNames
						.Select(candidate => packageEntries.FirstOrDefault(
							entry => LeafName(entry).Equals(candidate, StringComparison.OrdinalIgnoreCase)))
						.FirstOrDefault(candidate => candidate != null);
					if (sourceName == null)
					{
						failures.Add($"{archiveName}:{mission.Id} (source map missing)");
						progress?.Invoke(++completed, total, mission.Id);
						continue;
					}

					discovered++;
					progress?.Invoke(completed, total, mission.Id);
					try
					{
						using var source = package.GetStream(sourceName) ??
							throw new InvalidDataException("The campaign map entry could not be opened.");
						if (InstallMission(modData, source, sourceName, mission, destinationRoot))
							installed++;
						if (IsValidMission(Path.Combine(destinationRoot, mission.Id + ".oramap"), mission.Id))
							available.Add(mission.Id);
					}
					catch (Exception e) when (
						e is IOException || e is InvalidDataException || e is ArgumentException)
					{
						failures.Add($"{archiveName}:{sourceName} ({e.GetType().Name}: {e.Message})");
						if (IsValidMission(Path.Combine(destinationRoot, mission.Id + ".oramap"), mission.Id))
							available.Add(mission.Id);
					}
					finally { progress?.Invoke(++completed, total, mission.Id); }
				}
			}

			// Count valid files from earlier sessions even when the source archives are
			// temporarily unavailable. This keeps imported campaigns usable offline.
			foreach (var mission in RetailCampaignCatalog.Missions)
				if (IsValidMission(Path.Combine(destinationRoot, mission.Id + ".oramap"), mission.Id))
					available.Add(mission.Id);

			Log.Write("debug", $"Retail campaign import: discovered={discovered}, installed={installed}, available={available.Count}, failed={failures.Count}");
			foreach (var failure in failures.Take(24))
				Log.Write("debug", $"Retail campaign import skipped {failure}");

			return new RetailCampaignInstallResult(
				discovered,
				installed,
				new ReadOnlyCollection<string>(available.OrderBy(id => id, StringComparer.Ordinal).ToArray()),
				new ReadOnlyCollection<string>(failures));
		}

		static bool InstallMission(
			ModData modData,
			Stream source,
			string sourceName,
			RetailCampaignMission mission,
			string destinationRoot)
		{
			using var buffer = new MemoryStream();
			source.CopyTo(buffer);
			var bytes = buffer.ToArray();
			var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
			var identity = CacheIdentity(hash, mission.CompatibilityRevision);
			var destination = Path.Combine(destinationRoot, mission.Id + ".oramap");
			var identityPath = Path.Combine(destinationRoot, mission.Id + ".cache");

			if (IsValidMission(destination, mission.Id) &&
				File.Exists(identityPath) &&
				File.ReadAllText(identityPath).Trim().Equals(identity, StringComparison.Ordinal))
				return false;

			var staging = destination + ".staging-" + Guid.NewGuid().ToString("N");
			var identityStaging = identityPath + ".staging-" + Guid.NewGuid().ToString("N");
			try
			{
				ImportRA2MapCommand.ConvertMissionMap(
					modData,
					new MemoryStream(bytes, writable: false),
					sourceName,
					staging,
					mission);
				if (!TryValidateMission(staging, mission.Id, out var validationError))
				{
					throw new InvalidDataException(
						$"Converted campaign mission {mission.Id} failed validation: {validationError}");
				}

				File.WriteAllText(identityStaging, identity + Environment.NewLine);
				File.Move(staging, destination, true);
				File.Move(identityStaging, identityPath, true);
				return true;
			}
			finally
			{
				if (File.Exists(staging))
					File.Delete(staging);
				if (File.Exists(identityStaging))
					File.Delete(identityStaging);
			}
		}

		static bool IsValidMission(string path, string missionId)
			=> TryValidateMission(path, missionId, out _);

		static bool TryValidateMission(string path, string missionId, out string error)
		{
			error = "unknown validation error";
			if (!File.Exists(path))
			{
				error = "package does not exist";
				return false;
			}

			try
			{
				using var archive = ZipFile.OpenRead(path);
				var entries = archive.Entries.Select(entry => entry.FullName).ToArray();
				var yamlEntry = archive.Entries.FirstOrDefault(entry =>
					entry.FullName.Equals("map.yaml", StringComparison.OrdinalIgnoreCase));
				if (yamlEntry == null)
				{
					error = "map.yaml is missing";
					return false;
				}
				if (!entries.Contains("map.bin", StringComparer.OrdinalIgnoreCase))
				{
					error = "map.bin is missing";
					return false;
				}
				if (!entries.Contains("map.png", StringComparer.OrdinalIgnoreCase))
				{
					error = "map.png is missing";
					return false;
				}
				if (!entries.Contains("mission.ini", StringComparer.OrdinalIgnoreCase))
				{
					error = "private mission control data is missing";
					return false;
				}

				var safety = PublicContentSafetyPolicy.ValidateMapPackageEntries(entries);
				if (safety != ContentSafetyViolation.None)
				{
					error = $"unsafe package entry ({safety})";
					return false;
				}

				using var reader = new StreamReader(yamlEntry.Open());
				var mapYaml = reader.ReadToEnd();
				var rulesYaml = string.Join("\n", archive.Entries
					.Where(entry => entry.FullName.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase) &&
						!entry.FullName.Equals("map.yaml", StringComparison.OrdinalIgnoreCase))
					.Select(entry =>
					{
						using var rulesReader = new StreamReader(entry.Open());
						return rulesReader.ReadToEnd();
					}));
				var runtimeYaml = mapYaml + "\n" + rulesYaml;
				var playableSlots = mapYaml.Split('\n').Count(line =>
					line.Trim().Equals("Playable: True", StringComparison.OrdinalIgnoreCase));
				if (playableSlots != 1)
				{
					error = $"expected one playable house, found {playableSlots}";
					return false;
				}
				if (!mapYaml.Contains("Visibility: MissionSelector", StringComparison.OrdinalIgnoreCase))
				{
					error = "mission visibility is missing";
					return false;
				}
				if (!runtimeYaml.Contains("RetailCampaignRuntime:", StringComparison.Ordinal))
				{
					error = "campaign runtime trait is missing";
					return false;
				}
				if (!runtimeYaml.Contains($"Mission: {missionId}", StringComparison.Ordinal))
				{
					error = "campaign mission identity is missing";
					return false;
				}
				if (!runtimeYaml.Contains($"MissionId: {missionId}", StringComparison.Ordinal))
				{
					error = "campaign browser mission identity is missing";
					return false;
				}
				if (!runtimeYaml.Contains($"BrowserTitle: mission-{missionId}-title", StringComparison.Ordinal))
				{
					error = "campaign browser title is missing";
					return false;
				}
				if (mapYaml.Contains(": mpspawn", StringComparison.OrdinalIgnoreCase))
				{
					error = "multiplayer spawn actor leaked into mission";
					return false;
				}
				if (missionId.Equals("allied-01", StringComparison.OrdinalIgnoreCase))
				{
					var stagedTanya = YamlActorBlock(mapYaml, "Infantry@0: tany");
					if (string.IsNullOrEmpty(stagedTanya) ||
						!stagedTanya.Contains("Owner: Player House", StringComparison.Ordinal) ||
						!stagedTanya.Contains("Location: 61,58", StringComparison.Ordinal))
					{
						error = "the staged Allied 01 player character was not preserved";
						return false;
					}
				}

				error = string.Empty;
				return true;
			}
			catch (InvalidDataException e)
			{
				error = e.Message;
				return false;
			}
			catch (IOException e)
			{
				error = e.Message;
				return false;
			}
		}

		static string YamlActorBlock(string yaml, string actorHeader)
		{
			var lines = yaml.Split('\n');
			var start = Array.FindIndex(lines, line =>
				line.Trim().Equals(actorHeader, StringComparison.OrdinalIgnoreCase));
			if (start < 0)
				return string.Empty;

			var end = start + 1;
			while (end < lines.Length && (string.IsNullOrWhiteSpace(lines[end]) ||
				lines[end].StartsWith("\t\t", StringComparison.Ordinal)))
				end++;
			return string.Join("\n", lines.Skip(start).Take(end - start));
		}

		static string LeafName(string path)
		{
			var normalized = (path ?? string.Empty).Replace('\\', '/');
			var separator = Math.Max(normalized.LastIndexOf('/'), normalized.LastIndexOf('|'));
			return separator >= 0 ? normalized[(separator + 1)..] : normalized;
		}
	}
}
