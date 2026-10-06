using System;
using System.Linq;
using NUnit.Framework;
using OpenRA.Network;

namespace OpenRA.Test
{
	[TestFixture]
	public class AiDifficultyProfileTest
	{
		[Test]
		public void OfficialTiersAreOrderedIndependentAndCannotBeOverridden()
		{
			Assert.That(AiDifficultyCatalog.OfficialProfiles.Select(p => p.Id), Is.EqualTo(new[]
				{ "beginner", "easy", "normal", "hard", "brutal", "expert", "master", "nightmare" }));
			var nightmare = AiDifficultyCatalog.GetOfficial("nightmare");
			Assert.That(nightmare.IncomePercent, Is.EqualTo(200));
			Assert.That(nightmare.ProductionSpeedPercent, Is.EqualTo(150));
			nightmare.IncomePercent = 300;
			Assert.That(AiDifficultyCatalog.Normalize(nightmare).IncomePercent, Is.EqualTo(200));
			Assert.That(AiDifficultyCatalog.GetOfficial("nightmare").IncomePercent, Is.EqualTo(200));
			Assert.That(AiDifficultyCatalog.OfficialProfiles.Take(7).All(p => p.IncomePercent == 100 && p.ProductionSpeedPercent == 100), Is.True);
			Assert.That(AiDifficultyCatalog.GetOfficial("missing").Id, Is.EqualTo("normal"));
		}

		[Test]
		public void CustomCopiesHaveStableIndependentIdsAndBoundedSanitizedValues()
		{
			var custom = AiDifficultyCatalog.CreateCustom("hard");
			Assert.That(custom.Id, Is.Not.EqualTo(AiDifficultyCatalog.CreateCustom("hard").Id));
			custom.Name = "  { $name }\n\u202e" + new string('中', 40);
			custom.Style = "bad";
			custom.AttackIntervalSeconds = int.MinValue;
			custom.WaveSize = int.MaxValue;
			custom.Expansion = -1;
			custom.IncomePercent = 0;
			custom.ProductionSpeedPercent = int.MaxValue;
			var normalized = AiDifficultyCatalog.Normalize(custom);
			Assert.That(normalized.Id, Is.EqualTo(custom.Id));
			Assert.That(normalized.Name.Length, Is.LessThanOrEqualTo(24));
			Assert.That(normalized.Name.IndexOfAny(new[] { '{', '}', '$', '\n', '\u202e' }), Is.EqualTo(-1));
			Assert.That(normalized.Style, Is.EqualTo("balanced"));
			Assert.That(normalized.AttackIntervalSeconds, Is.EqualTo(2));
			Assert.That(normalized.WaveSize, Is.EqualTo(60));
			Assert.That(normalized.Expansion, Is.EqualTo(1));
			Assert.That(normalized.IncomePercent, Is.EqualTo(100));
			Assert.That(normalized.ProductionSpeedPercent, Is.EqualTo(200));
		}

		[Test]
		public void SynchronizedPayloadRoundTripsAndRejectsMalformedOrOversizeData()
		{
			var custom = AiDifficultyCatalog.CreateCustom("nightmare");
			custom.Name = "我的电脑";
			custom.IncomePercent = 125;
			var encoded = AiDifficultyCatalog.Encode(custom);
			Assert.That(encoded.Any(char.IsWhiteSpace), Is.False);
			Assert.That(AiDifficultyCatalog.TryDecode(encoded, out var decoded), Is.True);
			Assert.That(decoded.Id, Is.EqualTo(custom.Id));
			Assert.That(decoded.Name, Is.EqualTo(custom.Name));
			Assert.That(decoded.IncomePercent, Is.EqualTo(125));
			foreach (var bad in new[] { null, "", "!", new string('A', 4097), Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("null")) })
				Assert.That(AiDifficultyCatalog.TryDecode(bad, out _), Is.False);
			Assert.That(AiDifficultyCatalog.Resolve(null, encoded), Is.Null);
			Assert.That(AiDifficultyCatalog.Resolve("profbalanced", encoded), Is.Null);
			Assert.That(AiDifficultyCatalog.Resolve("difficulty-normal", encoded).IncomePercent, Is.EqualTo(100));
			Assert.That(AiDifficultyCatalog.Resolve(AiDifficultyCatalog.CustomBotType, encoded).IncomePercent, Is.EqualTo(125));
		}

		[Test]
		public void SessionSerializationPreservesIndependentBotSnapshots()
		{
			var session = new Session();
			session.Clients.Add(new Session.Client { Index = 1, Bot = "difficulty-nightmare", BotDifficulty = AiDifficultyCatalog.Encode(AiDifficultyCatalog.GetOfficial("nightmare")) });
			session.Clients.Add(new Session.Client { Index = 2, Bot = "difficulty-normal", BotDifficulty = AiDifficultyCatalog.Encode(AiDifficultyCatalog.GetOfficial("normal")) });
			var restored = Session.Deserialize(session.Serialize(), "test");
			Assert.That(AiDifficultyCatalog.Resolve(restored.Clients[0].Bot, restored.Clients[0].BotDifficulty).IncomePercent, Is.EqualTo(200));
			Assert.That(AiDifficultyCatalog.Resolve(restored.Clients[1].Bot, restored.Clients[1].BotDifficulty).IncomePercent, Is.EqualTo(100));
		}

		[Test]
		public void SavedSlotRestoresCustomSnapshotWithoutLocalSettings()
		{
			var custom = AiDifficultyCatalog.CreateCustom("nightmare");
			custom.Name = "保存的电脑";
			custom.IncomePercent = 123;
			var before = new Session.Client { Bot = AiDifficultyCatalog.CustomBotType, BotDifficulty = AiDifficultyCatalog.Encode(custom) };
			var saved = new SlotClient(before);
			var restored = SlotClient.Deserialize(saved.Serialize("Multi0").Value);
			var after = new Session.Client();
			restored.ApplyTo(after);
			Assert.That(after.BotDifficulty, Is.EqualTo(before.BotDifficulty));
			Assert.That(AiDifficultyCatalog.Resolve(after.Bot, after.BotDifficulty).IncomePercent, Is.EqualTo(123));
		}

		[Test]
		public void RuntimeScalingUsesInverseProductionTimeAndOnlyPositiveMinedIncome()
		{
			var nightmare = AiDifficultyCatalog.GetOfficial("nightmare");
			Assert.That(AiDifficultyRuntime.ProductionTime(90, nightmare), Is.EqualTo(60));
			Assert.That(AiDifficultyRuntime.ProductionTime(1, nightmare), Is.EqualTo(1));
			Assert.That(AiDifficultyRuntime.ProductionTime(90, null), Is.EqualTo(90));
			Assert.That(AiDifficultyRuntime.MinedIncome(50, nightmare), Is.EqualTo(100));
			Assert.That(AiDifficultyRuntime.MinedIncome(-50, nightmare), Is.EqualTo(-50));
			Assert.That(AiDifficultyRuntime.MinedIncome(int.MaxValue, nightmare), Is.EqualTo(int.MaxValue));
			Assert.That(AiDifficultyRuntime.MinedIncome(50, null), Is.EqualTo(50));
		}

		[Test]
		public void TiersAndStylesChangeDecisionsWithoutRemovingCpuFloor()
		{
			var tiers = AiDifficultyCatalog.OfficialProfiles;
			Assert.That(tiers.Select(p => AiDifficultyRuntime.DecisionInterval(p, 30)).Distinct().Count(), Is.EqualTo(8));
			Assert.That(tiers.All(p => AiDifficultyRuntime.DecisionInterval(p, 30) >= 5), Is.True);
			Assert.That(AiDifficultyRuntime.UnitShare(tiers[7], 10, 1500), Is.GreaterThan(AiDifficultyRuntime.UnitShare(tiers[0], 10, 1500)));
			var custom = AiDifficultyCatalog.CreateCustom("normal");
			custom.Style = "defensive";
			var defensive = AiDifficultyRuntime.AttackIntervalTicks(custom, 40, 1);
			Assert.That(AiDifficultyRuntime.DefenseReserve(custom), Is.GreaterThan(0));
			custom.Style = "rush";
			Assert.That(AiDifficultyRuntime.AttackIntervalTicks(custom, 40, 1), Is.LessThan(defensive));
			Assert.That(AiDifficultyRuntime.DecisionInterval(null, 30), Is.EqualTo(30));
		}
	}
}
