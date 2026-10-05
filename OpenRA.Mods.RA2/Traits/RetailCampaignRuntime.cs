#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software. It is
 * made available under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version.
 */
#endregion

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using OpenRA.Graphics;
using OpenRA.Mods.Common;
using OpenRA.Mods.Common.Effects;
using OpenRA.Mods.Common.FileFormats;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Common.Traits.Render;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Mods.RA2.Missions;
using OpenRA.Orders;
using OpenRA.Primitives;
using OpenRA.Traits;
using OpenRA.Widgets;

namespace OpenRA.Mods.RA2.Traits
{
	[TraitLocation(SystemActors.World)]
	[Desc("Runs the open-source compatibility layer for converted user-owned RA2 campaign maps.")]
	public sealed class RetailCampaignRuntimeInfo : TraitInfo
	{
		[Desc("Source house controlled by the human player.")]
		public readonly string Player = "Player House";

		[Desc("Stable NUKE HOUR campaign mission identifier.")]
		public readonly string Mission = "";

		public override object Create(ActorInitializer init) => new RetailCampaignRuntime(this);
	}

	public sealed class RetailCampaignRuntime : IWorldLoaded, ITick, ITickRender, INotifyOwnerChanged, INotifyAnyInfiltrated
	{
		Actor enteringTanya;
		CPos tanyaEntryCell;
		int tanyaEntryDeadline;
		bool tanyaEntryActive;
		const int RuntimeTicksPerSecond = 25;
		const int MaxImmediateScriptSteps = 32;

		sealed class TriggerState
		{
			public readonly Ra2MissionTrigger Trigger;
			public readonly List<Actor> Actors = new();
			public readonly HashSet<CPos> Cells = new();
			public readonly Dictionary<uint, int> PreviousHealth = new();
			public bool Enabled;
			public bool Repeat;
			public bool Destroyed;
			public int EnabledAt;

			public TriggerState(Ra2MissionTrigger trigger)
			{
				Trigger = trigger;
				Enabled = !trigger.Disabled;
			}
		}

		sealed class TeamScriptState
		{
			public readonly string TeamId;
			public readonly List<Actor> Actors;
			public readonly Ra2MissionScript Script;
			public int Step;
			public int ResumeAt;
			public bool WaitForIdle;
			public bool WaitForMembersInWorld;
			public bool WaitForTransportLoad;

			public TeamScriptState(string teamId, List<Actor> actors, Ra2MissionScript script)
			{
				TeamId = teamId;
				Actors = actors;
				Script = script;
			}
		}

		sealed class LockedOrderGenerator : IOrderGenerator
		{
			public IEnumerable<Order> Order(World world, CPos cell, int2 worldPixel, MouseInput mi)
				=> Enumerable.Empty<Order>();

			public void Tick(World world) { }
			public IEnumerable<IRenderable> Render(WorldRenderer wr, World world) => SpriteRenderable.None;
			public IEnumerable<IRenderable> RenderAboveShroud(WorldRenderer wr, World world) => SpriteRenderable.None;
			public IEnumerable<IRenderable> RenderAnnotations(WorldRenderer wr, World world) => SpriteRenderable.None;
			public string GetCursor(World world, CPos cell, int2 worldPixel, MouseInput mi) => null;
			public void Deactivate() { }
			public bool HandleKeyPress(KeyInput e) => true;
			public void SelectionChanged(World world, IEnumerable<Actor> selected) { }
		}

		readonly RetailCampaignRuntimeInfo info;
		readonly Dictionary<string, TriggerState> triggers = new(StringComparer.OrdinalIgnoreCase);
		readonly Dictionary<string, List<Actor>> teams = new(StringComparer.OrdinalIgnoreCase);
		readonly List<TeamScriptState> teamScripts = new();
		readonly HashSet<int> reportedActionOpcodes = new();
		readonly HashSet<int> reportedScriptOpcodes = new();
		readonly HashSet<int> globals = new();
		readonly HashSet<int> locals = new();
		readonly HashSet<string> firedTriggers = new(StringComparer.OrdinalIgnoreCase);
		readonly HashSet<string> displayedMissionTextKeys = new(StringComparer.OrdinalIgnoreCase);
		readonly HashSet<string> playerControlledHouses = new(StringComparer.OrdinalIgnoreCase);
		readonly Dictionary<(string Owner, bool Building), int> initialActorCounts = new();
		readonly Dictionary<(string Owner, string ActorType), int> initialActorTypeCounts = new();
		readonly Ra2MissionObjectEventTracker objectEvents = new();

		World world;
		WorldRenderer worldRenderer;
		Player player;
		MissionObjectives objectives;
		Ra2MissionSource source;
		IniFile missionIni;
		IniFile retailRulesIni;
		IniFile retailArtIni;
		IniFile retailEvaIni;
		IniFile retailSoundIni;
		Ra2CsfTable missionText;
		int objective = -1;
		int ticks;
		int globalTimerTicks = -1;
		uint initialMaxActorId;
		bool globalTimerRunning;
		bool resolved;
		bool scriptedOutcomeActions;
		bool inputLocked;
		bool openingAudit;
		bool openingAuditUnlockObserved;
		bool openingAuditPassed;
		bool accessAudit;
		bool accessAuditPassed;
		bool playerTeamAudit;
		bool playerTeamAuditStarted;
		bool playerTeamAuditPassed;
		int playerTeamAuditStartedAt = -1;
		int playerTeamAuditCount;
		int expectedStartingCash;
		bool objectiveAudit;
		bool objectiveAuditTargetsDestroyed;
		int objectiveAuditStartedAt = -1;
		string completionAuditWinningTrigger;
		string objectiveAuditWinningTrigger;
		Player objectiveAuditTargetOwner;

		public RetailCampaignRuntime(RetailCampaignRuntimeInfo info)
		{
			this.info = info;
		}

		void IWorldLoaded.WorldLoaded(World w, WorldRenderer wr)
		{
			world = w;
			worldRenderer = wr;
			player = w.LocalPlayer ??
				w.Players.FirstOrDefault(p => p.InternalName.Equals(info.Player, StringComparison.OrdinalIgnoreCase));
			if (player == null)
				throw new InvalidOperationException($"Campaign mission {info.Mission} has no playable house {info.Player}.");

			objectives = player.PlayerActor.Trait<MissionObjectives>();
			objective = objectives.Add(player,
				FluentProvider.GetMessage($"mission-{info.Mission}-objectives"), "Primary");
			LoadMissionControlData();
		}

		void LoadMissionControlData()
		{
			var alliedFirstMission = info.Mission.Equals("allied-01", StringComparison.OrdinalIgnoreCase);
			objectiveAudit = alliedFirstMission &&
				bool.TryParse(Environment.GetEnvironmentVariable("OPENRA_RA2_CAMPAIGN_OBJECTIVE_AUDIT"),
					out var requestedObjectiveAudit) && requestedObjectiveAudit;
			openingAudit = alliedFirstMission &&
				((bool.TryParse(Environment.GetEnvironmentVariable("OPENRA_RA2_CAMPAIGN_OPENING_AUDIT"),
					out var requestedOpeningAudit) && requestedOpeningAudit) || objectiveAudit);

			using var missionStream = world.Map.Package.GetStream("mission.ini");
			if (missionStream == null)
			{
				Log.Write("debug", $"Campaign mission {info.Mission} has no private mission.ini; using fallback objectives.");
				return;
			}

			var bytes = missionStream.ReadAllBytes();
			source = Ra2MissionSource.Parse(new MemoryStream(bytes, writable: false));
			missionIni = new IniFile(new MemoryStream(bytes, writable: false));
			LoadPlayerControlDomain();
			ApplyRetailPlayerControlDomain();
			ApplyRetailStartingCash();
			accessAudit = bool.TryParse(Environment.GetEnvironmentVariable("OPENRA_RA2_CAMPAIGN_ACCESS_AUDIT"),
				out var requestedAccessAudit) && requestedAccessAudit;
			playerTeamAudit = bool.TryParse(Environment.GetEnvironmentVariable("OPENRA_RA2_CAMPAIGN_TEAM_AUDIT"),
				out var requestedPlayerTeamAudit) && requestedPlayerTeamAudit;
			if (Game.ModData.DefaultFileSystem.TryOpen("rules.ini", out var rulesStream) ||
				Game.ModData.DefaultFileSystem.TryOpen("rulesmd.ini", out rulesStream))
				using (rulesStream)
					retailRulesIni = new IniFile(rulesStream);
			if (Game.ModData.DefaultFileSystem.TryOpen("art.ini", out var artStream) ||
				Game.ModData.DefaultFileSystem.TryOpen("artmd.ini", out artStream))
				using (artStream)
					retailArtIni = new IniFile(artStream);
			if (Game.ModData.DefaultFileSystem.TryOpen("eva.ini", out var evaStream) ||
				Game.ModData.DefaultFileSystem.TryOpen("evamd.ini", out evaStream))
				using (evaStream)
					retailEvaIni = new IniFile(evaStream);
			if (Game.ModData.DefaultFileSystem.TryOpen("sound.ini", out var soundStream) ||
				Game.ModData.DefaultFileSystem.TryOpen("soundmd.ini", out soundStream))
				using (soundStream)
					retailSoundIni = new IniFile(soundStream);

			if (Game.ModData.DefaultFileSystem.TryOpen("ra2.csf", out var csfStream))
				using (csfStream)
					try
					{
						missionText = Ra2CsfTable.Parse(csfStream);
					}
					catch (Exception ex) when (ex is InvalidDataException || ex is EndOfStreamException)
					{
						Log.Write("debug", $"Campaign mission text could not be read: {ex.Message}");
					}

			foreach (var trigger in source.Triggers.Values)
				triggers.Add(trigger.Key, new TriggerState(trigger));
			scriptedOutcomeActions = source.Actions.Values.Any(actions =>
				actions.Operations.Any(action => action.Opcode == 1 || action.Opcode == 2));

			foreach (var tag in source.Tags.Values)
				if (triggers.TryGetValue(tag.Trigger, out var state))
					state.Repeat |= tag.Repeating != 0;

			var spawned = world.WorldActor.Trait<SpawnMapActors>().Actors;
			AttachObjectTags(spawned, "Structures", 6);
			AttachObjectTags(spawned, "Units", 7);
			AttachObjectTags(spawned, "Infantry", 8);
			AttachObjectTags(spawned, "Aircraft", 7);
			AttachCellTags();

			foreach (var actor in world.Actors.Where(IsLive))
			{
				initialMaxActorId = Math.Max(initialMaxActorId, actor.ActorID);
				var key = (actor.Owner.InternalName, IsBuilding(actor));
				initialActorCounts[key] = initialActorCounts.GetValueOrDefault(key) + 1;
				var typeKey = (actor.Owner.InternalName, actor.Info.Name);
				initialActorTypeCounts[typeKey] = initialActorTypeCounts.GetValueOrDefault(typeKey) + 1;
			}

			foreach (var state in triggers.Values)
				foreach (var actor in state.Actors)
					if (actor.TraitOrDefault<IHealth>() is { } health)
						state.PreviousHealth[actor.ActorID] = health.HP;

			var eligibleWinners = world.Players
				.Where(candidate => candidate == player ||
					player.RelationshipWith(candidate) == PlayerRelationship.Ally)
				.Select(candidate => candidate.InternalName)
				.ToArray();
			var completion = Ra2CampaignCompletionAnalyzer.Analyze(source, missionIni, eligibleWinners,
				Ra2MissionEventSemantics.SupportedOpcodes);
			if (!completion.CanComplete || completion.UnresolvedWinningTriggers.Count > 0 ||
				completion.UnreachableWinningTriggers.Count > 0)
				throw new InvalidDataException($"Campaign mission {info.Mission} has no valid completion path.");

			var winningRoute = completion.Routes.First(route =>
				route.ResolvedWinner != null && eligibleWinners.Contains(route.ResolvedWinner,
					StringComparer.OrdinalIgnoreCase) && route.UnsupportedEventOpcodes.Count == 0);
			if (bool.TryParse(Environment.GetEnvironmentVariable("OPENRA_RA2_CAMPAIGN_COMPLETION_AUDIT"),
				out var completionAudit) && completionAudit)
				completionAuditWinningTrigger = winningRoute.WinningTrigger;

			if (objectiveAudit)
			{
				objectiveAuditWinningTrigger = winningRoute.WinningTrigger;
				objectiveAuditTargetOwner = ResolveObjectiveAuditTargetOwner(winningRoute);
				if (objectiveAuditTargetOwner == null)
					throw new InvalidDataException(
						$"Campaign objective audit cannot resolve the structure target for {info.Mission}.");
			}

			Log.Write("debug", $"Campaign runtime {info.Mission}: triggers={triggers.Count}, " +
				$"teams={source.TeamTypes.Count}, taggedActors={triggers.Values.Sum(t => t.Actors.Count)}, " +
				$"taggedCells={triggers.Values.Sum(t => t.Cells.Count)}");
		}

		void LoadPlayerControlDomain()
		{
			playerControlledHouses.Clear();
			playerControlledHouses.Add(player.InternalName);
			playerControlledHouses.Add(info.Player);
			foreach (var entry in missionIni.GetSection("Houses", true))
			{
				var house = entry.Value.Trim();
				var enabled = missionIni.GetSection(house, true).GetValue("PlayerControl", "no");
				if (IsIniTrue(enabled))
					playerControlledHouses.Add(house);
			}
		}

		void ApplyRetailPlayerControlDomain()
		{
			var transferred = 0;
			foreach (var actor in world.Actors.Where(actor => IsLive(actor) && actor.Owner != player &&
				playerControlledHouses.Contains(actor.Owner.InternalName)).ToArray())
			{
				actor.ChangeOwner(player);
				transferred++;
			}

			if (transferred > 0)
				Log.Write("debug", $"Campaign runtime {info.Mission}: transferred {transferred} actors from " +
					"secondary PlayerControl houses to the human player.");
		}

		void ApplyRetailStartingCash()
		{
			var playerCredits = new List<int>();
			foreach (var entry in missionIni.GetSection("Houses", true))
			{
				var house = entry.Value.Trim();
				var credits = Math.Max(0, ParseInt(
					missionIni.GetSection(house, true).GetValue("Credits", "0")));
				var owner = world.Players.FirstOrDefault(candidate =>
					candidate.InternalName.Equals(house, StringComparison.OrdinalIgnoreCase));
				if (owner != null && !playerControlledHouses.Contains(house))
					SetStartingCash(owner, credits * 100);

				if (playerControlledHouses.Contains(house))
					playerCredits.Add(credits);
			}

			expectedStartingCash = Ra2MissionRuntimeSemantics.StartingCash(playerCredits);
			SetStartingCash(player, expectedStartingCash);
		}

		static void SetStartingCash(Player owner, int cash)
		{
			var resources = owner?.PlayerActor.TraitOrDefault<PlayerResources>();
			if (resources == null)
				return;

			resources.Cash = Math.Max(0, cash);
			resources.Resources = 0;
		}

		static bool IsIniTrue(string value)
			=> value.Equals("yes", StringComparison.OrdinalIgnoreCase) ||
				value.Equals("true", StringComparison.OrdinalIgnoreCase) || value == "1";

		void AttachObjectTags(IReadOnlyDictionary<string, Actor> spawned, string sectionName, int tagIndex)
		{
			foreach (var entry in missionIni.GetSection(sectionName, true))
			{
				var fields = entry.Value.Split(',');
				if (fields.Length <= tagIndex || !source.Tags.TryGetValue(fields[tagIndex].Trim(), out var tag) ||
					!triggers.TryGetValue(tag.Trigger, out var state))
					continue;

				if (spawned.TryGetValue($"{sectionName}@{entry.Key}", out var actor))
					state.Actors.Add(actor);
			}
		}

		void AttachCellTags()
		{
			var mapSize = missionIni.GetSection("Map", true).GetValue("Size", "0,0,0,0")
				.Split(',').Select(ParseInt).ToArray();
			if (mapSize.Length < 4)
				return;

			var fullSize = new int2(mapSize[2], mapSize[3]);
			foreach (var entry in missionIni.GetSection("CellTags", true))
			{
				if (!int.TryParse(entry.Key, out var encoded) || !source.Tags.TryGetValue(entry.Value.Trim(), out var tag) ||
					!triggers.TryGetValue(tag.Trigger, out var state))
					continue;

				var raw = Ra2MissionMapCoordinates.DecodePacked(encoded);
				var mapPosition = Ra2MissionMapCoordinates.ToMapPosition(raw.X, raw.Y, fullSize);
				if (world.Map.Tiles.Contains(mapPosition))
					state.Cells.Add(mapPosition.ToCPos(world.Map));
			}
		}

		void ITick.Tick(Actor self)
		{
			if (resolved || world == null)
				return;

			ticks++;
			if (ticks == 1)
				ShowInitialMissionObjective();
			if (ticks == 1 && Ra2MissionRuntimeSemantics.KeepPlayerInputAvailableDuringOpening(info.Mission))
				EnsureAlliedFirstMissionPlayerAccess();
			if (ticks == 1 && playerTeamAudit)
				StartPlayerTeamAudit();

			if (ticks == 1 && completionAuditWinningTrigger != null)
			{
				if (!triggers.TryGetValue(completionAuditWinningTrigger, out var winningTrigger))
					throw new InvalidDataException($"Campaign completion audit cannot find trigger {completionAuditWinningTrigger}.");

				FireTrigger(winningTrigger);
				if (!resolved || objectives.Objectives[objective].State != ObjectiveState.Completed)
					throw new InvalidDataException($"Campaign completion audit did not complete mission {info.Mission}.");

				Log.Write("debug", $"Campaign completion audit passed: {info.Mission}; " +
					$"trigger={completionAuditWinningTrigger}; player={player.InternalName}.");
				return;
			}

			if (globalTimerRunning && globalTimerTicks > 0)
				globalTimerTicks--;

			TickTeamScripts();
			if (playerTeamAuditStarted && !playerTeamAuditPassed && ticks > playerTeamAuditStartedAt)
				ValidatePlayerTeamAudit();

			if (ticks % 5 == 0 && source != null)
				EvaluateTriggers();
			TickTanyaEntry();

			if (openingAuditUnlockObserved && !openingAuditPassed && !tanyaEntryActive)
				ValidateAlliedFirstMissionOpening();

			if (accessAudit && !accessAuditPassed)
				ValidateCampaignAccess();

			if (objectiveAudit && openingAuditPassed && !objectiveAuditTargetsDestroyed)
				DestroyAlliedFirstMissionObjectiveTargets();
			else if (objectiveAuditTargetsDestroyed &&
				ticks - objectiveAuditStartedAt > SecondsToTicks(30))
				throw new InvalidDataException(
					$"Campaign objective audit timed out before the retail victory chain completed {info.Mission}.");

			if (ticks % 25 == 0 && !scriptedOutcomeActions)
				EvaluateFallbackObjectives();
		}

		void ValidateCampaignAccess()
		{
			if (inputLocked)
				return;

			var controlled = world.Actors.Where(actor => actor.Owner == player && IsPlayerControllableActor(actor))
				.ToArray();
			if (controlled.Length == 0)
				return;

			var cash = player.PlayerActor.TraitOrDefault<PlayerResources>()?.GetCashAndResources() ?? 0;
			if (cash < expectedStartingCash)
				throw new InvalidDataException($"Campaign access audit found {cash} credits for {info.Mission}; " +
					$"expected at least {expectedStartingCash}.");

			accessAuditPassed = true;
			Log.Write("debug", $"Campaign access audit passed: {info.Mission}; tick={ticks}; " +
				$"actors={controlled.Length} ({string.Join(",", controlled.Select(actor => actor.Info.Name))}); " +
				$"cash={cash}; input=unlocked.");
		}

		void StartPlayerTeamAudit()
		{
			var playerTeams = source.TeamTypes.Values
				.Where(team => ResolvePlayer(team.House) == player)
				.ToArray();

			foreach (var team in playerTeams)
			{
				if (!source.TaskForces.TryGetValue(team.TaskForce, out var taskForce))
					throw new InvalidDataException(
						$"Campaign player-team audit cannot find task force {team.TaskForce} for team {team.Key}.");

				foreach (var entry in taskForce.Entries)
				{
					var actorType = Ra2ActorTypeCatalog.Normalize(entry.ActorType);
					if (string.IsNullOrWhiteSpace(actorType) || !world.Map.Rules.Actors.ContainsKey(actorType))
						throw new InvalidDataException(
							$"Campaign player-team audit cannot deliver {entry.ActorType} ({actorType}) " +
							$"for team {team.Key} in {info.Mission}.");
				}

				CreateTeam(team.Key, team.Waypoint >= 0 ? team.Waypoint : null, reinforce: true);
				if (!teams.TryGetValue(team.Key, out var delivered) || delivered.Count == 0)
					throw new InvalidDataException(
						$"Campaign player-team audit created no controllable units for team {team.Key} in {info.Mission}.");
				if (delivered.Any(actor => actor == null || actor.Owner != player))
					throw new InvalidDataException(
						$"Campaign player-team audit delivered team {team.Key} outside player control in {info.Mission}.");
			}

			playerTeamAuditCount = playerTeams.Length;
			playerTeamAuditStartedAt = ticks;
			playerTeamAuditStarted = true;
		}

		void ValidatePlayerTeamAudit()
		{
			playerTeamAuditPassed = true;
			Log.Write("debug", $"Campaign player-team audit passed: {info.Mission}; " +
				$"teams={playerTeamAuditCount}; tick={ticks}; owner={player.InternalName}.");
		}

		void EvaluateTriggers()
		{
			foreach (var state in triggers.Values.ToArray())
			{
				if (!state.Enabled || state.Destroyed || !source.Events.TryGetValue(state.Trigger.Key, out var events) ||
					events.Operations.Count == 0 || !events.Operations.All(operation => EvaluateEvent(state, operation)))
					continue;

				FireTrigger(state);
				if (resolved)
					return;
			}

			foreach (var state in triggers.Values)
				foreach (var actor in state.Actors.Where(IsLive))
					if (actor.TraitOrDefault<IHealth>() is { } health)
						state.PreviousHealth[actor.ActorID] = health.HP;
		}

		bool EvaluateEvent(TriggerState state, Ra2MissionOperation operation)
		{
			var p1 = Parameter(operation, 0);
			var p2 = Parameter(operation, 1);
			var owner = ResolvePlayer(Ra2MissionEventSemantics.OwnerReference(
				operation.Opcode, state.Trigger.House, p2));
			switch (operation.Opcode)
			{
				case 1:
					return EnteredTaggedArea(state, owner);
				case 2:
					return state.Actors.Any(actor => objectEvents.WasInfiltrated(actor.ActorID));
				case 6:
				case 44:
					return AttachedWasDamaged(state);
				case 4:
					return state.Actors.Any(actor => HasLocation(actor) && player.Shroud.IsExplored(actor.Location));
				case 5:
					return world.Actors.Any(actor => HasLocation(actor) && actor.Owner == owner &&
						player.Shroud.IsExplored(actor.Location));
				case 7:
				case 29:
				case 31:
					return HasAnyDestroyedAttachment(state);
				case 48:
					return HasAnyDestroyedCapturedOrInfiltratedAttachment(state);
				case 8:
					return true;
				case 9:
					return CountLive(owner, building: false) == 0;
				case 10:
					return CountLive(owner, building: true) == 0;
				case 11:
					return CountLive(owner, building: null) == 0;
				case 13:
				case 47:
					return ticks - state.EnabledAt >= SecondsToTicks(ParseInt(p2));
				case 14:
					return globalTimerRunning && globalTimerTicks == 0;
				case 15:
					return DestroyedCount(owner, building: true) >= ParseInt(p2);
				case 16:
					return DestroyedCount(owner, building: false) >= ParseInt(p2);
				case 17:
					return !world.Actors.Any(actor => IsLive(actor) && actor.Owner == owner &&
						actor.Info.HasTraitInfo<ProductionInfo>());
				case 19:
					return HasBuilt(owner, "BuildingTypes", p2, IsBuilding);
				case 20:
					return HasBuilt(owner, "VehicleTypes", p2, actor =>
						!IsBuilding(actor) && actor.Info.HasTraitInfo<MobileInfo>() &&
						!actor.Info.HasTraitInfo<AircraftInfo>() && !actor.Info.HasTraitInfo<WithInfantryBodyInfo>());
				case 21:
					return HasBuilt(owner, "InfantryTypes", p2, actor =>
						actor.Info.HasTraitInfo<WithInfantryBodyInfo>());
				case 22:
					return HasBuilt(owner, "AircraftTypes", p2, actor => actor.Info.HasTraitInfo<AircraftInfo>());
				case 25:
					return CrossedTaggedLine(state, owner, horizontal: true);
				case 26:
					return CrossedTaggedLine(state, owner, horizontal: false);
				case 27:
					return globals.Contains(ParseInt(p2));
				case 28:
					return !globals.Contains(ParseInt(p2));
				case 30:
					return owner?.PlayerActor.TraitOrDefault<PowerManager>()?.ExcessPower < 0;
				case 32:
					return HasExistingBuilding(owner, p2);
				case 33:
					return state.Actors.Any(world.Selection.Contains);
				case 36:
					return locals.Contains(ParseInt(p2));
				case 37:
					return !locals.Contains(ParseInt(p2));
				case 39:
					return AttachedBelowHealth(state, 50);
				case 40:
					return AttachedBelowHealth(state, 25);
				case 41:
					return AttachedWasDamaged(state);
				case 42:
					return AttachedBelowHealth(state, 50);
				case 43:
					return AttachedBelowHealth(state, 25);
				case 51:
					return ticks - state.EnabledAt >= SecondsToTicks(Math.Max(ParseInt(p1), ParseInt(p2)));
				case 52:
					return owner?.PlayerActor.TraitOrDefault<PlayerResources>()?.GetCashAndResources() < ParseInt(p2);
				case 55:
					return CountLiveNaval(owner) == 0;
				case 56:
					return CountLiveLand(owner) == 0;
				case 57:
					return !HasExistingBuilding(owner, p2);
				default:
					return false;
			}
		}

		void FireTrigger(TriggerState state)
		{
			firedTriggers.Add(state.Trigger.Key);
			if (source.Actions.TryGetValue(state.Trigger.Key, out var actions))
				foreach (var action in actions.Operations)
				{
					ExecuteAction(state, action);
					if (resolved)
						break;
				}

			if (!string.IsNullOrWhiteSpace(state.Trigger.LinkedTrigger) &&
				triggers.TryGetValue(state.Trigger.LinkedTrigger, out var linked))
				EnableTrigger(linked);

			if (state.Repeat)
				state.EnabledAt = ticks;
			else
				state.Enabled = false;
		}

		void ExecuteAction(TriggerState state, Ra2MissionOperation action)
		{
			var p2 = Parameter(action, 1);
			var p7 = Parameter(action, 6);
			switch (action.Opcode)
			{
				case 1:
					ResolveWinner(ResolvePlayer(p2));
					break;
				case 2:
					ResolveLoser(ResolvePlayer(p2));
					break;
				case 3:
				case 13:
				case 74:
					EnsureHouseCanAct(ResolvePlayer(p2) ?? ResolvePlayer(state.Trigger.House));
					break;
				case 4:
					CreateTeam(p2, null, reinforce: false);
					break;
				case 5:
					teams.Remove(p2);
					teamScripts.RemoveAll(script => script.TeamId.Equals(p2, StringComparison.OrdinalIgnoreCase));
					break;
				case 6:
					OrderHouseToHunt(ResolvePlayer(p2) ?? ResolvePlayer(state.Trigger.House));
					break;
				case 7:
					CreateTeam(p2, DecodeWaypoint(p7), reinforce: true);
					break;
				case 9:
					IssueHouseOrder(ResolvePlayer(state.Trigger.House), "Sell", buildingsOnly: true);
					break;
				case 11:
					DisplayMissionText(p2);
					break;
				case 12:
					if (triggers.TryGetValue(p2, out var destroyed))
						destroyed.Destroyed = true;
					break;
				case 14:
					ChangeAttachedOwner(state, ResolvePlayer(p2));
					break;
				case 16:
					player.Shroud.ExploreAll();
					break;
				case 17:
					RevealAroundWaypoint(DecodeWaypoint(p2), 6);
					break;
				case 18:
					RevealAroundWaypoint(DecodeWaypoint(p2), 12);
					break;
				case 19:
					PlayRetailAudio(p2, null);
					break;
				case 21:
					PlayRetailSpeech(p2);
					break;
				case 22:
					if (triggers.TryGetValue(p2, out var forced))
						FireTrigger(forced);
					break;
				case 23:
					globalTimerRunning = true;
					break;
				case 24:
					globalTimerRunning = false;
					break;
				case 25:
					globalTimerTicks = Math.Max(0, globalTimerTicks) + SecondsToTicks(ParseInt(p2));
					break;
				case 26:
					globalTimerTicks = Math.Max(0, globalTimerTicks - SecondsToTicks(ParseInt(p2)));
					break;
				case 27:
					globalTimerTicks = SecondsToTicks(ParseInt(p2));
					break;
				case 28:
					globals.Add(ParseInt(p2));
					break;
				case 29:
					globals.Remove(ParseInt(p2));
					break;
				case 32:
					DestroyAttached(state, erase: false);
					break;
				case 36:
					ChangeHouseActors(ResolvePlayer(state.Trigger.House), ResolvePlayer(p2));
					break;
				case 37:
					SetRelationship(ResolvePlayer(state.Trigger.House), ResolvePlayer(p2), allied: true, reciprocal: true);
					break;
				case 38:
					SetRelationship(ResolvePlayer(state.Trigger.House), ResolvePlayer(p2), allied: false, reciprocal: true);
					break;
				case 41:
					ReportPresentationFallback(action.Opcode,
						$"animation={p2}; waypoint={DecodeWaypoint(p7)}");
					break;
				case 46:
					if (!Ra2MissionRuntimeSemantics.KeepPlayerInputAvailableDuringOpening(info.Mission))
						LockPlayerInput();
					break;
				case 47:
					if (!tanyaEntryActive)
						UnlockPlayerInput();
					openingAuditUnlockObserved |= openingAudit;
					break;
				case 48:
					CenterCamera(DecodeWaypoint(p7));
					break;
				case 51:
					player.Shroud.ResetExploration();
					break;
				case 53:
					if (triggers.TryGetValue(p2, out var enabled))
						EnableTrigger(enabled);
					break;
				case 54:
					if (triggers.TryGetValue(p2, out var disabled))
						disabled.Enabled = false;
					break;
				case 55:
					CreateRadarEvent(ParseInt(p2), DecodeWaypoint(p7));
					break;
				case 56:
					locals.Add(ParseInt(p2));
					break;
				case 57:
					locals.Remove(ParseInt(p2));
					break;
				case 60:
					IssueAttachedOrder(state, "Sell");
					break;
				case 61:
					IssueAttachedOrder(state, "PowerDown");
					break;
				case 63:
					DamageAroundWaypoint(DecodeWaypoint(p2), 100);
					break;
				case 75:
					break;
				case 80:
					CreateTeam(p2, DecodeWaypoint(p7), reinforce: true);
					break;
				case 99:
					PlayRetailAudio(p2, WaypointCell(DecodeWaypoint(p7)));
					break;
				case 100:
					PlayRetailMovie(p2, paused: false);
					break;
				case 101:
					ReportPresentationFallback(action.Opcode, $"reshroud-waypoint={DecodeWaypoint(p2)}");
					break;
				case 104:
					FlashTeam(p2, ParseInt(p7));
					break;
				case 107:
					CreateTeam(p2, DecodeWaypoint(p7), reinforce: true);
					break;
				case 108:
					CreateCrate(DecodeWaypoint(p7));
					break;
				case 111:
					IssueAttachedOrder(state, "Unload");
					break;
				case 113:
					CheerHouse(ResolvePlayer(p2) ?? ResolvePlayer(state.Trigger.House));
					break;
				case 114:
					ReportPresentationFallback(action.Opcode, $"production-tab={p2}");
					break;
				case 115:
					ReportPresentationFallback(action.Opcode, $"cameo={p2}; duration={p7}");
					break;
				case 116:
					ReportPresentationFallback(action.Opcode, $"stop-sounds-waypoint={DecodeWaypoint(p7)}");
					break;
				case 117:
					PlayRetailMovie(p2, paused: true);
					break;
				default:
					if (reportedActionOpcodes.Add(action.Opcode))
						Log.Write("debug", $"Campaign mission {info.Mission} has no action adapter for opcode {action.Opcode}.");
					break;
			}
		}

		void DisplayMissionText(string key)
		{
			if (string.IsNullOrWhiteSpace(key))
				return;
			if (info.Mission.Equals("allied-01", StringComparison.OrdinalIgnoreCase) &&
				key.Equals("MISSION:ALL01A", StringComparison.OrdinalIgnoreCase) &&
				!displayedMissionTextKeys.Add(key))
				return;

			var text = RetailMissionText.Resolve(key,
				id => FluentProvider.TryGetMessage(id, out var translated) ? translated : null,
				label => missionText != null && missionText.TryResolve(label, out var imported) ? imported : null);

			if (!string.IsNullOrWhiteSpace(text))
				TextNotificationsManager.AddMissionLine(FluentProvider.GetMessage("retail-mission-prefix"), text);
		}

		void ShowInitialMissionObjective()
		{
			if (info.Mission.Equals("allied-01", StringComparison.OrdinalIgnoreCase))
				DisplayMissionText("MISSION:ALL01A");
		}

		void PlayRetailSpeech(string key)
		{
			try
			{
				if (Game.Sound.PlayNotification(world.Map.Rules, player, "Speech", key,
					player.Faction.InternalName))
					return;
			}
			catch (InvalidOperationException ex)
			{
				// Retail mission dialogue labels are not necessarily registered in
				// the OpenRA notification pool. Fall through to the imported audio
				// lookup instead of aborting the entire mission tick.
				Log.Write("debug", $"Campaign mission {info.Mission} notification pool does not contain " +
					$"{key}; trying imported retail audio instead ({ex.Message}).");
			}

			PlayRetailAudio(key, null);
		}

		void PlayRetailAudio(string key, CPos? cell)
		{
			var file = ResolveRetailAudioFile(key);
			if (file == null)
			{
				Log.Write("debug", $"Campaign mission {info.Mission} could not resolve audio cue {key}.");
				return;
			}

			if (cell != null)
				Game.Sound.Play(SoundType.World, file, world.Map.CenterOfCell(cell.Value));
			else
				Game.Sound.Play(SoundType.UI, file);
		}

		string ResolveRetailAudioFile(string key)
		{
			if (string.IsNullOrWhiteSpace(key))
				return null;

			var sovietEva = new[] { "cuba", "iraq", "libya", "russia", "yuri" }
				.Contains(player.Faction.InternalName, StringComparer.OrdinalIgnoreCase);
			var evaSide = sovietEva ? "Russian" : "Allied";
			foreach (var candidate in Ra2MissionAudioCatalog.Candidates(
				key, evaSide, retailEvaIni, retailSoundIni))
				foreach (var filename in new[] { candidate, candidate + ".wav", candidate + ".aud" })
					if (Game.ModData.DefaultFileSystem.TryOpen(filename, out var stream))
					{
						stream.Dispose();
						return filename;
					}

			return null;
		}

		void PlayRetailMovie(string key, bool paused)
		{
			var file = ResolveRetailMovieFile(key);
			if (file == null)
			{
				ReportPresentationFallback(paused ? 117 : 100,
					$"sidebar-movie={key}; optional retail movie archive not imported");
				return;
			}

			Sync.RunUnsynced(world, () =>
			{
				var videoPlayer = Ui.Root.GetOrNull<VideoPlayerWidget>("PLAYER");
				if (videoPlayer == null)
				{
					Log.Write("debug", $"Campaign mission {info.Mission} has no radar video player for {file}.");
					return;
				}

				try
				{
					videoPlayer.LoadAndPlay(file);
					if (paused)
						world.SetPauseState(true);
					videoPlayer.PlayThen(() =>
					{
						if (paused)
							world.SetPauseState(false);
					});
				}
				catch (Exception ex) when (ex is IOException || ex is InvalidDataException || ex is NotSupportedException)
				{
					if (paused)
						world.SetPauseState(false);
					Log.Write("debug", $"Campaign mission {info.Mission} could not play retail movie {file}: {ex.Message}");
				}
			});
		}

		string ResolveRetailMovieFile(string key)
		{
			if (retailArtIni == null || string.IsNullOrWhiteSpace(key))
				return null;

			var movie = retailArtIni.GetSection("Movies", true).GetValue(key, string.Empty).Trim();
			if (string.IsNullOrWhiteSpace(movie))
				return null;

			foreach (var candidate in new[]
			{
				movie + ".vqa",
				$"retailmovies01|{movie}.vqa",
				$"retailmovies02|{movie}.vqa",
			})
				if (Game.ModData.DefaultFileSystem.TryOpen(candidate, out var stream))
				{
					stream.Dispose();
					return candidate;
				}

			return null;
		}

		void LockPlayerInput()
		{
			Sync.RunUnsynced(world, () =>
			{
				if (world.OrderGenerator is LockedOrderGenerator)
					return;
				world.OrderGenerator = new LockedOrderGenerator();
				inputLocked = true;
			});
		}

		void UnlockPlayerInput()
		{
			Sync.RunUnsynced(world, () =>
			{
				if (!inputLocked)
					return;
				world.CancelInputMode();
				inputLocked = false;
			});
		}

		void EnsureAlliedFirstMissionPlayerAccess()
		{
			var tanya = world.Actors.FirstOrDefault(actor => IsLive(actor) &&
				actor.Info.Name.Equals("tany", StringComparison.OrdinalIgnoreCase));
			if (tanya == null)
			{
				if (!world.Map.Rules.Actors.ContainsKey("tany"))
					throw new InvalidDataException(
						$"Campaign mission {info.Mission} cannot provide the required Tanya actor.");

				var spawnCell = world.Map.FindTilesInCircle(player.HomeLocation, 4)
					.FirstOrDefault(world.Map.Contains);
				if (!world.Map.Contains(spawnCell))
					throw new InvalidDataException(
						$"Campaign mission {info.Mission} has no valid Tanya starting cell.");

				tanya = world.CreateActor(true, "tany", new TypeDictionary
				{
					new OwnerInit(player),
					new LocationInit(spawnCell),
					new FacingInit(WAngle.Zero),
				});
				Log.Write("debug", $"Campaign mission {info.Mission}: restored missing staged Tanya at {spawnCell}.");
			}
			else if (tanya.Owner != player)
				tanya.ChangeOwner(player);

			if (!IsPlayerControllableActor(tanya))
				throw new InvalidDataException(
					$"Campaign mission {info.Mission} staged Tanya is not selectable by the local player.");

			var mobile = tanya.Trait<Mobile>();
			var destination = WaypointCell(36) ?? player.HomeLocation;
			bool Land(CPos cell) => world.Map.Contains(cell) &&
				world.Map.GetTerrainInfo(cell).Type != "Water" && mobile.CanEnterCell(cell);
			var entry = world.Map.Contains(tanya.Location) && Land(tanya.Location) ? tanya.Location :
				world.Map.AllEdgeCells.Where(Land).OrderBy(cell => (cell - tanya.Location).LengthSquared)
				.Select(cell => (CPos?)cell).FirstOrDefault()
				?? throw new InvalidDataException("Tanya entry has no passable land boundary.");
			var route = TanyaLandEntry.FindPath(entry, destination, Land);
			tanyaEntryCell = route.Count > 0 ? route[0] : entry;
			enteringTanya = tanya;
			tanyaEntryActive = true;
			tanyaEntryDeadline = ticks + SecondsToTicks(15);
			LockPlayerInput();
			tanya.CancelActivity();
			if (!world.Map.Contains(tanya.Location))
			{
				// Stage just outside the land boundary, not at the remote retail spawn:
				// ReturnToCell is a straight entry animation, not terrain pathfinding.
				var staging = CVec.Directions.Select(step => entry + step)
					.Where(cell => world.Map.Tiles.Contains(cell) && !world.Map.Contains(cell) &&
						world.Map.GetTerrainInfo(cell).Type != "Water")
					.OrderBy(cell => (cell - tanya.Location).LengthSquared)
					.Select(cell => (CPos?)cell).FirstOrDefault() ?? entry;
				mobile.SetPosition(tanya, entry);
				mobile.SetCenterPosition(tanya, world.Map.CenterOfCell(staging));
				tanya.QueueActivity(mobile.ReturnToCell(tanya));
			}
			tanya.QueueActivity(mobile.MoveTo(check => (tanya.Location == tanyaEntryCell,
				TanyaLandEntry.FindPath(tanya.Location, tanyaEntryCell,
					cell => world.Map.Contains(cell) && world.Map.GetTerrainInfo(cell).Type != "Water" &&
						mobile.CanEnterCell(cell, check: check)))));
			RevealTanyaEntry();
		}

		void RevealTanyaEntry()
		{
			var projected = world.Map.FindTilesInCircle(tanyaEntryCell, 6)
				.SelectMany(cell => world.Map.ProjectedCellsCovering(cell.ToMPos(world.Map)));
			player.Shroud.ExploreProjectedCells(projected);
		}

		void TickTanyaEntry()
		{
			if (!tanyaEntryActive)
				return;
			RevealTanyaEntry();
			if (!IsLive(enteringTanya))
			{
				FinishTanyaEntry();
				return;
			}

			var arrived = (enteringTanya.CenterPosition - world.Map.CenterOfCell(tanyaEntryCell)).HorizontalLength < 512;
			if (ticks >= tanyaEntryDeadline && !arrived)
			{
				// A blocked path must not leave the player permanently locked in the intro.
				var mobile = enteringTanya.Trait<Mobile>();
				var cell = enteringTanya.Location;
				enteringTanya.CancelActivity();
				mobile.SetPosition(enteringTanya, cell);
				tanyaEntryCell = cell;
				Log.Write("debug", "Tanya entry recovered from a blocked opening path.");
				arrived = true;
			}
			if (arrived)
				FinishTanyaEntry();
		}

		void FinishTanyaEntry()
		{
			var tanya = enteringTanya;
			if (IsLive(tanya))
			{
				tanya.CancelActivity();
				Sync.RunUnsynced(world, () =>
				{
					world.Selection.Combine(world, new[] { tanya }, false, true);
					worldRenderer.Viewport.Center(tanya.CenterPosition);
				});
			}
			tanyaEntryActive = false;
			UnlockPlayerInput();
			Log.Write("debug", $"Campaign Tanya entry handed off; tick={ticks}; cell={tanya?.Location}.");
		}

		void ITickRender.TickRender(WorldRenderer wr, Actor self)
		{
			if (resolved || !tanyaEntryActive || world.RenderPlayer != player)
				return;
			Sync.RunUnsynced(world, () =>
			{
				wr.Viewport.Center(world.Map.CenterOfCell(tanyaEntryCell));
			});
		}

		void CenterCamera(int waypoint)
		{
			// The first mission's retail camera track belongs to its replaced intro.
			// Once handed off, never steal the player's camera back for that track.
			if (Ra2MissionRuntimeSemantics.KeepPlayerInputAvailableDuringOpening(info.Mission))
				return;
			var cell = WaypointCell(waypoint);
			if (cell != null)
				Sync.RunUnsynced(world, () => worldRenderer.Viewport.Center(world.Map.CenterOfCell(cell.Value)));
		}

		void CreateRadarEvent(int type, int waypoint)
		{
			var cell = WaypointCell(waypoint);
			var radar = world.WorldActor.TraitOrDefault<RadarPings>();
			if (cell == null || radar == null)
				return;

			var color = type == 3 ? Color.Yellow : type is 4 or 5 ? Color.Red : Color.White;
			radar.Add(() => player == world.RenderPlayer, world.Map.CenterOfCell(cell.Value), color, RuntimeTicksPerSecond * 4);
		}

		void FlashTeam(string teamId, int duration)
		{
			if (!teams.TryGetValue(teamId, out var actors))
				return;

			var flashes = Math.Max(2, duration / 4);
			foreach (var actor in actors.Where(IsLive))
				world.Add(new FlashTarget(actor, Color.White, count: flashes, interval: 2));
		}

		void CreateCrate(int waypoint)
		{
			var cell = WaypointCell(waypoint);
			var neutral = world.Players.FirstOrDefault(candidate => candidate.NonCombatant);
			if (cell == null || neutral == null || !world.Map.Rules.Actors.ContainsKey("crate"))
				return;

			world.CreateActor(true, "crate", new TypeDictionary
			{
				new OwnerInit(neutral),
				new LocationInit(cell.Value),
			});
		}

		void CheerHouse(Player owner)
		{
			if (owner == null)
				return;

			foreach (var actor in world.Actors.Where(actor => IsLive(actor) && actor.Owner == owner &&
				actor.Info.HasTraitInfo<WithInfantryBodyInfo>()))
				world.Add(new FlashTarget(actor, owner.Color, count: 4, interval: 3));
		}

		void ReportPresentationFallback(int opcode, string detail)
		{
			if (reportedActionOpcodes.Add(opcode))
				Log.Write("debug", $"Campaign mission {info.Mission} preserved action {opcode} as a non-blocking presentation fallback ({detail}).");
		}

		void ValidateAlliedFirstMissionOpening()
		{
			if (resolved)
				throw new InvalidDataException("Campaign opening audit resolved the mission before control reached the player.");
			if (inputLocked)
				throw new InvalidDataException("Campaign opening audit input remained locked after the opening sequence.");
			if (!teams.TryGetValue("094F68EC", out var tanyaTeam) ||
				!tanyaTeam.Any(actor => IsLive(actor) &&
					actor.Info.Name.Equals("tany", StringComparison.OrdinalIgnoreCase)))
				throw new InvalidDataException("Campaign opening audit could not find the live staged Tanya in her scripted team.");

			var openingDreadnoughts = teams.TryGetValue("0A67F6CC", out var dreadnoughtTeam)
				? dreadnoughtTeam.Count(actor => actor != null &&
					actor.Info.Name.Equals("dred", StringComparison.OrdinalIgnoreCase))
				: 0;
			if (openingDreadnoughts != 4)
				throw new InvalidDataException(
					$"Campaign opening audit expected four opening dreadnought instances, but found {openingDreadnoughts}.");
			var liveDreadnoughts = dreadnoughtTeam.Count(actor => IsLive(actor) &&
				actor.Info.Name.Equals("dred", StringComparison.OrdinalIgnoreCase));
			if (liveDreadnoughts == 0)
				throw new InvalidDataException("Campaign opening audit lost every dreadnought before control reached the player.");

			openingAuditPassed = true;
			Log.Write("debug", $"Campaign opening audit passed: {info.Mission}; " +
				$"tick={ticks}; tanya={tanyaTeam.Count(IsLive)}; dreadnoughts={liveDreadnoughts}; input=unlocked.");
		}

		Player ResolveObjectiveAuditTargetOwner(Ra2CampaignCompletionRoute winningRoute)
		{
			foreach (var triggerKey in winningRoute.TriggerPath)
			{
				if (!source.Triggers.TryGetValue(triggerKey, out var trigger) ||
					!source.Events.TryGetValue(triggerKey, out var events))
					continue;

				foreach (var operation in events.Operations.Where(operation => operation.Opcode == 10))
				{
					var ownerReference = Ra2MissionEventSemantics.OwnerReference(
						operation.Opcode, trigger.House, Parameter(operation, 1));
					var owner = ResolvePlayer(ownerReference);
					if (owner != null)
						return owner;
				}
			}

			return null;
		}

		void DestroyAlliedFirstMissionObjectiveTargets()
		{
			var targets = world.Actors.Where(actor => IsLive(actor) &&
				actor.Owner == objectiveAuditTargetOwner && IsBuilding(actor)).ToArray();
			if (targets.Length == 0)
				throw new InvalidDataException(
					$"Campaign objective audit found no live target buildings for {objectiveAuditTargetOwner.InternalName}.");

			objectiveAuditStartedAt = ticks;
			objectiveAuditTargetsDestroyed = true;
			foreach (var target in targets)
				target.Kill(world.WorldActor);

			Log.Write("debug", $"Campaign objective audit destroyed {targets.Length} target buildings " +
				$"for {objectiveAuditTargetOwner.InternalName}; waiting for the retail victory trigger.");
		}

		void EnableTrigger(TriggerState state)
		{
			state.Enabled = true;
			state.Destroyed = false;
			state.EnabledAt = ticks;
		}

		void CreateTeam(string teamId, int? actionWaypoint, bool reinforce)
		{
			if (!source.TeamTypes.TryGetValue(teamId, out var team) ||
				!source.TaskForces.TryGetValue(team.TaskForce, out var taskForce))
				return;

			var owner = ResolvePlayer(team.House);
			if (owner == null)
				return;

			var actors = new List<Actor>();
			if (!reinforce)
			{
				foreach (var entry in taskForce.Entries)
				{
					var actorType = Ra2ActorTypeCatalog.Normalize(entry.ActorType);
					actors.AddRange(world.Actors.Where(actor => IsLive(actor) && actor.Owner == owner &&
						actor.Info.Name.Equals(actorType, StringComparison.OrdinalIgnoreCase) &&
						!teams.Values.Any(list => list.Contains(actor))).Take(entry.Count));
				}
			}

			var waypoint = actionWaypoint ?? (team.Waypoint >= 0 ? team.Waypoint : null);
			var spawnCell = WaypointCell(waypoint) ?? owner.HomeLocation;
			var createdFullTransport = reinforce && team.Full &&
				CreateFullTransportTeam(owner, taskForce, spawnCell, actors);

			foreach (var entry in taskForce.Entries)
			{
				var actorType = Ra2ActorTypeCatalog.Normalize(entry.ActorType);
				if (createdFullTransport)
					continue;
				var missing = Math.Max(0, entry.Count - actors.Count(actor =>
					actor.Info.Name.Equals(actorType, StringComparison.OrdinalIgnoreCase)));
				if (!world.Map.Rules.Actors.ContainsKey(actorType))
					continue;

				for (var i = 0; i < missing; i++)
				{
					var cell = world.Map.FindTilesInCircle(spawnCell, 4).FirstOrDefault(world.Map.Contains);
					var created = world.CreateActor(true, actorType, new TypeDictionary
					{
						new OwnerInit(owner),
						new LocationInit(cell),
						new FacingInit(WAngle.Zero),
					});
					actors.Add(created);
				}
			}

			if (!teams.TryGetValue(teamId, out var instances))
				teams.Add(teamId, instances = new List<Actor>());
			instances.AddRange(actors);
			StartTeamScript(teamId, team, actors);
		}

		bool CreateFullTransportTeam(
			Player owner,
			Ra2MissionTaskForce taskForce,
			CPos spawnCell,
			List<Actor> actors)
		{
			var transportEntry = taskForce.Entries.FirstOrDefault(entry =>
			{
				var actorType = Ra2ActorTypeCatalog.Normalize(entry.ActorType);
				return world.Map.Rules.Actors.TryGetValue(actorType, out var actorInfo) &&
					actorInfo.HasTraitInfo<CargoInfo>();
			});
			if (transportEntry == null)
				return false;

			var transportType = Ra2ActorTypeCatalog.Normalize(transportEntry.ActorType);
			var transportInfo = world.Map.Rules.Actors[transportType];
			var cargoInfo = transportInfo.TraitInfo<CargoInfo>();
			var passengers = taskForce.Entries.Where(entry => entry != transportEntry)
				.SelectMany(entry => Enumerable.Repeat(
					Ra2ActorTypeCatalog.Normalize(entry.ActorType), Math.Max(0, entry.Count))).ToArray();

			for (var i = 0; i < transportEntry.Count; i++)
			{
				var cell = world.Map.FindTilesInCircle(spawnCell, 4).FirstOrDefault(world.Map.Contains);
				var transport = world.CreateActor(true, transportType, new TypeDictionary
				{
					new OwnerInit(owner),
					new LocationInit(cell),
					new FacingInit(WAngle.Zero),
					new CargoInit(cargoInfo, passengers),
				});
				actors.Add(transport);
				actors.AddRange(transport.Trait<Cargo>().Passengers);
			}

			return true;
		}

		void StartTeamScript(string teamId, Ra2MissionTeamType team, List<Actor> actors)
		{
			// The opening controller owns Tanya's entry and releases her directly to
			// the player. Do not replay the retail move/three-minute guard over orders.
			if (teamId == "094F68EC" && Ra2MissionRuntimeSemantics.KeepPlayerInputAvailableDuringOpening(info.Mission))
				return;
			if (!source.ScriptTypes.TryGetValue(team.Script, out var script))
				return;
			teamScripts.Add(new TeamScriptState(teamId, actors, script));
		}

		void TickTeamScripts()
		{
			foreach (var script in teamScripts.ToArray())
				TickTeamScript(script);
		}

		void TickTeamScript(TeamScriptState state)
		{
			state.Actors.RemoveAll(actor => actor == null || actor.Disposed || actor.IsDead);
			if (state.Actors.Count == 0)
			{
				teamScripts.Remove(state);
				return;
			}

			if (ticks < state.ResumeAt)
				return;
			if (state.WaitForMembersInWorld)
			{
				if (!state.Actors.Any(IsLive))
					return;
				state.WaitForMembersInWorld = false;
			}

			if (state.WaitForIdle)
			{
				if (state.Actors.Any(actor => IsLive(actor) && !actor.IsIdle))
					return;
				state.WaitForIdle = false;
			}

			if (state.WaitForTransportLoad)
			{
				if (state.Actors.Any(actor => IsLive(actor) && actor.Info.HasTraitInfo<PassengerInfo>()))
					return;
				state.WaitForTransportLoad = false;
			}

			for (var immediate = 0; immediate < MaxImmediateScriptSteps; immediate++)
			{
				if (state.Step < 0 || state.Step >= state.Script.Steps.Count)
				{
					teamScripts.Remove(state);
					return;
				}

				var step = state.Script.Steps[state.Step++];
				switch (step.Opcode)
				{
					case 0:
					case 48:
						OrderActorsToHunt(state.Actors);
						WaitForTeamActivity(state);
						return;
					case 1:
					case 3:
						OrderTeamToWaypoint(state, step.Argument, step.Opcode == 1 ? "AttackMove" : "Move");
						return;
					case 5:
						state.ResumeAt = ticks + Math.Max(1,
							Ra2MissionRuntimeSemantics.GuardDurationTicks(step.Argument, RuntimeTicksPerSecond));
						return;
					case 6:
						state.Step = Ra2MissionRuntimeSemantics.ScriptLineIndex(step.Argument);
						continue;
					case 8:
						UnloadTeam(state, step.Argument);
						return;
					case 9:
						DeployTeam(state);
						return;
					case 10:
						FollowNearestFriendly(state);
						return;
					case 11:
						ApplyAssignedMission(state, step.Argument);
						return;
					case 13:
						continue;
					case 14:
						LoadTeamTransport(state);
						return;
					case 16:
						OrderTeamToWaypoint(state, step.Argument, "AttackMove");
						return;
					case 19:
					case 21:
						IssueActorsOrder(state.Actors, "Scatter", false);
						WaitForTeamActivity(state);
						return;
					case 20:
						ChangeTeamOwner(state.Actors,
							ResolvePlayer(step.Argument.ToString(CultureInfo.InvariantCulture)));
						continue;
					case 24:
						if (reportedScriptOpcodes.Add(step.Opcode))
							Log.Write("debug", $"Campaign mission {info.Mission} preserved team speech " +
								$"{step.Argument} as a non-blocking presentation step.");
						continue;
					case 34:
						OrderTeamToStructure(state, step.Argument, "Move");
						return;
					case 37:
						DeleteTeamMembers(state);
						return;
					case 39:
						locals.Add(step.Argument);
						continue;
					case 42:
						foreach (var actor in state.Actors.Where(IsLive))
							if (actor.TraitOrDefault<IFacing>() is { } facing)
								facing.Facing = WAngle.FromFacing(step.Argument);
						continue;
					case 43:
						state.WaitForTransportLoad = state.Actors.Any(actor =>
							IsLive(actor) && actor.Info.HasTraitInfo<PassengerInfo>());
						if (state.WaitForTransportLoad)
							return;
						continue;
					case 46:
					case 47:
						OrderTeamToStructure(state, step.Argument, step.Opcode == 46 ? "Attack" : "Move");
						return;
					case 49:
						continue;
					case 50:
						foreach (var actor in state.Actors.Where(IsLive))
							world.Add(new FlashTarget(actor, Color.White,
								count: Math.Max(2, step.Argument / 4), interval: 2));
						continue;
					case 53:
					case 54:
						RegroupTeam(state);
						return;
					case 58:
						OrderTeamToFriendlyStructure(state, step.Argument);
						return;
					default:
						if (reportedScriptOpcodes.Add(step.Opcode))
							Log.Write("debug", $"Campaign mission {info.Mission} has no team-script adapter for opcode {step.Opcode}.");
						continue;
				}
			}

			Log.Write("debug", $"Campaign mission {info.Mission} stopped a zero-delay script loop in {state.TeamId}.");
			state.ResumeAt = ticks + 1;
		}

		void DeployTeam(TeamScriptState state)
		{
			foreach (var actor in state.Actors.Where(IsLive))
				foreach (var deploy in actor.TraitsImplementing<IIssueDeployOrder>())
					if (deploy.CanIssueDeployOrder(actor, false))
					{
						var order = deploy.IssueDeployOrder(actor, false);
						if (order != null)
							world.IssueOrder(order);
						break;
					}

			WaitForTeamActivity(state);
		}

		void LoadTeamTransport(TeamScriptState state)
		{
			var transports = state.Actors.Where(actor => IsLive(actor) &&
				actor.TraitOrDefault<Cargo>() != null).ToArray();
			foreach (var passengerActor in state.Actors.Where(actor => IsLive(actor) &&
				actor.TraitOrDefault<Passenger>() != null))
			{
				var passenger = passengerActor.Trait<Passenger>();
				var transport = transports.FirstOrDefault(candidate =>
				{
					var cargo = candidate.Trait<Cargo>();
					return !cargo.IsTraitDisabled && cargo.Info.Types.Contains(passenger.Info.CargoType) &&
						cargo.HasSpace(passenger.Info.Weight);
				});
				if (transport != null)
					world.IssueOrder(new Order("EnterTransport", passengerActor, Target.FromActor(transport), false));
			}

			state.ResumeAt = ticks + 1;
			state.WaitForTransportLoad = true;
		}

		void RegroupTeam(TeamScriptState state)
		{
			var leader = state.Actors.FirstOrDefault(HasLocation);
			if (leader != null)
				foreach (var actor in state.Actors.Where(actor => HasLocation(actor) && actor != leader))
					world.IssueOrder(new Order("Move", actor, Target.FromCell(world, leader.Location), false));
			WaitForTeamActivity(state);
		}

		void FollowNearestFriendly(TeamScriptState state)
		{
			var leader = state.Actors.FirstOrDefault(HasLocation);
			if (leader == null)
				return;

			var target = world.Actors.Where(actor => HasLocation(actor) && !state.Actors.Contains(actor) &&
				leader.Owner.RelationshipWith(actor.Owner) != PlayerRelationship.Enemy)
				.OrderBy(actor => (actor.Location - leader.Location).LengthSquared).FirstOrDefault();
			if (target != null)
				foreach (var actor in state.Actors.Where(HasLocation))
					world.IssueOrder(new Order("Move", actor, Target.FromCell(world, target.Location), false));
			WaitForTeamActivity(state);
		}

		void OrderTeamToWaypoint(TeamScriptState state, int waypoint, string order)
		{
			var destination = WaypointCell(waypoint);
			if (destination != null)
				foreach (var actor in state.Actors.Where(IsLive))
					world.IssueOrder(new Order(order, actor, Target.FromCell(world, destination.Value), false));
			WaitForTeamActivity(state);
		}

		void ApplyAssignedMission(TeamScriptState state, int mission)
		{
			if (mission == 15)
				OrderActorsToHunt(state.Actors);
			else if (mission == 16)
				IssueActorsOrder(state.Actors, "Unload", false);
			else
				IssueActorsOrder(state.Actors, "Stop", false);
			WaitForTeamActivity(state);
		}

		void UnloadTeam(TeamScriptState state, int behavior)
		{
			var transports = state.Actors.Where(actor => IsLive(actor) &&
				actor.Info.HasTraitInfo<CargoInfo>()).ToArray();
			IssueActorsOrder(transports, "Unload", false);
			if (behavior is 1 or 3)
				state.Actors.RemoveAll(actor => !actor.Info.HasTraitInfo<CargoInfo>());
			else if (behavior is 2)
				state.Actors.RemoveAll(actor => actor.Info.HasTraitInfo<CargoInfo>());

			state.ResumeAt = ticks + 1;
			state.WaitForMembersInWorld = behavior is 2;
			state.WaitForIdle = true;
		}

		void ChangeTeamOwner(IEnumerable<Actor> actors, Player owner)
		{
			if (owner == null)
				return;
			foreach (var actor in actors.Where(IsLive))
				actor.ChangeOwner(owner);
		}

		void DeleteTeamMembers(TeamScriptState state)
		{
			foreach (var actor in state.Actors.Where(IsLive).ToArray())
				world.AddFrameEndTask(_ => actor.Dispose());
			teamScripts.Remove(state);
		}

		void OrderTeamToStructure(TeamScriptState state, int selector, string order)
		{
			if (TryGetStructure(selector, state.Actors, friendly: false, out var target))
				foreach (var actor in state.Actors.Where(IsLive))
					world.IssueOrder(new Order(order, actor, Target.FromActor(target), false));
			WaitForTeamActivity(state);
		}

		void OrderTeamToFriendlyStructure(TeamScriptState state, int selector)
		{
			if (TryGetStructure(selector, state.Actors, friendly: true, out var target))
				foreach (var actor in state.Actors.Where(IsLive))
					world.IssueOrder(new Order("Move", actor, Target.FromActor(target), false));
			WaitForTeamActivity(state);
		}

		void WaitForTeamActivity(TeamScriptState state)
		{
			state.ResumeAt = ticks + 1;
			state.WaitForIdle = true;
		}

		void IssueActorsOrder(IEnumerable<Actor> actors, string order, bool queued)
		{
			foreach (var actor in actors.Where(IsLive))
				world.IssueOrder(new Order(order, actor, queued));
		}

		bool TryGetStructure(int selector, IReadOnlyCollection<Actor> team, bool friendly, out Actor actor)
		{
			actor = null;
			var decoded = Ra2MissionRuntimeSemantics.DecodeBuildingWithProperty(selector);
			var actorType = ResolveRetailActorType("BuildingTypes",
				decoded.BuildingIndex.ToString(CultureInfo.InvariantCulture));
			var teamActor = team.FirstOrDefault(HasLocation);
			if (teamActor == null || string.IsNullOrEmpty(actorType))
				return false;

			var candidates = world.Actors.Where(candidate => HasLocation(candidate) && IsBuilding(candidate) &&
				candidate.Info.Name.Equals(actorType, StringComparison.OrdinalIgnoreCase) &&
				(friendly
					? teamActor.Owner.RelationshipWith(candidate.Owner) != PlayerRelationship.Enemy
					: teamActor.Owner.RelationshipWith(candidate.Owner) == PlayerRelationship.Enemy)).ToArray();
			if (candidates.Length == 0)
				return false;

			var origin = teamActor.Location;
			actor = decoded.SelectionMode == 3
				? candidates.OrderByDescending(candidate => (candidate.Location - origin).LengthSquared).First()
				: candidates.OrderBy(candidate => (candidate.Location - origin).LengthSquared).First();
			return true;
		}

		void OrderHouseToHunt(Player owner)
		{
			if (owner != null)
				OrderActorsToHunt(world.Actors.Where(actor => IsLive(actor) && actor.Owner == owner));
		}

		void OrderActorsToHunt(IEnumerable<Actor> actors, bool queued = false)
		{
			foreach (var actor in actors.Where(HasLocation))
			{
				var enemy = world.Actors.Where(target => HasLocation(target) &&
					actor.Owner.RelationshipWith(target.Owner) == PlayerRelationship.Enemy)
					.OrderBy(target => (target.Location - actor.Location).LengthSquared).FirstOrDefault();
				if (enemy != null)
					world.IssueOrder(new Order("AttackMove", actor, Target.FromCell(world, enemy.Location), queued));
			}
		}

		void EnsureHouseCanAct(Player owner)
		{
			if (owner == null || owner == player)
				return;
			OrderHouseToHunt(owner);
		}

		void ChangeAttachedOwner(TriggerState state, Player newOwner)
		{
			if (newOwner == null)
				return;
			foreach (var actor in state.Actors.Where(IsLive))
				actor.ChangeOwner(newOwner);
		}

		void ChangeHouseActors(Player oldOwner, Player newOwner)
		{
			if (oldOwner == null || newOwner == null)
				return;
			foreach (var actor in world.Actors.Where(actor => IsLive(actor) && actor.Owner == oldOwner).ToArray())
				actor.ChangeOwner(newOwner);
		}

		void DestroyAttached(TriggerState state, bool erase)
		{
			foreach (var actor in state.Actors.Where(IsLive).ToArray())
				if (erase)
					world.AddFrameEndTask(_ => actor.Dispose());
				else
					actor.Kill(actor);
		}

		void IssueAttachedOrder(TriggerState state, string order)
		{
			foreach (var actor in state.Actors.Where(IsLive))
				world.IssueOrder(new Order(order, actor, false));
		}

		void IssueHouseOrder(Player owner, string order, bool buildingsOnly)
		{
			if (owner == null)
				return;
			foreach (var actor in world.Actors.Where(actor => IsLive(actor) && actor.Owner == owner &&
				(!buildingsOnly || IsBuilding(actor))))
				world.IssueOrder(new Order(order, actor, false));
		}

		void SetRelationship(Player from, Player to, bool allied, bool reciprocal)
		{
			if (from == null || to == null || from == to)
				return;
			SetOneWayRelationship(from, to, allied);
			if (reciprocal)
				SetOneWayRelationship(to, from, allied);
		}

		static void SetOneWayRelationship(Player from, Player to, bool allied)
		{
			if (allied)
			{
				from.AlliedPlayersMask = from.AlliedPlayersMask.Union(to.PlayerMask);
				from.EnemyPlayersMask = from.EnemyPlayersMask.Except(to.PlayerMask);
			}
			else
			{
				from.AlliedPlayersMask = from.AlliedPlayersMask.Except(to.PlayerMask);
				from.EnemyPlayersMask = from.EnemyPlayersMask.Union(to.PlayerMask);
			}
		}

		void DamageAroundWaypoint(int waypoint, int amount)
		{
			var center = WaypointCell(waypoint);
			if (center == null)
				return;
			foreach (var actor in world.Actors.Where(actor => HasLocation(actor) && actor.Location == center.Value).ToArray())
				actor.InflictDamage(world.WorldActor, new Damage(amount));
		}

		void RevealAroundWaypoint(int waypoint, int radius)
		{
			var center = WaypointCell(waypoint);
			if (center == null)
				return;

			var projected = world.Map.FindTilesInCircle(center.Value, radius)
				.SelectMany(cell => world.Map.ProjectedCellsCovering(cell.ToMPos(world.Map)));
			player.Shroud.ExploreProjectedCells(projected);
		}

		CPos? WaypointCell(int? waypoint)
		{
			if (waypoint == null)
				return null;
			var spawned = world.WorldActor.Trait<SpawnMapActors>().Actors;
			return spawned.TryGetValue($"Waypoint@{waypoint.Value}", out var actor) && HasLocation(actor) ? actor.Location : null;
		}

		Player ResolvePlayer(string value)
		{
			if (string.IsNullOrWhiteSpace(value) || value.Equals("<none>", StringComparison.OrdinalIgnoreCase))
				return null;
			var direct = world.Players.FirstOrDefault(p => p.InternalName.Equals(value, StringComparison.OrdinalIgnoreCase));
			if (direct != null)
				return playerControlledHouses.Contains(direct.InternalName) ? player : direct;

			var house = Ra2MissionHouseReference.Resolve(missionIni, value);
			if (house != null && playerControlledHouses.Contains(house))
				return player;
			return house == null ? null : world.Players.FirstOrDefault(p =>
				p.InternalName.Equals(house, StringComparison.OrdinalIgnoreCase));
		}

		bool HasBuilt(Player owner, string typeSection, string typeIndex, Func<Actor, bool> category)
		{
			if (owner == null)
				return false;

			var actorType = ResolveRetailActorType(typeSection, typeIndex);
			if (!string.IsNullOrEmpty(actorType))
			{
				var current = world.Actors.Count(actor => IsLive(actor) && actor.Owner == owner &&
					actor.Info.Name.Equals(actorType, StringComparison.OrdinalIgnoreCase));
				return current > initialActorTypeCounts.GetValueOrDefault((owner.InternalName, actorType));
			}

			return world.Actors.Any(actor => IsLive(actor) && actor.Owner == owner &&
				actor.ActorID > initialMaxActorId && category(actor));
		}

		bool HasExistingBuilding(Player owner, string typeIndex)
		{
			if (owner == null)
				return false;
			var actorType = ResolveRetailActorType("BuildingTypes", typeIndex);
			if (string.IsNullOrEmpty(actorType))
				return false;
			return world.Actors.Any(actor => IsLive(actor) && actor.Owner == owner && IsBuilding(actor) &&
				actor.Info.Name.Equals(actorType, StringComparison.OrdinalIgnoreCase));
		}

		int CountLiveNaval(Player owner)
		{
			if (owner == null)
				return int.MaxValue;
			return world.Actors.Count(actor => IsLive(actor) && actor.Owner == owner && IsNaval(actor));
		}

		int CountLiveLand(Player owner)
		{
			if (owner == null)
				return int.MaxValue;
			return world.Actors.Count(actor => IsLive(actor) && actor.Owner == owner && !IsBuilding(actor) && !IsNaval(actor));
		}

		string ResolveRetailActorType(string sectionName, string rawIndex)
		{
			if (retailRulesIni == null || !int.TryParse(rawIndex, out var index) || index < 0)
				return null;

			var entry = retailRulesIni.GetSection(sectionName, true)
				.Where(kv => int.TryParse(kv.Key, out _))
				.OrderBy(kv => ParseInt(kv.Key))
				.Skip(index)
				.FirstOrDefault();
			return string.IsNullOrWhiteSpace(entry.Value) ? null : Ra2ActorTypeCatalog.Normalize(entry.Value);
		}

		bool EnteredTaggedArea(TriggerState state, Player owner)
		{
			if (state.Cells.Count == 0)
				return false;
			return world.Actors.Any(actor => HasLocation(actor) && (owner == null || actor.Owner == owner) &&
				state.Cells.Contains(actor.Location));
		}

		bool CrossedTaggedLine(TriggerState state, Player owner, bool horizontal)
		{
			if (state.Cells.Count == 0)
				return false;
			var coordinates = horizontal ? state.Cells.Select(c => c.Y).ToHashSet() : state.Cells.Select(c => c.X).ToHashSet();
			return world.Actors.Any(actor => HasLocation(actor) && (owner == null || actor.Owner == owner) &&
				(horizontal ? coordinates.Contains(actor.Location.Y) : coordinates.Contains(actor.Location.X)));
		}

		bool AttachedWasDamaged(TriggerState state)
			=> state.Actors.Any(actor => IsLive(actor) && actor.TraitOrDefault<IHealth>() is { } health &&
				state.PreviousHealth.TryGetValue(actor.ActorID, out var previous) && health.HP < previous);

		static bool HasAnyDestroyedAttachment(TriggerState state)
			=> state.Actors.Count > 0 && state.Actors.Any(actor => !IsLive(actor));

		bool HasAnyDestroyedCapturedOrInfiltratedAttachment(TriggerState state)
			=> state.Actors.Count > 0 && state.Actors.Any(actor =>
				!IsLive(actor) || objectEvents.WasCapturedOrInfiltrated(actor.ActorID));

		static bool AttachedBelowHealth(TriggerState state, int percent)
			=> state.Actors.Any(actor => IsLive(actor) && actor.TraitOrDefault<IHealth>() is { } health &&
				health.HP * 100L <= health.MaxHP * percent);

		int CountLive(Player owner, bool? building)
		{
			if (owner == null)
				return int.MaxValue;
			return world.Actors.Count(actor => IsLive(actor) && actor.Owner == owner &&
				(building == null || IsBuilding(actor) == building.Value));
		}

		int DestroyedCount(Player owner, bool building)
		{
			if (owner == null)
				return 0;
			return Math.Max(0, initialActorCounts.GetValueOrDefault((owner.InternalName, building)) - CountLive(owner, building));
		}

		void EvaluateFallbackObjectives()
		{
			if (player.HasNoRequiredUnits(shortGame: false))
			{
				FailMission();
				return;
			}

			var enemies = world.Players.Where(p => !p.NonCombatant && player.RelationshipWith(p) == PlayerRelationship.Enemy);
			if (enemies.All(enemy => enemy.HasNoRequiredUnits(shortGame: false)))
				CompleteMission();
		}

		void ResolveWinner(Player winner)
		{
			if (winner == null)
			{
				Log.Write("debug", $"Campaign mission {info.Mission} ignored an unresolved winner reference.");
				return;
			}

			if (winner == player || player.RelationshipWith(winner) == PlayerRelationship.Ally)
				CompleteMission();
			else if (player.RelationshipWith(winner) == PlayerRelationship.Enemy)
				FailMission();
		}

		void ResolveLoser(Player loser)
		{
			if (loser == null)
			{
				Log.Write("debug", $"Campaign mission {info.Mission} ignored an unresolved loser reference.");
				return;
			}

			if (loser == player)
				FailMission();
			else if (player.RelationshipWith(loser) == PlayerRelationship.Enemy)
				CompleteMission();
		}

		void CompleteMission()
		{
			if (resolved)
				return;
			if (objectiveAudit && (!objectiveAuditTargetsDestroyed ||
				!firedTriggers.Contains(objectiveAuditWinningTrigger)))
				throw new InvalidDataException(
					$"Campaign objective audit bypassed the retail victory trigger for {info.Mission}.");
			UnlockPlayerInput();
			resolved = true;
			objectives.MarkCompleted(player, objective);
			CampaignProgress.RecordCompleted(info.Mission);
			if (objectiveAudit)
				Log.Write("debug", $"Campaign objective audit passed: {info.Mission}; " +
					$"trigger={objectiveAuditWinningTrigger}; player={player.InternalName}.");
		}

		void FailMission()
		{
			if (resolved)
				return;
			if (objectiveAudit)
				throw new InvalidDataException(
					$"Campaign objective audit reached a failure outcome for {info.Mission}.");
			UnlockPlayerInput();
			resolved = true;
			objectives.MarkFailed(player, objective);
		}

		void INotifyOwnerChanged.OnOwnerChanged(Actor self, Player oldOwner, Player newOwner)
		{
			if (oldOwner != newOwner && triggers.Values.Any(state => state.Actors.Contains(self)))
				objectEvents.RecordOwnershipChange(self.ActorID);
		}

		void INotifyAnyInfiltrated.Infiltrated(
			Actor target, Actor infiltrator, BitSet<TargetableType> types)
		{
			if (triggers.Values.Any(state => state.Actors.Contains(target)))
				objectEvents.RecordInfiltration(target.ActorID);
		}

		static bool IsBuilding(Actor actor) => actor.Info.HasTraitInfo<BuildingInfo>();

		static bool IsNaval(Actor actor)
			=> actor.Info.TraitInfoOrDefault<MobileInfo>()?.Locomotor.Equals(
				"naval", StringComparison.OrdinalIgnoreCase) == true;

		static bool IsLive(Actor actor) => actor != null && !actor.Disposed && actor.IsInWorld && !actor.IsDead;

		static bool HasLocation(Actor actor) => IsLive(actor) && actor.OccupiesSpace != null;

		static bool IsPlayerControllableActor(Actor actor)
			=> HasLocation(actor) && actor.Info.HasTraitInfo<ISelectableInfo>() &&
				(actor.Info.HasTraitInfo<MobileInfo>() || actor.Info.HasTraitInfo<ProductionInfo>());

		static string Parameter(Ra2MissionOperation operation, int index)
			=> index >= 0 && index < operation.Parameters.Count ? operation.Parameters[index] : "";

		static int ParseInt(string value) => int.TryParse(value, out var parsed) ? parsed : 0;

		static int DecodeWaypoint(string value) => Ra2MissionSource.ParseWaypoint(value);

		static int SecondsToTicks(int seconds) => Math.Max(0, seconds) * RuntimeTicksPerSecond;
	}
}
