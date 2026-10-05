using NUnit.Framework;
using OpenRA.Mods.RA2.Widgets;
using OpenRA.Widgets;

namespace OpenRA.Test
{
	[TestFixture]
	public class MenuVideoApertureTest
	{
		[TestCase("standard", 790, 15, 1920, 1080)]
		[TestCase("tablet", 575, 105, 1536, 1152)]
		[TestCase("tablet", 665, 100, 1536, 1152)]
		[TestCase("tablet", 687, 20, 1536, 1152)]
		[TestCase("tablet", 678, 20, 1536, 1152)]
		[TestCase("tablet", 679, 52, 1536, 1152)]
		[TestCase("ultrawide", 660, 15, 2048, 947)]
		public void SkyBetweenPipesIsReplaced(string profile, double x, double y, int width, int height)
		{
			Assert.That(MenuVideoAperture.Contains(profile, x / width, y / height), Is.True);
			Assert.That(MenuVideoAperture.Left(profile), Is.LessThan(x / width));
		}

		[TestCase("standard", 790, 47, 1920, 1080)]
		[TestCase("tablet", 590, 76, 1536, 1152)]
		[TestCase("tablet", 680, 85, 1536, 1152)]
		[TestCase("ultrawide", 660, 40, 2048, 947)]
		public void PipesBetweenSkyOpeningsRemainOpaque(string profile, double x, double y, int width, int height)
		{
			Assert.That(MenuVideoAperture.Contains(profile, x / width, y / height), Is.False);
		}

		[TestCase("standard")]
		[TestCase("tablet")]
		[TestCase("ultrawide")]
		public void VideoNeverCoversForeground(string profile)
		{
			Assert.That(MenuVideoAperture.Contains(profile, .25, .4), Is.False);
			Assert.That(MenuVideoAperture.Contains(profile, .75, .92), Is.False);
			Assert.That(MenuVideoAperture.Contains(profile, .75, .3), Is.True);
			Assert.That(MenuVideoAperture.Contains(profile, .02, .02), Is.False);
		}

		[Test]
		public void FrameClockWrapsWithoutExceedingLastFrame()
		{
			Assert.That(MenuVideoAperture.FrameAt(0, 24, 10), Is.EqualTo(0));
			Assert.That(MenuVideoAperture.FrameAt(9.99, 24, 10), Is.EqualTo(239));
			Assert.That(MenuVideoAperture.FrameAt(10, 24, 10), Is.EqualTo(0));
			Assert.That(MenuVideoAperture.FrameAt(10.5, 24, 10), Is.EqualTo(12));
		}

		[Test]
		public void CloningPreservesConfigurationWithoutSharingPlayback()
		{
			var original = new MenuVideoBackgroundWidget { Profile = "tablet", Video = "ra2|test.mp4",
				Background = "cc-soviet-shell-tablet" };
			var clone = (MenuVideoBackgroundWidget)original.Clone();
			Assert.That(clone.Profile, Is.EqualTo(original.Profile));
			Assert.That(clone.Video, Is.EqualTo(original.Video));
			Assert.That(clone.IosVideo, Is.EqualTo(original.IosVideo));
			Assert.That(clone.Background, Is.EqualTo(original.Background));
			original.Removed();
			clone.Removed();
		}

		[Test]
		public void InvalidClockInputCannotProduceInvalidFrameIndex()
		{
			Assert.That(MenuVideoAperture.FrameAt(double.NaN, 24, 10), Is.Zero);
			Assert.That(MenuVideoAperture.FrameAt(1, 24, 0), Is.Zero);
			Assert.That(MenuVideoAperture.FrameAt(-1, 24, 10), Is.Zero);
		}

		[Test]
		public void VideoOnlyPlaysWhileHomeMenuIsActive()
		{
			var root = new ContainerWidget();
			var home = new ContainerWidget { Id = "MAIN_MENU" };
			var background = new MenuVideoBackgroundWidget();
			root.AddChild(home);
			root.AddChild(background);
			home.IsVisible = () => true;
			Assert.That(background.ShouldPlayVideo, Is.True);
			home.IsVisible = () => false;
			Assert.That(background.ShouldPlayVideo, Is.False);
			Assert.That(new MenuVideoBackgroundWidget().ShouldPlayVideo, Is.False);
		}
	}
}
