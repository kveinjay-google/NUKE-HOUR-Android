using System;
using NUnit.Framework;
using OpenRA.Mods.Common.Widgets.Logic;
using OpenRA.Primitives;

namespace OpenRA.Test
{
	[TestFixture]
	public class PhoneCompactSurfaceTest
	{
		[Test]
		public void PhoneSettingsOnlyReserveTwoPointOuterBorder()
		{
			var layout = IosSettingsLayout.ForScreen(true, new Size(956, 440),
				new Size(956, 440), new IosSafeAreaInsets(62, 0, 62, 21));
			Assert.That(layout.Window.Left - layout.SafeBounds.Left, Is.EqualTo(2));
			Assert.That(layout.SafeBounds.Bottom - layout.Window.Bottom, Is.EqualTo(2));
		}

		[TestCase(956, 440, true)]
		[TestCase(1180, 820, false)]
		public void CompactContentExpandsOnlyPhoneSafeArea(int width, int height, bool phone)
		{
			var screen = new IosScreenSnapshot(new Size(width, height), new Size(width, height), default);
			var method = typeof(MultiplayerScreenLayout).GetMethod("ContentBounds", new[]
				{ typeof(IosScreenSnapshot), typeof(int), typeof(int), typeof(bool) });
			Assert.That(method, Is.Not.Null);
			var compact = (OpenRA.Widgets.WidgetBounds)method!.Invoke(null, new object[] { screen, 0, 0, true });
			var original = MultiplayerScreenLayout.ContentBounds(screen);
			Assert.That(compact.X, Is.EqualTo(phone ? 2 : original.X));
			Assert.That(compact.Width, Is.EqualTo(phone ? width - 4 : original.Width));
		}
	}
}
