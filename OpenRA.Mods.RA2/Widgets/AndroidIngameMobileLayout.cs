#region Copyright & License Information
/*
 * This file is part of OpenRA, which is free software. It is made
 * available under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System;
using OpenRA.Primitives;

namespace OpenRA.Mods.RA2.Widgets
{
	public readonly struct AndroidIngameMobileLayout
	{
		const int DesignWidth = 234;
		const int DesignTopHeight = 275;
		const int DesignRowHeight = 50;
		const int DesignBottomHeight = 66;
		const int DesignRows = 6;
		const int DesignPaletteX = 24;
		const int DesignPaletteY = 2;
		const int DesignIconWidth = 60;
		const int DesignIconHeight = 48;
		const int DesignIconMargin = 2;
		const int DesignHeight = DesignTopHeight + DesignRows * DesignRowHeight + DesignBottomHeight;

		public Rectangle TopBarBounds { get; }
		public Rectangle SidebarBounds { get; }
		public Rectangle PaletteBounds { get; }
		public int2 IconSize { get; }
		public int2 IconMargin { get; }
		public int Rows { get; }
		public double Scale { get; }

		AndroidIngameMobileLayout(
			Rectangle topBarBounds, Rectangle sidebarBounds, Rectangle paletteBounds,
			int2 iconSize, int2 iconMargin, int rows, double scale)
		{
			TopBarBounds = topBarBounds;
			SidebarBounds = sidebarBounds;
			PaletteBounds = paletteBounds;
			IconSize = iconSize;
			IconMargin = iconMargin;
			Rows = rows;
			Scale = scale;
		}

		public static AndroidIngameMobileLayout Create(Size resolution)
		{
			var screenWidth = Math.Max(1, resolution.Width);
			var screenHeight = Math.Max(1, resolution.Height);
			var scale = Math.Min(screenWidth / (double)DesignWidth, screenHeight / (double)DesignHeight);
			var sidebarWidth = Math.Max(1, (int)Math.Round(DesignWidth * scale));
			var topHeight = Math.Max(1, (int)Math.Round(DesignTopHeight * scale));
			var sidebar = new Rectangle(screenWidth - sidebarWidth, topHeight, sidebarWidth, screenHeight - topHeight);
			var topBar = new Rectangle(screenWidth - sidebarWidth, 0, sidebarWidth, topHeight);
			var iconSize = new int2(
				Math.Max(1, (int)Math.Round(DesignIconWidth * scale)),
				Math.Max(1, (int)Math.Round(DesignIconHeight * scale)));
			var iconMargin = new int2(
				Math.Max(0, (int)Math.Round(DesignIconMargin * scale)),
				Math.Max(0, (int)Math.Round(DesignIconMargin * scale)));
			var paletteWidth = 3 * iconSize.X + 2 * iconMargin.X;
			var paletteHeight = DesignRows * (iconSize.Y + iconMargin.Y);
			return new AndroidIngameMobileLayout(topBar, sidebar,
				new Rectangle(
					(int)Math.Round(DesignPaletteX * scale),
					(int)Math.Round(DesignPaletteY * scale),
					paletteWidth,
					paletteHeight),
				iconSize, iconMargin, DesignRows, scale);
		}
	}
}
