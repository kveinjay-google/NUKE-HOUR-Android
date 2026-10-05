#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software. It is
 * made available under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version.
 */
#endregion

using System.IO;
using NUnit.Framework;
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class TouchGestureAdapterTest
	{
		sealed class RecordingIndependentTouchInput : Widget, IIndependentTouchInput
		{
			public int BeginCount { get; private set; }
			public int MoveCount { get; private set; }
			public int EndCount { get; private set; }
			public int CancelCount { get; private set; }
			public int2 LastPosition { get; private set; }

			public bool BeginIndependentTouch(int2 position)
			{
				BeginCount++;
				LastPosition = position;
				return true;
			}

			public void MoveIndependentTouch(int2 position)
			{
				MoveCount++;
				LastPosition = position;
			}

			public void EndIndependentTouch(int2 position)
			{
				EndCount++;
				LastPosition = position;
			}

			public void CancelIndependentTouch()
			{
				CancelCount++;
			}
		}

		[TearDown]
		public void ClearTouchPressFeedback()
		{
			TouchPressFeedback.End();
		}

		[TestCase(0f, 0f, 0, 0)]
		[TestCase(0.5f, 0.5f, 512, 384)]
		[TestCase(1f, 1f, 1023, 767)]
		[TestCase(-0.1f, 1.1f, 0, 767)]
		public void MapsNormalizedPositionToLogicalWindow(float x, float y, int expectedX, int expectedY)
		{
			Assert.That(TouchGestureAdapter.MapPosition(x, y, new Size(1024, 768)),
				Is.EqualTo(new int2(expectedX, expectedY)));
		}

		[Test]
		public void TouchPressFeedbackTracksThePrimaryContactUntilReleaseOrCancellation()
		{
			var button = new ContainerWidget { Bounds = new WidgetBounds(80, 80, 80, 80) };
			var icon = new ContainerWidget { Bounds = new WidgetBounds(0, 0, 80, 80) };
			button.AddChild(icon);

			TouchPressFeedback.Begin(new int2(100, 110), icon);
			Assert.That(TouchPressFeedback.Active, Is.True);
			Assert.That(TouchPressFeedback.Position, Is.EqualTo(new int2(100, 110)));
			Assert.That(TouchPressFeedback.IsActiveFor(button), Is.True,
				"The deepest hit child must still light its owning button.");

			TouchPressFeedback.Move(new int2(170, 110));
			Assert.That(TouchPressFeedback.IsActiveFor(button), Is.False,
				"Dragging outside the button must extinguish its pressed state without transferring ownership.");
			TouchPressFeedback.Move(new int2(120, 110));
			Assert.That(TouchPressFeedback.IsActiveFor(button), Is.True);
			TouchPressFeedback.End();
			Assert.That(TouchPressFeedback.Active, Is.False);
			Assert.That(TouchPressFeedback.IsActiveFor(button), Is.False);
		}

		[Test]
		public void IndependentTouchCaptureOwnsOnlyItsFingerUntilRelease()
		{
			var target = new RecordingIndependentTouchInput();
			var capture = new IndependentTouchInputCapture();

			Assert.That(capture.TryBegin(11, new int2(40, 50), target), Is.True);
			Assert.That(capture.Active, Is.True);
			Assert.That(capture.Owns(11), Is.True);
			Assert.That(capture.Owns(22), Is.False,
				"A second finger must remain available to the normal battlefield input path.");
			Assert.That(capture.TryBegin(22, new int2(200, 220), target), Is.False);
			Assert.That(capture.Move(22, new int2(210, 230)), Is.False);
			Assert.That(capture.Move(11, new int2(60, 70)), Is.True);
			Assert.That(capture.End(22, new int2(210, 230)), Is.False);
			Assert.That(capture.End(11, new int2(60, 70)), Is.True);
			Assert.That(capture.Active, Is.False);
			Assert.That(target.BeginCount, Is.EqualTo(1));
			Assert.That(target.MoveCount, Is.EqualTo(1));
			Assert.That(target.EndCount, Is.EqualTo(1));
		}

		[Test]
		public void CancellingIndependentTouchCaptureReleasesJoystickWithoutACommandClick()
		{
			var target = new RecordingIndependentTouchInput();
			var capture = new IndependentTouchInputCapture();
			capture.TryBegin(11, new int2(40, 50), target);

			capture.Cancel();

			Assert.That(capture.Active, Is.False);
			Assert.That(target.CancelCount, Is.EqualTo(1));
			Assert.That(target.EndCount, Is.Zero);
			Assert.That(capture.End(11, new int2(40, 50)), Is.False);
		}

		[Test]
		public void IndependentTouchHitTestingFindsTheTopmostEligibleWidget()
		{
			var root = new ContainerWidget { Bounds = new WidgetBounds(0, 0, 400, 300) };
			var lower = new RecordingIndependentTouchInput { Bounds = new WidgetBounds(20, 20, 120, 120) };
			var upper = new RecordingIndependentTouchInput { Bounds = new WidgetBounds(40, 40, 120, 120) };
			root.AddChild(lower);
			root.AddChild(upper);

			Assert.That(root.IndependentTouchInputAt(new int2(60, 60)), Is.SameAs(upper));
			Assert.That(root.IndependentTouchInputAt(new int2(25, 25)), Is.SameAs(lower));
			Assert.That(root.IndependentTouchInputAt(new int2(300, 250)), Is.Null);
		}

		[Test]
		public void SdlClaimsIndependentJoystickTouchBeforeConsideringMultiTouchGestures()
		{
			var source = File.ReadAllText(Path.Combine(RepositoryRoot(),
				"engine", "OpenRA.Platforms.Default", "Sdl2Input.cs"));
			var fingerDown = SourceBetween(source,
				"case SDL.SDL_EventType.SDL_FINGERDOWN:",
				"case SDL.SDL_EventType.SDL_FINGERMOTION:");
			var capture = fingerDown.IndexOf("independentTouchCapture.TryBegin(", System.StringComparison.Ordinal);
			var ordinaryTouch = fingerDown.IndexOf("if (!touchDown)", System.StringComparison.Ordinal);

			Assert.That(capture, Is.GreaterThanOrEqualTo(0));
			Assert.That(ordinaryTouch, Is.GreaterThan(capture),
				"The joystick must claim its own finger before another active finger can promote the pair to a gesture.");
			StringAssert.Contains("continue;", fingerDown.Substring(capture, ordinaryTouch - capture));
		}

		[Test]
		public void SdlDirectTouchUsesFingerTolerantMultiTapDetection()
		{
			var source = File.ReadAllText(Path.Combine(RepositoryRoot(),
				"engine", "OpenRA.Platforms.Default", "Sdl2Input.cs"));
			var dispatchTouch = SourceBetween(source, "void DispatchTouch(", "void ResetTouchInput(");

			StringAssert.Contains("MultiTapDetection.DetectFromTouch(", dispatchTouch);
			StringAssert.Contains("MultiTapDetection.InfoFromTouch(", dispatchTouch);
			StringAssert.Contains("MultiTapDetection.CancelFromTouch(", dispatchTouch);
			StringAssert.DoesNotContain("MultiTapDetection.DetectFromMouse(", dispatchTouch);
			StringAssert.DoesNotContain("MultiTapDetection.InfoFromMouse(", dispatchTouch);
			StringAssert.DoesNotContain("MultiTapDetection.CancelFromMouse(", dispatchTouch);
		}

		[Test]
		public void StationaryTouchBecomesClickOnRelease()
		{
			var adapter = new TouchGestureAdapter();
			Assert.That(adapter.Begin(new int2(100, 120)).Count, Is.Zero);

			var result = adapter.End(new int2(100, 120));
			Assert.That(result.Count, Is.EqualTo(2));
			Assert.That(result.First.Type, Is.EqualTo(TouchPointerActionType.LeftDown));
			Assert.That(result.First.Position, Is.EqualTo(new int2(100, 120)));
			Assert.That(result.Second.Type, Is.EqualTo(TouchPointerActionType.LeftUp));
			Assert.That(result.Second.Position, Is.EqualTo(new int2(100, 120)));
		}

		[Test]
		public void MovementInsideThresholdStillBecomesClick()
		{
			var adapter = new TouchGestureAdapter();
			adapter.Begin(new int2(100, 100));
			Assert.That(adapter.Move(new int2(106, 105)).Count, Is.Zero);

			var result = adapter.End(new int2(106, 105));
			Assert.That(result.Count, Is.EqualTo(2));
			Assert.That(result.First.Type, Is.EqualTo(TouchPointerActionType.LeftDown));
			Assert.That(result.Second.Type, Is.EqualTo(TouchPointerActionType.LeftUp));
		}

		[Test]
		public void MovementPastThresholdStartsDragAtTouchOrigin()
		{
			var adapter = new TouchGestureAdapter();
			adapter.Begin(new int2(100, 100));

			var start = adapter.Move(new int2(109, 100));
			Assert.That(start.Count, Is.EqualTo(2));
			Assert.That(start.First.Type, Is.EqualTo(TouchPointerActionType.LeftDown));
			Assert.That(start.First.Position, Is.EqualTo(new int2(100, 100)));
			Assert.That(start.Second.Type, Is.EqualTo(TouchPointerActionType.LeftMove));
			Assert.That(start.Second.Position, Is.EqualTo(new int2(109, 100)));
			Assert.That(start.Second.Delta, Is.EqualTo(new int2(9, 0)));

			var end = adapter.End(new int2(109, 100));
			Assert.That(end.Count, Is.EqualTo(1));
			Assert.That(end.First.Type, Is.EqualTo(TouchPointerActionType.LeftUp));
		}

		[Test]
		public void ActiveDragReportsIncrementalMovement()
		{
			var adapter = new TouchGestureAdapter();
			adapter.Begin(new int2(100, 100));
			adapter.Move(new int2(109, 100));

			var move = adapter.Move(new int2(114, 103));
			Assert.That(move.Count, Is.EqualTo(1));
			Assert.That(move.First.Type, Is.EqualTo(TouchPointerActionType.LeftMove));
			Assert.That(move.First.Delta, Is.EqualTo(new int2(5, 3)));
		}

		[Test]
		public void SingleFingerMotionUsesOnlyOneWidgetTreePathAfterDragStarts()
		{
			var policy = typeof(TouchGestureAdapter).Assembly.GetType("OpenRA.TouchMotionDispatchPolicy");
			Assert.That(policy, Is.Not.Null,
				"The SDL input path needs one shared policy for deciding whether a fallback hover move is required.");
			var method = policy!.GetMethod("NeedsFallbackHover",
				System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
			Assert.That(method, Is.Not.Null);

			Assert.That(method!.Invoke(null, new object[] { TouchGestureResult.Empty }), Is.True,
				"Pre-drag motion still needs one hover update for hit testing and pressed feedback.");
			var drag = TouchGestureResult.One(new TouchPointerAction(
				TouchPointerActionType.LeftMove, new int2(114, 103), new int2(5, 3)));
			Assert.That(method.Invoke(null, new object[] { drag }), Is.False,
				"An authoritative drag move must not be preceded by a redundant full-tree hover move.");
		}

		[Test]
		public void SdlSingleFingerMotionDoesNotDispatchAnUnconditionalHoverMove()
		{
			var source = File.ReadAllText(Path.Combine(RepositoryRoot(),
				"engine", "OpenRA.Platforms.Default", "Sdl2Input.cs"));
			var fingerMotion = SourceBetween(source,
				"case SDL.SDL_EventType.SDL_FINGERMOTION:",
				"case SDL.SDL_EventType.SDL_FINGERUP:");
			var singleFingerMotion = fingerMotion.Substring(fingerMotion.IndexOf(
				"else if (touchDown && e.tfinger.fingerId == touchFingerId)",
				System.StringComparison.Ordinal));

			StringAssert.Contains("pendingPrimaryTouchPosition = pos;", singleFingerMotion);
			StringAssert.DoesNotContain("touchGestures.Move(pos)", singleFingerMotion);
			StringAssert.DoesNotContain("MouseInputEvent.Move", singleFingerMotion);
		}

		[Test]
		public void SdlCoalescesSingleFingerMotionWithinOneInputPump()
		{
			var source = File.ReadAllText(Path.Combine(RepositoryRoot(),
				"engine", "OpenRA.Platforms.Default", "Sdl2Input.cs"));
			var pump = SourceBetween(source, "public void PumpInput(", "if (pendingMotion != null)");

			StringAssert.Contains("int2? pendingPrimaryTouchPosition = null;", pump);
			StringAssert.Contains("void FlushPrimaryTouchMotion()", pump);
			StringAssert.Contains("pendingPrimaryTouchPosition = pos;", pump);
			StringAssert.Contains("FlushPrimaryTouchMotion();", pump);
			StringAssert.Contains("isSyntheticTouchMouseEvent", pump);
			StringAssert.Contains("!isSyntheticTouchMouseEvent", pump);
			Assert.That(CountOccurrences(pump, "touchGestures.Move(pendingPrimaryTouchPosition.Value)"),
				Is.EqualTo(1), "The coalesced touch sample must be recognized exactly once per input pump.");
		}

		[Test]
		public void StationaryLongPressBecomesForceAttackOnRelease()
		{
			var adapter = new TouchGestureAdapter();
			adapter.Begin(new int2(200, 220), 1000);

			Assert.That(adapter.Poll(1449).Count, Is.Zero);
			Assert.That(adapter.Poll(1450).Count, Is.Zero);

			var release = adapter.End(new int2(200, 220));
			Assert.That(release.Count, Is.EqualTo(2));
			Assert.That(release.First.Type, Is.EqualTo(TouchPointerActionType.ForceAttackDown));
			Assert.That(release.First.Position, Is.EqualTo(new int2(200, 220)));
			Assert.That(release.Second.Type, Is.EqualTo(TouchPointerActionType.ForceAttackUp));
			Assert.That(release.Second.Position, Is.EqualTo(new int2(200, 220)));
		}

		[TestCase(true, MouseButton.Left)]
		[TestCase(false, MouseButton.Right)]
		public void ForceAttackTouchUsesTheConfiguredActionButton(bool classic, MouseButton expected)
		{
			Assert.That(TouchPointerButtonPolicy.Resolve(
				TouchPointerActionType.ForceAttackDown, classic, false), Is.EqualTo(expected));
			Assert.That(TouchPointerButtonPolicy.Resolve(
				TouchPointerActionType.ForceAttackUp, classic, false), Is.EqualTo(expected));
		}

		[Test]
		public void SdlAddsCtrlOnlyToTheDedicatedLongPressForceAttackGesture()
		{
			var source = File.ReadAllText(Path.Combine(RepositoryRoot(),
				"engine", "OpenRA.Platforms.Default", "Sdl2Input.cs"));
			var dispatchTouch = SourceBetween(source, "void DispatchTouch(", "void ResetTouchInput(");

			StringAssert.Contains("TouchPointerActionType.ForceAttackDown", dispatchTouch);
			StringAssert.Contains("TouchPointerActionType.ForceAttackUp", dispatchTouch);
			StringAssert.Contains("isForceAttack ? mods | Modifiers.Ctrl", dispatchTouch);
		}

		[Test]
		public void DragCancelsLongPress()
		{
			var adapter = new TouchGestureAdapter();
			adapter.Begin(new int2(100, 100), 1000);
			adapter.Move(new int2(109, 100));

			Assert.That(adapter.Poll(2000).Count, Is.Zero);
		}

		[Test]
		public void MovementAfterLongPressDelayStartsLeftDragWithoutRightClick()
		{
			var adapter = new TouchGestureAdapter();
			adapter.Begin(new int2(100, 100), 1000);
			Assert.That(adapter.Poll(1450).Count, Is.Zero);

			var drag = adapter.Move(new int2(109, 100));
			Assert.That(drag.Count, Is.EqualTo(2));
			Assert.That(drag.First.Type, Is.EqualTo(TouchPointerActionType.LeftDown));
			Assert.That(drag.First.Position, Is.EqualTo(new int2(100, 100)));
			Assert.That(drag.Second.Type, Is.EqualTo(TouchPointerActionType.LeftMove));
			Assert.That(drag.Second.Position, Is.EqualTo(new int2(109, 100)));

			var end = adapter.End(new int2(109, 100));
			Assert.That(end.First.Type, Is.EqualTo(TouchPointerActionType.LeftUp));
		}

		[Test]
		public void CancellationSuppressesPendingTapAndLongPress()
		{
			var adapter = new TouchGestureAdapter();
			adapter.Begin(new int2(100, 100), 1000);

			Assert.That(adapter.Cancel().Count, Is.Zero);
			Assert.That(adapter.Poll(2000).Count, Is.Zero);
			Assert.That(adapter.End(new int2(100, 100)).Count, Is.Zero);
		}

		[Test]
		public void CancellationReleasesActiveDrag()
		{
			var adapter = new TouchGestureAdapter();
			adapter.Begin(new int2(100, 100), 1000);
			adapter.Move(new int2(109, 100));

			var cancel = adapter.Cancel();
			Assert.That(cancel.Count, Is.EqualTo(1));
			Assert.That(cancel.First.Type, Is.EqualTo(TouchPointerActionType.LeftCancel));
			Assert.That(cancel.First.Position, Is.EqualTo(new int2(109, 100)));
		}

		[Test]
		public void SecondFingerCancelsAnActiveSingleFingerDragWithoutClicking()
		{
			var adapter = new TouchGestureAdapter();
			adapter.Begin(new int2(100, 100), 1000);
			adapter.Move(new int2(109, 100));

			var handoff = adapter.BeginSecond(new int2(200, 100), 1010);
			Assert.That(handoff.Count, Is.EqualTo(1));
			Assert.That(handoff.First.Type, Is.EqualTo(TouchPointerActionType.LeftCancel));
		}

		[Test]
		public void ShortTwoFingerTapEmitsRightClickAtCentroid()
		{
			var adapter = new TouchGestureAdapter();
			adapter.Begin(new int2(100, 100), 1000);

			var start = adapter.BeginSecond(new int2(200, 100));
			Assert.That(start.Count, Is.Zero);

			var end = adapter.EndTwoFinger();
			Assert.That(end.Count, Is.EqualTo(2));
			Assert.That(end.First.Type, Is.EqualTo(TouchPointerActionType.RightDown));
			Assert.That(end.First.Position, Is.EqualTo(new int2(150, 100)));
			Assert.That(end.Second.Type, Is.EqualTo(TouchPointerActionType.RightUp));
			Assert.That(end.Second.Position, Is.EqualTo(new int2(150, 100)));
			Assert.That(adapter.End(new int2(110, 100)).Count, Is.Zero);
		}

		[Test]
		public void DelayedSecondFingerStartsItsOwnTapTimeout()
		{
			var adapter = new TouchGestureAdapter();
			adapter.Begin(new int2(100, 100), 1000);
			adapter.BeginSecond(new int2(200, 100), 1400);

			Assert.That(adapter.Poll(1450).Count, Is.Zero);
			var end = adapter.EndTwoFinger();
			Assert.That(end.Count, Is.EqualTo(2));
			Assert.That(end.First.Type, Is.EqualTo(TouchPointerActionType.RightDown));
			Assert.That(end.Second.Type, Is.EqualTo(TouchPointerActionType.RightUp));
		}

		[Test]
		public void SdlTwoFingerMotionUsesOneTimestampForTimestampAwareMoves()
		{
			var source = File.ReadAllText(Path.Combine(RepositoryRoot(),
				"engine", "OpenRA.Platforms.Default", "Sdl2Input.cs"));
			var fingerMotion = SourceBetween(source,
				"case SDL.SDL_EventType.SDL_FINGERMOTION:",
				"case SDL.SDL_EventType.SDL_FINGERUP:");
			var twoFingerMotion = SourceBetween(fingerMotion,
				"else if (touchDown && twoFingerTouch)",
				"else if (touchDown && e.tfinger.fingerId == touchFingerId)");

			Assert.That(CountOccurrences(twoFingerMotion, "SDL.SDL_GetTicks64()"), Is.EqualTo(1));
			StringAssert.Contains("var timestamp = SDL.SDL_GetTicks64();", twoFingerMotion);
			Assert.That(CountOccurrences(twoFingerMotion, "touchGestures.MovePrimary("), Is.EqualTo(1));
			Assert.That(CountOccurrences(twoFingerMotion, "touchGestures.MoveSecond("), Is.EqualTo(1));
			StringAssert.Contains("touchGestures.MovePrimary(pos, timestamp)", twoFingerMotion);
			StringAssert.Contains("touchGestures.MoveSecond(pos, timestamp)", twoFingerMotion);
			StringAssert.DoesNotContain("touchGestures.MovePrimary(pos)", twoFingerMotion);
			StringAssert.DoesNotContain("touchGestures.MoveSecond(pos)", twoFingerMotion);
		}

		[Test]
		public void SequentialSameDirectionMovementImmediatelyPansFromCentroid()
		{
			var adapter = new TouchGestureAdapter();
			adapter.Begin(new int2(100, 100), 1000);
			adapter.BeginSecond(new int2(200, 100), 1000);

			var firstMove = MovePrimaryAt(adapter, new int2(120, 100), 1010);
			Assert.That(firstMove.Count, Is.Zero);
			var start = MoveSecondAt(adapter, new int2(220, 100), 1020);
			Assert.That(start.Count, Is.EqualTo(2));
			Assert.That(start.First.Type, Is.EqualTo(TouchPointerActionType.MiddleDown));
			Assert.That(start.First.Position, Is.EqualTo(new int2(150, 100)));
			Assert.That(start.Second.Type, Is.EqualTo(TouchPointerActionType.MiddleMove));
			Assert.That(start.Second.Position, Is.EqualTo(new int2(170, 100)));
			Assert.That(start.Second.Delta, Is.EqualTo(new int2(20, 0)));

			var move = MovePrimaryAt(adapter, new int2(126, 104), 1030);
			Assert.That(move.Count, Is.EqualTo(1));
			Assert.That(move.First.Type, Is.EqualTo(TouchPointerActionType.MiddleMove));
			Assert.That(move.First.Delta, Is.EqualTo(new int2(3, 2)));

			var end = adapter.EndTwoFinger();
			Assert.That(end.Count, Is.EqualTo(1));
			Assert.That(end.First.Type, Is.EqualTo(TouchPointerActionType.MiddleUp));
			Assert.That(adapter.EndTwoFinger().Count, Is.Zero);
			Assert.That(adapter.Cancel().Count, Is.Zero);
		}

		[TestCase(true, false, MouseButton.Right)]
		[TestCase(true, true, MouseButton.Middle)]
		[TestCase(false, false, MouseButton.Middle)]
		[TestCase(false, true, MouseButton.Right)]
		public void TouchViewportPanUsesTheConfiguredViewportScrollButton(
			bool useClassicMouseStyle, bool useAlternateScrollButton, MouseButton expected)
		{
			Assert.That(TouchPointerButtonPolicy.Resolve(
				TouchPointerActionType.MiddleDown, useClassicMouseStyle, useAlternateScrollButton),
				Is.EqualTo(expected));
			Assert.That(TouchPointerButtonPolicy.Resolve(
				TouchPointerActionType.MiddleMove, useClassicMouseStyle, useAlternateScrollButton),
				Is.EqualTo(expected));
			Assert.That(TouchPointerButtonPolicy.Resolve(
				TouchPointerActionType.MiddleUp, useClassicMouseStyle, useAlternateScrollButton),
				Is.EqualTo(expected));
		}

		[Test]
		public void TouchTapButtonsRemainIndependentFromViewportScrollPreference()
		{
			Assert.That(TouchPointerButtonPolicy.Resolve(
				TouchPointerActionType.LeftDown, true, false), Is.EqualTo(MouseButton.Left));
			Assert.That(TouchPointerButtonPolicy.Resolve(
				TouchPointerActionType.RightDown, true, true), Is.EqualTo(MouseButton.Right));
			Assert.That(TouchPointerButtonPolicy.Resolve(
				TouchPointerActionType.Scroll, true, false), Is.EqualTo(MouseButton.None));
		}

		[Test]
		public void ActiveTouchViewportPanKeepsOneButtonUntilRelease()
		{
			var buttons = default(TouchPointerButtonState);
			Assert.That(buttons.Resolve(TouchPointerActionType.MiddleDown, true, false),
				Is.EqualTo(MouseButton.Right));

			// Changing the mouse preference must not split one gesture into
			// RightDown followed by MiddleMove/MiddleUp.
			Assert.That(buttons.Resolve(TouchPointerActionType.MiddleMove, true, true),
				Is.EqualTo(MouseButton.Right));
			Assert.That(buttons.Resolve(TouchPointerActionType.MiddleUp, true, true),
				Is.EqualTo(MouseButton.Right));

			Assert.That(buttons.Resolve(TouchPointerActionType.MiddleDown, true, true),
				Is.EqualTo(MouseButton.Middle));
		}

		[Test]
		public void ResetClearsTheLatchedTouchViewportPanButton()
		{
			var buttons = default(TouchPointerButtonState);
			Assert.That(buttons.Resolve(TouchPointerActionType.MiddleDown, true, false),
				Is.EqualTo(MouseButton.Right));

			buttons.Reset();
			Assert.That(buttons.Resolve(TouchPointerActionType.MiddleDown, true, true),
				Is.EqualTo(MouseButton.Middle));
		}

		[Test]
		public void ActiveViewportScrollKeepsItsButtonAcrossPreferenceChanges()
		{
			var scrollButton = default(ViewportScrollButtonState);
			Assert.That(scrollButton.CurrentOr(MouseButton.Right), Is.EqualTo(MouseButton.Right));

			scrollButton.Begin(MouseButton.Right);
			Assert.That(scrollButton.CurrentOr(MouseButton.Middle), Is.EqualTo(MouseButton.Right));

			scrollButton.End();
			Assert.That(scrollButton.CurrentOr(MouseButton.Middle), Is.EqualTo(MouseButton.Middle));
		}

		[Test]
		public void MovementPastDeadzoneInvalidatesTwoFingerTapWhilePending()
		{
			var adapter = new TouchGestureAdapter();
			adapter.Begin(new int2(100, 100), 1000);
			adapter.BeginSecond(new int2(200, 100), 1000);

			Assert.That(MovePrimaryAt(adapter, new int2(109, 100), 1010).Count, Is.Zero);
			Assert.That(adapter.EndTwoFinger().Count, Is.Zero);
		}

		[Test]
		public void TimestampAwareTwoFingerMovesPreserveDeadzoneJitter()
		{
			var adapter = new TouchGestureAdapter();
			adapter.Begin(new int2(100, 100), 1000);
			adapter.BeginSecond(new int2(200, 100), 1000);

			Assert.That(adapter.MovePrimary(new int2(105, 104), 1010).Count, Is.Zero);
			Assert.That(adapter.MoveSecond(new int2(204, 105), 1020).Count, Is.Zero);
			var end = adapter.EndTwoFinger();
			Assert.That(end.Count, Is.EqualTo(2));
			Assert.That(end.First.Type, Is.EqualTo(TouchPointerActionType.RightDown));
			Assert.That(end.Second.Type, Is.EqualTo(TouchPointerActionType.RightUp));
		}

		[Test]
		public void AnchoredPinchWaitsForTheArbitrationWindow()
		{
			var adapter = new TouchGestureAdapter();
			adapter.Begin(new int2(100, 100), 1000);
			adapter.BeginSecond(new int2(200, 100), 1000);

			Assert.That(MoveSecondAt(adapter, new int2(220, 100), 1010).Count, Is.Zero);
			Assert.That(adapter.Poll(1041).Count, Is.Zero);

			var pinch = adapter.Poll(1042);
			Assert.That(pinch.Count, Is.EqualTo(1));
			Assert.That(pinch.First.Type, Is.EqualTo(TouchPointerActionType.Scroll));
			Assert.That(pinch.First.Position, Is.EqualTo(new int2(160, 100)));
			Assert.That(pinch.First.Delta.Y, Is.EqualTo(1));
		}

		[Test]
		public void PinchFromSubDeadzoneFingerMotionsCancelsTwoFingerTap()
		{
			var adapter = new TouchGestureAdapter();
			adapter.Begin(new int2(100, 100), 1000);
			adapter.BeginSecond(new int2(200, 100), 1000);

			Assert.That(MovePrimaryAt(adapter, new int2(94, 100), 1010).Count, Is.Zero);
			var pinch = MoveSecondAt(adapter, new int2(206, 100), 1020);
			Assert.That(pinch.Count, Is.EqualTo(1));
			Assert.That(pinch.First.Type, Is.EqualTo(TouchPointerActionType.Scroll));
			Assert.That(pinch.First.Delta.Y, Is.EqualTo(1));
			Assert.That(adapter.EndTwoFinger().Count, Is.Zero);
		}

		[Test]
		public void StationaryTwoFingerLongPressDoesNotBecomeRightClick()
		{
			var adapter = new TouchGestureAdapter();
			adapter.Begin(new int2(100, 100), 1000);
			adapter.BeginSecond(new int2(200, 100));

			Assert.That(adapter.Poll(1450).Count, Is.Zero);
			Assert.That(adapter.EndTwoFinger().Count, Is.Zero);
		}

		[Test]
		public void TwoFingerTapAllowsNaturalJitterInsideDeadzone()
		{
			var adapter = new TouchGestureAdapter();
			adapter.Begin(new int2(100, 100), 1000);
			adapter.BeginSecond(new int2(200, 100), 1010);

			Assert.That(adapter.MovePrimary(new int2(105, 104)).Count, Is.Zero);
			Assert.That(adapter.MoveSecond(new int2(204, 105)).Count, Is.Zero);
			var end = adapter.EndTwoFinger();
			Assert.That(end.Count, Is.EqualTo(2));
			Assert.That(end.First.Type, Is.EqualTo(TouchPointerActionType.RightDown));
			Assert.That(end.Second.Type, Is.EqualTo(TouchPointerActionType.RightUp));
		}

		[Test]
		public void CancellationReleasesActiveTwoFingerPan()
		{
			var adapter = new TouchGestureAdapter();
			adapter.Begin(new int2(100, 100), 1000);
			adapter.BeginSecond(new int2(200, 100), 1000);
			MovePrimaryAt(adapter, new int2(120, 100), 1010);
			var start = MoveSecondAt(adapter, new int2(220, 100), 1020);
			Assert.That(start.Count, Is.EqualTo(2));
			Assert.That(start.First.Type, Is.EqualTo(TouchPointerActionType.MiddleDown));

			var cancel = adapter.Cancel();
			Assert.That(cancel.Count, Is.EqualTo(1));
			Assert.That(cancel.First.Type, Is.EqualTo(TouchPointerActionType.MiddleUp));
			Assert.That(adapter.Cancel().Count, Is.Zero);
		}

		[Test]
		public void SymmetricPinchImmediatelyEmitsWheelCompatibleZoomDirection()
		{
			var adapter = new TouchGestureAdapter();
			adapter.Begin(new int2(100, 100), 1000);
			adapter.BeginSecond(new int2(200, 100), 1000);

			Assert.That(MovePrimaryAt(adapter, new int2(90, 100), 1010).Count, Is.Zero);
			var pinchOut = MoveSecondAt(adapter, new int2(210, 100), 1020);
			Assert.That(pinchOut.Count, Is.EqualTo(1));
			Assert.That(pinchOut.First.Type, Is.EqualTo(TouchPointerActionType.Scroll));
			Assert.That(pinchOut.First.Delta.Y, Is.EqualTo(1));

			Assert.That(MovePrimaryAt(adapter, new int2(110, 100), 1030).Count, Is.Zero);
			var pinchIn = adapter.Poll(1062);
			Assert.That(pinchIn.Count, Is.EqualTo(1));
			Assert.That(pinchIn.First.Type, Is.EqualTo(TouchPointerActionType.Scroll));
			Assert.That(pinchIn.First.Delta.Y, Is.EqualTo(-1));
		}

		[Test]
		public void PanModeRemainsLockedWhenSeparationLaterChanges()
		{
			var adapter = new TouchGestureAdapter();
			adapter.Begin(new int2(100, 100), 1000);
			adapter.BeginSecond(new int2(200, 100), 1000);
			MovePrimaryAt(adapter, new int2(120, 100), 1010);
			MoveSecondAt(adapter, new int2(220, 100), 1020);

			var separationChange = MovePrimaryAt(adapter, new int2(80, 100), 1030);
			Assert.That(separationChange.Count, Is.EqualTo(1));
			Assert.That(separationChange.First.Type, Is.EqualTo(TouchPointerActionType.MiddleMove));
			Assert.That(separationChange.First.Delta, Is.EqualTo(new int2(-20, 0)));
			Assert.That(separationChange.First.Type, Is.Not.EqualTo(TouchPointerActionType.Scroll));
		}

		[Test]
		public void PinchModeRemainsLockedWhenBothFingersLaterTranslate()
		{
			var adapter = new TouchGestureAdapter();
			adapter.Begin(new int2(100, 100), 1000);
			adapter.BeginSecond(new int2(200, 100), 1000);
			MovePrimaryAt(adapter, new int2(90, 100), 1010);
			MoveSecondAt(adapter, new int2(210, 100), 1020);

			var firstTranslation = MovePrimaryAt(adapter, new int2(130, 100), 1030);
			AssertNoActionType(firstTranslation, TouchPointerActionType.Scroll);
			AssertNoActionType(firstTranslation, TouchPointerActionType.MiddleDown);
			AssertNoActionType(firstTranslation, TouchPointerActionType.MiddleMove);
			var secondTranslation = MoveSecondAt(adapter, new int2(250, 100), 1040);
			AssertNoActionType(secondTranslation, TouchPointerActionType.Scroll);
			AssertNoActionType(secondTranslation, TouchPointerActionType.MiddleDown);
			AssertNoActionType(secondTranslation, TouchPointerActionType.MiddleMove);
		}

		[Test]
		public void LockedSymmetricPinchSettlesOnceWhenTheSecondFingerArrives()
		{
			var adapter = new TouchGestureAdapter();
			adapter.Begin(new int2(100, 100), 1000);
			adapter.BeginSecond(new int2(200, 100), 1000);
			MovePrimaryAt(adapter, new int2(90, 100), 1010);
			MoveSecondAt(adapter, new int2(210, 100), 1020);

			var first = MovePrimaryAt(adapter, new int2(70, 100), 1030);
			Assert.That(first.Count, Is.Zero);
			var second = MoveSecondAt(adapter, new int2(230, 100), 1040);
			Assert.That(second.Count, Is.EqualTo(1));
			Assert.That(second.First.Type, Is.EqualTo(TouchPointerActionType.Scroll));
			Assert.That(second.First.Delta.Y, Is.EqualTo(1));
		}

		[Test]
		public void LockedPinchStillScrollsWhenTranslationIncludesSignificantSeparationChange()
		{
			var adapter = new TouchGestureAdapter();
			adapter.Begin(new int2(100, 100), 1000);
			adapter.BeginSecond(new int2(200, 100), 1000);
			MovePrimaryAt(adapter, new int2(90, 100), 1010);
			MoveSecondAt(adapter, new int2(210, 100), 1020);

			Assert.That(MovePrimaryAt(adapter, new int2(140, 100), 1030).Count, Is.Zero);
			var settled = MoveSecondAt(adapter, new int2(240, 100), 1040);
			Assert.That(settled.Count, Is.EqualTo(1));
			Assert.That(settled.First.Type, Is.EqualTo(TouchPointerActionType.Scroll));
			Assert.That(settled.First.Delta.Y, Is.EqualTo(-1));
		}

		[Test]
		public void LockedAnchoredPinchWaitsForAFreshArbitrationWindow()
		{
			var adapter = new TouchGestureAdapter();
			adapter.Begin(new int2(100, 100), 1000);
			adapter.BeginSecond(new int2(200, 100), 1000);
			MovePrimaryAt(adapter, new int2(90, 100), 1010);
			MoveSecondAt(adapter, new int2(210, 100), 1020);

			Assert.That(MoveSecondAt(adapter, new int2(230, 100), 1050).Count, Is.Zero);
			Assert.That(adapter.Poll(1040).Count, Is.Zero,
				"A regressed timestamp must not underflow the arbitration window.");
			Assert.That(adapter.Poll(1081).Count, Is.Zero);
			var settled = adapter.Poll(1082);
			Assert.That(settled.Count, Is.EqualTo(1));
			Assert.That(settled.First.Type, Is.EqualTo(TouchPointerActionType.Scroll));
			Assert.That(settled.First.Position, Is.EqualTo(new int2(160, 100)));
			Assert.That(settled.First.Delta.Y, Is.EqualTo(1));
		}

		[TestCase(30)]
		[TestCase(60)]
		[TestCase(120)]
		public void LockedPinchCommonTranslationDoesNotScrollAtRepresentativeCadences(int frequency)
		{
			var adapter = new TouchGestureAdapter();
			adapter.Begin(new int2(100, 100), 1000);
			adapter.BeginSecond(new int2(200, 100), 1000);
			MovePrimaryAt(adapter, new int2(90, 100), 1010);
			MoveSecondAt(adapter, new int2(210, 100), 1020);
			var frameDuration = (1000 + frequency / 2) / frequency;
			var movementPerFrame = 360 / frequency;
			var frameCount = frequency / 10;

			for (var frame = 1; frame <= frameCount; frame++)
			{
				var offset = frame * movementPerFrame;
				var timestamp = 1100UL + (ulong)(frame * frameDuration);
				var primary = MovePrimaryAt(adapter, new int2(90 + offset, 100), timestamp);
				Assert.That(primary.Count, Is.Zero);
				AssertNoActionType(primary, TouchPointerActionType.Scroll);
				AssertNoActionType(primary, TouchPointerActionType.MiddleMove);

				var second = MoveSecondAt(adapter, new int2(210 + offset, 100), timestamp + 1);
				Assert.That(second.Count, Is.Zero);
				AssertNoActionType(second, TouchPointerActionType.Scroll);
				AssertNoActionType(second, TouchPointerActionType.MiddleMove);
				Assert.That(adapter.Poll(timestamp + (ulong)(frameDuration - 1)).Count, Is.Zero);
			}
		}

		[Test]
		public void PinchAccumulatorSurvivesDominantCommonTranslation()
		{
			var adapter = new TouchGestureAdapter();
			adapter.Begin(new int2(100, 100), 1000);
			adapter.BeginSecond(new int2(200, 100), 1000);
			MovePrimaryAt(adapter, new int2(90, 100), 1010);
			MoveSecondAt(adapter, new int2(210, 100), 1020);

			for (var frame = 1; frame <= 6; frame++)
			{
				var timestamp = 1100UL + (ulong)(frame * 20);
				var primary = MovePrimaryAt(adapter, new int2(90 + frame * 9, 100), timestamp);
				Assert.That(primary.Count, Is.Zero);
				AssertNoActionType(primary, TouchPointerActionType.MiddleMove);

				var second = MoveSecondAt(adapter, new int2(210 + frame * 11, 100), timestamp + 1);
				if (frame < 6)
					Assert.That(second.Count, Is.Zero);
				else
				{
					Assert.That(second.Count, Is.EqualTo(1));
					Assert.That(second.First.Type, Is.EqualTo(TouchPointerActionType.Scroll));
					Assert.That(second.First.Delta.Y, Is.EqualTo(1));
				}

				AssertNoActionType(second, TouchPointerActionType.MiddleDown);
				AssertNoActionType(second, TouchPointerActionType.MiddleMove);
				Assert.That(adapter.Poll(timestamp + 19).Count, Is.Zero);
			}

			for (var frame = 1; frame <= 6; frame++)
			{
				var timestamp = 1300UL + (ulong)(frame * 20);
				var primary = MovePrimaryAt(adapter, new int2(144 + frame * 11, 100), timestamp);
				Assert.That(primary.Count, Is.Zero);
				AssertNoActionType(primary, TouchPointerActionType.MiddleMove);

				var second = MoveSecondAt(adapter, new int2(276 + frame * 9, 100), timestamp + 1);
				if (frame < 6)
					Assert.That(second.Count, Is.Zero);
				else
				{
					Assert.That(second.Count, Is.EqualTo(1));
					Assert.That(second.First.Type, Is.EqualTo(TouchPointerActionType.Scroll));
					Assert.That(second.First.Delta.Y, Is.EqualTo(-1));
				}

				AssertNoActionType(second, TouchPointerActionType.MiddleDown);
				AssertNoActionType(second, TouchPointerActionType.MiddleMove);
				Assert.That(adapter.Poll(timestamp + 19).Count, Is.Zero);
			}
		}

		[TestCase(false)]
		[TestCase(true)]
		public void EndingOrCancellingLockedPinchClearsPendingPairArbitration(bool cancel)
		{
			var adapter = new TouchGestureAdapter();
			adapter.Begin(new int2(100, 100), 1000);
			adapter.BeginSecond(new int2(200, 100), 1000);
			MovePrimaryAt(adapter, new int2(90, 100), 1010);
			MoveSecondAt(adapter, new int2(210, 100), 1020);
			Assert.That(MoveSecondAt(adapter, new int2(230, 100), 1050).Count, Is.Zero);

			var interrupted = cancel ? adapter.Cancel() : adapter.EndTwoFinger();
			Assert.That(interrupted.Count, Is.Zero);
			Assert.That(adapter.Poll(2000).Count, Is.Zero);
			adapter.Begin(new int2(50, 50), 3000);
			adapter.BeginSecond(new int2(150, 50), 3000);
			var tap = adapter.EndTwoFinger();
			Assert.That(tap.Count, Is.EqualTo(2));
			Assert.That(tap.First.Type, Is.EqualTo(TouchPointerActionType.RightDown));
			Assert.That(tap.Second.Type, Is.EqualTo(TouchPointerActionType.RightUp));
		}

		[Test]
		public void PureRotationEmitsNeitherPanNorPinch()
		{
			var adapter = new TouchGestureAdapter();
			adapter.Begin(new int2(100, 100), 1000);
			adapter.BeginSecond(new int2(200, 100), 1000);

			Assert.That(MovePrimaryAt(adapter, new int2(150, 50), 1010).Count, Is.Zero);
			Assert.That(MoveSecondAt(adapter, new int2(150, 150), 1020).Count, Is.Zero);
			Assert.That(adapter.Poll(1100).Count, Is.Zero);
			Assert.That(adapter.EndTwoFinger().Count, Is.Zero);
		}

		[Test]
		public void BalancedPanAndPinchEvidenceRemainsPending()
		{
			var adapter = new TouchGestureAdapter();
			adapter.Begin(new int2(100, 100), 1000);
			adapter.BeginSecond(new int2(200, 100), 1000);

			Assert.That(MovePrimaryAt(adapter, new int2(110, 100), 1010).Count, Is.Zero);
			Assert.That(MoveSecondAt(adapter, new int2(230, 100), 1020).Count, Is.Zero);
			Assert.That(adapter.Poll(1100).Count, Is.Zero);
			Assert.That(adapter.EndTwoFinger().Count, Is.Zero);
		}

		[TestCase(30)]
		[TestCase(60)]
		[TestCase(120)]
		public void MultiFrameSameDirectionMotionPansAtRepresentativeCadences(int frequency)
		{
			var adapter = new TouchGestureAdapter();
			adapter.Begin(new int2(100, 100), 1000);
			adapter.BeginSecond(new int2(200, 100), 1000);
			var frameDuration = (1000 + frequency / 2) / frequency;
			var movementPerFrame = 120 / frequency;
			var frameCount = 8 / movementPerFrame + 1;

			for (var frame = 1; frame <= frameCount; frame++)
			{
				var offset = frame * movementPerFrame;
				var timestamp = 1000UL + (ulong)(frame * frameDuration);
				var primary = MovePrimaryAt(adapter, new int2(100 + offset, 100), timestamp);
				Assert.That(primary.Count, Is.Zero);
				AssertNoActionType(primary, TouchPointerActionType.Scroll);

				var second = MoveSecondAt(adapter, new int2(200 + offset, 100), timestamp + 1);
				AssertNoActionType(second, TouchPointerActionType.Scroll);
				if (frame < frameCount)
					Assert.That(second.Count, Is.Zero);
				else
				{
					Assert.That(second.Count, Is.EqualTo(2));
					Assert.That(second.First.Type, Is.EqualTo(TouchPointerActionType.MiddleDown));
					Assert.That(second.Second.Type, Is.EqualTo(TouchPointerActionType.MiddleMove));
					Assert.That(second.Second.Delta, Is.EqualTo(new int2(offset, 0)));
				}

				var poll = adapter.Poll(timestamp + (ulong)(frameDuration - 1));
				Assert.That(poll.Count, Is.Zero);
				AssertNoActionType(poll, TouchPointerActionType.Scroll);
			}
		}

		[Test]
		public void CancellationReleasesTwoFingerGestureAndAllowsNewTap()
		{
			var adapter = new TouchGestureAdapter();
			adapter.Begin(new int2(100, 100), 1000);
			adapter.BeginSecond(new int2(200, 100));

			var reset = adapter.Cancel();
			Assert.That(reset.Count, Is.Zero);

			adapter.Begin(new int2(50, 50), 2000);
			var tap = adapter.End(new int2(50, 50));
			Assert.That(tap.First.Type, Is.EqualTo(TouchPointerActionType.LeftDown));
			Assert.That(tap.Second.Type, Is.EqualTo(TouchPointerActionType.LeftUp));
		}

		[Test]
		public void RepeatedCancellationIsIdempotent()
		{
			var adapter = new TouchGestureAdapter();
			adapter.Begin(new int2(100, 100), 1000);
			adapter.BeginSecond(new int2(200, 100));

			Assert.That(adapter.Cancel().Count, Is.Zero);
			Assert.That(adapter.Cancel().Count, Is.Zero);
		}

		[TestCase(false)]
		[TestCase(true)]
		public void ActivePanReleasesOnceForFingerLiftOrCancellation(bool cancel)
		{
			var adapter = new TouchGestureAdapter();
			adapter.Begin(new int2(100, 100), 1000);
			adapter.BeginSecond(new int2(200, 100), 1000);
			MovePrimaryAt(adapter, new int2(120, 100), 1010);
			MoveSecondAt(adapter, new int2(220, 100), 1020);

			var release = cancel ? adapter.Cancel() : adapter.EndTwoFinger();
			Assert.That(release.Count, Is.EqualTo(1));
			Assert.That(release.First.Type, Is.EqualTo(TouchPointerActionType.MiddleUp));
			Assert.That(adapter.EndTwoFinger().Count, Is.Zero);
			Assert.That(adapter.Cancel().Count, Is.Zero);
		}

		[Test]
		public void ThirdFingerRestartsViewportPanAtThreeFingerCentroid()
		{
			var adapter = new TouchGestureAdapter();
			adapter.Begin(new int2(90, 90), 1000);
			adapter.BeginSecond(new int2(180, 90));

			var start = adapter.BeginThird(new int2(270, 90));
			Assert.That(start.Count, Is.EqualTo(1));
			Assert.That(start.First.Type, Is.EqualTo(TouchPointerActionType.MiddleDown));
			Assert.That(start.First.Position, Is.EqualTo(new int2(180, 90)));

			var move = adapter.MoveThird(new int2(300, 90));
			Assert.That(move.First.Type, Is.EqualTo(TouchPointerActionType.MiddleMove));
			Assert.That(move.First.Delta, Is.EqualTo(new int2(10, 0)));
		}

		[Test]
		public void CancellingActiveThreeFingerPanReleasesMiddleButtonExactlyOnce()
		{
			var adapter = new TouchGestureAdapter();
			adapter.Begin(new int2(90, 90), 1000);
			adapter.BeginSecond(new int2(180, 90), 1010);
			var start = adapter.BeginThird(new int2(270, 90));
			Assert.That(start.Count, Is.EqualTo(1));
			Assert.That(start.First.Type, Is.EqualTo(TouchPointerActionType.MiddleDown));

			var cancel = adapter.Cancel();
			Assert.That(cancel.Count, Is.EqualTo(1));
			Assert.That(cancel.First.Type, Is.EqualTo(TouchPointerActionType.MiddleUp));
			Assert.That(cancel.First.Position, Is.EqualTo(new int2(180, 90)));
			Assert.That(adapter.Cancel().Count, Is.Zero);
			Assert.That(adapter.EndThreeFinger().Count, Is.Zero);
			Assert.That(adapter.EndTwoFinger().Count, Is.Zero);
			Assert.That(adapter.End(new int2(90, 90)).Count, Is.Zero);
		}

		[Test]
		public void ThirdFingerHandoffBalancesAnActiveTwoFingerPan()
		{
			var adapter = new TouchGestureAdapter();
			adapter.Begin(new int2(100, 100), 1000);
			adapter.BeginSecond(new int2(200, 100), 1000);
			MovePrimaryAt(adapter, new int2(120, 100), 1010);
			MoveSecondAt(adapter, new int2(220, 100), 1020);

			var handoff = adapter.BeginThird(new int2(300, 100));
			Assert.That(handoff.Count, Is.EqualTo(2));
			Assert.That(handoff.First.Type, Is.EqualTo(TouchPointerActionType.MiddleUp));
			Assert.That(handoff.First.Position, Is.EqualTo(new int2(170, 100)));
			Assert.That(handoff.Second.Type, Is.EqualTo(TouchPointerActionType.MiddleDown));
			Assert.That(handoff.Second.Position, Is.EqualTo(new int2(213, 100)));

			var move = adapter.MoveThird(new int2(330, 100));
			Assert.That(move.Count, Is.EqualTo(1));
			Assert.That(move.First.Type, Is.EqualTo(TouchPointerActionType.MiddleMove));
			var end = adapter.EndThreeFinger();
			Assert.That(end.Count, Is.EqualTo(1));
			Assert.That(end.First.Type, Is.EqualTo(TouchPointerActionType.MiddleUp));
			Assert.That(adapter.Cancel().Count, Is.Zero);
		}

		[Test]
		public void EndingThreeFingerPanSuppressesRemainingTouches()
		{
			var adapter = new TouchGestureAdapter();
			adapter.Begin(new int2(90, 90), 1000);
			adapter.BeginSecond(new int2(180, 90));
			adapter.BeginThird(new int2(270, 90));

			var end = adapter.EndThreeFinger();
			Assert.That(end.Count, Is.EqualTo(1));
			Assert.That(end.First.Type, Is.EqualTo(TouchPointerActionType.MiddleUp));
			Assert.That(adapter.End(new int2(90, 90)).Count, Is.Zero);
			Assert.That(adapter.Cancel().Count, Is.Zero);
		}

		static TouchGestureResult MovePrimaryAt(TouchGestureAdapter adapter, int2 position, ulong timestamp)
		{
			return adapter.MovePrimary(position, timestamp);
		}

		static TouchGestureResult MoveSecondAt(TouchGestureAdapter adapter, int2 position, ulong timestamp)
		{
			return adapter.MoveSecond(position, timestamp);
		}

		static string RepositoryRoot()
		{
			var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
			while (directory != null && !File.Exists(Path.Combine(
				directory.FullName, "engine", "OpenRA.Platforms.Default", "Sdl2Input.cs")))
				directory = directory.Parent;

			Assert.That(directory, Is.Not.Null, "Unable to locate the repository root.");
			return directory!.FullName;
		}

		static string SourceBetween(string source, string startMarker, string endMarker)
		{
			var start = source.IndexOf(startMarker, System.StringComparison.Ordinal);
			Assert.That(start, Is.GreaterThanOrEqualTo(0), $"Missing source marker: {startMarker}");
			var end = source.IndexOf(endMarker, start + startMarker.Length, System.StringComparison.Ordinal);
			Assert.That(end, Is.GreaterThan(start), $"Missing source marker: {endMarker}");
			return source.Substring(start, end - start);
		}

		static int CountOccurrences(string source, string value)
		{
			var count = 0;
			var index = 0;
			while ((index = source.IndexOf(value, index, System.StringComparison.Ordinal)) >= 0)
			{
				count++;
				index += value.Length;
			}

			return count;
		}

		static void AssertNoActionType(TouchGestureResult result, TouchPointerActionType type)
		{
			if (result.Count > 0)
				Assert.That(result.First.Type, Is.Not.EqualTo(type));
			if (result.Count > 1)
				Assert.That(result.Second.Type, Is.Not.EqualTo(type));
			if (result.Count > 2)
				Assert.That(result.Third.Type, Is.Not.EqualTo(type));
		}
	}
}
