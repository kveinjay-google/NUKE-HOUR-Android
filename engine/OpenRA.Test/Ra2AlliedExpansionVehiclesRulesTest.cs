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
	public sealed class Ra2AlliedExpansionVehiclesRulesTest
	{
		[TestCase("mods/ra2")]
		[TestCase("engine/mods/ra2")]
		public void RobotTankHasItsRetailControlCenterAndAmphibiousCombatContract(string relativeModPath)
		{
			var mod = Path.Combine(RepositoryRoot(), relativeModPath);
			if (relativeModPath == "engine/mods/ra2" && !Directory.Exists(mod))
				Assert.Ignore("Optional development mirror is not part of the public Android source tree; mods/ra2 is authoritative.");
			var vehicle = ActorBlock(File.ReadAllText(Path.Combine(mod, "rules", "allied-vehicles.yaml")), "robo");
			StringAssert.Contains("Prerequisites: garobo, ~gaweap", vehicle);
			StringAssert.Contains("Cost: 600", vehicle);
			StringAssert.Contains("HP: 180", vehicle);
			StringAssert.Contains("Locomotor: amphibious", vehicle);
			StringAssert.Contains("Weapon: Robogun", vehicle);
			StringAssert.Contains("Prerequisites: robot-control", vehicle);
			StringAssert.Contains("PauseOnCondition: !robot-controlled", vehicle);
			StringAssert.Contains("VoiceSet: RobotTankVoice", vehicle);
			StringAssert.Contains("TargetTypes: ImmuneToRadiation", vehicle);

			var structure = ActorBlock(File.ReadAllText(Path.Combine(mod, "rules", "allied-structures.yaml")), "garobo");
			StringAssert.Contains("Cost: 600", structure);
			StringAssert.Contains("HP: 600", structure);
			StringAssert.Contains("Amount: -100", structure);
			StringAssert.Contains("Prerequisite: robot-control", structure);
			StringAssert.Contains("RequiresCondition: !build-incomplete && !lowpower", structure);

			var weapon = ActorBlock(File.ReadAllText(Path.Combine(mod, "weapons", "bullets.yaml")), "Robogun");
			StringAssert.Contains("ReloadDelay: 60", weapon);
			StringAssert.Contains("Range: 5c0", weapon);
			StringAssert.Contains("Damage: 65", weapon);

			var voxels = ActorBlock(File.ReadAllText(Path.Combine(mod, "sequences", "voxels.yaml")), "robo");
			StringAssert.Contains("turret: robotur", voxels);
		}

		[TestCase("mods/ra2")]
		[TestCase("engine/mods/ra2")]
		public void BattleFortressCarriesFiveInfantryWhoCanFireFromDistinctPorts(string relativeModPath)
		{
			var mod = Path.Combine(RepositoryRoot(), relativeModPath);
			if (relativeModPath == "engine/mods/ra2" && !Directory.Exists(mod))
				Assert.Ignore("Optional development mirror is not part of the public Android source tree; mods/ra2 is authoritative.");
			var vehicle = ActorBlock(File.ReadAllText(Path.Combine(mod, "rules", "allied-vehicles.yaml")), "bfrt");
			StringAssert.Contains("Prerequisites: gatech, ~gaweap", vehicle);
			StringAssert.Contains("Cost: 2000", vehicle);
			StringAssert.Contains("HP: 600", vehicle);
			StringAssert.Contains("MaxWeight: 5", vehicle);
			StringAssert.Contains("EjectOnDeath: true", vehicle);
			StringAssert.Contains("AttackOpenTopped:", vehicle);
			StringAssert.Contains("Armaments: primary, secondary, elite", vehicle);
			StringAssert.Contains("PortOffsets: 220,-190,90, 220,190,90, -120,-200,80, -120,200,80, 220,0,130", vehicle);
			StringAssert.Contains("Weapon: 20mmrapid", vehicle);
			StringAssert.Contains("VoiceSet: BattleFortressVoice", vehicle);

			var voxels = ActorBlock(File.ReadAllText(Path.Combine(mod, "sequences", "voxels.yaml")), "bfrt");
			StringAssert.Contains("idle:", voxels);
		}

		static string ActorBlock(string source, string actor)
		{
			var marker = actor + ":";
			var start = source.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
			Assert.That(start, Is.GreaterThanOrEqualTo(0), $"Missing actor or definition {actor}.");

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
