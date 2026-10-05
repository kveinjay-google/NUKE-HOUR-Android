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
using NUnit.Framework;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class RetailCampaignRuntimeContractTest
	{
		static string RepositoryRoot()
		{
			var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
			while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, "OpenRA.Mods.RA2")))
				directory = directory.Parent;
			return directory?.FullName ?? throw new InvalidOperationException("Could not locate repository root.");
		}

		[Test]
		public void SuccessfulMissionCompletionPersistsCampaignProgress()
		{
			var runtime = File.ReadAllText(Path.Combine(
				RepositoryRoot(), "OpenRA.Mods.RA2", "Traits", "RetailCampaignRuntime.cs"));

			StringAssert.Contains("CampaignProgress.RecordCompleted(info.Mission);", runtime);
		}

		[Test]
		public void EveryRetailCampaignMissionHasEnglishAndChineseNames()
		{
			var root = RepositoryRoot();
			var english = File.ReadAllText(Path.Combine(root, "mods", "ra2", "fluent", "chrome.ftl"));
			var chinese = File.ReadAllText(Path.Combine(root, "mods", "ra2", "fluent", "zh-CN", "chrome.ftl"));

			foreach (var faction in new[] { "allied", "soviet" })
				for (var mission = 1; mission <= 12; mission++)
				{
					var key = $"mission-{faction}-{mission:00}-title =";
					StringAssert.Contains(key, english);
					StringAssert.Contains(key, chinese);
				}
		}

		[Test]
		public void CachedMissionsMustContainBrowserAndProgressionMetadata()
		{
			var installer = File.ReadAllText(Path.Combine(
				RepositoryRoot(), "OpenRA.Mods.RA2", "Content", "RetailCampaignInstaller.cs"));

			StringAssert.Contains("MissionId: {missionId}", installer);
			StringAssert.Contains("BrowserTitle: mission-", installer);
		}

		[Test]
		public void ConvertedCampaignMissionsGenerateAndRequireAThumbnail()
		{
			var root = RepositoryRoot();
			var importer = File.ReadAllText(Path.Combine(
				root, "OpenRA.Mods.RA2", "UtilityCommands", "ImportRA2MapCommand.cs"));
			var installer = File.ReadAllText(Path.Combine(
				root, "OpenRA.Mods.RA2", "Content", "RetailCampaignInstaller.cs"));

			StringAssert.DoesNotContain("LockPreview = mission != null", importer);
			StringAssert.Contains("map.png is missing", installer);
		}

		[Test]
		public void AlliedFirstMissionCacheRequiresItsStagedPlayerCharacter()
		{
			var installer = File.ReadAllText(Path.Combine(
				RepositoryRoot(), "OpenRA.Mods.RA2", "Content", "RetailCampaignInstaller.cs"));

			StringAssert.Contains("Infantry@0: tany", installer);
			StringAssert.Contains("Owner: Player House", installer);
			StringAssert.Contains("Location: 61,58", installer);
		}

		[Test]
		public void AlliedFirstMissionUsesSequencedFullTransportTeamsAndNonDestructivePresentationActions()
		{
			var runtime = File.ReadAllText(Path.Combine(
				RepositoryRoot(), "OpenRA.Mods.RA2", "Traits", "RetailCampaignRuntime.cs"));

			StringAssert.Contains("CreateFullTransportTeam", runtime);
			StringAssert.Contains("TickTeamScripts();", runtime);
			StringAssert.Contains("case 115:", runtime);
			StringAssert.Contains("cameo={p2}; duration={p7}", runtime);
			StringAssert.Contains("case 117:", runtime);
			StringAssert.Contains("PlayRetailMovie(p2, paused: false);", runtime);
			StringAssert.Contains("PlayRetailMovie(p2, paused: true);", runtime);
			StringAssert.Contains("ResolveRetailMovieFile", runtime);
			StringAssert.Contains("VideoPlayerWidget", runtime);
			StringAssert.DoesNotContain("case 115:\n\t\t\t\t\tDestroyAttached", runtime);
			StringAssert.DoesNotContain("case 117:\n\t\t\t\t\tSetRelationship", runtime);
		}

		[Test]
		public void AlliedFirstMissionOpeningAuditRequiresTanyaFourDreadnoughtsAndUnlockedInput()
		{
			var runtime = File.ReadAllText(Path.Combine(
				RepositoryRoot(), "OpenRA.Mods.RA2", "Traits", "RetailCampaignRuntime.cs"));

			StringAssert.Contains("OPENRA_RA2_CAMPAIGN_OPENING_AUDIT", runtime);
			StringAssert.Contains("Campaign opening audit passed", runtime);
			StringAssert.Contains("094F68EC", runtime);
			StringAssert.Contains("0A67F6CC", runtime);
			StringAssert.Contains("expected four opening dreadnought instances", runtime);
			StringAssert.Contains("input remained locked", runtime);
		}

		[Test]
		public void CampaignRuntimeConsolidatesPlayerControlHousesAndRestoresRetailStartingCash()
		{
			var runtime = File.ReadAllText(Path.Combine(
				RepositoryRoot(), "OpenRA.Mods.RA2", "Traits", "RetailCampaignRuntime.cs"));

			StringAssert.Contains("ApplyRetailPlayerControlDomain", runtime);
			StringAssert.Contains("PlayerControl", runtime);
			StringAssert.Contains("ApplyRetailStartingCash", runtime);
			StringAssert.Contains("Ra2MissionRuntimeSemantics.StartingCash", runtime);
			StringAssert.Contains("OPENRA_RA2_CAMPAIGN_ACCESS_AUDIT", runtime);
			StringAssert.Contains("Campaign access audit passed", runtime);
		}

		[Test]
		public void CampaignRuntimeUsesTheLocalPlayerAndLocalizedMissionObjective()
		{
			var runtime = File.ReadAllText(Path.Combine(
				RepositoryRoot(), "OpenRA.Mods.RA2", "Traits", "RetailCampaignRuntime.cs"));

			StringAssert.Contains("w.LocalPlayer ??", runtime);
			StringAssert.Contains("FluentProvider.GetMessage($\"mission-{info.Mission}-objectives\")", runtime);
			StringAssert.DoesNotContain("Complete the mission objectives.", runtime);
		}

		[Test]
		public void AlliedFirstMissionMakesTanyaImmediatelyVisibleAndControllable()
		{
			var runtime = File.ReadAllText(Path.Combine(
				RepositoryRoot(), "OpenRA.Mods.RA2", "Traits", "RetailCampaignRuntime.cs"));

			StringAssert.Contains("EnsureAlliedFirstMissionPlayerAccess", runtime);
			StringAssert.Contains("IsPlayerControllableActor", runtime);
			StringAssert.Contains("KeepPlayerInputAvailableDuringOpening", runtime);
			StringAssert.Contains("world.Selection.Combine(world, new[] { tanya }", runtime);
		}

		[Test]
		public void AlliedFirstMissionShowsItsFirstObjectiveOnTheFirstPlayableTick()
		{
			var runtime = File.ReadAllText(Path.Combine(
				RepositoryRoot(), "OpenRA.Mods.RA2", "Traits", "RetailCampaignRuntime.cs"));

			StringAssert.Contains("ShowInitialMissionObjective();", runtime);
			StringAssert.Contains("DisplayMissionText(\"MISSION:ALL01A\")", runtime);
			StringAssert.Contains("displayedMissionTextKeys.Add(key)", runtime);
		}

		[Test]
		public void CampaignRuntimeCanAuditEveryPlayerTeamDeliveryContract()
		{
			var runtime = File.ReadAllText(Path.Combine(
				RepositoryRoot(), "OpenRA.Mods.RA2", "Traits", "RetailCampaignRuntime.cs"));

			StringAssert.Contains("OPENRA_RA2_CAMPAIGN_TEAM_AUDIT", runtime);
			StringAssert.Contains("StartPlayerTeamAudit", runtime);
			StringAssert.Contains("Campaign player-team audit passed", runtime);
		}

		[Test]
		public void AlliedFirstMissionObjectiveAuditUsesTheRetailVictoryChain()
		{
			var runtime = File.ReadAllText(Path.Combine(
				RepositoryRoot(), "OpenRA.Mods.RA2", "Traits", "RetailCampaignRuntime.cs"));

			StringAssert.Contains("OPENRA_RA2_CAMPAIGN_OBJECTIVE_AUDIT", runtime);
			StringAssert.Contains("DestroyAlliedFirstMissionObjectiveTargets", runtime);
			StringAssert.Contains("Campaign objective audit passed", runtime);
			StringAssert.Contains("objective audit reached a failure outcome", runtime);
			StringAssert.Contains("firedTriggers.Contains(objectiveAuditWinningTrigger)", runtime);
		}

		[Test]
		public void MissingRetailSpeechNotificationFallsBackWithoutCrashingTheMission()
		{
			var runtime = File.ReadAllText(Path.Combine(
				RepositoryRoot(), "OpenRA.Mods.RA2", "Traits", "RetailCampaignRuntime.cs"));

			StringAssert.Contains("catch (InvalidOperationException ex)", runtime);
			StringAssert.Contains("notification pool does not contain", runtime);
			StringAssert.Contains("PlayRetailAudio(key, null);", runtime);
		}
	}
}
