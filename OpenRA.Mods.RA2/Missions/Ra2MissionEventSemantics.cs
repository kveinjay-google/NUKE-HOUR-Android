#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version.
 */
#endregion

using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace OpenRA.Mods.RA2.Missions
{
	public static class Ra2MissionEventSemantics
	{
		static readonly int[] Supported =
		{
			1, 2, 4, 5, 6, 7, 8, 9, 10, 11, 13, 14, 15, 16, 17,
			19, 20, 21, 22, 25, 26, 27, 28, 29, 30, 31, 32, 33,
			36, 37, 39, 40, 41, 42, 43, 44, 47, 48, 51, 52, 55, 56, 57,
		};

		static readonly HashSet<int> ParameterHouseOpcodes = new()
		{
			1, 5, 9, 10, 11, 30, 44, 55, 56,
		};

		public static IReadOnlyCollection<int> SupportedOpcodes { get; } =
			new ReadOnlyCollection<int>(Supported);

		public static string OwnerReference(int opcode, string triggerHouse, string parameter)
			=> ParameterHouseOpcodes.Contains(opcode) ? parameter : triggerHouse;
	}
}
