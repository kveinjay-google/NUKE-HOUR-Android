#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Common.Activities;

namespace OpenRA.Test
{
	[TestFixture]
	public class VerticalBombingPolicyTest
	{
		[TestCase(50, 25, 1706, 600)]
		[TestCase(10, 25, 1706, 100)]
		[TestCase(2, 25, 1706, 0)]
		[TestCase(50, 0, 1706, 0)]
		[TestCase(50, 25, 200, 200)]
		public void RetreatReservesTimeToReturnBeforeReload(int reload, int speed, int separation, int expected)
		{
			var distance = VerticalBombingPolicy.RetreatDistance(reload, speed, separation);
			Assert.That(distance, Is.EqualTo(expected));
			if (speed > 0)
				Assert.That(2 * ((distance + speed - 1) / speed) + 2, Is.LessThanOrEqualTo(Math.Max(2, reload)));
		}
		[Test]
		public void OnlyKirovEnablesCoordinatedBombing()
		{
			var root = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
			while (root != null && !Directory.Exists(Path.Combine(root.FullName, "OpenRA.Mods.RA2")))
				root = root.Parent;
			Assert.That(root, Is.Not.Null);
			var actors = MiniYaml.FromFile(Path.Combine(root.FullName, "mods/ra2/rules/aircraft.yaml"));
			var enabled = actors.Where(actor => actor.Value.Nodes.Any(trait => trait.Key == "AttackFrontal"
				&& trait.Value.Nodes.Any(field => field.Key == "CoordinatedVerticalBombing" && field.Value.Value == "true")));
			Assert.That(enabled.Select(actor => actor.Key), Is.EqualTo(new[] { "zep" }));
			Assert.That(new AttackFrontalInfo().CoordinatedVerticalBombing, Is.False);
		}

		[TestCase(false, 50, true, false)]
		[TestCase(true, 50, false, false)]
		[TestCase(true, 2, true, false)]
		[TestCase(true, 50, true, true)]
		public void YieldOnlyForReadyAllyAfterCompleteBurst(bool ally, int cooldown, bool complete, bool expected)
			=> Assert.That(VerticalBombingPolicy.CanYield(ally, cooldown, complete), Is.EqualTo(expected));

		[TestCase(26, 600, 25, true)]
		[TestCase(27, 600, 25, false)]
		[TestCase(50, 600, 0, true)]
		public void StartReturnBeforeWeaponReady(int cooldown, int distance, int speed, bool expected)
			=> Assert.That(VerticalBombingPolicy.ShouldReturn(cooldown, distance, speed), Is.EqualTo(expected));

		[TestCase(2)]
		[TestCase(4)]
		[TestCase(8)]
		public void GroupYieldingPreservesEachBombersReloadCadence(int count)
		{
			// A staggered group with ready allies exercises a complete 50-tick cooldown.
			// Integrate the production travel budget at 25 units/tick and require every
			// bomber to be back in the drop zone on every scheduled firing tick.
			for (var bomber = 0; bomber < count; bomber++)
			{
				var nextShot = bomber * 50 / count;
				var shots = 0;
				var distance = 0;
				var parking = 0;
				var returning = false;
				for (var tick = nextShot; tick < 500; tick++)
				{
					var cooldown = nextShot - tick;
					if (cooldown == 0)
					{
						Assert.That(distance, Is.Zero, $"Bomber {bomber} missed firing tick {tick}");
						shots++;
						nextShot += 50;
						parking = VerticalBombingPolicy.RetreatDistance(50, 25, 1706);
						returning = false;
					}
					else
					{
						returning |= VerticalBombingPolicy.ShouldReturn(cooldown, distance, 25);
						distance = returning ? Math.Max(0, distance - 25) : Math.Min(parking, distance + 25);
					}
				}

				Assert.That(shots, Is.EqualTo(10));
			}
		}

	}
}
