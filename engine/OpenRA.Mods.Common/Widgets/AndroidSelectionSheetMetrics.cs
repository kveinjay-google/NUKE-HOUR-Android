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
using OpenRA.MobileUi;
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Mods.Common.Widgets
{
	/// <summary>
	/// Pure geometry model for the Android phone "Mobile Selection Sheet"
	/// (the phone replacement for the desktop anchored DropDown popup).
	///
	/// There is one source of truth for list metrics:
	///
	///   contentHeight = 2 * topBottomSpacing + rowCount * rowHeight
	///                   + (rowCount - 1) * itemSpacing
	///   uniformRowTop(listTop, offset, index) = listTop + offset + topBottomSpacing
	///                   + index * rowHeight + index * itemSpacing
	///   itemTop(listTop, offset, itemContentTop) = listTop + offset + itemContentTop
	///
	/// The same values feed the ScrollPanel layout (children are added with
	/// their final bounds before ListLayout runs) and every touch hit test, so
	/// what the user sees and what receives the touch can never diverge. All
	/// inputs are UI logical pixels converted once per sheet open through
	/// MobileUiService (dp -&gt; UI px); nothing is recomputed per frame.
	/// </summary>
	public static class AndroidSelectionSheetMetrics
	{
		// Spec: sheet width 45%-70% of the usable screen, never wider than 420dp.
		public const float WidthFractionMin = 0.45f;
		public const float WidthFractionPreferred = 0.7f;
		public const int MaxWidthDp = 420;

		// Spec: sheet height never above 80% of the safe content area.
		public const float MaxHeightFraction = 0.96f;

		// dp sizes for the sheet's own chrome.
		public const float HeaderHeightDp = 52;
		public const float RowHeightDp = 56;
		public const float HeaderRowHeightDp = 44;
		public const float TopBottomSpacingDp = 10;
		public const float ItemSpacingDp = 6;
		public const float HorizontalPaddingDp = 14;
		public const float CloseSizeDp = 48;

		/// <summary>Row/metric sizes, converted to UI pixels for the current surface.</summary>
		public readonly struct Layout
		{
			public readonly int PanelWidth;
			public readonly int MaxPanelHeight;
			public readonly int HeaderHeight;
			public readonly int RowHeight;
			public readonly int HeaderRowHeight;
			public readonly int TopBottomSpacing;
			public readonly int ItemSpacing;
			public readonly int HorizontalPadding;
			public readonly int CloseSize;

			public Layout(
				int panelWidth, int maxPanelHeight, int headerHeight, int rowHeight, int headerRowHeight,
				int topBottomSpacing, int itemSpacing, int horizontalPadding, int closeSize)
			{
				PanelWidth = panelWidth;
				MaxPanelHeight = maxPanelHeight;
				HeaderHeight = headerHeight;
				RowHeight = rowHeight;
				HeaderRowHeight = headerRowHeight;
				TopBottomSpacing = topBottomSpacing;
				ItemSpacing = itemSpacing;
				HorizontalPadding = horizontalPadding;
				CloseSize = closeSize;
			}

			/// <summary>Total content height for a uniform-height list with <paramref name="rowCount"/> rows.</summary>
			public int ContentHeight(int rowCount)
			{
				if (rowCount <= 0)
					return 0;

				return 2 * TopBottomSpacing + rowCount * RowHeight + (rowCount - 1) * ItemSpacing;
			}

			/// <summary>Absolute Y of row <paramref name="index"/> for a uniform-height list.</summary>
			public int RowTop(int listTop, int scrollOffset, int index)
			{
				return RowTopForLog(listTop, scrollOffset, TopBottomSpacing, RowHeight, ItemSpacing, index);
			}

			/// <summary>Smallest (most negative) list offset that shows the last row fully.</summary>
			public int ScrollOffsetBottom(int contentHeight)
			{
				return Math.Min(0, MaxPanelHeight - contentHeight);
			}
		}

		/// <summary>True on Android phones/foldables when the phone UI service is live.</summary>
		public static bool UseOnThisPlatform
		{
			get
			{
				var service = MobileUiService.Instance;
				return Platform.IsAndroid && service.IsEnabledMobile && PhoneInput.PhoneTouchMode;
			}
		}

		/// <summary>Build the pixel layout from the cached MobileUiService values.</summary>
		public static Layout ComputeLayout(MobileUiService service)
		{
			return ComputeLayout(service.UsableWidthDp, service.UsableHeightDp, service.DpToUi(1));
		}

		/// <summary>
		/// Pure geometry builder (no engine state). <paramref name="dpToUi"/> is
		/// the dp-to-UI-pixel ratio (density / uiScale).
		/// </summary>
		public static Layout ComputeLayout(float usableWidthDp, float usableHeightDp, float dpToUi)
		{
			var scale = Math.Max(0.0001f, dpToUi);
			var usableW = Math.Max(0f, usableWidthDp);
			var usableH = Math.Max(0f, usableHeightDp);
			var widthDp = Math.Max(
				Math.Min(usableW * WidthFractionMin, MaxWidthDp),
				Math.Min(usableW * WidthFractionPreferred, MaxWidthDp));
			var panelWidth = Math.Max(1, (int)Math.Round(widthDp * scale));
			var maxHeight = Math.Max(1, (int)Math.Round(usableH * MaxHeightFraction * scale));

			return new Layout(
				panelWidth,
				maxHeight,
				Math.Max(1, (int)Math.Round(HeaderHeightDp * scale)),
				Math.Max(1, (int)Math.Round(RowHeightDp * scale)),
				Math.Max(1, (int)Math.Round(HeaderRowHeightDp * scale)),
				Math.Max(1, (int)Math.Round(TopBottomSpacingDp * scale)),
				Math.Max(1, (int)Math.Round(ItemSpacingDp * scale)),
				Math.Max(1, (int)Math.Round(HorizontalPaddingDp * scale)),
				Math.Max(1, (int)Math.Round(CloseSizeDp * scale)));
		}

		/// <summary>Uniform-height index formula (see class comment).</summary>
		public static int RowTopForLog(int listTop, int scrollOffset, int topBottomSpacing, int rowHeight, int itemSpacing, int index)
		{
			return listTop + scrollOffset + topBottomSpacing + index * rowHeight + index * itemSpacing;
		}

		/// <summary>Absolute top of a list child whose content-space top is <paramref name="itemContentTop"/>.</summary>
		public static int ItemTop(int listTop, int scrollOffset, int itemContentTop)
		{
			return listTop + scrollOffset + itemContentTop;
		}

		/// <summary>
		/// Reflows one option row to the sheet geometry before it is added to
		/// the list: the row fills the content width and every known child is
		/// repositioned from the same padding/row-height values used for hit
		/// testing. No child is touched after AddChild.
		/// </summary>
		public static void PrepareRow(Widget row, Layout layout, bool isHeader)
		{
			var contentWidth = Math.Max(0, layout.PanelWidth - 2 * layout.HorizontalPadding);
			var rowHeight = isHeader ? layout.HeaderRowHeight : layout.RowHeight;
			var leftMargin = layout.HorizontalPadding;
			row.Bounds = new WidgetBounds(0, 0, contentWidth, rowHeight);

			Widget flag = null;
			foreach (var child in row.Children)
				if (child.Id == "FLAG" && child is ImageWidget)
					flag = child;

			var labelX = leftMargin;
			if (flag != null)
			{
				var flagWidth = Math.Min(flag.Bounds.Width, rowHeight);
				var flagHeight = Math.Min(flag.Bounds.Height, rowHeight);
				flag.Bounds = new WidgetBounds(
					leftMargin,
					Math.Max(0, (rowHeight - flagHeight) / 2),
					flagWidth,
					flagHeight);
				if (flag is ImageWidget image)
					image.StretchToFit = true;

				labelX += flagWidth + layout.ItemSpacing;
			}

			foreach (var label in row.Children.OfType<LabelWidget>())
			{
				if (label.Id == "LABEL" || label.Parent == row)
				{
					label.Bounds = new WidgetBounds(
						labelX,
						0,
						Math.Max(0, contentWidth - labelX - layout.HorizontalPadding),
						rowHeight);
					label.VAlign = TextVAlign.Middle;
				}
			}

			// Non-text children (tooltip regions, images) must not swallow touches.
			foreach (var child in row.Children)
				if (child != null && child.Id != "LABEL" && child.Id != "FLAG")
					child.IgnoreMouseOver = true;
		}
	}
}
