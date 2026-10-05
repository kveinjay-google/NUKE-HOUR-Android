using System;
using OpenRA.Primitives;
using OpenRA.Widgets;
namespace OpenRA.Mods.Common.Widgets.Logic
{
	public sealed class AndroidPauseMenuLayout
	{
		public WidgetBounds ShellBounds { get; }
		public WidgetBounds TitleBounds { get; }
		public WidgetBounds InfoBounds { get; }
		public WidgetBounds[] ButtonBounds { get; }

		AndroidPauseMenuLayout(WidgetBounds shellBounds, WidgetBounds titleBounds,
			WidgetBounds infoBounds, WidgetBounds[] buttonBounds)
		{
			ShellBounds = shellBounds;
			TitleBounds = titleBounds;
			InfoBounds = infoBounds;
			ButtonBounds = buttonBounds;
		}

		public static AndroidPauseMenuLayout Compute(Rectangle safeBounds, int pagePadding,
			int gap, int buttonHeight, int actionCount)
		{
			var padding = Math.Max(0, pagePadding);
			var spacing = Math.Max(0, gap);
			var height = Math.Max(1, buttonHeight);
			var shell = new WidgetBounds(safeBounds.X, safeBounds.Y,
				Math.Max(1, safeBounds.Width), Math.Max(1, safeBounds.Height));
			var contentWidth = Math.Max(1, shell.Width - 2 * padding);
			var actionWidth = Math.Max(1, (contentWidth - spacing) * 7 / 20);
			var infoWidth = Math.Max(1, contentWidth - actionWidth - spacing);
			var title = new WidgetBounds(padding, padding, actionWidth, height);
			var info = new WidgetBounds(padding + actionWidth + spacing, padding,
				infoWidth, Math.Max(1, shell.Height - 2 * padding));

			const int columns = 2;
			var rows = Math.Max(1, (actionCount + columns - 1) / columns);
			var cellWidth = Math.Max(1, (actionWidth - spacing) / columns);
			var fullRowWidth = columns * cellWidth + (columns - 1) * spacing;
			var detailInset = spacing;
			var detailGap = Math.Max(2, detailInset / 2);
			var actionTop = title.Bottom + detailGap;
			var actionBottom = shell.Height - padding - detailInset;
			var actionHeight = Math.Max(1, actionBottom - actionTop);
			var rowContentHeight = Math.Max(1, actionHeight - (rows - 1) * spacing);
			var buttons = new WidgetBounds[Math.Max(0, actionCount)];
			for (var i = 0; i < buttons.Length; i++)
			{
				var column = i % columns;
				var row = i / columns;
				var rowStart = row * rowContentHeight / rows;
				var rowEnd = (row + 1) * rowContentHeight / rows;
				var isOddLastButton = i == buttons.Length - 1 && buttons.Length % columns != 0;
				buttons[i] = new WidgetBounds(padding + column * (cellWidth + spacing),
					actionTop + rowStart + row * spacing, isOddLastButton ? fullRowWidth : cellWidth,
					Math.Max(1, rowEnd - rowStart));
			}

			return new AndroidPauseMenuLayout(shell, title, info, buttons);
		}
	}
}
