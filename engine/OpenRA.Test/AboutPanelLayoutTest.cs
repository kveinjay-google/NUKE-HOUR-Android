using NUnit.Framework;
using OpenRA.Mods.Common.Widgets.Logic;
using OpenRA.Primitives;

namespace OpenRA.Test
{
	[TestFixture]
	public class AboutPanelLayoutTest
	{
		[TestCase(667, 320)]
		[TestCase(812, 375)]
		[TestCase(1180, 820)]
		[TestCase(1440, 900)]
		[TestCase(2560, 1440)]
		public void FullscreenAboutKeepsContentAndFixedActionsSeparate(int width, int height)
		{
			var size = new Size(width, height);
			var layout = new AboutPanelLayout(new IosScreenSnapshot(size, size, default));
			Assert.That(layout.Title.Bottom, Is.LessThan(layout.Scroll.Y));
			Assert.That(layout.Scroll.Bottom, Is.LessThan(layout.Website.Y));
			Assert.That(layout.Website.Right, Is.LessThan(layout.Back.X));
			Assert.That(layout.Back.Bottom, Is.LessThan(height));
			var screen = new IosScreenSnapshot(size, size, default);
			var requiredBottomClearance = screen.LogicalPoints(screen.IsCompactPhone ? 32 : 24);
			Assert.That(height - layout.Back.Bottom, Is.GreaterThanOrEqualTo(requiredBottomClearance));
			Assert.That(layout.Back.Height, Is.GreaterThanOrEqualTo(56));
			Assert.That(layout.Scroll.Height, Is.GreaterThan(40));
			Assert.That(layout.Scroll.Width, Is.LessThanOrEqualTo(1120));
		}
	}
}
