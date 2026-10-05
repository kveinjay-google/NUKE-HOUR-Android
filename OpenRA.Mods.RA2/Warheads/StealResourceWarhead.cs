#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software. It is
 * made available under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System;
using OpenRA.GameRules;
using OpenRA.Mods.Common.Effects;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Common.Warheads;
using OpenRA.Traits;

namespace OpenRA.Mods.RA2.Warheads
{
	[Desc("Transfers cash and stored resources from the target owner to the firing actor's owner.")]
	public sealed class StealResourceWarhead : Warhead
	{
		[Desc("Maximum amount transferred on each impact.")]
		public readonly int Cash = 10;

		[Desc("Show the transferred amount above the target building.")]
		public readonly bool ShowTicks = true;

		public override void DoImpact(in Target target, WarheadArgs args)
		{
			var source = args.SourceActor;
			if (source == null || target.Type != TargetType.Actor || target.Actor == null ||
				!IsValidAgainst(target.Actor, source))
				return;

			var targetResources = target.Actor.Owner.PlayerActor.Trait<PlayerResources>();
			var sourceResources = source.Owner.PlayerActor.Trait<PlayerResources>();
			var stolen = Math.Min(Cash, targetResources.GetCashAndResources());
			if (stolen <= 0 || !targetResources.TakeCash(stolen))
				return;

			sourceResources.GiveCash(stolen);
			if (!ShowTicks)
				return;

			var targetActor = target.Actor;
			source.World.AddFrameEndTask(w => w.Add(new FloatingText(
				targetActor.CenterPosition,
				source.OwnerColor(),
				FloatingText.FormatCashTick(stolen),
				30)));
		}
	}
}
