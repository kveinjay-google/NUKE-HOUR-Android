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

using System.IO;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using OpenRA.Mods.Common.Widgets.Logic;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class Ra2CampaignMissionNotificationContractTest
	{
		static string RepositoryRoot([CallerFilePath] string sourceFile = "")
		{
			var directory = new FileInfo(sourceFile).Directory;
			while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, "OpenRA.Mods.RA2")))
				directory = directory.Parent;

			return directory?.FullName ?? throw new DirectoryNotFoundException("Could not locate repository root.");
		}

		[Test]
		public void MissionPromptsUseALargeUpperCenterHudSurface()
		{
			var root = RepositoryRoot();
			var playerChrome = File.ReadAllText(Path.Combine(root, "mods", "ra2", "chrome", "ingame-player.yaml"));
			var worldChrome = File.ReadAllText(Path.Combine(root, "mods", "ra2", "chrome", "ingame.yaml"));

			StringAssert.Contains("Container@MISSION_NOTIFICATIONS:", playerChrome);
			StringAssert.Contains("X: (WINDOW_WIDTH - 234) / 10", playerChrome);
			StringAssert.Contains("Y: 84", playerChrome);
			StringAssert.Contains("Width: (WINDOW_WIDTH - 234) * 4 / 5", playerChrome);
			StringAssert.Contains("MissionTemplate: MISSION_NOTIFICATION_LINE_TEMPLATE", playerChrome);
			StringAssert.Contains("Font: BigBold", playerChrome);
			StringAssert.Contains("Label@MISSION_TEXT:\n\t\t\t\t\tX: (WINDOW_WIDTH - 234) / 10", worldChrome);
			StringAssert.Contains("Width: (WINDOW_WIDTH - 234) * 4 / 5", worldChrome);
			StringAssert.Contains("Height: 34", worldChrome);
			StringAssert.Contains("Font: BigBold", worldChrome);
		}

		[Test]
		public void DedicatedMissionDisplaySuppressesOnlyTheLowerOverlayCopy()
		{
			var root = RepositoryRoot();
			var missionLogicPath = Path.Combine(root, "OpenRA.Mods.RA2", "Widgets", "Logic",
				"RetailCampaignMissionNotificationsLogic.cs");
			Assert.That(File.Exists(missionLogicPath), Is.True, "The RA2 mission-only notification handler must exist.");

			var missionLogic = File.ReadAllText(missionLogicPath);
			var chatLogic = File.ReadAllText(Path.Combine(root, "engine", "OpenRA.Mods.Common", "Widgets", "Logic",
				"Ingame", "IngameChatLogic.cs"));

			StringAssert.Contains("notification.Pool != TextNotificationPool.Mission", missionLogic);
			StringAssert.Contains("MISSION_NOTIFICATIONS_DISPLAY", chatLogic);
			StringAssert.Contains("notification.Pool != TextNotificationPool.Mission", chatLogic);
			StringAssert.Contains("AddNotification(notification, chatOverlay == null);", chatLogic,
				"Mission messages must remain in the chat history and retain existing sound behavior.");
		}

		[TestCase(TextNotificationPool.Mission, true)]
		[TestCase(TextNotificationPool.System, false)]
		[TestCase(TextNotificationPool.Chat, false)]
		[TestCase(TextNotificationPool.Join, false)]
		[TestCase(TextNotificationPool.Leave, false)]
		public void CampaignChatSurfaceKeepsOnlyMissionContent(TextNotificationPool pool, bool expected)
		{
			Assert.That(IngameChatLogic.ShouldDisplayNotification(true, pool), Is.EqualTo(expected));
			Assert.That(IngameChatLogic.ShouldDisplayNotification(false, pool),
				Is.EqualTo(pool is TextNotificationPool.Chat or TextNotificationPool.System or TextNotificationPool.Mission));
		}

		[TestCase(TextNotificationPool.Transients)]
		[TestCase(TextNotificationPool.Feedback)]
		public void CampaignSuppressesGenericTransientMessages(TextNotificationPool pool)
		{
			Assert.That(IngameTransientNotificationsLogic.ShouldDisplayNotification(true, pool), Is.False);
			Assert.That(IngameTransientNotificationsLogic.ShouldDisplayNotification(false, pool), Is.True);
		}
	}
}
