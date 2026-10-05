using System;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using OpenRA.Graphics;
using OpenRA.Primitives;
using OpenRA.Widgets;
using NUnit.Framework;
using OpenRA.Mods.RA2.Widgets;
using OpenRA.Mods.Common.Widgets;

namespace OpenRA.Test
{
	[TestFixture]
	public class MobileQuickbarTest
	{
		static ButtonWidget DisabledButton()
		{
#pragma warning disable SYSLIB0050
			var button = (ButtonWidget)FormatterServices.GetUninitializedObject(typeof(ButtonWidget));
#pragma warning restore SYSLIB0050
			button.Bounds = new WidgetBounds(0, 0, 50, 50);
			button.IsDisabled = () => true;
			Ui.MouseFocusWidget = null;
			Viewport.LastMousePos = new int2(10, 10);
			return button;
		}

		static MouseInput Input(MouseInputEvent type, int x = 10) =>
			new MouseInput(type, MouseButton.Left, new int2(x, 10), int2.Zero, Modifiers.None, 1);

		static void CompleteHold(ButtonWidget button)
		{
			typeof(ButtonWidget).GetField("longPressStarted", BindingFlags.Instance | BindingFlags.NonPublic)!
				.SetValue(button, Game.RunTime - 700);
			button.Tick();
		}

		[Test]
		public void DisabledShortcutCanOpenEditorWithoutActivatingOnRelease()
		{
			var button = DisabledButton();
			var edits = 0;
			var commands = 0;
			button.OnLongPress = () => { edits++; button.IsDisabled = () => false; };
			button.OnMouseUp = _ => commands++;
			Assert.That(button.HandleMouseInput(Input(MouseInputEvent.Down)), Is.True);
			CompleteHold(button);
			CompleteHold(button);
			button.HandleMouseInput(Input(MouseInputEvent.Up));
			Assert.That(edits, Is.EqualTo(1));
			Assert.That(commands, Is.Zero);
			Assert.That(Ui.MouseFocusWidget, Is.Null);
		}

		[TestCase(1)]
		[TestCase(2)]
		public void ShortTapPreservesClickAndControlGroupDoubleClick(int taps)
		{
			var button = DisabledButton();
			var edits = 0; var clicks = 0; var doubleClicks = 0;
			button.OnLongPress = () => edits++;
			button.OnMouseUp = _ => clicks++;
			button.OnDoubleClick = () => doubleClicks++;
			button.HandleMouseInput(Input(MouseInputEvent.Down));
			button.IsDisabled = () => false;
			button.HandleMouseInput(new MouseInput(MouseInputEvent.Up, MouseButton.Left, new int2(10, 10), int2.Zero, Modifiers.None, taps));
			Assert.That(edits, Is.Zero);
			Assert.That(clicks, Is.EqualTo(taps == 1 ? 1 : 0));
			Assert.That(doubleClicks, Is.EqualTo(taps == 2 ? 1 : 0));
		}

		[TestCase(false)]
		[TestCase(true)]
		public void MovementOrCancellationPreventsEditing(bool cancel)
		{
			var button = DisabledButton();
			var edits = 0;
			button.OnLongPress = () => edits++;
			button.HandleMouseInput(Input(MouseInputEvent.Down));
			if (cancel) button.YieldMouseFocus(Input(MouseInputEvent.Cancel));
			else
			{
				button.HandleMouseInput(Input(MouseInputEvent.Move, 40));
				button.HandleMouseInput(Input(MouseInputEvent.Move));
			}
			CompleteHold(button);
			button.YieldMouseFocus(Input(MouseInputEvent.Up));
			Assert.That(edits, Is.Zero);
		}

		[Test]
		public void MobileMigratesExistingOrderAndKeepsHomeLast()
		{
			var order = CommandBarLayoutPolicy.NormalizeVisibleOrder(new[] { "CYCLE_BASE", "GROUP_05", "GROUP_01", "STOP" }, true, 7).ToArray();
			Assert.That(order, Does.Contain("GROUP_05"));
			Assert.That(CommandBarLayoutPolicy.NormalizeVisibleOrder(new[] { "GROUP_04", "STOP" }, true, 7), Does.Contain("GROUP_05"));
			Assert.That(order.Last(), Is.EqualTo("CYCLE_BASE"));
			Assert.That(order.First(), Is.EqualTo("CTRL_TOGGLE"));
			Assert.That(CommandBarLayoutPolicy.AvailableIds(new[] { "GROUP_05" }, false), Does.Contain("GROUP_05"));
		}

		[TestCase(490, 46, 2, 13)]
		[TestCase(690, 46, 2, 13)]
		[TestCase(1810, 121, 5, 13)]
		public void RowFitsIncludingCollapseAndAllGaps(int width, int desired, int gap, int count)
		{
			var size = TouchCommandBarSlotPolicy.FitRowButtonSize(width, count, gap, desired);
			Assert.That(size * count + gap * (count - 1), Is.LessThanOrEqualTo(width));
			Assert.That(size, Is.InRange(1, desired));
		}

		[TestCase(599, 0, false)]
		[TestCase(600, 0, true)]
		[TestCase(1000, 13, false)]
		public void LongPressRequiresHoldWithoutMovement(long elapsed, int movement, bool expected)
		{
			Assert.That(ButtonWidget.ShouldActivateLongPress(elapsed, movement), Is.EqualTo(expected));
		}
	}
}
