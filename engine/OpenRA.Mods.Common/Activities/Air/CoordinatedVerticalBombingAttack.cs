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
using System.Linq;
using OpenRA.Mods.Common.Traits;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Activities
{
	public static class VerticalBombingPolicy
	{
		// Reserve the complete outward/return trip, plus two activity transition ticks.
		public static int RetreatDistance(int reloadTicks, int speed, int separation)
			=> Math.Min(Math.Max(0, separation), Math.Max(0, reloadTicks - 2) / 2 * Math.Max(0, speed));

		public static bool ShouldReturn(int reloadTicks, int distance, int speed)
			=> speed <= 0 || reloadTicks <= (Math.Max(0, distance) + speed - 1) / speed + 2;

		public static bool CanYield(bool hasReadyAlly, int reloadTicks, bool burstComplete)
			=> hasReadyAlly && burstComplete && reloadTicks > 2;
	}

	public sealed class CoordinatedVerticalBombingAttack : Attack
	{
		readonly Aircraft aircraft;
		readonly AttackFrontal attack;
		readonly bool allowMovement;
		readonly bool forceAttack;
		bool yieldedThisReload;
		bool returning;
		WPos? parkingPosition;

		public CoordinatedVerticalBombingAttack(Actor self, AttackFrontal attack, in Target target,
			bool allowMovement, bool forceAttack, Color? targetLineColor)
			: base(self, target, allowMovement, forceAttack, targetLineColor)
		{
			this.attack = attack;
			this.allowMovement = allowMovement;
			this.forceAttack = forceAttack;
			aircraft = self.Trait<Aircraft>();
		}

		bool CanCoordinate(Actor self) => !IsCanceling && allowMovement && !self.IsDead
			&& !attack.IsTraitPaused && !attack.IsTraitDisabled
			&& !aircraft.IsTraitPaused && !aircraft.IsTraitDisabled && aircraft.Info.CanHover && aircraft.Info.CanSlide
			&& !aircraft.ForceLanding && target.Type == TargetType.Actor
			&& target.IsValidFor(self) && target.Actor.CanBeViewedByPlayer(self.Owner);

		public bool CooperatesWith(Actor self, Actor other)
		{
			return CanCoordinate(self) && self.Owner.IsAlliedWith(other.Owner)
				&& other.CurrentActivity is CoordinatedVerticalBombingAttack ally
				&& ally.CanCoordinate(other) && target.Actor == ally.target.Actor;
		}

		Armament[] Weapons() => attack.ChooseArmamentsForTarget(target, forceAttack)
			.Where(a => !a.IsTraitDisabled && !a.IsTraitPaused).ToArray();

		public override bool Tick(Actor self)
		{
			// Use the normal Attack path for fog, cancellation, orders, takeoff and invalid weapons.
			if (!CanCoordinate(self) || self.World.Map.DistanceAboveTerrain(self.CenterPosition) != aircraft.Info.CruiseAltitude)
			{
				parkingPosition = null;
				return base.Tick(self);
			}

			var weapons = Weapons();
			if (weapons.Length == 0)
				return base.Tick(self);

			var reload = weapons.Min(a => a.FireDelay);
			if (reload == 0)
			{
				yieldedThisReload = false;
				returning = false;
				parkingPosition = null;
			}

			var speed = aircraft.MovementSpeed;
			var dropPosition = target.Positions.ClosestToIgnoringPath(self.CenterPosition);
			var range = weapons.Min(a => a.MaxRange()).Length;
			var distance = Math.Max(0, (self.CenterPosition - dropPosition).HorizontalLength - range);
			if (parkingPosition.HasValue && VerticalBombingPolicy.ShouldReturn(reload, distance, speed))
			{
				parkingPosition = null;
				returning = true;
			}

			if (!yieldedThisReload && !returning && target.IsInRange(self.CenterPosition, new WDist(range)))
			{
				var readyAlly = self.World.FindActorsInCircle(self.CenterPosition, aircraft.Info.IdealSeparation)
					.Any(other => other != self && CooperatesWith(self, other)
						&& ((CoordinatedVerticalBombingAttack)other.CurrentActivity).Weapons().Any(a => !a.IsReloading));
				if (VerticalBombingPolicy.CanYield(readyAlly, reload, weapons.All(a => a.Burst == a.Weapon.Burst)))
				{
					var retreat = VerticalBombingPolicy.RetreatDistance(reload, speed, aircraft.Info.IdealSeparation.Length);
					if (retreat > 0)
					{
						var outward = self.CenterPosition - dropPosition;
						var facing = outward.HorizontalLengthSquared > 0 ? outward.Yaw : new WAngle((int)(self.ActorID * 391 % 1024));
						var candidate = self.CenterPosition + aircraft.FlyStep(retreat, facing);
						if (self.World.Map.Contains(self.World.Map.CellContaining(candidate)))
							parkingPosition = candidate;
						yieldedThisReload = true;
					}
				}
			}

			if (parkingPosition.HasValue)
			{
				attack.IsAiming = false;
				var delta = parkingPosition.Value - self.CenterPosition;
				if (delta.HorizontalLength > 0 && speed > 0)
				{
					var step = aircraft.FlyStep(Math.Min(speed, delta.HorizontalLength), delta.Yaw);
					Fly.FlyTick(self, aircraft, delta.Yaw, aircraft.Info.CruiseAltitude, step);
				}

				return false;
			}

			// Re-evaluate the nearest target position every tick, including moving targets.
			// Avoid a blocking Fly child: target changes and reload readiness remain responsive.
			if (!target.IsInRange(self.CenterPosition, new WDist(range)) && speed > 0)
			{
				attack.IsAiming = false;
				var delta = dropPosition - self.CenterPosition;
				var step = aircraft.FlyStep(Math.Min(speed, Math.Max(1, distance)), delta.Yaw);
				Fly.FlyTick(self, aircraft, delta.Yaw, aircraft.Info.CruiseAltitude, step);
				return false;
			}

			return base.Tick(self);
		}
	}
}
