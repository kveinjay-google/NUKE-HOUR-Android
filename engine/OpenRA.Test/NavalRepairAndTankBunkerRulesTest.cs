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

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class NavalRepairAndTankBunkerRulesTest
	{
		[Test]
		public void TankBunkerCanHoldOneStandardVehicle()
		{
			var defaults = ActorBlock(ReadRule("defaults.yaml"), "^Vehicle");
			StringAssert.Contains("Passenger:\n\t\tVoice: Move\n\t\tCargoType: Vehicle\n\t\tWeight: 3", defaults);

			var bunker = ActorBlock(ReadRule("yuri-structures.yaml"), "natbnk");
			StringAssert.Contains("Cargo:\n\t\tTypes: Vehicle\n\t\tMaxWeight: 3", bunker);
		}

		[Test]
		public void TankBunkerExposesAndArmsItsLoadedVehicle()
		{
			var bunker = ActorBlock(ReadRule("yuri-structures.yaml"), "natbnk");
			StringAssert.Contains("Inherits@AUTOTARGET: ^AutoTargetAll", bunker);
			StringAssert.Contains("LoadedCondition: loaded", bunker);
			StringAssert.Contains("WithCargoBuilding:", bunker);
			StringAssert.Contains("DisplayTypes: Vehicle", bunker);
			StringAssert.Contains("AttackOpenTopped:", bunker);
			StringAssert.Contains("WithIdleOverlay@GARRISONED_REAR:", bunker);
			StringAssert.Contains("WithIdleOverlay@GARRISONED_FRONT:", bunker);

			var sequences = ActorBlock(ReadSequence("yuri-structures.yaml"), "natbnk");
			StringAssert.Contains("idle-garrisoned:", sequences);
			StringAssert.Contains("idle-garrisoned-front:", sequences);
		}

		[Test]
		public void LoadedTankTurretIsAimedBeforeItsWeaponFires()
		{
			var source = File.ReadAllText(Path.Combine(RepositoryRoot(),
				"OpenRA.Mods.RA2", "Traits", "Attack", "AttackOpenTopped.cs"));
			StringAssert.Contains("Dictionary<Actor, Turreted[]>", source);
			StringAssert.Contains("FaceTarget(a.Actor, target)", source);
			StringAssert.Contains("((ITick)a).Tick(a.Actor)", source);
		}

		[Test]
		public void TankBunkerRendererBuildsAVisiblePassengerPreview()
		{
			var source = File.ReadAllText(Path.Combine(RepositoryRoot(),
				"OpenRA.Mods.RA2", "Traits", "Render", "WithCargoBuilding.cs"));
			StringAssert.Contains("INotifyPassengerEntered", source);
			StringAssert.Contains("ActorPreviewInitializer", source);
			StringAssert.Contains("IActorPreviewInitModifier", source);
		}

		[Test]
		public void ShipsRepairNearEveryFactionShipyard()
		{
			var ship = ActorBlock(ReadRule("defaults.yaml"), "^Ship");
			AssertRepairableNearAllShipyards(ship);

			foreach (var (file, actor) in new[]
			{
				("allied-naval.yaml", "dlph"),
				("soviet-naval.yaml", "sqd")
			})
				AssertRepairableNearAllShipyards(ActorBlock(ReadRule(file), actor));
		}

		[TestCase("allied-structures.yaml", "gayard")]
		[TestCase("soviet-structures.yaml", "nayard")]
		[TestCase("yuri-structures.yaml", "yayard")]
		public void EveryShipyardProvidesUnitRepairs(string file, string actor)
		{
			StringAssert.Contains("\n\tRepairsUnits:\n", ActorBlock(ReadRule(file), actor));
		}

		static void AssertRepairableNearAllShipyards(string actor)
		{
			StringAssert.Contains("\n\tRepairableNear:\n", actor);
			StringAssert.Contains("\n\t\tCloseEnough: 3c0\n", actor);
			StringAssert.Contains("\n\t\tRepairActors: gayard, nayard, yayard\n", actor);
			StringAssert.DoesNotContain("\n\tRepairable:\n", actor);
		}

		static string ReadRule(string file)
		{
			return File.ReadAllText(Path.Combine(RepositoryRoot(), "mods", "ra2", "rules", file));
		}

		static string ReadSequence(string file)
		{
			return File.ReadAllText(Path.Combine(RepositoryRoot(), "mods", "ra2", "sequences", file));
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
			while (directory != null && !File.Exists(Path.Combine(
				directory.FullName, "packaging", "nukehour-version.json")))
				directory = directory.Parent;

			Assert.That(directory, Is.Not.Null, "Unable to locate the repository root.");
			return directory!.FullName;
		}
	}
}
