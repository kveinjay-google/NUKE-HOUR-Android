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
using System.Linq;

namespace OpenRA.Mods.Common
{
	public static class CampaignProgress
	{
		public static bool IsUnlocked(string prerequisite, IEnumerable<string> completedMissions)
		{
			if (string.IsNullOrWhiteSpace(prerequisite))
				return true;

			return completedMissions != null && completedMissions.Any(
				mission => string.Equals(mission?.Trim(), prerequisite.Trim(), StringComparison.OrdinalIgnoreCase));
		}

		public static string[] AddCompletion(IEnumerable<string> completedMissions, string missionId)
		{
			var result = (completedMissions ?? Array.Empty<string>())
				.Where(mission => !string.IsNullOrWhiteSpace(mission))
				.Select(mission => mission.Trim())
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.ToList();

			if (string.IsNullOrWhiteSpace(missionId))
				return result.ToArray();

			var normalized = missionId.Trim();
			if (!result.Contains(normalized, StringComparer.OrdinalIgnoreCase))
				result.Add(normalized);

			return result.ToArray();
		}

		public static bool IsCompleted(string missionId, IEnumerable<string> completedMissions)
			=> !string.IsNullOrWhiteSpace(missionId) && completedMissions != null && completedMissions.Any(
				mission => string.Equals(mission, missionId, StringComparison.OrdinalIgnoreCase));

		public static bool IsCompleted(string missionId)
			=> IsCompleted(missionId, Game.Settings.Game.CompletedCampaignMissions);

		public static bool IsUnlocked(string prerequisite)
			=> IsUnlocked(prerequisite, Game.Settings.Game.CompletedCampaignMissions);

		public static bool RecordCompleted(string missionId)
		{
			var current = Game.Settings.Game.CompletedCampaignMissions;
			var updated = AddCompletion(current, missionId);
			if (updated.SequenceEqual(current ?? Array.Empty<string>(), StringComparer.Ordinal))
				return false;

			Game.Settings.Game.CompletedCampaignMissions = updated;
			Game.Settings.Save();
			return true;
		}
	}
}
