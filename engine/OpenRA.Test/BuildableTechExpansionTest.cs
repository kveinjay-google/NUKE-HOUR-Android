using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using OpenRA.Mods.Common.Traits;
using OpenRA.Network;
using OpenRA.Traits;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class BuildableTechExpansionTest
	{
		IReadOnlyDictionary<string, MiniYaml> rules;

		static string Root()
		{
			var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
			while (directory != null)
			{
				var git = Path.Combine(directory.FullName, ".git");
				if ((Directory.Exists(git) || File.Exists(git)) && Directory.Exists(Path.Combine(directory.FullName, "mods/ra2")))
					return directory.FullName;
				directory = directory.Parent;
			}

			throw new InvalidOperationException("Repository root not found.");
		}

		[OneTimeSetUp]
		public void LoadResolvedRules()
		{
			var root = Root();
			var manifest = MiniYaml.FromFile(Path.Combine(root, "mods/ra2/mod.yaml"));
			var sources = manifest.Single(n => n.Key == "Rules").Value.Nodes.Select(n =>
			{
				Assert.That(n.Key, Does.StartWith("ra2|"));
				return MiniYaml.FromFile(Path.Combine(root, "mods/ra2", n.Key[4..]));
			});
			rules = MiniYaml.Merge(sources).ToDictionary(n => n.Key, n => n.Value, StringComparer.OrdinalIgnoreCase);
		}

		MiniYaml Rule(string actor)
		{
			Assert.That(rules.ContainsKey(actor), Is.True, "Missing resolved actor: " + actor);
			return rules[actor];
		}

		static MiniYaml Trait(MiniYaml rule, string trait)
		{
			var node = rule.NodeWithKeyOrDefault(trait);
			Assert.That(node, Is.Not.Null, "Missing trait: " + trait);
			return node.Value;
		}

		LobbyPrerequisiteCheckboxInfo Option() => FieldLoader.Load<LobbyPrerequisiteCheckboxInfo>(
			Trait(Rule("Player"), "LobbyPrerequisiteCheckbox@TECH_EXPANSION"));

		[Test]
		public void RoomOptionIsVisibleEditableAndDefaultsOff()
		{
			var info = Option();
			Assert.Multiple(() =>
			{
				Assert.That(info.ID, Is.EqualTo("buildtech"));
				Assert.That(info.Enabled, Is.False);
				Assert.That(info.Visible, Is.True);
				Assert.That(info.Locked, Is.False);
				Assert.That(info.Prerequisites, Is.EquivalentTo(new[] { "global-buildtech" }));
			});
		}

		[TestCase(null, false)]
		[TestCase("False", false)]
		[TestCase("True", true)]
		public void SynchronizedLobbyOptionControlsActualRuntimePrerequisite(string value, bool enabled)
		{
			var info = Option();
			var session = new Session();
			if (value != null)
				session.GlobalSettings.LobbyOptions[info.ID] = new Session.LobbyOptionState { Value = value, PreferredValue = value };
			var synchronized = Session.Deserialize(session.Serialize(), "buildable-tech-test");
			var orders = (OrderManager)RuntimeHelpers.GetUninitializedObject(typeof(OrderManager));
			orders.LobbyInfo = synchronized;
			var world = (World)RuntimeHelpers.GetUninitializedObject(typeof(World));
			typeof(World).GetField("OrderManager", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(world, orders);
			var actor = (Actor)RuntimeHelpers.GetUninitializedObject(typeof(Actor));
			typeof(Actor).GetField("World").SetValue(actor, world);
			var checkbox = new LobbyPrerequisiteCheckbox(info);
			((INotifyCreated)checkbox).Created(actor);
			Assert.That(((ITechTreePrerequisite)checkbox).ProvidesPrerequisites,
				Is.EquivalentTo(enabled ? new[] { "global-buildtech" } : Array.Empty<string>()));
		}

		[TestCase("gatech")]
		[TestCase("natech")]
		[TestCase("yatech")]
		public void EachFactionsCompletedBattleLabProvidesExpansionTech(string actor)
		{
			var info = FieldLoader.Load<ProvidesPrerequisiteInfo>(Trait(Rule(actor), "ProvidesPrerequisite@TECH_EXPANSION"));
			Assert.That(info.Prerequisite, Is.EqualTo("expansion-tech"));
			Assert.That(info.RequiresCondition, Is.Not.Null);
			Assert.That(info.RequiresCondition.Evaluate(new Dictionary<string, int> { ["build-incomplete"] = 1 }), Is.False);
			Assert.That(info.RequiresCondition.Evaluate(new Dictionary<string, int> { ["build-incomplete"] = 0 }), Is.True);
			Assert.That(info.Factions, Is.Empty, "Captured battle labs must grant the same prerequisite.");
		}

		[TestCase("camach", 1500)]
		[TestCase("caslab", 2500)]
		[TestCase("capowr", 1000)]
		public void BuildableVariantsHaveSeparateLimitedRecipesAndKeepNeutralFacilities(string original, int cost)
		{
			var neutral = Rule(original);
			var variant = Rule(original + ".buildable");
			var build = FieldLoader.Load<BuildableInfo>(Trait(variant, "Buildable"));
			Assert.Multiple(() =>
			{
				Assert.That(build.Queue, Is.EquivalentTo(new[] { "Building" }));
				Assert.That(build.Prerequisites, Is.EquivalentTo(new[] { "~global-buildtech", "expansion-tech", "~techlevel.unrestricted" }));
				Assert.That(build.BuildLimit, Is.EqualTo(1));
				Assert.That(build.IconPalette, Is.EqualTo("player"));
				Assert.That(build.IconPaletteIsPlayerPalette, Is.True);
				Assert.That(FieldLoader.Load<ValuedInfo>(Trait(variant, "Valued")).Cost, Is.EqualTo(cost));
				Assert.That(Trait(variant, "RenderSprites").NodeWithKey("Image").Value.Value, Is.EqualTo(original));
				Assert.That(neutral.NodeWithKeyOrDefault("Buildable"), Is.Null);
				Assert.That(neutral.NodeWithKeyOrDefault("Sellable"), Is.Null);
			});
			Trait(neutral, "Capturable");
			Trait(variant, "Capturable");
			Trait(variant, "RepairableBuilding");
			Trait(variant, "Sellable");
			Assert.That(FieldLoader.Load<RequiresBuildableAreaInfo>(Trait(variant, "RequiresBuildableArea")).AreaTypes,
				Does.Contain("building"));
			Assert.That(Trait(variant, "OwnerLostAction").NodeWithKey("Action").Value.Value, Is.EqualTo("Kill"));
			Trait(variant, "MustBeDestroyed");
			var benefit = original == "camach" ? "ProvidesPrerequisite" : original == "caslab" ? "SecretLab" : "Power";
			Assert.That(Trait(variant, benefit).Nodes.WriteToString(),
				Is.EqualTo(Trait(neutral, benefit).Nodes.WriteToString()));
		}
		sealed class RecipeState : ITechTreeElement
		{
			public bool Available;
			public bool Hidden;
			public void PrerequisitesAvailable(string key) => Available = true;
			public void PrerequisitesUnavailable(string key) => Available = false;
			public void PrerequisitesItemHidden(string key) => Hidden = true;
			public void PrerequisitesItemVisible(string key) => Hidden = false;
		}

		[TestCase("camach")]
		[TestCase("caslab")]
		[TestCase("capowr")]
		public void ActualTechTreeGatesRecipeAndReopensAfterLabOrBuildLimitRecovery(string original)
		{
			var actor = original + ".buildable";
			var build = FieldLoader.Load<BuildableInfo>(Trait(Rule(actor), "Buildable"));
			var state = new RecipeState();
			// Exercise the engine watcher with the resolved recipe, without requiring retail assets or a renderer.
			var watcherType = typeof(TechTree).GetNestedType("Watcher", BindingFlags.NonPublic);
			var watcher = Activator.CreateInstance(watcherType, actor, build.Prerequisites, build.BuildLimit, state);
			var update = watcherType.GetMethod("Update");
			var owned = new Dictionary<string, int> { ["techlevel.unrestricted"] = 1 };
			void Check(bool available, bool hidden, string reason)
			{
				update.Invoke(watcher, new object[] { owned });
				Assert.That(state.Available, Is.EqualTo(available), reason);
				Assert.That(state.Hidden, Is.EqualTo(hidden), reason);
			}

			Check(false, true, "Default-off room hides the recipe.");
			owned["expansion-tech"] = 1;
			Check(false, true, "A battle lab cannot bypass the room option.");
			owned["global-buildtech"] = 1;
			Check(true, false, "Enabled room with a completed battle lab allows production.");
			owned.Remove("expansion-tech");
			Check(false, false, "Losing the last battle lab disables new production.");
			owned["expansion-tech"] = 1;
			owned[original] = 2;
			Check(true, false, "Captured neutral originals do not consume the buildable variant limit.");
			owned[actor] = 1;
			Check(false, false, "One built variant reaches its per-owner limit.");
			owned.Remove(actor);
			Check(true, false, "Losing or selling the variant permits rebuilding.");
			owned.Remove("techlevel.unrestricted");
			Check(false, true, "Restricted technology rooms hide the recipe.");
		}

		[TestCase("camach")]
		[TestCase("caslab")]
		[TestCase("capowr")]
		public void SidebarIconUsesExistingBuildingFirstFrame(string image)
		{
			var sequences = MiniYaml.FromFile(Path.Combine(Root(), "mods/ra2/sequences/tech-structures.yaml"));
			var sequence = sequences.Single(n => n.Key == image).Value;
			var icon = Trait(sequence, "icon");
			var filename = icon.NodeWithKeyOrDefault("Filename")?.Value.Value
				?? Trait(sequence, "Defaults").NodeWithKey("Filename").Value.Value;
			Assert.That(filename, Is.EqualTo(image + ".shp"));
			Assert.That(icon.NodeWithKeyOrDefault("Start")?.Value.Value ?? "0", Is.EqualTo("0"));
			Assert.That(icon.NodeWithKeyOrDefault("Length")?.Value.Value ?? "1", Is.EqualTo("1"));
		}

	}
}
