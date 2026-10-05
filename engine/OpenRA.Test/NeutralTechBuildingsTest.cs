using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.RA2.Traits;
using OpenRA.Support;
using OpenRA.Traits;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class NeutralTechBuildingsTest
	{
		static string Root()
		{
			var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
			while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, ".git")))
				directory = directory.Parent;
			return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
		}

		static MiniYaml Actor(string file, string actor)
		{
			var node = MiniYaml.FromString(File.ReadAllText(Path.Combine(Root(), "mods/ra2", file)), file)
				.SingleOrDefault(n => n.Key == actor);
			Assert.That(node, Is.Not.Null, "Missing actor: " + actor);
			return node.Value;
		}

		[TestCase("camach", "3, 3", 800)]
		[TestCase("caslab", "3, 3", 1000)]
		[TestCase("capowr", "2, 2", 800)]
		public void NeutralStructuresHaveCaptureInheritanceAndSpriteBindings(string actor, string footprint, int hp)
		{
			var rule = Actor("rules/tech-structures.yaml", actor);
			Assert.That(rule.NodeWithKey("Inherits").Value.Value, Is.EqualTo("^TechBuilding"));
			var building = FieldLoader.Load<BuildingInfo>(rule.NodeWithKey("Building").Value);
			Assert.That(building.Dimensions.ToString(), Is.EqualTo(footprint.Replace(" ", "")));
			Assert.That(rule.NodeWithKey("Health").Value.NodeWithKey("HP").Value.Value, Is.EqualTo(hp.ToString()));
			var sequence = Actor("sequences/tech-structures.yaml", actor);
			Assert.That(sequence.NodeWithKey("Defaults"), Is.Not.Null);
		}

		[Test]
		public void TechPowerPlantUsesOwnerAwareTwoHundredPower()
		{
			var power = FieldLoader.Load<PowerInfo>(Actor("rules/tech-structures.yaml", "capowr").NodeWithKey("Power").Value);
			Assert.That(power.Amount, Is.EqualTo(200));
			Assert.That(typeof(Power).GetInterfaces(), Does.Contain(typeof(INotifyOwnerChanged)));
			Assert.That(typeof(Power).GetInterfaces(), Does.Contain(typeof(INotifyRemovedFromWorld)));
		}

		[TestCase("^Vehicle")]
		[TestCase("^Ship")]
		[TestCase("^Aircraft")]
		public void MechanicalActorsRepairOnlyWhileTheirOwnerHasMachineShop(string actor)
		{
			var rule = Actor("rules/defaults.yaml", actor);
			var grant = FieldLoader.Load<GrantConditionOnPrerequisiteInfo>(rule.NodeWithKey("GrantConditionOnPrerequisite@MACHINE_SHOP").Value);
			Assert.That(grant.Prerequisites, Is.EqualTo(new[] { "machine-shop" }));
			Assert.That(grant.Condition, Is.EqualTo("machine-shop-repair"));
			var heal = rule.NodeWithKey("ChangesHealth@MACHINE_SHOP").Value;
			Assert.That(heal.NodeWithKey("Step").Value.Value, Is.EqualTo("5"));
			Assert.That(heal.NodeWithKey("Delay").Value.Value, Is.EqualTo("75"));
			Assert.That(heal.NodeWithKey("StartIfBelow").Value.Value, Is.EqualTo("100"));
			Assert.That(heal.NodeWithKey("RequiresCondition").Value.Value, Is.EqualTo(grant.Condition));
		}

		[Test]
		public void SecretLabHasDeterministicForeignSelectionAndOwnerLifecycle()
		{
			var type = typeof(Ra2GameRules).Assembly.GetType("OpenRA.Mods.RA2.Traits.SecretLab");
			Assert.That(type, Is.Not.Null, "Missing secret-lab gameplay trait.");
			Assert.That(type.GetInterfaces(), Does.Contain(typeof(ITechTreePrerequisite)));
			Assert.That(type.GetInterfaces(), Does.Contain(typeof(INotifyOwnerChanged)));
			Assert.That(type.GetInterfaces(), Does.Contain(typeof(ISync)));
		}

		[TestCase("america", new[] { "terror", "deso", "yuri", "ttnk", "dtruck" })]
		[TestCase("france", new[] { "terror", "deso", "yuri", "ttnk", "dtruck" })]
		[TestCase("russia", new[] { "snipe", "tnkd", "gtgcan" })]
		[TestCase("iraq", new[] { "snipe", "tnkd", "gtgcan" })]
		[TestCase("yuri", new[] { "snipe", "terror", "deso", "yuri", "tnkd", "ttnk", "dtruck", "gtgcan" })]
		[TestCase("Neutral", new string[0])]
		public void SecretLabCandidatePoolExcludesOwnersFactionSide(string faction, string[] expected)
		{
			var type = typeof(Ra2GameRules).Assembly.GetType("OpenRA.Mods.RA2.Traits.SecretLab");
			Assert.That(type, Is.Not.Null, "Missing secret-lab gameplay trait.");
			var eligible = (string[])type.GetMethod("EligibleUnits").Invoke(null, new object[] { faction });
			Assert.That(eligible, Is.EquivalentTo(expected));
			var select = type.GetMethod("SelectUnit");
			var first = new MersenneTwister(12345);
			var second = new MersenneTwister(12345);
			for (var i = 0; i < 64; i++)
			{
				var picked = (string)select.Invoke(null, new object[] { faction, first });
				Assert.That(picked, Is.EqualTo(select.Invoke(null, new object[] { faction, second })));
				if (expected.Length > 0) Assert.That(expected, Does.Contain(picked));
				else Assert.That(picked, Is.Null);
			}
		}

		[TestCase("snipe", "allied-infantry.yaml", "radar, ~infantry.england")]
		[TestCase("terror", "soviet-infantry.yaml", "naradr, ~infantry.cuba")]
		[TestCase("deso", "soviet-infantry.yaml", "naradr, ~infantry.iraq")]
		[TestCase("yuri", "soviet-infantry.yaml", "natech, ~nahand")]
		[TestCase("tnkd", "allied-vehicles.yaml", "~vehicles.germany")]
		[TestCase("ttnk", "soviet-vehicles.yaml", "~naweap, naradr, ~vehicles.russia")]
		[TestCase("dtruck", "soviet-vehicles.yaml", "naradr, ~vehicles.libya")]
		[TestCase("gtgcan", "allied-structures.yaml", "radar, ~structures.france")]
		public void SecretUnlockPreservesNativeRecipeAndNeedsNoEnemyFactory(string unit, string file, string original)
		{
			var player = Actor("rules/player.yaml", "Player");
			var native = player.NodeWithKey("GrantConditionOnPrerequisite@NATIVE_" + unit.ToUpperInvariant()).Value;
			Assert.That(native.NodeWithKey("Prerequisites").Value.Value, Is.EqualTo(original));
			var permission = player.NodeWithKey("ProvidesPrerequisite@NATIVE_" + unit.ToUpperInvariant()).Value;
			Assert.That(permission.NodeWithKey("Prerequisite").Value.Value, Is.EqualTo("can-build-" + unit));
			Assert.That(permission.NodeWithKey("RequiresCondition").Value.Value,
				Is.EqualTo(native.NodeWithKey("Condition").Value.Value));
			var buildable = FieldLoader.Load<BuildableInfo>(Actor("rules/" + file, unit).NodeWithKey("Buildable").Value);
			Assert.That(buildable.Prerequisites, Is.EqualTo(new[] { "~can-build-" + unit }));
		}

		[Test]
		public void BattleFortressHasDedicatedVehicleCrushingWithoutChangingOrdinaryTanks()
		{
			var bfrt = Actor("rules/allied-vehicles.yaml", "bfrt");
			var mobile = FieldLoader.Load<MobileInfo>(bfrt.NodeWithKey("Mobile").Value);
			Assert.That(mobile.Locomotor, Is.EqualTo("battlefortress"));
			var world = Actor("rules/world.yaml", "^BaseWorld");
			var crusher = FieldLoader.Load<LocomotorInfo>(world.NodeWithKey("Locomotor@BATTLEFORTRESS").Value);
			Assert.That(crusher.Crushes.ToString(), Does.Contain("vehicle"));
			var heavy = FieldLoader.Load<LocomotorInfo>(world.NodeWithKey("Locomotor@HEAVYTRACKED").Value);
			Assert.That(heavy.Crushes.ToString(), Does.Not.Contain("vehicle"));
			Assert.That(bfrt.NodeWithKey("Cargo").Value.NodeWithKey("MaxWeight").Value.Value, Is.EqualTo("5"));
			Assert.That(bfrt.NodeWithKey("AttackOpenTopped"), Is.Not.Null);
		}

		[Test]
		public void BattleFortressMindControlOverlayHasItsRequiredSpriteSequence()
		{
			var rule = Actor("rules/allied-vehicles.yaml", "bfrt");
			Assert.That(rule.NodeWithKey("Inherits@MC").Value.Value, Is.EqualTo("^MindControllable"));
			Assert.That(Actor("sequences/vehicles.yaml", "bfrt").NodeWithKeyOrDefault("Inherits@MC"), Is.Not.Null,
				"Creating Battle Fortress initializes its mindcontrol overlay even before capture.");
		}

		[TestCase("allied-vehicles.yaml", "bfrt")]
		[TestCase("allied-vehicles.yaml", "amcv")]
		[TestCase("soviet-vehicles.yaml", "smcv")]
		[TestCase("yuri-vehicles.yaml", "pcv")]
		[TestCase("yuri-vehicles.yaml", "smin")]
		public void OriginalOmniCrushResistantVehiclesRemainImmune(string file, string unit)
		{
			Assert.That(Actor("rules/" + file, unit).NodeWithKeyOrDefault("-Crushable@BATTLEFORTRESS"), Is.Not.Null);
		}

		[Test]
		public void SecretLabCaptureReselectsForNewOwnerAndNeutralOwnerGetsNothing()
		{
			var actor = (Actor)RuntimeHelpers.GetUninitializedObject(typeof(Actor));
			var world = (World)RuntimeHelpers.GetUninitializedObject(typeof(World));
			typeof(World).GetField("SharedRandom").SetValue(world, new MersenneTwister(763));
			typeof(Actor).GetField("World").SetValue(actor, world);
			var lab = new SecretLab();
			foreach (var faction in new[] { "america", "russia", "yuri", "Neutral" })
			{
				var owner = (Player)RuntimeHelpers.GetUninitializedObject(typeof(Player));
				var info = FieldLoader.Load<FactionInfo>(new MiniYaml(null, new[] { new MiniYamlNode("InternalName", faction) }));
				typeof(Player).GetField("Faction").SetValue(owner, info);
				typeof(Player).GetField("NonCombatant").SetValue(owner, faction == "Neutral");
				typeof(Actor).GetProperty("Owner").SetValue(actor, owner);
				((INotifyOwnerChanged)lab).OnOwnerChanged(actor, null, owner);
				if (faction == "Neutral") Assert.That(lab.ProvidesPrerequisites, Is.Empty);
				else
				{
					Assert.That(SecretLab.EligibleUnits(faction), Does.Contain(lab.UnlockedUnit));
					Assert.That(lab.ProvidesPrerequisites, Is.EqualTo(new[] { "can-build-" + lab.UnlockedUnit }));
				}
			}
		}

		[TestCase(-1)]
		[TestCase(0)]
		[TestCase(7)]
		public void SecretLabSaveDataRestoresTheExactChoiceWithoutRerolling(int index)
		{
			var actor = (Actor)RuntimeHelpers.GetUninitializedObject(typeof(Actor));
			var lab = new SecretLab();
			var save = (IGameSaveTraitData)lab;
			save.ResolveTraitData(actor, new MiniYaml(null, new[] { new MiniYamlNode("SelectedIndex", index.ToString()) }));
			var restored = new SecretLab();
			((IGameSaveTraitData)restored).ResolveTraitData(actor, new MiniYaml(null, save.IssueTraitData(actor)));
			Assert.That(restored.UnlockedUnit, Is.EqualTo(lab.UnlockedUnit));
			Assert.That(restored.ProvidesPrerequisites, Is.EqualTo(lab.ProvidesPrerequisites));
			var hash = typeof(Sync).GetMethod("Hash", BindingFlags.NonPublic | BindingFlags.Static);
			Assert.That(hash.Invoke(null, new object[] { restored }), Is.EqualTo(hash.Invoke(null, new object[] { lab })));
			Assert.That(() => save.ResolveTraitData(actor,
				new MiniYaml(null, new[] { new MiniYamlNode("SelectedIndex", "99") })), Throws.TypeOf<YamlException>());
		}
	}
}
