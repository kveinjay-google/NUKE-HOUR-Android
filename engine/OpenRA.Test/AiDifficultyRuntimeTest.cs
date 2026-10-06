using System;
using System.Reflection;
using System.Collections.Generic;
using OpenRA.Mods.Common.Traits.BotModules.Squads;
using System.Runtime.Serialization;
using NUnit.Framework;
using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;
using OpenRA.Support;

namespace OpenRA.Test
{
	[TestFixture]
	public class AiDifficultyRuntimeTest
	{
		const BindingFlags Fields = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
		static T Empty<T>() => (T)FormatterServices.GetUninitializedObject(typeof(T));
		static void Field(object target, string name, object value) => target.GetType().GetField(name, Fields).SetValue(target, value);
		World world;
		uint id;

		[SetUp]
		public void Setup()
		{
			world = Empty<World>();
			var traits = typeof(World).GetField("TraitDict", Fields);
			traits.SetValue(world, Activator.CreateInstance(traits.FieldType, true));
		}

		Actor Actor(Player owner)
		{
			var actor = Empty<Actor>();
			Field(actor, "World", world);
			Field(actor, "ActorID", ++id);
			Field(actor, "<Owner>k__BackingField", owner);
			return actor;
		}

		Player Player(string difficulty, out PlayerResources resources)
		{
			var player = Empty<Player>();
			Field(player, "AiDifficulty", AiDifficultyCatalog.Resolve(difficulty, ""));
			Field(player, "IsBot", difficulty != null);
			var actor = Actor(player);
			Field(player, "PlayerActor", actor);
			resources = Empty<PlayerResources>();
			var info = new PlayerResourcesInfo();
			info.ResourceValues["ore"] = 25;
			Field(resources, "Info", info);
			Field(resources, "owner", player);
			resources.ResourceCapacity = 10000;
			actor.AddTrait(resources);
			return player;
		}

		[TestCase(false)]
		[TestCase(true)]
		public void ActualRefineryDeliveryBoostsOnlyMinedValueAndUsesNewOwner(bool storage)
		{
			var bot = Player("difficulty-nightmare", out var botResources);
			var human = Player(null, out var humanResources);
			var actor = Actor(bot);
			var info = new RefineryInfo();
			Field(info, "UseStorage", storage);
			Field(info, "ShowTicks", false);
			var refinery = new Refinery(actor, info);
			((INotifyCreated)refinery).Created(actor);
			Assert.That(((IAcceptResources)refinery).AcceptResources(actor, "ore", 2), Is.EqualTo(2));
			Assert.That(botResources.GetCashAndResources(), Is.EqualTo(100));
			botResources.GiveCash(50); // refunds and transfers use the original resource API
			Assert.That(botResources.GetCashAndResources(), Is.EqualTo(150));
			Field(actor, "<Owner>k__BackingField", human);
			((INotifyOwnerChanged)refinery).OnOwnerChanged(actor, bot, human);
			Assert.That(((IAcceptResources)refinery).AcceptResources(actor, "ore", 2), Is.EqualTo(2));
			Assert.That(humanResources.GetCashAndResources(), Is.EqualTo(50));
			Assert.That(botResources.GetCashAndResources(), Is.EqualTo(150));
		}

		[Test]
		public void RefineryStorageCountsOnlyAcceptedOreAtBoostedValue()
		{
			var bot = Player("difficulty-nightmare", out var resources);
			resources.ResourceCapacity = 75;
			var actor = Actor(bot);
			var refinery = new Refinery(actor, new RefineryInfo());
			((INotifyCreated)refinery).Created(actor);
			Assert.That(((IAcceptResources)refinery).AcceptResources(actor, "ore", 2), Is.EqualTo(1));
			Assert.That(resources.Resources, Is.EqualTo(50));
		}

		[Test]
		public void ActualProductionQueueUsesCurrentOwnerAndNeverReducesCost()
		{
			var bot = Player("difficulty-nightmare", out _);
			var human = Player(null, out _);
			var actor = Actor(bot);
			var queue = Empty<ProductionQueue>();
			Field(queue, "<Actor>k__BackingField", actor);
			Field(queue, "Info", new ProductionQueueInfo());
			Field(queue, "developerMode", Empty<DeveloperMode>());
			var buildable = new BuildableInfo();
			Field(buildable, "BuildDuration", 90);
			Field(buildable, "BuildDurationModifier", 100);
			var valued = new ValuedInfo();
			Field(valued, "Cost", 500);
			var unit = new ActorInfo("unit", new TraitInfo[] { buildable, valued });
			Assert.That(queue.GetBuildTime(unit, buildable), Is.EqualTo(60));
			Assert.That(queue.GetProductionCost(unit), Is.EqualTo(500));
			Field(actor, "<Owner>k__BackingField", human);
			Assert.That(queue.GetBuildTime(unit, buildable), Is.EqualTo(90));
			Assert.That(queue.GetProductionCost(unit), Is.EqualTo(500));
		}
		[Test]
		public void CustomPlayerNameUsesFrozenSnapshotLiterally()
		{
			var player = Player(AiDifficultyCatalog.CustomBotType, out _);
			var custom = AiDifficultyCatalog.CreateCustom("normal");
			custom.Name = "我的快攻电脑";
			Field(player, "BotType", AiDifficultyCatalog.CustomBotType);
			Field(player, "AiDifficulty", custom);
			var name = typeof(Player).GetMethod("ResolvePlayerName", Fields).Invoke(player, Array.Empty<object>());
			Assert.That(name, Is.EqualTo(custom.Name));
		}

		[TestCase("beginner", 4, 3000)]
		[TestCase("normal", 10, 1500)]
		[TestCase("nightmare", 30, 250)]
		public void SquadModuleUsesActualWaveSizeAndInitialAttackDelay(string difficulty, int wave, int delay)
		{
			var player = Player("difficulty-" + difficulty, out _);
			Field(world, "LocalRandom", new MersenneTwister(1));
			Field(world, "Timestep", 40);
			var module = Empty<SquadManagerBotModule>();
			Field(module, "World", world);
			Field(module, "Player", player);
			Field(module, "Info", new SquadManagerBotModuleInfo());
			Field(module, "Squads", new List<Squad>());
			Field(module, "notifyIdleBaseUnits", Array.Empty<IBotNotifyIdleBaseUnits>());
			var idle = new List<Actor>();
			for (var i = 0; i < wave - 1; i++)
				idle.Add(Actor(player));
			Field(module, "unitsHangingAroundTheBase", idle);
			var bot = Empty<ModularBot>();
			Field(bot, "player", player);
			typeof(SquadManagerBotModule).GetMethod("TraitEnabled", Fields).Invoke(module, new object[] { player.PlayerActor });
			Assert.That(typeof(SquadManagerBotModule).GetField("minAttackForceDelayTicks", Fields).GetValue(module), Is.EqualTo(delay));
			var create = typeof(SquadManagerBotModule).GetMethod("CreateAttackForce", Fields);
			create.Invoke(module, new object[] { bot });
			Assert.That(module.Squads, Is.Empty);
			idle.Add(Actor(player));
			create.Invoke(module, new object[] { bot });
			Assert.That(module.Squads.Count, Is.EqualTo(1));
			Assert.That(module.Squads[0].Units.Count, Is.EqualTo(wave));
			Assert.That(idle, Is.Empty);
		}

		[Test]
		public void BaseExpansionAffectsCapacityWithoutChangingSharedInfo()
		{
			var info = new BaseBuilderBotModuleInfo();
			info.ProductionTypes.Add("factory");
			var beginner = Empty<BaseBuilderBotModule>();
			Field(beginner, "Info", info);
			Field(beginner, "player", Player("difficulty-beginner", out _));
			var nightmare = Empty<BaseBuilderBotModule>();
			Field(nightmare, "Info", info);
			Field(nightmare, "player", Player("difficulty-nightmare", out _));
			Assert.That(beginner.BuildingLimit("factory", 6), Is.EqualTo(1));
			Assert.That(nightmare.BuildingLimit("factory", 6), Is.EqualTo(4));
			Assert.That(nightmare.NewProductionCashThreshold, Is.LessThan(beginner.NewProductionCashThreshold));
			Assert.That(nightmare.StructureDecisionDelay(true), Is.LessThan(beginner.StructureDecisionDelay(true)));
			Assert.That(nightmare.StructureDecisionDelay(true), Is.GreaterThanOrEqualTo(10));
			Assert.That(info.NewProductionCashThreshold, Is.EqualTo(5000));
		}

	}
}
