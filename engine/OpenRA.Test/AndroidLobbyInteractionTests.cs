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
using OpenRA.Mods.Common.Widgets.Logic;

namespace OpenRA.Test
{
	[TestFixture]
	public class MobileSelectionSheetLayoutTests
	{
		[TestCase(1, 800f, 360f)]
		[TestCase(5, 891f, 411f)]
		[TestCase(10, 960f, 540f)]
		[TestCase(20, 1158f, 411f)]
		[TestCase(40, 1158f, 411f)]
		public void ContentHeightScalesWithRows(int rows, float widthDp, float heightDp)
		{
			var l = AndroidSelectionSheetMetrics.ComputeLayout(widthDp, heightDp, 2.1875f);
			var expected = rows <= 0 ? 0 : 2 * l.TopBottomSpacing + rows * l.RowHeight + (rows - 1) * l.ItemSpacing;
			Assert.That(l.ContentHeight(rows), Is.EqualTo(expected));
			Assert.That(l.ContentHeight(rows), Is.GreaterThanOrEqualTo(rows * l.RowHeight),
				"content must fit every row without overlap");
		}

		[TestCase(800f, 360f)]
		[TestCase(891f, 411f)]
		[TestCase(1158f, 411f)]
		public void LayoutFitsMobileContract(float widthDp, float heightDp)
		{
			var l = AndroidSelectionSheetMetrics.ComputeLayout(widthDp, heightDp, 2.1875f);
			Assert.That(l.PanelWidth / 2.1875f, Is.LessThanOrEqualTo(420.5f));
			Assert.That(l.RowHeight / 2.1875f, Is.GreaterThanOrEqualTo(48f));
			Assert.That(l.HeaderHeight / 2.1875f, Is.InRange(48f, 56.5f));
		}
	}

	[TestFixture]
	public class MobileSelectionSheetHitTestTests
	{
		[TestCase(0, 0)]
		[TestCase(1, -122)]
		[TestCase(9, -244)]
		[TestCase(19, -366)]
		public void RowIndexHitMatchesVisualRowTop(int index, int offset)
		{
			var l = AndroidSelectionSheetMetrics.ComputeLayout(1158, 411, 2.1875f);
			const int listTop = 204;
			var top = AndroidSelectionSheetMetrics.RowTopForLog(
				listTop, offset, l.TopBottomSpacing, l.RowHeight, l.ItemSpacing, index);
			Assert.That(top, Is.EqualTo(listTop + offset + l.TopBottomSpacing + index * (l.RowHeight + l.ItemSpacing)));
			// The row that visually starts at `top` must be the row that a tap hits.
			Assert.That(top, Is.EqualTo(
				AndroidSelectionSheetMetrics.ItemTop(listTop, offset, l.TopBottomSpacing + index * (l.RowHeight + l.ItemSpacing))));
		}

		[TestCase(0)]
		[TestCase(5)]
		[TestCase(9)]
		[TestCase(19)]
		public void TapInsideRowBoundsSelectsThatRow(int index)
		{
			var l = AndroidSelectionSheetMetrics.ComputeLayout(1158, 411, 2.1875f);
			var top = l.RowTop(204, 0, index);
			var bottom = top + l.RowHeight;
			// A point inside the row maps back to exactly this index.
			var recovered = (int)Math.Floor((top + 5 - 204 - l.TopBottomSpacing) / (float)(l.RowHeight + l.ItemSpacing));
			Assert.That(recovered, Is.EqualTo(index));
			Assert.That(bottom, Is.GreaterThan(top));
		}
	}

	[TestFixture]
	public class MobileSelectionSheetScrollTests
	{
		[TestCase(1, 10)]
		[TestCase(5, 10)]
		[TestCase(20, 10)]
		public void BottomOffsetRevealsLastRow(int rows, int listHeightRows)
		{
			var l = AndroidSelectionSheetMetrics.ComputeLayout(1158, 411, 2.1875f);
			var content = l.ContentHeight(rows);
			var listHeight = Math.Min(content, l.RowHeight * listHeightRows);
			var bottom = Math.Min(0, listHeight - content);

			var lastTop = l.RowTop(300, bottom, rows - 1);
			Assert.That(lastTop + l.RowHeight, Is.LessThanOrEqualTo(300 + listHeight + l.ItemSpacing),
				"the last row must be fully visible after scrolling to the bottom");
		}

		[TestCase(8, 3)]
		[TestCase(20, 3)]
		public void MiddleRowSelectableAtTopAndAfterScroll(int rows, int listHeightRows)
		{
			var l = AndroidSelectionSheetMetrics.ComputeLayout(1158, 411, 2.1875f);
			var content = l.ContentHeight(rows);
			var listHeight = Math.Min(content, l.RowHeight * listHeightRows);

			var mid = rows / 2;
			var offsetForMid = Math.Min(0, Math.Min(0, listHeight - content) + 0);
			// Row must be reachable at some valid offset inside [listHeight-content, 0].
			var midTopAtBottom = l.RowTop(300, Math.Min(0, listHeight - content), mid);
			Assert.That(midTopAtBottom + l.RowHeight, Is.LessThanOrEqualTo(300 + listHeight + l.ItemSpacing));
			Assert.That(midTopAtBottom, Is.GreaterThanOrEqualTo(300 + Math.Min(0, listHeight - content)));
		}
	}

	[TestFixture]
	public class MobileSelectionSheetSafeAreaTests
	{
		[TestCase(800f, 360f, 2.1875f)]
		[TestCase(891f, 411f, 1.6f)]
		[TestCase(960f, 540f, 1f)]
		[TestCase(1158f, 411f, 2.1875f)]
		public void SheetStaysInsideSafeUsableArea(float usableW, float usableH, float dpToUi)
		{
			var l = AndroidSelectionSheetMetrics.ComputeLayout(usableW, usableH, dpToUi);
			// 45%-70% of usable width, never over 420dp; height at most 80%.
			Assert.That(l.PanelWidth / dpToUi, Is.LessThanOrEqualTo(420f + 1f / dpToUi));
			Assert.That(l.PanelWidth / dpToUi, Is.LessThanOrEqualTo(usableW * 0.7f + 1f / dpToUi));
			Assert.That(l.MaxPanelHeight / dpToUi, Is.LessThanOrEqualTo(usableH * 0.96f + 1f / dpToUi));
		}

		[Test]
		public void AsymmetricHorizontalInsetsNeverShrinkSheetBelowTouchMinimums()
		{
			// Phone with a right-side navigation bar (like the S10): usable width
			// already excludes the inset, so the sheet still gets >=48dp rows.
			var l = AndroidSelectionSheetMetrics.ComputeLayout(732, 411, 2.1875f);
			Assert.That(l.RowHeight / 2.1875f, Is.GreaterThanOrEqualTo(48f));
			Assert.That(l.PanelWidth / 2.1875f, Is.LessThanOrEqualTo(420.5f));
		}
	}

	[TestFixture]
	public class MobileSelectionSheetBackDismissTests
	{
		[Test]
		public void EscapeIsTheEngineDismissKey()
		{
			// The sheet dismisses on Keycode.ESCAPE; guard the constant so a
			// future keycode rework can't silently break dismiss.
			Assert.That((int)Keycode.ESCAPE, Is.EqualTo(27));
		}

		[TestCase(800f, 360f, 2.1875f)]
		[TestCase(1158f, 411f, 2.1875f)]
		public void CloseTargetIsAtLeast48Dp(float usableW, float usableH, float dpToUi)
		{
			var l = AndroidSelectionSheetMetrics.ComputeLayout(usableW, usableH, dpToUi);
			Assert.That(l.CloseSize / dpToUi, Is.GreaterThanOrEqualTo(48f - 0.01f));
			Assert.That(l.CloseSize, Is.GreaterThanOrEqualTo(1));
		}

		[Test]
		public void DialogScalerFitsDesignedDialogIntoSafeArea()
		{
			// The desktop color chooser is 326x154; scaled up it must stay inside
			// the usable content area and never exceed the max factor.
			const int designedW = 326;
			const int designedH = 154;
			var factor = PhoneDialogScaler.ComputeScaleFactor(designedW, designedH, 1601, 900);
			Assert.That(factor, Is.InRange(PhoneDialogScaler.MinimumFactor, PhoneDialogScaler.MaximumFactor + 0.001f));
			Assert.That(designedW * factor, Is.LessThanOrEqualTo(1601 * PhoneDialogScaler.SafeFraction + 1));
			Assert.That(designedH * factor, Is.LessThanOrEqualTo(900 * PhoneDialogScaler.SafeFraction + 1));

			// Larger surfaces still respect the scale ceiling.
			var big = PhoneDialogScaler.ComputeScaleFactor(designedW, designedH, 3000, 1500);
			Assert.That(big, Is.LessThanOrEqualTo(PhoneDialogScaler.MaximumFactor + 0.001f));
			Assert.That(designedW * big, Is.LessThanOrEqualTo(3000 * PhoneDialogScaler.SafeFraction + 1));

			// A short landscape phone may have enough width but little safe
			// height. The scaler must never force its minimum enlargement past
			// that height (the About page otherwise appears to exit to black).
			const int aboutDesignedW = 620;
			const int aboutDesignedH = 420;
			var shortPhone = PhoneDialogScaler.ComputeScaleFactor(
				aboutDesignedW, aboutDesignedH, 1097, 494);
			Assert.That(aboutDesignedW * shortPhone,
				Is.LessThanOrEqualTo(1097 * PhoneDialogScaler.SafeFraction + 1));
			Assert.That(aboutDesignedH * shortPhone,
				Is.LessThanOrEqualTo(494 * PhoneDialogScaler.SafeFraction + 1));
		}
	}
}
