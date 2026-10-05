using NUnit.Framework;
using OpenRA.Mods.Common.Widgets.Logic;
using OpenRA.Primitives;

namespace OpenRA.Test
{
	[TestFixture]
	public class ConnectionDialogLayoutTest
	{
		[TestCase(667, 320)]
		[TestCase(812, 375)]
		[TestCase(390, 844)]
		[TestCase(1180, 820)]
		[TestCase(1440, 900)]
		public void FixedFooterStaysInsidePanel(int width, int height)
		{
			var size = new Size(width, height);
			var layout = new ConnectionDialogLayout(new IosScreenSnapshot(size, size, default));
			Assert.That(layout.Panel.X, Is.GreaterThanOrEqualTo(0));
			Assert.That(layout.Panel.Bottom, Is.LessThan(height));
			Assert.That(layout.Panel.Width, Is.LessThanOrEqualTo(680));
			Assert.That(layout.Title.Bottom, Is.LessThan(layout.Details.Y));
			Assert.That(layout.Details.Height, Is.GreaterThan(40));
			Assert.That(layout.Details.Bottom, Is.LessThan(layout.Actions.Y));
			Assert.That(layout.Actions.Height, Is.EqualTo(56));
			Assert.That(layout.Actions.Bottom, Is.LessThan(layout.Panel.Height));
			Assert.That(layout.Actions.Right, Is.LessThan(layout.Panel.Width));
		}

		[TestCase(1624, 750, 812, 375)]
		[TestCase(2360, 1640, 1180, 820)]
		[TestCase(1440, 900, 1440, 900)]
		public void ArtworkConnectionFillsSafeAreaWithoutCompactModal(int width, int height, int nativeWidth, int nativeHeight)
		{
			var screen = new IosScreenSnapshot(new Size(width, height), new Size(nativeWidth, nativeHeight), default);
			var layout = new ConnectionDialogLayout(screen, true, true);
			var border = screen.IsCompactPhone ? screen.LogicalPoints(2) : 0;
			Assert.That(layout.Panel.Width, Is.EqualTo(screen.SafeBounds.Width - 2 * border));
			Assert.That(layout.Panel.Height, Is.EqualTo(screen.SafeBounds.Height - 2 * border));
			Assert.That(layout.Details.X, Is.GreaterThan(layout.Panel.Width / 2));
			Assert.That(layout.Details.Bottom, Is.LessThan(layout.Actions.Y));
			Assert.That(layout.Actions.Bottom, Is.LessThan(layout.Panel.Height));
		}

		[Test]
		public void ConnectingUsesCompactPanelWithSafeAreaAndRetinaTargets()
		{
			var screen = new IosScreenSnapshot(new Size(1624, 750), new Size(812, 375), new IosSafeAreaInsets(44, 0, 44, 21));
			var layout = new ConnectionDialogLayout(screen, true);
			Assert.That(layout.Panel.X, Is.GreaterThanOrEqualTo(screen.SafeBounds.Left));
			Assert.That(layout.Panel.Right, Is.LessThanOrEqualTo(screen.SafeBounds.Right));
			Assert.That(layout.Panel.Bottom, Is.LessThanOrEqualTo(screen.SafeBounds.Bottom));
			Assert.That(layout.Panel.Height, Is.EqualTo(screen.LogicalPoints(280)));
			Assert.That(layout.Actions.Height, Is.EqualTo(screen.LogicalPoints(56)));
		}
	}
}
