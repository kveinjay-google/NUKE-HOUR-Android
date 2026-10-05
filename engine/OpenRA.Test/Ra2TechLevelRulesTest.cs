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
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class Ra2TechLevelRulesTest
	{
		static readonly IReadOnlyDictionary<string, string> AlliedStructureTiers =
			new Dictionary<string, string>
			{
				{ "gapowr", "infonly" },
				{ "gapile", "infonly" },
				{ "garefn", "infonly" },
				{ "gaweap", "low" },
				{ "gayard", "low" },
				{ "gapill", "low" },
				{ "nasam", "low" },
				{ "gaairc", "medium" },
				{ "amradr", "medium" },
				{ "gadept", "medium" },
				{ "gatech", "unrestricted" },
			};

		static readonly IReadOnlyDictionary<string, string> SovietStructureTiers =
			new Dictionary<string, string>
			{
				{ "napowr", "infonly" },
				{ "nahand", "infonly" },
				{ "narefn", "infonly" },
				{ "naweap", "low" },
				{ "nayard", "low" },
				{ "naflak", "low" },
				{ "nalasr", "low" },
				{ "naradr", "medium" },
				{ "nadept", "medium" },
				{ "natech", "unrestricted" },
			};

		static readonly IReadOnlyDictionary<string, string> YuriStructureTiers =
			new Dictionary<string, string>
			{
				{ "yapowr", "infonly" },
				{ "yabrck", "infonly" },
				{ "yarefn", "infonly" },
				{ "yaweap", "low" },
				{ "yayard", "low" },
				{ "yaggun", "low" },
				{ "yadome", "medium" },
				{ "yadept", "medium" },
				{ "yatech", "unrestricted" },
			};

		[TestCase("mods/ra2/rules")]
		[TestCase("engine/mods/ra2/rules")]
		public void ProductionRootsAndBaseDefensesRespectTheSelectedTechLevel(string relativeRulesPath)
		{
			AssertTiers(relativeRulesPath, "allied-structures.yaml", AlliedStructureTiers);
			AssertTiers(relativeRulesPath, "soviet-structures.yaml", SovietStructureTiers);
			AssertTiers(relativeRulesPath, "yuri-structures.yaml", YuriStructureTiers);
		}

		static void AssertTiers(
			string relativeRulesPath,
			string fileName,
			IReadOnlyDictionary<string, string> expectedTiers)
		{
			var source = File.ReadAllText(Path.Combine(RepositoryRoot(), relativeRulesPath, fileName));
			foreach (var pair in expectedTiers)
			{
				var actor = ActorBlock(source, pair.Key);
				StringAssert.Contains($"~techlevel.{pair.Value}", actor,
					$"{pair.Key} must require the {pair.Value} lobby tech tier.");
			}
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
