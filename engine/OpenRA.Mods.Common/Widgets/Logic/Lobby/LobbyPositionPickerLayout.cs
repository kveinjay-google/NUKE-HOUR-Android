using System;
using System.Linq;
using OpenRA.Widgets;

namespace OpenRA.Mods.Common.Widgets.Logic
{
	public sealed class LobbyPositionPickerLayout
	{
		public int Width { get; }
		public int Height { get; }
		public int ContentHeight { get; }
		public int ItemHeight { get; }
		public int Gap { get; }

		public LobbyPositionPickerLayout(IosScreenSnapshot screen, int count)
		{
			Gap = screen.LogicalPoints(6);
			ItemHeight = screen.LogicalPoints(56);
			Width = Math.Max(1, Math.Min(screen.LogicalPoints(340), screen.SafeBounds.Width - 2 * Gap));
			ContentHeight = count * (ItemHeight + Gap) + Gap;
			Height = Math.Max(1, Math.Min(ContentHeight,
				Math.Min(4 * ItemHeight + 5 * Gap, screen.SafeBounds.Height - 2 * Gap)));
		}

		public static void Prepare(ScrollPanelWidget panel, IosScreenSnapshot screen, bool touch)
		{
			var items = panel.Children.OfType<ScrollItemWidget>().ToArray();
			var layout = new LobbyPositionPickerLayout(screen, items.Length);
			panel.RemoveChildren();
			panel.Bounds.Width = layout.Width;
			panel.Bounds.Height = layout.Height;
			panel.ScrollBar = ScrollBar.Hidden;
			panel.ScrollbarWidth = 0;
			panel.TopBottomSpacing = panel.ItemSpacing = layout.Gap;
			panel.EnableContentDragging = true;
			panel.ContentDragThreshold = screen.LogicalPoints(8);
			panel.Layout = new ListLayout(panel);
			foreach (var item in items)
			{
				var header = item.Id == "HEADER";
				var itemHeight = header ? screen.LogicalPoints(30) : layout.ItemHeight;
				item.Bounds = new WidgetBounds(0, 0, layout.Width, itemHeight);
				foreach (var label in item.Children.OfType<LabelWidget>())
				{
					label.Bounds = new WidgetBounds(layout.Gap * 2, 0,
						layout.Width - 4 * layout.Gap, itemHeight);
					label.Align = TextAlign.Center;
					label.Font = touch ? "IosBold" : "SettingsBold";
				}

				panel.AddChild(item);
			}

			panel.Bounds.Height = Math.Min(panel.ContentHeight, layout.Height);
			LobbyLogic.ApplySovietLobbyStyle(panel, touch);
			foreach (var header in items.Where(item => item.Id == "HEADER"))
				header.Background = "";
		}
	}
}
