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
	public sealed class Ra2SpecialUnitBehaviorTest
	{
		[TestCase("mods/ra2/rules/player.yaml")]
		[TestCase("engine/mods/ra2/rules/player.yaml")]
		public void EnemyDefeatHasSpeechAndLocalizedScreenNotification(string relativePath)
		{
			var rules = Read(relativePath);
			StringAssert.Contains("EnemyDefeatedNotification: PlayerDefeated", rules);
			StringAssert.Contains("EnemyDefeatedTextNotification: notification-enemy-defeated", rules);
			var source = Read("engine/OpenRA.Mods.Common/Traits/Player/ConquestVictoryConditions.cs");
			StringAssert.Contains("localPlayer.RelationshipWith(player) == PlayerRelationship.Enemy", source);
			StringAssert.Contains("defeatNotified = true", source);
			StringAssert.Contains("AddTransientLine(localPlayer, info.EnemyDefeatedTextNotification)", source);
		}
		[TestCase("mods/ra2/rules/allied-structures.yaml")]
		public void AirforceCommandSupportsFourPersistentAircraftSlots(string relativePath)
		{
			var airfield = ActorBlock(Read(relativePath), "gaairc");
			StringAssert.Contains("Reservable:\n\t\tCapacity: 4", airfield);
			StringAssert.Contains("EvictYieldingReservations: false", airfield);
		}

		[Test]
		public void AircraftReserveTheSpecificAirfieldExitTheyUse()
		{
			var returnToBase = Read("engine/OpenRA.Mods.Common/Activities/Air/ReturnToBase.cs");
			var aircraft = Read("engine/OpenRA.Mods.Common/Traits/Air/Aircraft.cs");
			var production = Read("engine/OpenRA.Mods.Common/Traits/Production.cs");

			StringAssert.Contains("Reservable.IsSlotAvailableFor(dest, self, e)", returnToBase);
			StringAssert.Contains("MakeReservation(dest, exit)", returnToBase);
			StringAssert.Contains("MakeReservation(host, exit)", aircraft);
			StringAssert.Contains("Reservable.IsSlotAvailableFor(self, null, exit)", production);
		}

		[TestCase("mods/ra2/rules/yuri-structures.yaml")]
		public void SpyCanStealCashFromYuriRefinery(string relativePath)
		{
			var refinery = ActorBlock(Read(relativePath), "yarefn");
			StringAssert.Contains("InfiltrateForCash:", refinery);
			StringAssert.Contains("Types: SpyInfiltrate", refinery);
			StringAssert.Contains("Percentage: 50", refinery);
			StringAssert.Contains("Minimum: 500", refinery);
		}

		[TestCase("mods/ra2/rules/allied-infantry.yaml")]
		public void ChronoLegionnaireUsesTeleportForOrdinaryMovement(string relativePath)
		{
			var chronoLegionnaire = ActorBlock(Read(relativePath), "cleg");
			StringAssert.Contains("UseForNormalMove: true", chronoLegionnaire);
			StringAssert.Contains("HasDistanceLimit: false", chronoLegionnaire);
		}

		[TestCase("mods/ra2/rules/soviet-structures.yaml")]
		[TestCase("engine/mods/ra2/rules/soviet-structures.yaml")]
		public void IronCurtainUsesItsEffectForTargetingAndActivation(string relativePath)
		{
			var actor = ActorBlock(Read(relativePath), "nairon");
			StringAssert.Contains("EffectImage: explosion", actor);
			StringAssert.Contains("EffectSequence: iron_fx", actor);
			StringAssert.Contains("EffectPalette: effect", actor);
			StringAssert.Contains("AnimateTarget: true", actor);
			StringAssert.Contains("Duration: 500", actor);
			var source = Read("engine/OpenRA.Mods.Common/Traits/SupportPowers/GrantExternalConditionPower.cs");
			StringAssert.Contains("targetAnimation?.Tick()", source);
			StringAssert.Contains("new SpriteEffect(a, w, info.EffectImage", source);
		}

		[TestCase("mods/ra2/rules/allied-structures.yaml")]
		[TestCase("engine/mods/ra2/rules/allied-structures.yaml")]
		public void ChronoshiftTargetUsesLoopingWarpAnimationAndNeverTheIconPalette(string relativePath)
		{
			var source = Read("engine/OpenRA.Mods.Cnc/Traits/SupportPowers/ChronoshiftPower.cs");
			StringAssert.Contains("public readonly bool UseFootprintOutline = false", source);
			StringAssert.Contains("new PolygonAnnotationRenderable", source);
			StringAssert.DoesNotContain("wr.Palette(power.Info.IconPalette)", source,
				"World-space target overlays must never use the support-power icon palette.");

			var chronosphere = ActorBlock(Read(relativePath), "gacsph");
			StringAssert.Contains("ChronoshiftPower@chronoshift:", chronosphere);
			StringAssert.Contains("TargetAnimationImage: chrono", chronosphere);
			StringAssert.Contains("TargetAnimationSequence: warpout", chronosphere);
			StringAssert.DoesNotContain("UseFootprintOutline: true", chronosphere);
			StringAssert.Contains("animation.PlayRepeating(info.TargetAnimationSequence)", source);
			StringAssert.Contains("targetAnimation?.Tick()", source);
		}

		[TestCase("mods/ra2/rules/yuri-vehicles.yaml")]
		[TestCase("engine/mods/ra2/rules/yuri-vehicles.yaml")]
		public void FlyingDiscStaysAirborneWhenIdleAndCannotBeForceLanded(string relativePath)
		{
			var flyingDisc = ActorBlock(Read(relativePath), "disk");
			StringAssert.Contains("IdleBehavior: None", flyingDisc);
			StringAssert.Contains("TakeOffOnCreation: true", flyingDisc);
			StringAssert.Contains("CanForceLand: false", flyingDisc);
			StringAssert.DoesNotContain("LandableTerrainTypes:", flyingDisc);
		}

		[TestCase("mods/ra2/rules/yuri-vehicles.yaml")]
		[TestCase("engine/mods/ra2/rules/yuri-vehicles.yaml")]
		public void FlyingDiscContinuouslyRotatesItsVoxelTurret(string relativePath)
		{
			var flyingDisc = ActorBlock(Read(relativePath), "disk");
			StringAssert.Contains("SpinsTurret:", flyingDisc);
			StringAssert.Contains("Speed: 8", flyingDisc);
			StringAssert.Contains("WithVoxelTurret:", flyingDisc);

			var source = Read("engine/OpenRA.Mods.Common/Traits/SpinsTurret.cs");
			StringAssert.Contains("turret.Rotate(Info.Speed);", source);
		}

		[TestCase("mods/ra2/rules/yuri-structures.yaml")]
		[TestCase("engine/mods/ra2/rules/yuri-structures.yaml")]
		public void BioReactorAcceptsFiveInfantryAndAddsOneHundredPowerPerOccupant(string relativePath)
		{
			var reactor = ActorBlock(Read(relativePath), "yapowr");
			StringAssert.Contains("Cargo:\n\t\tTypes: Infantry\n\t\tMaxWeight: 5", reactor);
			StringAssert.Contains("LoadedCondition: loaded", reactor);
			StringAssert.Contains("WithCargoPipsDecoration:", reactor);

			for (var i = 1; i <= 5; i++)
			{
				StringAssert.Contains($"Power@OCCUPANT{i}:\n\t\tAmount: 100", reactor);
				StringAssert.Contains($"RequiresCondition: loaded >= {i} && !power-outage", reactor);
			}

			var infantryDefault = ActorBlock(Read("mods/ra2/rules/defaults.yaml"), "^Infantry");
			StringAssert.Contains("Passenger:\n\t\tVoice: Move\n\t\tCargoType: Infantry", infantryDefault);
		}

		[TestCase("mods/ra2/rules/aircraft.yaml")]
		[TestCase("engine/mods/ra2/rules/aircraft.yaml")]
		public void ParadropPlaneHasEnoughFlightPathToReleaseOriginalSquadsOneSoldierAtATime(string relativePath)
		{
			var plane = ActorBlock(Read(relativePath), "pdplane");
			StringAssert.Contains("DropRange: 8c0", plane,
				"The drop lane must be long enough to spread out the nine-soldier Soviet airport squad.");
			StringAssert.Contains("DropInterval: 8", plane,
				"Each passenger needs visible separation from the preceding parachute.");
		}

		[TestCase("mods/ra2/rules/allied-structures.yaml", "amradr", 8)]
		[TestCase("engine/mods/ra2/rules/allied-structures.yaml", "amradr", 8)]
		[TestCase("mods/ra2/rules/tech-structures.yaml", "caairp", 15)]
		[TestCase("engine/mods/ra2/rules/tech-structures.yaml", "caairp", 15)]
		public void ParadropSupportPowersKeepOriginalInfantryTotals(string relativePath, string actor, int dropItemCount)
		{
			var supportPower = ActorBlock(Read(relativePath), actor);
			Assert.That(CountDropItems(supportPower), Is.EqualTo(dropItemCount));
		}

		[TestCase("mods/ra2/rules/allied-infantry.yaml")]
		[TestCase("engine/mods/ra2/rules/allied-infantry.yaml")]
		public void AmericanGiFacesItsTargetBeforePlayingTheFireAnimation(string relativePath)
		{
			var gi = ActorBlock(Read(relativePath), "e1");
			StringAssert.Contains("AttackFrontal:\n\t\tVoice: Attack\n\t\tRequiresCondition: undeployed\n\t\tFacingTolerance: 0\n\t\tWaitForFacingBeforeAttack: true", gi);
			StringAssert.Contains("AttackTurreted@deployed:\n\t\tArmaments: deployed, deployed-elite\n\t\tTurrets: deploy\n\t\tVoice: Attack\n\t\tRequiresCondition: deployed\n\t\tWaitForFacingBeforeAttack: true", gi);
			StringAssert.Contains("Armament@deployed:\n\t\tName: deployed\n\t\tWeapon: para\n\t\tTurret: deploy", gi);
			StringAssert.Contains("Armament@elite-deployed:\n\t\tName: deployed-elite\n\t\tWeapon: paraE\n\t\tTurret: deploy", gi);
			StringAssert.Contains("Turreted:\n\t\tTurret: deploy\n\t\tRealignDelay: -1\n\t\tTurnSpeed: 1023", gi);
		}

		[Test]
		public void AttackFacingWaitIsOptInAndCoversFrontalAndTurretedAttackPaths()
		{
			var attackBase = Read("engine/OpenRA.Mods.Common/Traits/Attack/AttackBase.cs");
			var frontalAttack = Read("engine/OpenRA.Mods.Common/Activities/Attack.cs");
			var turretedAttack = Read("engine/OpenRA.Mods.Common/Traits/Attack/AttackTurreted.cs");

			StringAssert.Contains("public readonly bool WaitForFacingBeforeAttack = false", attackBase);
			StringAssert.Contains("Info.WaitForFacingBeforeAttack && facingChanged", frontalAttack);
			StringAssert.Contains("Info.WaitForFacingBeforeAttack && turretMoved", turretedAttack);
		}

		[Test]
		public void PortableChronoCanOverrideNormalMoveWithoutChangingLegacyDefault()
		{
			var source = Read("engine/OpenRA.Mods.Cnc/Traits/PortableChrono.cs");
			StringAssert.Contains("public readonly bool UseForNormalMove = false", source);
			StringAssert.Contains("useForNormalMove || modifiers.HasModifier(TargetModifiers.ForceMove)", source);
		}

		[Test]
		public void StolenTechnologyForcesImmediateTechTreeRefresh()
		{
			var source = Read("engine/OpenRA.Mods.Cnc/Traits/Infiltration/InfiltrateForSupportPower.cs");
			StringAssert.Contains("CreateActor(info.Proxy", source);
			StringAssert.Contains("Trait<TechTree>().Update()", source);
		}

		[TestCase("mods/ra2/rules/")]
		public void SpyVeterancyUnitsExposeTheMatchingProductionBadge(string rulesRoot)
		{
			AssertVeterancyBadges(rulesRoot + "allied-infantry.yaml",
				"barracks.infiltrated", "e1", "snipe", "ghost", "tany", "jumpjet", "cleg");
			AssertVeterancyBadges(rulesRoot + "soviet-infantry.yaml",
				"barracks.infiltrated", "e2", "flakt", "shk", "deso");
			AssertVeterancyBadges(rulesRoot + "allied-vehicles.yaml",
				"warfactory.infiltrated", "mtnk", "tnkd", "fv", "sref", "mgtk");
			AssertVeterancyBadges(rulesRoot + "soviet-vehicles.yaml",
				"warfactory.infiltrated", "htk", "htnk", "apoc", "ttnk");
		}

		[TestCase("mods/ra2/rules/yuri-structures.yaml")]
		public void YuriProductionBuildingsGrantSpyVeterancyPrerequisites(string relativePath)
		{
			var source = Read(relativePath);
			var barracks = ActorBlock(source, "yabrck");
			var warFactory = ActorBlock(source, "yaweap");

			StringAssert.Contains("Proxy: barracks.infiltrated", barracks);
			StringAssert.Contains("TargetTypes: Ground, C4, DetonateAttack, Structure, SpyInfiltrate", barracks);
			StringAssert.Contains("Proxy: warfactory.infiltrated", warFactory);
			StringAssert.Contains("TargetTypes: Ground, C4, DetonateAttack, Structure, SpyInfiltrate", warFactory);
		}

		[Test]
		public void SpyVeterancyChangesBothProductionPresentationAndSpawnedRank()
		{
			var producible = Read("engine/OpenRA.Mods.Common/Traits/ProducibleWithLevel.cs");
			var overlay = Read("engine/OpenRA.Mods.Common/Traits/Render/ProductionIconOverlayManager.cs");

			StringAssert.Contains("HasPrerequisites(info.Prerequisites)", producible);
			StringAssert.Contains("ge.GiveLevels(info.InitialLevels", producible);
			StringAssert.Contains("overlayActive[ai] = true", overlay);
		}

		[Test]
		public void OnlyYuriPrimeCanMindControlEnemyBaseBuildings()
		{
			var defaults = Read("mods/ra2/rules/defaults.yaml");
			var mindControllableBuilding = ActorBlock(defaults, "^MindControllableBuilding");
			var baseBuilding = ActorBlock(defaults, "^BaseBuilding");
			var weapons = Read("mods/ra2/weapons/misc.yaml");
			var regularMindControl = ActorBlock(weapons, "MindControl");
			var superMindControl = ActorBlock(weapons, "SuperMindControl");
			var yuriPrime = ActorBlock(Read("mods/ra2/rules/soviet-infantry.yaml"), "yuripr");

			StringAssert.Contains("MindControllable:", mindControllableBuilding);
			StringAssert.Contains("Condition: controlled", mindControllableBuilding);
			StringAssert.Contains("TargetTypes: MindControlBuilding", mindControllableBuilding);
			StringAssert.Contains("RequiresCondition: !controlled", mindControllableBuilding);
			StringAssert.Contains("Inherits@MC: ^MindControllableBuilding", baseBuilding);
			StringAssert.DoesNotContain("MindControlBuilding", regularMindControl,
				"Regular Yuri units, Master Minds, and Psychic Towers must remain unable to control buildings.");
			StringAssert.Contains("ValidTargets: MindControl, MindControlBuilding", superMindControl);
			StringAssert.Contains("ValidTargets: MindControl, MindControlBuilding", yuriPrime);
		}

		static void AssertVeterancyBadges(string relativePath, string prerequisite, params string[] actors)
		{
			var source = Read(relativePath);
			foreach (var actor in actors)
			{
				var block = ActorBlock(source, actor);
				StringAssert.Contains("ProducibleWithLevel:", block, actor);
				StringAssert.Contains($"Prerequisites: {prerequisite}", block, actor);
				StringAssert.Contains("WithProductionIconOverlay:", block, actor);
				StringAssert.Contains("Types: Veterancy", block, actor);
			}
		}

		static int CountDropItems(string actor)
		{
			var total = 0;
			using var reader = new StringReader(actor);
			while (reader.ReadLine() is { } line)
			{
				var marker = "DropItems:";
				var index = line.IndexOf(marker, StringComparison.Ordinal);
				if (index < 0)
					continue;

				total += line[(index + marker.Length)..]
					.Split(',', StringSplitOptions.RemoveEmptyEntries).Length;
			}

			return total;
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
			while (directory != null && !File.Exists(Path.Combine(
				directory.FullName, "packaging", "nukehour-version.json")))
				directory = directory.Parent;

			Assert.That(directory, Is.Not.Null, "Unable to locate the repository root.");
			return directory!.FullName;
		}
	}
}
