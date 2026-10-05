using NUnit.Framework;
using OpenRA.Mods.Common.Orders;
using OpenRA.Primitives;

namespace OpenRA.Test
{
	[TestFixture]
	public class TouchAttackTargetingTest
	{
		[Test]
		public void PhysicalMouseDoesNotInheritTouchAssistance()
		{
			var mouse = new MouseInput(MouseInputEvent.Up, MouseButton.Left, int2.Zero, int2.Zero, Modifiers.None, 1);
			var touch = new MouseInput(MouseInputEvent.Up, MouseButton.Left, int2.Zero, int2.Zero, Modifiers.None, 1, isTouch: true);
			Assert.That(mouse.IsTouch, Is.False);
			Assert.That(touch.IsTouch, Is.True);
		}

		[TestCase(true, true, Modifiers.None, 1, true)]
		[TestCase(true, true, Modifiers.Shift, 1, true)]
		[TestCase(false, true, Modifiers.None, 1, false)]
		[TestCase(true, false, Modifiers.None, 1, false)]
		[TestCase(true, true, Modifiers.Alt, 1, false)]
		[TestCase(true, true, Modifiers.Ctrl, 1, false)]
		[TestCase(true, true, Modifiers.None, 0, false)]
		public void AssistancePreservesMouseSelectionAndExplicitGroundCommands(bool touch, bool ios, Modifiers modifiers, int count, bool expected)
		{
			Assert.That(TouchAttackTargeting.ShouldAssist(touch, ios, modifiers, count), Is.EqualTo(expected));
		}

		[Test]
		public void NearMissBesideNarrowInfantryBoundsFindsEnemy()
		{
			var bounds = new[] { new Rectangle(100, 100, 6, 20) };
			Assert.That(TouchAttackTargeting.NearestIndex(bounds, _ => true, new int2(111, 108), 12), Is.EqualTo(0));
		}

		[Test]
		public void NearestEligibleTargetWinsInsteadOfEnumerationOrder()
		{
			var bounds = new[] { new Rectangle(110, 100, 5, 10), new Rectangle(102, 100, 5, 10) };
			Assert.That(TouchAttackTargeting.NearestIndex(bounds, _ => true, new int2(100, 105), 12), Is.EqualTo(1));
		}

		[Test]
		public void IneligibleFriendlyHiddenOrUnattackableTargetIsSkipped()
		{
			var bounds = new[] { new Rectangle(100, 100, 5, 10), new Rectangle(108, 100, 5, 10) };
			Assert.That(TouchAttackTargeting.NearestIndex(bounds, i => i == 1, new int2(101, 105), 12), Is.EqualTo(1));
		}

		[TestCase(130, 105)]
		[TestCase(116, 120)]
		public void EmptyGroundOutsideCircularToleranceRemainsGround(int x, int y)
		{
			var bounds = new[] { new Rectangle(100, 100, 5, 10) };
			Assert.That(TouchAttackTargeting.NearestIndex(bounds, _ => true, new int2(x, y), 12), Is.EqualTo(-1));
		}
	}
}
