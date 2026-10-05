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

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using OpenRA.Mods.Common.FileFormats;

namespace OpenRA.Mods.RA2.Missions
{
	public sealed record Ra2MissionOperation(int Opcode, IReadOnlyList<string> Parameters, string Raw);
	public sealed record Ra2MissionOperationList(string Key, IReadOnlyList<Ra2MissionOperation> Operations, string Raw);
	public sealed record Ra2MissionTrigger(
		string Key, string House, string LinkedTrigger, string Name, bool Disabled, string Raw);
	public sealed record Ra2MissionTag(string Key, int Repeating, string Name, string Trigger, string Raw);
	public sealed record Ra2MissionTaskForceEntry(int Count, string ActorType);
	public sealed record Ra2MissionTaskForce(string Key, IReadOnlyList<Ra2MissionTaskForceEntry> Entries, string Raw);
	public sealed record Ra2MissionScriptStep(int Opcode, int Argument);
	public sealed record Ra2MissionScript(string Key, IReadOnlyList<Ra2MissionScriptStep> Steps, string Raw);
	public sealed record Ra2MissionTeamType(
		string Key, string House, string TaskForce, string Script, int Waypoint, bool Full, string Raw);

	public sealed class Ra2MissionSource
	{
		public IReadOnlyDictionary<string, Ra2MissionTrigger> Triggers { get; }
		public IReadOnlyDictionary<string, Ra2MissionOperationList> Events { get; }
		public IReadOnlyDictionary<string, Ra2MissionOperationList> Actions { get; }
		public IReadOnlyDictionary<string, Ra2MissionTag> Tags { get; }
		public IReadOnlyDictionary<string, Ra2MissionTaskForce> TaskForces { get; }
		public IReadOnlyDictionary<string, Ra2MissionScript> ScriptTypes { get; }
		public IReadOnlyDictionary<string, Ra2MissionTeamType> TeamTypes { get; }
		public IReadOnlyDictionary<string, string> AITriggerTypes { get; }

		Ra2MissionSource(
			IReadOnlyDictionary<string, Ra2MissionTrigger> triggers,
			IReadOnlyDictionary<string, Ra2MissionOperationList> events,
			IReadOnlyDictionary<string, Ra2MissionOperationList> actions,
			IReadOnlyDictionary<string, Ra2MissionTag> tags,
			IReadOnlyDictionary<string, Ra2MissionTaskForce> taskForces,
			IReadOnlyDictionary<string, Ra2MissionScript> scriptTypes,
			IReadOnlyDictionary<string, Ra2MissionTeamType> teamTypes,
			IReadOnlyDictionary<string, string> aiTriggerTypes)
		{
			Triggers = triggers;
			Events = events;
			Actions = actions;
			Tags = tags;
			TaskForces = taskForces;
			ScriptTypes = scriptTypes;
			TeamTypes = teamTypes;
			AITriggerTypes = aiTriggerTypes;
		}

		public static Ra2MissionSource Parse(Stream stream)
		{
			if (stream == null)
				throw new ArgumentNullException(nameof(stream));

			var ini = new IniFile(stream);
			var triggers = Section(ini, "Triggers").ToDictionary(
				kv => kv.Key,
				kv =>
				{
					var fields = Fields(kv.Value);
					return new Ra2MissionTrigger(
						kv.Key,
						At(fields, 0),
						At(fields, 1),
						At(fields, 2),
						IsTrue(At(fields, 3)),
						kv.Value);
				}, StringComparer.OrdinalIgnoreCase);
			var events = ParseOperations(ini, "Events", 3);
			var actions = ParseOperations(ini, "Actions", 8);
			var tags = Section(ini, "Tags").ToDictionary(
				kv => kv.Key,
				kv =>
				{
					var fields = Fields(kv.Value);
					return new Ra2MissionTag(
						kv.Key,
						ParseInt(At(fields, 0), 0),
						At(fields, 1),
						At(fields, 2),
						kv.Value);
				}, StringComparer.OrdinalIgnoreCase);
			var taskForces = ParseTaskForces(ini);
			var scripts = ParseScripts(ini);
			var teams = Section(ini, "TeamTypes").ToDictionary(
				kv => kv.Value,
				kv =>
				{
					var id = kv.Value;
					var section = ini.GetSection(id, true);
					return new Ra2MissionTeamType(id,
						section.GetValue("House", string.Empty),
						section.GetValue("TaskForce", string.Empty),
						section.GetValue("Script", string.Empty),
						ParseWaypoint(section.GetValue("Waypoint", "-1")),
						IsTrue(section.GetValue("Full", "no")),
						Serialize(section));
				}, StringComparer.OrdinalIgnoreCase);
			var aiTriggers = Section(ini, "AITriggerTypes").ToDictionary(
				kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase);

			foreach (var team in teams.Values)
			{
				if (!IsNone(team.TaskForce) && !taskForces.ContainsKey(team.TaskForce))
					throw new InvalidDataException($"TeamType {team.Key} references missing TaskForce {team.TaskForce}.");
				if (!IsNone(team.Script) && !scripts.ContainsKey(team.Script))
					throw new InvalidDataException($"TeamType {team.Key} references missing ScriptType {team.Script}.");
			}

			return new Ra2MissionSource(
				ReadOnly(triggers), ReadOnly(events), ReadOnly(actions), ReadOnly(tags),
				ReadOnly(taskForces), ReadOnly(scripts), ReadOnly(teams), ReadOnly(aiTriggers));
		}

		static Dictionary<string, Ra2MissionOperationList> ParseOperations(IniFile ini, string section, int stride)
		{
			var result = new Dictionary<string, Ra2MissionOperationList>(StringComparer.OrdinalIgnoreCase);
			foreach (var kv in Section(ini, section))
			{
				var fields = Fields(kv.Value);
				if (!int.TryParse(At(fields, 0), out var count) || count < 0)
					throw new InvalidDataException($"{section} entry {kv.Key} has an invalid operation count.");

				var operations = new List<Ra2MissionOperation>(count);
				for (var i = 0; i < count; i++)
				{
					var offset = 1 + i * stride;
					if (offset >= fields.Length || !int.TryParse(fields[offset], out var opcode))
						throw new InvalidDataException($"{section} entry {kv.Key} has an invalid opcode at index {i}.");
					var parameters = fields.Skip(offset + 1).Take(stride - 1).ToArray();
					operations.Add(new Ra2MissionOperation(opcode, Array.AsReadOnly(parameters), kv.Value));
				}

				result.Add(kv.Key, new Ra2MissionOperationList(kv.Key, operations.AsReadOnly(), kv.Value));
			}

			return result;
		}

		static Dictionary<string, Ra2MissionTaskForce> ParseTaskForces(IniFile ini)
		{
			var result = new Dictionary<string, Ra2MissionTaskForce>(StringComparer.OrdinalIgnoreCase);
			foreach (var kv in Section(ini, "TaskForces"))
			{
				var id = kv.Value;
				var section = ini.GetSection(id, true);
				var entries = new List<Ra2MissionTaskForceEntry>();
				foreach (var entry in section.Where(e => int.TryParse(e.Key, out _)).OrderBy(e => NumericKey(e.Key)))
				{
					var fields = Fields(entry.Value);
					if (fields.Length < 2 || !int.TryParse(fields[0], out var count))
						throw new InvalidDataException($"TaskForce {id} has an invalid entry {entry.Key}.");
					entries.Add(new Ra2MissionTaskForceEntry(count, fields[1]));
				}

				result.Add(id, new Ra2MissionTaskForce(id, entries.AsReadOnly(), Serialize(section)));
			}

			return result;
		}

		static Dictionary<string, Ra2MissionScript> ParseScripts(IniFile ini)
		{
			var result = new Dictionary<string, Ra2MissionScript>(StringComparer.OrdinalIgnoreCase);
			foreach (var kv in Section(ini, "ScriptTypes"))
			{
				var id = kv.Value;
				var section = ini.GetSection(id, true);
				var steps = new List<Ra2MissionScriptStep>();
				foreach (var entry in section.Where(e => int.TryParse(e.Key, out _)).OrderBy(e => NumericKey(e.Key)))
				{
					var fields = Fields(entry.Value);
					if (fields.Length < 2 || !int.TryParse(fields[0], out var opcode) || !int.TryParse(fields[1], out var argument))
						throw new InvalidDataException($"ScriptType {id} has an invalid step {entry.Key}.");
					steps.Add(new Ra2MissionScriptStep(opcode, argument));
				}

				result.Add(id, new Ra2MissionScript(id, steps.AsReadOnly(), Serialize(section)));
			}

			return result;
		}

		static IEnumerable<KeyValuePair<string, string>> Section(IniFile ini, string name)
			=> ini.GetSection(name, true);

		static string[] Fields(string value)
			=> (value ?? string.Empty).Split(',').Select(field => field.Trim()).ToArray();

		static string At(string[] fields, int index)
			=> index >= 0 && index < fields.Length ? fields[index] : string.Empty;

		static int NumericKey(string key)
			=> int.TryParse(key, out var value) ? value : int.MaxValue;

		static int ParseInt(string value, int fallback)
			=> int.TryParse(value, out var parsed) ? parsed : fallback;

		public static int ParseWaypoint(string value)
		{
			if (int.TryParse(value, out var numeric))
				return numeric;
			if (string.IsNullOrWhiteSpace(value))
				return -1;

			var result = 0;
			foreach (var character in value.Trim().ToUpperInvariant())
			{
				if (character < 'A' || character > 'Z')
					return -1;
				result = result * 26 + character - 'A' + 1;
			}

			return result - 1;
		}

		static bool IsTrue(string value)
			=> value == "1" || value.Equals("yes", StringComparison.OrdinalIgnoreCase) ||
				value.Equals("true", StringComparison.OrdinalIgnoreCase);

		static string Serialize(IniSection section)
			=> string.Join(";", section.Select(kv => $"{kv.Key}={kv.Value}"));

		static bool IsNone(string value)
			=> string.IsNullOrWhiteSpace(value) || value.Equals("<none>", StringComparison.OrdinalIgnoreCase);

		static IReadOnlyDictionary<string, T> ReadOnly<T>(Dictionary<string, T> source)
			=> new ReadOnlyDictionary<string, T>(source);
	}
}
