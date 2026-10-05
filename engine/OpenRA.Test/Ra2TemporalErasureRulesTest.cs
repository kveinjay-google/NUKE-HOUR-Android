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
	public sealed class Ra2TemporalErasureRulesTest
	{
		[TestCase("mods/ra2/rules/defaults.yaml")]
		[TestCase("engine/mods/ra2/rules/defaults.yaml")]
		public void TargetsAccumulateRecoverableTemporalErasure(string relativePath)
		{
			var defaults = Read(relativePath);
			var chrono = DefinitionBlock(defaults, "^ChronoDisable");
			StringAssert.Contains("AffectedByTemporal:", chrono);
			StringAssert.Contains("Condition: chronodisable", chrono);
			StringAssert.Contains("RevokeDelay: 8", chrono);
			StringAssert.Contains("RecoveryRate: 8", chrono);
			StringAssert.Contains("EraseDamageMultiplier: 500", chrono);
			StringAssert.Contains("EraseDamageTypes: Temporal", chrono);
			StringAssert.Contains("WithColoredOverlay@ChronoDisable:", chrono);
			StringAssert.Contains("Color: 60C8FF70", chrono);
			StringAssert.Contains("RejectsOrders@ChronoDisable:", chrono);
			StringAssert.Contains("RemoveOrders: true", chrono);
			StringAssert.Contains("Targetable@ChronoDisable:", chrono);
			StringAssert.Contains("TargetTypes: ChronoDisable", chrono);

			var building = DefinitionBlock(defaults, "^Building");
			StringAssert.Contains("Inherits@chrono: ^ChronoDisable", building);
			StringAssert.Contains("RequiresCondition: !chronodisable", building);
			StringAssert.DoesNotContain("DamageTypes: Temporal", building);
			StringAssert.DoesNotContain("DeathTypes: Temporal", building);
		}

		[TestCase("mods/ra2/weapons/zaps.yaml")]
		[TestCase("engine/mods/ra2/weapons/zaps.yaml")]
		public void NeutronRifleDealsContinuousTemporalDamage(string relativePath)
		{
			var weapon = DefinitionBlock(Read(relativePath), "NeutronRifle");
			StringAssert.Contains("ReloadDelay: 1", weapon);
			StringAssert.Contains("ValidTargets: ChronoDisable", weapon);
			StringAssert.Contains("Warhead@Temporal: TemporalDamage", weapon);
			StringAssert.Contains("Damage: 8", weapon);
			StringAssert.Contains("DamageTypes: Temporal", weapon);
			StringAssert.DoesNotContain("SpreadDamage", weapon);
			StringAssert.DoesNotContain("GrantExternalCondition", weapon);
		}

		[Test]
		public void RuntimeAddsConcurrentBeamsRecoversGraduallyAndCreditsTheKill()
		{
			var trait = Read("OpenRA.Mods.RA2/Traits/AffectedByTemporal.cs");
			var warhead = Read("OpenRA.Mods.RA2/Warheads/TemporalDamageWarhead.cs");
			var registry = Read("ios/OpenRA.iOS/GeneratedAotObjectRegistry.g.cs");

			StringAssert.Contains("(long)temporalDamage + damage", trait);
			StringAssert.Contains("self.CancelActivity();", trait);
			StringAssert.Contains("Math.Max(0, temporalDamage - Info.RecoveryRate)", trait);
			StringAssert.Contains("self.Kill(lastDamager", trait);
			StringAssert.Contains("victim.TraitOrDefault<AffectedByTemporal>()", warhead);
			StringAssert.Contains("affected.AddDamage(damage, firedBy, DamageTypes)", warhead);
			StringAssert.Contains("OpenRA.Mods.RA2.Traits.AffectedByTemporalInfo", registry);
			StringAssert.Contains("OpenRA.Mods.RA2.Warheads.TemporalDamageWarhead", registry);
		}

		static string Read(string relativePath)
		{
			return File.ReadAllText(Path.Combine(RepositoryRoot(), relativePath));
		}

		static string DefinitionBlock(string source, string name)
		{
			var marker = name + ":";
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
