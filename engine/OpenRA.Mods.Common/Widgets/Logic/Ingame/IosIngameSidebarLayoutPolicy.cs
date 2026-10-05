#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version.
 */
#endregion

using System;
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Mods.Common.Widgets.Logic
{
	public readonly struct IosIngameSidebarLayout
	{
		public readonly Rectangle TopBounds;
		public readonly Rectangle ProductionBounds;
		public readonly int MaximumRows;
		public readonly int BackgroundRows;
		public readonly int BackgroundRemainderHeight;
		public readonly int BottomCapY;
		public readonly int ScrollButtonsY;

		public IosIngameSidebarLayout(
			Rectangle topBounds, Rectangle productionBounds,
			int maximumRows, int backgroundRows, int backgroundRemainderHeight, int bottomCapY)
		{
			TopBounds = topBounds;
			ProductionBounds = productionBounds;
			MaximumRows = maximumRows;
			BackgroundRows = backgroundRows;
			BackgroundRemainderHeight = backgroundRemainderHeight;
			BottomCapY = bottomCapY;
			// Pixel-identical empty slots in both faction atlases begin at (39, 7).
			ScrollButtonsY = bottomCapY + 7;
		}

		public int ScrollButtonLocalY(int parentY) => ScrollButtonsY - parentY;

		public WidgetBounds ScrollButtonLocalBounds(int parentY, bool scrollUp, int parentX = 27) => new(
			(scrollUp ? 116 : 39) - parentX,
			ScrollButtonLocalY(parentY),
			77,
			27);
	}

	/// <summary>
	/// Keeps the classic RA2 sidebar anchored to the safe right edge while allowing
	/// the production palette to use all remaining vertical space on iOS.
	/// </summary>
	public static class IosIngameSidebarLayoutPolicy
	{
		const int SidebarWidth = 234;
		const int TopHeight = 275;
		const int PaletteTop = 2;
		const int BottomCapHeight = 66;
		const int RowHeight = 50;
		const int MinimumRows = 4;

		public static IosIngameSidebarLayout Create(IosScreenSnapshot snapshot)
		{
			// The sidebar is itself the edge treatment, so it must occupy the complete
			// viewport instead of being inset like a floating touch control.
			var viewport = new Rectangle(0, 0, snapshot.EffectiveSize.Width, snapshot.EffectiveSize.Height);
			var width = Math.Min(SidebarWidth, viewport.Width);
			var left = viewport.Right - width;
			// Reserve the enlarged phone tab row below, not on top of, repair/sell.
			var topHeight = snapshot.IsCompactPhone
				? TopHeight - 30 + IosProductionCategoryPolicy.ButtonHeight(width, true)
				: TopHeight;
			var topBounds = new Rectangle(left, viewport.Top, width, Math.Min(topHeight, viewport.Height));
			var productionTop = topBounds.Bottom;
			var productionHeight = Math.Max(1, viewport.Bottom - productionTop);
			var productionBounds = new Rectangle(left, productionTop, width, productionHeight);
			var paletteHeight = Math.Max(RowHeight, productionHeight - PaletteTop - BottomCapHeight);
			var maximumRows = Math.Max(MinimumRows, paletteHeight / RowHeight);
			var bottomCapY = Math.Max(0, productionHeight - BottomCapHeight);
			var backgroundRows = Math.Max(0, bottomCapY / RowHeight);
			var backgroundRemainderHeight = Math.Max(0, bottomCapY - backgroundRows * RowHeight);

			return new IosIngameSidebarLayout(
				topBounds, productionBounds, maximumRows, backgroundRows,
				backgroundRemainderHeight, bottomCapY);
		}

		public static int[] BackgroundRowHeights(IosIngameSidebarLayout layout)
		{
			var count = layout.BackgroundRows + (layout.BackgroundRemainderHeight > 0 ? 1 : 0);
			var heights = new int[count];
			for (var i = 0; i < layout.BackgroundRows; i++)
				heights[i] = RowHeight;
			if (layout.BackgroundRemainderHeight > 0)
				heights[^1] = layout.BackgroundRemainderHeight;
			return heights;
		}
	}
}
