// Copyright (c) The OpenRA Developers and Contributors
// This file is part of OpenRA, which is free software. It is made
// available to you under the terms of the GNU General Public License
// as published by the Free Software Foundation, either version 3 of
// the License, or (at your option) any later version.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.RA2.Content;
using OpenRA.Primitives;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class ElevatedBridgeRepairRulesTest
	{
		[Test]
		public void ElevatedBridgeSegmentReactsToDamageStateChanges()
		{
			Assert.That(typeof(ElevatedBridgeSegment).GetInterfaces(),
				Does.Contain(typeof(INotifyDamageStateChanged)));
		}

		[TestCase("mods/ra2/rules/bridges.yaml")]
		public void ElevatedBridgeActorsAreConnectedToDamageAndRepairRules(string relativePath)
		{
			var rules = File.ReadAllText(Path.Combine(RepositoryRoot(), relativePath));
			var hut = ActorBlock(rules, "cabhut");
			var common = ActorBlock(rules, "^ElevatedBridgeSegment");
			var bridge1 = ActorBlock(rules, "bridge1");
			var bridge2 = ActorBlock(rules, "bridge2");
			var destroyedBridge1 = ActorBlock(rules, "bridge1.destroyed");
			var destroyedBridge2 = ActorBlock(rules, "bridge2.destroyed");

			StringAssert.Contains("Types: GroundLevelBridge, ElevatedBridge", hut);
			StringAssert.Contains("-2,3", hut);
			StringAssert.Contains("2,-3", hut);

			StringAssert.Contains("Health:\n\t\tHP: 500", common);
			StringAssert.Contains("TargetTypes: Ground, Building, Bridge", common);
			StringAssert.Contains("ElevatedBridgeSegment:", common);
			StringAssert.Contains("ValidDamageStates: Dead", common);
			StringAssert.Contains("RequiresCondition: !bridge-destroyed", common);

			StringAssert.Contains("Inherits: ^ElevatedBridgeSegment", bridge1);
			StringAssert.Contains("FootprintOffsets: 0,-1, 0,0, 0,1", bridge1);
			StringAssert.Contains("NeighbourOffsets: -1,0, 1,0", bridge1);
			StringAssert.Contains("Actor: bridge1.destroyed", bridge1);
			StringAssert.Contains("Inherits: ^ElevatedBridgeSegment", bridge2);
			StringAssert.Contains("FootprintOffsets: -1,0, 0,0, 1,0", bridge2);
			StringAssert.Contains("NeighbourOffsets: 0,-1, 0,1", bridge2);
			StringAssert.Contains("Actor: bridge2.destroyed", bridge2);

			StringAssert.Contains("BridgePlaceholder:", destroyedBridge1);
			StringAssert.Contains("Type: ElevatedBridge", destroyedBridge1);
			StringAssert.Contains("DamageState: Dead", destroyedBridge1);
			StringAssert.Contains("ReplaceWithActor: bridge1", destroyedBridge1);
			StringAssert.Contains("FootprintOffsets: 0,-1, 0,0, 0,1", destroyedBridge1);
			StringAssert.Contains("DisableElevatedBridgeLayer: true", destroyedBridge1);
			StringAssert.Contains("-WithSpriteBody:", destroyedBridge1);

			StringAssert.Contains("BridgePlaceholder:", destroyedBridge2);
			StringAssert.Contains("ReplaceWithActor: bridge2", destroyedBridge2);
			StringAssert.Contains("FootprintOffsets: -1,0, 0,0, 1,0", destroyedBridge2);
		}

		[Test]
		public void ElevatedBridgeSegmentsLeaveRepairablePlaceholdersWhenDestroyed()
		{
			var source = File.ReadAllText(Path.Combine(
				RepositoryRoot(), "engine", "OpenRA.Mods.Common", "Traits", "Buildings",
				"ElevatedBridgeSegment.cs"));

			StringAssert.DoesNotContain("health.RemoveOnDeath = false;", source);
			StringAssert.DoesNotContain("health.Resurrect(self, repairer);", source);
			StringAssert.Contains("e.DamageState == DamageState.Dead", source);
			StringAssert.Contains("elevatedBridgeLayer.DisableBridgeCells(cells);", source);
		}

		[Test]
		public void DestroyedElevatedBridgePlaceholdersOwnTheirWholeFootprintAndMovementState()
		{
			var source = File.ReadAllText(Path.Combine(
				RepositoryRoot(), "engine", "OpenRA.Mods.Common", "Traits", "Buildings", "BridgePlaceholder.cs"));

			StringAssert.Contains("public readonly CVec[] FootprintOffsets", source);
			StringAssert.Contains("public readonly bool DisableElevatedBridgeLayer", source);
			StringAssert.Contains("bridgeLayer.Add(self, cells);", source);
			StringAssert.Contains("elevatedBridgeLayer.DisableBridgeCells(cells);", source);
		}

		[TestCase("mods/ra2/rules/world.yaml")]
		public void Ra2WorldProvidesTheMovementLayerRequiredByImportedBridgeMaps(string relativePath)
		{
			var rules = File.ReadAllText(Path.Combine(RepositoryRoot(), relativePath));
			var world = SectionBlock(rules, "World", "EditorWorld");

			StringAssert.Contains("\n\tElevatedBridgeLayer:", world);
		}

		static string SectionBlock(string source, string section, string nextSection)
		{
			var marker = section + ":";
			var start = source.IndexOf(marker, StringComparison.Ordinal);
			Assert.That(start, Is.GreaterThanOrEqualTo(0), $"Missing section {section}.");

			var next = source.IndexOf("\n" + nextSection + ":", start, StringComparison.Ordinal);
			Assert.That(next, Is.GreaterThan(start), $"Missing section {nextSection}.");
			return source[start..next];
		}

		static string ActorBlock(string source, string actor)
		{
			var marker = actor + ":";
			var start = source.IndexOf(marker, StringComparison.Ordinal);
			Assert.That(start, Is.GreaterThanOrEqualTo(0), $"Missing actor {actor}.");

			var next = source.IndexOf("\n\n", start, StringComparison.Ordinal);
			return next < 0 ? source[start..] : source[start..next];
		}

		[Test]
		public void EngineerCanRepairBuildingsAndTargetBridgeSegmentsDirectly()
		{
			var infantryPath = Path.Combine(RepositoryRoot(), "mods", "ra2", "rules", "allied-infantry.yaml");
			var infantry = File.ReadAllText(infantryPath);
			var engineer = ActorBlock(infantry, "engineer");
			StringAssert.Contains("InstantlyRepairs:", engineer);
			StringAssert.Contains("RepairsBridges:", engineer);

			var defaultsPath = Path.Combine(RepositoryRoot(), "mods", "ra2", "rules", "defaults.yaml");
			var defaults = File.ReadAllText(defaultsPath);
			StringAssert.Contains("InstantlyRepairable:", ActorBlock(defaults, "^Building"));

			var hutSource = File.ReadAllText(Path.Combine(
				RepositoryRoot(), "engine", "OpenRA.Mods.Common", "Traits", "Buildings", "BridgeHut.cs"));
			StringAssert.Contains("ContainsSegmentActor(Actor actor)", hutSource);

			var repairSource = File.ReadAllText(Path.Combine(
				RepositoryRoot(), "engine", "OpenRA.Mods.Common", "Traits", "RepairsBridges.cs"));
			StringAssert.Contains("TryResolveRepairHut", repairSource);
			StringAssert.Contains("Target.FromActor(repairHut)", repairSource);
			StringAssert.Contains("target.TraitOrDefault<IBridgeSegment>()", repairSource);
		}

		[Test]
		public void RetailImporterReconstructsDestroyedElevatedBridgeSpansBetweenRepairHuts()
		{
			// Real MP02T2 bridge geometry captured from an imported retail map:
			// the four destroyed concrete spans between the two intact runs are absent
			// from OverlayPack and must be reconstructed as dead repairable actors.
			var existing = new Dictionary<CPos, string>
			{
				[new CPos(101, -6)] = "bridge1",
				[new CPos(102, -6)] = "bridge1",
				[new CPos(107, -6)] = "bridge1",
				[new CPos(108, -6)] = "bridge1",
			};
			var repairHuts = new[] { new CPos(98, -4), new CPos(111, -8) };

			var reconstructed = FindDestroyedElevatedBridgeSpans(existing, repairHuts);

			Assert.That(reconstructed.Count, Is.EqualTo(4));
			for (var x = 103; x <= 106; x++)
				Assert.That(reconstructed[new CPos(x, -6)], Is.EqualTo("bridge1.destroyed"));
		}

		[Test]
		public void RetailImporterUsesDestroyedVariantsInsteadOfZeroHealthInitializers()
		{
			var source = File.ReadAllText(Path.Combine(
				RepositoryRoot(), "OpenRA.Mods.RA2", "UtilityCommands", "ImportRA2MapCommand.cs"));

			StringAssert.DoesNotContain("new HealthInit(0)", source);
			StringAssert.Contains("DestroyedElevatedBridgeActor", source);
		}

		[Test]
		public void RetailImporterDoesNotJoinElevatedBridgeRunsWithoutTwoRepairHuts()
		{
			var existing = new Dictionary<CPos, string>
			{
				[new CPos(101, -6)] = "bridge1",
				[new CPos(102, -6)] = "bridge1",
				[new CPos(107, -6)] = "bridge1",
				[new CPos(108, -6)] = "bridge1",
			};

			var reconstructed = FindDestroyedElevatedBridgeSpans(
				existing, new[] { new CPos(98, -4) });

			Assert.That(reconstructed, Is.Empty);
		}

		[Test]
		public void RetailImporterBuildsConcreteBridgeMovementGeometry()
		{
			var existing = Enumerable.Range(101, 8).ToDictionary(
				x => new CPos(x, -6), _ => "bridge1");
			var repairHuts = new[] { new CPos(98, -4), new CPos(111, -8) };

			var placeholder = FindElevatedBridgePlaceholders(existing, repairHuts).Single();

			Assert.That(ReadProperty<CPos>(placeholder, "Location"), Is.EqualTo(new CPos(100, -7)));
			Assert.That(ReadProperty<string>(placeholder, "Orientation"), Is.EqualTo("X"));
			Assert.That(ReadProperty<int>(placeholder, "Length"), Is.EqualTo(9));
			Assert.That(ReadProperty<byte>(placeholder, "Height"), Is.EqualTo(6));
		}

		[TestCase(0xed, "bridgb1")]
		[TestCase(0xee, "bridgb2")]
		public void WoodenElevatedBridgeOverlaysAreImported(int overlay, string actor)
		{
			var importer = typeof(RetailMapInstaller).Assembly.GetType(
				"OpenRA.Mods.RA2.UtilityCommands.ImportRA2MapCommand", true);
			var mapping = (IDictionary)importer.GetField("OverlayToActor", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
			Assert.That(mapping[(byte)overlay], Is.EqualTo(actor));
		}

		[TestCase("bridge1", "X")]
		[TestCase("bridge2", "Y")]
		[TestCase("bridgb1", "X")]
		[TestCase("bridgb2", "Y")]
		public void IntactBridgesWithoutRepairHutsStillHaveCompleteMovementGeometry(string actor, string orientation)
		{
			var existing = Enumerable.Range(10, 8).ToDictionary(
				x => orientation == "X" ? new CPos(x, 20) : new CPos(20, x), _ => actor);
			var placeholders = FindElevatedBridgePlaceholders(existing, Array.Empty<CPos>()).ToArray();
			Assert.That(placeholders.Length, Is.EqualTo(1));
			Assert.That(ReadProperty<CPos>(placeholders[0], "Location"),
				Is.EqualTo(orientation == "X" ? new CPos(9, 19) : new CPos(19, 9)));
			Assert.That(ReadProperty<int>(placeholders[0], "Length"), Is.EqualTo(9));
		}

		[Test]
		public void RepairHutNearestInteriorCellDoesNotTruncateAnIntactBridge()
		{
			var existing = Enumerable.Range(10, 8).ToDictionary(x => new CPos(x, 20), _ => "bridge1");
			var placeholder = FindElevatedBridgePlaceholders(existing,
				new[] { new CPos(11, 22), new CPos(16, 18) }).Single();
			Assert.That(ReadProperty<CPos>(placeholder, "Location"), Is.EqualTo(new CPos(9, 19)));
			Assert.That(ReadProperty<int>(placeholder, "Length"), Is.EqualTo(9));
		}

		[TestCase(4)]
		[TestCase(8)]
		[TestCase(12)]
		public void ImportedBridgeDeckUsesActualBankHeight(int height)
		{
			var existing = Enumerable.Range(101, 8).ToDictionary(x => new CPos(x, -6), _ => "bridge1");
			var importer = typeof(RetailMapInstaller).Assembly.GetType(
				"OpenRA.Mods.RA2.UtilityCommands.ImportRA2MapCommand", true);
			var method = importer.GetMethod("FindElevatedBridgePlaceholders", BindingFlags.NonPublic | BindingFlags.Static);
			Assert.That(method.GetParameters().Length, Is.EqualTo(3), "Bridge geometry must consume actual terrain height.");
			Func<CPos, byte> terrainHeight = c => (byte)(c.X == 100 || c.X == 109 ? height : height - 4);
			var result = ((IEnumerable)method.Invoke(null,
				new object[] { existing, Array.Empty<CPos>(), terrainHeight })).Cast<object>().Single();
			Assert.That(ReadProperty<byte>(result, "Height"), Is.EqualTo(height));
		}

		[Test]
		public void IsolatedSurvivingSpanKeepsItsDeckAboveTheRiverbed()
		{
			var existing = new Dictionary<CPos, string> { [new CPos(20, 20)] = "bridge1" };
			var importer = typeof(RetailMapInstaller).Assembly.GetType(
				"OpenRA.Mods.RA2.UtilityCommands.ImportRA2MapCommand", true);
			var method = importer.GetMethod("FindElevatedBridgePlaceholders", BindingFlags.NonPublic | BindingFlags.Static);
			var result = ((IEnumerable)method.Invoke(null, new object[]
			{
				existing, Array.Empty<CPos>(), (Func<CPos, byte>)(_ => 0),
			})).Cast<object>().Single();
			Assert.That(ReadProperty<byte>(result, "Height"), Is.EqualTo(4));
		}

		[TestCase(8, true)]
		[TestCase(4, false)]
		[TestCase(12, false)]
		public void LayerTransitionsRequireTheGroundToMeetTheDeckInBothDirections(int groundHeight, bool reachable)
		{
			// These APIs only need map geometry: no renderer, actors or retail assets.
			var map = (Map)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(Map));
			var grid = new MapGrid(new MiniYaml(null, new[]
			{
				new MiniYamlNode("Type", "RectangularIsometric"),
				new MiniYamlNode("MaximumTerrainHeight", "16"),
			}));
			SetField(map, "Grid", grid);
			var cell = new CPos(10, 5);
			var heights = new CellLayer<byte>(MapGridType.RectangularIsometric, new Size(32, 32));
			heights[cell] = (byte)groundHeight;
			SetField(map, "<Height>k__BackingField", heights);
			SetField(map, "<Ramp>k__BackingField", new CellLayer<byte>(MapGridType.RectangularIsometric, new Size(32, 32)));
			var centers = new CellLayer<WPos>(MapGridType.RectangularIsometric, new Size(32, 32));
			var ground = map.CenterOfCell(cell);
			centers[cell] = new WPos(ground.X, ground.Y, 8 * map.CellHeightStep.Length);
			var layer = (ElevatedBridgeLayer)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(ElevatedBridgeLayer));
			SetField(layer, "map", map);
			SetField(layer, "cellCenters", centers);
			var elevated = new CPos(cell.X, cell.Y, CustomMovementLayerType.ElevatedBridge);
			SetField(layer, "ends", new HashSet<CPos> { cell, elevated });
			var movement = (ICustomMovementLayer)layer;
			var expected = reachable ? (short)0 : OpenRA.Mods.Common.Pathfinder.PathGraph.MovementCostForUnreachableCell;
			Assert.That(movement.EntryMovementCost(null, elevated), Is.EqualTo(expected));
			Assert.That(movement.ExitMovementCost(null, cell), Is.EqualTo(expected));
		}

		static void SetField(object target, string name, object value)
			=> target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(target, value);

		[TestCase("temperat")]
		[TestCase("snow")]
		[TestCase("urban")]
		public void IntactBridgeheadsNeverRandomlySelectCollapsedArt(string theater)
		{
			var yaml = MiniYaml.FromFile(Path.Combine(RepositoryRoot(), "mods", "ra2", "tilesets", theater + ".yaml"));
			var templates = yaml.Single(n => n.Key == "Templates").Value.Nodes;
			var bridgeheads = templates.SelectMany(n => n.Value.Nodes)
				.Where(n => n.Key == "Images" && n.Value.Value.StartsWith("ovrps", StringComparison.Ordinal));
			Assert.That(bridgeheads, Is.Not.Empty);
			foreach (var node in bridgeheads)
				Assert.That(node.Value.Value.Split(',').Any(image =>
					System.Text.RegularExpressions.Regex.IsMatch(image.Trim(), @"^ovrpsb?0[1-6]a\.")),
					Is.False, node.Value.Value + " is a destroyed bridgehead, not a cosmetic variant.");
		}

		[Test]
		public void RetailMapInstallerInvalidatesMapsConvertedBeforeBridgeReconstruction()
		{
			var field = typeof(RetailMapInstaller).GetField(
				"LegacyMapConversionVersion", BindingFlags.NonPublic | BindingFlags.Static);

			Assert.That(field, Is.Not.Null,
				"Changing retail map semantics must invalidate previously converted map packages.");
			Assert.That(field!.GetRawConstantValue(), Is.EqualTo("v5"));
		}

		static IDictionary FindDestroyedElevatedBridgeSpans(
			IReadOnlyDictionary<CPos, string> existing, IReadOnlyCollection<CPos> repairHuts)
		{
			var importer = typeof(RetailMapInstaller).Assembly.GetType(
				"OpenRA.Mods.RA2.UtilityCommands.ImportRA2MapCommand", throwOnError: true);
			var method = importer!.GetMethod(
				"FindDestroyedElevatedBridgeSpans", BindingFlags.NonPublic | BindingFlags.Static);
			Assert.That(method, Is.Not.Null,
				"The retail importer must reconstruct destroyed elevated bridge actors before saving the map.");

			return (IDictionary)method!.Invoke(null, new object[] { existing, repairHuts })!;
		}

		static IEnumerable<object> FindElevatedBridgePlaceholders(
			IReadOnlyDictionary<CPos, string> existing, IReadOnlyCollection<CPos> repairHuts)
		{
			var importer = typeof(RetailMapInstaller).Assembly.GetType(
				"OpenRA.Mods.RA2.UtilityCommands.ImportRA2MapCommand", throwOnError: true);
			var method = importer!.GetMethod(
				"FindElevatedBridgePlaceholders", BindingFlags.NonPublic | BindingFlags.Static);
			Assert.That(method, Is.Not.Null,
				"Imported elevated bridges must define the movement-layer geometry used for pathfinding.");

			var args = method!.GetParameters().Length == 2 ? new object[] { existing, repairHuts } :
				new object[] { existing, repairHuts, (Func<CPos, byte>)(c => c.X == 100 || c.X == 109 ? (byte)6 : (byte)2) };
			return ((IEnumerable)method.Invoke(null, args)!).Cast<object>();
		}

		static T ReadProperty<T>(object value, string property)
		{
			var member = value.GetType().GetProperty(property, BindingFlags.Public | BindingFlags.Instance);
			Assert.That(member, Is.Not.Null, $"Missing {property} on bridge placeholder descriptor.");
			return (T)member!.GetValue(value)!;
		}

		static string RepositoryRoot([CallerFilePath] string sourceFile = "")
		{
			foreach (var candidate in new[]
			{
				Path.GetDirectoryName(sourceFile),
				TestContext.CurrentContext.TestDirectory,
				TestContext.CurrentContext.WorkDirectory,
				Environment.CurrentDirectory,
			})
			{
				var directory = new DirectoryInfo(candidate);
				while (directory != null && !File.Exists(Path.Combine(
					directory.FullName, "packaging", "nukehour-version.json")))
					directory = directory.Parent;

				if (directory != null)
					return directory.FullName;
			}

			Assert.Fail("Unable to locate the repository root.");
			return null;
		}
	}
}
