using System;
using NUnit.Framework;
using OpenRA.Mods.Common.Widgets.Logic;
using OpenRA.Mods.RA2.Widgets;
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Test
{
    [TestFixture]
    public class PhoneHubArtworkLayoutTest
    {
        [TestCase(667, 375)]
        [TestCase(1558, 720)]
        [TestCase(2400, 1080)]
        public void BadgesUseTheSameCropAsThePaintedCardPlates(int width, int height)
        {
            var size = new Size(width, height);
            var snapshot = new IosScreenSnapshot(size, new Size(844, 390), default);
            var content = new WidgetBounds(17, 2, width - 34, height - 4);
            var slices = StretchBackgroundWidget.CalculatePhoneFrameSlices(new Rectangle(0, 0, 1850, 850), new Rectangle(0, 0, width, height), 0, 120, 28);
            var target = slices[4].Target;
            var crop = StretchBackgroundWidget.CalculateAspectFillCrop(slices[4].Source, target.Size);
            foreach (var y in new[] { 354, 699 })
            {
                var badge = RankedPanelLayout.PhoneHubArtworkBounds(new WidgetBounds(142, y, 80, 80), snapshot, content);
                var text = RankedPanelLayout.PhoneHubArtworkBounds(new WidgetBounds(262, y, 580, 80), snapshot, content);
                var expectedTop = target.Y + (int)Math.Round((y - crop.Y) * target.Height / (double)crop.Height);
                Assert.That(badge.Y + content.Y, Is.EqualTo(expectedTop));
                Assert.That(text.Y, Is.EqualTo(badge.Y));
                Assert.That(text.Height, Is.EqualTo(badge.Height));
                Assert.That(badge.Right, Is.LessThan(text.X));
                Assert.That(text.Bottom + content.Y, Is.LessThanOrEqualTo(height));
            }
        }
    }
}
