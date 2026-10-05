using System;
using System.IO;
using NUnit.Framework;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class IosProductionGestureTest
	{
		sealed class Target : IWidgetTouchGestureTarget
		{
			public int Begins, Holds, Pinches, Direction;
			public bool Enabled = true;
			public bool AcceptsTouch(int2 position) => Enabled && position.X >= 100;
			public void BeginTouch(int2 position) => Begins++;
			public void HandleTouchLongPress(int2 origin, int2 position) => Holds++;
			public void HandleTouchPinch(int direction) { Pinches++; Direction = direction; }
		}

		static TouchPointerAction Action(TouchPointerActionType type, int direction = 0) =>
			new(type, new int2(150, 150), new int2(0, direction));

		[Test]
		public void RealHoldCancelsOnceWithoutMouseActions()
		{
			var target = new Target();
			var capture = new WidgetTouchGestureCapture();
			var adapter = new TouchGestureAdapter();
			capture.Begin(target, new int2(150, 150));
			adapter.Begin(new int2(150, 150), 100);
			adapter.Poll(550);
			var result = adapter.End(new int2(150, 150));
			Assert.That(capture.Handle(result.First), Is.True);
			Assert.That(capture.Handle(result.Second), Is.True);
			Assert.That(target.Holds, Is.EqualTo(1));
		}

		[Test]
		public void CaptureNotifiesOriginalTargetOnBeginOnly()
		{
			var target = new Target();
			var capture = new WidgetTouchGestureCapture();
			capture.Begin(target, new int2(150, 150));
			capture.BeginSecond(target);
			Assert.That(target.Begins, Is.EqualTo(1));
			capture.Begin(target, int2.Zero);
			Assert.That(target.Begins, Is.EqualTo(1));
		}

		[Test]
		public void TapAndDragKeepNormalMousePath()
		{
			var capture = new WidgetTouchGestureCapture();
			var target = new Target();
			capture.Begin(target, new int2(150, 150));
			foreach (var type in new[] { TouchPointerActionType.LeftDown, TouchPointerActionType.LeftMove, TouchPointerActionType.LeftUp })
				Assert.That(capture.Handle(Action(type)), Is.False);
			Assert.That(target.Holds, Is.Zero);
		}

		[TestCase(1)]
		[TestCase(-1)]
		public void RealTwoFingerAdapterSwitchesOnceAndDoesNotLeakClicks(int direction)
		{
			var target = new Target();
			var capture = new WidgetTouchGestureCapture();
			var adapter = new TouchGestureAdapter();
			var first = new int2(150, 150);
			capture.Begin(target, first);
			adapter.Begin(first, 1000);
			capture.BeginSecond(target);
			void Consume(TouchGestureResult result)
			{
				if (result.Count > 0)
					Assert.That(capture.Handle(result.First), Is.True);
				if (result.Count > 1)
					Assert.That(capture.Handle(result.Second), Is.True);
			}

			Consume(adapter.BeginSecond(new int2(250, 150), 1000));
			for (var step = 1; step <= 5; step++)
			{
				Consume(adapter.MovePrimary(new int2(150 - direction * step * 6, 150), (ulong)(1000 + step * 20)));
				Consume(adapter.MoveSecond(new int2(250 + direction * step * 6, 150), (ulong)(1010 + step * 20)));
			}

			Consume(adapter.EndTwoFinger());
			Assert.That(target.Pinches, Is.EqualTo(1));
			Assert.That(target.Direction, Is.EqualTo(direction));
			Assert.That(target.Holds, Is.Zero);
		}

		[TestCase(1)]
		[TestCase(-1)]
		public void PinchRequiresTwoStepsAndOnlyChangesOnce(int direction)
		{
			var target = new Target();
			var capture = new WidgetTouchGestureCapture();
			capture.Begin(target, new int2(150, 150));
			capture.BeginSecond(target);
			Assert.That(capture.Handle(Action(TouchPointerActionType.Scroll, direction)), Is.True);
			Assert.That(target.Pinches, Is.Zero);
			capture.Handle(Action(TouchPointerActionType.Scroll, direction));
			capture.Handle(Action(TouchPointerActionType.Scroll, -direction));
			Assert.That(target.Pinches, Is.EqualTo(1));
			Assert.That(target.Direction, Is.EqualTo(direction));
		}

		[TestCase(true)]
		[TestCase(false)]
		public void MixedWorldAndPaletteGestureIsConsumedWithoutProduction(bool paletteFirst)
		{
			var target = new Target();
			var capture = new WidgetTouchGestureCapture();
			capture.Begin(paletteFirst ? target : null, new int2(150, 150));
			capture.BeginSecond(paletteFirst ? null : target);
			foreach (var type in new[] { TouchPointerActionType.Scroll, TouchPointerActionType.RightDown,
				TouchPointerActionType.MiddleDown, TouchPointerActionType.ForceAttackDown })
				Assert.That(capture.Handle(Action(type, 1)), Is.True);
			Assert.That(target.Holds + target.Pinches, Is.Zero);
		}

		[Test]
		public void SecondFingerKeepsCancelForExistingDragButSuppressesTwoFingerTap()
		{
			var target = new Target();
			var capture = new WidgetTouchGestureCapture();
			capture.Begin(target, new int2(150, 150));
			capture.BeginSecond(target);
			Assert.That(capture.Handle(Action(TouchPointerActionType.LeftCancel)), Is.False);
			Assert.That(capture.Handle(Action(TouchPointerActionType.RightDown)), Is.True);
			Assert.That(capture.Handle(Action(TouchPointerActionType.RightUp)), Is.True);
			Assert.That(target.Holds, Is.Zero);
		}

		[Test]
		public void ModalInterruptionAndThirdFingerPreventSideEffects()
		{
			var target = new Target();
			var capture = new WidgetTouchGestureCapture();
			capture.Begin(target, new int2(150, 150));
			capture.Block();
			Assert.That(capture.Handle(Action(TouchPointerActionType.ForceAttackDown)), Is.True);
			capture.Begin(target, new int2(150, 150));
			capture.BeginSecond(target);
			capture.BeginThird();
			capture.Handle(Action(TouchPointerActionType.Scroll, 1));
			capture.Handle(Action(TouchPointerActionType.Scroll, 1));
			Assert.That(target.Holds + target.Pinches, Is.Zero);
			capture.Reset();
			Assert.That(capture.Handle(Action(TouchPointerActionType.ForceAttackDown)), Is.False);
		}

		[Test]
		public void WorldHoldAndPinchRemainUnchanged()
		{
			var capture = new WidgetTouchGestureCapture();
			capture.Begin(null, new int2(10, 10));
			Assert.That(capture.Handle(Action(TouchPointerActionType.ForceAttackDown)), Is.False);
			capture.BeginSecond(null);
			Assert.That(capture.Handle(Action(TouchPointerActionType.Scroll, 1)), Is.False);
		}

		[Test]
		public void ReleaseBetweenFramePollsStillCancelsAtDeadline()
		{
			var adapter = new TouchGestureAdapter();
			var capture = new WidgetTouchGestureCapture();
			var target = new Target();
			var pos = new int2(150, 150);
			capture.Begin(target, pos);
			adapter.Begin(pos, 100);
			adapter.Poll(540);
			adapter.Poll(555); // SDL release-time poll, before End.
			var result = adapter.End(pos);
			capture.Handle(result.First);
			capture.Handle(result.Second);
			Assert.That(target.Holds, Is.EqualTo(1));
		}

		[TestCase(true)]
		[TestCase(false)]
		public void IndependentTouchSuppressesPaletteInEitherOrder(bool paletteFirst)
		{
			var capture = new WidgetTouchGestureCapture();
			var target = new Target();
			capture.Begin(target, new int2(150, 150), !paletteFirst);
			if (paletteFirst)
				Assert.That(capture.IndependentTouchBegan(), Is.True);
			foreach (var type in new[] { TouchPointerActionType.LeftDown, TouchPointerActionType.LeftUp,
				TouchPointerActionType.ForceAttackDown, TouchPointerActionType.Scroll })
				Assert.That(capture.Handle(Action(type, 1)), Is.True);
			Assert.That(capture.Handle(Action(TouchPointerActionType.LeftCancel)), Is.False);
			Assert.That(target.Holds + target.Pinches, Is.Zero);
			capture.Begin(null, int2.Zero, true);
			Assert.That(capture.IndependentTouchBegan(), Is.False);
			Assert.That(capture.Handle(Action(TouchPointerActionType.LeftDown)), Is.False);
		}

		[Test]
		public void ReleaseUpdatesHoldDeadlineAndIndependentTouchesBlockProduction()
		{
			var root = TestContext.CurrentContext.TestDirectory;
			while (!File.Exists(Path.Combine(root, "engine", "OpenRA.Platforms.Default", "Sdl2Input.cs")))
				root = Directory.GetParent(root)?.FullName ?? throw new InvalidOperationException("Repository not found");
			var source = File.ReadAllText(Path.Combine(root, "engine", "OpenRA.Platforms.Default", "Sdl2Input.cs"));
			StringAssert.Contains("DispatchTouch(touchGestures.Poll(SDL.SDL_GetTicks64()), inputHandler, mods);\n\t\t\t\t\t\t\tDispatchTouch(touchGestures.End(pos)", source);
			StringAssert.Contains("if (widgetTouchGestures.IndependentTouchBegan())", source);
			StringAssert.Contains("pos, independentTouchCapture.Active)", source);
		}

		[Test]
		public void ProductionGestureIsRoutedBeforeForceAttackMouseTranslation()
		{
			var root = TestContext.CurrentContext.TestDirectory;
			while (!File.Exists(Path.Combine(root, "engine", "OpenRA.Platforms.Default", "Sdl2Input.cs")))
				root = Directory.GetParent(root)?.FullName ?? throw new InvalidOperationException("Repository not found");
			var source = File.ReadAllText(Path.Combine(root, "engine", "OpenRA.Platforms.Default", "Sdl2Input.cs"));
			var dispatch = source.Substring(source.IndexOf("void DispatchTouch(", StringComparison.Ordinal));
			StringAssert.Contains("widgetTouchGestures.Handle(action)", dispatch);
			Assert.That(dispatch.IndexOf("widgetTouchGestures.Handle(action)", StringComparison.Ordinal),
				Is.LessThan(dispatch.IndexOf("touchPointerButtons.Resolve", StringComparison.Ordinal)));
		}
	}
}
