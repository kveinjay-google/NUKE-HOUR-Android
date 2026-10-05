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
using System.Linq;
using OpenRA.Widgets;

namespace OpenRA.Mods.Common.Widgets.Logic
{
	public sealed class IosLobbyLayout
	{
		public int Gap { get; }
		public bool HidePlayerUtility { get; }
		public int PlayerRowSpacing { get; }
		public WidgetBounds Header { get; }
		public WidgetBounds Tabs { get; }
		public WidgetBounds Main { get; }
		public WidgetBounds Servers { get; }
		public WidgetBounds Players { get; }
		public WidgetBounds PlayerHeader { get; }
		public WidgetBounds PlayerList { get; }
		public WidgetBounds Map { get; }
		public WidgetBounds ChangeMap { get; }
		public WidgetBounds Position { get; }
		public WidgetBounds Chat { get; }
		public WidgetBounds ChatDisplay { get; }
		public WidgetBounds ChatInput { get; }
		public WidgetBounds Footer { get; }
		public WidgetBounds Start { get; }
		public WidgetBounds Disconnect { get; }
		public WidgetBounds PlayerRow { get; }
		public WidgetBounds PlayerName { get; }
		public WidgetBounds PlayerColor { get; }
		public WidgetBounds PlayerFaction { get; }
		public WidgetBounds PlayerTeam { get; }
		public WidgetBounds PlayerHandicap { get; }
		public WidgetBounds PlayerSpawn { get; }
		public WidgetBounds PlayerReady { get; }
		public int PlayerScrollbarWidth { get; }

		IosLobbyLayout(int width, int height, IosMenuLayoutPolicy policy, bool skirmishMode,
			bool fullWidthContent = false, bool showMap = true, bool reserveUtility = true,
			bool showHeader = true, int[] preferredColumnWidths = null, bool showPlayerScrollbar = false,
			bool hidePlayerUtility = false)
		{
			width = Math.Max(1, width);
			height = Math.Max(1, height);
			Gap = policy.Gap;
			HidePlayerUtility = hidePlayerUtility && policy.IsPhone && skirmishMode;
			PlayerRowSpacing = HidePlayerUtility ? Math.Max(1, Gap / 2) : Gap;

			var headerHeight = showHeader ? Math.Min(height, policy.HeaderHeight) : 0;
			Header = new WidgetBounds(0, 0, width, headerHeight);
			var tabsY = showHeader ? Math.Min(height, Header.Bottom + Gap) : 0;
			var tabsHeight = Math.Min(policy.MinimumTarget, Math.Max(0, height - tabsY));
			Tabs = new WidgetBounds(0, tabsY, width, tabsHeight);

			var footerHeight = Math.Min(policy.FooterHeight, height);
			var footerY = Math.Max(Tabs.Bottom + Gap, height - footerHeight);
			Footer = new WidgetBounds(0, footerY, width, Math.Max(0, height - footerY));

			var mainY = Math.Min(Footer.Y, Tabs.Bottom + Gap);
			var mainBottom = Math.Max(mainY, Footer.Y - Gap);
			var mainHeight = mainBottom - mainY;
			Main = new WidgetBounds(0, mainY, width, mainHeight);
			Servers = Main;
			var minimumChatHeight = policy.MinimumTarget + Gap + policy.MinimumReadableTextHeight;
			var preferredUtilityHeight = policy.IsPhone
				? Math.Max(minimumChatHeight, mainHeight / 3)
				: 3 * policy.MinimumTarget;
			var utilityHeight = reserveUtility && !HidePlayerUtility ? Math.Min(mainHeight,
				Math.Max(minimumChatHeight, preferredUtilityHeight)) : 0;
			var inlineOptionsAction = fullWidthContent && !showMap && !reserveUtility;
			var actionHeight = reserveUtility || HidePlayerUtility || inlineOptionsAction ? 0 :
				Math.Min(policy.MinimumTarget, Math.Max(0, mainHeight - 2 * Gap));
			var actionSpacing = actionHeight > 0 ? 2 * Gap : 0;
			var upperHeight = Math.Max(0, mainHeight - utilityHeight - (utilityHeight > 0 ? Gap : 0) -
				actionHeight - actionSpacing);

			var minimumPlayerWidth = 9 * policy.MinimumTarget + Gap + (policy.IsPhone ? 0 : policy.MinimumTarget);
			var availableWidth = Math.Max(0, width - Gap);
			var preferredPlayerWidth = 2 * availableWidth / 3;

			// Keep a two-thirds roster on roomy displays, expanding it only when
			// the multiplayer columns need their physical touch-target widths.
			var playersWidth = skirmishMode ? preferredPlayerWidth :
				Math.Min(Math.Max(0, availableWidth - 2 * policy.MinimumTarget),
					Math.Max(preferredPlayerWidth, minimumPlayerWidth));
			if (policy.IsPhone && preferredColumnWidths != null)
				playersWidth = Math.Min(Math.Max(0, availableWidth - 2 * policy.MinimumTarget),
					preferredColumnWidths.Take(skirmishMode ? 5 : 6)
						.Sum(value => Math.Max(policy.MinimumTarget, value)));
			// Phone skirmish is primarily a roster editor, not a map gallery.
			// Reserve a compact preview while still allowing long content to grow.
			if (policy.IsPhone && skirmishMode)
				playersWidth = Math.Min(Math.Max(0, availableWidth - 2 * policy.MinimumTarget),
					Math.Max(playersWidth, availableWidth * 3 / 4));
			if (fullWidthContent)
				playersWidth = width;

			var mapWidth = Math.Max(0, width - Gap - playersWidth);
			Players = new WidgetBounds(0, mainY, playersWidth, upperHeight);
			var playerHeaderHeight = Math.Min(
				policy.IsPhone ? policy.MinimumReadableTextHeight : policy.MinimumTarget, Players.Height);
			PlayerHeader = new WidgetBounds(0, 0, Players.Width, playerHeaderHeight);
			var playerListY = Math.Min(Players.Height, PlayerHeader.Bottom + Gap);
			PlayerList = new WidgetBounds(0, playerListY, Players.Width,
				Math.Max(0, Players.Height - playerListY));

			var utilityY = utilityHeight > 0 && upperHeight > 0 ? Players.Bottom + Gap : Main.Bottom;
			var utilityWidth = playersWidth;
			var positionWidth = Math.Min(utilityWidth, Math.Max(2 * policy.MinimumTarget, width / 5));
			Chat = new WidgetBounds(0, utilityY, utilityWidth, utilityHeight);
			var chatInputHeight = Math.Min(policy.MinimumTarget, Chat.Height);
			var chatInputGap = policy.IsPhone ? Gap : 0;
			var chatDisplayHeight = Math.Max(0, Chat.Height - chatInputHeight - chatInputGap);
			var chatInputY = chatDisplayHeight + chatInputGap;
			ChatDisplay = new WidgetBounds(0, 0, Chat.Width, chatDisplayHeight);
			Position = reserveUtility ?
				new WidgetBounds(0, utilityY + chatInputY, positionWidth, chatInputHeight) :
				new WidgetBounds(0, Math.Max(Main.Y, Main.Bottom - Gap - actionHeight), positionWidth, actionHeight);
			var chatInputX = Math.Min(Chat.Width, Position.Right + Gap);
			ChatInput = new WidgetBounds(
				chatInputX, chatInputY, Math.Max(0, Chat.Width - chatInputX), chatInputHeight);

			if (policy.IsPhone && showMap)
			{
				Map = new WidgetBounds(Players.Right + Gap, Main.Y, mapWidth, Main.Height);
				var footerAvailable = Math.Max(0, Footer.Width - 2 * Gap);
				var firstRight = footerAvailable / 3;
				var secondRight = 2 * footerAvailable / 3;
				Start = new WidgetBounds(Footer.X, Footer.Y, firstRight, Footer.Height);
				ChangeMap = new WidgetBounds(Start.Right + Gap, Footer.Y,
					Math.Max(0, secondRight - firstRight), Footer.Height);
				Disconnect = new WidgetBounds(ChangeMap.Right + Gap, Footer.Y,
					Math.Max(0, Footer.Right - ChangeMap.Right - Gap), Footer.Height);
			}
			else
			{
				var changeHeight = Math.Min(policy.MinimumTarget, Main.Height);
				var mapHeight = Math.Max(0, Main.Height - changeHeight - (changeHeight > 0 ? Gap : 0));
				Map = new WidgetBounds(Players.Right + Gap, mainY, mapWidth, mapHeight);
				ChangeMap = new WidgetBounds(
					Map.X, Map.Bottom + (changeHeight > 0 ? Gap : 0), mapWidth, changeHeight);
				var footerButtonWidth = Math.Max(0, (Footer.Width - Gap) / 2);
				Start = new WidgetBounds(Footer.X, Footer.Y, footerButtonWidth, Footer.Height);
				Disconnect = new WidgetBounds(Start.Right + Gap, Footer.Y,
					Math.Max(0, Footer.Right - Start.Right - Gap), Footer.Height);
			}

			var reclaimedScrollbarWidth = policy.IsPhone ? 0 : policy.MinimumTarget + Gap;
			PlayerScrollbarWidth = showPlayerScrollbar && !policy.IsPhone ? policy.MinimumTarget : 0;
			var playerRowWidth = Math.Max(0, Players.Width - PlayerScrollbarWidth);
			var rowHeight = HidePlayerUtility ? (int)Math.Ceiling(44 * policy.LogicalPerPoint) : policy.MinimumTarget;
			PlayerRow = new WidgetBounds(0, 0, playerRowWidth, Math.Min(rowHeight, Players.Height));
			var fittedPreferences = preferredColumnWidths;
			if (policy.IsPhone && skirmishMode && preferredColumnWidths != null)
			{
				fittedPreferences = preferredColumnWidths.Take(5).Select(value => Math.Max(policy.MinimumTarget, value)).ToArray();
				var spare = Math.Max(0, playerRowWidth - fittedPreferences.Sum());
				// Grow the two text-heavy fields proportionally. Color, team and
				// spawn stay compact instead of acquiring empty padding.
				var nameExtra = spare * fittedPreferences[0] / (fittedPreferences[0] + fittedPreferences[2]);
				fittedPreferences[0] += nameExtra;
				fittedPreferences[2] += spare - nameExtra;
			}
			var columns = fittedPreferences != null && (policy.IsPhone || !skirmishMode)
				? FitPlayerColumnsCore(playerRowWidth, policy.MinimumTarget,
					fittedPreferences.Take(skirmishMode ? 5 : 6).ToArray(), policy.IsPhone)
				: PlayerColumns(playerRowWidth, policy.MinimumTarget, skirmishMode, reclaimedScrollbarWidth);
			if (HidePlayerUtility)
				for (var i = 0; i < columns.Length; i++)
					columns[i] = new WidgetBounds(columns[i].X, columns[i].Y, columns[i].Width, PlayerRow.Height);
			PlayerName = columns[0];
			PlayerColor = columns[1];
			PlayerFaction = columns[2];
			PlayerTeam = columns[3];
			PlayerHandicap = new WidgetBounds(PlayerTeam.Right, 0, 0, policy.MinimumTarget);
			PlayerSpawn = columns[4];
			PlayerReady = skirmishMode ?
				new WidgetBounds(PlayerSpawn.Right, 0, 0, policy.MinimumTarget) : columns[5];
		}

		public static IosLobbyLayout Create(int width, int height, bool compact)
		{
			return new IosLobbyLayout(width, height, IosMenuLayoutPolicy.Create(true, width, height), false);
		}

		public static IosLobbyLayout Create(int width, int height, IosMenuLayoutPolicy policy)
		{
			return new IosLobbyLayout(width, height, policy, false);
		}

		public static IosLobbyLayout Create(int width, int height, IosMenuLayoutPolicy policy, bool skirmishMode,
			bool fullWidthContent = false, bool showMap = true, bool reserveUtility = true,
			bool showHeader = true, int[] preferredColumnWidths = null, bool showPlayerScrollbar = false,
			bool hidePlayerUtility = false)
		{
			return new IosLobbyLayout(width, height, policy, skirmishMode, fullWidthContent, showMap,
				reserveUtility, showHeader, preferredColumnWidths, showPlayerScrollbar, hidePlayerUtility);
		}

		public static WidgetBounds[] FitPlayerColumns(int width, int minimumTarget, int[] preferred)
			=> FitPlayerColumnsCore(width, minimumTarget, preferred, false);

		static WidgetBounds[] FitPlayerColumnsCore(int width, int minimumTarget, int[] preferred, bool preserveName)
		{
			var widths = preferred.Select(value => Math.Max(minimumTarget, value)).ToArray();
			// Names are variable-length. Give spare space to the name, not faction;
			// when constrained, reclaim name padding before shortening fixed labels.
			var excess = Math.Max(0, widths.Sum() - width);
			var fromName = preserveName ? 0 : Math.Min(excess, widths[0] - minimumTarget);
			widths[0] -= fromName;
			excess -= fromName;
			if (excess > 0)
			{
				var floor = Math.Min(minimumTarget, width / widths.Length);
				var available = Math.Max(0, width - floor * widths.Length);
				var flexible = widths.Sum(value => value - floor);
				for (var i = 0; i < widths.Length; i++)
					widths[i] = floor + (flexible > 0 ? (int)((long)(widths[i] - floor) * available / flexible) : 0);
			}
			widths[0] += Math.Max(0, width - widths.Sum());
			var columns = new WidgetBounds[widths.Length];
			var x = 0;
			for (var i = 0; i < widths.Length; i++)
			{
				columns[i] = new WidgetBounds(x, 0, widths[i], minimumTarget);
				x += widths[i];
			}
			return columns;
		}

		static WidgetBounds[] PlayerColumns(int width, int minimumTarget, bool skirmishMode,
			int firstColumnBonus = 0)
		{
			firstColumnBonus = Math.Min(Math.Max(0, firstColumnBonus), width);
			var baseWidth = width - firstColumnBonus;
			// Reserve readable faction/spawn values plus their dropdown arrows.
			// The name must not consume a third of the five-column skirmish roster.
			var minimumUnits = skirmishMode ? new[] { 3, 2, 3, 2, 3 } : new[] { 2, 1, 3, 1, 1, 1 };
			var weights = skirmishMode ? new[] { 3, 2, 3, 2, 3 } : new[] { 2, 1, 3, 1, 1, 1 };
			var totalMinimumUnits = minimumUnits.Sum();
			var required = totalMinimumUnits * minimumTarget;
			var extra = Math.Max(0, baseWidth - required);
			var totalWeight = weights.Sum();
			var columns = new WidgetBounds[weights.Length];
			var x = 0;
			var distributedMinimumUnits = 0;
			var distributedWeight = 0;
			for (var i = 0; i < weights.Length; i++)
			{
				distributedMinimumUnits += minimumUnits[i];
				distributedWeight += weights[i];
				var right = i == weights.Length - 1 ? baseWidth : Math.Min(baseWidth,
					required > baseWidth ? (int)((long)distributedMinimumUnits * baseWidth / totalMinimumUnits) :
					distributedMinimumUnits * minimumTarget +
					(int)((long)extra * distributedWeight / totalWeight));
				columns[i] = new WidgetBounds(x, 0, Math.Max(0, right - x), minimumTarget);
				x = right;
			}

			if (firstColumnBonus > 0)
			{
				columns[0] = new WidgetBounds(columns[0].X, columns[0].Y,
					columns[0].Width + firstColumnBonus, columns[0].Height);
				for (var i = 1; i < columns.Length; i++)
					columns[i] = new WidgetBounds(columns[i].X + firstColumnBonus, columns[i].Y,
						columns[i].Width, columns[i].Height);
			}

			return columns;
		}
	}
}
