using System;

namespace OpenRA
{
	// Touch-only UI gestures must be consumed before the global adapter's
	// force-attack and viewport-pan actions become mouse input.
	public interface IWidgetTouchGestureTarget
	{
		bool AcceptsTouch(int2 position);
		void BeginTouch(int2 position);
		void HandleTouchLongPress(int2 origin, int2 position);
		void HandleTouchPinch(int direction);
	}

	public sealed class WidgetTouchGestureCapture
	{
		IWidgetTouchGestureTarget target;
		int2 origin;
		bool ownsSequence, multiple, paired, blocked, pinchApplied;
		int pinchSteps;
		public bool OwnsSequence => ownsSequence;

		public void Begin(IWidgetTouchGestureTarget candidate, int2 position, bool independentTouchActive = false)
		{
			Reset();
			origin = position;
			target = candidate != null && candidate.AcceptsTouch(position) ? candidate : null;
			ownsSequence = target != null;
			blocked = independentTouchActive;
			target?.BeginTouch(position);
		}

		public bool IndependentTouchBegan()
		{
			blocked = true;
			return ownsSequence;
		}

		public void BeginSecond(IWidgetTouchGestureTarget candidate)
		{
			multiple = true;
			paired = target != null && ReferenceEquals(target, candidate);
			ownsSequence |= candidate != null;
		}

		public void BeginThird() { multiple = true; paired = false; }
		public void Block() { blocked = true; }

		public bool Handle(TouchPointerAction action)
		{
			if (!ownsSequence || action.Type == TouchPointerActionType.LeftCancel)
				return false;
			if (blocked)
				return true;

			if (multiple)
			{
				if (paired && !pinchApplied && action.Type == TouchPointerActionType.Scroll && target.AcceptsTouch(origin))
				{
					pinchSteps += action.Delta.Y;
					if (Math.Abs(pinchSteps) >= 2)
					{
						pinchApplied = true;
						target.HandleTouchPinch(Math.Sign(pinchSteps));
					}
				}

				// Includes two-finger tap and pan: neither is a production click.
				return true;
			}

			if (action.Type == TouchPointerActionType.ForceAttackDown)
			{
				if (target.AcceptsTouch(origin) && target.AcceptsTouch(action.Position))
					target.HandleTouchLongPress(origin, action.Position);
				return true;
			}

			return action.Type == TouchPointerActionType.ForceAttackUp;
		}

		public void Reset()
		{
			target = null;
			ownsSequence = multiple = paired = blocked = pinchApplied = false;
			pinchSteps = 0;
		}
	}
}
