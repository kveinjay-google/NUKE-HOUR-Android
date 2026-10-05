// Copyright (c) The OpenRA Developers and Contributors
// Licensed under the GNU General Public License v3.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using NUnit.Framework;
using OpenRA.Mods.Common.Traits;
using OpenRA.Network;
using OpenRA.Traits;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class RuntimePopulationLimitsTest
	{
		const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
		const string Command = "SetPopulationTotalLimit";
		static T Empty<T>() => (T)FormatterServices.GetUninitializedObject(typeof(T));
		static void Set(object target, string name, object value) => target.GetType().GetField(name, Fields).SetValue(target, value);

		sealed class Fixture
		{
			public readonly World World = Empty<World>();
			public readonly PopulationLimits Limits;
			public readonly OrderManager Orders = Empty<OrderManager>();
			public readonly Actor Actor = Empty<Actor>();
			public readonly Player Owner = Empty<Player>();
			public Fixture(MapVisibility visibility = MapVisibility.Lobby, bool admin = true)
			{
				var traits = typeof(World).GetField("TraitDict", Fields);
				traits.SetValue(World, Activator.CreateInstance(traits.FieldType, true));
				Set(World, "OrderManager", Orders);
				Orders.LobbyInfo = new Session();
				Orders.LobbyInfo.Clients.Add(new Session.Client { Index = 1, IsAdmin = admin });
				Orders.LobbyInfo.Clients.Add(new Session.Client { Index = 2 });
				Set(Orders, "<Connection>k__BackingField", Empty<EchoConnection>());
				var map = Empty<Map>();
				Set(map, "Visibility", visibility);
				Set(World, "Map", map);
				Set(World, "WorldActor", Actor);
				Set(Actor, "World", World);
				Set(Actor, "ActorID", 1u);
				Set(Owner, "ClientIndex", 1);
				Set(Owner, "resolvedPlayerName", "Host");
				Set(Actor, "<Owner>k__BackingField", Owner);
				Limits = new PopulationLimits(World);
				Actor.AddTrait(Limits);
				((INotifyCreated)Limits).Created(Actor);
				Set(World, "OrderValidators", new IValidateOrder[] { new ValidateOrder() });
			}

			public Order Order(uint cap, Actor subject = null) => new(Command, subject ?? Actor, false) { ExtraData = cap };
			public bool Validate(int client, Order order) => new ValidateOrder().OrderValidation(Orders, World, client, order);
			public void Deliver(int client, Order order)
			{
				Assert.That(Limits, Is.InstanceOf<IResolveOrder>(), "World population limit must resolve synchronized orders.");
				Set(Actor, "resolveOrders", new IResolveOrder[] { (IResolveOrder)(object)Limits });
				typeof(UnitOrders).GetMethod("ProcessOrder", BindingFlags.NonPublic | BindingFlags.Static)
					.Invoke(null, new object[] { Orders, World, client, order });
			}
		}

		[Test]
		public void HostOrderChangesSimulationOnlyWhenDeliveredAndOrdinaryClientsCannotChangeIt()
		{
			var f = new Fixture();
			var order = f.Order(200);
			Assert.That(order.IsImmediate, Is.False);
			Assert.That(f.Validate(1, order), Is.True, "Host can control the world actor only for this valid command.");
			Assert.That(f.Limits.TotalLimit, Is.Zero, "Creating a UI order cannot mutate simulation.");
			f.Deliver(2, order);
			Assert.That(f.Limits.TotalLimit, Is.Zero);
			f.Deliver(1, order);
			Assert.That(f.Limits.TotalLimit, Is.EqualTo(200));
			f.Deliver(1, f.Order(500));
			Assert.That(f.Limits.TotalLimit, Is.EqualTo(500));
			f.Deliver(1, f.Order(0));
			Assert.That(f.Limits.Enabled, Is.False);
		}

		[Test]
		public void RuntimeAuthorityDoesNotDependOnAsynchronousLobbyRefreshTiming()
		{
			var f = new Fixture();
			f.Orders.LobbyInfo.Clients.Clear();
			Assert.That(f.Validate(1, f.Order(100)), Is.True, "In-game authority is captured from the starting room.");
			Assert.That(f.Validate(2, f.Order(100)), Is.False);
		}

		[Test]
		public void MalformedImmediateWrongSubjectAndNonSkirmishOrdersAreRejected()
		{
			var f = new Fixture();
			Assert.That(f.Validate(1, f.Order(201)), Is.False);
			Assert.That(f.Validate(1, f.Order(uint.MaxValue)), Is.False);
			var immediate = f.Order(200); immediate.IsImmediate = true;
			Assert.That(f.Validate(1, immediate), Is.False);
			Assert.That(f.Validate(1, f.Order(200, Empty<Actor>())), Is.False);
			var grouped = new Order(Command, f.Actor, false, groupedActors: new[] { f.Actor }) { ExtraData = 100 };
			Assert.That(f.Validate(1, grouped), Is.False);
			Assert.That(new Fixture(MapVisibility.MissionSelector | MapVisibility.Lobby).Validate(1,
				new Order(Command, null, false)), Is.False);
			var mission = new Fixture(MapVisibility.MissionSelector | MapVisibility.Lobby);
			Assert.That(mission.Validate(1, mission.Order(200)), Is.False);
			var noHost = new Fixture(admin: false);
			Assert.That(noHost.Validate(1, noHost.Order(200)), Is.False);
		}

		[Test]
		public void LoweringPreservesExistingUnitsAndReservationsAndRaisingResumesProduction()
		{
			var f = new Fixture();
			var unit = new ActorInfo("unit", new PopulationInfo());
			for (uint i = 2; i < 152; i++)
			{
				var a = Empty<Actor>();
				Set(a, "World", f.World); Set(a, "ActorID", i); Set(a, "<Owner>k__BackingField", f.Owner);
				a.AddTrait(new Population());
			}

			var rules = Empty<Ruleset>();
			Set(rules, "Actors", new ActorInfoDictionary(new Dictionary<string, ActorInfo> { { "unit", unit } }));
			Set(f.World.Map, "<Rules>k__BackingField", rules);
			var producer = Empty<Actor>(); Set(producer, "World", f.World); Set(producer, "ActorID", 152u);
			Set(producer, "<Owner>k__BackingField", f.Owner);
			var queue = Empty<ProductionQueue>(); Set(queue, "<Actor>k__BackingField", producer);
			var items = new List<ProductionItem>();
			for (var i = 0; i < 10; i++) { var item = Empty<ProductionItem>(); Set(item, "Item", "unit"); items.Add(item); }
			Set(queue, "Queue", items); producer.AddTrait(queue);
			f.Deliver(1, f.Order(100));
			Assert.That(f.Limits.Snapshot(f.Owner).TotalReserved, Is.EqualTo(10));
			Assert.That(f.Limits.Snapshot(f.Owner).TotalUnits, Is.EqualTo(150));
			Assert.That(f.Limits.CanProduce(f.Owner, unit), Is.False);
			Assert.That(f.Limits.CanQueue(f.Owner, unit), Is.False);
			f.Deliver(1, f.Order(200));
			Assert.That(f.Limits.CanProduce(f.Owner, unit), Is.True);
			Assert.That(f.Limits.Snapshot(f.Owner).TotalReserved, Is.EqualTo(10), "Changing the cap retains queued orders.");
			Assert.That(f.Limits.CanQueue(f.Owner, unit), Is.True);
			Assert.That(f.Limits.Snapshot(f.Owner).TotalUnits, Is.EqualTo(150));
		}

		[Test]
		public void SerializedOrdersReplayIdenticallyAndTheMutableLimitIsInSyncHash()
		{
			var a = new Fixture(); var b = new Fixture();
			Set(b.Orders, "<Connection>k__BackingField", Empty<ReplayConnection>());
			Assert.That(a.Limits, Is.InstanceOf<ISync>());
			var hash = typeof(Sync).GetMethod("Hash", BindingFlags.NonPublic | BindingFlags.Static);
			var oldHash = (int)hash.Invoke(null, new object[] { a.Limits });
			foreach (var cap in new uint[] { 100, 300, 0, 2000 })
			{
				var bytes = a.Order(cap).Serialize();
				using var stream = new MemoryStream(bytes);
				using var reader = new BinaryReader(stream);
				Set(b.World, "actors", new SortedDictionary<uint, Actor> { { 1, b.Actor } });
				b.Deliver(1, OpenRA.Order.Deserialize(b.World, reader));
				a.Deliver(1, a.Order(cap));
				Assert.That(a.Limits.TotalLimit, Is.EqualTo(b.Limits.TotalLimit));
				Assert.That(hash.Invoke(null, new object[] { a.Limits }), Is.EqualTo(hash.Invoke(null, new object[] { b.Limits })));
			}

			Assert.That(hash.Invoke(null, new object[] { a.Limits }), Is.Not.EqualTo(oldHash));
		}
	}
}
