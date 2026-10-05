#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software. It is
 * made available under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version.
 */
#endregion

using System;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using NUnit.Framework;
using OpenRA.Mods.Common.FileFormats;
using OpenRA.Mods.RA2.Missions;
using OpenRA.Primitives;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class RetailCampaignFirstMissionTest
	{
		[Test]
		public void PrivateAlliedFirstMissionPackageKeepsTheStagedTanyaAndOpeningTeam()
		{
			var root = Environment.GetEnvironmentVariable("NUKE_HOUR_CAMPAIGN_AUDIT_ROOT");
			if (string.IsNullOrWhiteSpace(root))
				Assert.Ignore("Set NUKE_HOUR_CAMPAIGN_AUDIT_ROOT to audit user-imported retail campaign packages.");

			var path = Path.Combine(root!, "allied-01.oramap");
			Assert.That(File.Exists(path), Is.True, "The private Allied 01 package is missing.");

			using var archive = ZipFile.OpenRead(path);
			var mapYaml = Encoding.UTF8.GetString(ReadBytes(archive, "map.yaml"));
			var missionBytes = ReadBytes(archive, "mission.ini");
			var ini = new IniFile(new MemoryStream(missionBytes, writable: false));
			var source = Ra2MissionSource.Parse(new MemoryStream(missionBytes, writable: false));

			Assert.Multiple(() =>
			{
				StringAssert.Contains("Infantry@0: tany", mapYaml);
				StringAssert.Contains("Owner: Player House", Section(mapYaml, "Infantry@0"));
				StringAssert.Contains("Location: 61,58", Section(mapYaml, "Infantry@0"));

				var tanya = ini.GetSection("Infantry", true).GetValue("0", "").Split(',');
				Assert.That(tanya.ElementAtOrDefault(1), Is.EqualTo("TANY"));
				Assert.That(tanya.ElementAtOrDefault(8), Is.EqualTo("08B9B35C"));
				Assert.That(source.Tags["08B9B35C"].Trigger, Is.EqualTo("08B9B62C"));
				Assert.That(source.TeamTypes["094F68EC"].Script, Is.EqualTo("09680DBC"));
				Assert.That(source.ScriptTypes["09680DBC"].Steps.Select(step => (step.Opcode, step.Argument)),
					Is.EqualTo(new[] { (3, 36), (5, 30) }));

				var openingDreadnoughts = source.Actions["07A9578C"].Operations;
				Assert.That(openingDreadnoughts, Has.Count.EqualTo(4));
				Assert.That(openingDreadnoughts.All(action => action.Opcode == 4 &&
					action.Parameters.ElementAtOrDefault(1) == "0A67F6CC"), Is.True);

				Assert.That(source.TeamTypes["0AB436CC"].Full, Is.True);
				Assert.That(source.TeamTypes["0AB436CC"].TaskForce, Is.EqualTo("08629CDC"));
				Assert.That(source.ScriptTypes["0AC7832C"].Steps.Select(step => (step.Opcode, step.Argument)),
					Is.EqualTo(new[] { (3, 75), (8, 2), (39, 0), (3, 48), (0, 4) }));

				var actions = source.Actions.Values.SelectMany(entry => entry.Operations)
					.Select(operation => operation.Opcode).Distinct().ToArray();
				var scripts = source.ScriptTypes.Values.SelectMany(script => script.Steps)
					.Select(step => step.Opcode).Distinct().ToArray();
				Assert.That(actions, Is.SubsetOf(Ra2MissionRuntimeSemantics.AlliedFirstMissionActionOpcodes));
				Assert.That(scripts, Is.SubsetOf(Ra2MissionRuntimeSemantics.AlliedFirstMissionScriptOpcodes));
			});

			var mapSize = ini.GetSection("Map", true).GetValue("Size", "").Split(',')
				.Select(value => int.Parse(value, CultureInfo.InvariantCulture)).ToArray();
			var fullSize = new int2(mapSize[2], mapSize[3]);
			Assert.That(ini.GetSection("CellTags", true), Is.Not.Empty);
			foreach (var cellTag in ini.GetSection("CellTags", true))
			{
				var raw = Ra2MissionMapCoordinates.DecodePacked(
					int.Parse(cellTag.Key, CultureInfo.InvariantCulture));
				var mapPosition = Ra2MissionMapCoordinates.ToMapPosition(raw.X, raw.Y, fullSize);
				Assert.That(mapPosition.U, Is.InRange(0, fullSize.X - 1), $"CellTag {cellTag.Key} U");
				Assert.That(mapPosition.V, Is.InRange(0, fullSize.Y * 2 - 1), $"CellTag {cellTag.Key} V");
			}
		}

		static byte[] ReadBytes(ZipArchive archive, string name)
		{
			var entry = archive.GetEntry(name) ?? throw new InvalidDataException($"Package is missing {name}.");
			using var stream = entry.Open();
			using var memory = new MemoryStream();
			stream.CopyTo(memory);
			return memory.ToArray();
		}

		static string Section(string yaml, string name)
		{
			var marker = name + ":";
			var start = yaml.IndexOf(marker, StringComparison.Ordinal);
			if (start < 0)
				return string.Empty;

			var next = yaml.IndexOf("\n\tActor", start + marker.Length, StringComparison.Ordinal);
			return next < 0 ? yaml[start..] : yaml[start..next];
		}
	}
}
