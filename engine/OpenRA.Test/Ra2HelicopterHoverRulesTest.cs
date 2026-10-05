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
	public sealed class Ra2HelicopterHoverRulesTest
	{
		[TestCase("mods/ra2/rules/aircraft.yaml")]
		[TestCase("engine/mods/ra2/rules/aircraft.yaml")]
		public void NighthawkRemainsAirborneAndStationaryWhenIdle(string relativePath)
		{
			var source = File.ReadAllText(Path.Combine(RepositoryRoot(), relativePath));
			var actor = ActorBlock(source, "shad");

			StringAssert.Contains("\n\t\tIdleBehavior: None", actor);
			StringAssert.Contains("\n\t\tCanHover: true", actor);
			StringAssert.Contains("\n\t\tVTOL: true", actor);
			StringAssert.DoesNotContain("IdleBehavior: Land", actor);
		}

		static string ActorBlock(string source, string actor)
		{
			var start = source.IndexOf(actor + ":\n", StringComparison.Ordinal);
			Assert.That(start, Is.GreaterThanOrEqualTo(0));
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

			Assert.That(directory, Is.Not.Null, "Unable to locate repository root.");
			return directory!.FullName;
		}
	}
}
