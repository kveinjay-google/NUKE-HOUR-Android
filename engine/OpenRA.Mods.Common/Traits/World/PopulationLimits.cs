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
using System.Globalization;
using System.Linq;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	public static class PopulationMessages
	{
		[FluentReference("status")]
		public const string Feedback = "population-feedback";
		[FluentReference]
		public const string Unlimited = "population-unlimited";
		[FluentReference]
		public const string PlayerLabel = "population-player-label";
		[FluentReference]
		public const string TotalLabel = "population-total-label";
		[FluentReference]
		public const string Description = "population-description";
		[FluentReference]
		public const string RuntimeDescription = "population-runtime-description";
		[FluentReference("current", "reserved", "limit")]
		public const string Status = "population-status";
		[FluentReference("current", "reserved", "limit")]
		public const string TotalStatus = "population-total-status";
		[FluentReference]
		public const string PlayerBlocked = "population-player-blocked";
		[FluentReference]
		public const string TotalBlocked = "population-total-blocked";
		[FluentReference]
		public const string Blocked = "population-blocked";
	}

	// Explicit opt-in keeps other mods, buildings, projectiles and visual effects unchanged.
	public sealed class PopulationInfo : TraitInfo<Population> { }
	public sealed class Population { }

	public readonly struct PopulationSnapshot
	{
		public readonly int PlayerUnits, TotalUnits, PlayerReserved, TotalReserved;
		public PopulationSnapshot(int playerUnits, int totalUnits, int playerReserved, int totalReserved)
		{
			PlayerUnits = playerUnits;
			TotalUnits = totalUnits;
			PlayerReserved = playerReserved;
			TotalReserved = totalReserved;
		}

		public bool CanAdd(int playerLimit, int totalLimit, int amount = 1, bool includeReservations = true)
		{
			return amount >= 0
				&& (playerLimit == 0 || (long)PlayerUnits + (includeReservations ? PlayerReserved : 0) + amount <= playerLimit)
				&& (totalLimit == 0 || (long)TotalUnits + (includeReservations ? TotalReserved : 0) + amount <= totalLimit);
		}
	}

	[TraitLocation(SystemActors.World)]
	public sealed class PopulationLimitsInfo : TraitInfo, ILobbyOptions
	{
		IEnumerable<LobbyOption> ILobbyOptions.LobbyOptions(MapPreview map)
		{
			if (!PopulationLimits.IsSkirmish(map.Visibility))
				yield break;

			var values = new Dictionary<string, string> { { "0", map.GetMessage(PopulationMessages.Unlimited) } };
			foreach (var value in PopulationLimits.Presets.Skip(1))
				values.Add(value.ToString(CultureInfo.InvariantCulture), value.ToString(CultureInfo.InvariantCulture));
			yield return new LobbyOption(map, "population-player", PopulationMessages.PlayerLabel, PopulationMessages.Description,
				true, 30, values, "0", false);
			yield return new LobbyOption(map, "population-total", PopulationMessages.TotalLabel, PopulationMessages.Description,
				true, 31, values, "0", false);
		}

		public override object Create(ActorInitializer init) { return new PopulationLimits(init.Self.World); }
	}

	public sealed class PopulationLimits : INotifyCreated, IResolveOrder, ISync
	{
		public const string TotalOptionId = "population-total";
		public const string SetTotalOrder = "SetPopulationTotalLimit";
		public static readonly int[] Presets = { 0, 100, 200, 300, 500, 800, 1000, 1500, 2000 };
		readonly World world;
		readonly Dictionary<Player, int> deliveries = new();
		// Lobby client refreshes are immediate and can arrive on different simulation frames.
		// In-game administration stays with the starting host, as the server already requires.
		int controllerClientIndex = -1;
		public int PlayerLimit { get; private set; }
		[Sync]
		public int TotalLimit { get; private set; }
		public bool Enabled => PlayerLimit != 0 || TotalLimit != 0;

		public PopulationLimits(World world) { this.world = world; }
		public static bool IsSkirmish(MapVisibility visibility) => (visibility & MapVisibility.Lobby) != 0
			&& (visibility & (MapVisibility.MissionSelector | MapVisibility.Shellmap)) == 0;
		public static int ParseLimit(string value) => int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var limit)
			&& Presets.Contains(limit) ? limit : 0;
		public static bool Counts(ActorInfo actor) => actor.HasTraitInfo<PopulationInfo>();

		void INotifyCreated.Created(Actor self)
		{
			if (!IsSkirmish(world.Map.Visibility))
				return;
			controllerClientIndex = world.LobbyInfo.Clients.FirstOrDefault(c => c.IsAdmin && !c.IsBot)?.Index ?? -1;
			PlayerLimit = ParseLimit(world.LobbyInfo.GlobalSettings.OptionOrDefault("population-player", "0"));
			TotalLimit = ParseLimit(world.LobbyInfo.GlobalSettings.OptionOrDefault("population-total", "0"));
		}

		public bool CanControlTotal(int clientId) => controllerClientIndex >= 0 && clientId == controllerClientIndex
			&& !world.IsGameOver && world.Type == WorldType.Regular && IsSkirmish(world.Map.Visibility);

		public bool ValidateRuntimeOrder(int clientId, Order order) => CanControlTotal(clientId) && ValidRuntimePayload(order);

		bool ValidRuntimePayload(Order order) => order.OrderString == SetTotalOrder && order.Subject == world.WorldActor
			&& !order.IsImmediate && !order.Queued && order.Type == OrderType.Fields
			&& order.GroupedActors == null && order.ExtraActors == null && order.Target.Type == TargetType.Invalid
			&& order.TargetString == null && order.ExtraData <= int.MaxValue && Presets.Contains((int)order.ExtraData);

		void IResolveOrder.ResolveOrder(Actor self, Order order)
		{
			// ValidateOrder authenticates the sending client before reaching this synchronized path.
			if (self == world.WorldActor && ValidRuntimePayload(order) && !world.IsGameOver && IsSkirmish(world.Map.Visibility))
				TotalLimit = (int)order.ExtraData;
		}

		// Read-only diagnostics: exclude production reservations and transient delivery holds.
		public static int CountCurrentUnits(World world)
		{
			var total = 0;
			foreach (var actor in world.ActorsHavingTrait<Population>())
				if (!actor.IsDead && !actor.WillDispose && !actor.Owner.NonCombatant)
					total++;
			return total;
		}

		public PopulationSnapshot Snapshot(Player player)
		{
			var own = 0;
			var total = 0;
			var ownReserved = 0;
			var totalReserved = 0;
			// TraitDictionary includes cargo, parasites and actors waiting to enter the world.
			// Do not use World.Actors/IsInWorld: loading a transport must never release capacity.
			foreach (var actor in world.ActorsHavingTrait<Population>())
			{
				if (actor.IsDead || actor.WillDispose || actor.Owner.NonCombatant)
					continue;
				total++;
				if (actor.Owner == player)
					own++;
			}

			foreach (var delivery in deliveries)
			{
				total += delivery.Value;
				if (delivery.Key == player)
					own += delivery.Value;
			}

			foreach (var pair in world.ActorsWithTrait<ProductionQueue>())
			{
				if (pair.Actor.IsDead || pair.Actor.WillDispose || pair.Actor.Owner.NonCombatant)
					continue;
				foreach (var item in pair.Trait.AllQueued())
				{
					if (!Counts(world.Map.Rules.Actors[item.Item]))
						continue;
					totalReserved++;
					if (pair.Actor.Owner == player)
						ownReserved++;
				}
			}

			return new PopulationSnapshot(own, total, ownReserved, totalReserved);
		}

		public bool CanQueue(Player owner, ActorInfo actor) => !Enabled || !Counts(actor)
			|| Snapshot(owner).CanAdd(PlayerLimit, TotalLimit);
		public bool CanProduce(Player owner, ActorInfo actor) => !Enabled || !Counts(actor)
			|| Snapshot(owner).CanAdd(PlayerLimit, TotalLimit, includeReservations: false);

		// Bridge queue removal and deferred actor creation: concurrent factories cannot see a vacant slot.
		public void HoldDelivery(Player owner, ActorInfo actor)
		{
			if (!Enabled || !Counts(actor))
				return;
			deliveries[owner] = deliveries.GetValueOrDefault(owner) + 1;
			world.AddFrameEndTask(_ => deliveries[owner]--);
		}

		public static bool CanSpawn(World world, Player owner, string actor) =>
			world.WorldActor.TraitOrDefault<PopulationLimits>()?.CanQueue(owner, world.Map.Rules.Actors[actor.ToLowerInvariant()]) ?? true;

		public string Status(Player owner, ActorInfo actor)
		{
			if (!Enabled || !Counts(actor))
				return "";
			var s = Snapshot(owner);
			var unlimited = FluentProvider.GetMessage(PopulationMessages.Unlimited);
			var status = FluentProvider.GetMessage(PopulationMessages.Status, "current", s.PlayerUnits, "reserved", s.PlayerReserved,
				"limit", PlayerLimit == 0 ? unlimited : PlayerLimit.ToString(CultureInfo.InvariantCulture))
				+ "\n" + FluentProvider.GetMessage(PopulationMessages.TotalStatus, "current", s.TotalUnits, "reserved", s.TotalReserved,
					"limit", TotalLimit == 0 ? unlimited : TotalLimit.ToString(CultureInfo.InvariantCulture));
			if (!s.CanAdd(PlayerLimit, TotalLimit))
				status += "\n" + FluentProvider.GetMessage(PlayerLimit != 0 && s.PlayerUnits + s.PlayerReserved >= PlayerLimit
					? PopulationMessages.PlayerBlocked : PopulationMessages.TotalBlocked);
			return status;
		}
	}
}
