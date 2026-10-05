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

namespace OpenRA.Mods.RA2.Missions
{
	public readonly struct Ra2BuildingWithProperty
	{
		public int BuildingIndex { get; }
		public int SelectionMode { get; }

		public Ra2BuildingWithProperty(int buildingIndex, int selectionMode)
		{
			BuildingIndex = buildingIndex;
			SelectionMode = selectionMode;
		}
	}

	public static class Ra2MissionRuntimeSemantics
	{
		static readonly int[] CampaignActions =
		{
			1, 2, 3, 4, 5, 6, 7, 9, 11, 12, 13, 14, 16, 17, 18, 19, 21, 22, 23, 24,
			25, 26, 27, 28, 29, 32, 36, 37, 38, 41, 46, 47, 48, 51, 53, 54, 55, 56, 57,
			60, 61, 63, 74, 75, 80, 99, 100, 101, 104, 107, 108, 111, 113, 114, 115, 116, 117,
		};

		static readonly int[] CampaignScripts =
		{
			0, 1, 3, 5, 6, 8, 9, 10, 11, 13, 14, 16, 19, 20, 21, 24, 34, 37, 39, 42, 43, 46, 47,
			48, 49, 50, 53, 54, 58,
		};

		static readonly int[] AlliedFirstMissionActions =
		{
			1, 2, 3, 4, 5, 7, 11, 12, 14, 17, 19, 21, 32, 36, 41, 46, 47, 48,
			53, 54, 55, 63, 74, 80, 99, 100, 101, 104, 108, 111, 113, 114, 115, 116, 117,
		};

		static readonly int[] AlliedFirstMissionScripts =
		{
			0, 1, 3, 5, 6, 8, 11, 19, 20, 37, 39, 46, 48, 49, 50,
		};

		public static IReadOnlyCollection<int> AlliedFirstMissionActionOpcodes { get; } =
			new ReadOnlyCollection<int>(AlliedFirstMissionActions);

		public static IReadOnlyCollection<int> AlliedFirstMissionScriptOpcodes { get; } =
			new ReadOnlyCollection<int>(AlliedFirstMissionScripts);

		public static IReadOnlyCollection<int> CampaignActionOpcodes { get; } =
			new ReadOnlyCollection<int>(CampaignActions);

		public static IReadOnlyCollection<int> CampaignScriptOpcodes { get; } =
			new ReadOnlyCollection<int>(CampaignScripts);

		public static int StartingCash(IEnumerable<int> retailCredits)
			=> Math.Max(0, retailCredits.DefaultIfEmpty(0).Max()) * 100;

		public static int GuardDurationTicks(int tenthsOfMinute, int ticksPerSecond)
			=> Math.Max(0, tenthsOfMinute) * 6 * Math.Max(1, ticksPerSecond);

		public static int ScriptLineIndex(int oneBasedLine)
			=> Math.Max(0, oneBasedLine - 1);

		public static bool KeepPlayerInputAvailableDuringOpening(string mission)
			=> mission?.Equals("allied-01", StringComparison.OrdinalIgnoreCase) == true;

		public static Ra2BuildingWithProperty DecodeBuildingWithProperty(int selector)
			=> new Ra2BuildingWithProperty(selector & 0xFFFF, (selector >> 16) & 3);
	}
}
