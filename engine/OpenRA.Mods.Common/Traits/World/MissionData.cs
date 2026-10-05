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

using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	[Desc("Defines the FMVs that can be played by missions.")]
	[TraitLocation(SystemActors.World)]
	public class MissionDataInfo : TraitInfo<MissionData>
	{
		[Desc("Stable mission identifier used for local campaign progression.")]
		public readonly string MissionId;

		[Desc("Fluent key used as the mission title in the mission browser.")]
		public readonly string BrowserTitle;

		[Desc("Mission identifier that must be completed before this mission can be started.")]
		public readonly string UnlockPrerequisite;

		[Desc("Briefing text displayed in the mission browser.")]
		public readonly string Briefing;

		[Desc("Prevents players from changing mission-defined lobby options.")]
		public readonly bool FixedRules;

		[Desc("Mission theme text or Fluent key displayed in the mission browser.")]
		public readonly string Theme;

		[Desc("Alternate-history background text or Fluent key displayed in the mission browser.")]
		public readonly string HistoricalBackground;

		[Desc("Primary objective text or Fluent key displayed in the mission browser.")]
		public readonly string PrimaryObjectives;

		[Desc("Starting assets text or Fluent key displayed in the mission browser.")]
		public readonly string StartingAssets;

		[Desc("Operational notes text or Fluent key displayed in the mission browser.")]
		public readonly string OperationalNotes;

		[Desc("Played by the \"Background Info\" button in the mission browser.")]
		public readonly string BackgroundVideo;

		[Desc("Played by the \"Briefing\" button in the mission browser.")]
		public readonly string BriefingVideo;

		[Desc("Automatically played before starting the mission.")]
		public readonly string StartVideo;

		[Desc("Automatically played when the player wins the mission.")]
		public readonly string WinVideo;

		[Desc("Automatically played when the player loses the mission.")]
		public readonly string LossVideo;
	}

	public class MissionData { }
}
