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
	public sealed class DolphinWeaponRulesTest
	{
		[TestCase("mods/ra2/weapons/zaps.yaml")]
		[TestCase("engine/mods/ra2/weapons/zaps.yaml")]
		public void DolphinSonicBeamUsesRetailCadenceAndEnhancedEnemyDamage(string relativePath)
		{
			var source = File.ReadAllText(Path.Combine(RepositoryRoot(), relativePath));
			var weapon = YamlBlock(source, "SonicZap");

			StringAssert.Contains("ReloadDelay: 120", weapon);
			StringAssert.Contains("Range: 6c0", weapon);
			StringAssert.Contains("Warhead@1Dam: SpreadDamage", weapon);
			StringAssert.Contains("Damage: 8", Section(weapon, "Warhead@1Dam", "Warhead@2Dam"));
			StringAssert.Contains("ValidRelationships: Neutral, Enemy", weapon);
			StringAssert.Contains("Damage: 4", Section(weapon, "Warhead@2Dam", null));
			StringAssert.Contains("InvalidTargets: ImmuneToAllySonic", weapon);
		}

		static string Section(string source, string startMarker, string endMarker)
		{
			var start = source.IndexOf(startMarker + ":", StringComparison.Ordinal);
			Assert.That(start, Is.GreaterThanOrEqualTo(0));
			var end = endMarker == null ? source.Length : source.IndexOf(endMarker + ":", start, StringComparison.Ordinal);
			Assert.That(end, Is.GreaterThan(start));
			return source[start..end];
		}

		static string YamlBlock(string source, string name)
		{
			var start = source.IndexOf(name + ":\n", StringComparison.Ordinal);
			Assert.That(start, Is.GreaterThanOrEqualTo(0));
			var end = source.IndexOf("\n\n", start, StringComparison.Ordinal);
			return end < 0 ? source[start..] : source[start..end];
		}

		static string RepositoryRoot()
		{
			var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
			while (directory != null &&
				(!Directory.Exists(Path.Combine(directory.FullName, "mods", "ra2")) ||
					!Directory.Exists(Path.Combine(directory.FullName, "engine"))))
				directory = directory.Parent;

			Assert.That(directory, Is.Not.Null, "Unable to locate repository root.");
			return directory!.FullName;
		}
	}
}
