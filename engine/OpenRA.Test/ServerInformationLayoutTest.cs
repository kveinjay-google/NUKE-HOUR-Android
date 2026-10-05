using NUnit.Framework;
using OpenRA.Mods.Common.Widgets.Logic;
using OpenRA.Primitives;

namespace OpenRA.Test
{
	[TestFixture]
	public class ServerInformationLayoutTest
	{
		[TestCase(844, 390, 844, 390)]
		[TestCase(1558, 720, 844, 390)]
		[TestCase(667, 375, 667, 375)]
		[TestCase(1180, 820, 1180, 820)]
		[TestCase(2360, 1640, 1180, 820)]
		[TestCase(1920, 1080, 1920, 1080)]
		public void DialogIsCenteredWithVisibleSurroundingsAndContainedControls(int w, int h, int nw, int nh)
		{
			var screen = new IosScreenSnapshot(new Size(w, h), new Size(nw, nh), new IosSafeAreaInsets(47, 0, 47, 21));
			var layout = new ServerInformationLayout(screen);
			var safe = screen.SafeBounds;
			Assert.That(layout.Dialog.Width, Is.LessThan(safe.Width));
			Assert.That(layout.Dialog.Height, Is.LessThan(safe.Height));
			Assert.That(layout.Dialog.X - safe.X, Is.EqualTo((safe.Width - layout.Dialog.Width) / 2));
			Assert.That(layout.Dialog.Y - safe.Y, Is.EqualTo((safe.Height - layout.Dialog.Height) / 2));
			Assert.That(layout.Title.Bottom, Is.LessThan(layout.Content.Top));
			Assert.That(layout.Content.Bottom, Is.LessThan(layout.Close.Top));
			Assert.That(layout.Close.Bottom, Is.LessThan(layout.Dialog.Height * 97 / 100));
			Assert.That(layout.Close.Height, Is.GreaterThanOrEqualTo(screen.LogicalPoints(44)));
			foreach (var rect in new[] { layout.Title, layout.Content, layout.Close })
			{
				Assert.That(rect.Left, Is.GreaterThan(0));
				Assert.That(rect.Right, Is.LessThan(layout.Dialog.Width));
			}
		}
	}
}
