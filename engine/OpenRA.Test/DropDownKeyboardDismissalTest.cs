using System;
using System.Reflection;
using System.Runtime.Serialization;
using NUnit.Framework;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Widgets;

namespace OpenRA.Test
{
	[TestFixture]
	[NonParallelizable]
	public sealed class DropDownKeyboardDismissalTest
	{
		sealed class RecordingInput : Widget
		{
			public int Presses;
			public override bool HandleKeyPress(KeyInput key)
			{
				Presses++;
				return true;
			}
		}

		[TestCase(true)]
		[TestCase(false)]
		public void EscapeClosesTopmostDropdownAndRestoresExistingFocus(bool cancelWithEscape)
		{
			var previousRoot = Ui.Root;
			var previousFocus = Ui.KeyboardFocusWidget;
			try
			{
				Ui.Root = new ContainerWidget();
				Ui.KeyboardFocusWidget = null;
				var chat = new RecordingInput();
				Ui.Root.AddChild(chat);
				chat.TakeKeyboardFocus();
				var panel = new RecordingInput();
				var mask = new MaskWidget();
				Ui.Root.AddChild(mask);
				Ui.Root.AddChild(panel);
#pragma warning disable SYSLIB0050
				var dropdown = (DropDownButtonWidget)FormatterServices.GetUninitializedObject(typeof(DropDownButtonWidget));
#pragma warning restore SYSLIB0050
				void Set(string name, object value) => typeof(DropDownButtonWidget)
					.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(dropdown, value);
				Set("panelRoot", Ui.Root);
				Set("panel", panel);
				Set("fullscreenMask", mask);
				var cancelled = 0;
				typeof(DropDownButtonWidget).GetMethod("CapturePanelKeyboard", BindingFlags.Instance | BindingFlags.NonPublic)!
					.Invoke(dropdown, new object[] { (Action)(() => cancelled++) });

				Assert.That(Ui.KeyboardFocusWidget, Is.SameAs(mask));
				Assert.That(Ui.HandleKeyPress(new KeyInput { Key = Keycode.DOWN, Event = KeyInputEvent.Down }), Is.True);
				Assert.That(panel.Presses, Is.EqualTo(1), "Dropdown keyboard navigation must still reach its content.");
				Assert.That(chat.Presses, Is.Zero, "The underlying chat must not consume modal keys.");
				Assert.That(Ui.HandleKeyPress(new KeyInput { Key = Keycode.ESCAPE, Event = KeyInputEvent.Up }), Is.True);
				Assert.That(Ui.Root.Children, Does.Contain(panel), "Key release alone must not cancel.");

				if (cancelWithEscape)
					Assert.That(Ui.HandleKeyPress(new KeyInput { Key = Keycode.ESCAPE, Event = KeyInputEvent.Down }), Is.True);
				else
					dropdown.RemovePanel();

				Assert.That(Ui.Root.Children, Does.Not.Contain(panel));
				Assert.That(Ui.Root.Children, Does.Not.Contain(mask));
				Assert.That(Ui.KeyboardFocusWidget, Is.SameAs(chat));
				Assert.That(chat.Presses, Is.Zero);
				Assert.That(cancelled, Is.EqualTo(cancelWithEscape ? 1 : 0));
				dropdown.RemovePanel();
				Assert.That(cancelled, Is.EqualTo(cancelWithEscape ? 1 : 0), "Removal must be idempotent.");
			}
			finally
			{
				Ui.Root = previousRoot;
				Ui.KeyboardFocusWidget = previousFocus;
			}
		}
	}
}
