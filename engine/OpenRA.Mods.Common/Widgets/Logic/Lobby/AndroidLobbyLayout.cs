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
using OpenRA.MobileUi;
using OpenRA.Primitives;

namespace OpenRA.Mods.Common.Widgets.Logic
{
	/// <summary>
	/// Pure geometry model for the Android phone SERVER_LOBBY layout.
	///
	/// Layout contract (dp values, converted to OpenRA UI px by the caller):
	///   - root content area: safe area minus 8-12dp margins;
	///   - title bar: 48-56dp tall;
	///   - tab row: 48-52dp tall, full width, equal cells;
	///   - bottom bar: 56-64dp tall; Start is the primary (larger) control and
	///     Back is secondary - no three equal footer buttons;
	///   - main area split: players 62-66% / map 34-38%;
	///   - the map column hosts a 48dp tool bar with the "change map" control
	///     (and the host's slots/reset controls) above the map content;
	///   - each player row is a two-line card: line 1 name|color|ready,
	///     line 2 faction|team|spawn (handicap removed on phones).
	/// All geometry derives from MobileUiService dp tokens once per layout
	/// change; nothing here is queried per frame.
	/// </summary>
	public static class AndroidLobbyLayout
	{
		// Root margins and gaps.
		public const float MarginDp = 10f;
		public const float GapDp = 8f;

		// Bar heights.
		public const float HeaderHeightDp = 52f;
		public const float TabHeightDp = 48f;
		public const float FooterHeightDp = 60f;
		public const float MapToolbarHeightDp = 48f;

		// Main split (players fraction stays within the phone contract).
		public const float PlayersFraction = 0.64f;

		// Footer hierarchy: Back secondary, Start primary.
		public const float BackWidthDp = 132f;

		// Player card.
		public const float CardRowHeightDp = 96f;
		public const float CardGapDp = 6f;
		public const float Line1HeightDp = 48f;
		public const float CardHorizontalPaddingDp = 8f;

		public readonly struct PlayerCell
		{
			public readonly int X;
			public readonly int Y;
			public readonly int Width;
			public readonly int Height;

			public PlayerCell(int x, int y, int width, int height)
			{
				X = x;
				Y = y;
				Width = width;
				Height = height;
			}

			public int Right => X + Width;
		}

		/// <summary>Child coordinates are local to the lobby root (x/y relative to its bounds).</summary>
		public readonly struct Layout
		{
			public readonly int ContentWidth;
			public readonly int ContentHeight;
			public readonly int HeaderHeight;
			public readonly int TabHeight;
			public readonly int FooterHeight;
			public readonly int MapToolbarHeight;
			public readonly int Gap;

			public readonly int TabY;
			public readonly int MainY;
			public readonly int MainHeight;
			public readonly int FooterY;

			public readonly int PlayerWidth;
			public readonly int MapX;
			public readonly int MapWidth;

			// Map column tool bar (change map / slots / reset) above map content.
			public readonly int ToolbarY;
			public readonly int ToolbarHeight;
			public readonly int ToolbarWidth;

			// Map preview content area.
			public readonly int MapContentY;
			public readonly int MapContentHeight;

			// Footer cells.
			public readonly PlayerCell BackCell;
			public readonly PlayerCell StartCell;

			// Player card metrics (local to each row).
			public readonly int CardRowHeight;
			public readonly int Line1Height;
			public readonly int CardGap;
			public readonly int CardPadding;
			public readonly PlayerCell NameCell;
			public readonly PlayerCell ColorCell;
			public readonly PlayerCell ReadyCell;
			public readonly PlayerCell FactionCell;
			public readonly PlayerCell TeamCell;
			public readonly PlayerCell SpawnCell;
			public readonly PlayerCell JoinCell;

			public Layout(int width, int height, float dpToUi)
			{
				ContentWidth = Math.Max(1, width);
				ContentHeight = Math.Max(1, height);

				HeaderHeight = Math.Max(1, (int)Math.Round(HeaderHeightDp * dpToUi));
				TabHeight = Math.Max(1, (int)Math.Round(TabHeightDp * dpToUi));
				FooterHeight = Math.Max(1, (int)Math.Round(FooterHeightDp * dpToUi));
				MapToolbarHeight = Math.Max(1, (int)Math.Round(MapToolbarHeightDp * dpToUi));
				Gap = Math.Max(1, (int)Math.Round(GapDp * dpToUi));

				TabY = HeaderHeight + Gap;
				FooterY = Math.Max(TabY + TabHeight, ContentHeight - FooterHeight);
				MainY = Math.Min(FooterY, TabY + TabHeight + Gap);
				var mainBottom = Math.Max(MainY, FooterY - Gap);
				MainHeight = Math.Max(1, mainBottom - MainY);

				var playerWidth = (int)Math.Round(ContentWidth * PlayersFraction);
				PlayerWidth = Math.Max(1, playerWidth);
				MapX = PlayerWidth + Gap;
				MapWidth = Math.Max(1, ContentWidth - MapX);

				ToolbarY = MainY;
				ToolbarHeight = Math.Min(MapToolbarHeight, MainHeight);
				ToolbarWidth = MapWidth;
				MapContentY = MainY + ToolbarHeight + Gap;
				MapContentHeight = Math.Max(1, MainHeight - ToolbarHeight - Gap);

				// Footer: Back (secondary, compact) on the left, Start (primary) fills the rest.
				var backWidth = Math.Min(ContentWidth - Gap, Math.Max(1, (int)Math.Round(BackWidthDp * dpToUi)));
				BackCell = new PlayerCell(0, FooterY, backWidth, ContentHeight - FooterY);
				StartCell = new PlayerCell(Math.Min(ContentWidth, backWidth + Gap), FooterY,
					Math.Max(1, ContentWidth - backWidth - Gap), ContentHeight - FooterY);

				// Player card.
				CardRowHeight = Math.Max(1, (int)Math.Round(CardRowHeightDp * dpToUi));
				Line1Height = Math.Max(1, Math.Min(CardRowHeight, (int)Math.Round(Line1HeightDp * dpToUi)));
				CardGap = Math.Max(1, (int)Math.Round(CardGapDp * dpToUi));
				CardPadding = Math.Max(1, (int)Math.Round(CardHorizontalPaddingDp * dpToUi));

				var contentWidth = Math.Max(1, PlayerWidth - 2 * CardPadding);
				var minCell = Math.Max(1, (int)Math.Round(48f * dpToUi));
				var line2Y = Line1Height;
				var line2Height = Math.Max(1, CardRowHeight - line2Y);

				var nameWidth = Math.Max(minCell, (int)(contentWidth * 0.5f));
				var colorWidth = Math.Max(minCell, (int)(contentWidth * 0.24f));
				var readyX = nameWidth + colorWidth + 2 * CardGap;
				var readyWidth = Math.Max(minCell, contentWidth - readyX);

				NameCell = new PlayerCell(CardPadding, 0, nameWidth, Line1Height);
				ColorCell = new PlayerCell(CardPadding + nameWidth + CardGap, 0, colorWidth, Line1Height);
				ReadyCell = new PlayerCell(CardPadding + readyX, 0, readyWidth, Line1Height);

				// Line 2: faction | spawn | team (spawn sits right behind the
				// country control; handicap removed).
				var factionWidth = Math.Max(minCell, (int)(contentWidth * 0.34f));
				var spawnWidth = Math.Max(minCell, (int)(contentWidth * 0.3f));
				var teamWidth = Math.Max(minCell, contentWidth - factionWidth - spawnWidth - 2 * CardGap);

				var x = CardPadding;
				FactionCell = new PlayerCell(x, line2Y, factionWidth, line2Height);
				x += factionWidth + CardGap;
				SpawnCell = new PlayerCell(x, line2Y, spawnWidth, line2Height);
				x += spawnWidth + CardGap;
				TeamCell = new PlayerCell(x, line2Y, Math.Max(1, contentWidth - (x - CardPadding)), line2Height);

				JoinCell = new PlayerCell(NameCell.Right + CardGap, 0,
					Math.Max(1, contentWidth - NameCell.Width - CardGap), Line1Height);
			}
		}

		/// <summary>
		/// Absolute content rectangle: the lobby fills the entire render canvas
		/// edge-to-edge (no gutters) so the wallpaper never shows around it.
		/// </summary>
		public static Rectangle ContentBounds(MobileUiService service)
		{
			return new Rectangle(
				0,
				0,
				Math.Max(1, service.CanvasWidth),
				Math.Max(1, service.CanvasHeight));
		}

		/// <summary>Pure variant for tests (dp inputs).</summary>
		public static Layout ComputeLayout(float usableWidthDp, float usableHeightDp, float dpToUi)
		{
			var width = Math.Max(1, (int)Math.Round(usableWidthDp * dpToUi));
			var height = Math.Max(1, (int)Math.Round(usableHeightDp * dpToUi));
			return new Layout(width, height, dpToUi);
		}

		/// <summary>Builds a layout from an already-computed pixel content size.</summary>
		public static Layout ComputeLayoutFromPx(int widthPx, int heightPx, float dpToUi)
		{
			return new Layout(widthPx, heightPx, dpToUi);
		}

		/// <summary>
		/// Phone player-card field captions occupy the left side of the whole
		/// touch row. Keeping the full row height lets LabelWidget vertically
		/// center the caption, while the capped width leaves a distinct value
		/// area and the final square for the enlarged dropdown marker.
		/// </summary>
		public static PlayerCell MobileCaptionBounds(PlayerCell cell, int padding)
		{
			padding = Math.Max(0, padding);
			var markerReserve = Math.Min(cell.Width, cell.Height);
			var valueAndMarkerLimit = Math.Max(1, cell.Width - markerReserve - 2 * padding);
			var desiredWidth = Math.Max(1, (int)Math.Round(cell.Width * 0.32f));
			var width = Math.Min(desiredWidth, Math.Max(1, valueAndMarkerLimit / 2));
			return new PlayerCell(cell.X + padding, cell.Y, width, cell.Height);
		}
	}
}
