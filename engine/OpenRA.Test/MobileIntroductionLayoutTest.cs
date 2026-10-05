using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using NUnit.Framework;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Mods.Common.Widgets.Logic;
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Test
{
	[TestFixture]
	public class MobileIntroductionLayoutTest
	{
		static T Bare<T>(string id) where T : Widget
		{
#pragma warning disable SYSLIB0050
			var widget = (T)FormatterServices.GetUninitializedObject(typeof(T));
#pragma warning restore SYSLIB0050
			typeof(Widget).GetField("Children").SetValue(widget, new List<Widget>());
			widget.Id = id; widget.IsVisible = () => true;
			return widget;
		}

		[TestCase(667, 375, 667, 375)]
		[TestCase(1558, 720, 844, 390)]
		[TestCase(2400, 1080, 914, 411)]
		[TestCase(2266, 1488, 1133, 744)]
		public void PromptKeepsControlsReadableInsideSafeAreaAndFooter(int w, int h, int pw, int ph)
		{
			var layout = IosSettingsLayout.ForScreen(true, new Size(w, h), new Size(pw, ph), default);
			var prompt = Bare<BackgroundWidget>("PROMPT");
			prompt.AddChild(Bare<BackgroundWidget>("NUKE_HOUR_DESKTOP_PANEL"));
			foreach (var id in new[] { "PROMPT_TITLE", "DESC_A", "DESC_B" }) prompt.AddChild(Bare<LabelWidget>(id));
			var scroll = Bare<ScrollPanelWidget>("SETTINGS_SCROLLPANEL"); prompt.AddChild(scroll); scroll.Layout = new ListLayout(scroll);
			foreach (var id in new[] { "PROFILE_SECTION_HEADER", "INPUT_SECTION_HEADER", "DISPLAY_SECTION_HEADER" })
			{
				var header = Bare<BackgroundWidget>(id); header.AddChild(Bare<LabelWidget>("LABEL")); scroll.AddChild(header);
			}
			void AddColumn(string id, Widget control)
			{
				var column = new ContainerWidget { Id = id }; column.AddChild(Bare<LabelWidget>(id + "_LABEL")); column.AddChild(control); scroll.AddChild(column);
			}
			AddColumn("PLAYER_CONTAINER", Bare<TextFieldWidget>("PLAYERNAME"));
			var color = Bare<DropDownButtonWidget>("PLAYERCOLOR"); color.AddChild(Bare<ColorBlockWidget>("COLORBLOCK"));
			AddColumn("PLAYERCOLOR_CONTAINER", color);
			AddColumn("MOUSE_CONTROL_CONTAINER", Bare<DropDownButtonWidget>("MOUSE_CONTROL_DROPDOWN"));
			AddColumn("EDGESCROLL_CHECKBOX_CONTAINER", Bare<CheckboxWidget>("EDGESCROLL_CHECKBOX"));
			AddColumn("BATTLEFIELD_CAMERA_DROPDOWN_CONTAINER", Bare<DropDownButtonWidget>("BATTLEFIELD_CAMERA_DROPDOWN"));
			AddColumn("UI_SCALE_DROPDOWN_CONTAINER", Bare<DropDownButtonWidget>("UI_SCALE_DROPDOWN"));
			AddColumn("CURSORDOUBLE_CHECKBOX_CONTAINER", Bare<CheckboxWidget>("CURSORDOUBLE_CHECKBOX"));
			var proceed = Bare<ButtonWidget>("CONTINUE_BUTTON"); prompt.AddChild(proceed);
			IntroductionPromptLogic.ApplyMobilePresentation(prompt, layout);
			IntroductionPromptLogic.ApplyMobilePresentation(prompt, layout);
			Assert.That(prompt.Bounds.X, Is.GreaterThanOrEqualTo(layout.SafeBounds.X));
			Assert.That(prompt.Bounds.Right, Is.LessThanOrEqualTo(layout.SafeBounds.Right));
			if (layout.IsPhone) Assert.That(prompt.Bounds.Width, Is.EqualTo(layout.SafeBounds.Width - 2 * layout.Scale(2)));
			else Assert.That(prompt.Bounds.ToRectangle(), Is.EqualTo(layout.Window));
			Assert.That(scroll.Bounds.Bottom, Is.LessThan(proceed.Bounds.Y));
			Assert.That(proceed.Bounds.Bottom, Is.LessThanOrEqualTo(prompt.Bounds.Height));
			Assert.That(scroll.EnableContentDragging, Is.True);
			foreach (var control in scroll.Children.Where(c => c.IsVisible()).SelectMany(c => c.Children).OfType<ButtonWidget>())
			{
				Assert.That(control.Bounds.Height, Is.GreaterThanOrEqualTo(layout.MinimumTarget));
				Assert.That(control.Bounds.Right, Is.LessThanOrEqualTo(control.Parent.Bounds.Width));
			}
			Assert.That(scroll.Get("PLAYER_CONTAINER").Bounds.Right, Is.LessThan(scroll.Get("PLAYERCOLOR_CONTAINER").Bounds.X));
			Assert.That(scroll.ContentHeight, Is.GreaterThan(0));
			if (layout.IsPhone) Assert.That(scroll.ContentHeight, Is.LessThanOrEqualTo(scroll.Bounds.Height));
		}
	}
}
