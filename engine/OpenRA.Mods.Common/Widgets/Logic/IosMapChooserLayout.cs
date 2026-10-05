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
using OpenRA.Primitives;

namespace OpenRA.Mods.Common.Widgets.Logic
{
	public sealed class IosMapChooserLayout
	{
		const int FooterButtonCount = 5;

		readonly int gap;

		public int MinimumTarget { get; }
		public int ScrollbarWidth { get; }
		public Rectangle Panel { get; }
		public Rectangle LocalPanel { get; }
		public Rectangle Title { get; }
		public Rectangle TabBar { get; }
		public Rectangle SystemTab { get; }
		public Rectangle RemoteTab => SystemTab;
		public Rectangle UserTab { get; }
		public Rectangle UserTabFor(bool systemMapsVisible) => systemMapsVisible ? UserTab : SystemTab;
		public Rectangle RemoteStatus { get; }
		public Rectangle MapPane { get; }
		public Rectangle SelectedMap { get; }
		public Rectangle SelectedPreview { get; }
		public Rectangle SelectedTitle { get; }
		public Rectangle SelectedDetails { get; }
		public Rectangle SelectedAuthor { get; }
		public int RowHeight { get; }
		public int GridColumns { get; }
		public int CardWidth { get; }
		public int CardHeight { get; }
		public Rectangle CardPreview { get; }
		public Rectangle CardPlayerCount { get; }
		public int Gap => gap;
		public Rectangle FilterRow { get; }
		public Rectangle FilterLabel { get; }
		public Rectangle FilterInput { get; }
		public Rectangle FilterJoiner { get; }
		public Rectangle GameModeFilter { get; }
		public Rectangle OrderByLabel { get; }
		public Rectangle OrderBy { get; }
		public Rectangle Footer { get; }

		public static IosMapChooserLayout Create(IosScreenSnapshot snapshot, bool commandCenter = false) => new(snapshot, commandCenter);

		public static Rectangle MapArtworkContentBounds(IosScreenSnapshot snapshot)
		{
			if (snapshot.IsCompactPhone)
				return MultiplayerScreenLayout.ContentBounds(snapshot).ToRectangle();

			// The approved 1672 x 941 map shell has a quiet inner rectangle at
			// (134, 94)-(1538, 837). Use conservative 8%/10%/92%/89% bounds
			// so controls clear both the rails and the diagonal lower corner armor.
			var safe = snapshot.SafeBounds;
			var width = snapshot.EffectiveSize.Width;
			var height = snapshot.EffectiveSize.Height;
			var left = Math.Max(safe.Left, (int)Math.Ceiling(width * .08));
			var top = Math.Max(safe.Top, (int)Math.Ceiling(height * .10));
			var right = Math.Min(safe.Right, (int)Math.Floor(width * .92));
			var bottom = Math.Min(safe.Bottom, (int)Math.Floor(height * .89));
			return Rectangle.FromLTRB(left, top, Math.Max(left, right), Math.Max(top, bottom));
		}

		IosMapChooserLayout(IosScreenSnapshot snapshot, bool commandCenter)
		{
			MinimumTarget = snapshot.LogicalPoints(48);
			ScrollbarWidth = MinimumTarget;
			gap = snapshot.LogicalPoints(snapshot.IsCompactPhone ? 6 : 12);
			var safeMargin = snapshot.LogicalPoints(snapshot.IsCompactPhone ? 8 : 18);
			Panel = commandCenter ? MapArtworkContentBounds(snapshot) :
				IosTouchDialogLayout.Inset(snapshot.SafeBounds, safeMargin);
			LocalPanel = new Rectangle(0, 0, Panel.Width, Panel.Height);

			var contentInset = snapshot.LogicalPoints(snapshot.IsCompactPhone ? 8 : 20);
			var inner = commandCenter ? LocalPanel : IosTouchDialogLayout.Inset(LocalPanel, contentInset);
			var titleHeight = snapshot.LogicalPoints(snapshot.IsCompactPhone ? 28 : 44);
			Title = new Rectangle(inner.X, inner.Y, inner.Width, Math.Min(titleHeight, inner.Height));
			TabBar = new Rectangle(inner.X, Title.Bottom + gap, inner.Width, MinimumTarget);
			Footer = new Rectangle(inner.X, inner.Bottom - MinimumTarget, inner.Width, MinimumTarget);
			FilterRow = new Rectangle(inner.X, Footer.Top - gap - MinimumTarget, inner.Width, MinimumTarget);
			MapPane = Rectangle.FromLTRB(
				inner.Left,
				TabBar.Bottom + gap,
				inner.Right,
				Math.Max(TabBar.Bottom + gap, FilterRow.Top - gap));
			RowHeight = MinimumTarget + 2 * gap;
			if (commandCenter)
			{
				var listWidth = 2 * Math.Max(0, MapPane.Width - gap) / 3;
				SelectedMap = new Rectangle(MapPane.X + listWidth + gap, MapPane.Y,
					Math.Max(0, MapPane.Width - listWidth - gap), MapPane.Height);
				MapPane = new Rectangle(MapPane.X, MapPane.Y, listWidth, MapPane.Height);
				var labelHeight = snapshot.LogicalPoints(24);
				var previewHeight = Math.Max(0, Math.Min(SelectedMap.Width, SelectedMap.Height - labelHeight - gap));
				SelectedPreview = new Rectangle(0, 0, SelectedMap.Width, previewHeight);
				SelectedDetails = new Rectangle(0, SelectedPreview.Bottom + gap, SelectedMap.Width, labelHeight);
				SelectedTitle = new Rectangle(0, SelectedDetails.Bottom, SelectedMap.Width, 0);
				SelectedAuthor = SelectedTitle;
			}

			GridColumns = 3;
			CardWidth = Math.Max(MinimumTarget,
				(MapPane.Width - (GridColumns + 1) * gap) / GridColumns);
			var cardLabelHeight = snapshot.LogicalPoints(24);
			CardHeight = Math.Max(MinimumTarget, CardWidth * 3 / 4 + cardLabelHeight + gap);
			var cardInset = Math.Max(1, gap / 2);
			CardPreview = new Rectangle(cardInset, cardInset, Math.Max(0, CardWidth - 2 * cardInset),
				Math.Max(0, CardHeight - cardLabelHeight - 3 * cardInset));
			CardPlayerCount = new Rectangle(cardInset, CardPreview.Bottom + cardInset,
				Math.Max(0, CardWidth - 2 * cardInset), cardLabelHeight);

			var tabWidth = Math.Max(MinimumTarget, (TabBar.Width - gap) / 4);
			SystemTab = new Rectangle(TabBar.X, TabBar.Y, tabWidth, TabBar.Height);
			UserTab = new Rectangle(SystemTab.Right + gap, TabBar.Y, tabWidth, TabBar.Height);
			RemoteStatus = Rectangle.FromLTRB(
				UserTab.Right + gap, TabBar.Top, TabBar.Right, TabBar.Bottom);

			var filterAvailable = Math.Max(6, FilterRow.Width - 5 * gap);
			var widths = WeightedWidths(filterAvailable, 8, 28, 3, 25, 11, 25);
			var x = FilterRow.X;
			FilterLabel = Next(ref x, FilterRow.Y, widths[0], FilterRow.Height);
			x += gap;
			FilterInput = Next(ref x, FilterRow.Y, widths[1], FilterRow.Height);
			x += gap;
			FilterJoiner = Next(ref x, FilterRow.Y, widths[2], FilterRow.Height);
			x += gap;
			GameModeFilter = Next(ref x, FilterRow.Y, widths[3], FilterRow.Height);
			x += gap;
			OrderByLabel = Next(ref x, FilterRow.Y, widths[4], FilterRow.Height);
			x += gap;
			OrderBy = Rectangle.FromLTRB(x, FilterRow.Y, FilterRow.Right, FilterRow.Bottom);
		}

		public Rectangle FooterButtonBounds(int index)
		{
			if (index < 0 || index >= FooterButtonCount)
				throw new ArgumentOutOfRangeException(nameof(index));

			var availableWidth = Math.Max(FooterButtonCount, Footer.Width - (FooterButtonCount - 1) * gap);
			var baseWidth = availableWidth / FooterButtonCount;
			var remainder = availableWidth % FooterButtonCount;
			var x = Footer.X + index * (baseWidth + gap) + Math.Min(index, remainder);
			return new Rectangle(x, Footer.Y, baseWidth + (index < remainder ? 1 : 0), Footer.Height);
		}

		static Rectangle Next(ref int x, int y, int width, int height)
		{
			var result = new Rectangle(x, y, width, height);
			x += width;
			return result;
		}

		static int[] WeightedWidths(int total, params int[] weights)
		{
			var result = new int[weights.Length];
			var assigned = 0;
			var totalWeight = 0;
			foreach (var weight in weights)
				totalWeight += weight;

			for (var i = 0; i < weights.Length; i++)
			{
				result[i] = i == weights.Length - 1 ? total - assigned : total * weights[i] / totalWeight;
				assigned += result[i];
			}

			return result;
		}
	}
}
