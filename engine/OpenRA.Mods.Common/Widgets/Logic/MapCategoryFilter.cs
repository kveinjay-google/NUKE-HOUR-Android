using System;
using System.Linq;

namespace OpenRA.Mods.Common.Widgets.Logic
{
	public static class MapCategoryFilter
	{
		public static readonly string[] Keys = { "all", "players-2", "players-3-4", "players-5-6", "players-7plus", "naval", "snow", "temperate" };

		// Older bundled Westwood maps predate descriptive category tags. Keep
		// this small curated compatibility list outside map packages: adding a
		// category to map.yaml would change network map identity.
		public static bool IsKnownNaval(string title, string author) =>
			string.Equals(author, "Westwood Studios", StringComparison.OrdinalIgnoreCase) &&
			new[] { "South Pacific", "Tsunami", "Bering Strait" }.Contains(title, StringComparer.OrdinalIgnoreCase);

		public static bool Matches(string key, int players, string tileset, string[] tags) => key switch
		{
			"players-2" => players == 2,
			"players-3-4" => players >= 3 && players <= 4,
			"players-5-6" => players >= 5 && players <= 6,
			"players-7plus" => players >= 7,
			"naval" => (tags ?? Array.Empty<string>()).Any(t =>
				string.Equals(t, "Naval", StringComparison.OrdinalIgnoreCase) || t == "海军" || t == "海战"),
			"snow" => string.Equals(tileset, "SNOW", StringComparison.OrdinalIgnoreCase),
			"temperate" => string.Equals(tileset, "TEMPERATE", StringComparison.OrdinalIgnoreCase),
			_ => true,
		};
	}
}
