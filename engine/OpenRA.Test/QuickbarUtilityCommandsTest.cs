#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software. It is
 * made available under the terms of the GNU General Public License,
 * version 3 or any later version. For more information, see COPYING.
 */
#endregion

using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using OpenRA.Mods.RA2.Widgets;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class QuickbarUtilityCommandsTest
	{
		static string RepositoryRoot()
		{
			var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
			while (directory != null && (!Directory.Exists(Path.Combine(directory.FullName, "mods", "ra2")) ||
				!Directory.Exists(Path.Combine(directory.FullName, "OpenRA.Mods.RA2"))))
				directory = directory.Parent;

			Assert.That(directory, Is.Not.Null);
			return directory!.FullName;
		}

		[Test]
		public void AutoRepairAndBeaconArePermanentTouchQuickbarActions()
		{
			var defaults = CommandBarCatalog.DefaultVisibleIds().ToArray();
			var compact = (string[])typeof(CustomCommandBarWidget).GetField(
				"TouchCompactIds", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;

			Assert.Multiple(() =>
			{
				Assert.That(CommandBarLayoutPolicy.CurrentVersion, Is.EqualTo(8));
				Assert.That(defaults, Does.Contain("AUTO_REPAIR"));
				Assert.That(defaults, Does.Contain("BEACON"));
				Assert.That(compact, Has.Length.EqualTo(13));
				Assert.That(compact, Does.Contain("AUTO_REPAIR"));
				Assert.That(compact, Does.Contain("BEACON"));
				Assert.That(CommandBarLayoutPolicy.CanHide("AUTO_REPAIR", true), Is.False);
				Assert.That(CommandBarLayoutPolicy.CanHide("BEACON", true), Is.False);
			});
		}

		[Test]
		public void ExistingTouchPreferencesGainBothUtilityActionsExactlyOnce()
		{
			var upgraded = CommandBarLayoutPolicy.NormalizeVisibleOrder(
				new[] { "CTRL_TOGGLE", "GROUP_01", "REPAIR", "SELL", "STOP", "BEACON", "BEACON" },
				true, 6).ToArray();

			Assert.Multiple(() =>
			{
				Assert.That(upgraded.Count(id => id == "AUTO_REPAIR"), Is.EqualTo(1));
				Assert.That(upgraded.Count(id => id == "BEACON"), Is.EqualTo(1));
				Assert.That(upgraded, Does.Not.Contain("SELECT_ALL"));
				Assert.That(upgraded, Does.Not.Contain("QUEUE_ORDERS"));
			});
		}

		[Test]
		public void AutoRepairQuickbarSlotKeepsItsOrderBindingAndToggleState()
		{
			var root = RepositoryRoot();
			var yaml = File.ReadAllText(Path.Combine(root, "mods", "ra2", "chrome", "ingame-player.yaml"));
			var chrome = File.ReadAllText(Path.Combine(root, "mods", "ra2", "chrome.yaml"));
			var logic = File.ReadAllText(Path.Combine(root, "OpenRA.Mods.RA2", "Widgets", "Logic",
				"SelectionCommandBarLogic.cs"));
			var sidebar = File.ReadAllText(Path.Combine(root, "OpenRA.Mods.RA2", "Widgets", "HdSidebarWidget.cs"));

			StringAssert.Contains("Button@AUTO_REPAIR:", yaml);
			StringAssert.Contains("ImageName: auto-repair", yaml);
			StringAssert.Contains("BindAutoRepair(widget);", logic);
			StringAssert.Contains("new Order(\"AutoRepair\"", logic);
			StringAssert.Contains("Manager()?.Enabled == true", logic);
			StringAssert.Contains("auto-repair: 4364, 12, 104, 104", chrome);
			StringAssert.Contains("\"AUTO_REPAIR_BUTTON\", \"BEACON_BUTTON\"", sidebar);
		}
	}
}
