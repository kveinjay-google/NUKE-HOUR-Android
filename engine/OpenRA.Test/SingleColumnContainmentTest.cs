using System.Linq;
using NUnit.Framework;
using OpenRA.Mods.Common.Widgets.Logic;
using OpenRA.Primitives;

namespace OpenRA.Test
{
	[TestFixture]
	public class SingleColumnContainmentTest
	{
		[TestCase(1558, 720, 844, 390)]
		[TestCase(1564, 720, 956, 440)]
		[TestCase(1133, 744, 1133, 744)]
		[TestCase(1180, 820, 1180, 820)]
		[TestCase(2360, 1640, 1180, 820)]
		[TestCase(1366, 1024, 1366, 1024)]
		[TestCase(844, 390, 844, 390)]
		public void SingleColumnKeepsTheExistingRowHeightAndFillsItsWideRasterInterior(
			int width, int height, int nativeWidth, int nativeHeight)
		{
			var snapshot = new IosScreenSnapshot(new Size(width, height), new Size(nativeWidth, nativeHeight), new IosSafeAreaInsets(47, 0, 47, 21));
			var shell = IosIngameSidebarLayoutPolicy.Create(snapshot);
			var layout = IosProductionPaletteLayoutPolicy.Create(snapshot, shell, IosProductionPaletteMode.LargeSingleColumn);
			var rowHeight = layout.IconSize.Y + layout.IconMargin.Y;
			// Actual inner raster edges: x=31..203 of 234, y=7..109 of 116.
			var left = (shell.ProductionBounds.Width * 31 + 233) / 234;
			var right = shell.ProductionBounds.Width * 203 / 234;
			var top = (rowHeight * 7 + 115) / 116;
			var bottom = rowHeight * 109 / 116;
			Assert.Multiple(() =>
			{
				Assert.That(layout.PaletteX, Is.GreaterThanOrEqualTo(left));
				Assert.That(layout.PaletteX + layout.IconSize.X, Is.LessThanOrEqualTo(right));
				Assert.That(layout.PaletteX, Is.EqualTo(left));
				Assert.That(layout.PaletteX + layout.IconSize.X, Is.EqualTo(right));
				Assert.That(layout.PaletteY, Is.GreaterThanOrEqualTo(top));
				Assert.That(layout.PaletteY + layout.IconSize.Y, Is.LessThanOrEqualTo(bottom));
				Assert.That(bottom - top - layout.IconSize.Y, Is.InRange(0, 1));
				Assert.That(layout.IconSize.Y, Is.EqualTo(bottom - top));
				Assert.That(rowHeight, Is.EqualTo(System.Math.Min(116, System.Math.Max(1, shell.BottomCapY - 2))));
				Assert.That(layout.MaximumRows * rowHeight, Is.LessThanOrEqualTo(shell.BottomCapY));
			});
		}

		[TestCase(1558, 720, 844, 390)]
		[TestCase(1564, 720, 956, 440)]
		[TestCase(1133, 744, 1133, 744)]
		[TestCase(1180, 820, 1180, 820)]
		[TestCase(2360, 1640, 1180, 820)]
		[TestCase(1366, 1024, 1366, 1024)]
		[TestCase(844, 390, 844, 390)]
		public void EveryDensityKeepsCameosInsideItsMatchingRasterRows(int width, int height, int nativeWidth, int nativeHeight)
		{
			var snapshot = new IosScreenSnapshot(new Size(width, height), new Size(nativeWidth, nativeHeight), new IosSafeAreaInsets(47, 0, 47, 21));
			var shell = IosIngameSidebarLayoutPolicy.Create(snapshot);
			foreach (var mode in new[]
			{
				IosProductionPaletteMode.LargeSingleColumn,
				IosProductionPaletteMode.DoubleColumn,
				IosProductionPaletteMode.CompactThreeColumns
			})
			{
				var layout = IosProductionPaletteLayoutPolicy.Create(snapshot, shell, mode);
				var backgrounds = IosProductionPaletteLayoutPolicy.BackgroundRowHeights(layout, shell.BottomCapY);
				var rowHeight = layout.IconSize.Y + layout.IconMargin.Y;
				Assert.That(backgrounds, Has.All.LessThanOrEqualTo(rowHeight));
				Assert.That(backgrounds.Sum(), Is.EqualTo(shell.BottomCapY));
				Assert.That(layout.PaletteY + layout.IconSize.Y, Is.LessThanOrEqualTo(rowHeight));
				Assert.That(layout.PaletteX, Is.GreaterThanOrEqualTo(24));
				Assert.That(layout.PaletteX + layout.GridWidth, Is.LessThanOrEqualTo(210));
			}
		}
	}
}
