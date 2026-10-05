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
using System.Collections.Generic;

namespace OpenRA.Mods.RA2.Missions
{
	public static class Ra2ActorTypeCatalog
	{
		static readonly IReadOnlyDictionary<string, string> Aliases =
			new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
			{
				{ "adog", "dog" },
				{ "ctech", "civ1" },
				{ "horv", "harv" },
				{ "napsya", "napsis" },
				{ "napsyb", "napsis" },
				{ "sengineer", "engineer" },
				{ "snonitlamp", "galite" },
			};

		public static string Normalize(string actorType)
		{
			var normalized = (actorType ?? string.Empty).Trim().ToLowerInvariant();
			return Aliases.TryGetValue(normalized, out var replacement) ? replacement : normalized;
		}
	}
}
