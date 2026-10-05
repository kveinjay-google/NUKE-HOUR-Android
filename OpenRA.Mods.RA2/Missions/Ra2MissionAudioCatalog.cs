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
using OpenRA.Mods.Common.FileFormats;

namespace OpenRA.Mods.RA2.Missions
{
	public static class Ra2MissionAudioCatalog
	{
		public static IReadOnlyList<string> Candidates(
			string key, string evaSide, IniFile eva, IniFile sounds)
		{
			if (string.IsNullOrWhiteSpace(key))
				return Array.Empty<string>();

			var candidates = new List<string>();
			if (eva != null)
			{
				var section = eva.GetSection(key, true);
				Add(candidates, section.GetValue(evaSide, string.Empty));
				Add(candidates, section.GetValue(
					evaSide.Equals("Russian", StringComparison.OrdinalIgnoreCase) ? "Allied" : "Russian",
					string.Empty));
			}

			if (sounds != null)
				foreach (var candidate in sounds.GetSection(key, true).GetValue("Sounds", string.Empty)
					.Split(',').SelectMany(group => group.Split((char[])null,
						StringSplitOptions.RemoveEmptyEntries)))
					Add(candidates, candidate);

			Add(candidates, key);
			return candidates.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
		}

		static void Add(ICollection<string> candidates, string candidate)
		{
			candidate = candidate?.Trim().TrimStart('$');
			if (!string.IsNullOrWhiteSpace(candidate) && !candidate.StartsWith(';'))
				candidates.Add(candidate);
		}
	}
}
