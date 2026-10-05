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
using System.Reflection;
using OpenRA.Primitives;
using System.Runtime.Serialization;
using NUnit.Framework;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Common.Widgets.Logic;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class PopulationLimitsTest
	{
		const BindingFlags Fields = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
		static T Empty<T>() => (T)FormatterServices.GetUninitializedObject(typeof(T));
		static void Set(object target, string name, object value) => target.GetType().GetField(name, Fields).SetValue(target, value);

		[Test]
		public void LiveSnapshotIncludesTransportedUnitsTransfersOwnershipAndDropsDisposedTransformSource()
		{
			var world = Empty<World>();
			var traits = typeof(World).GetField("TraitDict", Fields);
			traits.SetValue(world, Activator.CreateInstance(traits.FieldType, true));
			var a = Empty<Player>();
			var b = Empty<Player>();
			var limits = new PopulationLimits(world);
			Actor Unit(uint id, Player owner)
			{
				var actor = Empty<Actor>();
				Set(actor, "World", world);
				Set(actor, "ActorID", id);
				Set(actor, "<Owner>k__BackingField", owner);
				Set(actor, "<IsInWorld>k__BackingField", true);
				actor.AddTrait(new Population());
				return actor;
			}

			var carrier = Unit(1, a);
			var passenger = Unit(2, a);
			Assert.That(limits.Snapshot(a).PlayerUnits, Is.EqualTo(2));
			Set(passenger, "<IsInWorld>k__BackingField", false);
			Assert.That(limits.Snapshot(a).PlayerUnits, Is.EqualTo(2), "Loading cargo cannot free population.");
			Set(passenger, "<Owner>k__BackingField", b);
			Assert.That(limits.Snapshot(a).PlayerUnits, Is.EqualTo(1));
			Assert.That(limits.Snapshot(b).PlayerUnits, Is.EqualTo(1));
			Assert.That(limits.Snapshot(b).TotalUnits, Is.EqualTo(2));
			Set(carrier, "<WillDispose>k__BackingField", true);
			Unit(3, a);
			Assert.That(limits.Snapshot(a).PlayerUnits, Is.EqualTo(1), "Transform replacement must not count its disposing source.");
			Set(passenger, "<Disposed>k__BackingField", true);
			Assert.That(limits.Snapshot(b).PlayerUnits, Is.Zero);
			Assert.That(limits.Snapshot(b).TotalUnits, Is.EqualTo(1));
		}

		[Test]
		public void RealQueuesReserveAcrossOwnersAndDeliveryBridgesDeferredCreation()
		{
			var world = Empty<World>();
			var traits = typeof(World).GetField("TraitDict", Fields);
			traits.SetValue(world, Activator.CreateInstance(traits.FieldType, true));
			var frameEnd = new Queue<Action<World>>();
			Set(world, "frameEndActions", frameEnd);
			var unit = new ActorInfo("unit", new PopulationInfo());
			var building = new ActorInfo("building");
			var rules = Empty<Ruleset>();
			Set(rules, "Actors", new ActorInfoDictionary(new Dictionary<string, ActorInfo> { { "unit", unit }, { "building", building } }));
			var map = Empty<Map>();
			Set(map, "<Rules>k__BackingField", rules);
			Set(world, "Map", map);
			var a = Empty<Player>();
			var b = Empty<Player>();
			uint id = 0;
			ProductionQueue Queue(Player owner, params string[] names)
			{
				var actor = Empty<Actor>();
				Set(actor, "World", world);
				Set(actor, "ActorID", ++id);
				Set(actor, "<Owner>k__BackingField", owner);
				var queue = Empty<ProductionQueue>();
				Set(queue, "<Actor>k__BackingField", actor);
				var items = new List<ProductionItem>();
				foreach (var name in names)
				{
					var item = Empty<ProductionItem>();
					Set(item, "Item", name);
					items.Add(item);
				}

				Set(queue, "Queue", items);
				actor.AddTrait(queue);
				return queue;
			}

			var first = Queue(a, "unit", "unit", "building");
			Queue(a, "unit");
			Queue(b, "unit", "unit");
			var limits = new PopulationLimits(world);
			Set(limits, "<PlayerLimit>k__BackingField", 3);
			Set(limits, "<TotalLimit>k__BackingField", 5);
			Assert.That(limits.Snapshot(a).PlayerReserved, Is.EqualTo(3));
			Assert.That(limits.Snapshot(a).TotalReserved, Is.EqualTo(5));
			Assert.That(limits.CanQueue(a, unit), Is.False);
			Assert.That(limits.CanQueue(a, building), Is.True);
			Assert.That(limits.CanProduce(a, unit), Is.True);
			first.EndProduction(first.CurrentItem());
			limits.HoldDelivery(a, unit);
			Assert.That(limits.CanQueue(a, unit), Is.False, "Removing a finished queue item cannot free its in-flight delivery slot.");
			Assert.That(limits.Snapshot(a).PlayerUnits, Is.EqualTo(1));
			frameEnd.Dequeue()(world);
			Assert.That(limits.CanQueue(a, unit), Is.True, "Failed/non-unit delivery release leaves no stale reservation.");
			var resources = Empty<PlayerResources>();
			Set(first, "playerResources", resources);
			var cancelled = first.CurrentItem();
			Set(cancelled, "TotalCost", 100);
			cancelled.RemainingCost = 30;
			var cancel = typeof(ProductionQueue).GetMethod("CancelProductionInner", Fields);
			Assert.That(cancel.Invoke(first, new object[] { "unit" }), Is.True);
			Assert.That(resources.Cash, Is.EqualTo(70), "Cancelling retained paid work refunds exactly the paid portion.");
			Assert.That(limits.Snapshot(a).PlayerReserved, Is.EqualTo(1));
			Set(first.Actor, "<WillDispose>k__BackingField", true);
			Assert.That(limits.Snapshot(a).PlayerReserved, Is.EqualTo(1), "Destroying a producer releases that queue's reservations.");
		}

		[Test]
		public void TooltipSnapshotsAreThrottledButRefreshOnActorOwnerAndClockChanges()
		{
			var calls = 0;
			var cache = new PopulationTooltipStatusCache((actor, owner) => (++calls).ToString());
			var unit = new ActorInfo("unit");
			var otherUnit = new ActorInfo("other");
			var player = Empty<Player>();
			Assert.That(cache.Get(unit, player, 0), Is.EqualTo("1"));
			Assert.That(cache.Get(unit, player, 249), Is.EqualTo("1"));
			Assert.That(cache.Get(unit, player, 250), Is.EqualTo("2"));
			Assert.That(cache.Get(otherUnit, player, 251), Is.EqualTo("3"));
			Assert.That(cache.Get(otherUnit, null, 252), Is.EqualTo("4"));
			Assert.That(cache.Get(otherUnit, null, 0), Is.EqualTo("5"));
		}

		[TestCase("0", 0)]
		[TestCase("200", 200)]
		[TestCase("-1", 0)]
		[TestCase("201", 0)]
		[TestCase("2147483648", 0)]
		[TestCase("oops", 0)]
		public void OnlyAdvertisedRoomPresetsAreAccepted(string input, int expected)
		{
			Assert.That(PopulationLimits.ParseLimit(input), Is.EqualTo(expected));
		}

		[Test]
		public void CampaignAndShellmapNeverEnableLimits()
		{
			Assert.That(PopulationLimits.IsSkirmish(MapVisibility.Lobby), Is.True);
			Assert.That(PopulationLimits.IsSkirmish(MapVisibility.MissionSelector | MapVisibility.Lobby), Is.False);
			Assert.That(PopulationLimits.IsSkirmish(MapVisibility.Shellmap), Is.False);
		}

		[Test]
		public void ReservationsAcrossFactoriesAndPlayersConsumeRoomCapacity()
		{
			Assert.That(new PopulationSnapshot(95, 193, 4, 6).CanAdd(100, 200), Is.True);
			Assert.That(new PopulationSnapshot(95, 193, 5, 7).CanAdd(100, 200), Is.False);
			Assert.That(new PopulationSnapshot(1, 193, 0, 7).CanAdd(100, 200), Is.False);
			Assert.That(new PopulationSnapshot(95, 193, 5, 7).CanAdd(100, 200, includeReservations: false), Is.True,
				"Already reserved production must not deadlock behind its own reservation.");
		}

		[Test]
		public void CaptureOverCapHoldsProductionUntilLossesAndUnlimitedNeverBlocks()
		{
			Assert.That(new PopulationSnapshot(101, 201, 5, 7).CanAdd(100, 200, includeReservations: false), Is.False);
			Assert.That(new PopulationSnapshot(99, 199, 5, 7).CanAdd(100, 200, includeReservations: false), Is.True);
			Assert.That(new PopulationSnapshot(int.MaxValue, int.MaxValue, 5, 7).CanAdd(0, 0), Is.True);
			Assert.That(new PopulationSnapshot(int.MaxValue, int.MaxValue, 5, 7).CanAdd(100, 200), Is.False);
		}

		[Test]
		public void CancellingReservationsReleasesCapacityAndBatchesAreAtomic()
		{
			Assert.That(new PopulationSnapshot(90, 190, 10, 10).CanAdd(100, 200), Is.False);
			Assert.That(new PopulationSnapshot(90, 190, 9, 9).CanAdd(100, 200), Is.True);
			Assert.That(new PopulationSnapshot(90, 190, 0, 0).CanAdd(100, 200, 10), Is.True);
			Assert.That(new PopulationSnapshot(90, 190, 0, 0).CanAdd(100, 200, 11), Is.False);
		}
	}
}
