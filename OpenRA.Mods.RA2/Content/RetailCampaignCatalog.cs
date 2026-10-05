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
using System.Linq;

namespace OpenRA.Mods.RA2.Content
{
	public sealed record RetailCampaignMission(
		string Id,
		string Group,
		int Order,
		string Archive,
		IReadOnlyList<string> SourceNames,
		string BrowserTitle,
		string UnlockPrerequisite,
		string CompatibilityScript,
		int CompatibilityRevision);

	public static class RetailCampaignCatalog
	{
		const int CurrentCompatibilityRevision = 10;

		static readonly string[] AlliedSources =
		{
			"all01t.map", "all02s.map", "all03u.map", "all04u.map", "all05s.map", "all06u.map",
			"all07t.map", "all08u.map", "all09t.map", "all10s.map", "all11t.map", "all12s.map",
		};

		static readonly string[] SovietSources =
		{
			"sov01t.map", "sov02t.map", "sov03u.map", "sov04s.map", "sov05u.map", "sov06t.map",
			"sov07s.map", "sov08u.map", "sov09u.map", "sov10t.map", "sov11s.map", "sov12s.map",
		};

		static readonly IReadOnlyList<RetailCampaignMission> MissionEntries = Build();
		static readonly IReadOnlyDictionary<string, RetailCampaignMission> MissionsBySource = BuildIndex(MissionEntries);

		public static IReadOnlyList<RetailCampaignMission> Missions => MissionEntries;

		public static RetailCampaignMission Resolve(string archive, string sourceName)
		{
			if (string.IsNullOrWhiteSpace(archive) || string.IsNullOrWhiteSpace(sourceName))
				return null;

			var key = Key(archive, sourceName);
			return MissionsBySource.TryGetValue(key, out var mission) ? mission : null;
		}

		static IReadOnlyList<RetailCampaignMission> Build()
		{
			var result = new List<RetailCampaignMission>(24);
			for (var i = 0; i < AlliedSources.Length; i++)
				result.Add(Create("allied", "Allied Campaign", "maps01.mix", AlliedSources[i], i + 1));
			for (var i = 0; i < SovietSources.Length; i++)
				result.Add(Create("soviet", "Soviet Campaign", "maps02.mix", SovietSources[i], i + 1));
			return new ReadOnlyCollection<RetailCampaignMission>(result);
		}

		static RetailCampaignMission Create(string prefix, string group, string archive, string source, int order)
		{
			var id = $"{prefix}-{order:00}";
			var prerequisite = order > 1 ? $"{prefix}-{order - 1:00}" : null;
			return new(id, group, order, archive, Array.AsReadOnly(new[] { source }),
				$"mission-{id}-title", prerequisite, "campaign-runtime.lua", CurrentCompatibilityRevision);
		}

		static IReadOnlyDictionary<string, RetailCampaignMission> BuildIndex(
			IEnumerable<RetailCampaignMission> entries)
		{
			var result = new Dictionary<string, RetailCampaignMission>(StringComparer.OrdinalIgnoreCase);
			foreach (var mission in entries)
				foreach (var source in mission.SourceNames)
				{
					var key = Key(mission.Archive, source);
					if (!result.TryAdd(key, mission))
						throw new InvalidOperationException($"Ambiguous retail campaign source: {key}");
				}

			return new ReadOnlyDictionary<string, RetailCampaignMission>(result);
		}

		static string Key(string archive, string source)
			=> $"{Leaf(archive)}|{Leaf(source)}";

		static string Leaf(string path)
		{
			var normalized = path.Replace('\\', '/');
			var separator = normalized.LastIndexOf('/');
			return (separator < 0 ? normalized : normalized[(separator + 1)..]).ToLowerInvariant();
		}
	}
}
