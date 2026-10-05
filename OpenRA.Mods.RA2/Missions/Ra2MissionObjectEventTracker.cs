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

namespace OpenRA.Mods.RA2.Missions
{
	public sealed class Ra2MissionObjectEventTracker
	{
		readonly HashSet<uint> infiltrated = new();
		readonly HashSet<uint> ownershipChanged = new();

		public void RecordInfiltration(uint actorId) => infiltrated.Add(actorId);

		public void RecordOwnershipChange(uint actorId) => ownershipChanged.Add(actorId);

		public bool WasInfiltrated(uint actorId) => infiltrated.Contains(actorId);

		public bool WasCapturedOrInfiltrated(uint actorId)
			=> infiltrated.Contains(actorId) || ownershipChanged.Contains(actorId);
	}
}
