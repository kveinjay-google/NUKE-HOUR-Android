using System;
using OpenRA.Primitives;

namespace OpenRA.Mods.RA2.Widgets
{
	// Original RA2 hierarchy: detached instrument/radar, action, category,
	// production and paging regions. All coordinates are local to the shell.
	public sealed class HdSidebarLayout
	{
		public Rectangle Shell, Cash, Timer, Power, Menu, Radar, Actions, Categories, Palette, Footer;
		public int Columns, Rows, CapacityRows, Gap, CellWidth, CellHeight;

		public static Rectangle[] SplitRow(Rectangle area, int count)
		{
			if (count <= 0)
				throw new ArgumentOutOfRangeException(nameof(count));

			var buttons = new Rectangle[count];
			for (var i = 0; i < count; i++)
			{
				var left = area.Width * i / count;
				var right = area.Width * (i + 1) / count;
				buttons[i] = new Rectangle(area.X + left, area.Y, right - left, area.Height);
			}

			return buttons;
		}

		public static Rectangle[] ClassicActionTouchBounds(int sidebarWidth)
		{
			var left = sidebarWidth * 32 / 234;
			var right = sidebarWidth * 201 / 234;
			return SplitRow(new Rectangle(left, 202, right - left, 44), 2);
		}

		public static Rectangle[] ClassicActionVisualBounds(int sidebarWidth)
		{
			var targets = ClassicActionTouchBounds(sidebarWidth);
			var width = Math.Max(1, targets[0].Width - 2);
			var result = new Rectangle[targets.Length];
			for (var i = 0; i < targets.Length; i++)
				result[i] = new Rectangle(targets[i].X + (targets[i].Width - width) / 2,
					202, width, 44);
			return result;
		}

		public static Rectangle ClassicControlPanelBounds(int sidebarWidth, int sidebarHeight, bool compactPhone)
		{
			var height = compactPhone ? 102 : 78;
			return new Rectangle(0, Math.Max(0, sidebarHeight - height), sidebarWidth, height);
		}

		public void FitProductionContent(int iconCount)
		{
			Rows = Math.Min(CapacityRows, Math.Max(1, (iconCount + Columns - 1) / Columns));
			Palette = new Rectangle(Palette.X, Palette.Y, Palette.Width,
				Rows * CellHeight + Math.Max(0, Rows - 1) * Gap);
			Footer = new Rectangle(Footer.X, Math.Min(Footer.Y, Palette.Bottom + Gap),
				Footer.Width, Footer.Height);
		}

		public static HdSidebarLayout Create(Size viewport, bool touch, bool phone, double scale,
			int safeRight, int safeBottom, bool singleColumn, bool compactColumns = false)
		{
			int P(double value) => Math.Max(1, (int)Math.Round(value * scale));
			var inset = P(18);
			// Four production categories retain 44-point hit widths on phones and tablets.
			var width = Math.Min(viewport.Width / 2, P(phone ? 212 : touch ? 300 : 270) + safeRight);
			var content = width - safeRight - inset * 2;
			var buttonHeight = P(touch ? 44 : 34);
			var instrumentTop = P(4);
			var instrumentHeight = P(22);
			var instrumentGap = P(3);
			var instrumentWidth = (content - 2 * instrumentGap) / 3;
			var cash = new Rectangle(inset, instrumentTop, instrumentWidth, instrumentHeight);
			var timer = new Rectangle(cash.Right + instrumentGap, instrumentTop, instrumentWidth, instrumentHeight);
			var power = new Rectangle(timer.Right + instrumentGap, instrumentTop,
				content - 2 * instrumentWidth - 2 * instrumentGap, instrumentHeight);
			var menu = new Rectangle(inset, P(30), content, buttonHeight);
			var radarHeight = Math.Min((int)(content * .68), (int)(viewport.Height * .22));
			var radar = new Rectangle(inset, menu.Bottom + P(4), content, radarHeight);
			var actions = new Rectangle(inset, radar.Bottom + P(4), content, buttonHeight);
			var categories = new Rectangle(inset, actions.Bottom + P(4), content, buttonHeight);
			var footer = new Rectangle(inset, viewport.Height - safeBottom - inset - buttonHeight, content, buttonHeight);
			var palette = new Rectangle(inset, categories.Bottom + P(6), content,
				Math.Max(1, footer.Top - categories.Bottom - P(12)));
			// A saved classic single-column preference must not magnify a retail cameo
			// across the entire HD shell. Pinch still chooses large or compact tiles.
			var columns = singleColumn || (phone && !compactColumns) ? 2 : 3;
			var gap = P(4);
			var cw = Math.Max(1, (content - gap * (columns - 1)) / columns);
			// At very short heights contain the original 5:4 cameo; never distort it.
			var ch = Math.Max(1, Math.Min(cw * 4 / 5, palette.Height));
			var rows = Math.Max(1, (palette.Height + gap) / (ch + gap));
			return new HdSidebarLayout
			{
				Shell = new Rectangle(viewport.Width - width, 0, width, viewport.Height),
				Cash = cash, Timer = timer, Power = power, Menu = menu,
				Radar = radar, Actions = actions, Categories = categories,
				Palette = palette, Footer = footer, Columns = columns,
				Rows = rows, CapacityRows = rows, Gap = gap, CellWidth = cw, CellHeight = ch
			};
		}
	}
}
