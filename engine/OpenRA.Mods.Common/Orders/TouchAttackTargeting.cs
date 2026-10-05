using System;
using System.Collections.Generic;
using OpenRA.Primitives;

namespace OpenRA.Mods.Common.Orders
{
	public static class TouchAttackTargeting
	{
		public static int NearestIndex(IReadOnlyList<Rectangle> bounds, Func<int, bool> eligible, int2 point, int radius)
		{
			var best = -1;
			var bestDistance = (long)radius * radius + 1;
			for (var i = 0; i < bounds.Count; i++)
			{
				var b = bounds[i];
				var dx = Math.Max(0, Math.Max(b.Left - point.X, point.X - b.Right));
				var dy = Math.Max(0, Math.Max(b.Top - point.Y, point.Y - b.Bottom));
				var distance = (long)dx * dx + (long)dy * dy;
				if (distance >= bestDistance || !eligible(i)) continue;
				best = i;
				bestDistance = distance;
			}
			return best;
		}

		public static bool ShouldAssist(bool isTouch, bool isIos, Modifiers modifiers, int selectedCount) =>
			isTouch && isIos && selectedCount > 0 &&
			!modifiers.HasModifier(Modifiers.Alt) && !modifiers.HasModifier(Modifiers.Ctrl);
	}
}
