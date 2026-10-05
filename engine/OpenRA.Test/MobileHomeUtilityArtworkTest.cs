using NUnit.Framework;
using OpenRA.Mods.Common.Widgets.Logic;
using OpenRA.Mods.RA2.Widgets;
using OpenRA.Primitives;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class MobileHomeUtilityArtworkTest
	{
		[TestCase("SETTINGS_BUTTON", 0)]
		[TestCase("THANKS_BUTTON", 1)]
		[TestCase("ABOUT_BUTTON", 2)]
		[TestCase("QUIT_BUTTON", 3)]
		public void PhonePaintRemainsOnPhysicalShellSlotsWhenInputIsClipped(string id, int slot)
		{
			var viewport = new Size(2400, 1080);
			foreach (var inset in new[] { 0, 50, 130 })
			{
				var policy = IosMenuLayoutPolicy.Create(true,
					new IosScreenSnapshot(viewport, new Size(1200, 540), new IosSafeAreaInsets(inset, 0, inset, 0)));
				var layout = IosMainMenuLayout.Create(policy);
				var hit = layout.Controls[slot + 3].ToRectangle();
				var paint = MacHomeUtilityButtonWidget.CalculateArtworkBounds(hit, policy, id);
				Assert.That(policy.IsPhone, Is.True);
				Assert.That(paint, Is.EqualTo(new Rectangle(60 + slot * 571, 914, 566, 103)));
				Assert.That(layout.Controls[slot + 3].ToRectangle(), Is.EqualTo(hit), "Paint must not alter input bounds.");
			}
		}

		[TestCase(false, 1200, 540)]
		[TestCase(true, 1024, 768)]
		public void TabletAndDesktopPaintRetainsExistingBounds(bool mobile, int nativeWidth, int nativeHeight)
		{
			var policy = IosMenuLayoutPolicy.Create(mobile,
				new IosScreenSnapshot(new Size(2400, 1080), new Size(nativeWidth, nativeHeight), default));
			var hit = new Rectangle(100, 200, 500, 100);
			Assert.That(MacHomeUtilityButtonWidget.CalculateArtworkBounds(hit, policy, "SETTINGS_BUTTON"), Is.EqualTo(hit));
		}
	}
}
