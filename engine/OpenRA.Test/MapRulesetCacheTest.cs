using System;
using NUnit.Framework;
namespace OpenRA.Test
{
    [TestFixture]
    public class MapRulesetCacheTest
    {
        [Test]
        public void ReusesValidationUntilMapSnapshotChangesAndRetriesFailures()
        {
            var cache = new MapRulesetCache();
            var snapshot = new object();
            var loads = 0;
            cache.Get(snapshot, () => { loads++; return null; });
            cache.Get(snapshot, () => { loads++; return null; });
            Assert.That(loads, Is.EqualTo(1));
            snapshot = new object();
            Assert.Throws<InvalidOperationException>(() => cache.Get(snapshot, () => throw new InvalidOperationException()));
            cache.Get(snapshot, () => { loads++; return null; });
            Assert.That(loads, Is.EqualTo(2));
        }
    }
}
