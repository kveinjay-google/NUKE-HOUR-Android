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
using System.Linq;
using System.Text;
using OpenRA.Graphics;
using OpenRA.MobileUi;
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Mods.Common.Widgets
{
	/// <summary>
	/// Android phone modal replacement for the desktop anchored DropDown popup.
	/// A full-screen backdrop plus a centered scrollable option list; it opens
	/// as the topmost child of Ui.Root so the lobby below never receives input
	/// while it is open. Closing is triggered by tapping the backdrop, tapping
	/// the close glyph, pressing Escape, or picking an option.
	///
	/// Row geometry, scrolling and touch hit-testing all come from
	/// <see cref="AndroidSelectionSheetMetrics"/>, so the visual row bounds and
	/// the event row bounds are one and the same.
	/// </summary>
	public sealed class MobileSelectionSheetWidget : Widget
	{
		public static bool UseOnThisPlatform => AndroidSelectionSheetMetrics.UseOnThisPlatform;

#if DEBUG
		/// <summary>Currently open sheet (debug autotap driver).</summary>
		public static MobileSelectionSheetWidget Current;

		/// <summary>Scrolls until the given row is visible but does not select it (debug capture aid).</summary>
		public void DebugScrollRow(int index)
		{
			var count = list.Children.Count;
			if (index < 0 || index >= count || !list.Children[index].IsVisible())
				return;

			var child = list.Children[index];
			var cx = listBounds.X + Math.Max(1, listBounds.Width / 2);
			var dyTotal = 0;
			if (list.ContentHeight > listBounds.Height)
			{
				var rowTop = AndroidSelectionSheetMetrics.ItemTop(listBounds.Y, list.ListOffset, child.Bounds.Y);
				if (rowTop + child.Bounds.Height > listBounds.Bottom + layout.ItemSpacing)
					dyTotal = listBounds.Bottom - (rowTop + child.Bounds.Height) - layout.ItemSpacing;
				else if (rowTop < listBounds.Y - layout.ItemSpacing)
					dyTotal = listBounds.Y - rowTop + layout.ItemSpacing;
			}

			if (dyTotal != 0)
				DebugDragBy(cx, dyTotal);

			Log.Write("debug", $"autotap sheetrowscroll index={index} offset={list.ListOffset} dy={dyTotal}");
		}

		/// <summary>Simulates a real tap through Ui.HandleInput on the sheet row index.</summary>
		public void DebugTapRow(int index)
		{
			var count = list.Children.Count;
			if (index < 0 || index >= count || !list.Children[index].IsVisible())
				return;

			var child = list.Children[index];
			var cx = listBounds.X + Math.Max(1, listBounds.Width / 2);

			if (list.ContentHeight > listBounds.Height)
			{
				var rowTop = AndroidSelectionSheetMetrics.ItemTop(listBounds.Y, list.ListOffset, child.Bounds.Y);
				var dyTotal = 0;
				if (rowTop + child.Bounds.Height > listBounds.Bottom + layout.ItemSpacing)
					dyTotal = listBounds.Bottom - (rowTop + child.Bounds.Height) - layout.ItemSpacing;
				else if (rowTop < listBounds.Y - layout.ItemSpacing)
					dyTotal = listBounds.Y - rowTop + layout.ItemSpacing;

				if (dyTotal != 0)
					DebugDragBy(cx, dyTotal);
			}

			var finalRowTop = AndroidSelectionSheetMetrics.ItemTop(listBounds.Y, list.ListOffset, child.Bounds.Y);
			var cy = finalRowTop + child.Bounds.Height / 2;
			DebugTap(cx, cy);
			Log.Write("debug", $"autotap sheetrow index={index} at ui ({cx},{cy}) rowTop={finalRowTop} offset={list.ListOffset}");
		}

		static void DebugTap(int x, int y)
		{
			var pos = new int2(x, y);
			Ui.HandleInput(new MouseInput(MouseInputEvent.Down, MouseButton.Left, pos, int2.Zero, Modifiers.None, 0));
			Ui.HandleInput(new MouseInput(MouseInputEvent.Up, MouseButton.Left, pos, int2.Zero, Modifiers.None, 0));
		}

		void DebugDragBy(int cx, int dyTotal)
		{
			var startY = listBounds.Y + Math.Max(1, listBounds.Height / 2);
			var start = new int2(cx, startY);
			var midY = startY + dyTotal / 2;
			Ui.HandleInput(new MouseInput(MouseInputEvent.Down, MouseButton.Left, start, int2.Zero, Modifiers.None, 0));
			Ui.HandleInput(new MouseInput(MouseInputEvent.Move, MouseButton.Left, new int2(cx, midY), int2.Zero, Modifiers.None, 0));
			Ui.HandleInput(new MouseInput(MouseInputEvent.Move, MouseButton.Left, new int2(cx, startY + dyTotal), int2.Zero, Modifiers.None, 0));
			Ui.HandleInput(new MouseInput(MouseInputEvent.Up, MouseButton.Left, new int2(cx, startY + dyTotal), int2.Zero, Modifiers.None, 0));
		}
#endif

		readonly string title;
		readonly AndroidSelectionSheetMetrics.Layout layout;
		readonly ScrollPanelWidget list;
		readonly int closeSize;

		Rectangle safeBounds;
		Rectangle panelRect;
		Rectangle closeRect;
		Rectangle titleRect;
		Rectangle listBounds;
		bool closing;

		MobileSelectionSheetWidget(ModData modData, string title, AndroidSelectionSheetMetrics.Layout layout)
		{
			this.title = title;
			this.layout = layout;
			closeSize = Math.Max(1, layout.CloseSize);

			Visible = true;
			Bounds = new WidgetBounds(0, 0, Math.Max(1, Game.Renderer.Resolution.Width), Math.Max(1, Game.Renderer.Resolution.Height));

			list = new ScrollPanelWidget(modData)
			{
				TopBottomSpacing = layout.TopBottomSpacing,
				ItemSpacing = layout.ItemSpacing,
				ScrollBar = ScrollBar.Hidden,
				ScrollbarWidth = 0,
				EnableContentDragging = true
			};

			var service = MobileUiService.Instance;
			list.ContentDragThreshold = Math.Max(1, (int)Math.Round(service.DpToUi(8)));
		}

		public override Widget Clone()
		{
			throw new InvalidOperationException("Mobile selection sheets are not cloneable");
		}

		public static MobileSelectionSheetWidget Create(
			ModData modData, string title, AndroidSelectionSheetMetrics.Layout layout)
		{
			return new MobileSelectionSheetWidget(modData, title, layout);
		}

		public int RowCount => list.Children.Count;

		/// <summary>True when options are queued for the next Open() call - in grid
		/// mode items are staged in the grid and only become list rows during
		/// Open(), so this must include the pending grid items.</summary>
		public bool HasPendingOptions => list.Children.Count > 0 || (gridEnabled && gridItems.Count > 0);

		public ScrollPanelWidget List => list;

		bool gridEnabled;
		int gridColumnsCap;
		readonly System.Collections.Generic.List<Widget> gridItems = new();
		int gridCellHeight;
		int gridCellWidth;

		public void EnableGrid(int columns)
		{
			gridEnabled = true;
			gridColumnsCap = Math.Max(1, columns);
			gridItems.Clear();
		}

		/// <summary>Option cells are grouped into wrapping lines by ArrangeGrid at open time.</summary>
		void AddToGrid(ScrollItemWidget item)
		{
			gridItems.Add(item);
		}

		static void PrepareGridCell(ScrollItemWidget item, AndroidSelectionSheetMetrics.Layout layout, int cellWidth, int cellHeight)
		{
			Widget flag = null;
			foreach (var child in item.Children)
				if (child.Id == "FLAG")
					flag = child;

			item.Bounds = new WidgetBounds(0, 0, cellWidth, cellHeight);
			foreach (var label in item.Children.OfType<LabelWidget>())
			{
				if (label.Id == "LABEL")
				{
					label.Font = "Bold";
					if (flag == null)
					{
						label.Bounds = new WidgetBounds(0, 0, cellWidth, cellHeight);
						label.Align = TextAlign.Center;
					}
					else
					{
						flag.Bounds = new WidgetBounds(6, Math.Max(0, (cellHeight - flag.Bounds.Height) / 2),
							flag.Bounds.Width, Math.Min(flag.Bounds.Height, cellHeight));
						label.Bounds = new WidgetBounds(
							flag.Bounds.Width + 10, 0,
							Math.Max(0, cellWidth - flag.Bounds.Width - 16),
							cellHeight);
						label.Align = TextAlign.Left;
					}

					label.VAlign = TextVAlign.Middle;
				}
			}
		}

		/// <summary>
		/// Lays all pending grid cells out so that as many fit on screen as
		/// possible (bigger cells, taller popup, bold labels). Header rows are
		/// dropped in grid mode to maximise the number of visible options.
		/// </summary>
		void ArrangeGrid()
		{
			if (gridItems.Count == 0)
				return;

			var service = MobileUiService.Instance;
			var contentWidth = Math.Max(1, layout.PanelWidth - 2 * layout.HorizontalPadding);
			var spacing = layout.ItemSpacing;
			var minCellWidth = Math.Max(1, (int)Math.Round(service.DpToUi(88)));
			var cellHeight = Math.Max(layout.RowHeight, (int)Math.Round(service.DpToUi(62)));
			var columnsCap = Math.Max(1, (contentWidth + spacing) / Math.Max(1, minCellWidth + spacing));
			if (gridColumnsCap > 0)
				columnsCap = Math.Min(columnsCap, gridColumnsCap);
			var margin = Math.Max(1, (int)Math.Round(service.DpToUi(12)));
			var availableHeight = Math.Max(1,
				safeBounds.Height - 2 * margin - layout.HeaderHeight);
			var rowsAvailable = Math.Max(1,
				availableHeight / Math.Max(1, cellHeight + spacing));

			var columns = columnsCap;
			for (var c = columnsCap; c >= 1; c--)
			{
				var rows = (int)Math.Ceiling(gridItems.Count / (float)Math.Max(1, c));
				if (rows <= rowsAvailable)
					columns = c;
			}

			gridCellHeight = cellHeight;
			gridCellWidth = Math.Max(1, (contentWidth - (columns - 1) * spacing) / columns);

			ContainerWidget line = null;
			var column = 0;
			foreach (var item in gridItems)
			{
				if (line == null || column >= columns)
				{
					line = new ContainerWidget
					{
						Bounds = new WidgetBounds(0, 0, contentWidth, cellHeight)
					};
					list.AddChild(line);
					column = 0;
				}

				column++;
				var widget = item as ScrollItemWidget;
				if (widget == null)
					continue;

				widget.Bounds = new WidgetBounds(
					(column - 1) * (gridCellWidth + spacing), 0, gridCellWidth, cellHeight);
				PrepareGridCell(widget, layout, gridCellWidth, cellHeight);
				line.AddChild(widget);
			}

			gridItems.Clear();
		}

		/// <summary>Adds one selectable option (already bound by the caller).</summary>
		public void AddItemRow(ScrollItemWidget item)
		{
			item.IsVisible = () => true;
			item.IsDisabled = () => false;

			var onClick = item.OnClick;
			item.OnClick = () =>
			{
#if DEBUG
				LogTouchSelection(item, list.Children.Count);
#endif
				onClick();
				Close();
			};

			if (gridEnabled)
			{
				AddToGrid(item);
				return;
			}

			AndroidSelectionSheetMetrics.PrepareRow(item, layout, false);
			list.AddChild(item);
		}

		/// <summary>Adds a non-interactive group header row (dropped in grid mode).</summary>
		public void AddHeaderRow(ScrollItemWidget header)
		{
			if (header == null)
				return;

			header.IsVisible = () => true;
			header.IsDisabled = () => true;
			header.IsSelected = () => false;
			header.OnClick = () => { };

			if (gridEnabled)
				return;

			AndroidSelectionSheetMetrics.PrepareRow(header, layout, true);
			list.AddChild(header);
		}

		public void Open()
		{
			if (Parent != null)
				return;

			var service = MobileUiService.Instance;
			safeBounds = service.LogicalSafeBounds;
			if (safeBounds.Width <= 0 || safeBounds.Height <= 0)
				safeBounds = new Rectangle(0, 0, Game.Renderer.Resolution.Width, Game.Renderer.Resolution.Height);

			if (gridEnabled)
				ArrangeGrid();

			var margin = Math.Max(1, (int)service.DpToUi(12));
			var listWidth = Math.Max(1, layout.PanelWidth - 2 * layout.HorizontalPadding);
			var maxListHeight = Math.Max(1,
				Math.Min(
					layout.MaxPanelHeight - layout.HeaderHeight,
					safeBounds.Height - 2 * margin - layout.HeaderHeight));

			var contentHeight = list.ContentHeight;
			var listHeight = Math.Min(contentHeight, maxListHeight);
			if (listHeight < layout.RowHeight)
				listHeight = Math.Min(layout.RowHeight, maxListHeight);

			var panelHeight = layout.HeaderHeight + listHeight;
			panelHeight = Math.Min(panelHeight, Math.Max(1, safeBounds.Height - 2 * margin));
			panelHeight = Math.Max(panelHeight, layout.HeaderHeight + Math.Min(layout.RowHeight, maxListHeight));

			var x = safeBounds.X + Math.Max(0, (safeBounds.Width - layout.PanelWidth) / 2);
			var y = safeBounds.Y + Math.Max(0, (safeBounds.Height - panelHeight) / 2);

			panelRect = new Rectangle(x, y, layout.PanelWidth, panelHeight);
			titleRect = new Rectangle(
				x + layout.HorizontalPadding, y,
				Math.Max(0, layout.PanelWidth - 2 * layout.HorizontalPadding - closeSize),
				layout.HeaderHeight);
			closeRect = new Rectangle(
				x + layout.PanelWidth - closeSize - layout.HorizontalPadding, y,
				closeSize, layout.HeaderHeight);
			listBounds = new Rectangle(x + layout.HorizontalPadding, y + layout.HeaderHeight, listWidth, listHeight);
			list.Bounds = new WidgetBounds(listBounds.X, listBounds.Y, listBounds.Width, listBounds.Height);

			// Finger-sized right-edge scrollbar.
			list.ScrollbarWidth = Math.Max(list.ScrollbarWidth, Math.Max(1, (int)service.RoundDpToUi(32)));
			list.MinimumThumbSize = Math.Max(list.MinimumThumbSize, Math.Max(1, (int)service.RoundDpToUi(48)));

			Bounds = new WidgetBounds(0, 0, Math.Max(1, Game.Renderer.Resolution.Width), Math.Max(1, Game.Renderer.Resolution.Height));
			AddChild(list);

			Ui.Root.AddChild(this);
			Ui.ResetTooltips();
			OpenRA.MobileUi.PhoneBackState.ModalOpen = true;
#if DEBUG
			Current = this;
			Log.Write("debug", $"mobile-sheet opened: title='{title}' rows={RowCount} listBounds=({listBounds.X},{listBounds.Y} {listBounds.Width}x{listBounds.Height}) contentHeight={list.ContentHeight} panel=({panelRect.X},{panelRect.Y} {panelRect.Width}x{panelRect.Height}) rowHeight={layout.RowHeight} topBottom={layout.TopBottomSpacing} itemSpacing={layout.ItemSpacing}");
#endif
		}

		public void Close()
		{
			if (closing || Parent == null)
				return;

			closing = true;
			Ui.Root.RemoveChild(this);
			Ui.ResetTooltips();
			OpenRA.MobileUi.PhoneBackState.ModalOpen = false;
#if DEBUG
			Current = null;
#endif
			closing = false;
		}

		public override void Tick()
		{
			// Android hardware back: the activity flags Pressed while the sheet
			// is open; consume it here and dismiss like an Escape press.
			if (OpenRA.MobileUi.PhoneBackState.Pressed)
			{
				OpenRA.MobileUi.PhoneBackState.Pressed = false;
#if DEBUG
				Log.Write("debug", "mobile-sheet closed by Android hardware back");
#endif
				Close();
			}
		}

		public override bool EventBoundsContains(int2 location)
		{
			// The modal owns the whole screen while it is open.
			return true;
		}

		public override bool HandleKeyPress(KeyInput e)
		{
			if ((e.Key == Keycode.ESCAPE || e.Key == Keycode.AC_BACK) && e.Event == KeyInputEvent.Down)
			{
#if DEBUG
				Log.Write("debug", "mobile-sheet closed by key " + e.Key);
#endif
				Close();
				return true;
			}

			return false;
		}

		public override bool HandleMouseInput(MouseInput mi)
		{
			if (mi.Event == MouseInputEvent.Down)
			{
				if (closeRect.Contains(mi.Location))
				{
					Close();
					return true;
				}

				if (!panelRect.Contains(mi.Location))
				{
					Close();
					return true;
				}

				// Inside the panel on empty space: keep the sheet open and eat
				// the press so nothing below the modal is ever activated.
				return true;
			}

			if (mi.Event == MouseInputEvent.Move)
				return false;

			// Any other event (release, scroll, cancel) is consumed while open.
			return true;
		}

		public override string GetCursor(int2 pos)
		{
			return null;
		}

		public override void DrawOuter()
		{
			if (!IsVisible())
				return;

			Draw();
			foreach (var child in Children)
				child.DrawOuter();

			DrawScrollLane();
		}

		public override void Draw()
		{
			var resolution = new Rectangle(0, 0, Game.Renderer.Resolution.Width, Game.Renderer.Resolution.Height);
			WidgetUtils.FillRectWithColor(resolution, Color.FromArgb(150, 0, 0, 0));

			WidgetUtils.FillRectWithColor(panelRect, Color.FromArgb(242, 20, 20, 26));
			DrawBorder(panelRect, Color.FromArgb(255, 150, 150, 160));

			if (!string.IsNullOrEmpty(title) && Game.Renderer.Fonts.TryGetValue("Bold", out var titleFont))
			{
				var text = WidgetUtils.TruncateText(title, Math.Max(0, titleRect.Width), titleFont);
				var size = titleFont.Measure(text);
				titleFont.DrawText(text, new float2(
					titleRect.X,
					titleRect.Y + Math.Max(0, (titleRect.Height - size.Y) / 2)), Color.White);
			}

			// Close glyph.
			if (Game.Renderer.Fonts.TryGetValue("Bold", out var closeFont))
			{
				var glyph = "X";
				var g = closeFont.Measure(glyph);
				WidgetUtils.FillRectWithColor(closeRect, Color.FromArgb(70, 255, 255, 255));
				DrawBorder(closeRect, Color.FromArgb(130, 255, 255, 255));
				closeFont.DrawText(glyph, new float2(
					closeRect.X + Math.Max(0, (closeRect.Width - g.X) / 2),
					closeRect.Y + Math.Max(0, (closeRect.Height - g.Y) / 2)), Color.White);
			}
		}

		// Drawn after the list so it overlays the content instead of hiding under it.
		void DrawScrollLane()
		{
			var lane = Math.Max(2, (int)Math.Round(MobileUiService.Instance.DpToUi(4)));
			if (list.ContentHeight <= listBounds.Height)
				return;

			var laneRect = new Rectangle(listBounds.Right - lane, listBounds.Y, lane, listBounds.Height);
			WidgetUtils.FillRectWithColor(laneRect, Color.FromArgb(90, 255, 255, 255));

			var ratio = listBounds.Height / (float)list.ContentHeight;
			var thumbHeight = Math.Min(listBounds.Height, Math.Max(lane * 2, (int)(listBounds.Height * ratio)));
			var travel = Math.Max(0, listBounds.Height - thumbHeight);
			var offsetRatio = list.ContentHeight > listBounds.Height
				? Math.Abs(list.ListOffset) / (float)Math.Max(1, list.ContentHeight - listBounds.Height)
				: 0f;
			var thumbY = listBounds.Y + Math.Min(travel, (int)(offsetRatio * travel));
			WidgetUtils.FillRectWithColor(new Rectangle(listBounds.Right - lane, thumbY, lane, thumbHeight),
				Color.FromArgb(255, 185, 185, 195));
		}

		static void DrawBorder(Rectangle bounds, Color color)
		{
			Game.Renderer.RgbaColorRenderer.DrawRect(
				new float3(bounds.Left, bounds.Top, 0),
				new float3(bounds.Right, bounds.Bottom, 0),
				1, color);
		}

#if DEBUG
		void LogTouchSelection(ScrollItemWidget item, int index)
		{
			var sb = new StringBuilder();
			sb.Append("mobile-sheet select: itemIndex=").Append(index)
				.Append(" rowHeight=").Append(layout.RowHeight)
				.Append(" scrollOffset=").Append(list.ListOffset)
				.Append(" contentHeight=").Append(list.ContentHeight)
				.Append(" rowTop=").Append(AndroidSelectionSheetMetrics.ItemTop(
					listBounds.Y, list.ListOffset, item.Bounds.Y));
			var label = item.GetOrNull<LabelWidget>("LABEL");
			if (label != null)
			{
				try
				{
					sb.Append(" label=").Append(label.GetText());
				}
				catch (Exception e)
				{
					sb.Append(" labelError=").Append(e.Message);
				}
			}

			Log.Write("debug", sb.ToString());
		}
#endif
	}
}
