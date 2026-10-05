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
using System.Linq;
using OpenRA.Mods.Common.FileFormats;

namespace OpenRA.Mods.RA2.Missions
{
	public static class Ra2MissionHouseReference
	{
		public static string Resolve(IniFile mission, string reference)
		{
			if (mission == null || string.IsNullOrWhiteSpace(reference) ||
				reference.Equals("<none>", StringComparison.OrdinalIgnoreCase))
				return null;

			var houses = mission.GetSection("Houses", true);
			if (int.TryParse(reference, out var index))
			{
				if (index < 13)
					return houses.FirstOrDefault(entry =>
						int.TryParse(entry.Key, out var houseIndex) && houseIndex == index).Value;

				var countryIndex = index - 13;
				var country = mission.GetSection("Countries", true).FirstOrDefault(entry =>
					int.TryParse(entry.Key, out var candidate) && candidate == countryIndex).Value;
				return ResolveCountryHouse(mission, houses.Select(entry => entry.Value), country);
			}

			var direct = houses.Select(entry => entry.Value).FirstOrDefault(house =>
				house.Equals(reference, StringComparison.OrdinalIgnoreCase));
			if (!string.IsNullOrWhiteSpace(direct))
				return direct;

			return ResolveCountryHouse(mission, houses.Select(entry => entry.Value), reference);
		}

		static string ResolveCountryHouse(IniFile mission, System.Collections.Generic.IEnumerable<string> houses,
			string country)
		{
			if (string.IsNullOrWhiteSpace(country))
				return null;

			return houses.FirstOrDefault(house => mission.GetSection(house, true).GetValue("Country", "")
				.Equals(country, StringComparison.OrdinalIgnoreCase));
		}
	}
}
