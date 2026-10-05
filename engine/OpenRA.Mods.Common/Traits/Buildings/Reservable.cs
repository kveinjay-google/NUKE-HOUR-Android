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
using System.Linq;
using OpenRA.Mods.Common.Activities;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	[Desc("Reserve landing places for aircraft.")]
	sealed class ReservableInfo : TraitInfo, IRulesetLoaded
	{
		[Desc("Maximum number of aircraft that can reserve this actor at the same time.")]
		public readonly int Capacity = 1;

		[Desc("Allow a new reservation to displace an aircraft that has finished resupplying.")]
		public readonly bool EvictYieldingReservations = true;

		public override object Create(ActorInitializer init) { return new Reservable(this); }

		void IRulesetLoaded<ActorInfo>.RulesetLoaded(Ruleset rules, ActorInfo info)
		{
			if (Capacity < 1)
				throw new YamlException("Reservable capacity must be at least 1.");
		}
	}

	public class Reservable : ITick, INotifyOwnerChanged, INotifySold, INotifyActorDisposing, INotifyCreated
	{
		sealed class Reservation
		{
			public readonly Actor Actor;
			public readonly Aircraft Aircraft;
			public readonly Exit Slot;

			public Reservation(Actor actor, Aircraft aircraft, Exit slot)
			{
				Actor = actor;
				Aircraft = aircraft;
				Slot = slot;
			}
		}

		readonly ReservableInfo info;
		readonly List<Reservation> reservations = new();
		RallyPoint rallyPoint;

		internal Reservable(ReservableInfo info)
		{
			this.info = info;
		}

		void INotifyCreated.Created(Actor self)
		{
			rallyPoint = self.TraitOrDefault<RallyPoint>();
		}

		void ITick.Tick(Actor self)
		{
			// Nothing to do.
			if (reservations.Count == 0)
				return;

			foreach (var reservation in reservations.ToArray())
			{
				if (!Target.FromActor(reservation.Actor).IsValidFor(self))
					reservation.Aircraft.UnReserve();
			}
		}

		public IDisposable Reserve(Actor self, Actor forActor, Aircraft forAircraft, Exit slot = null)
		{
			if (!IsSlotAvailableFor(self, forActor, slot))
				throw new InvalidOperationException($"No reservation slot is available on {self.Info.Name} ({self.ActorID}).");

			if (reservations.Count >= info.Capacity)
			{
				var yielding = slot == null ? null : reservations.FirstOrDefault(r => r.Slot == slot && r.Aircraft.MayYieldReservation);
				yielding ??= reservations.FirstOrDefault(r => r.Aircraft.MayYieldReservation);
				if (yielding != null && info.EvictYieldingReservations)
					UnReserve(self, yielding);
			}

			var reservation = new Reservation(forActor, forAircraft, slot);
			reservations.Add(reservation);

			// NOTE: we really don't care about the GC eating DisposableActions that apply to a world *other* than
			// the one we're playing in.
			return new DisposableAction(
				() => reservations.Remove(reservation),
				() => Game.RunAfterTick(() =>
				{
					if (Game.IsCurrentWorld(self.World))
						throw new InvalidOperationException(
							$"Attempted to finalize an undisposed DisposableAction. {forActor.Info.Name} ({forActor.ActorID}) reserved {self.Info.Name} ({self.ActorID})");
				}));
		}

		public static bool IsReserved(Actor a)
		{
			var res = a.TraitOrDefault<Reservable>();
			return res != null && res.reservations.Count >= res.info.Capacity &&
				(!res.info.EvictYieldingReservations || res.reservations.All(r => !r.Aircraft.MayYieldReservation));
		}

		public static bool IsAvailableFor(Actor reservable, Actor forActor)
		{
			var res = reservable.TraitOrDefault<Reservable>();
			return res == null ||
				res.reservations.Any(r => r.Actor == forActor) ||
				res.reservations.Count < res.info.Capacity ||
				(res.info.EvictYieldingReservations && res.reservations.Any(r => r.Aircraft.MayYieldReservation));
		}

		public static bool IsSlotAvailableFor(Actor reservable, Actor forActor, Exit slot)
		{
			var res = reservable.TraitOrDefault<Reservable>();
			if (res == null)
				return true;

			var existing = res.reservations.FirstOrDefault(r => r.Actor == forActor);
			if (existing != null)
				return existing.Slot == slot;

			if (slot != null)
			{
				var slotReservation = res.reservations.FirstOrDefault(r => r.Slot == slot);
				if (slotReservation != null)
					return res.info.EvictYieldingReservations && slotReservation.Aircraft.MayYieldReservation;
			}

			return IsAvailableFor(reservable, forActor);
		}

		void UnReserve(Actor self, Reservation reservation)
		{
			if (reservation != null)
			{
				if (reservation.Aircraft.GetActorBelow() == self)
				{
					// Cache this before releasing the reservation so queued activities keep the same movement trait.
					var aircraft = reservation.Aircraft;
					if (rallyPoint != null && rallyPoint.Path.Count > 0)
						foreach (var cell in rallyPoint.Path)
							reservation.Actor.QueueActivity(new AttackMoveActivity(reservation.Actor, () => aircraft.MoveTo(cell, 1, targetLineColor: Color.OrangeRed)));
					else
						reservation.Actor.QueueActivity(new TakeOff(reservation.Actor));
				}

				reservation.Aircraft.UnReserve();
			}
		}

		void UnReserveAll(Actor self)
		{
			foreach (var reservation in reservations.ToArray())
				UnReserve(self, reservation);
		}

		void INotifyActorDisposing.Disposing(Actor self) { UnReserveAll(self); }

		void INotifyOwnerChanged.OnOwnerChanged(Actor self, Player oldOwner, Player newOwner) { UnReserveAll(self); }

		void INotifySold.Selling(Actor self) { UnReserveAll(self); }
		void INotifySold.Sold(Actor self) { UnReserveAll(self); }
	}
}
