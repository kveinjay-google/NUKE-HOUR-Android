using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using NUnit.Framework;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Mods.Common.Widgets.Logic;
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Test
{
	[TestFixture]
	public class DesktopSettingsLayoutTest
	{
		static Type GeometryType => typeof(SettingsLogic).Assembly.GetType("OpenRA.Mods.Common.Widgets.Logic.DesktopSettingsLayout");
		static T EmptyWidget<T>(string id) where T : Widget
		{
			var result = (T)FormatterServices.GetUninitializedObject(typeof(T));
			typeof(Widget).GetField("Children").SetValue(result, new List<Widget>());
			result.Id = id;
			result.IsVisible = () => true;
			return result;
		}

		[TestCase(800, 600)]
		[TestCase(1280, 800)]
		[TestCase(1920, 1080)]
		[TestCase(2560, 1440)]
		public void DesktopFrameFillsWindowAndKeepsSixTabsAndFooterSeparated(int width, int height)
		{
			Assert.That(GeometryType, Is.Not.Null);
			var layout = GeometryType.GetMethod("ForViewport").Invoke(null, new object[] { new Size(width, height) });
			Rectangle Bounds(string property) => (Rectangle)GeometryType.GetProperty(property).GetValue(layout);
			Assert.That(Bounds("Window"), Is.EqualTo(new Rectangle(0, 0, width, height)));
			foreach (var region in new[] { "Header", "Tabs", "Content", "Reset", "Back" })
				Assert.That(Bounds("Window").Contains(Bounds(region)), Is.True, region);
			Assert.That(Bounds("Tabs").Right, Is.LessThan(Bounds("Content").Left));
			Assert.That(Bounds("Header").Bottom, Is.LessThanOrEqualTo(Bounds("Content").Top));
			Assert.That(Bounds("Content").Bottom, Is.LessThan(Bounds("Back").Top));
			var lastBottom = 0;
			for (var i = 0; i < 6; i++)
			{
				var tab = (Rectangle)GeometryType.GetMethod("TabBounds").Invoke(layout, new object[] { i, 6 });
				Assert.That(Bounds("Tabs").Contains(tab), Is.True);
				Assert.That(tab.Top, Is.GreaterThanOrEqualTo(lastBottom));
				Assert.That(tab.Height, Is.InRange(32, 44));
				lastBottom = tab.Bottom;
			}
		}

		[Test]
		public void DesktopAiRowsStayCompactWhenShowingEditingAndResizing()
		{
			var type = typeof(SettingsLogic).Assembly.GetType("OpenRA.Mods.Common.Widgets.Logic.DesktopAiSettingsLayout");
			Assert.That(type, Is.Not.Null);
			var apply = type.GetMethod("Apply");
			var scroll = EmptyWidget<ScrollPanelWidget>("SETTINGS_SCROLLPANEL");
			scroll.Layout = new ListLayout(scroll);
			var editing = false;
			foreach (var id in new[] { "INTERVAL", "WAVE", "INCOME", "SPEED" })
			{
				var row = EmptyWidget<ContainerWidget>("AI_" + id + "_ROW");
				row.IsVisible = () => editing;
				row.AddChild(EmptyWidget<LabelWidget>("AI_" + id + "_LABEL"));
				row.AddChild(EmptyWidget<LabelWidget>("AI_" + id + "_VALUE"));
				row.AddChild(EmptyWidget<SliderWidget>("AI_" + id));
				scroll.AddChild(row);
			}
			foreach (var width in new[] { 900, 600, 1100, 900 })
			{
				scroll.Bounds = new WidgetBounds(0, 0, width, 500);
				apply.Invoke(null, new object[] { scroll });
				editing = true;
				scroll.Layout.AdjustChildren();
				var bottom = 0;
				foreach (var row in scroll.Children)
				{
					var label = row.Children.OfType<LabelWidget>().First();
					var slider = row.Children.OfType<SliderWidget>().Single();
					Assert.That(row.Bounds.Height, Is.InRange(36, 48));
					Assert.That(slider.Bounds.Height, Is.EqualTo(36));
					Assert.That(label.Bounds.X + label.Bounds.Width, Is.LessThanOrEqualTo(slider.Bounds.X));
					Assert.That(slider.Bounds.X + slider.Bounds.Width, Is.LessThanOrEqualTo(row.Bounds.Width));
					Assert.That(row.Bounds.Y, Is.GreaterThanOrEqualTo(bottom));
					bottom = row.Bounds.Y + row.Bounds.Height;
				}
				Assert.That(scroll.ContentHeight, Is.LessThan(220));
			}
		}
		[Test]
		public void GeneralDesktopRowsResizeFromOriginalAndReserveHiddenControlHeight()
		{
			Assert.That(GeometryType, Is.Not.Null);
			var method = GeometryType.GetMethod("ScaleRowFromOriginal");
			var row = EmptyWidget<ContainerWidget>("ROW");
			row.Bounds = new WidgetBounds(0, 0, 600, 50);
			var label = EmptyWidget<LabelWidget>("LABEL");
			label.Bounds = new WidgetBounds(10, 0, 280, 20);
			row.AddChild(label);
			var dropdown = EmptyWidget<DropDownButtonWidget>("SETTING");
			dropdown.Bounds = new WidgetBounds(10, 25, 280, 25);
			dropdown.IsVisible = () => false;
			row.AddChild(dropdown);
			var original = new Dictionary<Widget, WidgetBounds> { { row, row.Bounds }, { label, label.Bounds }, { dropdown, dropdown.Bounds } };
			foreach (var scale in new[] { 1d, 0.8, 1.5, 1d, 1d })
			{
				method.Invoke(null, new object[] { row, (Func<Widget, WidgetBounds>)(w => original[w]), scale });
				Assert.That(row.Bounds.Width, Is.EqualTo((int)Math.Round(600 * scale)));
				Assert.That(dropdown.Bounds.Width, Is.EqualTo((int)Math.Round(280 * scale)));
				Assert.That(dropdown.Bounds.Height, Is.EqualTo(36));
				Assert.That(row.Bounds.Height, Is.EqualTo(66));
				Assert.That(label.Bounds.Y + label.Bounds.Height, Is.LessThanOrEqualTo(dropdown.Bounds.Y));
			}
			dropdown.IsVisible = () => true;
			Assert.That(dropdown.Bounds.Y + dropdown.Bounds.Height, Is.LessThanOrEqualTo(row.Bounds.Height));
		}

	}
}
