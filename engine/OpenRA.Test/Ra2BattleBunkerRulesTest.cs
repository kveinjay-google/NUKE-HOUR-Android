// Copyright (c) The OpenRA Developers and Contributors
// This file is part of OpenRA, which is free software. It is made
// available under the terms of the GNU General Public License
// as published by the Free Software Foundation, either version 3 of
// the License, or (at your option) any later version.

using System;
using System.IO;
using NUnit.Framework;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class Ra2BattleBunkerRulesTest
	{
		[TestCase("mods/ra2")]
		[TestCase("engine/mods/ra2")]
		public void SovietBattleBunkerCarriesFiveInfantryWhoCanFireFromDistinctPorts(string relativeModPath)
		{
			var root = RepositoryRoot();
			var mod = Path.Combine(root, relativeModPath);
			var actor = DefinitionBlock(File.ReadAllText(Path.Combine(mod, "rules", "soviet-structures.yaml")), "nabnkr");
			StringAssert.Contains("Queue: Support", actor);
			StringAssert.Contains("Prerequisites: nahand, ~structures.soviets", actor);
			StringAssert.Contains("Cost: 500", actor);
			StringAssert.Contains("HP: 600", actor);
			StringAssert.Contains("Types: Infantry", actor);
			StringAssert.Contains("MaxWeight: 5", actor);
			StringAssert.Contains("EjectOnDeath: true", actor);
			StringAssert.Contains("LoadedCondition: loaded", actor);
			StringAssert.Contains("AttackOpenTopped:", actor);
			StringAssert.Contains("Armaments: garrisoned, garrisoned-secondary", actor);
			StringAssert.Contains("PortOffsets: 768,0,1024, 448,-682,1024, -448,-682,1024, -768,0,1024, -448,682,1024", actor);
			StringAssert.Contains("Sequence: garrisoned", actor);

			var sequence = DefinitionBlock(File.ReadAllText(Path.Combine(mod, "sequences", "soviet-structures.yaml")), "nabnkr");
			StringAssert.Contains("Filename: ngbnkr.shp", sequence);
			StringAssert.Contains("SNOW: nabnkr.shp", sequence);
			StringAssert.Contains("Filename: ngbnkrmk.shp", sequence);
			StringAssert.Contains("Filename: cameomd|bnkricon.shp", sequence);

			var english = File.ReadAllText(Path.Combine(mod, "fluent", "rules.ftl"));
			var chinese = File.ReadAllText(Path.Combine(mod, "fluent", "zh-CN", "rules.ftl"));
			StringAssert.Contains("actor-nabnkr =", english);
			StringAssert.Contains(".name = Battle Bunker", english);
			StringAssert.Contains("actor-nabnkr =", chinese);
			StringAssert.Contains(".name = 战斗碉堡", chinese);
		}

		static string DefinitionBlock(string source, string name)
		{
			var marker = name + ":\n";
			var start = source.IndexOf(marker, StringComparison.Ordinal);
			Assert.That(start, Is.GreaterThanOrEqualTo(0), $"Missing definition {name}.");

			var next = source.IndexOf("\n\n", start, StringComparison.Ordinal);
			return next < 0 ? source[start..] : source[start..next];
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
