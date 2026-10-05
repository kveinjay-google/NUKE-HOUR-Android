#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software. It is
 * made available under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version.
 */
#endregion

using System.Reflection;
using System;
using NUnit.Framework;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Mods.Common.Widgets.Logic;
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class SettingsPresentationLayoutTest
	{
		[Test]
		public void StableIosScreenMetricsDoNotRequestASettingsRelayout()
		{
			var viewport = new Size(1558, 720);
			var snapshot = new IosScreenSnapshot(viewport, new Size(844, 390),
				new IosSafeAreaInsets(47, 0, 47, 21));
			Assert.That(SettingsLogic.NeedsIosRelayout(
				viewport, viewport, snapshot, snapshot.NativePointSize, snapshot.SafeBounds), Is.False);
			Assert.That(SettingsLogic.NeedsIosRelayout(
				new Size(1560, 720), viewport, snapshot, snapshot.NativePointSize, snapshot.SafeBounds), Is.True);
			var changedSafeArea = new IosScreenSnapshot(viewport, snapshot.NativePointSize,
				new IosSafeAreaInsets(59, 0, 59, 21));
			Assert.That(SettingsLogic.NeedsIosRelayout(
				viewport, viewport, changedSafeArea, snapshot.NativePointSize, snapshot.SafeBounds), Is.True);
		}

		[Test]
		public void PopupPreparationIsOptInAndSettingsTouchBoundsStayLocal()
		{
			var ordinary = BarePopupWidget<DropDownButtonWidget>();
			Assert.That(ordinary.PreparePanel, Is.Null);
			Assert.That(ordinary.GetPopupSafeBounds, Is.Null);
			var settings = BarePopupWidget<DropDownButtonWidget>();
			typeof(SettingsLogic).GetMethod("ApplyDesktopFont", BindingFlags.Static | BindingFlags.NonPublic)!
				.Invoke(null, new object[] { settings });
			Assert.That(settings.PreparePanel, Is.Not.Null);
			var phone = IosSettingsLayout.ForPreview("phone", new Size(844, 390));
			typeof(SettingsLogic).GetMethod("ConfigureIosTouchWidgets", BindingFlags.Static | BindingFlags.NonPublic)!
				.Invoke(null, new object[] { settings, phone });
			Assert.That(settings.GetPopupSafeBounds(), Is.EqualTo(phone.SafeBounds));
			Assert.That(ordinary.PreparePanel, Is.Null);
		}

		[TestCase(null, 1180, 820, 32, "SettingsRegular")]
		[TestCase("phone", 844, 390, 48, "IosRegular")]
		[TestCase("tablet", 1180, 820, 48, "IosRegular")]
		[TestCase("native", 1688, 780, 96, "IosRegular")]
		public void SettingsPopupUsesReadableFieldsAndRows(string preview, int width, int height, int target, string font)
		{
			var layout = preview == "native" ? IosSettingsLayout.ForScreen(true, new Size(width, height),
				new Size(844, 390), new IosSafeAreaInsets(47, 0, 47, 21)) :
				IosSettingsLayout.ForPreview(preview, new Size(width, height));
			var panel = BarePopupWidget<ScrollPanelWidget>();
			panel.Bounds = new WidgetBounds(0, 0, 300, 75);
			panel.Layout = new ListLayout(panel);
			for (var i = 0; i < 3; i++)
			{
				var item = BarePopupWidget<ScrollItemWidget>();
				item.Bounds = new WidgetBounds(0, 0, 270, 25);
				var label = BarePopupWidget<LabelWidget>();
				label.Bounds = new WidgetBounds(8, 0, 260, 25);
				item.AddChild(label);
				panel.AddChild(item);
			}

			var prepare = typeof(SettingsLogic).GetMethod("PrepareSettingsDropDownPanel", BindingFlags.Static | BindingFlags.NonPublic);
			Assert.That(prepare, Is.Not.Null, "Dynamically created settings popups need their own presentation pass.");
			prepare!.Invoke(null, new object[] { panel, layout });
			Assert.That(panel.Background, Is.EqualTo("settings-v2-control"));
			Assert.That(panel.Button, Is.EqualTo("settings-v2-control"));
			Assert.That(panel.EnableContentDragging, Is.EqualTo(layout.Enabled));
			Assert.That(panel.Bounds.Height, Is.GreaterThanOrEqualTo(3 * target));
			foreach (ScrollItemWidget item in panel.Children)
			{
				Assert.That(item.Bounds.Height, Is.GreaterThanOrEqualTo(target));
				Assert.That(item.Background, Is.EqualTo("settings-v2-control"));
				var label = (LabelWidget)item.Children[0];
				Assert.That(label.Font, Is.EqualTo(font));
				Assert.That(label.Bounds.Height, Is.EqualTo(item.Bounds.Height));
				Assert.That(label.Bounds.Right, Is.LessThanOrEqualTo(item.Bounds.Width));
			}
		}

		static T BarePopupWidget<T>() where T : Widget
		{
			#pragma warning disable SYSLIB0050
			var widget = (T)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(T));
			#pragma warning restore SYSLIB0050
			typeof(Widget).GetField(nameof(Widget.Children))!.SetValue(widget, new System.Collections.Generic.List<Widget>());
			widget.IsVisible = () => true;
			return widget;
		}

		[Test]
		public void PhoneKeepsADirectControlBelowItsSiblingLabel()
		{
			var row = new ContainerWidget { Bounds = new WidgetBounds(0, 0, 650, 80) };
			var label = new ContainerWidget { Id = "LABEL", Bounds = new WidgetBounds(10, 0, 630, 20) };
			var control = new SliderWidget { Bounds = new WidgetBounds(10, 25, 630, 48) };
			row.AddChild(label);
			row.AddChild(control);
			var original = control.Bounds.ToRectangle();
			typeof(SettingsLogic).GetMethod("ReflowIosVisibleColumns", BindingFlags.Static | BindingFlags.NonPublic)!
				.Invoke(null, new object[] { row, IosSettingsLayout.ForViewport(true, 844, 390) });
			Assert.That(control.Bounds.ToRectangle(), Is.EqualTo(original));
			Assert.That(control.Bounds.Y, Is.GreaterThanOrEqualTo(label.Bounds.Bottom));
		}

		[TestCase(844, 390, 844, 390, 92)]
		[TestCase(812, 375, 812, 375, 80)]
		[TestCase(1558, 720, 844, 390, 92)]
		[TestCase(1559, 720, 812, 375, 80)]
		public void CompactPhonesReserveAFullHotkeyTouchRowAndRemapNotices(
			int effectiveWidth, int effectiveHeight, int nativeWidth, int nativeHeight, int minimumListHeight)
		{
			var layout = IosSettingsLayout.ForScreen(true, new Size(effectiveWidth, effectiveHeight),
				new Size(nativeWidth, nativeHeight), new IosSafeAreaInsets(47, 0, 47, 21));
			Assert.That(layout.HotkeyList.Height / layout.LogicalPerPoint, Is.GreaterThanOrEqualTo(minimumListHeight));
			Assert.That(layout.Reset.Height / layout.LogicalPerPoint, Is.GreaterThanOrEqualTo(48));
			Assert.That(layout.HotkeyFooter.Height / layout.LogicalPerPoint, Is.GreaterThanOrEqualTo(64));
			Assert.That(layout.SettingsScrollbarWidth, Is.Zero);
			Assert.That(layout.Tabs.Top - layout.Window.Top, Is.LessThanOrEqualTo(layout.Window.Height * .11));
		}

		[TestCase("跳转至最近的雷达事件:", 6, "跳转至最近的\n雷达事件:")]
		[TestCase("Select all units", 10, "Select all\nunits")]
		[TestCase("A😀B", 2, "A😀\nB")]
		public void SettingsLabelsWrapChineseAndWholeTextElements(string text, int width, string expected)
		{
			var method = typeof(SettingsLogic).GetMethod("WrapSettingsText", BindingFlags.Public | BindingFlags.Static);
			Assert.That(method, Is.Not.Null, "Settings wrapping must support labels without spaces.");
			Func<string, int> measure = value => new System.Globalization.StringInfo(value).LengthInTextElements;
			Assert.That(method!.Invoke(null, new object[] { text, width, measure }), Is.EqualTo(expected));
		}

		[TestCase(2)]
		[TestCase(3)]
		public void PhoneSettingsUseTwoReadableColumnsAndExpandOnlyWhenNeeded(int count)
		{
			var row = new ContainerWidget { Bounds = new WidgetBounds(0, 0, 650, 120) };
			for (var i = 0; i < count; i++)
			{
				var column = new ContainerWidget { Bounds = new WidgetBounds(i * 210, 0, 200, 0) };
				column.AddChild(new SliderWidget { Bounds = new WidgetBounds(0, 36, 200, 48) });
				row.AddChild(column);
			}

			var layout = IosSettingsLayout.ForViewport(true, 844, 390);
			typeof(SettingsLogic).GetMethod("ReflowIosVisibleColumns", BindingFlags.Static | BindingFlags.NonPublic)!
				.Invoke(null, new object[] { row, layout });
			var columns = row.Children;
			Assert.That(columns[0].Bounds.Width, Is.GreaterThanOrEqualTo(280));
			Assert.That(columns[1].Bounds.Width, Is.GreaterThanOrEqualTo(280));
			Assert.That(columns[0].Bounds.Y, Is.EqualTo(columns[1].Bounds.Y));
			Assert.That(columns[0].Bounds.Right, Is.LessThanOrEqualTo(columns[1].Bounds.X));
			if (count == 3)
				Assert.That(columns[2].Bounds.Y, Is.GreaterThan(columns[0].Bounds.Y));
			Assert.That(row.Bounds.Height, Is.GreaterThanOrEqualTo(
				columns[^1].Bounds.Y + columns[^1].Children[0].Bounds.Bottom));
		}

		[Test]
		public void DesktopRowsGrowToReadableTargetsWithoutChangingControlValues()
		{
			var row = new ContainerWidget { Bounds = new WidgetBounds(0, 0, 650, 50) };
			var slider = new SliderWidget { Bounds = new WidgetBounds(10, 25, 300, 20), Value = .37f };
			row.AddChild(slider);
			var method = typeof(SettingsLogic).GetMethod("ScaleDesktopWidgetTree", BindingFlags.Static | BindingFlags.NonPublic);
			Assert.That(method, Is.Not.Null, "Desktop controls need a readable, spacious layout.");
			method!.Invoke(null, new object[] { row, 1.6 });
			Assert.That(slider.Bounds.Height, Is.GreaterThanOrEqualTo(32));
			Assert.That(row.Bounds.Height, Is.GreaterThanOrEqualTo(slider.Bounds.Bottom));
			Assert.That(slider.Value, Is.EqualTo(.37f));
			Assert.That(slider.UseTouchStepControls, Is.False);
		}

		[Test]
		public void TabletNavigationFitsTheShellNavigationRail()
		{
			var layout = IosSettingsLayout.ForViewport(true, 1180, 820);
			Assert.That(layout.Tabs.Left, Is.GreaterThanOrEqualTo(layout.Window.Left + layout.Window.Width * .035));
			Assert.That(layout.Tabs.Right, Is.LessThanOrEqualTo(layout.Window.Left + layout.Window.Width * .19));
			Assert.That(layout.Content.Left, Is.GreaterThanOrEqualTo(layout.Window.Left + layout.Window.Width * .20));
			Assert.That(layout.Content.Right, Is.LessThanOrEqualTo(layout.Window.Left + layout.Window.Width * .96));
		}

		[TestCase(null, false, false)]
		[TestCase("", false, false)]
		[TestCase("yes", false, false)]
		[TestCase("phone", true, true)]
		[TestCase("tablet", true, false)]
		public void PreviewIsExplicitAndUsesTheRequestedNativeDevice(string value, bool enabled, bool phone)
		{
			var method = typeof(IosSettingsLayout).GetMethod("ForPreview", BindingFlags.Static | BindingFlags.Public);
			Assert.That(method, Is.Not.Null, "An opt-in settings-only preview policy is required.");
			var layout = (IosSettingsLayout)method!.Invoke(null, new object[] { value, new Size(1558, 720) });
			Assert.That(layout.Enabled, Is.EqualTo(enabled));
			Assert.That(layout.IsPhone, Is.EqualTo(phone));
			if (enabled)
				Assert.That(layout.MinimumTarget / layout.LogicalPerPoint, Is.GreaterThanOrEqualTo(48));
		}
	}
}
