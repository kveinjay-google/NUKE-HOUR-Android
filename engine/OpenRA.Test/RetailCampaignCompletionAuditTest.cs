#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version.
 */
#endregion

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using NUnit.Framework;
using OpenRA.Mods.Common.FileFormats;
using OpenRA.Mods.RA2.Missions;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class RetailCampaignCompletionAuditTest
	{
		static readonly string[] ExpectedMissions = Enumerable.Range(1, 12)
			.Select(number => $"allied-{number:00}")
			.Concat(Enumerable.Range(1, 12).Select(number => $"soviet-{number:00}"))
			.ToArray();

		[Test]
		public void EveryLocallyImportedMissionHasAReachableSupportedVictoryRoute()
		{
			var root = Environment.GetEnvironmentVariable("NUKE_HOUR_CAMPAIGN_AUDIT_ROOT");
			if (string.IsNullOrWhiteSpace(root))
				Assert.Ignore("Set NUKE_HOUR_CAMPAIGN_AUDIT_ROOT to audit user-imported retail campaign packages.");

			var packages = Directory.GetFiles(root, "*.oramap").ToDictionary(
				path => Path.GetFileNameWithoutExtension(path), StringComparer.OrdinalIgnoreCase);
			Assert.That(packages.Keys, Is.EquivalentTo(ExpectedMissions));

			Assert.Multiple(() =>
			{
				foreach (var missionId in ExpectedMissions)
				{
					using var archive = ZipFile.OpenRead(packages[missionId]);
					var missionBytes = ReadBytes(archive, "mission.ini");
					var mapYaml = Encoding.UTF8.GetString(ReadBytes(archive, "map.yaml"));
					var source = Ra2MissionSource.Parse(new MemoryStream(missionBytes));
					var ini = new IniFile(new MemoryStream(missionBytes));
					var player = NestedValue(mapYaml, "RetailCampaignRuntime", "Player");
					var eligibleWinners = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { player };
					var allies = NestedValue(mapYaml, $"PlayerReference@{player}", "Allies", required: false);
					foreach (var ally in allies.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
						eligibleWinners.Add(ally);

					var result = Ra2CampaignCompletionAnalyzer.Analyze(source, ini, eligibleWinners,
						Ra2MissionEventSemantics.SupportedOpcodes);
					var route = result.Routes.FirstOrDefault(candidate => candidate.ResolvedWinner != null &&
						eligibleWinners.Contains(candidate.ResolvedWinner) && candidate.UnsupportedEventOpcodes.Count == 0);
					TestContext.Progress.WriteLine($"{missionId}: player={player}; winner={route?.ResolvedWinner ?? "none"}; " +
						$"path={string.Join(" -> ", route?.TriggerPath ?? Array.Empty<string>())}");

					Assert.That(result.CanComplete, Is.True,
						$"{missionId} has no reachable supported victory route for {player} or its allies.");
					Assert.That(result.UnresolvedWinningTriggers, Is.Empty,
						$"{missionId} contains winner references that cannot be resolved.");
					Assert.That(result.UnreachableWinningTriggers, Is.Empty,
						$"{missionId} contains victory triggers that can never be enabled.");
				}
			});
		}

		static byte[] ReadBytes(ZipArchive archive, string name)
		{
			var entry = archive.GetEntry(name) ?? throw new InvalidDataException($"Package is missing {name}.");
			using var stream = entry.Open();
			using var memory = new MemoryStream();
			stream.CopyTo(memory);
			return memory.ToArray();
		}

		static string NestedValue(string yaml, string sectionName, string propertyName, bool required = true)
		{
			var lines = yaml.Replace("\r\n", "\n").Split('\n');
			for (var i = 0; i < lines.Length; i++)
			{
				if (!lines[i].Trim().Equals(sectionName + ":", StringComparison.Ordinal))
					continue;

				var sectionIndent = Indent(lines[i]);
				for (var j = i + 1; j < lines.Length &&
					(string.IsNullOrWhiteSpace(lines[j]) || Indent(lines[j]) > sectionIndent); j++)
				{
					var trimmed = lines[j].Trim();
					if (trimmed.StartsWith(propertyName + ":", StringComparison.Ordinal))
						return trimmed[(propertyName.Length + 1)..].Trim();
				}
			}

			if (required)
				throw new InvalidDataException($"Map YAML is missing {sectionName}.{propertyName}.");
			return string.Empty;
		}

		static int Indent(string line) => line.TakeWhile(char.IsWhiteSpace).Count();
	}
}
