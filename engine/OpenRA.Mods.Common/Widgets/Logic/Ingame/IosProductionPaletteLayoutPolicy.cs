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
	public readonly struct IosProductionPaletteLayout
	{
		public readonly int Columns;
		public readonly int2 IconSize;
		public readonly int2 IconMargin;
		public readonly int MaximumRows;
		public readonly int PaletteX;
		public readonly int PaletteY;

		public int GridWidth => Columns * IconSize.X + Math.Max(0, Columns - 1) * IconMargin.X;

		public IosProductionPaletteLayout(
			int columns, int2 iconSize, int2 iconMargin, int maximumRows, int paletteX, int paletteY = 2)
		{
			Columns = columns;
			IconSize = iconSize;
			IconMargin = iconMargin;
			MaximumRows = maximumRows;
			PaletteX = paletteX;
			PaletteY = paletteY;
		}
	}

	public static class IosProductionPaletteLayoutPolicy
	{
		const int PaletteTop = 2;

		public static IosProductionPaletteLayout Create(
			IosScreenSnapshot snapshot, IosIngameSidebarLayout shell, IosProductionPaletteMode mode)
		{
			var columns = mode switch
			{
				IosProductionPaletteMode.LargeSingleColumn => 1,
				IosProductionPaletteMode.DoubleColumn => 2,
				IosProductionPaletteMode.CompactThreeColumns => 3,
				_ => snapshot.IsCompactPhone ? 1 : 3
			};
			var preferredRowHeight = columns switch { 1 => 116, 2 => 80, _ => 50 };
			var availableHeight = Math.Max(1, shell.BottomCapY - PaletteTop);
			var rowHeight = Math.Min(preferredRowHeight, availableHeight);
			if (columns == 1)
			{
				// Preserve the existing 234x116 cell. Wide cameos fill its interior;
				// original cameos continue using the renderer's uniform fit fallback.
				var left = (shell.ProductionBounds.Width * 31 + 233) / 234;
				var right = shell.ProductionBounds.Width * 203 / 234;
				var top = (rowHeight * 7 + 115) / 116;
				var bottom = rowHeight * 109 / 116;
				var wideSize = new int2(Math.Max(1, right - left), Math.Max(1, bottom - top));
				return new IosProductionPaletteLayout(1, wideSize, new int2(0, rowHeight - wideSize.Y),
					Math.Max(1, availableHeight / rowHeight), left, top);
			}
			var iconSize = columns switch
			{
				2 => new int2(90, 72),
				_ => new int2(60, 48)
			};
			var iconWidth = Math.Max(1, Math.Min(iconSize.X, rowHeight * 5 / 4));
			iconSize = new int2(iconWidth, Math.Max(1, Math.Min(rowHeight, iconWidth * 4 / 5)));
			var margin = new int2(columns == 1 ? 0 : columns == 2 ? 4 : 2,
				Math.Max(0, rowHeight - iconSize.Y));
			var maximumRows = Math.Max(1, availableHeight / rowHeight);
			var paletteY = Math.Max(0, (rowHeight - iconSize.Y) / 2);

			var gridWidth = columns * iconSize.X + (columns - 1) * margin.X;
			var paletteX = Math.Max(0, (shell.ProductionBounds.Width - gridWidth) / 2);
			return new IosProductionPaletteLayout(columns, iconSize, margin, maximumRows, paletteX, paletteY);
		}

		public static Rectangle LargePanelContentBounds(int width, int height)
		{
			// All three faction backgrounds share this inset within the 640x512 art.
			// Round inward so fractional UI scales never put image pixels on the rim.
			var left = (width * 44 + 639) / 640;
			var top = (height * 80 + 511) / 512;
			var right = width * 596 / 640;
			var bottom = height * 424 / 512;
			return new Rectangle(left, top, Math.Max(0, right - left), Math.Max(0, bottom - top));
		}

		public static int[] BackgroundRowHeights(IosProductionPaletteLayout layout, int fillHeight)
		{
			if (fillHeight <= 0)
				return Array.Empty<int>();

			var height = Math.Max(1, layout.IconSize.Y + layout.IconMargin.Y);
			var rows = (fillHeight + height - 1) / height;
			var result = new int[rows];
			for (var i = 0; i < rows; i++)
				result[i] = Math.Min(height, fillHeight - i * height);
			return result;
		}

	}
}
