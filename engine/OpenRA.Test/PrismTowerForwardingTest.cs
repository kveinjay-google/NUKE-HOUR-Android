// Copyright (c) The OpenRA Developers and Contributors
// This file is part of OpenRA, which is free software. It is made
// available to you under the terms of the GNU General Public License
// as published by the Free Software Foundation, either version 3 of
// the License, or (at your option) any later version.

using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using OpenRA.Mods.RA2.Traits;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class PrismTowerForwardingTest
	{
		[Test]
		public void SupporterSelectionIsEligibleCappedAndDeterministic()
		{
			var candidates = new[]
			{
				new PrismSupportCandidate<string>("far", 1, 900, true),
				new PrismSupportCandidate<string>("near-high-id", 9, 100, true),
				new PrismSupportCandidate<string>("near-low-id", 3, 100, true),
				new PrismSupportCandidate<string>("middle", 5, 400, true),
				new PrismSupportCandidate<string>("busy", 2, 25, false)
			};

			var selected = PrismTowerForwardingPolicy.Select(candidates, 3)
				.Select(candidate => candidate.Value).ToArray();
			var reversed = PrismTowerForwardingPolicy.Select(candidates.Reverse(), 3)
				.Select(candidate => candidate.Value).ToArray();

			Assert.That(selected, Is.EqualTo(new[] { "near-low-id", "near-high-id", "middle" }));
			Assert.That(reversed, Is.EqualTo(selected));
			Assert.That(selected, Does.Not.Contain("busy"));
		}

		[Test]
		public void SupporterSelectionRejectsNegativeLimits()
		{
			Assert.Throws<ArgumentOutOfRangeException>(() =>
				PrismTowerForwardingPolicy.Select(Array.Empty<PrismSupportCandidate<int>>(), -1).ToArray());
		}

		[Test]
		public void SupportCooldownCompensatesForTheForwardingLead()
		{
			Assert.That(PrismTowerForwardingPolicy.SynchronizedSupportReloadDelay(45, 3), Is.EqualTo(48));
		}

		[Test]
		public void PrismTowerAcquiresAndFiresAtNewTargetsPromptlyWithoutIncreasingBurstDamage()
		{
			var tower = Block(Read("mods/ra2/rules/allied-structures.yaml"), "atesla");
			var weapon = Block(Read("mods/ra2/weapons/zaps.yaml"), "PrismShot");
			var sequences = Block(Read("mods/ra2/sequences/allied-structures.yaml"), "atesla");

			StringAssert.Contains("AttackPrismSupported:", tower);
			StringAssert.Contains("ReloadDelay: 45", tower);
			StringAssert.Contains("InitialChargeDelay: 12", tower);
			StringAssert.Contains("AutoTarget:\n\t\tMinimumScanTimeInterval: 1\n\t\tMaximumScanTimeInterval: 4", tower);
			StringAssert.Contains("Range: 8c0", weapon);
			StringAssert.Contains("Damage: 120", weapon);
			StringAssert.Contains("\tactive:\n\t\tFilename: ggpris_a.shp", sequences);
			Assert.That(sequences.Split("Tick: 60", StringSplitOptions.None).Length - 1, Is.EqualTo(2),
				"Normal and damaged charge animations must complete with the shortened firing delay.");
		}

		[Test]
		public void PrismTowerDefinesThreeStageForwardingAndExactDamageMultipliers()
		{
			var tower = Block(Read("mods/ra2/rules/allied-structures.yaml"), "atesla");
			StringAssert.Contains("Armament@Support:\n\t\tName: support\n\t\tWeapon: PrismSupport", tower);
			StringAssert.Contains("AttackPrismSupported:", tower);
			StringAssert.Contains("SupportArmament: support", tower);
			StringAssert.Contains("MaxSupporters: 3", tower);
			StringAssert.Contains("TicksPerHop: 3", tower);
			StringAssert.Contains("BuffCondition: prism-stack", tower);
			StringAssert.Contains("FirepowerMultiplier@PRISM1:\n\t\tModifier: 200\n\t\tRequiresCondition: prism-stack == 1", tower);
			StringAssert.Contains("FirepowerMultiplier@PRISM2:\n\t\tModifier: 300\n\t\tRequiresCondition: prism-stack == 2", tower);
			StringAssert.Contains("FirepowerMultiplier@PRISM3:\n\t\tModifier: 400\n\t\tRequiresCondition: prism-stack >= 3", tower);
		}

		[Test]
		public void PrismTowerRequiresExistingBuildAreaButDoesNotExtendIt()
		{
			var tower = Block(Read("mods/ra2/rules/allied-structures.yaml"), "atesla");
			var baseBuilding = Block(Read("mods/ra2/rules/defaults.yaml"), "^BaseBuilding");

			StringAssert.Contains("Inherits: ^SupportBuilding", tower);
			StringAssert.Contains("RequiresBuildableArea:", baseBuilding);
			StringAssert.Contains("GivesBuildableArea:", baseBuilding);
			StringAssert.Contains("-GivesBuildableArea:", tower);
		}

		[Test]
		public void SupportBeamHasEightCellRangeAndCannotDamageTheReceiver()
		{
			var weapon = Block(Read("mods/ra2/weapons/zaps.yaml"), "PrismSupport");
			StringAssert.Contains("Inherits: PrismShot", weapon);
			StringAssert.Contains("Range: 8c0", weapon);
			StringAssert.Contains("ValidTargets: Ground, Water, Air", weapon);
			StringAssert.Contains("Damage: 0", weapon);
			StringAssert.Contains("-Report:", weapon);
		}

		[Test]
		public void IosAotRegistryConstructsTheForwardingTraitInfo()
		{
			var registry = Read("ios/OpenRA.iOS/GeneratedAotObjectRegistry.g.cs");
			StringAssert.Contains("\"OpenRA.Mods.RA2.Traits.AttackPrismSupportedInfo\"", registry);
			StringAssert.Contains(
				"GeneratedAotObjectRegistry.RegisterBasic(typeof(global::OpenRA.Mods.RA2.Traits.AttackPrismSupportedInfo), static () => new global::OpenRA.Mods.RA2.Traits.AttackPrismSupportedInfo());",
				registry);
		}

		static string Block(string source, string name)
		{
			var marker = name + ":";
			var start = source.IndexOf(marker, StringComparison.Ordinal);
			Assert.That(start, Is.GreaterThanOrEqualTo(0), $"Missing block {name}.");
			var end = source.IndexOf("\n\n", start, StringComparison.Ordinal);
			return end < 0 ? source[start..] : source[start..end];
		}

		static string Read(string relativePath)
		{
			return File.ReadAllText(Path.Combine(RepositoryRoot(), relativePath));
		}

		static string RepositoryRoot()
		{
			var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
			while (directory != null && !File.Exists(Path.Combine(
				directory.FullName, "packaging", "nukehour-version.json")))
				directory = directory.Parent;

			Assert.That(directory, Is.Not.Null, "Unable to locate the repository root.");
			return directory!.FullName;
		}
	}
}
