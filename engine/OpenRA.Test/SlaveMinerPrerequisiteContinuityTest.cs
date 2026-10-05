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
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class SlaveMinerPrerequisiteContinuityTest
	{
		const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
		static T Empty<T>() => (T)FormatterServices.GetUninitializedObject(typeof(T));
		static void Set(object target, string field, object value) => target.GetType().GetField(field, Fields).SetValue(target, value);
		static string Root()
		{
			var p = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
			while (p != null && !File.Exists(Path.Combine(p.FullName, "AGENTS.md"))) p = p.Parent;
			return p?.FullName ?? throw new InvalidOperationException("Repository not found.");
		}

		sealed class Fixture
		{
			public readonly World World = Empty<World>();
			uint nextId;
			public Fixture()
			{
				var traits = typeof(World).GetField("TraitDict", Fields);
				traits.SetValue(World, Activator.CreateInstance(traits.FieldType, true));
			}

			Actor Actor(Player owner, string name, params TraitInfo[] infos)
			{
				var a = Empty<Actor>();
				Set(a, "World", World); Set(a, "ActorID", ++nextId);
				Set(a, "Info", new ActorInfo(name, infos));
				Set(a, "<Owner>k__BackingField", owner);
				Set(a, "<IsInWorld>k__BackingField", true);
				return a;
			}

			public (Player Player, TechTree Tech) Player()
			{
				var p = Empty<Player>();
				Set(p, "<World>k__BackingField", World);
				var faction = new FactionInfo(); Set(faction, "InternalName", "yuri"); Set(p, "Faction", faction);
				var actor = Actor(p, "player"); Set(p, "PlayerActor", actor);
				var tech = new TechTree(new ActorInitializer(actor, new TypeDictionary()));
				actor.AddTrait(tech);
				return (p, tech);
			}

			public Actor Miner(Player owner, string rulesPath, string name)
			{
				var file = name == "smin" ? "yuri-vehicles.yaml" : "yuri-structures.yaml";
				var yaml = MiniYaml.FromFile(Path.Combine(Root(), rulesPath, file)).Single(n => n.Key == name).Value;
				var infos = yaml.Nodes.Where(n => n.Key.Split('@')[0] == "ProvidesPrerequisite")
					.Select(n => FieldLoader.Load<ProvidesPrerequisiteInfo>(n.Value)).ToArray();
				var a = Actor(owner, name, infos);
				foreach (var info in infos) a.AddTrait(info.Create(new ActorInitializer(a, new TypeDictionary())));
				foreach (var trait in a.TraitsImplementing<INotifyCreated>()) trait.Created(a);
				return a;
			}
		}

		static readonly string[] Refinery = { "refinery", "psirefn" };

		[TestCase("mods/ra2/rules")]
		[TestCase("engine/mods/ra2/rules")]
		public void FoldingAndRedeployingRetainRefineryPrerequisitesForTheCurrentOwner(string rulesPath)
		{
			var f = new Fixture(); var owner = f.Player(); var other = f.Player();
			var deployed = f.Miner(owner.Player, rulesPath, "yarefn");
			Assert.That(owner.Tech.HasPrerequisites(Refinery), Is.True);
			Set(deployed, "<IsInWorld>k__BackingField", false);
			var mobile = f.Miner(owner.Player, rulesPath, "smin");
			Assert.That(owner.Tech.HasPrerequisites(Refinery), Is.True, "Automatic folding must retain the refinery unlock.");
			Assert.That(other.Tech.HasPrerequisites(Refinery), Is.False);
			Set(mobile, "<Owner>k__BackingField", other.Player);
			foreach (var trait in mobile.TraitsImplementing<ProvidesPrerequisite>())
				trait.OnOwnerChanged(mobile, owner.Player, other.Player);
			Assert.That(owner.Tech.HasPrerequisites(Refinery), Is.False);
			Assert.That(other.Tech.HasPrerequisites(Refinery), Is.True);
			Set(mobile, "<IsInWorld>k__BackingField", false);
			deployed = f.Miner(other.Player, rulesPath, "yarefn");
			Assert.That(other.Tech.HasPrerequisites(Refinery), Is.True);
			Set(deployed, "<Disposed>k__BackingField", true);
			Assert.That(other.Tech.HasPrerequisites(Refinery), Is.False, "The unlock ends when the last owned miner is destroyed.");
		}
	}
}
