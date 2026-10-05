// Copyright (c) The OpenRA Developers and Contributors
// This file is part of OpenRA, which is free software. It is made
// available under the terms of the GNU General Public License
// as published by the Free Software Foundation, either version 3 of
// the License, or (at your option) any later version.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class Ra2SubmarineAutoAttackRulesTest
	{
		[TestCase("mods/ra2")]
		[TestCase("engine/mods/ra2")]
		public void AttackSubmarineUsesProactiveHumanAndAiStances(string relativeModPath)
		{
			var mod = Path.Combine(RepositoryRoot(), relativeModPath);
			var submarine = DefinitionBlock(File.ReadAllText(Path.Combine(mod, "rules", "soviet-naval.yaml")), "sub");
			AssertProactive(submarine);
			StringAssert.Contains("Weapon: SubTorpedo", submarine);
			StringAssert.Contains("TargetTypes: Underwater, Submergeable", submarine);

			var boomer = DefinitionBlock(File.ReadAllText(Path.Combine(mod, "rules", "yuri-naval.yaml")), "bsub");
			AssertProactive(boomer);
			StringAssert.Contains("Weapon: BoomerTorpedo", boomer);
			StringAssert.Contains("TargetTypes: Underwater, Submergeable", boomer);
		}

		[TestCase("mods/ra2")]
		[TestCase("engine/mods/ra2")]
		public void EveryArmedYuriNavalUnitCanAutomaticallyEngageItsIntendedTargets(string relativeModPath)
		{
			var mod = Path.Combine(RepositoryRoot(), relativeModPath);
			var navalRules = File.ReadAllText(Path.Combine(mod, "rules", "yuri-naval.yaml"));
			var navalActors = TopLevelDefinitions(navalRules)
				.Where(definition => definition.Value.Contains("Queue: Ship", StringComparison.Ordinal))
				.ToDictionary(definition => definition.Key, definition => definition.Value);

			CollectionAssert.AreEquivalent(new[] { "yhvr", "bsub" }, navalActors.Keys,
				"The Yuri fleet changed. Classify every new naval unit as armed or intentionally unarmed here.");

			var transport = navalActors["yhvr"];
			StringAssert.Contains("Cargo:", transport);
			StringAssert.DoesNotContain("Armament", transport,
				"The Yuri amphibious transport is intentionally unarmed and must not receive invented weapons.");

			var boomer = navalActors["bsub"];
			AssertProactive(boomer);
			StringAssert.Contains("AttackFrontal:", boomer);
			StringAssert.Contains("Weapon: BoomerTorpedo", boomer);
			StringAssert.Contains("Weapon: BoomerTorpedoE", boomer);
			StringAssert.Contains("Name: secondary", boomer);
			StringAssert.Contains("Weapon: BoomerLauncher", boomer);

			var sharedWeapons = File.ReadAllText(Path.Combine(mod, "weapons", "missiles.yaml"));
			var yuriWeapons = File.ReadAllText(Path.Combine(mod, "weapons", "yuri.yaml"));
			var submarineTorpedo = DefinitionBlock(sharedWeapons, "SubTorpedo");
			StringAssert.Contains("ValidTargets: Water, Underwater", submarineTorpedo);
			var boomerLauncher = DefinitionBlock(yuriWeapons, "BoomerLauncher");
			StringAssert.Contains("ValidTargets: Ground, Water", boomerLauncher);
		}

		static void AssertProactive(string actor)
		{
			StringAssert.Contains("Inherits@AUTOTARGET: ^AutoTargetGroundAssaultMove", actor);
			StringAssert.Contains("AutoTarget:", actor);
			StringAssert.Contains("InitialStance: Defend", actor);
			StringAssert.Contains("InitialStanceAI: AttackAnything", actor);
			StringAssert.DoesNotContain("InitialStance: ReturnFire", actor);
		}

		static string DefinitionBlock(string source, string actor)
		{
			var marker = actor + ":\n";
			var start = source.IndexOf(marker, StringComparison.Ordinal);
			Assert.That(start, Is.GreaterThanOrEqualTo(0), $"Missing actor {actor}.");
			var next = source.IndexOf("\n\n", start, StringComparison.Ordinal);
			return next < 0 ? source[start..] : source[start..next];
		}

		static IReadOnlyDictionary<string, string> TopLevelDefinitions(string source)
		{
			var definitions = new Dictionary<string, string>(StringComparer.Ordinal);
			var starts = source.Split('\n')
				.Select((line, index) => (line, index))
				.Where(entry => entry.line.Length > 1 &&
					!char.IsWhiteSpace(entry.line[0]) && entry.line.EndsWith(":", StringComparison.Ordinal))
				.ToArray();

			var lines = source.Split('\n');
			for (var i = 0; i < starts.Length; i++)
			{
				var start = starts[i].index;
				var end = i + 1 < starts.Length ? starts[i + 1].index : lines.Length;
				var name = starts[i].line[..^1];
				definitions.Add(name, string.Join('\n', lines[start..end]));
			}

			return definitions;
		}

		static string RepositoryRoot()
		{
			var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
			while (directory != null &&
				(!Directory.Exists(Path.Combine(directory.FullName, "mods", "ra2")) ||
					!Directory.Exists(Path.Combine(directory.FullName, "engine"))))
				directory = directory.Parent;

			Assert.That(directory, Is.Not.Null, "Unable to locate the repository root.");
			return directory!.FullName;
		}
	}
}
