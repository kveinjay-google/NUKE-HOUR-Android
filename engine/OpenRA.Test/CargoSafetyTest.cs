using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using NUnit.Framework;
using OpenRA.Mods.Common.Traits;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Test
{
	[TestFixture]
	public class CargoSafetyTest
	{
		World world;
		uint nextId;
		const BindingFlags Fields = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
		static T Empty<T>() => (T)FormatterServices.GetUninitializedObject(typeof(T));
		static void Field(object target, string name, object value) => target.GetType().GetField(name, Fields).SetValue(target, value);

		[SetUp]
		public void Setup()
		{
			world = Empty<World>();
			var traits = typeof(World).GetField("TraitDict", Fields);
			traits.SetValue(world, Activator.CreateInstance(traits.FieldType, true));
			nextId = 1;
		}

		Actor Unit(string type = "Vehicle", int weight = 3, bool carrier = false, bool passenger = true)
		{
			var info = new PassengerInfo();
			Field(info, "CargoType", type);
			Field(info, "Weight", weight);
			var infos = new List<TraitInfo> { new MobileInfo() };
			if (passenger)
				infos.Add(info);
			if (carrier)
				infos.Add(new CargoInfo());
			var actor = Empty<Actor>();
			Field(actor, "World", world);
			Field(actor, "ActorID", nextId++);
			Field(actor, "Info", new ActorInfo("unit" + nextId, infos.ToArray()));
			Field(actor, "<IsInWorld>k__BackingField", true);
			Field(actor, "created", true);
			if (passenger)
				actor.AddTrait(info.Create(new ActorInitializer(actor, new TypeDictionary())));
			return actor;
		}

		Cargo Hold(Actor actor, int capacity = 12, params string[] types)
		{
			var info = new CargoInfo();
			Field(info, "MaxWeight", capacity);
			foreach (var type in types.Length == 0 ? new[] { "Infantry", "Vehicle" } : types)
				info.Types.Add(type);
			var cargo = new Cargo(new ActorInitializer(actor, new TypeDictionary()), info);
			actor.AddTrait(cargo);
			return cargo;
		}

		[TestCase("Infantry", 1)]
		[TestCase("Vehicle", 3)]
		public void OrdinaryPassengersFitTheirTransport(string type, int weight)
		{
			Assert.That(Hold(Unit(carrier: true)).CanLoad(Unit(type, weight)), Is.True);
		}

		[Test]
		public void TransportCannotLoadItself()
		{
			var boat = Unit(carrier: true);
			Assert.That(Hold(boat).CanLoad(boat), Is.False);
		}

		[Test]
		public void EmptyTransportCannotBeNestedInAnotherTransport()
		{
			Assert.That(Hold(Unit(carrier: true)).CanLoad(Unit(carrier: true)), Is.False);
		}

		[Test]
		public void NonPassengerIsRejectedWithoutThrowing()
		{
			Assert.That(Hold(Unit(carrier: true)).CanLoad(Unit(passenger: false)), Is.False);
		}

		[Test]
		public void WrongCargoTypeIsRejected()
		{
			Assert.That(Hold(Unit(carrier: true), 12, "Infantry").CanLoad(Unit()), Is.False);
		}

		[TestCase(0)]
		[TestCase(-3)]
		[TestCase(13)]
		public void InvalidOrExcessiveWeightIsRejected(int weight)
		{
			Assert.That(Hold(Unit(carrier: true)).CanLoad(Unit(weight: weight)), Is.False);
		}

		[Test]
		public void AlreadyCarriedPassengerCannotEnterASecondTransport()
		{
			var passenger = Unit();
			passenger.Trait<Passenger>().Transport = Unit(carrier: true);
			Assert.That(Hold(Unit(carrier: true)).CanLoad(passenger), Is.False);
		}

		[Test]
		public void DisposedPassengerIsRejected()
		{
			var passenger = Unit();
			Field(passenger, "<Disposed>k__BackingField", true);
			Assert.That(Hold(Unit(carrier: true)).CanLoad(passenger), Is.False);
		}

		[Test]
		public void InvalidReservationDoesNotLockTheTransport()
		{
			var boat = Unit(carrier: true);
			var cargo = Hold(boat);
			var reserve = typeof(Cargo).GetMethod("ReserveSpace", Fields);
			Assert.That(reserve.Invoke(cargo, new object[] { Unit(carrier: true) }), Is.False);
			Assert.That(boat.CurrentActivity, Is.Null);
			Assert.That(cargo.HasSpace(12), Is.True);
		}

		[Test]
		public void DirectLoadRejectsNestedCargoWithoutChangingTheHold()
		{
			var boat = Unit(carrier: true);
			var cargo = Hold(boat);
			cargo.Load(boat, Unit(carrier: true));
			Assert.That(cargo.PassengerCount, Is.Zero);
			Assert.That(cargo.HasSpace(12), Is.True);
		}

		[Test]
		public void DirectLoadCannotDuplicateAPassengerBeforeCreationNotifications()
		{
			var boat = Unit(carrier: true);
			var cargo = Hold(boat);
			var passenger = Unit();
			cargo.Load(boat, passenger);
			cargo.Load(boat, passenger);
			Assert.That(cargo.PassengerCount, Is.EqualTo(1));
			Assert.That(passenger.Trait<Passenger>().Transport, Is.SameAs(boat));
		}

		[Test]
		public void DirectLoadCannotOverfillTheHold()
		{
			var boat = Unit(carrier: true);
			var cargo = Hold(boat, 3);
			cargo.Load(boat, Unit());
			cargo.Load(boat, Unit());
			Assert.That(cargo.PassengerCount, Is.EqualTo(1));
		}

		[Test]
		public void ScriptOnlyParatrooperCargoStillAcceptsOrdinaryInfantry()
		{
			var plane = Unit(carrier: true, passenger: false);
			var cargo = Hold(plane);
			cargo.Info.Types.Clear();
			cargo.Load(plane, Unit("Infantry", 1));
			Assert.That(cargo.PassengerCount, Is.EqualTo(1));
		}

		[Test]
		public void BoardingCursorUsesTheSameRestrictionsAsActualLoading()
		{
			var boat = Unit(carrier: true);
			var carrier = Unit(carrier: true);
			Hold(boat);
			var canEnter = typeof(Passenger).GetMethod("CanEnter", Fields, null, new[] { typeof(Actor) }, null);
			Assert.That(canEnter.Invoke(carrier.Trait<Passenger>(), new object[] { boat }), Is.False);
			Assert.That(canEnter.Invoke(Unit().Trait<Passenger>(), new object[] { boat }), Is.True);
		}

		[Test]
		public void FourOrdinaryVehiclesLoadAndUnloadWithoutLosingCapacity()
		{
			var boat = Unit(carrier: true);
			var cargo = Hold(boat);
			for (var i = 0; i < 4; i++)
				cargo.Load(boat, Unit());
			Assert.That(cargo.PassengerCount, Is.EqualTo(4));
			Assert.That(cargo.CanLoad(Unit()), Is.False);
			var released = cargo.Unload(boat);
			Assert.That(released.Trait<Passenger>().Transport, Is.Null);
			Assert.That(cargo.CanLoad(released), Is.True);
			cargo.Load(boat, released);
			Assert.That(cargo.PassengerCount, Is.EqualTo(4));
		}

		[Test]
		public void ScriptOnlyPlaneDoesNotOfferPlayerBoardingOrders()
		{
			var cargo = Hold(Unit(carrier: true, passenger: false));
			cargo.Info.Types.Clear();
			Assert.That(cargo.CanLoad(Unit("Infantry", 1)), Is.False);
		}

		[Test]
		public void InitialCargoRejectsNestedAndMissingActorsBeforeCreatingThem()
		{
			var boat = Unit(carrier: true);
			var nested = Unit(carrier: true);
			var map = Empty<Map>();
			var rules = Empty<Ruleset>();
			Field(rules, "Actors", new ActorInfoDictionary(new Dictionary<string, ActorInfo>
			{
				[nested.Info.Name] = nested.Info,
			}));
			Field(map, "<Rules>k__BackingField", rules);
			Field(world, "Map", map);
			var info = new CargoInfo();
			Field(info, "MaxWeight", 12);
			info.Types.Add("Vehicle");
			var cargo = new Cargo(new ActorInitializer(boat, new TypeDictionary
			{
				new CargoInit(info, new[] { nested.Info.Name, "missing", "" }),
			}), info);
			Assert.That(cargo.PassengerCount, Is.Zero);
		}

		[Test]
		public void RuntimeCargoRejectsDuplicatesCarriersAndOverCapacity()
		{
			var boat = Unit(carrier: true);
			var passenger = Unit();
			var info = new CargoInfo();
			Field(info, "MaxWeight", 3);
			info.Types.Add("Vehicle");
			var cargo = new Cargo(new ActorInitializer(boat, new TypeDictionary
			{
				new RuntimeCargoInit(info, new[] { passenger, passenger, Unit(carrier: true), Unit() }),
			}), info);
			Assert.That(cargo.PassengerCount, Is.EqualTo(1));
			Assert.That(cargo.Peek(), Is.SameAs(passenger));
		}

		[Test]
		public void CapacityArithmeticCannotWrapAround()
		{
			var cargo = Hold(Unit(carrier: true), int.MaxValue);
			Field(cargo, "totalWeight", int.MaxValue - 1);
			Assert.That(cargo.HasSpace(3), Is.False);
		}

		[Test]
		public void ReservationIsConsumedExactlyOnceBySuccessfulLoading()
		{
			var boat = Unit(carrier: true);
			var cargo = Hold(boat, 3);
			var actor = Unit();
			var passenger = actor.Trait<Passenger>();
			Assert.That(passenger.Reserve(actor, cargo), Is.True);
			Assert.That(passenger.Reserve(actor, cargo), Is.True);
			Assert.That(cargo.CanLoad(actor), Is.True);
			Assert.That(cargo.CanLoad(Unit()), Is.False);
			cargo.Load(boat, actor);
			passenger.Unreserve(actor);
			cargo.Unload(boat);
			Assert.That(cargo.HasSpace(3), Is.True);
		}

		[Test]
		public void InvalidDirectLoadLeavesTheActorInTheWorld()
		{
			var boat = Unit(carrier: true);
			var cargo = Hold(boat, 12, "Infantry");
			var vehicle = Unit();
			Assert.That(cargo.Load(boat, vehicle), Is.False);
			Assert.That(vehicle.IsInWorld, Is.True);
			Assert.That(vehicle.Trait<Passenger>().Transport, Is.Null);
			Assert.That(cargo.PassengerCount, Is.Zero);
		}

		[Test]
		public void OnePassengerCannotBeLoadedByTwoUninitialisedTransports()
		{
			var first = Unit(carrier: true);
			var second = Unit(carrier: true);
			var firstCargo = Hold(first);
			var secondCargo = Hold(second);
			var passenger = Unit();
			Assert.That(firstCargo.Load(first, passenger), Is.True);
			Assert.That(secondCargo.Load(second, passenger), Is.False);
			Assert.That(firstCargo.PassengerCount, Is.EqualTo(1));
			Assert.That(secondCargo.PassengerCount, Is.Zero);
		}

		[Test]
		public void YuriHovercraftDoesNotInheritVehiclePassengerOrders()
		{
			var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
			while (directory != null && !File.Exists(Path.Combine(directory.FullName, "packaging", "nukehour-version.json")))
				directory = directory.Parent;
			Assert.That(directory, Is.Not.Null);
			var rules = File.ReadAllText(Path.Combine(directory.FullName, "mods", "ra2", "rules", "yuri-naval.yaml"));
			StringAssert.Contains("\n\t-Passenger:", rules.Substring(0, rules.IndexOf("\n\n", StringComparison.Ordinal)));
		}
	}
}
