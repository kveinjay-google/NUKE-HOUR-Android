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
using NUnit.Framework;
using OpenRA.MobileUi;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Mods.Common.Widgets.Logic;
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Test
{
	[TestFixture]
	[NonParallelizable]
	public class AndroidLobbyResponsiveProfileTests
	{
		[TestCase(800f, 360f, MobileLayoutProfile.CompactPhoneLandscape)]
		[TestCase(891f, 411f, MobileLayoutProfile.PhoneLandscape)]
		[TestCase(960f, 432f, MobileLayoutProfile.FoldableLandscape)]
		[TestCase(960f, 540f, MobileLayoutProfile.FoldableLandscape)]
		[TestCase(1280f, 800f, MobileLayoutProfile.TabletLandscape)]
		[TestCase(2000f, 1200f, MobileLayoutProfile.TabletLandscape)]
		public void ProfileBucketsMatchTheContract(float w, float h, MobileLayoutProfile expected)
		{
			Assert.That(MobileLayoutProfileResolver.Resolve(w, h), Is.EqualTo(expected));
		}

		[TestCase(800f, 360f, true)]
		[TestCase(891f, 411f, true)]
		[TestCase(960f, 540f, true)]
		[TestCase(1280f, 800f, false)]
		public void TabletDoesNotUsePhoneTouchMode(float w, float h, bool isPhoneTouch)
		{
			// PhoneTouchMode must stay false for tablets even with the mobile
			// service enabled - the tablet keeps the desktop lobby/dropdowns.
			MobileUiService.Instance.IsEnabledMobile = true;
			MobileUiService.Instance.Update(
				2000, 1200, 2000, 1200, 1000, 600, 2f, 1f, 60f,
				w, h, MobileUiSizePreference.Standard100, 0, 0, 0, 0);
			TestContext.Progress.WriteLine($"profile={MobileUiService.Instance.Profile} enabled={MobileUiService.Instance.IsEnabledMobile} phoneTouch={PhoneInput.PhoneTouchMode} expect={isPhoneTouch}");
			Assert.That(PhoneInput.PhoneTouchMode, Is.EqualTo(isPhoneTouch));
		}
	}

	[TestFixture]
	public class AndroidPhoneLobbyGeometryTests
	{
		static string RepositoryRoot()
		{
			var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
			while (dir != null)
			{
				if (File.Exists(Path.Combine(dir.FullName, "OpenRA.Mods.RA2.sln")))
					return dir.FullName;

				dir = dir.Parent;
			}

			return null;
		}

		[Test]
		public void IngamePauseMenuUsesThePhoneActionGrid()
		{
			var layout = AndroidPauseMenuLayout.Compute(
				new Rectangle(12, 20, 840, 390), 14, 10, 60, 7);

			Assert.That(layout.ButtonBounds, Has.Length.EqualTo(7));
			Assert.That(layout.ButtonBounds[0].X, Is.LessThan(layout.ButtonBounds[1].X));
			Assert.That(layout.ButtonBounds[0].Y, Is.EqualTo(layout.ButtonBounds[1].Y));
			Assert.That(layout.ButtonBounds[0].Right, Is.LessThanOrEqualTo(layout.InfoBounds.X));
			Assert.That(layout.ButtonBounds[6].X, Is.EqualTo(layout.ButtonBounds[0].X));
			Assert.That(layout.ButtonBounds[6].Right, Is.EqualTo(layout.ButtonBounds[1].Right),
				"an odd final action should span the full row instead of leaving a dead half-row");
			Assert.That(layout.InfoBounds.Width, Is.GreaterThan(layout.ButtonBounds[6].Width),
				"the three detail tabs and their controls must be the primary phone workspace");
			Assert.That(layout.ButtonBounds[6].Bottom, Is.LessThanOrEqualTo(layout.ShellBounds.Height - 14));
			Assert.That(layout.InfoBounds.Right, Is.LessThanOrEqualTo(layout.ShellBounds.Width - 14));

			var compact = AndroidPauseMenuLayout.Compute(
				new Rectangle(0, 0, 800, 360), 14, 10, 52, 8);
			Assert.That(compact.ButtonBounds[7].Bottom, Is.LessThanOrEqualTo(compact.ShellBounds.Height - 14));
		}

		[Test]
		public void PauseActionsNeverShrinkBelowTheDetailTouchHeight()
		{
			var menu = AndroidPauseMenuLayout.Compute(
				new Rectangle(0, 0, 840, 390), 14, 10, 60, 7);
			var details = AndroidGameInfoTouchLayout.ComputeGrid(620, 8, 6, 60, 2, 6, 24);

			Assert.That(menu.ButtonBounds.All(b => b.Height >= details[0].Height), Is.True);
		}

		[Test]
		public void PauseActionRowsFillTheSameVerticalRegionAsTheDetailPane()
		{
			const int padding = 14;
			const int gap = 10;
			const int touchHeight = 60;
			var menu = AndroidPauseMenuLayout.Compute(
				new Rectangle(0, 0, 840, 390), padding, gap, touchHeight, 7);
			var details = AndroidGameInfoLayout.Compute(
				menu.InfoBounds.Width, menu.InfoBounds.Height, gap, gap / 2, touchHeight, 3);

			Assert.That(menu.ButtonBounds[0].Y,
				Is.EqualTo(menu.InfoBounds.Y + details.TabBounds[0].Y));
			Assert.That(menu.ButtonBounds[^1].Bottom,
				Is.EqualTo(menu.InfoBounds.Y + details.PanelBounds.Bottom));
			Assert.That(menu.ButtonBounds[0].Height, Is.EqualTo(menu.ButtonBounds[1].Height));
			Assert.That(menu.ButtonBounds[2].Y, Is.EqualTo(menu.ButtonBounds[0].Bottom + gap));
		}

		[Test]
		public void PhoneConfirmationUsesTheDialogWidthForContentAndActions()
		{
			var screen = new IosScreenSnapshot(new Size(844, 390), new Size(844, 390), default);
			var layout = IosTouchDialogLayout.CreateConfirmation(screen, 1, 2, false);
			Assert.That(layout.Panel.Width, Is.GreaterThan(844 / 2));
			Assert.That(layout.PromptText.Width, Is.EqualTo(layout.Title.Width));
			Assert.That(layout.ButtonBounds(0).Height, Is.GreaterThanOrEqualTo(48));
			Assert.That(layout.ButtonBounds(0).Right, Is.LessThanOrEqualTo(layout.ButtonBounds(1).Left));
			Assert.That(layout.ButtonBounds(1).Right, Is.EqualTo(layout.Footer.Right));
			Assert.That(layout.Panel.Right, Is.LessThanOrEqualTo(screen.SafeBounds.Right));
		}

		[Test]
		public void PhoneGameInfoTabsFillTheirColumnWithoutOverflowing()
		{
			var type = typeof(IngameMenuLogic).Assembly.GetType(
				"OpenRA.Mods.Common.Widgets.Logic.AndroidGameInfoLayout");
			Assert.That(type, Is.Not.Null, "the phone game-info panel needs its own responsive layout");

			var compute = type.GetMethod("Compute");
			var layout = compute.Invoke(null, new object[] { 420, 360, 12, 8, 52, 3 });
			var tabs = (WidgetBounds[])type.GetProperty("TabBounds").GetValue(layout);
			var panel = (WidgetBounds)type.GetProperty("PanelBounds").GetValue(layout);

			Assert.That(tabs, Has.Length.EqualTo(3));
			Assert.That(tabs[0].Height, Is.GreaterThanOrEqualTo(52));
			Assert.That(tabs[0].X, Is.LessThan(tabs[1].X));
			Assert.That(tabs[2].Right, Is.LessThanOrEqualTo(420 - 12));
			Assert.That(panel.Y, Is.GreaterThan(tabs[0].Bottom));
			Assert.That(panel.Bottom, Is.LessThanOrEqualTo(360 - 12));
		}

		[Test]
		public void PhoneStatsColumnsFitInsideTheAvailableInfoPanel()
		{
			var columns = IngameCommandLayout.StatsColumns(380, 48);
			Assert.That(columns, Has.Length.EqualTo(4));
			Assert.That(columns[0].X, Is.EqualTo(0));
			Assert.That(columns[3].Right, Is.EqualTo(380));
			Assert.That(columns.Zip(columns.Skip(1), (left, right) => left.Right <= right.X), Is.All.True);
			Assert.That(columns.All(column => column.Height == 48), Is.True);
		}

		[Test]
		public void PhoneDetailControlsUseAReusableTouchGrid()
		{
			var type = typeof(IngameMenuLogic).Assembly.GetType(
				"OpenRA.Mods.Common.Widgets.Logic.AndroidGameInfoTouchLayout");
			Assert.That(type, Is.Not.Null, "all three detail tabs need the same touch-target policy");

			var computeGrid = type.GetMethod("ComputeGrid");
			var bounds = (WidgetBounds[])computeGrid.Invoke(null,
				new object[] { 620, 8, 6, 48, 3, 7, 24 });

			Assert.That(bounds, Has.Length.EqualTo(7));
			Assert.That(bounds.All(b => b.Height >= 48), Is.True);
			Assert.That(bounds.All(b => b.X >= 8 && b.Right <= 612), Is.True);
			Assert.That(bounds[0].Right, Is.LessThanOrEqualTo(bounds[1].X));
			Assert.That(bounds[2].Bottom, Is.LessThanOrEqualTo(bounds[3].Y));
		}

		static string ReadCommandLayout(string root) => File.ReadAllText(Path.Combine(root,
			"engine/OpenRA.Mods.Common/Widgets/Logic/Ingame/IngameCommandLayout.cs"));

		[Test]
		public void PhoneGameInfoRestylesEveryDetailTab()
		{
			var root = RepositoryRoot();
			Assert.That(root, Is.Not.Null);
			var source = ReadCommandLayout(root);
			foreach (var panel in new[] { "DEBUG_PANEL", "OBJECTIVES_PANEL", "PLAYER_LIST", "MAP_DESCRIPTION_PANEL" })
				Assert.That(source, Does.Contain(panel));
			Assert.That(source, Does.Contain("scroll.EnableContentDragging = true"));
			var menu = File.ReadAllText(Path.Combine(root, "engine/OpenRA.Mods.Common/Widgets/Logic/Ingame/IngameMenuLogic.cs"));
			Assert.That(menu, Does.Contain("commandLayout.Apply(menu, modData)"));
		}

		[Test]
		public void PhoneDebugPanelUsesItsOwnFingerScrollArea()
		{
			var source = ReadCommandLayout(RepositoryRoot());
			Assert.That(source, Does.Contain("COMMAND_DEBUG_SCROLL"));
			Assert.That(source, Does.Contain("new ScrollPanelWidget(modData)"));
			Assert.That(source, Does.Contain("scroll.EnableContentDragging = true"));
			Assert.That(source, Does.Contain("scroll.ContentHeight = content.Bounds.Bottom + gap"));
		}

		[Test]
		public void PhoneActionRailUsesTheCompactTouchHeight()
		{
			var layout = new IngameCommandLayout(new IosScreenSnapshot(new Size(844, 390), new Size(844, 390), default));
			Assert.That(layout.ActionHeight, Is.EqualTo(56));
			Assert.That(layout.TabHeight, Is.EqualTo(48));
			Assert.That(layout.Navigation.Right, Is.LessThan(layout.Information.X));
			Assert.That(layout.Information.Right, Is.LessThanOrEqualTo(layout.Content.Right));
		}

		[Test]
		public void PhoneLobbyDropdownItemsUseTouchHeight()
		{
			var root = RepositoryRoot();
			Assert.That(root, Is.Not.Null);
			var source = File.ReadAllText(Path.Combine(root, "engine/OpenRA.Mods.Common/Widgets/DropDownButtonWidget.cs"));
			Assert.That(source, Does.Contain("if (Platform.UsesMobileLayout)"));
			Assert.That(source, Does.Contain("AdaptIosDropDownItem(item, panel, snapshot, false"));
			Assert.That(source, Does.Contain("IosTouchWidgetPolicy.EnsureMinimumTouchHeight"));
		}

		[TestCase(800f, 360f)]
		[TestCase(891f, 411f)]
		[TestCase(1158f, 411f)]
		[TestCase(1280f, 800f)]
		public void LobbyRegionsKeepTheContract(float usableW, float usableH)
		{
			foreach (var factor in new[] { 1f, 1.6f, 2.1875f })
			{
				var l = AndroidLobbyLayout.ComputeLayout(usableW, usableH, factor);
				Assert.That(l.HeaderHeight / factor, Is.InRange(48f - 0.01f, 56.01f));
				Assert.That(l.TabHeight / factor, Is.InRange(48f - 0.01f, 52.01f));
				var footerDp = (l.ContentHeight - l.FooterY) / factor;
				Assert.That(footerDp, Is.InRange(56f - 0.01f, 64.01f));
				var playerFrac = l.PlayerWidth / (float)l.ContentWidth;
				Assert.That(playerFrac, Is.InRange(0.62f - 0.001f, 0.66f + 0.001f));
				Assert.That(l.MapX + l.MapWidth, Is.LessThanOrEqualTo(l.ContentWidth));
				Assert.That(l.FooterY, Is.GreaterThan(l.TabY + l.TabHeight));
			}
		}

		[TestCase(1, 891f, 411f)]
		[TestCase(2, 891f, 411f)]
		[TestCase(4, 891f, 411f)]
		[TestCase(6, 891f, 411f)]
		[TestCase(8, 891f, 411f)]
		public void ManyPlayersScrollWithoutLosingTheLastCard(int players, float usableW, float usableH)
		{
			var l = AndroidLobbyLayout.ComputeLayout(usableW, usableH, 2.1875f);
			var listH = l.MainHeight; // players column main height
			var contentH = 2 * l.CardGap + players * l.CardRowHeight + (players - 1) * l.CardGap;
			var bottom = Math.Min(0, listH - contentH);
			var lastTop = l.MainY + bottom + l.CardGap + (players - 1) * (l.CardRowHeight + l.CardGap);
			Assert.That(lastTop + l.CardRowHeight, Is.LessThanOrEqualTo(l.MainY + listH + l.CardGap),
				"with many players the last card must still be reachable via scroll");
		}
	}

	[TestFixture]
	public class AndroidPlayerCardLayoutTests
	{
		[TestCase(180, 96, 8)]
		[TestCase(320, 120, 12)]
		public void MobileCaptionsAreVerticallyCenteredAndLeaveRoomForValues(
			int width, int height, int padding)
		{
			var cell = new AndroidLobbyLayout.PlayerCell(20, 40, width, height);
			var caption = AndroidLobbyLayout.MobileCaptionBounds(cell, padding);

			Assert.That(caption.Y, Is.EqualTo(cell.Y));
			Assert.That(caption.Height, Is.EqualTo(cell.Height));
			Assert.That(caption.X, Is.EqualTo(cell.X + padding));
			Assert.That(caption.Right, Is.LessThan(cell.Right - height),
				"the caption must leave a clear value and dropdown-marker region");
		}

		[TestCase(1f)]
		[TestCase(1.6f)]
		[TestCase(2.1875f)]
		[TestCase(3f)]
		public void TwoLineCardCellsNeverOverlapOrShrinkBelowTouchMin(float factor)
		{
			foreach (var widthDp in new[] { 800f, 891f, 960f, 1158f })
			{
				var l = AndroidLobbyLayout.ComputeLayout(widthDp, 411f, factor);
				Assert.That(l.NameCell.Right, Is.LessThanOrEqualTo(l.ColorCell.X), "name must end before color");
				Assert.That(l.ColorCell.Right, Is.LessThanOrEqualTo(l.ReadyCell.X), "color must end before ready");
				Assert.That(l.FactionCell.Right, Is.LessThanOrEqualTo(l.SpawnCell.X));
				Assert.That(l.SpawnCell.Right, Is.LessThanOrEqualTo(l.TeamCell.X));

				var min = 48f * factor - 1;
				Assert.That(l.NameCell.Width, Is.GreaterThanOrEqualTo(min));
				Assert.That(l.ColorCell.Width, Is.GreaterThanOrEqualTo(min));
				Assert.That(l.ReadyCell.Width, Is.GreaterThanOrEqualTo(min));
				Assert.That(l.FactionCell.Width, Is.GreaterThanOrEqualTo(min));
				Assert.That(l.TeamCell.Width, Is.GreaterThanOrEqualTo(min));
				Assert.That(l.SpawnCell.Width, Is.GreaterThanOrEqualTo(min));

				// Two lines fill the card (each ~44-52dp tall).
				Assert.That(l.Line1Height, Is.GreaterThanOrEqualTo(44f * factor - 1));
				Assert.That(l.Line1Height, Is.LessThanOrEqualTo(48f * factor + 1));
				Assert.That(l.CardRowHeight - l.Line1Height, Is.GreaterThanOrEqualTo(44f * factor - 1));
			}
		}
	}

	[TestFixture]
	public class AndroidLobbyWidgetMappingTests
	{
		static string RepositoryRoot()
		{
			var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
			while (dir != null)
			{
				if (File.Exists(Path.Combine(dir.FullName, "OpenRA.Mods.RA2.sln")))
					return dir.FullName;

				dir = dir.Parent;
			}

			return null;
		}

		static string Read(string root, params string[] segments)
		{
			var path = Path.Combine(new[] { root }.Concat(segments).ToArray());
			return File.Exists(path) ? File.ReadAllText(path) : null;
		}

		[Test]
		public void CommonLobbyChromeDefinesTheMappedShell()
		{
			var root = RepositoryRoot();
			Assume.That(root, Is.Not.Null, "source repo not reachable from test dir");

			var shell = Read(root, "engine", "mods", "common", "chrome", "lobby.yaml");
			Assume.That(shell, Is.Not.Null);
			foreach (var id in new[]
			{
				"SERVER_NAME", "MAP_PREVIEW_ROOT", "SKIRMISH_TABS", "MULTIPLAYER_TABS",
				"TOP_PANELS_ROOT", "CHANGEMAP_BUTTON", "LOBBYCHAT", "START_GAME_BUTTON",
				"DISCONNECT_BUTTON", "SLOTS_DROPDOWNBUTTON", "RESET_OPTIONS_BUTTON"
			})
				Assert.That(shell, Does.Contain(id + ":"), "missing lobby shell node " + id);
		}

		[Test]
		public void AndroidSettingsExposeSmallMediumLargeMapViewPresets()
		{
			var root = RepositoryRoot();
			Assume.That(root, Is.Not.Null, "source repo not reachable from test dir");

			var chrome = Read(root, "engine", "mods", "common", "fluent", "zh-CN", "chrome.ftl");
			var options = Read(root, "engine", "mods", "common", "fluent", "zh-CN", "common.ftl");
			Assert.That(chrome, Does.Contain("label-battlefield-camera-dropdown = 战场镜头："));
			Assert.That(options, Does.Contain(".close = 近"));
			Assert.That(options, Does.Contain(".medium = 中"));
			Assert.That(options, Does.Contain(".far = 远"));
		}

		[Test]
		public void AndroidSettingsKeepTheVirtualJoystickSizeControlVisible()
		{
			var root = RepositoryRoot();
			Assume.That(root, Is.Not.Null, "source repo not reachable from test dir");

			var settings = Read(root, "engine", "OpenRA.Mods.Common", "Widgets", "Logic", "Settings", "SettingsLogic.cs");
			Assert.That(settings, Does.Contain("ApplyIosVisibility(panel)"));
		}

		[Test]
		public void PlayerRowTemplatesExposeTheCardFieldIds()
		{
			var root = RepositoryRoot();
			Assume.That(root, Is.Not.Null);
			var players = Read(root, "engine", "mods", "common", "chrome", "lobby-players.yaml");
			Assume.That(players, Is.Not.Null);
			Assert.That(players, Does.Contain("TEMPLATE_EDITABLE_PLAYER"));
			Assert.That(players, Does.Contain("TEMPLATE_NONEDITABLE_PLAYER"));
			Assert.That(players, Does.Contain("TEMPLATE_EMPTY"));
			foreach (var field in new[]
			{
				"NAME", "SLOT_OPTIONS", "COLOR", "COLORBLOCK", "FACTION", "TEAM_DROPDOWN",
				"HANDICAP_DROPDOWN", "SPAWN_DROPDOWN", "STATUS_CHECKBOX", "JOIN"
			})
				Assert.That(players, Does.Contain(field + ":"), "missing row field " + field);
			Assert.That(players, Does.Contain("FACTION_DROPDOWN_TEMPLATE"));
		}

		[Test]
		public void DropDownTemplatesExistForPhoneInterception()
		{
			var root = RepositoryRoot();
			Assume.That(root, Is.Not.Null);
			var dropdowns = Read(root, "engine", "mods", "common", "chrome", "dropdowns.yaml");
			Assume.That(dropdowns, Is.Not.Null);
			foreach (var id in new[] { "LABEL_DROPDOWN_TEMPLATE", "TEAM_DROPDOWN_TEMPLATE", "SPAWN_DROPDOWN_TEMPLATE" })
				Assert.That(dropdowns, Does.Contain(id + ":"), "missing dropdown template " + id);
		}

		[Test]
		public void RuntimeCodeKeepsTheMobileSeams()
		{
			var root = RepositoryRoot();
			Assert.That(root, Is.Not.Null);
			var lobby = Read(root, "engine/OpenRA.Mods.Common/Widgets/Logic/Lobby/LobbyLogic.cs");
			Assert.That(lobby, Does.Contain("ApplyIosLobbyLayout"));
			Assert.That(lobby, Does.Contain("LayoutIosPlayerRow"));
			Assert.That(lobby, Does.Contain("Platform.UsesMobileLayout"));
			var dropdown = Read(root, "engine/OpenRA.Mods.Common/Widgets/DropDownButtonWidget.cs");
			Assert.That(dropdown, Does.Contain("AdaptIosDropDownPanel"));
			Assert.That(dropdown, Does.Contain("IosTouchWidgetPolicy.PlacePopup"));
			Assert.That(dropdown, Does.Contain("IosTouchWidgetPolicy.MaximumPopupHeight"));
			var platform = Read(root, "engine/OpenRA.Game/Platform.cs");
			Assert.That(platform, Does.Contain("UsesMobileLayout => IsIOS || IsAndroid"));
		}
	}
}
