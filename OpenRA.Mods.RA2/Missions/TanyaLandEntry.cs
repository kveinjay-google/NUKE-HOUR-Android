using System;
using System.Collections.Generic;

namespace OpenRA.Mods.RA2.Missions
{
	public static class TanyaLandEntry
	{
		// A bounded land-only flood fill chooses the reachable shore nearest the
		// retail waypoint (which may itself be water). Return engine path order.
		public static List<CPos> FindPath(CPos start, CPos desired, Func<CPos, bool> canWalk)
		{
			var parents = new Dictionary<CPos, CPos> { [start] = start };
			var queue = new Queue<CPos>();
			queue.Enqueue(start);
			var best = start;
			while (queue.Count > 0)
			{
				var current = queue.Dequeue();
				if ((current - desired).LengthSquared < (best - desired).LengthSquared)
					best = current;
				foreach (var step in CVec.Directions)
				{
					// Cardinal steps cannot cut diagonally across a water/cliff corner.
					if (step.X != 0 && step.Y != 0)
						continue;
					var next = current + step;
					if (parents.ContainsKey(next) || !canWalk(next))
						continue;
					parents.Add(next, current);
					queue.Enqueue(next);
				}
			}

			var path = new List<CPos>();
			for (var cell = best; cell != start; cell = parents[cell])
				path.Add(cell);
			return path;
		}
	}
}
