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
using NUnit.Framework;
using OpenRA.Mods.Common.Widgets;

namespace OpenRA.Test
{
	[TestFixture]
	public class AndroidSelectionSheetMetricsTest
	{
		// (usableWidthDp, usableHeightDp) profiles from the phone UI contract.
		static readonly object[] ProfileCases =
		{
			new object[] { 800f, 360f },   // CompactPhoneLandscape
			new object[] { 891f, 411f },   // PhoneLandscape
			new object[] { 960f, 432f },   // PhoneLandscape
			new object[] { 960f, 540f },   // Foldable
			new object[] { 1280f, 800f }   // Tablet-sized
		};

		// Typical dp -> UI conversion ratios (density / uiScale).
		static readonly float[] DpToUiFactors = { 1f, 1.6f, 2.1875f, 3f };

		[TestCaseSource(nameof(ProfileCases))]
		public void LayoutRespectsWidthConstraints(float usableWidthDp, float usableHeightDp)
		{
			foreach (var factor in DpToUiFactors)
			{
				var layout = AndroidSelectionSheetMetrics.ComputeLayout(usableWidthDp, usableHeightDp, factor);

				Assert.That(layout.PanelWidth, Is.GreaterThan(0), "panel width must be positive");
				var onePxDp = 1f / factor; // pixel rounding may push the bound by one px
				var widthDp = layout.PanelWidth / factor;
				Assert.That(widthDp, Is.LessThanOrEqualTo(420f + onePxDp + 0.01f), "sheet width must not exceed 420dp");
				var floorDp = Math.Min(usableWidthDp * 0.45f, 420f);
				Assert.That(widthDp, Is.GreaterThanOrEqualTo(floorDp - onePxDp - 0.01f), "sheet width must keep at least the 45% floor where the cap allows");
				Assert.That(widthDp, Is.LessThanOrEqualTo(usableWidthDp * 0.7f + onePxDp + 0.01f), "sheet width must not exceed 70% of usable width");
			}
		}

		[TestCaseSource(nameof(ProfileCases))]
		public void LayoutRespectsHeightConstraintAndTouchTargets(float usableWidthDp, float usableHeightDp)
		{
			foreach (var factor in DpToUiFactors)
			{
				var layout = AndroidSelectionSheetMetrics.ComputeLayout(usableWidthDp, usableHeightDp, factor);

				var heightDp = layout.MaxPanelHeight / factor;
				var onePxDp = 1f / factor;
				Assert.That(heightDp, Is.LessThanOrEqualTo(usableHeightDp * 0.96f + onePxDp + 0.01f),
					"sheet height must not exceed 96% of usable height");
				Assert.That(layout.RowHeight / factor, Is.GreaterThanOrEqualTo(48f - 0.01f),
					"option rows must be at least 48dp tall");
				Assert.That(layout.CloseSize / factor, Is.GreaterThanOrEqualTo(48f - 0.01f),
					"close target must be at least 48dp");
			}
		}

		[TestCase(1)]
		[TestCase(5)]
		[TestCase(10)]
		[TestCase(20)]
		[TestCase(40)]
		public void ContentHeightMatchesManualSum(int rows)
		{
			var layout = AndroidSelectionSheetMetrics.ComputeLayout(891, 411, 2.1875f);

			var expected = 0;
			if (rows > 0)
				expected = 2 * layout.TopBottomSpacing + rows * layout.RowHeight + (rows - 1) * layout.ItemSpacing;

			Assert.That(layout.ContentHeight(rows), Is.EqualTo(expected));
		}

		[TestCaseSource(nameof(ProfileCases))]
		public void UniformRowTopsAreContiguousAndInsideContent(float usableWidthDp, float usableHeightDp)
		{
			var layout = AndroidSelectionSheetMetrics.ComputeLayout(usableWidthDp, usableHeightDp, 2.1875f);

			const int rows = 12;
			const int listTop = 300;
			var offsets = new[]
			{
				0,
				-layout.RowHeight * 2,
				Math.Min(0, (Math.Min(layout.ContentHeight(rows), layout.MaxPanelHeight) - layout.ContentHeight(rows)))
			};

			foreach (var offset in offsets)
			{
				var contentTop = listTop + offset;
				var contentBottom = listTop + offset + layout.ContentHeight(rows);
				for (var i = 0; i < rows; i++)
				{
					var top = layout.RowTop(listTop, offset, i);
					Assert.That(top, Is.EqualTo(
						listTop + offset + layout.TopBottomSpacing + i * layout.RowHeight + i * layout.ItemSpacing));

					if (i > 0)
						Assert.That(top, Is.EqualTo(layout.RowTop(listTop, offset, i - 1) + layout.RowHeight + layout.ItemSpacing),
							"rows must be contiguous (no overlapping hit regions)");

					var bottom = top + layout.RowHeight;
					Assert.That(bottom, Is.LessThanOrEqualTo(contentBottom + layout.ItemSpacing));
					Assert.That(top, Is.GreaterThanOrEqualTo(contentTop));
				}
			}
		}

		[Test]
		public void LastRowIsFullyVisibleWhenScrolledToBottom()
		{
			var layout = AndroidSelectionSheetMetrics.ComputeLayout(891, 411, 2.1875f);
			const int rows = 20;
			var contentHeight = layout.ContentHeight(rows);
			var listHeight = Math.Min(contentHeight, layout.MaxPanelHeight);
			var offsetBottom = Math.Min(0, listHeight - contentHeight);
			const int listTop = 200;

			var lastTop = layout.RowTop(listTop, offsetBottom, rows - 1);
			Assert.That(lastTop + layout.RowHeight, Is.LessThanOrEqualTo(listTop + listHeight + layout.ItemSpacing),
				"scrolling to the bottom must leave the last row fully visible and selectable");
		}

		[Test]
		public void ItemTopMatchesScrollPanelChildPositioning()
		{
			// ItemTop(listTop, offset, itemContentTop) mirrors the ScrollPanel
			// child origin: RenderOrigin + currentListOffset + child.Bounds.Y.
			var layout = AndroidSelectionSheetMetrics.ComputeLayout(891, 411, 2.1875f);
			const int listTop = 100;
			var offset = -layout.RowHeight * 3;
			const int contentTop = 22; // first-row content Y equals TopBottomSpacing

			Assert.That(AndroidSelectionSheetMetrics.ItemTop(listTop, offset, contentTop),
				Is.EqualTo(listTop + offset + contentTop));
			Assert.That(
				AndroidSelectionSheetMetrics.ItemTop(listTop, offset, contentTop + 2 * (layout.RowHeight + layout.ItemSpacing)),
				Is.EqualTo(listTop + offset + contentTop + 2 * (layout.RowHeight + layout.ItemSpacing)));
		}
	}
}
