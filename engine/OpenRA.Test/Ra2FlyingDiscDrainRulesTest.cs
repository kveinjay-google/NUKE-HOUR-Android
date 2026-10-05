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
	public sealed class Ra2FlyingDiscDrainRulesTest
	{
		[TestCase("mods/ra2")]
		[TestCase("engine/mods/ra2")]
		public void FlyingDiscCanDisablePowerPlantsAndStealFromRefineries(string relativeModPath)
		{
			var root = RepositoryRoot();
			var mod = Path.Combine(root, relativeModPath);
			var disc = DefinitionBlock(File.ReadAllText(Path.Combine(mod, "rules", "yuri-vehicles.yaml")), "disk");
			StringAssert.Contains("Weapon: DiskDrain", disc);
			StringAssert.Contains("Weapon: DiskSteal", disc);
			StringAssert.Contains("Armaments: primary, secondary, drain, steal", disc);

			var weapons = File.ReadAllText(Path.Combine(mod, "weapons", "yuri.yaml"));
			var drain = DefinitionBlock(weapons, "DiskDrain");
			StringAssert.Contains("ValidTargets: DiskDrain", drain);
			StringAssert.Contains("Warhead@condition: GrantExternalCondition", drain);
			StringAssert.Contains("Condition: diskdrain", drain);
			StringAssert.Contains("Duration: 8", drain);
			var steal = DefinitionBlock(weapons, "DiskSteal");
			StringAssert.Contains("ValidTargets: DiskSteal", steal);
			StringAssert.Contains("Warhead@cash: StealResource", steal);
			StringAssert.Contains("Cash: 10", steal);

			AssertPowerTarget(mod, "allied-structures.yaml", "gapowr");
			AssertPowerTarget(mod, "soviet-structures.yaml", "napowr");
			AssertPowerTarget(mod, "soviet-structures.yaml", "nanrct");
			AssertPowerTarget(mod, "yuri-structures.yaml", "yapowr");
			AssertRefineryTarget(mod, "allied-structures.yaml", "garefn");
			AssertRefineryTarget(mod, "soviet-structures.yaml", "narefn");
			AssertRefineryTarget(mod, "yuri-structures.yaml", "yarefn");

			var defaults = File.ReadAllText(Path.Combine(mod, "rules", "defaults.yaml"));
			var drainable = DefinitionBlock(defaults, "^DiskDrainable");
			StringAssert.Contains("Condition: diskdrain", drainable);
			StringAssert.Contains("TargetTypes: DiskDrain", drainable);
			var stealable = DefinitionBlock(defaults, "^DiskStealable");
			StringAssert.Contains("TargetTypes: DiskSteal", stealable);
		}

		[Test]
		public void StealResourceWarheadTransfersAvailableFundsAndIsRegisteredForIosAot()
		{
			var root = RepositoryRoot();
			var source = File.ReadAllText(Path.Combine(root, "OpenRA.Mods.RA2", "Warheads", "StealResourceWarhead.cs"));
			StringAssert.Contains("class StealResourceWarhead : Warhead", source);
			StringAssert.Contains("Math.Min(Cash, targetResources.GetCashAndResources())", source);
			StringAssert.Contains("targetResources.TakeCash(stolen)", source);
			StringAssert.Contains("sourceResources.GiveCash(stolen)", source);
			StringAssert.Contains("IsValidAgainst(target.Actor, source)", source);

			var registry = File.ReadAllText(Path.Combine(root, "ios", "OpenRA.iOS", "GeneratedAotObjectRegistry.g.cs"));
			StringAssert.Contains("OpenRA.Mods.RA2.Warheads.StealResourceWarhead", registry);
		}

		static void AssertPowerTarget(string mod, string file, string actor)
		{
			var block = DefinitionBlock(File.ReadAllText(Path.Combine(mod, "rules", file)), actor);
			StringAssert.Contains("Inherits@DISK: ^DiskDrainable", block);
			StringAssert.Contains("RequiresCondition: !power-outage && !diskdrain", block);
		}

		static void AssertRefineryTarget(string mod, string file, string actor)
		{
			var block = DefinitionBlock(File.ReadAllText(Path.Combine(mod, "rules", file)), actor);
			StringAssert.Contains("Inherits@DISK: ^DiskStealable", block);
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
