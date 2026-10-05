#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version.
 */
#endregion

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using OpenRA.Mods.Common.FileFormats;

namespace OpenRA.Mods.RA2.Missions
{
	public sealed record Ra2CampaignCompletionRoute(
		string WinningTrigger,
		string WinnerReference,
		string ResolvedWinner,
		IReadOnlyList<string> TriggerPath,
		IReadOnlyList<int> UnsupportedEventOpcodes);

	public sealed record Ra2CampaignCompletionAnalysis(
		bool CanComplete,
		IReadOnlyList<Ra2CampaignCompletionRoute> Routes,
		IReadOnlyList<string> UnreachableWinningTriggers,
		IReadOnlyList<string> UnresolvedWinningTriggers);

	public static class Ra2CampaignCompletionAnalyzer
	{
		public static Ra2CampaignCompletionAnalysis Analyze(
			Ra2MissionSource source,
			IniFile mission,
			string playerHouse,
			IEnumerable<int> supportedEventOpcodes)
			=> Analyze(source, mission, new[] { playerHouse }, supportedEventOpcodes);

		public static Ra2CampaignCompletionAnalysis Analyze(
			Ra2MissionSource source,
			IniFile mission,
			IEnumerable<string> playerAndAlliedHouses,
			IEnumerable<int> supportedEventOpcodes)
		{
			if (source == null)
				throw new ArgumentNullException(nameof(source));
			if (mission == null)
				throw new ArgumentNullException(nameof(mission));

			var supported = new HashSet<int>(supportedEventOpcodes ?? Array.Empty<int>());
			var winningHouses = new HashSet<string>(
				playerAndAlliedHouses?.Where(house => !string.IsNullOrWhiteSpace(house)) ?? Array.Empty<string>(),
				StringComparer.OrdinalIgnoreCase);
			var predecessor = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
			var reachable = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			var queue = new Queue<string>();
			foreach (var trigger in source.Triggers.Values.Where(trigger => !trigger.Disabled))
			{
				reachable.Add(trigger.Key);
				queue.Enqueue(trigger.Key);
			}

			while (queue.Count > 0)
			{
				var key = queue.Dequeue();
				if (!source.Triggers.TryGetValue(key, out var trigger))
					continue;

				if (!string.IsNullOrWhiteSpace(trigger.LinkedTrigger) &&
					!trigger.LinkedTrigger.Equals("<none>", StringComparison.OrdinalIgnoreCase))
					AddReachable(trigger.LinkedTrigger, key, source, reachable, predecessor, queue);

				if (!source.Actions.TryGetValue(key, out var actions))
					continue;
				foreach (var action in actions.Operations.Where(action => action.Opcode == 22 || action.Opcode == 53))
					AddReachable(Parameter(action, 1), key, source, reachable, predecessor, queue);
			}

			var routes = new List<Ra2CampaignCompletionRoute>();
			var unreachable = new List<string>();
			var unresolved = new List<string>();
			foreach (var trigger in source.Triggers.Values)
			{
				if (!source.Actions.TryGetValue(trigger.Key, out var actions))
					continue;
				foreach (var win in actions.Operations.Where(action => action.Opcode == 1))
				{
					if (!reachable.Contains(trigger.Key))
					{
						unreachable.Add(trigger.Key);
						continue;
					}

					var winnerReference = Parameter(win, 1);
					var winner = Ra2MissionHouseReference.Resolve(mission, winnerReference);
					if (winner == null)
						unresolved.Add(trigger.Key);

					var path = BuildPath(trigger.Key, predecessor);
					var unsupported = path
						.Where(source.Events.ContainsKey)
						.SelectMany(key => source.Events[key].Operations)
						.Select(operation => operation.Opcode)
						.Where(opcode => !supported.Contains(opcode))
						.Distinct()
						.OrderBy(opcode => opcode)
						.ToArray();
					routes.Add(new Ra2CampaignCompletionRoute(trigger.Key, winnerReference, winner,
						Array.AsReadOnly(path), Array.AsReadOnly(unsupported)));
				}
			}

			var canComplete = routes.Any(route =>
				route.UnsupportedEventOpcodes.Count == 0 &&
				route.ResolvedWinner != null && winningHouses.Contains(route.ResolvedWinner));
			return new Ra2CampaignCompletionAnalysis(canComplete,
				new ReadOnlyCollection<Ra2CampaignCompletionRoute>(routes),
				Array.AsReadOnly(unreachable.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(key => key).ToArray()),
				Array.AsReadOnly(unresolved.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(key => key).ToArray()));
		}

		static void AddReachable(
			string target,
			string from,
			Ra2MissionSource source,
			ISet<string> reachable,
			IDictionary<string, string> predecessor,
			Queue<string> queue)
		{
			if (string.IsNullOrWhiteSpace(target) || !source.Triggers.ContainsKey(target) || !reachable.Add(target))
				return;

			predecessor[target] = from;
			queue.Enqueue(target);
		}

		static string[] BuildPath(string target, IReadOnlyDictionary<string, string> predecessor)
		{
			var path = new List<string> { target };
			while (predecessor.TryGetValue(path[^1], out var parent))
				path.Add(parent);
			path.Reverse();
			return path.ToArray();
		}

		static string Parameter(Ra2MissionOperation operation, int index)
			=> index >= 0 && index < operation.Parameters.Count ? operation.Parameters[index] : string.Empty;
	}
}
