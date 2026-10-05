#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is
 * made available under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System;
using System.IO;
using NUnit.Framework;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class Ra2DogAutoAttackRulesTest
	{
		[TestCase("mods/ra2")]
		[TestCase("engine/mods/ra2")]
		public void AlliedDogDetectsChasesAndLeapsAtEnemyInfantryWithinSight(string relativeModPath)
		{
			var rules = Path.Combine(RepositoryRoot(), relativeModPath, "rules");
			var dog = ActorBlock(File.ReadAllText(Path.Combine(rules, "allied-infantry.yaml")), "dog");
			StringAssert.Contains("Armament:", dog);
			StringAssert.Contains("Weapon: DogJaw", dog);
			StringAssert.Contains("AttackLeap:", dog);
			StringAssert.Contains("AutoTarget:", dog);
			StringAssert.Contains("InitialStance: AttackAnything", dog);
			StringAssert.Contains("ScanRadius: 9", dog);
			StringAssert.Contains("MinimumScanTimeInterval: 1", dog);
			StringAssert.Contains("MaximumScanTimeInterval: 3", dog);
			StringAssert.Contains("AutoTargetPriority@DEFAULT:", dog);
			StringAssert.Contains("ValidTargets: Infantry", dog);
			StringAssert.Contains("ValidRelationships: Enemy", dog);

			var infantry = ActorBlock(File.ReadAllText(Path.Combine(rules, "defaults.yaml")), "^Infantry");
			StringAssert.Contains("TargetTypes: Ground, Infantry", infantry);
			StringAssert.Contains("EdibleByLeap:", infantry);

			var weapon = ActorBlock(File.ReadAllText(Path.Combine(
				RepositoryRoot(), relativeModPath, "weapons", "melee.yaml")), "DogJaw");
			StringAssert.Contains("ValidTargets: Infantry", weapon);
		}

		static string ActorBlock(string source, string actor)
		{
			var marker = actor + ":";
			var start = source.IndexOf(marker, StringComparison.Ordinal);
			Assert.That(start, Is.GreaterThanOrEqualTo(0), $"Missing actor {actor}.");

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
