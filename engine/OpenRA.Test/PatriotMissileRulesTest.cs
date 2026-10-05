// Copyright (c) The OpenRA Developers and Contributors
// This file is part of OpenRA, which is free software. It is made
// available to you under the terms of the GNU General Public License
// as published by the Free Software Foundation, either version 3 of
// the License, or (at your option) any later version.

using System;
using System.IO;
using NUnit.Framework;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class PatriotMissileRulesTest
	{
		[TestCase("mods/ra2/weapons/missiles.yaml")]
		[TestCase("engine/mods/ra2/weapons/missiles.yaml")]
		public void PatriotUsesEnhancedCadenceDamageAndGuidance(string relativePath)
		{
			var patriot = YamlBlock(relativePath, "RedEye2:");
			StringAssert.Contains("\n\tReloadDelay: 40\n", patriot,
				"The Patriot should fire materially faster than the retail ROF=55 baseline.");
			StringAssert.Contains("\n\tRange: 12c0\n", patriot,
				"Retail RedEye2 uses a twelve-cell attack range.");
			StringAssert.Contains("\n\tWarhead@1Dam: SpreadDamage\n\t\tDamage: 100\n", patriot,
				"The Patriot should hit harder than the retail Damage=75 baseline.");
			StringAssert.Contains("\n\t\tArm: 2\n", patriot,
				"The retail AAHeatSeeker arms after two ticks.");
			StringAssert.Contains("\n\t\tSpeed: 140\n", patriot,
				"The NUKE HOUR missile should be moderately faster than the retail Speed=100 baseline.");
			StringAssert.Contains("\n\t\tLockOnProbability: 100\n", patriot);
			StringAssert.Contains("\n\t\tLockOnInaccuracy: 0\n", patriot);
			StringAssert.Contains("\n\t\tHorizontalRateOfTurn: 256\n", patriot);
			StringAssert.Contains("\n\t\tVerticalRateOfTurn: 256\n", patriot);
			StringAssert.Contains("\n\t\tAllowSnapping: true", patriot,
				"A fast missile must snap through its final sub-tick travel instead of overshooting the target.");
		}

		[TestCase("mods/ra2/rules/allied-structures.yaml")]
		[TestCase("engine/mods/ra2/rules/allied-structures.yaml")]
		public void PatriotActivelyScansItsEntireAttackRange(string relativePath)
		{
			var patriot = YamlBlock(relativePath, "nasam:");
			StringAssert.Contains("\n\tInherits@AUTOTARGET: ^AutoTargetAir\n", patriot);
			StringAssert.Contains("\n\t\tWeapon: RedEye2\n", patriot);
			StringAssert.Contains("\n\tRevealsShroud:\n\t\tRange: 12c0\n", patriot,
				"AutoTarget ignores unseen enemies, so the Patriot must reveal its complete weapon range.");
			StringAssert.Contains("\n\tAutoTarget:\n\t\tScanRadius: 12\n\t\tMinimumScanTimeInterval: 1\n" +
				"\t\tMaximumScanTimeInterval: 3\n\t\tInitialStance: AttackAnything\n", patriot,
				"The stationary air-defense battery must promptly engage enemies without a manual attack order.");
		}

		[TestCase("mods/ra2/rules/defaults.yaml", "mods/ra2/rules/allied-infantry.yaml")]
		[TestCase("engine/mods/ra2/rules/defaults.yaml", "engine/mods/ra2/rules/allied-infantry.yaml")]
		public void AirTargetContractIncludesAircraftAndAirborneInfantry(string defaultsPath, string infantryPath)
		{
			var autoTarget = YamlBlock(defaultsPath, "^AutoTargetAir:");
			StringAssert.Contains("\n\t\tValidTargets: Air\n", autoTarget);

			var rocketeer = YamlBlock(infantryPath, "jumpjet:");
			StringAssert.Contains("\n\tTargetable@airborne:\n\t\tTargetTypes: Air, Disguise\n", rocketeer,
				"Flying infantry must participate in the same Air target contract as aircraft.");
		}

		static string YamlBlock(string relativePath, string marker)
		{
			var source = File.ReadAllText(Path.Combine(RepositoryRoot(), relativePath));
			var start = source.IndexOf(marker + "\n", StringComparison.Ordinal);
			Assert.That(start, Is.GreaterThanOrEqualTo(0), $"Missing YAML block {marker} in {relativePath}.");
			var end = source.IndexOf("\n\n", start, StringComparison.Ordinal);
			return end < 0 ? source[start..] : source[start..end];
		}

		static string RepositoryRoot()
		{
			var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
			while (directory != null && (!Directory.Exists(Path.Combine(directory.FullName, ".git")) ||
				!Directory.Exists(Path.Combine(directory.FullName, "mods", "ra2"))))
				directory = directory.Parent;

			return directory?.FullName ?? throw new InvalidOperationException("Could not locate the repository root.");
		}
	}
}
