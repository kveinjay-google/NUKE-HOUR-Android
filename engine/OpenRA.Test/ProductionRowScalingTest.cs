using NUnit.Framework;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Mods.Common.Widgets.Logic;
using OpenRA.Widgets;

namespace OpenRA.Test
{
	[TestFixture]
	public class ProductionRowScalingTest
	{
		[TestCase(116, 487)]
		[TestCase(80, 487)]
		[TestCase(50, 487)]
		[TestCase(50, 20)]
		public void BackgroundAlwaysFillsAvailableHeightIncludingPartialLastRow(int rowHeight, int fillHeight)
		{
			var layout = new IosProductionPaletteLayout(1, new OpenRA.int2(132, rowHeight),
				new OpenRA.int2(0, 0), 1, 0);
			var rows = IosProductionPaletteLayoutPolicy.BackgroundRowHeights(layout, fillHeight);
			Assert.That(System.Linq.Enumerable.Sum(rows), Is.EqualTo(fillHeight));
			Assert.That(rows, Has.All.LessThanOrEqualTo(rowHeight));
		}

		[TestCase(false, 100, 320, true)]
		[TestCase(false, 319, 320, true)]
		[TestCase(false, 320, 320, false)]
		[TestCase(true, 100, 320, false)]
		public void MomentaryFeedbackExpiresAndNeverActivatesDisabledButtons(bool disabled, long now, long until, bool expected)
		{
			Assert.That(ButtonWidget.IsActivationFeedbackVisible(disabled, now, until), Is.EqualTo(expected));
		}

		[TestCase(116)]
		[TestCase(80)]
		[TestCase(50)]
		public void NativeSizeRowsPreserveHighDensityTemplateScaling(int height)
		{
			var template = new ImageWidget { StretchToFit = true, Bounds = new WidgetBounds(0, 0, 234, height) };
			var row = (ImageWidget)ClassicProductionLogic.CloneProductionRow(template, height, height, 234);
			Assert.That(row.StretchToFit, Is.True, "A 4x atlas must remain fitted even when widget dimensions match the template.");
			Assert.That(row.Bounds.Y, Is.EqualTo(height));
			Assert.That(template.Bounds.Y, Is.Zero);
		}
		[TestCase(234, 50, false)]
		[TestCase(233, 50, true)]
		[TestCase(234, 30, true)]
		public void LegacyRowsOnlyStretchWhenResized(int width, int height, bool expected)
		{
			var template = new ImageWidget { Bounds = new WidgetBounds(0, 0, 234, 50) };
			var row = (ImageWidget)ClassicProductionLogic.CloneProductionRow(template, 0, height, width);
			Assert.That(row.StretchToFit, Is.EqualTo(expected));
		}
	}
}
