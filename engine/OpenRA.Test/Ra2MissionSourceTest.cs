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
using System.Text;
using NUnit.Framework;
using OpenRA.Mods.Common.FileFormats;
using OpenRA.Mods.RA2.Missions;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class Ra2MissionSourceTest
	{
		const string SyntheticMission = @"
[Triggers]
START=Americans,LINKED,Start trigger,1,1,1,1,0
[Events]
START=1,13,0
[Actions]
START=1,1,0,0,0,0,0,0,0
[Tags]
TAG_START=2,Start tag,START
[TaskForces]
0=TF_A
[TF_A]
Name=Test force
0=2,E1
[ScriptTypes]
0=SCRIPT_A
[SCRIPT_A]
Name=Test script
0=0,0
[TeamTypes]
0=TEAM_A
[TEAM_A]
Name=Test team
House=Americans
TaskForce=TF_A
Script=SCRIPT_A
Waypoint=AB
Full=yes
[AITriggerTypes]
AI_A=Name,TEAM_A,<none>,0,1,1,1,0,0,0,0,0,0,0,0,0,0,0
";

		[Test]
		public void ParserPreservesTypedMissionControlData()
		{
			using var stream = new MemoryStream(Encoding.UTF8.GetBytes(SyntheticMission));
			var source = Ra2MissionSource.Parse(stream);

			Assert.That(source.Triggers.Keys, Is.EquivalentTo(new[] { "START" }));
			Assert.That(source.Triggers["START"].LinkedTrigger, Is.EqualTo("LINKED"));
			Assert.That(source.Triggers["START"].Disabled, Is.True);
			Assert.That(source.Events["START"].Operations.Single().Opcode, Is.EqualTo(13));
			Assert.That(source.Actions["START"].Operations.Single().Opcode, Is.EqualTo(1));
			Assert.That(source.Tags["TAG_START"].Repeating, Is.EqualTo(2));
			Assert.That(source.TeamTypes["TEAM_A"].TaskForce, Is.EqualTo("TF_A"));
			Assert.That(source.TeamTypes["TEAM_A"].Script, Is.EqualTo("SCRIPT_A"));
			Assert.That(source.TeamTypes["TEAM_A"].Waypoint, Is.EqualTo(27));
			Assert.That(source.TeamTypes["TEAM_A"].Full, Is.True);
			Assert.That(source.TaskForces["TF_A"].Entries.Single().ActorType, Is.EqualTo("E1"));
		}

		[Test]
		public void ParserRejectsBrokenTeamReferencesWithTheOwningKey()
		{
			using var stream = new MemoryStream(Encoding.UTF8.GetBytes(
				SyntheticMission.Replace("TaskForce=TF_A", "TaskForce=MISSING")));
			var error = Assert.Throws<InvalidDataException>(() => Ra2MissionSource.Parse(stream));
			StringAssert.Contains("TEAM_A", error!.Message);
			StringAssert.Contains("MISSING", error.Message);
		}

		[TestCase("ADOG", "dog")]
		[TestCase("CTECH", "civ1")]
		[TestCase("HORV", "harv")]
		[TestCase("NAPSYA", "napsis")]
		[TestCase("NAPSYB", "napsis")]
		[TestCase("SNONITLAMP", "galite")]
		[TestCase("SENGINEER", "engineer")]
		[TestCase("E1", "e1")]
		public void CampaignOnlyActorAliasesResolveToSupportedRuntimeTypes(string source, string expected)
		{
			Assert.That(Ra2ActorTypeCatalog.Normalize(source), Is.EqualTo(expected));
		}

		[Test]
		public void CsfParserDecodesWestwoodMissionTextAndIgnoresExtraMetadata()
		{
			using var stream = new MemoryStream();
			using (var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true))
			{
				writer.Write(0x43534620u);
				writer.Write(3u);
				writer.Write(1u);
				writer.Write(1u);
				writer.Write(0u);
				writer.Write(9u);
				writer.Write(0x4C424C20u);
				writer.Write(1u);
				var label = Encoding.ASCII.GetBytes("MISSION:Objective1");
				writer.Write((uint)label.Length);
				writer.Write(label);
				writer.Write(0x53545257u);
				var encoded = Encoding.Unicode.GetBytes("摧毁所有敌军基地。");
				writer.Write((uint)(encoded.Length / 2));
				writer.Write(encoded.Select(value => (byte)~value).ToArray());
				writer.Write(4u);
				writer.Write(Encoding.ASCII.GetBytes("meta"));
			}

			stream.Position = 0;
			var table = Ra2CsfTable.Parse(stream);

			Assert.That(table.TryResolve("mission:objective1", out var text), Is.True);
			Assert.That(text, Is.EqualTo("摧毁所有敌军基地。"));
		}

		[TestCase("0", "Americans")]
		[TestCase("7", "Confederation")]
		[TestCase("13", "Player House")]
		[TestCase("22", "Player House")]
		[TestCase("Player", "Player House")]
		[TestCase("Player House", "Player House")]
		public void HouseReferencesResolveBothBuiltinAndMissionSpecificIndexes(string reference, string expected)
		{
			const string houses = @"
[Countries]
0=Player
9=Player
[Houses]
0=Americans
7=Confederation
9=Player House
13=Neutral2
[Americans]
Country=Americans
[Confederation]
Country=Confederation
[Player House]
Country=Player
[Neutral2]
Country=Neutral
";
			using var stream = new MemoryStream(Encoding.UTF8.GetBytes(houses));
			var ini = new IniFile(stream);

			Assert.That(Ra2MissionHouseReference.Resolve(ini, reference), Is.EqualTo(expected));
		}

		[TestCase("")]
		[TestCase("<none>")]
		[TestCase("99")]
		public void MissingHouseReferencesDoNotPretendToBeThePlayer(string reference)
		{
			using var stream = new MemoryStream(Encoding.UTF8.GetBytes("[Houses]\n0=Americans\n"));
			var ini = new IniFile(stream);

			Assert.That(Ra2MissionHouseReference.Resolve(ini, reference), Is.Null);
		}

		[Test]
		public void CompletionAnalyzerFindsAnEnabledPathToThePlayersWinAction()
		{
			const string mission = @"
[Countries]
0=Player
[Houses]
0=Player House
[Player House]
Country=Player
[Triggers]
START=Player House,<none>,Enable victory,0
WIN=Player House,<none>,Victory,1
[Events]
START=1,13,0,1
WIN=1,13,0,1
[Actions]
START=1,53,0,WIN,0,0,0,0,0
WIN=1,1,0,13,0,0,0,0,0
";
			using var stream = new MemoryStream(Encoding.UTF8.GetBytes(mission));
			var bytes = stream.ToArray();
			var source = Ra2MissionSource.Parse(new MemoryStream(bytes));
			var ini = new IniFile(new MemoryStream(bytes));

			var result = Ra2CampaignCompletionAnalyzer.Analyze(source, ini, "Player House", new[] { 13 });

			Assert.That(result.CanComplete, Is.True);
			Assert.That(result.Routes, Has.Count.EqualTo(1));
			Assert.That(result.Routes[0].TriggerPath, Is.EqualTo(new[] { "START", "WIN" }));
			Assert.That(result.Routes[0].ResolvedWinner, Is.EqualTo("Player House"));
			Assert.That(result.Routes[0].UnsupportedEventOpcodes, Is.Empty);
		}

		[Test]
		public void CompletionAnalyzerRejectsUnreachableOrUnsupportedWinConditions()
		{
			const string mission = @"
[Houses]
0=Americans
[Americans]
Country=Americans
[Triggers]
START=Americans,<none>,Start,0
UNSUPPORTED=Americans,<none>,Unsupported victory,0
LOCKED=Americans,<none>,Locked victory,1
[Events]
START=1,13,0,1
UNSUPPORTED=1,55,0,0
LOCKED=1,13,0,1
[Actions]
UNSUPPORTED=1,1,0,0,0,0,0,0,0
LOCKED=1,1,0,0,0,0,0,0,0
";
			var bytes = Encoding.UTF8.GetBytes(mission);
			var source = Ra2MissionSource.Parse(new MemoryStream(bytes));
			var ini = new IniFile(new MemoryStream(bytes));

			var result = Ra2CampaignCompletionAnalyzer.Analyze(source, ini, "Americans", new[] { 13 });

			Assert.That(result.CanComplete, Is.False);
			Assert.That(result.Routes.Single(route => route.WinningTrigger == "UNSUPPORTED")
				.UnsupportedEventOpcodes, Is.EqualTo(new[] { 55 }));
			Assert.That(result.UnreachableWinningTriggers, Is.EqualTo(new[] { "LOCKED" }));
		}

		[Test]
		public void CompletionAnalyzerAcceptsAnAlliedHouseAsTheWinner()
		{
			const string mission = @"
[Houses]
5=Africans
7=Confederation
[Africans]
Country=Africans
[Confederation]
Country=Confederation
[Triggers]
WIN=Africans,<none>,Allied victory,0
[Events]
WIN=1,13,0,1
[Actions]
WIN=1,1,0,7,0,0,0,0,0
";
			var bytes = Encoding.UTF8.GetBytes(mission);
			var source = Ra2MissionSource.Parse(new MemoryStream(bytes));
			var ini = new IniFile(new MemoryStream(bytes));

			var result = Ra2CampaignCompletionAnalyzer.Analyze(source, ini,
				new[] { "Africans", "Confederation" }, new[] { 13 });

			Assert.That(result.CanComplete, Is.True);
		}

		[TestCase(1, "Player House", "7", "7")]
		[TestCase(5, "Player House", "7", "7")]
		[TestCase(10, "Player House", "7", "7")]
		[TestCase(30, "Player House", "7", "7")]
		[TestCase(44, "Player House", "7", "7")]
		[TestCase(55, "Player House", "7", "7")]
		[TestCase(56, "Player House", "7", "7")]
		[TestCase(15, "Player House", "7", "Player House")]
		[TestCase(19, "Player House", "7", "Player House")]
		[TestCase(32, "Player House", "7", "Player House")]
		[TestCase(52, "Player House", "7", "Player House")]
		[TestCase(57, "Player House", "7", "Player House")]
		public void EventOwnerReferenceMatchesTheRa2ParameterContract(
			int opcode, string triggerHouse, string parameter, string expected)
		{
			Assert.That(Ra2MissionEventSemantics.OwnerReference(opcode, triggerHouse, parameter),
				Is.EqualTo(expected));
		}

		[TestCase(55)]
		[TestCase(56)]
		[TestCase(57)]
		public void Ra2CampaignRuntimeSupportsOriginalGameCompletionEvents(int opcode)
		{
			Assert.That(Ra2MissionEventSemantics.SupportedOpcodes, Does.Contain(opcode));
		}

		[Test]
		public void ObjectEventTrackerDistinguishesInfiltrationFromOwnershipChange()
		{
			var tracker = new Ra2MissionObjectEventTracker();

			tracker.RecordInfiltration(17);
			tracker.RecordOwnershipChange(23);

			Assert.That(tracker.WasInfiltrated(17), Is.True);
			Assert.That(tracker.WasCapturedOrInfiltrated(17), Is.True);
			Assert.That(tracker.WasInfiltrated(23), Is.False);
			Assert.That(tracker.WasCapturedOrInfiltrated(23), Is.True);
			Assert.That(tracker.WasCapturedOrInfiltrated(99), Is.False);
		}

		[Test]
		public void AlliedFirstMissionRuntimeContractCoversStoryAndTeamScriptOperations()
		{
			Assert.Multiple(() =>
			{
				Assert.That(Ra2MissionRuntimeSemantics.AlliedFirstMissionActionOpcodes,
					Is.EquivalentTo(new[]
					{
						1, 2, 3, 4, 5, 7, 11, 12, 14, 17, 19, 21, 32, 36, 41, 46, 47, 48,
						53, 54, 55, 63, 74, 80, 99, 100, 101, 104, 108, 111, 113, 114, 115, 116, 117,
					}));
				Assert.That(Ra2MissionRuntimeSemantics.AlliedFirstMissionScriptOpcodes,
					Is.EquivalentTo(new[] { 0, 1, 3, 5, 6, 8, 11, 19, 20, 37, 39, 46, 48, 49, 50 }));
			});
		}

		[Test]
		public void TeamScriptTimingAndLineNumbersUseTheRetailRa2Units()
		{
			Assert.Multiple(() =>
			{
				// RA2 stores Guard Area durations in tenths of a minute.
				Assert.That(Ra2MissionRuntimeSemantics.GuardDurationTicks(30, 25), Is.EqualTo(4500));
				// Script jump arguments are one-based line numbers, while our list is zero-based.
				Assert.That(Ra2MissionRuntimeSemantics.ScriptLineIndex(1), Is.Zero);
				Assert.That(Ra2MissionRuntimeSemantics.ScriptLineIndex(4), Is.EqualTo(3));
			});
		}

		[Test]
		public void RetailCampaignStartingCashUsesTheLargestPlayerControlledHouseBalance()
		{
			Assert.Multiple(() =>
			{
				Assert.That(Ra2MissionRuntimeSemantics.StartingCash(new[] { 0, 100, 20 }), Is.EqualTo(10000));
				Assert.That(Ra2MissionRuntimeSemantics.StartingCash(new[] { 1500 }), Is.EqualTo(150000));
				Assert.That(Ra2MissionRuntimeSemantics.StartingCash(Array.Empty<int>()), Is.Zero);
			});
		}

		[Test]
		public void RetailCampaignRuntimeContractCoversUnitControlAndDeliveryOperations()
		{
			Assert.Multiple(() =>
			{
				Assert.That(Ra2MissionRuntimeSemantics.CampaignActionOpcodes,
					Does.Contain(107), "Chronoshift reinforcement must deliver campaign units.");
				Assert.That(Ra2MissionRuntimeSemantics.CampaignScriptOpcodes,
					Does.Contain(9), "Deploy must let scripted MCVs become construction yards.");
				Assert.That(Ra2MissionRuntimeSemantics.CampaignScriptOpcodes,
					Does.Contain(14), "Load transport must preserve scripted insertion teams.");
				Assert.That(Ra2MissionRuntimeSemantics.CampaignScriptOpcodes,
					Does.Contain(43), "Wait for full transport must not skip the insertion handoff.");
				Assert.That(Ra2MissionRuntimeSemantics.CampaignScriptOpcodes,
					Does.Contain(58), "Move to friendly structure must preserve scripted player delivery.");
			});
		}

		[TestCase("allied-01", true)]
		[TestCase("ALLIED-01", true)]
		[TestCase("allied-02", false)]
		[TestCase("soviet-01", false)]
		public void OnlyTheLongAlliedOpeningKeepsPlayerInputAvailable(string mission, bool expected)
		{
			Assert.That(
				Ra2MissionRuntimeSemantics.KeepPlayerInputAvailableDuringOpening(mission),
				Is.EqualTo(expected));
		}

		[TestCase(33, 33, 0)]
		[TestCase(131105, 33, 2)]
		[TestCase(196641, 33, 3)]
		public void BuildingWithPropertySelectorsKeepTheirIndexAndDistanceMode(
			int selector, int expectedIndex, int expectedMode)
		{
			var decoded = Ra2MissionRuntimeSemantics.DecodeBuildingWithProperty(selector);

			Assert.That(decoded.BuildingIndex, Is.EqualTo(expectedIndex));
			Assert.That(decoded.SelectionMode, Is.EqualTo(expectedMode));
		}

		[Test]
		public void RetailMissionAudioCatalogResolvesEvaDialogueAndSoundEvents()
		{
			using var evaStream = new MemoryStream(Encoding.UTF8.GetBytes(@"
[EVA_EstablishBattlefieldControl]
Allied=ceva016
Russian=csof016
[Mis_A1_EvaProtectStatue]
Allied=ma1ev03
Russian=ma1ev03
"));
			using var soundStream = new MemoryStream(Encoding.UTF8.GetBytes(@"
[NukeSiren]
Sounds=snuksire
[_Amb_OceanHeavy]
Sounds= $awav01a $awav01b
"));
			var eva = new IniFile(evaStream);
			var sound = new IniFile(soundStream);

			Assert.Multiple(() =>
			{
				Assert.That(Ra2MissionAudioCatalog.Candidates(
					"EVA_EstablishBattlefieldControl", "Allied", eva, sound)[0], Is.EqualTo("ceva016"));
				Assert.That(Ra2MissionAudioCatalog.Candidates(
					"Mis_A1_EvaProtectStatue", "Allied", eva, sound)[0], Is.EqualTo("ma1ev03"));
				Assert.That(Ra2MissionAudioCatalog.Candidates(
					"NukeSiren", "Allied", eva, sound)[0], Is.EqualTo("snuksire"));
				Assert.That(Ra2MissionAudioCatalog.Candidates(
					"_Amb_OceanHeavy", "Allied", eva, sound), Does.Contain("awav01b"));
			});
		}
	}
}
