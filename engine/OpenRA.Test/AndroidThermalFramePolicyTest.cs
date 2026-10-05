using NUnit.Framework;
using OpenRA.Support;
namespace OpenRA.Test
{
    [TestFixture]
    public class AndroidThermalFramePolicyTest
    {
        [Test]
        public void EscalatesImmediatelyAndRequiresStableCoolingBeforeRecovery()
        {
            var policy = new AndroidThermalFramePolicy();
            Assert.That(policy.Update(0, 0), Is.Zero);
            Assert.That(policy.Update(2, 1000), Is.EqualTo(60));
            Assert.That(policy.Update(3, 2000), Is.EqualTo(30));
            Assert.That(policy.Update(0, 3000), Is.EqualTo(30));
            Assert.That(policy.Update(0, 62999), Is.EqualTo(30));
            Assert.That(policy.Update(0, 63000), Is.Zero);
        }
        [Test]
        public void OscillatingTemperatureDoesNotRepeatedlyRaiseFrameRate()
        {
            var policy = new AndroidThermalFramePolicy();
            policy.Update(3, 0); policy.Update(0, 1000);
            policy.Update(3, 59000); policy.Update(0, 60000);
            Assert.That(policy.Update(0, 119999), Is.EqualTo(30));
            Assert.That(policy.Update(0, 120000), Is.Zero);
        }
        [Test]
        public void UnknownOrInvalidSensorReadingsDoNotReleaseAnExistingCap()
        {
            var policy = new AndroidThermalFramePolicy();
            Assert.That(policy.Update(-1, 0), Is.Zero);
            policy.Update(4, 1); policy.Update(0, 2);
            Assert.That(policy.Update(int.MaxValue, 70000), Is.EqualTo(20));
            Assert.That(policy.Update(-1, 80000), Is.EqualTo(20));
            Assert.That(policy.Update(0, 90000), Is.EqualTo(20));
        }
        [TestCase(20, 20)]
        [TestCase(20, 30)]
        [TestCase(40, 20)]
        public void FastSimulationDoesNotForceExtraFramesOrWaitForThermalRendering(int logicInterval, int cap)
        {
            long nextLogic = 0, nextRender = 0, forcedRender = 0;
            var renderBeforeNextTick = true; // A pending guard from before thermal activation.
            var ticks = 0; var renders = 0;
            var renderInterval = AndroidThermalFramePolicy.LimitRenderInterval(1, cap);
            // Drive the loop's deadline/guard contract through 4 seconds of regular gameplay.
            for (var now = 0; now < 4000; now++)
            {
                renderBeforeNextTick = AndroidThermalFramePolicy.RequiresRenderBeforeLogic(renderBeforeNextTick, cap);
                if (now < System.Math.Min(nextLogic, nextRender)) continue;
                var force = renderBeforeNextTick || now >= forcedRender;
                if (now >= nextLogic && !renderBeforeNextTick)
                {
                    ticks++;
                    nextLogic += logicInterval;
                    renderBeforeNextTick = AndroidThermalFramePolicy.RequiresRenderBeforeLogic(true, cap);
                }
                if ((now >= nextRender && now < nextLogic) || force)
                {
                    renders++;
                    nextRender = now + renderInterval;
                    forcedRender = now + System.Math.Max(100, renderInterval);
                    renderBeforeNextTick = false;
                }
            }
            Assert.That(ticks, Is.EqualTo(4000 / logicInterval), "Thermal rendering must never stall simulation.");
            Assert.That(renders, Is.LessThanOrEqualTo(cap * 4), "Forced per-tick rendering must respect the cap.");
        }

        [Test]
        public void OrdinaryLoopPreservesTheExistingRenderBeforeTickGuard()
        {
            Assert.That(AndroidThermalFramePolicy.RequiresRenderBeforeLogic(true, 0), Is.True);
            Assert.That(AndroidThermalFramePolicy.RequiresRenderBeforeLogic(false, 0), Is.False);
        }

        [TestCase(16, 30, 34)]
        [TestCase(100, 30, 100)]
        [TestCase(40, 60, 40)]
        [TestCase(8, 0, 8)]
        [TestCase(8, -1, 8)]
        [TestCase(8, int.MaxValue, 8)]
        public void ThermalLimitOnlySlowsRenderingAndPreservesStricterUserCap(int interval, int cap, int expected)
        {
            Assert.That(AndroidThermalFramePolicy.LimitRenderInterval(interval, cap), Is.EqualTo(expected));
        }
    }
}
