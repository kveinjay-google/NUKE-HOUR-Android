using System;

namespace OpenRA.Mods.Common.LoadScreens
{
	// Cosmetic only: never consume synchronized gameplay randomness.
	public static class SplashWallpaperSelection
	{
		public static int SelectIndex(int count, int previous, Func<int, int> draw)
		{
			if (count < 1) throw new ArgumentOutOfRangeException(nameof(count));
			if (count == 1) return 0;
			if (previous < 0 || previous >= count) return draw(count);
			var selected = draw(count - 1);
			return selected < previous ? selected : selected + 1;
		}
	}
}
