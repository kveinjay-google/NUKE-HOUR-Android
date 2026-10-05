using NUnit.Framework;

namespace OpenRA.Test
{
	[TestFixture]
	public class GameSaveCatchUpPolicyTest
	{
		[TestCase(true, 1, 0, true)]
		[TestCase(true, 63, 7, true)]
		[TestCase(true, 64, 0, false)]
		[TestCase(true, 1, 8, false)]
		[TestCase(false, 1, 0, false)]
		public void CatchUpIsBoundedAndOnlyRunsWhileLoading(bool loading, int iterations, long elapsed, bool expected)
		{
			var type = typeof(Game).Assembly.GetType("OpenRA.GameSaveCatchUpPolicy");
			Assert.That(type, Is.Not.Null, "External-loop save restoration needs a bounded catch-up policy.");
			Assert.That(type.GetMethod("ShouldContinue").Invoke(null, new object[] { loading, iterations, elapsed }), Is.EqualTo(expected));
		}
	}
}
