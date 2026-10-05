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
	public sealed class Ra2VehicleDamageSmokeTest
	{
		[TestCase("mods/ra2/rules/defaults.yaml", "mods/ra2/sequences/misc.yaml")]
		[TestCase("engine/mods/ra2/rules/defaults.yaml", "engine/mods/ra2/sequences/misc.yaml")]
		public void HeavyAndCriticalVehiclesContinuouslyEmitImportedGreySmoke(
			string rulesPath, string sequencesPath)
		{
			var vehicle = ActorBlock(Read(rulesPath), "^Vehicle");
			StringAssert.Contains("GrantConditionOnDamageState@HEAVY:", vehicle);
			StringAssert.Contains("Condition: heavy-damage", vehicle);
			StringAssert.Contains("ValidDamageStates: Heavy, Critical", vehicle);
			StringAssert.Contains("FloatingSpriteEmitter@DAMAGE_SMOKE:", vehicle);
			StringAssert.Contains("RequiresCondition: heavy-damage", vehicle);
			StringAssert.Contains("Image: smokey2", vehicle);
			StringAssert.Contains("Sequences: idle", vehicle);
			StringAssert.Contains("Duration: -1", vehicle);

			var smoke = ActorBlock(Read(sequencesPath), "smokey2");
			StringAssert.Contains("idle:", smoke);
			StringAssert.Contains("Filename: smokey2.shp", smoke);
		}

		static string Read(string relativePath)
		{
			return File.ReadAllText(Path.Combine(RepositoryRoot(), relativePath));
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
