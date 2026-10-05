using System.Linq;
using NUnit.Framework;
using OpenRA.Mods.RA2.Widgets;
using OpenRA.Primitives;

namespace OpenRA.Test
{
	[TestFixture]
	public class HdSidebarLayoutTest
	{
		[Test]
		public void ClassicActionsDivideTheSilverPanelIntoTwoEqualControls()
		{
			var bounds = HdSidebarLayout.ClassicActionVisualBounds(234);
			Assert.That(bounds, Has.Length.EqualTo(2));
			Assert.That(bounds[0].Width, Is.EqualTo(bounds[1].Width));
			Assert.That(bounds.All(b => b.Height == 44), Is.True);
			Assert.That(bounds[0].X, Is.GreaterThanOrEqualTo(32));
			Assert.That(bounds[1].Right, Is.LessThanOrEqualTo(201));
		}

		[Test]
		public void ClassicActionsUseTwoNonOverlappingFortyFourPointTouchTargets()
		{
			var bounds = HdSidebarLayout.ClassicActionTouchBounds(234);
			Assert.That(bounds, Has.Length.EqualTo(2));
			Assert.That(bounds[0].X, Is.EqualTo(32));
			Assert.That(bounds[1].Right, Is.EqualTo(201));
			Assert.That(bounds.All(b => b.Width >= 44 && b.Height >= 44), Is.True);
			Assert.That(bounds[1].X, Is.EqualTo(bounds[0].Right));
		}

		[TestCase(844, 390, true)]
		[TestCase(1180, 820, false)]
		public void TouchRowsReserveFortyFourPointsPerControl(int width, int height, bool phone)
		{
			var layout = HdSidebarLayout.Create(new Size(width, height), true, phone, 1, 0, 0, false);
			Assert.That(layout.Categories.Width / 4, Is.GreaterThanOrEqualTo(44));
			Assert.That(layout.Actions.Width / 2, Is.GreaterThanOrEqualTo(44));
		}

		[TestCase(844, 390, true, 1.0)]
		[TestCase(1180, 820, false, 1.0)]
		[TestCase(2360, 1640, false, 2.0)]
		public void EveryPhoneAndTabletControlHasAFortyFourPointHitTarget(
			int width, int height, bool phone, double scale)
		{
			var layout = HdSidebarLayout.Create(new Size(width, height), true, phone, scale, 0, 0, false);
			var minimum = (int)(44 * scale);
			foreach (var row in new[]
			{
				HdSidebarLayout.SplitRow(layout.Menu, 3),
				HdSidebarLayout.SplitRow(layout.Actions, 2),
				HdSidebarLayout.SplitRow(layout.Categories, 4),
				HdSidebarLayout.SplitRow(layout.Footer, 2)
			})
				foreach (var button in row)
					Assert.Multiple(() =>
					{
						Assert.That(button.Width, Is.GreaterThanOrEqualTo(minimum));
						Assert.That(button.Height, Is.GreaterThanOrEqualTo(minimum));
					});

			Assert.Multiple(() =>
			{
				Assert.That(layout.CellWidth, Is.GreaterThanOrEqualTo(minimum));
				Assert.That(layout.CellHeight, Is.GreaterThanOrEqualTo(minimum));
			});
		}

		[TestCase(844, 390, true, true, 1.0)]
		[TestCase(1180, 820, true, false, 1.0)]
		[TestCase(2360, 1640, true, false, 2.0)]
		[TestCase(1920, 1080, false, false, 1.0)]
		public void ResourceReadoutsHaveSeparateContainedSlots(int width, int height, bool touch, bool phone, double scale)
		{
			var layout = HdSidebarLayout.Create(new Size(width, height), touch, phone, scale, 0, 0, false);
			var shell = new Rectangle(0, 0, layout.Shell.Width, layout.Shell.Height);
			Assert.Multiple(() =>
			{
				Assert.That(shell.Contains(layout.Cash), Is.True);
				Assert.That(shell.Contains(layout.Timer), Is.True);
				Assert.That(shell.Contains(layout.Power), Is.True);
				Assert.That(layout.Cash.Right, Is.LessThanOrEqualTo(layout.Timer.Left));
				Assert.That(layout.Timer.Right, Is.LessThanOrEqualTo(layout.Power.Left));
				Assert.That(layout.Power.Bottom, Is.LessThanOrEqualTo(layout.Menu.Top));
			});
		}

		[TestCase(false, false, 2)]
		[TestCase(true, false, 2)]
		[TestCase(false, true, 3)]
		public void PhoneDensityModesRemainDistinct(bool large, bool compact, int columns)
		{
			var layout = HdSidebarLayout.Create(new Size(844, 390), true, true, 1, 0, 0, large, compact);
			Assert.That(layout.Columns, Is.EqualTo(columns));
		}

		[TestCase(1180, 820, 1.0)]
		[TestCase(2360, 1640, 2.0)]
		public void SavedLargeModeStillShowsMultipleOriginalCameos(int width, int height, double scale)
		{
			var layout = HdSidebarLayout.Create(new Size(width, height), true, false, scale, 0, 0, true);
			Assert.That(layout.Columns, Is.EqualTo(2));
			Assert.That(layout.Rows, Is.GreaterThanOrEqualTo(3));
			Assert.That(layout.CellWidth, Is.LessThanOrEqualTo(140 * scale));
		}

		[Test]
		public void ProductionFooterFollowsTheLastUsedRowWithoutReducingCapacity()
		{
			var layout = HdSidebarLayout.Create(new Size(1180, 820), false, false, 1, 0, 0, false);
			var capacity = layout.Rows;
			layout.FitProductionContent(10);

			Assert.Multiple(() =>
			{
				Assert.That(layout.Rows, Is.EqualTo(4));
				Assert.That(layout.Palette.Height,
					Is.EqualTo(4 * layout.CellHeight + 3 * layout.Gap));
				Assert.That(layout.Footer.Top, Is.EqualTo(layout.Palette.Bottom + layout.Gap));
				Assert.That(layout.CapacityRows, Is.EqualTo(capacity));
			});
		}

		[Test]
		public void FullProductionListKeepsTheFooterAtItsCapacityLimit()
		{
			var layout = HdSidebarLayout.Create(new Size(1180, 820), false, false, 1, 0, 0, false);
			var originalFooter = layout.Footer;
			layout.FitProductionContent(999);

			Assert.Multiple(() =>
			{
				Assert.That(layout.Rows, Is.EqualTo(layout.CapacityRows));
				Assert.That(layout.Footer.Top, Is.EqualTo(layout.Palette.Bottom + layout.Gap));
				Assert.That(layout.Footer.Bottom, Is.LessThanOrEqualTo(originalFooter.Bottom));
			});
		}
		[TestCase(844, 390, true, true)]
		[TestCase(1180, 820, true, false)]
		[TestCase(1366, 1024, true, false)]
		[TestCase(1920, 1080, false, false)]
		[TestCase(3840, 2160, false, false)]
		public void RegionsStayInsideSingleShell(int width, int height, bool touch, bool phone)
		{
			var layout = HdSidebarLayout.Create(new Size(width, height), touch, phone, 1, 0, 0, false);
			var local = new Rectangle(0, 0, layout.Shell.Width, height);
			foreach (var region in new[] { layout.Cash, layout.Timer, layout.Power, layout.Radar, layout.Menu, layout.Actions, layout.Categories, layout.Palette, layout.Footer })
				Assert.That(local.Contains(region), Is.True, region.ToString());
			Assert.That(layout.Categories.Bottom, Is.LessThanOrEqualTo(layout.Palette.Top));
			Assert.That(layout.Palette.Bottom, Is.LessThanOrEqualTo(layout.Footer.Top));
			Assert.That(layout.Columns, Is.GreaterThanOrEqualTo(1));
			if (touch)
			{
				Assert.That(layout.Categories.Height, Is.GreaterThanOrEqualTo(44));
				Assert.That(layout.Footer.Height, Is.GreaterThanOrEqualTo(44));
			}
		}
	}
}
