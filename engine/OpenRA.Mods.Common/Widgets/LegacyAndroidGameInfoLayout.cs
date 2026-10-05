using System;
using OpenRA.Primitives;
using OpenRA.Widgets;
namespace OpenRA.Mods.Common.Widgets.Logic
{
	public sealed class AndroidGameInfoLayout
	{
		public WidgetBounds TitleBounds { get; }
		public WidgetBounds[] TabBounds { get; }
		public WidgetBounds PanelBounds { get; }

		AndroidGameInfoLayout(WidgetBounds titleBounds, WidgetBounds[] tabBounds, WidgetBounds panelBounds)
		{
			TitleBounds = titleBounds;
			TabBounds = tabBounds;
			PanelBounds = panelBounds;
		}

		public static AndroidGameInfoLayout Compute(int width, int height, int padding,
			int gap, int minimumTarget, int tabCount)
		{
			var safeWidth = Math.Max(1, width);
			var safeHeight = Math.Max(1, height);
			var inset = Math.Max(0, padding);
			var spacing = Math.Max(0, gap);
			var target = Math.Max(1, minimumTarget);
			var contentWidth = Math.Max(1, safeWidth - 2 * inset);
			var title = new WidgetBounds(inset, 0, contentWidth, target);
			var tabsTop = title.Bottom + spacing;
			var tabs = new WidgetBounds[Math.Max(0, tabCount)];
			var tabWidth = tabs.Length == 0
				? contentWidth
				: Math.Max(1, (contentWidth - (tabs.Length - 1) * spacing) / tabs.Length);

			for (var i = 0; i < tabs.Length; i++)
			{
				var x = inset + i * (tabWidth + spacing);
				var right = i == tabs.Length - 1 ? safeWidth - inset : x + tabWidth;
				tabs[i] = new WidgetBounds(x, tabsTop, Math.Max(1, right - x), target);
			}

			var panelTop = tabsTop + (tabs.Length > 0 ? target + spacing : 0);
			var panel = new WidgetBounds(inset, panelTop, contentWidth,
				Math.Max(1, safeHeight - inset - panelTop));
			return new AndroidGameInfoLayout(title, tabs, panel);
		}
	}

	/// <summary>Shared touch grid used by the objectives, options and debug
	/// pages inside the phone in-game menu.</summary>
	public static class AndroidGameInfoTouchLayout
	{
		public static WidgetBounds[] ComputeGrid(int width, int padding, int gap,
			int minimumTarget, int columns, int count, int top)
		{
			var safeWidth = Math.Max(1, width);
			var inset = Math.Max(0, padding);
			var spacing = Math.Max(0, gap);
			var target = Math.Max(1, minimumTarget);
			var columnCount = Math.Max(1, columns);
			var itemCount = Math.Max(0, count);
			var contentWidth = Math.Max(1, safeWidth - 2 * inset);
			var cellWidth = Math.Max(1,
				(contentWidth - (columnCount - 1) * spacing) / columnCount);
			var bounds = new WidgetBounds[itemCount];
			for (var i = 0; i < itemCount; i++)
			{
				var column = i % columnCount;
				var row = i / columnCount;
				var x = inset + column * (cellWidth + spacing);
				var right = column == columnCount - 1 ? safeWidth - inset : x + cellWidth;
				bounds[i] = new WidgetBounds(x, top + row * (target + spacing),
					Math.Max(1, right - x), target);
			}

			return bounds;
		}
	}
}
