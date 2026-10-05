using System;
using System.Linq;
using OpenRA.Widgets;

namespace OpenRA.Mods.Common.Widgets.Logic
{
	// Compact touch-safe choices for the RA2 player roster.
	public sealed class LobbyRosterPickerLayout
	{
		public int Width { get; }
		public int Height { get; }
		public int ContentHeight { get; }
		public int Columns { get; }
		public int Rows { get; }
		public int CellWidth { get; }
		public int CellHeight { get; }
		public int EmblemSize { get; }
		public int Gap { get; }
		public bool NeedsScrolling { get; }

		public LobbyRosterPickerLayout(IosScreenSnapshot screen, int count, bool factions, int preferredWidth = 0)
		{
			Gap = screen.LogicalPoints(6);
			var preferredColumns = factions ? 5 : 1;
			var minimumCellWidth = screen.LogicalPoints(factions ? 88 : 68);
			var preferredCellWidth = screen.LogicalPoints(factions ? 96 : 76);
			var maximumWidth = screen.LogicalPoints(factions ? 540 : 720);
			if (!factions)
				maximumWidth = Math.Max(1, preferredWidth > 0 ? preferredWidth : screen.LogicalPoints(150));

			var availableWidth = Math.Max(1, Math.Min(maximumWidth, screen.SafeBounds.Width - 2 * Gap));
			Columns = preferredColumns;
			while (Columns > 1 &&
				(availableWidth - (Columns + 1) * Gap) / Columns < minimumCellWidth)
				Columns--;

			var calculatedWidth = factions ? Columns * preferredCellWidth + (Columns + 1) * Gap : maximumWidth;
			Width = Math.Max(1, Math.Min(availableWidth, calculatedWidth));
			CellWidth = Math.Max(1, (Width - (Columns + 1) * Gap) / Columns);
			CellHeight = screen.LogicalPoints(factions ? 60 : 52);
			EmblemSize = Math.Max(1, Math.Min(CellWidth - 2 * Gap, screen.LogicalPoints(28)));
			Rows = Math.Max(1, (count + Columns - 1) / Columns);
			ContentHeight = Rows * CellHeight + (Rows + 1) * Gap;
			Height = Math.Max(1, Math.Min(ContentHeight, screen.SafeBounds.Height - 2 * Gap));
			NeedsScrolling = ContentHeight > Height;
		}

		public static void Prepare(ScrollPanelWidget panel, IosScreenSnapshot screen, bool touch, bool factions,
			int preferredWidth = 0)
		{
			var items = panel.Children.OfType<ScrollItemWidget>().ToArray();
			var layout = new LobbyRosterPickerLayout(screen, items.Length, factions, preferredWidth);
			panel.RemoveChildren();
			panel.Bounds.Width = layout.Width;
			panel.Bounds.Height = layout.Height;
			panel.ScrollBar = ScrollBar.Hidden;
			panel.ScrollbarWidth = 0;
			panel.TopBottomSpacing = panel.ItemSpacing = layout.Gap;
			panel.EnableContentDragging = layout.NeedsScrolling;
			panel.ContentDragThreshold = screen.LogicalPoints(8);
			panel.Layout = new GridLayout(panel);
			foreach (var item in items)
			{
				item.Bounds = new WidgetBounds(0, 0, layout.CellWidth, layout.CellHeight);
				foreach (var image in item.Children.OfType<ImageWidget>())
				{
					image.Bounds = new WidgetBounds((layout.CellWidth - layout.EmblemSize) / 2,
						layout.Gap / 2, layout.EmblemSize, layout.EmblemSize);
					image.StretchToFit = true;
				}

				foreach (var label in item.Children.OfType<LabelWidget>())
				{
					label.Bounds = factions ?
						new WidgetBounds(layout.Gap, layout.EmblemSize + layout.Gap / 2,
							layout.CellWidth - 2 * layout.Gap, layout.CellHeight - layout.EmblemSize - layout.Gap / 2) :
						new WidgetBounds(layout.Gap, 0, layout.CellWidth - 2 * layout.Gap, layout.CellHeight);
					label.Align = TextAlign.Center;
					label.Font = touch ? "IosBold" : "SettingsBold";
					var getText = label.GetText;
					label.GetText = CampaignBrowserLayout.CreateTitleGetter(getText,
						() => label.Bounds.Width, text => Game.Renderer.Fonts[label.Font].Measure(text).X);
				}

				panel.AddChild(item);
			}

			LobbyLogic.ApplySovietLobbyStyle(panel, touch);
		}
	}
}
