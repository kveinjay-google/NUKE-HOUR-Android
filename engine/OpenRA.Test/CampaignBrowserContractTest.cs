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
	public sealed class CampaignBrowserContractTest
	{
		static string RepositoryRoot()
		{
			var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
			while (directory != null &&
				(!Directory.Exists(Path.Combine(directory.FullName, "mods", "ra2")) ||
				!Directory.Exists(Path.Combine(directory.FullName, "OpenRA.Mods.RA2"))))
				directory = directory.Parent;
			return directory?.FullName ?? throw new InvalidOperationException("Could not locate repository root.");
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
			StringAssert.Contains("item.IsDisabled = () => false;", browser);
			StringAssert.Contains("selectedMap == null || !CanStartMission(selectedMap)", browser);
			StringAssert.Contains("if (CanStartMission(preview))", browser);
			StringAssert.Contains("Path.GetFileNameWithoutExtension(p.PackageName)", browser);
			StringAssert.Contains("StringComparer.OrdinalIgnoreCase", browser);
		}

		[Test]
		public void RetailCampaignTitleCatalogIsLocalizedForFutureImportedMissions()
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
	}
}
