#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software. It is
 * made available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System;
using System.IO;
using NUnit.Framework;
using OpenRA.Mods.Common.Activities;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class HarvesterAutonomyTest
	{
		static string RepositoryRoot()
		{
			var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
			while (directory != null &&
				(!Directory.Exists(Path.Combine(directory.FullName, "mods", "ra2")) ||
				!Directory.Exists(Path.Combine(directory.FullName, "engine", "OpenRA.Mods.Common"))))
				directory = directory.Parent;

			Assert.That(directory, Is.Not.Null, "Unable to locate the repository root.");
			return directory!.FullName;
		}

		[Test]
		public void ResourceSearchFallsBackFromLocalRadiusToTheCompleteMap()
		{
			Assert.That(HarvesterSearchPolicy.SearchRadiusSquared(12, true),
				Is.EqualTo(new int?[] { 144, null }));
		}

		[Test]
		public void ResourceSearchDoesNotRepeatAnAlreadyGlobalSearch()
		{
			Assert.That(HarvesterSearchPolicy.SearchRadiusSquared(0, true),
				Is.EqualTo(new int?[] { null }));
			Assert.That(HarvesterSearchPolicy.SearchRadiusSquared(12, false),
				Is.EqualTo(new int?[] { 144 }));
		}

		[TestCase("allied-vehicles.yaml", "cmin:")]
		[TestCase("soviet-vehicles.yaml", "harv:")]
		public void VehicleHarvestersResumeAutomaticallyAndChooseNearestReachableOre(
			string fileName, string actorName)
		{
			var rules = File.ReadAllText(Path.Combine(RepositoryRoot(), "mods", "ra2", "rules", fileName));
			var actor = rules.Substring(rules.IndexOf(actorName, StringComparison.Ordinal));
			var nextActor = actor.IndexOf('\n');
			while (nextActor >= 0)
			{
				nextActor = actor.IndexOf('\n', nextActor + 1);
				if (nextActor < 0 || (nextActor + 1 < actor.Length && actor[nextActor + 1] != '\t'))
					break;
			}

			if (nextActor >= 0)
				actor = actor.Substring(0, nextActor);

			StringAssert.Contains("SearchEntireMapOnFailure: true", actor);
			StringAssert.Contains("SearchOnIdle: true", actor);
			StringAssert.Contains("ResourceRefineryDirectionPenalty: 0", actor);
		}

		[Test]
		public void YuriMobileMinerRetainsItsGlobalNearestFieldMigration()
		{
			var rules = File.ReadAllText(Path.Combine(
				RepositoryRoot(), "mods", "ra2", "rules", "yuri-vehicles.yaml"));
			var actor = rules.Substring(rules.IndexOf("smin:", StringComparison.Ordinal));
			StringAssert.Contains("AutoSlaveMiner:", actor);
			StringAssert.Contains("SearchRadius: 0", actor);
		}
	}
}
