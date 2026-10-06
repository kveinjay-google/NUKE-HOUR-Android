using NUnit.Framework;
using OpenRA.Mods.RA2.Content;

namespace OpenRA.Test
{
    [TestFixture]
    public sealed class ImportProgressEstimateTest
    {
        [TestCase(0, 100, 5, null)]
        [TestCase(10, 0, 5, null)]
        [TestCase(10, 100, 0.5, null)]
        [TestCase(25, 100, 10, 30d)]
        [TestCase(100, 100, 10, 0d)]
        [TestCase(110, 100, 10, 0d)]
        public void RemainingUsesMeasuredWorkAndWaitsForSamples(long done, long total, double seconds, double? expected)
        {
            Assert.That(ImportProgressEstimate.RemainingSeconds(done, total, seconds), Is.EqualTo(expected));
        }
    }
}
