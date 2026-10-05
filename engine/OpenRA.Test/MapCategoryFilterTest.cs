using NUnit.Framework;
using OpenRA.Mods.Common.Widgets.Logic;

namespace OpenRA.Test
{
	[TestFixture]
	public class MapCategoryFilterTest
	{
		[Test]
		public void NavalFallbackIsAnExplicitListNotANameSubstringGuess()
		{
			Assert.That(MapCategoryFilter.IsKnownNaval("South Pacific", "Westwood Studios"), Is.True);
			Assert.That(MapCategoryFilter.IsKnownNaval("Tsunami", "Westwood Studios"), Is.True);
			Assert.That(MapCategoryFilter.IsKnownNaval("Bering Strait", "Westwood Studios"), Is.True);
			Assert.That(MapCategoryFilter.IsKnownNaval("Ocean Lake", "Custom"), Is.False);
			Assert.That(MapCategoryFilter.IsKnownNaval("Tsunami", "Custom"), Is.False);
		}

		[Test]
		public void FilterUsesPlayerCountTerrainAndExplicitNavalMetadata()
		{
			var type = typeof(MapChooserLogic).Assembly.GetType("OpenRA.Mods.Common.Widgets.Logic.MapCategoryFilter");
			Assert.That(type, Is.Not.Null);
			var method = type!.GetMethod("Matches");
			bool Match(string key, int players, string tile, params string[] tags) =>
				(bool)method!.Invoke(null, new object[] { key, players, tile, tags });
			Assert.That(Match("players-2", 2, "TEMPERATE"), Is.True);
			Assert.That(Match("players-2", 4, "TEMPERATE"), Is.False);
			Assert.That(Match("players-7plus", 8, "SNOW"), Is.True);
			Assert.That(Match("snow", 4, "SNOW"), Is.True);
			Assert.That(Match("naval", 4, "TEMPERATE", "Naval"), Is.True);
			Assert.That(Match("naval", 4, "TEMPERATE", "Conquest"), Is.False);
			Assert.That(Match("all", 4, "TEMPERATE"), Is.True);
		}
	}
}
