// Copyright (c) The OpenRA Developers and Contributors
// Licensed under the GNU General Public License v3.
using System.Reflection;
using System.Runtime.Serialization;
using NUnit.Framework;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Mods.RA2.Widgets.Logic;
using OpenRA.Widgets;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class PowerMeterLayoutTest
	{
		const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

		static PowerMeterWidget Meter(ContainerWidget rows)
		{
			var meter = (PowerMeterWidget)FormatterServices.GetUninitializedObject(typeof(PowerMeterWidget));
			var defaults = new ContainerWidget();
			foreach (var field in typeof(Widget).GetFields(Fields))
				field.SetValue(meter, field.GetValue(defaults));
			typeof(PowerMeterWidget).GetField("sidebarProduction", Fields).SetValue(meter, rows);
			typeof(PowerMeterWidget).GetField("MeterAlongside", Fields).SetValue(meter, "ROW_TEMPLATE");
			typeof(PowerMeterWidget).GetField("barWidth", Fields).SetValue(meter, 12);
			return meter;
		}

		[TestCase("ROW_TEMPLATE", 50)]
		[TestCase("ONE_COLUMN_ROW_TEMPLATE", 116)]
		[TestCase("TWO_COLUMN_ROW_TEMPLATE", 80)]
		[TestCase("THREE_COLUMN_ROW_TEMPLATE", 50)]
		public void MeterIncludesTheVisibleDensityRowsAndExcludesTheBottomCap(string id, int height)
		{
			var rows = new ContainerWidget();
			rows.AddChild(new ContainerWidget { Id = id, Bounds = new WidgetBounds(0, 0, 234, height) });
			rows.AddChild(new ContainerWidget { Id = id, Bounds = new WidgetBounds(0, height, 234, 17) });
			rows.AddChild(new ContainerWidget { Id = "BOTTOM_CAP", Bounds = new WidgetBounds(0, height + 17, 234, 60) });
			var meter = Meter(rows);
			meter.CalculateMeterBarDimensions();
			Assert.That(meter.Bounds.Height, Is.EqualTo(height + 17));
			Assert.That(meter.Bounds.Width, Is.EqualTo(12));

			rows.RemoveChildren();
			rows.AddChild(new ContainerWidget { Id = "THREE_COLUMN_ROW_TEMPLATE", Bounds = new WidgetBounds(0, 0, 234, 50) });
			meter.CalculateMeterBarDimensions();
			Assert.That(meter.Bounds.Height, Is.EqualTo(50), "Switching density must resize the existing meter.");
		}

		[TestCase(0, 0, 200, 0, 0, 0)]
		[TestCase(25, 0, 200, 1, 0, 1)]
		[TestCase(26, 1, 200, 2, 1, 2)]
		[TestCase(25, 50, 200, 2, 2, 1)]
		[TestCase(10000, 5000, 200, 200, 100, 200)]
		[TestCase(5000, 10000, 200, 200, 200, 100)]
		[TestCase(10000, 5000, 0, 0, 0, 0)]
		[TestCase(1073741823, 2147483647, 200, 200, 200, 100)]
		public void BarCountsRepresentZeroPartialDeficitAndCompressedPower(
			int provided, int drained, int bars, int total, int used, int available)
		{
			Assert.That(PowerMeterWidget.CalculatePowerSteps(provided, drained, bars, 25),
				Is.EqualTo((total, used, available)));
		}
	}
}
