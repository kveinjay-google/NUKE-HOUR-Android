using System;
using System.Linq;
using OpenRA.Widgets;

namespace OpenRA.Mods.Common.Widgets.Logic
{
	public sealed class LobbyOptionPickerLayout
	{
		public int Width { get; }
		public int Height { get; }
		public int ContentHeight { get; }
		public int Columns { get; }
		public int CellWidth { get; }
		public int CellHeight { get; }
		public int Gap { get; }
		public int Scrollbar { get; }

		public LobbyOptionPickerLayout(IosScreenSnapshot screen, int count)
		{
			Gap = screen.LogicalPoints(12);
			Scrollbar = screen.LogicalPoints(48);
			Width = Math.Max(1, Math.Min(screen.LogicalPoints(720), screen.SafeBounds.Width - 2 * Gap));
			Columns = Width >= screen.LogicalPoints(600) ? 3 : Width >= screen.LogicalPoints(360) ? 2 : 1;
			CellWidth = Math.Max(1, (Width - Scrollbar - (Columns + 1) * Gap) / Columns);
			CellHeight = screen.LogicalPoints(64);
			var rows = Math.Max(1, (count + Columns - 1) / Columns);
			ContentHeight = rows * CellHeight + (rows + 1) * Gap;
			Height = Math.Max(1, Math.Min(ContentHeight,
				Math.Min(3 * CellHeight + 4 * Gap, screen.SafeBounds.Height - 2 * Gap)));
		}

		public static void Prepare(ScrollPanelWidget panel, IosScreenSnapshot screen, bool touch)
		{
			var items = panel.Children.OfType<ScrollItemWidget>().ToArray();
			var layout = new LobbyOptionPickerLayout(screen, items.Length);
			panel.RemoveChildren();
			panel.Bounds.Width = layout.Width;
			panel.Bounds.Height = layout.Height;
			panel.ScrollbarWidth = layout.Scrollbar;
			panel.TopBottomSpacing = panel.ItemSpacing = layout.Gap;
			panel.EnableContentDragging = true;
			panel.ContentDragThreshold = screen.LogicalPoints(8);
			panel.Layout = new GridLayout(panel);
			foreach (var item in items)
			{
				item.Bounds = new WidgetBounds(0, 0, layout.CellWidth, layout.CellHeight);
				foreach (var label in item.Children.OfType<LabelWidget>())
				{
					label.Bounds = new WidgetBounds(layout.Gap, 0, layout.CellWidth - 2 * layout.Gap, layout.CellHeight);
					label.Align = TextAlign.Center;
					label.Font = touch ? "IosBold" : "SettingsBold";
					var originalText = label.GetText();
					label.GetText = CampaignBrowserLayout.CreateTitleGetter(() => originalText,
						() => label.Bounds.Width, text => Game.Renderer.Fonts[label.Font].Measure(text).X);
				}
				panel.AddChild(item);
			}
			LobbyLogic.ApplySovietLobbyStyle(panel, touch);
		}
	}
}
