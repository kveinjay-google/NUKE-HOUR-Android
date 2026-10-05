#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using OpenRA.Mods.Common.Traits;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class MultiFactoryProductionSpeedTest
	{
		static string RepositoryRoot()
		{
			var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
			while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, ".git")))
				directory = directory.Parent;

			Assert.That(directory, Is.Not.Null, "Unable to locate the repository root.");
			return directory!.FullName;
		}

		[TestCase(100, 50, 86, 43)]
		[TestCase(86, 43, 100, 50)]
		[TestCase(100, 49, 86, 42)]
		[TestCase(100, 1, 50, 1)]
		[TestCase(100, 100, 50, 50)]
		public void RemainingTimePreservesCompletedProgress(
			int oldTotal, int oldRemaining, int newTotal, int expected)
		{
			Assert.That(
				ProductionTimeScaling.RescaleRemainingTime(oldTotal, oldRemaining, newTotal),
				Is.EqualTo(expected));
		}

		[TestCase("Building")]
		[TestCase("Support")]
		[TestCase("Infantry")]
		[TestCase("Vehicle")]
		[TestCase("Ship")]
		public void Ra2SelectedQueuesUseRecommendedMultiFactoryScaling(string queue)
		{
			var path = Path.Combine(RepositoryRoot(), "mods", "ra2", "rules", "player.yaml");
			var player = MiniYaml.FromString(File.ReadAllText(path), path)
				.Single(node => node.Key == "Player");
			var productionQueue = player.Value.NodeWithKey($"ClassicProductionQueue@{queue}");

			Assert.That(productionQueue.Value.NodeWithKey("SpeedUp").Value.Value,
				Is.EqualTo("true").IgnoreCase);
			Assert.That(productionQueue.Value.NodeWithKey("BuildTimeSpeedReduction").Value.Value,
				Is.EqualTo("100, 75, 60, 50, 45, 40"));
		}

		[TestCase("allied-structures.yaml", "gacnst")]
		[TestCase("allied-structures.yaml", "gapile")]
		[TestCase("allied-structures.yaml", "gaweap")]
		[TestCase("allied-structures.yaml", "gayard")]
		[TestCase("soviet-structures.yaml", "nacnst")]
		[TestCase("soviet-structures.yaml", "nahand")]
		[TestCase("soviet-structures.yaml", "naweap")]
		[TestCase("soviet-structures.yaml", "nayard")]
		[TestCase("yuri-structures.yaml", "yacnst")]
		[TestCase("yuri-structures.yaml", "yabrck")]
		[TestCase("yuri-structures.yaml", "yaweap")]
		[TestCase("yuri-structures.yaml", "yayard")]
		public void IncompleteFacilitiesDoNotCountAsUsableProducers(string rulesFile, string actorName)
		{
			var path = Path.Combine(RepositoryRoot(), "mods", "ra2", "rules", rulesFile);
			var actor = MiniYaml.FromString(File.ReadAllText(path), path)
				.Single(node => node.Key == actorName);
			var production = actor.Value.NodeWithKey("Production");

			Assert.That(production.Value.NodeWithKey("RequiresCondition").Value.Value,
				Is.EqualTo("!build-incomplete"));
		}

		[Test]
		public void ActiveProductionRefreshesWhenProducerCountChanges()
		{
			var refresh = typeof(ProductionItem).GetMethod(
				"RefreshBuildTime", BindingFlags.Instance | BindingFlags.Public);
			Assert.That(refresh, Is.Not.Null);

			var root = RepositoryRoot();
			var itemSource = File.ReadAllText(Path.Combine(root, "engine", "OpenRA.Mods.Common",
				"Traits", "Player", "ProductionQueue.cs"));
			var classicSource = File.ReadAllText(Path.Combine(root, "engine", "OpenRA.Mods.Common",
				"Traits", "Player", "ClassicProductionQueue.cs"));
			var parallelSource = File.ReadAllText(Path.Combine(root, "engine", "OpenRA.Mods.Common",
				"Traits", "Player", "ClassicParallelProductionQueue.cs"));

			StringAssert.Contains("ProductionTimeScaling.RescaleRemainingTime", itemSource);
			StringAssert.IsMatch(
				@"if \(Queue\.Count > 0 && !allProductionPaused && info\.SpeedUp\)\s+Queue\[0\]\.RefreshBuildTime\(\);",
				classicSource);
			StringAssert.IsMatch(@"if \(info\.SpeedUp\)\s+item\.RefreshBuildTime\(\);", parallelSource);
		}
	}
}
