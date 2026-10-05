// Copyright (c) The OpenRA Developers and Contributors
// Licensed under the GNU General Public License v3.
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using NUnit.Framework;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Test
{
	[TestFixture, NonParallelizable]
	public sealed class SingleColumnProductionSwipeTest
	{
		const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
		ProductionPaletteWidget palette;
		PlatformType? previousPlatform;
		Widget previousFocus, previousOver, previousRoot;
		int2 previousMouse;
		long previousMove;
		static void Set(object target, string field, object value) => target.GetType().GetField(field, Fields).SetValue(target, value);

		[SetUp]
		public void SetUp()
		{
			previousPlatform = (PlatformType?)typeof(Platform).GetField("hostPlatformOverride", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
			previousFocus = Ui.MouseFocusWidget; previousOver = Ui.MouseOverWidget; previousRoot = Ui.Root;
			previousMouse = Viewport.LastMousePos; previousMove = Viewport.LastMoveRunTime;
			Platform.OverrideHostPlatform(PlatformType.iOS); Ui.MouseFocusWidget = null; Ui.MouseOverWidget = null;
			Ui.Root = new ContainerWidget();
			palette = (ProductionPaletteWidget)FormatterServices.GetUninitializedObject(typeof(ProductionPaletteWidget));
			var defaults = new ContainerWidget();
			foreach (var field in typeof(Widget).GetFields(Fields)) field.SetValue(palette, field.GetValue(defaults));
			Set(palette, "icons", new Dictionary<Rectangle, ProductionIcon>());
			Set(palette, "TapSlop", 12); Set(palette, "SwipeThreshold", 36); Set(palette, "TouchScrollStep", 24);
			palette.IconSize = new int2(120, 90); palette.Columns = 1; palette.MaxIconRowOffset = 3;
			Set(palette, "<TotalIconCount>k__BackingField", 20);
		}

		[TearDown]
		public void TearDown()
		{
			typeof(Platform).GetField("hostPlatformOverride", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, previousPlatform);
			Ui.MouseFocusWidget = previousFocus; Ui.MouseOverWidget = previousOver; Ui.Root = previousRoot;
			Viewport.LastMousePos = previousMouse; Viewport.LastMoveRunTime = previousMove;
		}

		void Input(MouseInputEvent type, int deltaY)
		{
			var position = new int2(100, 500 + deltaY);
			Viewport.LastMousePos = position;
			Assert.That(palette.HandleMouseInput(new MouseInput(type, MouseButton.Left, position, int2.Zero, Modifiers.None, 0, true)), Is.True);
		}

		[TestCase(-31, 3)]
		[TestCase(-240, 3)]
		[TestCase(31, -3)]
		[TestCase(240, -3)]
		public void OneVerticalGestureChangesExactlyThreeItemsRegardlessOfDistance(int delta, int change)
		{
			palette.IconRowOffset = 6;
			Input(MouseInputEvent.Down, 0); Input(MouseInputEvent.Move, delta); Input(MouseInputEvent.Up, delta);
			Assert.That(palette.IconRowOffset, Is.EqualTo(6 + change));
		}

		[Test]
		public void RepeatedMovesAndReleaseDoNotRepeatThePageAndNextGestureCanPageAgain()
		{
			Input(MouseInputEvent.Down, 0);
			foreach (var delta in new[] { -31, -60, -120, -240 }) Input(MouseInputEvent.Move, delta);
			Input(MouseInputEvent.Up, -240);
			Assert.That(palette.IconRowOffset, Is.EqualTo(3));
			Input(MouseInputEvent.Down, 0); Input(MouseInputEvent.Up, -45);
			Assert.That(palette.IconRowOffset, Is.EqualTo(6));
		}

		[Test]
		public void PagingClampsAtBothEndsAndSmallDragsDoNotPage()
		{
			palette.IconRowOffset = 16;
			Input(MouseInputEvent.Down, 0); Input(MouseInputEvent.Up, -100);
			Assert.That(palette.IconRowOffset, Is.EqualTo(17));
			palette.IconRowOffset = 1;
			Input(MouseInputEvent.Down, 0); Input(MouseInputEvent.Up, 100);
			Assert.That(palette.IconRowOffset, Is.Zero);
			Input(MouseInputEvent.Down, 0); Input(MouseInputEvent.Move, -20); Input(MouseInputEvent.Up, -20);
			Assert.That(palette.IconRowOffset, Is.Zero);
		}

		[Test]
		public void CompactThreeColumnDragKeepsItsExistingDistanceBasedRows()
		{
			palette.Columns = 3;
			Input(MouseInputEvent.Down, 0); Input(MouseInputEvent.Move, -60); Input(MouseInputEvent.Up, -60);
			Assert.That(palette.IconRowOffset, Is.EqualTo(2));
		}
	}
}
