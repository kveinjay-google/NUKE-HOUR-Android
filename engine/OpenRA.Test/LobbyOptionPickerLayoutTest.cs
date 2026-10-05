using System;
using NUnit.Framework;
using OpenRA.Mods.Common.Widgets.Logic;
using OpenRA.Primitives;

namespace OpenRA.Test
{
	[TestFixture]
	public class LobbyOptionPickerLayoutTest
	{
		[TestCase(812, 375, 12)]
		[TestCase(500, 375, 12)]
		[TestCase(1180, 820, 9)]
		[TestCase(1440, 900, 5)]
		public void PickerUsesLargeGridCellsWithinSafeArea(int width, int height, int count)
		{
			var screen = new IosScreenSnapshot(new Size(width, height), new Size(width, height), default);
			var layout = new LobbyOptionPickerLayout(screen, count);
			Assert.That(layout.Columns, Is.InRange(2, 3));
			Assert.That(layout.CellHeight, Is.GreaterThanOrEqualTo(64));
			Assert.That(layout.CellWidth, Is.GreaterThanOrEqualTo(140));
			Assert.That(layout.Width, Is.LessThan(width));
			Assert.That(layout.Height, Is.LessThan(height));
			Assert.That(layout.Columns * (layout.CellWidth + layout.Gap) + layout.Gap + layout.Scrollbar,
				Is.LessThanOrEqualTo(layout.Width));
			Assert.That(layout.ContentHeight, Is.GreaterThanOrEqualTo(layout.Height));
		}

		[Test]
		public void LobbyFieldsAreTallerThanStandardTouchTarget()
		{
			var policy = IosMenuLayoutPolicy.Create(true, 1180, 820);
			Assert.That(policy.OptionControlBounds(0, 1000, true).Height, Is.GreaterThanOrEqualTo(60));
			Assert.That(policy.OptionControlBounds(0, 1000, true).Bottom, Is.LessThanOrEqualTo(policy.OptionRowHeight(true)));
		}

		[Test]
		public void RetinaLayoutPreservesPhysicalCellSizeAndPopupStaysInsideInsets()
		{
			var screen = new IosScreenSnapshot(new Size(1688, 780), new Size(844, 390), default);
			var layout = new LobbyOptionPickerLayout(screen, 20);
			Assert.That(layout.CellHeight / screen.LogicalPerPoint, Is.GreaterThanOrEqualTo(64));
			Assert.That(layout.ContentHeight, Is.GreaterThan(layout.Height));
			var safe = new Rectangle(80, 20, 1500, 700);
			var placed = IosTouchWidgetPolicy.PlacePopup(new Rectangle(1400, 620, 100, 60), 1400,
				layout.Width, layout.Height, safe);
			Assert.That(placed.Left, Is.GreaterThanOrEqualTo(safe.Left));
			Assert.That(placed.Top, Is.GreaterThanOrEqualTo(safe.Top));
			Assert.That(placed.Right, Is.LessThanOrEqualTo(safe.Right));
			Assert.That(placed.Bottom, Is.LessThanOrEqualTo(safe.Bottom));
		}

		[TestCase(1558, 720, 844, 390)]
		[TestCase(1180, 820, 1180, 820)]
		[TestCase(568, 320, 568, 320)]
		public void RosterFactionPickerShowsAllThirteenCountriesInACompactFiveColumnGrid(
			int width, int height, int nativeWidth, int nativeHeight)
		{
			var screen = new IosScreenSnapshot(
				new Size(width, height), new Size(nativeWidth, nativeHeight), default);
			var layout = new LobbyRosterPickerLayout(screen, 13, true);

			Assert.That(layout.Columns, Is.EqualTo(5));
			Assert.That(layout.Rows, Is.EqualTo(3));
			Assert.That(layout.Width, Is.LessThanOrEqualTo(screen.LogicalPoints(540)));
			Assert.That(layout.CellHeight / screen.LogicalPerPoint, Is.InRange(56, 64));
			Assert.That(layout.EmblemSize / screen.LogicalPerPoint, Is.InRange(26, 32));
			Assert.That(layout.Height, Is.EqualTo(layout.ContentHeight));
			Assert.That(layout.NeedsScrolling, Is.False);
			Assert.That(layout.Height, Is.LessThan(height));
		}

		[TestCase(4, false)]
		[TestCase(10, true)]
		[TestCase(13, true)]
		public void TeamAndSpawnPickersUseAnAnchorWidthSingleColumnDropdown(int count, bool expectsScrolling)
		{
			var screen = new IosScreenSnapshot(new Size(844, 390), new Size(844, 390), default);
			var anchorWidth = screen.LogicalPoints(150);
			var layout = new LobbyRosterPickerLayout(screen, count, false, anchorWidth);

			Assert.That(layout.Columns, Is.EqualTo(1));
			Assert.That(layout.CellHeight / screen.LogicalPerPoint, Is.InRange(48, 56));
			Assert.That(layout.Width, Is.EqualTo(anchorWidth));
			Assert.That(layout.NeedsScrolling, Is.EqualTo(expectsScrolling));
			Assert.That(layout.Height, Is.LessThanOrEqualTo(layout.ContentHeight));
		}

		[Test]
		public void PositionManagementPickerOffersTallRows()
		{
			var screen = new IosScreenSnapshot(new Size(844, 390), new Size(844, 390), default);
			var layout = new LobbyPositionPickerLayout(screen, 10);
			Assert.That(layout.ItemHeight, Is.GreaterThanOrEqualTo(56));
			Assert.That(layout.Height, Is.LessThan(screen.SafeBounds.Height));
			Assert.That(layout.ContentHeight, Is.GreaterThan(layout.Height));
		}
	}
}
