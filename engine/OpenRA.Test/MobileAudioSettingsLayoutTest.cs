using System;
using System.Linq;
using NUnit.Framework;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Mods.Common.Widgets.Logic;
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Test
{
	[TestFixture]
	public class MobileAudioSettingsLayoutTest
	{
		[TestCase(667, 375, 667, 375)]
		[TestCase(1558, 720, 844, 390)]
		[TestCase(2400, 1080, 914, 411)]
		public void VolumeControlsHaveEqualWidthsAndAlignWithRelatedSwitches(int width, int height, int pointsWidth, int pointsHeight)
		{
			var layout = IosSettingsLayout.ForScreen(true, new Size(width, height), new Size(pointsWidth, pointsHeight), default);
			var scroll = (ScrollPanelWidget)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(ScrollPanelWidget));
			typeof(Widget).GetField("Children")!.SetValue(scroll, new System.Collections.Generic.List<Widget>());
			scroll.Bounds = new WidgetBounds(0, 0, layout.Content.Width, layout.Content.Height);
			scroll.Layout = new ListLayout(scroll);
			scroll.TopBottomSpacing = layout.Scale(4); scroll.ItemSpacing = layout.Scale(6);
			var sliders = new[] { "SOUND", "MUSIC", "VIDEO" };
			var switches = new[] { "MUTE_SOUND", "MUTE_BACKGROUND_MUSIC", "CASH_TICKS" };
			foreach (var id in sliders)
			{
				var column = new ContainerWidget { Id = id + "_VOLUME_CONTAINER" };
				var label = (LabelWidget)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(LabelWidget));
				typeof(Widget).GetField("Children")!.SetValue(label, new System.Collections.Generic.List<Widget>());
				label.Id = id + "_LABEL"; label.IsVisible = () => true; column.AddChild(label);
#pragma warning disable SYSLIB0050
				var slider = (SliderWidget)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(SliderWidget));
#pragma warning restore SYSLIB0050
				typeof(Widget).GetField("Children")!.SetValue(slider, new System.Collections.Generic.List<Widget>());
				typeof(Widget).GetField("Children")!.SetValue(slider, new System.Collections.Generic.List<Widget>());
				slider.Id = id + "_VOLUME"; slider.IsVisible = () => true;
				column.AddChild(slider); scroll.AddChild(column);
			}
			foreach (var id in switches)
			{
				var column = new ContainerWidget { Id = id + "_CONTAINER" };
#pragma warning disable SYSLIB0050
				var checkbox = (CheckboxWidget)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(CheckboxWidget));
#pragma warning restore SYSLIB0050
				typeof(Widget).GetField("Children")!.SetValue(checkbox, new System.Collections.Generic.List<Widget>());
				typeof(Widget).GetField("Children")!.SetValue(checkbox, new System.Collections.Generic.List<Widget>());
				checkbox.Id = id; checkbox.IsVisible = () => true;
				column.AddChild(checkbox); scroll.AddChild(column);
			}
			MobileAudioSettingsLayout.Apply(scroll, layout);
			MobileAudioSettingsLayout.Apply(scroll, layout);
			var actual = sliders.Select(id => scroll.Get<SliderWidget>(id + "_VOLUME")).ToArray();
			Assert.That(actual.Select(s => s.Bounds.Width).Distinct().Count(), Is.EqualTo(1));
			for (var i = 0; i < 3; i++)
			{
				var slider = actual[i]; var checkbox = scroll.Get<CheckboxWidget>(switches[i]);
				Assert.That(slider.RenderBounds.Y, Is.EqualTo(checkbox.RenderBounds.Y));
				Assert.That(slider.RenderBounds.IntersectsWith(checkbox.RenderBounds), Is.False);
				Assert.That(slider.Bounds.Height, Is.GreaterThanOrEqualTo(layout.MinimumTarget));
				Assert.That(slider.Parent.Bounds.Width, Is.GreaterThanOrEqualTo(slider.Bounds.Right));
				Assert.That(slider.Bounds.Left, Is.GreaterThanOrEqualTo(scroll.Get<LabelWidget>(sliders[i] + "_LABEL").Bounds.Right));
			}
			Assert.That(scroll.ContentHeight, Is.LessThanOrEqualTo(scroll.Bounds.Height));
		}
	}
}
