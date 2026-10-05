// Copyright (c) The OpenRA Developers and Contributors
// Licensed under the GNU General Public License v3.
using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using OpenRA.Mods.Common.Traits;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class NavalYardPlacementRulesTest
	{
		[TestCase("mods/ra2/rules", "allied-structures.yaml", "gayard")]
		[TestCase("mods/ra2/rules", "soviet-structures.yaml", "nayard")]
		[TestCase("mods/ra2/rules", "yuri-structures.yaml", "yayard")]
		[TestCase("engine/mods/ra2/rules", "allied-structures.yaml", "gayard")]
		[TestCase("engine/mods/ra2/rules", "soviet-structures.yaml", "nayard")]
		[TestCase("engine/mods/ra2/rules", "yuri-structures.yaml", "yayard")]
		public void AllFactionsGetDoubleThePreviousThirteenCellReachAndRetainWaterPlacement(string directory, string file, string actor)
		{
			var root = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
			while (root != null && !File.Exists(Path.Combine(root.FullName, "AGENTS.md"))) root = root.Parent;
			Assert.That(root, Is.Not.Null);
			var rules = MiniYaml.FromFile(Path.Combine(root.FullName, directory, file));
			var nodes = rules.Single(n => n.Key == actor).Value.Nodes;
			var range = nodes.SingleOrDefault(n => n.Key == "RequiresBuildableArea");
			Assert.That(range, Is.Not.Null, "Naval yard must override the land building range.");
			var info = new RequiresBuildableAreaInfo();
			var baseArea = MiniYaml.FromFile(Path.Combine(root.FullName, directory, "defaults.yaml"))
				.Single(n => n.Key == "^BaseBuilding").Value.Nodes.Single(n => n.Key == "RequiresBuildableArea").Value;
			FieldLoader.Load(info, new MiniYaml(null, MiniYaml.Merge(new[] { baseArea.Nodes.ToList(), range.Value.Nodes.ToList() })));
			Assert.That(info.Adjacent, Is.EqualTo(26));
			var building = new BuildingInfo();
			FieldLoader.Load(building, nodes.Single(n => n.Key == "Building").Value);
			Assert.That(building.TerrainTypes, Is.EquivalentTo(new[] { "Water" }));
			Assert.That(building.Dimensions, Is.EqualTo(new CVec(4, 4)));
			Assert.That(nodes.Any(n => n.Key == "-GivesBuildableArea"), Is.True, "Cannot chain shipyards to extend the land base.");
		}
	}
}
