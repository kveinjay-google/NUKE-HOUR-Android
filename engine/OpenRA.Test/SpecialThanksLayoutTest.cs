using NUnit.Framework;
using OpenRA.Mods.RA2.Widgets;
using OpenRA.Primitives;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class SpecialThanksLayoutTest
	{
		[Test]
		public void DesktopStaysDesktopAtUltrawideAspect()
		{
			var size = new Size(3440, 1440);
			var snapshot = new IosScreenSnapshot(size, size, default);
			Assert.That(SpecialThanksPanelWidget.ProfileFor(false, snapshot), Is.EqualTo("desktop"));
		}

		[TestCase(932, 430, "phone")]
		[TestCase(1194, 834, "tablet")]
		public void IosUsesNativeDeviceClass(int width, int height, string expected)
		{
			var size = new Size(width, height);
			var snapshot = new IosScreenSnapshot(size, size, default);
			Assert.That(SpecialThanksPanelWidget.ProfileFor(true, snapshot), Is.EqualTo(expected));
		}

		[TestCase(320, 0, 0)]
		[TestCase(320, 3, 0)]
		[TestCase(320, 4, 0)]
		[TestCase(320, 7, 240)]
		public void FourSupporterRowsDefineTheManualScrollRange(int height, int count, int expected)
		{
			Assert.That(SpecialThanksRollWidget.MaximumScrollOffset(count, height), Is.EqualTo(expected));
		}

		[TestCase(-100, 7, 320, 0)]
		[TestCase(160, 7, 320, 160)]
		[TestCase(999, 7, 320, 240)]
		public void ManualScrollOffsetIsClampedToTheAvailableNames(
			int offset, int count, int height, int expected)
		{
			Assert.That(SpecialThanksRollWidget.ClampScrollOffset(offset, count, height), Is.EqualTo(expected));
		}

		[TestCase(0)]
		[TestCase(7.5)]
		[TestCase(80)]
		public void FittingNamesNeverFade(double seconds)
		{
			Assert.That(SpecialThanksRollWidget.PageAt(4, 4, seconds), Is.EqualTo((0, 1f)));
		}

		[TestCase(0, 0, 0f)]
		[TestCase(.5, 0, .5f)]
		[TestCase(4, 0, 1f)]
		[TestCase(7.5, 0, .5f)]
		[TestCase(8, 1, 0f)]
		[TestCase(12, 1, 1f)]
		[TestCase(16, 0, 0f)]
		public void OverflowPagesFadeWithoutScrolling(double seconds, int page, float alpha)
		{
			Assert.That(SpecialThanksRollWidget.PageAt(5, 4, seconds), Is.EqualTo((page, alpha)));
		}

		[TestCase(1484, 1060, 1180, 820)]
		[TestCase(1484, 1060, 2732, 2048)]
		[TestCase(1672, 941, 1920, 1080)]
		[TestCase(1848, 851, 2868, 1320)]
		public void ClothRemainsCenteredAndInsideViewport(int sw, int sh, int tw, int th)
		{
			var rect = SpecialThanksPanelWidget.MapRegion(new Size(sw, sh), new Size(tw, th), .31, .31, .38, .18);
			Assert.That(new Rectangle(0, 0, tw, th).Contains(rect), Is.True);
			Assert.That(System.Math.Abs(rect.Left + rect.Right - tw), Is.LessThanOrEqualTo(2));
		}

		[TestCase("phone", 2868, 1320)]
		[TestCase("tablet", 2360, 1640)]
		[TestCase("desktop", 1920, 1080)]
		public void SupporterRollIsClippedInsideTheRedCurtain(string profile, int width, int height)
		{
			var target = new Size(width, height);
			var roll = SpecialThanksPanelWidget.SupporterRollBoundsFor(profile, target);
			var curtain = SpecialThanksPanelWidget.CurtainSafeBoundsFor(profile, target);
			Assert.That(curtain.Contains(roll), Is.True);
		}

		[TestCase("phone", 2868, 1320)]
		[TestCase("tablet", 2360, 1640)]
		[TestCase("desktop", 1920, 1080)]
		public void ActionLabelsStayInsideTheirBakedButtonFrames(string profile, int width, int height)
		{
			var target = new Size(width, height);
			for (var i = 0; i < 3; i++)
			{
				var label = SpecialThanksPanelWidget.ActionButtonBoundsFor(profile, target, i);
				var frame = SpecialThanksPanelWidget.ActionButtonSafeBoundsFor(profile, target, i);
				Assert.That(frame.Contains(label), Is.True, $"{profile} button {i}");
			}
		}

		[TestCase("phone", 2868, 1320)]
		[TestCase("tablet", 2360, 1640)]
		[TestCase("desktop", 1920, 1080)]
		public void TitleStaysInsideTheBakedTitleFrame(string profile, int width, int height)
		{
			var target = new Size(width, height);
			var title = SpecialThanksPanelWidget.TitleBoundsFor(profile, target);
			var frame = SpecialThanksPanelWidget.TitleFrameSafeBoundsFor(profile, target);
			Assert.That(frame.Contains(title), Is.True);
		}
	}
}
