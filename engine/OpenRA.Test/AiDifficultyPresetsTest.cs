using System.Linq;
using NUnit.Framework;

namespace OpenRA.Test
{
	[TestFixture]
	public class AiDifficultyPresetsTest
	{
		[Test]
		public void EditingCopyDoesNotChangeOfficialOrSavedSnapshot()
		{
			var presets = AiDifficultyPresets.Load(null);
			var draft = AiDifficultyCatalog.CreateCustom("nightmare");
			draft.Name = "My challenge";
			Assert.That(AiDifficultyPresets.Save(presets, draft), Is.True);
			var snapshot = AiDifficultyCatalog.Encode(presets[0]);
			draft.IncomePercent = 100;
			Assert.That(presets[0].IncomePercent, Is.EqualTo(200));
			Assert.That(AiDifficultyCatalog.GetOfficial("nightmare").IncomePercent, Is.EqualTo(200));
			Assert.That(AiDifficultyCatalog.Encode(presets[0]), Is.EqualTo(snapshot));
		}

		[Test]
		public void MalformedCustomIdCannotAddAnOfficialEntryToLibrary()
		{
			var profiles = AiDifficultyPresets.Load(null);
			var profile = new AiDifficultyProfile { Id = "custom-not-a-guid", Name = "Invalid" };
			Assert.That(AiDifficultyPresets.Save(profiles, profile), Is.False);
			Assert.That(profiles, Is.Empty);
		}

		[Test]
		public void LibraryRoundTripSupportsRenameAndRejectsDuplicateNames()
		{
			var presets = AiDifficultyPresets.Load(null);
			var first = AiDifficultyCatalog.CreateCustom("brutal");
			first.Name = "Named AI";
			Assert.That(AiDifficultyPresets.Save(presets, first), Is.True);
			var other = AiDifficultyCatalog.CreateCustom("easy");
			other.Name = "Named AI";
			Assert.That(AiDifficultyPresets.Save(presets, other), Is.False);
			first.Name = "Renamed AI";
			Assert.That(AiDifficultyPresets.Save(presets, first), Is.True);
			var restored = AiDifficultyPresets.Load(AiDifficultyPresets.Encode(presets));
			Assert.That(restored.Single().Name, Is.EqualTo("Renamed AI"));
			Assert.That(AiDifficultyPresets.Load(new[] { "broken", AiDifficultyCatalog.Encode(first), AiDifficultyCatalog.Encode(first) }).Count, Is.EqualTo(1));
			Assert.That(AiDifficultyPresets.Save(presets, AiDifficultyCatalog.GetOfficial("normal")), Is.False);
		}
	}
}
