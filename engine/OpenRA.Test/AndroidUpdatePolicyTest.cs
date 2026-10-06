using System;
using NUnit.Framework;
using OpenRA.Mods.RA2.Content;
namespace OpenRA.Test
{
    [TestFixture]
    public sealed class AndroidUpdatePolicyTest
    {
        [TestCase(0, 0, true)]
        [TestCase(6, 2, false)]
        [TestCase(7, 2, true)]
        [TestCase(8, 0, false)]
        [TestCase(8, 1, true)]
        public void ChecksWeeklyWithDailyFailureBackoff(int successDays, int attemptDays, bool expected)
        {
            const long now = 2000000000;
            var success = successDays == 0 ? 0 : now - successDays * 86400;
            var attempt = successDays == 0 ? 0 : now - attemptDays * 86400;
            Assert.That(AndroidUpdatePolicy.IsDue(now, success, attempt), Is.EqualTo(expected));
        }
        [TestCase(31, false)]
        [TestCase(32, true)]
        public void OnlyNewerBuildsAreUpdates(long build, bool expected)
        {
            var json = "{\"schema\":1,\"packageId\":\"com.openra.android.personal\",\"channel\":\"stable\",\"build\":" + build + ",\"version\":\"0.0.32\",\"notes\":\"更新说明\"}";
            Assert.That(AndroidUpdatePolicy.Parse(json, 31) != null, Is.EqualTo(expected));
        }
        [TestCase("{}")]
        [TestCase("{\"schema\":2,\"build\":99}")]
        [TestCase("not json")]
        public void MalformedMetadataDoesNotProduceAPrompt(string json)
        {
            Assert.That(AndroidUpdatePolicy.Parse(json, 31), Is.Null);
        }
    }
}
