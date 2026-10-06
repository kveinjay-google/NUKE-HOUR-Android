using NUnit.Framework;

namespace OpenRA.Test
{
	[TestFixture]
	public class AiDifficultyLobbyPolicyTest
	{
		[Test]
		public void OfficialBotsCannotBeOverriddenByClientPayload()
		{
			var custom = AiDifficultyCatalog.CreateCustom("nightmare");
			Assert.That(AiDifficultyLobbyPolicy.TrySnapshot("difficulty-normal", AiDifficultyCatalog.Encode(custom), out _, out _), Is.False);
			Assert.That(AiDifficultyLobbyPolicy.TrySnapshot("difficulty-nightmare", "", out var snapshot, out var name), Is.True);
			Assert.That(name, Is.Null);
			Assert.That(AiDifficultyCatalog.TryDecode(snapshot, out var p), Is.True);
			Assert.That(p.IncomePercent, Is.EqualTo(200));
		}

		[Test]
		public void CustomSnapshotsAreIndependentAndInvalidPayloadsRejected()
		{
			var p = AiDifficultyCatalog.CreateCustom("brutal");
			p.Name = "My AI";
			Assert.That(AiDifficultyLobbyPolicy.TrySnapshot(AiDifficultyCatalog.CustomBotType, AiDifficultyCatalog.Encode(p), out var snapshot, out var name), Is.True);
			p.Name = "Changed locally";
			Assert.That(name, Is.EqualTo("My AI"));
			Assert.That(AiDifficultyCatalog.TryDecode(snapshot, out var stored), Is.True);
			Assert.That(stored.Name, Is.EqualTo("My AI"));
			Assert.That(AiDifficultyLobbyPolicy.TrySnapshot(AiDifficultyCatalog.CustomBotType, "garbage", out _, out _), Is.False);
			Assert.That(AiDifficultyLobbyPolicy.TrySnapshot("test", snapshot, out _, out _), Is.False);
			Assert.That(AiDifficultyLobbyPolicy.TrySnapshot("test", "", out _, out _), Is.True);
		}
	}
}
