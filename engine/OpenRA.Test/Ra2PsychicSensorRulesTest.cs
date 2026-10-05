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
	public sealed class Ra2PsychicSensorRulesTest
	{
		[TestCase("mods/ra2")]
		[TestCase("engine/mods/ra2")]
		public void PsychicSensorRevealsEnemyOrdersWithinRetailRange(string relativeModPath)
		{
			var root = RepositoryRoot();
			var actor = ActorBlock(File.ReadAllText(Path.Combine(
				root, relativeModPath, "rules", "soviet-structures.yaml")), "napsis");

			StringAssert.Contains("PsychicSensor:", actor);
			StringAssert.Contains("Range: 15c0", actor);
			StringAssert.Contains("RequiresCondition: !lowpower && !build-incomplete", actor);
			StringAssert.Contains("DetectCloaked:", actor);
			Assert.That(Count(actor, "Range: 15c0"), Is.GreaterThanOrEqualTo(2));

			var source = File.ReadAllText(Path.Combine(root, "OpenRA.Mods.RA2", "Traits", "PsychicSensor.cs"));
			StringAssert.Contains("IRenderAnnotations", source);
			StringAssert.Contains("CurrentActivity", source);
			StringAssert.Contains("TargetLineNodes", source);
			StringAssert.Contains("PlayerRelationship.Enemy", source);
			StringAssert.Contains("HorizontalLengthSquared", source);
			StringAssert.Contains("IsTraitDisabled", source);

			var registry = File.ReadAllText(Path.Combine(root, "ios", "OpenRA.iOS", "GeneratedAotObjectRegistry.g.cs"));
			StringAssert.Contains("OpenRA.Mods.RA2.Traits.PsychicSensorInfo", registry);
		}

		static int Count(string source, string value)
		{
			var count = 0;
			var offset = 0;
			while ((offset = source.IndexOf(value, offset, StringComparison.Ordinal)) >= 0)
			{
				count++;
				offset += value.Length;
			}

			return count;
		}

		static string ActorBlock(string source, string actor)
		{
			var marker = actor + ":\n";
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
