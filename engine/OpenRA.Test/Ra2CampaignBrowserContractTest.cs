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
	public sealed class Ra2CampaignBrowserContractTest
	{
		static string RepositoryRoot()
		{
			var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
			while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, "ios", "OpenRA.iOS")))
				directory = directory.Parent;
			return directory?.FullName ?? throw new InvalidOperationException("Could not locate repository root.");
		}

		[Test]
		public void ManifestGroupsPrivateCacheMissionsAndDoesNotEnableEmptyBrowser()
		{
			var root = RepositoryRoot();
			var manifest = File.ReadAllText(Path.Combine(root, "mods", "ra2", "mod.yaml"));
			var browser = File.ReadAllText(Path.Combine(root, "engine", "OpenRA.Mods.Common", "Widgets", "Logic", "MainMenuLogic.cs"));

			StringAssert.Contains("Missions:\n\tra2|missions/campaigns.yaml", manifest);
			StringAssert.Contains("nukehour-campaign-v1: System", manifest);
			StringAssert.Contains("missionsButton.Disabled = !hasMissions;", browser);
		}

		[Test]
		public void MissionBrowserUsesResponsiveTouchLayoutAndVisibleProgressStates()
		{
			var root = RepositoryRoot();
			var layout = File.ReadAllText(Path.Combine(root, "engine", "mods", "common", "chrome", "missionbrowser.yaml"));
			var browser = File.ReadAllText(Path.Combine(root, "engine", "OpenRA.Mods.Common", "Widgets", "Logic", "MissionBrowserLogic.cs"));

			StringAssert.Contains("Width: WINDOW_WIDTH * 9 / 10", layout);
			StringAssert.Contains("Height: WINDOW_HEIGHT * 9 / 10", layout);
			StringAssert.Contains("Height: 42", layout);
			StringAssert.Contains("Label@STATUS:", layout);
			StringAssert.Contains("CampaignProgress.IsUnlocked", browser);
			StringAssert.DoesNotContain("item.IsDisabled = () => isLocked;", browser);
			StringAssert.Contains("() => SelectMap(preview)", browser);
			StringAssert.DoesNotContain("void SelectMap(MapPreview preview)\n\t\t{\n\t\t\tif (!CanStartMission(preview))", browser);
			StringAssert.Contains("selectedMap == null || !CanStartMission(selectedMap)", browser);
			StringAssert.Contains("Path.GetFileNameWithoutExtension(p.PackageName)", browser);
			StringAssert.Contains("StringComparer.OrdinalIgnoreCase", browser);
		}

		[Test]
		public void MissionDescriptionFontWorkRunsOnTheRenderThread()
		{
			var root = RepositoryRoot();
			var browser = File.ReadAllText(Path.Combine(root, "engine", "OpenRA.Mods.Common", "Widgets", "Logic", "MissionBrowserLogic.cs"));
			var selectMapStart = browser.IndexOf("void SelectMap(MapPreview preview)", StringComparison.Ordinal);
			var rebuildOptionsStart = browser.IndexOf("void RebuildOptions()", selectMapStart, StringComparison.Ordinal);
			Assert.That(selectMapStart, Is.GreaterThanOrEqualTo(0));
			Assert.That(rebuildOptionsStart, Is.GreaterThan(selectMapStart));

			var selectMap = browser.Substring(selectMapStart, rebuildOptionsStart - selectMapStart);
			var renderThreadDispatch = selectMap.IndexOf("Game.RunAfterTick(() =>", StringComparison.Ordinal);
			var wrapText = selectMap.IndexOf("WidgetUtils.WrapText(", StringComparison.Ordinal);
			var measureText = selectMap.IndexOf("descriptionFont.Measure(", StringComparison.Ordinal);
			Assert.That(renderThreadDispatch, Is.GreaterThanOrEqualTo(0));
			Assert.That(wrapText, Is.GreaterThan(renderThreadDispatch),
				"SpriteFont wrapping must happen after dispatching back to the render thread.");
			Assert.That(measureText, Is.GreaterThan(renderThreadDispatch),
				"SpriteFont measurement must happen after dispatching back to the render thread.");
		}

		[Test]
		public void CampaignGroupsAreLocalizedInsteadOfShowingInternalIdentifiers()
		{
			var root = RepositoryRoot();
			var campaigns = File.ReadAllText(Path.Combine(root, "mods", "ra2", "missions", "campaigns.yaml"));
			var english = File.ReadAllText(Path.Combine(root, "mods", "ra2", "fluent", "chrome.ftl"));
			var chinese = File.ReadAllText(Path.Combine(root, "mods", "ra2", "fluent", "zh-CN", "chrome.ftl"));

			StringAssert.Contains("campaign-allied-title:", campaigns);
			StringAssert.Contains("campaign-soviet-title:", campaigns);
			StringAssert.Contains("campaign-allied-title =", english);
			StringAssert.Contains("campaign-soviet-title =", english);
			StringAssert.Contains("campaign-allied-title =", chinese);
			StringAssert.Contains("campaign-soviet-title =", chinese);
		}

		[Test]
		public void ImportedCampaignsUseFixedRulesAndStructuredMissionIntel()
		{
			var root = RepositoryRoot();
			var importer = File.ReadAllText(Path.Combine(root, "OpenRA.Mods.RA2", "UtilityCommands", "ImportRA2MapCommand.cs"));
			var catalog = File.ReadAllText(Path.Combine(root, "OpenRA.Mods.RA2", "Content", "RetailCampaignCatalog.cs"));
			var missionData = File.ReadAllText(Path.Combine(root, "engine", "OpenRA.Mods.Common", "Traits", "World", "MissionData.cs"));
			var browser = File.ReadAllText(Path.Combine(root, "engine", "OpenRA.Mods.Common", "Widgets", "Logic", "MissionBrowserLogic.cs"));

			StringAssert.Contains("new(\"FixedRules\", \"True\")", importer);
			foreach (var suffix in new[] { "theme", "history", "objectives", "assets", "notes" })
				StringAssert.Contains("$\"mission-{mission.Id}-" + suffix + "\"", importer);

			foreach (var field in new[] { "FixedRules", "Theme", "HistoricalBackground", "PrimaryObjectives", "StartingAssets", "OperationalNotes" })
				StringAssert.Contains("readonly " + (field == "FixedRules" ? "bool " : "string ") + field, missionData);

			StringAssert.Contains("MissionBrowserPresentationPolicy.ShowCustomOptions", browser);
			StringAssert.Contains("BuildMissionIntel", browser);
			StringAssert.Contains("CurrentCompatibilityRevision = 10", catalog,
				"Existing private mission caches must be rebuilt to receive structured intel metadata.");
		}

		[Test]
		public void EveryImportedCampaignMissionHasBilingualStructuredIntel()
		{
			var root = RepositoryRoot();
			var english = File.ReadAllText(Path.Combine(root, "mods", "ra2", "fluent", "chrome.ftl"));
			var chinese = File.ReadAllText(Path.Combine(root, "mods", "ra2", "fluent", "zh-CN", "chrome.ftl"));

			foreach (var faction in new[] { "allied", "soviet" })
				for (var mission = 1; mission <= 12; mission++)
					foreach (var suffix in new[] { "theme", "history", "objectives", "assets", "notes" })
					{
						var key = $"mission-{faction}-{mission:00}-{suffix} =";
						StringAssert.Contains(key, english, "Missing English mission intel: " + key);
						StringAssert.Contains(key, chinese, "Missing Chinese mission intel: " + key);
					}
		}
	}
}
