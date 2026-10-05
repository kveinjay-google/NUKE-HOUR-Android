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
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using OpenRA.Mods.Cnc.FileFormats;
using OpenRA.Mods.Cnc.FileSystem;
using OpenRA.Mods.Common.FileFormats;
using OpenRA.Mods.RA2.Content;
using OpenRA.Mods.RA2.Missions;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class RetailCampaignArchiveProbeTest
	{
		[Test]
		public void UserOwnedCoreArchivesExposeExpectedRuntimeRoles()
		{
			Log.AddChannel("debug", null);
			var retailRoot = Environment.GetEnvironmentVariable("OPENRA_RA2_RETAIL_ROOT");
			if (string.IsNullOrWhiteSpace(retailRoot))
				Assert.Ignore("Set OPENRA_RA2_RETAIL_ROOT to probe user-owned core archives.");

			var expectedRoles = new[]
			{
				("ra2.mix", new[] { "local.mix", "conquer.mix" }),
				("language.mix", new[] { "audio.mix", "cameo.mix" }),
				("ra2md.mix", new[] { "localmd.mix", "conqmd.mix" }),
				("langmd.mix", new[] { "audiomd.mix", "cameomd.mix" }),
			};

			foreach (var (archiveName, roles) in expectedRoles)
			{
				using var package = new MixLoader.MixFile(
					File.OpenRead(Path.Combine(retailRoot!, archiveName)), archiveName, Array.Empty<string>());
				Assert.That(roles.Any(package.ContainsHash), Is.True,
					$"{archiveName} does not expose any expected runtime role.");
			}
		}

		[Test]
		public void UserOwnedRetailCollectionPassesCompatibilityAndContainsSkirmishMaps()
		{
			Log.AddChannel("debug", null);
			var retailRoot = Environment.GetEnvironmentVariable("OPENRA_RA2_RETAIL_ROOT");
			if (string.IsNullOrWhiteSpace(retailRoot))
				Assert.Ignore("Set OPENRA_RA2_RETAIL_ROOT to probe user-owned retail archives.");

			foreach (var archiveName in RetailContentCatalog.KnownFiles)
			{
				var path = Path.Combine(retailRoot!, archiveName);
				Assert.That(File.Exists(path), Is.True, $"Missing selected retail archive: {archiveName}");
				Assert.That(RetailArchiveCompatibility.TryValidate(path, out var reason), Is.True, reason);
			}

			var status = new RetailContentCatalog().Inspect(retailRoot!);
			Assert.That(status.Profile, Is.EqualTo(RetailContentProfile.SteamComplete));
			Assert.That(status.CanEnterGame, Is.True);

			var root = TestContext.CurrentContext.TestDirectory;
			while (!File.Exists(Path.Combine(root, "global mix database.dat")))
				root = Directory.GetParent(root)?.FullName ??
					throw new AssertionException("Could not locate global mix database.dat.");

			string[] names;
			using (var database = new XccGlobalDatabase(File.OpenRead(Path.Combine(root, "global mix database.dat"))))
				names = database.Entries;

			var mapCount = 0;
			foreach (var archiveName in new[] { "multi.mix", "multimd.mix" })
			{
				using var package = new MixLoader.MixFile(
					File.OpenRead(Path.Combine(retailRoot!, archiveName)), archiveName, names);
				mapCount += package.Contents.Count(name =>
					new[] { ".map", ".mpr", ".yrm" }.Contains(
						Path.GetExtension(name), StringComparer.OrdinalIgnoreCase));
			}

			Assert.That(mapCount, Is.GreaterThan(0), "No skirmish maps were resolved from the retail archives.");
		}

		[Test]
		public async Task UserOwnedRetailCollectionCompletesTheTransactionalImporter()
		{
			Log.AddChannel("debug", null);
			var retailRoot = Environment.GetEnvironmentVariable("OPENRA_RA2_RETAIL_ROOT");
			if (string.IsNullOrWhiteSpace(retailRoot))
				Assert.Ignore("Set OPENRA_RA2_RETAIL_ROOT to probe the transactional importer.");

			var output = Path.Combine(Path.GetTempPath(), "openra-retail-probe-" + Guid.NewGuid().ToString("N"));
			try
			{
				var files = Directory.EnumerateFiles(retailRoot!, "*", SearchOption.AllDirectories)
					.Where(path => PublicContentSafetyPolicy.ValidateImportFile(path) == ContentSafetyViolation.None)
					.ToArray();
				var result = await new RetailContentImporter().ImportAsync(
					new RetailImportRequest(files, output, long.MaxValue), CancellationToken.None);

				Assert.That(result.Published, Is.True, result.Message);
				Assert.That(result.Status.Profile, Is.EqualTo(RetailContentProfile.SteamComplete));
				Assert.That(result.Status.CanEnterGame, Is.True);
				Assert.That(result.Hashes.Count, Is.EqualTo(RetailContentCatalog.KnownFiles.Count));
				Assert.That(Directory.EnumerateFiles(Path.Combine(output, "ra2")).Count(),
					Is.EqualTo(RetailContentCatalog.KnownFiles.Count));
			}
			finally
			{
				if (Directory.Exists(output))
					Directory.Delete(output, true);
			}
		}

		[Test]
		public void UserOwnedArchivesCanBeInventoriedWithoutPublishingRetailBytes()
		{
			Log.AddChannel("debug", null);
			var archivePaths = Environment.GetEnvironmentVariable("OPENRA_RA2_CAMPAIGN_ARCHIVES");
			if (string.IsNullOrWhiteSpace(archivePaths))
				Assert.Ignore("Set OPENRA_RA2_CAMPAIGN_ARCHIVES to probe user-owned campaign archives.");

			var root = TestContext.CurrentContext.TestDirectory;
			while (!File.Exists(Path.Combine(root, "global mix database.dat")))
				root = Directory.GetParent(root)?.FullName ??
					throw new AssertionException("Could not locate global mix database.dat.");

			string[] names;
			using (var database = new XccGlobalDatabase(File.OpenRead(Path.Combine(root, "global mix database.dat"))))
				names = database.Entries;

			var discovered = 0;
			foreach (var archivePath in archivePaths!.Split(Path.PathSeparator))
			{
				var archiveName = Path.GetFileName(archivePath);
				using var package = new MixLoader.MixFile(File.OpenRead(archivePath), archiveName, names);
				foreach (var sourceName in package.Contents.OrderBy(n => n, StringComparer.OrdinalIgnoreCase))
				{
					var mission = RetailCampaignCatalog.Resolve(archiveName, sourceName);
					if (mission == null)
						continue;

					discovered++;
					using var source = package.GetStream(sourceName);
					var bytes = source.ReadAllBytes();
					var parsed = Ra2MissionSource.Parse(new MemoryStream(bytes, writable: false));
					var ownerIni = new IniFile(new MemoryStream(bytes, writable: false));
					var playerControlledHouses = ownerIni.GetSection("Houses", true)
						.Select(entry => entry.Value)
						.Where(house => ownerIni.GetSection(house, true).GetValue("PlayerControl", "no")
							.Equals("yes", StringComparison.OrdinalIgnoreCase))
						.ToHashSet(StringComparer.OrdinalIgnoreCase);
					foreach (var team in parsed.TeamTypes.Values)
					{
						var teamHouse = Ra2MissionHouseReference.Resolve(ownerIni, team.House);
						if (teamHouse == null || !playerControlledHouses.Contains(teamHouse))
							continue;

						Assert.That(parsed.ScriptTypes[team.Script].Steps.Select(step => step.Opcode).Distinct(),
							Is.SubsetOf(Ra2MissionRuntimeSemantics.CampaignScriptOpcodes),
							$"{mission.Id} player team {team.Key} contains an unsupported control or delivery step.");
						foreach (var entry in parsed.TaskForces[team.TaskForce].Entries)
							Assert.That(Ra2ActorTypeCatalog.Normalize(entry.ActorType), Is.Not.Empty,
								$"{mission.Id} player team {team.Key} contains an unresolved actor type.");
					}
					var mapSection = ownerIni.GetSection("Map", true);
					var owners = new[] { "Structures", "Units", "Infantry", "Aircraft" }
						.SelectMany(section => ownerIni.GetSection(section, true))
						.Select(kv => kv.Value.Split(',')[0])
						.Distinct(StringComparer.OrdinalIgnoreCase)
						.OrderBy(owner => owner, StringComparer.OrdinalIgnoreCase);
					if (mission.Id == "allied-01")
					{
						var actions = parsed.Actions.Values.SelectMany(entry => entry.Operations)
							.Select(operation => operation.Opcode).Distinct().ToArray();
						var scripts = parsed.ScriptTypes.Values.SelectMany(script => script.Steps)
							.Select(step => step.Opcode).Distinct().ToArray();
						Assert.That(actions, Is.SubsetOf(Ra2MissionRuntimeSemantics.AlliedFirstMissionActionOpcodes));
						Assert.That(scripts, Is.SubsetOf(Ra2MissionRuntimeSemantics.AlliedFirstMissionScriptOpcodes));

						var ini = new IniFile(new MemoryStream(bytes, writable: false));
						foreach (var sectionName in new[] { "Basic", "Houses", "Countries", "TaskForces", "ScriptTypes", "TeamTypes" })
							TestContext.Out.WriteLine($"RAW-{sectionName}: " + string.Join("; ",
								ini.GetSection(sectionName, true).Take(3).Select(kv => $"{kv.Key}={kv.Value}")));
						foreach (var sectionName in new[] { "Player", "Player House", "BadGuy1", "BadGuy1 House", "BadGuy2", "BadGuy2 House", "0A67F34C", "07A92B3C", "0A67F6CC" })
							TestContext.Out.WriteLine($"RAW-{sectionName}: " + string.Join("; ",
								ini.GetSection(sectionName, true).Select(kv => $"{kv.Key}={kv.Value}")));
					}
					TestContext.Out.WriteLine(
						$"{mission.Id}|{sourceName}" +
						$"|size={mapSection.GetValue("Size", "")}" +
						$"|local={mapSection.GetValue("LocalSize", "")}" +
						$"|events={string.Join(',', parsed.Events.Values.SelectMany(e => e.Operations).Select(o => o.Opcode).Distinct().OrderBy(o => o))}" +
						$"|actions={string.Join(',', parsed.Actions.Values.SelectMany(e => e.Operations).Select(o => o.Opcode).Distinct().OrderBy(o => o))}" +
						$"|scripts={string.Join(',', parsed.ScriptTypes.Values.SelectMany(s => s.Steps).Select(s => s.Opcode).Distinct().OrderBy(o => o))}" +
						$"|taskforces={parsed.TaskForces.Count}|teams={parsed.TeamTypes.Count}" +
						$"|owners={string.Join(',', owners)}");
				}
			}

			Assert.That(discovered, Is.EqualTo(24));
		}
	}
}
