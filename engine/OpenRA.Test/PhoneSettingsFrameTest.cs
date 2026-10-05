using NUnit.Framework;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Mods.Common.Widgets.Logic;
using OpenRA.Primitives;

namespace OpenRA.Test
{
	[TestFixture]
	public class PhoneSettingsFrameTest
	{
		[TestCase(844, 390)]
		[TestCase(956, 440)]
		public void PhoneHasNoBrandReservationAndUsesSharedContentInset(int width, int height)
		{
			var screen = new IosScreenSnapshot(new Size(width, height), new Size(width, height), default);
			var layout = new IngameCommandLayout(screen);
			Assert.That(layout.Header.Height, Is.Zero);
			Assert.That(layout.Content, Is.EqualTo(MultiplayerScreenLayout.ContentBounds(screen, compactPhone: true)));
			Assert.That(layout.Content.Y, Is.EqualTo(screen.LogicalPoints(2)));
		}
	}
}
