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
using System.Collections.Generic;
using OpenRA.GameRules;
using OpenRA.Mods.Common;
using OpenRA.Mods.Common.Activities;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Common.Warheads;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.RA2.Warheads
{
	[Desc("Spawns a formation of scripted bombers that attack the impact position and leave the map.")]
	public sealed class SendAirstrikeWarhead : Warhead, IRulesetLoaded<WeaponInfo>
	{
		[ActorReference(typeof(AircraftInfo))]
		[FieldLoader.Require]
		[Desc("Aircraft used to deliver the airstrike.")]
		public readonly string UnitType = null;

		[Desc("Number of aircraft in the formation.")]
		public readonly int SquadSize = 1;

		[Desc("Offset vector between aircraft in the formation.")]
		public readonly WVec SquadOffset = new(-1536, 1536, 0);

		[Desc("Number of possible random approach facings.")]
		public readonly int QuantizedFacings = 32;

		[Desc("Additional distance beyond the map edge used for spawning and removal.")]
		public readonly WDist Cordon = new(5120);

		public void RulesetLoaded(Ruleset rules, WeaponInfo info)
		{
			if (SquadSize <= 0)
				throw new YamlException($"{nameof(SquadSize)} must be greater than zero.");

			if (QuantizedFacings <= 0)
				throw new YamlException($"{nameof(QuantizedFacings)} must be greater than zero.");

			if (!rules.Actors.TryGetValue(UnitType.ToLowerInvariant(), out var aircraft))
				throw new YamlException($"Actors Ruleset does not contain an entry '{UnitType.ToLowerInvariant()}'.");

			if (!aircraft.HasTraitInfo<AircraftInfo>())
				throw new YamlException($"Airstrike actor '{UnitType}' requires Aircraft.");

			if (!aircraft.HasTraitInfo<AttackBomberInfo>())
				throw new YamlException($"Airstrike actor '{UnitType}' requires AttackBomber.");
		}

		public override void DoImpact(in Target target, WarheadArgs args)
		{
			if (target.Type == TargetType.Invalid)
				return;

			var firedBy = args.SourceActor;
			if (target.Type == TargetType.Actor && !IsValidAgainst(target.Actor, firedBy))
				return;

			if (target.Type == TargetType.FrozenActor && !IsValidAgainst(target.FrozenActor, firedBy))
				return;

			// Boris calls the strike on an identified structure, not arbitrary terrain.
			if (target.Type == TargetType.Terrain)
				return;

			var world = firedBy.World;
			var targetPosition = target.CenterPosition;
			var facing = new WAngle(1024 * world.SharedRandom.Next(QuantizedFacings) / QuantizedFacings);
			var attackRotation = WRot.FromYaw(facing);
			var altitude = world.Map.Rules.Actors[UnitType.ToLowerInvariant()].TraitInfo<AircraftInfo>().CruiseAltitude.Length;
			var delta = new WVec(0, -1024, 0).Rotate(attackRotation);
			var airborneTarget = targetPosition + new WVec(0, 0, altitude);
			var startEdge = airborneTarget - (world.Map.DistanceToEdge(airborneTarget, -delta) + Cordon).Length * delta / 1024;
			var finishEdge = airborneTarget + (world.Map.DistanceToEdge(airborneTarget, delta) + Cordon).Length * delta / 1024;
			var aircraft = new List<(Actor Actor, WVec SpawnOffset)>();

			for (var i = -SquadSize / 2; i <= SquadSize / 2; i++)
			{
				// Even-sized formations leave the center lane empty.
				if (i == 0 && (SquadSize & 1) == 0)
					continue;

				// Includes the 90 degree rotation between body and world coordinates.
				var spawnOffset = new WVec(i * SquadOffset.Y, -Math.Abs(i) * SquadOffset.X, 0).Rotate(attackRotation);
				var targetOffset = new WVec(i * SquadOffset.Y, 0, 0).Rotate(attackRotation);
				var bomber = world.CreateActor(false, UnitType, new TypeDictionary
				{
					new CenterPositionInit(startEdge + spawnOffset),
					new OwnerInit(firedBy.Owner),
					new FacingInit(facing),
				});

				bomber.Trait<AttackBomber>().SetTarget(airborneTarget + targetOffset);
				aircraft.Add((bomber, spawnOffset));
			}

			world.AddFrameEndTask(w =>
			{
				foreach (var (bomber, spawnOffset) in aircraft)
				{
					w.Add(bomber);
					bomber.QueueActivity(new Fly(bomber, Target.FromPos(airborneTarget + spawnOffset)));
					bomber.QueueActivity(new Fly(bomber, Target.FromPos(finishEdge + spawnOffset)));
					bomber.QueueActivity(new RemoveSelf());
				}
			});
		}
	}
}
