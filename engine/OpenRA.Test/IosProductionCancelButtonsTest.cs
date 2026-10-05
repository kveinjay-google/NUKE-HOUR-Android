#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version.
 */
#endregion

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using NUnit.Framework;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Mods.Common.Widgets.Logic;
using OpenRA.Widgets;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class IosProductionCancelButtonsTest
	{
		[TestCase(false, 5, 14)]
		[TestCase(false, 9, 10)]
		[TestCase(true, 5, 14)]
		[TestCase(true, 9, 10)]
		public void CancelLabelCentersActualGlyphsInsideFaceWithoutTouchingTrim(bool modern, int bearing, int height)
		{
			var button = new OpenRA.Primitives.Rectangle(1700, 810, 300, 60);
			var face = ProductionFooterButtonWidget.LabelFaceBounds(button, modern);
			var glyphs = new OpenRA.Primitives.Rectangle(-1, bearing, 80, height);
			var origin = ProductionFooterButtonWidget.CenterGlyphs(face, glyphs);
			Assert.That(origin.Y + glyphs.Y + height / 2f, Is.EqualTo(face.Y + face.Height / 2f));
			Assert.That(origin.X + glyphs.X + 40, Is.EqualTo(face.X + face.Width / 2f));
			Assert.That(origin.Y + glyphs.Y - 1, Is.GreaterThan(face.Top));
			Assert.That(origin.Y + glyphs.Bottom + 1, Is.LessThan(face.Bottom));
			Assert.That(face.Bottom, Is.LessThan(button.Bottom - 10));
		}
		[TestCase(24, 0, true)]
		[TestCase(-40, 5, true)]
		[TestCase(23, 0, false)]
		[TestCase(30, 40, false)]
		public void FooterSwipeRejectsTapsAndVerticalDrags(int x, int y, bool expected)
		{
			Assert.That(ButtonWidget.IsHorizontalSwipe(new int2(x, y)), Is.EqualTo(expected));
		}

		[TestCase(false, 4, 1, 0)]
		[TestCase(false, 40, 0, 1)]
		[TestCase(true, 4, 0, 0)]
		[TestCase(true, -40, 0, 1)]
		[NonParallelizable]
		public void DisabledFooterStillSwipesAndSwipeNeverCancels(bool disabled, int dx, int clicks, int swipes)
		{
			var previous = Ui.MouseFocusWidget;
			try
			{
				Ui.MouseFocusWidget = null;
				var button = WidgetWithoutRenderer<ButtonWidget>("footer");
				button.Bounds = new WidgetBounds(0, 0, 120, 50);
				button.IsDisabled = () => disabled;
				var actualClicks = 0;
				var actualSwipes = 0;
				button.OnMouseUp = _ => actualClicks++;
				button.OnHorizontalSwipe = () => actualSwipes++;
				button.HandleMouseInput(new MouseInput { Event = MouseInputEvent.Down,
					Button = MouseButton.Left, Location = new int2(60, 20) });
				Assert.That(button.IsFooterPressActive, Is.True, "Press feedback also applies with an empty queue.");
				button.HandleMouseInput(new MouseInput { Event = MouseInputEvent.Up,
					Button = MouseButton.Left, Location = new int2(60 + dx, 20) });
				Assert.That(actualClicks, Is.EqualTo(clicks));
				Assert.That(actualSwipes, Is.EqualTo(swipes));
				Assert.That(button.HasMouseFocus, Is.False);
			}
			finally { Ui.MouseFocusWidget = previous; }
		}

		[Test]
		public void SingleCancelButtonExactlyUsesTheTwoOriginalSlots()
		{
			var first = new WidgetBounds(89, 305, 77, 27);
			var second = new WidgetBounds(12, 305, 77, 27);
			Assert.That(IosProductionFooterPolicy.CombinedBounds(first, second),
				Is.EqualTo(new WidgetBounds(12, 305, 154, 27)));
		}

		static FieldInfo Preference()
		{
			var field = typeof(GameSettings).GetField("IosProductionCancelButtons");
			Assert.That(field, Is.Not.Null, "The opt-in production Cancel preference must be persisted.");
			return field;
		}

		static T WidgetWithoutRenderer<T>(string id) where T : Widget
		{
			var widget = (T)FormatterServices.GetUninitializedObject(typeof(T));
			typeof(Widget).GetField(nameof(Widget.Children)).SetValue(widget, new List<Widget>());
			widget.Id = id;
			widget.IsVisible = () => true;
			return widget;
		}

		static Action BindFooter(ButtonWidget button, bool isIos, GameSettings settings,
			Func<bool> canCancel, Action cancel)
		{
			var policy = typeof(ClassicProductionLogic).Assembly.GetType("OpenRA.Mods.Common.Widgets.Logic.IosProductionFooterPolicy");
			Assert.That(policy, Is.Not.Null);
			return (Action)policy.GetMethod("Bind").Invoke(null,
				new object[] { button, isIos, settings, canCancel, cancel, (Func<string>)(() => "Cancel") });
		}

		[Test]
		public void CancelButtonsDefaultOnAndResetRestoresOn()
		{
			var settings = new GameSettings();
			var preference = Preference();
			Assert.That(preference.GetValue(settings), Is.True);
			preference.SetValue(settings, false);
			var reset = typeof(InputSettingsLogic).GetMethod("ResetIosProductionCancelButtons");
			Assert.That(reset, Is.Not.Null);
			reset.Invoke(null, new object[] { settings });
			Assert.That(preference.GetValue(settings), Is.True);
		}

		[Test]
		public void EnabledPreferenceSurvivesSettingsSaveAndReload()
		{
			var path = Path.Combine(TestContext.CurrentContext.WorkDirectory, $"cancel-buttons-{Guid.NewGuid():N}.yaml");
			try
			{
				var settings = new Settings(path, new Arguments());
				Preference().SetValue(settings.Game, true);
				settings.Save();
				Assert.That(Preference().GetValue(new Settings(path, new Arguments()).Game), Is.True);
			}
			finally
			{
				if (File.Exists(path))
					File.Delete(path);
			}
		}

		[TestCase(false)]
		[TestCase(true)]
		public void ReturningFromHdReappliesCurrentFooterMode(bool initiallyCancel)
		{
			var settings = new GameSettings { IosProductionCancelButtons = initiallyCancel };
			var button = WidgetWithoutRenderer<ButtonWidget>("SCROLL_UP_BUTTON");
			button.Background = "scrollup-buttons-allies";
			button.GetText = () => "";
			button.IsVisible = () => false;
			var refresh = BindFooter(button, true, settings, () => true, () => { });
			var oldBackground = button.Background;
			var oldText = button.GetText;
			var oldVisible = button.IsVisible;
			settings.IosProductionCancelButtons = !initiallyCancel;
			refresh();
			button.Background = oldBackground;
			button.GetText = oldText;
			button.IsVisible = oldVisible;
			var restore = typeof(IosProductionFooterPolicy).GetMethod("RestoreCurrentMode");
			Assert.That(restore, Is.Not.Null);
			restore.Invoke(null, new object[] { button });
			Assert.That(button.IsVisible(), Is.EqualTo(!initiallyCancel));
			Assert.That(button.GetText(), Is.EqualTo(initiallyCancel ? "" : "Cancel"));
			Assert.That(button.Background, Is.EqualTo(initiallyCancel ? "scrollup-buttons-allies" : "button"));
		}

		[TestCase(false, false)]
		[TestCase(false, true)]
		[TestCase(true, false)]
		[TestCase(true, true)]
		public void FooterUsesCancelOnlyForIosOptInAndRestoresOriginalState(bool isIos, bool enabled)
		{
			var settings = new GameSettings();
			Preference().SetValue(settings, enabled);
			var button = WidgetWithoutRenderer<ButtonWidget>("SCROLL_UP_BUTTON");
			button.Background = "scrollup-buttons-soviet";
			button.Bounds = new WidgetBounds(89, 305, 77, 27);
			Func<string> text = () => "original text";
			Func<string> tooltip = () => "original tooltip";
			Func<string> description = () => "original description";
			Func<bool> visible = () => false;
			Func<bool> disabled = () => true;
			var scrolls = 0;
			Action scroll = () => scrolls++;
			button.GetText = text;
			button.GetTooltipText = tooltip;
			button.GetTooltipDesc = description;
			button.IsVisible = visible;
			button.IsDisabled = disabled;
			button.OnClick = scroll;
			var cancellable = false;
			var cancellations = 0;
			var refresh = BindFooter(button, isIos, settings, () => cancellable, () => cancellations++);
			refresh();
			Assert.That(button.Bounds, Is.EqualTo(new WidgetBounds(89, 305, 77, 27)));
			if (isIos && enabled)
			{
				Assert.That(button.Background, Is.EqualTo("button"));
				Assert.That(button.GetText(), Is.EqualTo("Cancel"));
				Assert.That(button.GetTooltipText(), Is.EqualTo("Cancel"));
				Assert.That(button.GetTooltipDesc(), Is.Empty);
				Assert.That(button.IsVisible(), Is.True);
				Assert.That(button.IsDisabled(), Is.True);
				cancellable = true;
				Assert.That(button.IsDisabled(), Is.False);
				button.OnClick();
				Assert.That(cancellations, Is.EqualTo(1));
				Assert.That(scrolls, Is.Zero);
				var cancelAction = button.OnClick;
				refresh();
				Assert.That(button.OnClick, Is.SameAs(cancelAction), "An unchanged mode must retain delegates.");
				Preference().SetValue(settings, false);
				refresh();
			}

			Assert.That(button.Background, Is.EqualTo("scrollup-buttons-soviet"));
			Assert.That(button.GetText, Is.SameAs(text));
			Assert.That(button.GetTooltipText, Is.SameAs(tooltip));
			Assert.That(button.GetTooltipDesc, Is.SameAs(description));
			Assert.That(button.IsVisible, Is.SameAs(visible));
			Assert.That(button.IsDisabled, Is.SameAs(disabled));
			Assert.That(button.OnClick, Is.SameAs(scroll));
			button.OnClick();
			Assert.That(scrolls, Is.EqualTo(1));
		}

		[TestCase(false)]
		[TestCase(true)]
		public void CheckboxIsIosOnlyAndChangesLivePreference(bool isIos)
		{
			var settings = new GameSettings();
			var panel = new ContainerWidget();
			var row = new ContainerWidget { Id = "TOUCH_PRODUCTION_CANCEL_CONTAINER" };
			var checkbox = WidgetWithoutRenderer<CheckboxWidget>("TOUCH_PRODUCTION_CANCEL_CHECKBOX");
			row.AddChild(checkbox);
			panel.AddChild(row);
			var bind = typeof(InputSettingsLogic).GetMethod("BindIosProductionCancelButtons");
			Assert.That(bind, Is.Not.Null);
			bind.Invoke(null, new object[] { panel, settings, isIos });
			Assert.That(row.IsVisible(), Is.EqualTo(isIos));
			Assert.That(checkbox.IsChecked(), Is.True);
			checkbox.OnClick();
			Assert.That(Preference().GetValue(settings), Is.False);
			Assert.That(checkbox.IsChecked(), Is.False);
		}

		[Test]
		public void SharedInputPanelAndBothFooterSlotsAreBoundWithLocalizedHelp()
		{
			var root = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
			while (root != null && !File.Exists(Path.Combine(root.FullName, "OpenRA.Mods.RA2.sln")))
				root = root.Parent;
			Assert.That(root, Is.Not.Null);
			var yaml = File.ReadAllText(Path.Combine(root.FullName, "mods/ra2/chrome/settings-input.yaml"));
			Assert.That(yaml, Does.Contain("Checkbox@TOUCH_PRODUCTION_CANCEL_CHECKBOX:"));
			Assert.That(yaml, Does.Contain("label-touch-production-cancel-description"));
			var logic = File.ReadAllText(Path.Combine(root.FullName,
				"engine/OpenRA.Mods.Common/Widgets/Logic/Settings/InputSettingsLogic.cs"));
			Assert.That(logic, Does.Contain("BindIosProductionCancelButtons(panel, gs, Platform.UsesMobileLayout)"));
			Assert.That(logic, Does.Contain("ResetIosProductionCancelButtons(gs)"));
			var production = File.ReadAllText(Path.Combine(root.FullName,
				"engine/OpenRA.Mods.Common/Widgets/Logic/Ingame/ClassicProductionLogic.cs"));
			Assert.That(production, Does.Contain("BindCancelButton(scrollUp)"));
			Assert.That(production, Does.Contain("BindCancelButton(scrollDown)"));
			Assert.That(production, Does.Contain("palette.CanCancelSelectedProduction"));
			Assert.That(production, Does.Contain("palette.CancelSelectedProduction"));
			foreach (var file in new[] { "mods/ra2/fluent/mod.ftl", "mods/ra2/fluent/zh-CN/mod.ftl" })
			{
				var fluent = File.ReadAllText(Path.Combine(root.FullName, file));
				Assert.That(fluent, Does.Contain("checkbox-touch-production-cancel ="));
				Assert.That(fluent, Does.Contain("label-touch-production-cancel-description ="));
				Assert.That(fluent, Does.Contain("button-touch-production-cancel ="));
			}
		}
	}
}
