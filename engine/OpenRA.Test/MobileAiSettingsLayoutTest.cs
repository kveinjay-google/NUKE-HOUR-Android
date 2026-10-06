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
	public class MobileAiSettingsLayoutTest
	{
		static T Widget<T>(string id) where T : Widget
		{
			var widget = (T)FormatterServices.GetUninitializedObject(typeof(T));
			typeof(Widget).GetField("Children").SetValue(widget, new List<Widget>());
			widget.Id = id;
			widget.IsVisible = () => true;
			return widget;
		}

		[TestCase(667, 375, 667, 375)]
		[TestCase(1558, 720, 844, 390)]
		[TestCase(2400, 1080, 914, 411)]
		public void HiddenEditorRowsReflowToFullWidthWithoutOverlappingAfterOpening(int width, int height, int pointsWidth, int pointsHeight)
		{
			var type = typeof(AiDifficultySettingsLogic).Assembly.GetType("OpenRA.Mods.Common.Widgets.Logic.MobileAiSettingsLayout");
			Assert.That(type, Is.Not.Null);
			var apply = type.GetMethod("Apply", BindingFlags.Public | BindingFlags.Static);
			var layout = IosSettingsLayout.ForScreen(true, new Size(width, height), new Size(pointsWidth, pointsHeight), default);
			var scroll = Widget<ScrollPanelWidget>("SETTINGS_SCROLLPANEL");
			scroll.Bounds = new WidgetBounds(0, 0, layout.Content.Width, layout.Content.Height);
			scroll.Layout = new ListLayout(scroll);
			scroll.TopBottomSpacing = layout.Scale(4);
			scroll.ItemSpacing = layout.Scale(6);
			var editing = false;
			var status = Widget<ContainerWidget>("AI_STATUS_ROW");
			status.AddChild(Widget<LabelWidget>("AI_STATUS"));
			scroll.AddChild(status);
			var select = Widget<ContainerWidget>("AI_PRESET_ROW");
			select.IsVisible = () => !editing;
			select.AddChild(Widget<LabelWidget>("AI_PRESET_LABEL"));
			select.AddChild(Widget<DropDownButtonWidget>("AI_PRESET"));
			scroll.AddChild(select);
			foreach (var id in new[] { "INTERVAL", "WAVE", "EXPANSION", "INCOME", "SPEED" })
			{
				var row = Widget<ContainerWidget>("AI_" + id + "_ROW");
				row.IsVisible = () => editing;
				row.AddChild(Widget<LabelWidget>("AI_" + id + "_LABEL"));
				row.AddChild(Widget<LabelWidget>("AI_" + id + "_VALUE"));
				row.AddChild(Widget<SliderWidget>("AI_" + id));
				scroll.AddChild(row);
			}
			var save = Widget<ContainerWidget>("AI_SAVE_ROW");
			save.IsVisible = () => editing;
			save.AddChild(Widget<ButtonWidget>("AI_SAVE"));
			scroll.AddChild(save);
			apply.Invoke(null, new object[] { scroll, layout });
			var selectionHeight = scroll.ContentHeight;
			editing = true;
			scroll.Layout.AdjustChildren();
			Assert.That(scroll.ContentHeight, Is.GreaterThan(selectionHeight));
			var previousBottom = 0;
			foreach (var row in scroll.Children.Where(c => c.IsVisible()))
			{
				Assert.That(row.Bounds.Y, Is.GreaterThanOrEqualTo(previousBottom));
				Assert.That(row.Bounds.Width, Is.GreaterThan(scroll.Bounds.Width * 0.8));
				foreach (var child in row.Children)
				{
					Assert.That(child.Bounds.Y + child.Bounds.Height, Is.LessThanOrEqualTo(row.Bounds.Height));
					Assert.That(child.Bounds.X + child.Bounds.Width, Is.LessThanOrEqualTo(row.Bounds.Width));
					if (child is SliderWidget || child is ButtonWidget)
					{
						Assert.That(child.Bounds.Height, Is.GreaterThanOrEqualTo(layout.MinimumTarget));
						Assert.That(child.Bounds.Width, Is.EqualTo(row.Bounds.Width));
					}
				}
				var slider = row.Children.OfType<SliderWidget>().FirstOrDefault();
				if (slider != null)
					Assert.That(slider.Bounds.Y, Is.GreaterThanOrEqualTo(row.Children.OfType<LabelWidget>().Max(l => l.Bounds.Y + l.Bounds.Height)));
				previousBottom = row.Bounds.Y + row.Bounds.Height;
			}
			Assert.That(scroll.ContentHeight, Is.GreaterThanOrEqualTo(previousBottom));
			editing = false;
			scroll.Layout.AdjustChildren();
			Assert.That(scroll.ContentHeight, Is.EqualTo(selectionHeight));
			apply.Invoke(null, new object[] { scroll, layout });
			Assert.That(scroll.ContentHeight, Is.EqualTo(selectionHeight), "Repeated application must not grow or compress rows.");
		}
	}
}
