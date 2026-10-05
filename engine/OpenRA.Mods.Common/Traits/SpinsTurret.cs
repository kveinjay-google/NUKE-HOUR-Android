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

using System.Linq;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	[Desc("Continuously rotates a turret around its vertical axis.")]
	public sealed class SpinsTurretInfo : PausableConditionalTraitInfo, Requires<TurretedInfo>
	{
		[Desc("Turreted 'Turret' key to rotate.")]
		public readonly string Turret = "primary";

		[Desc("Rotation applied each tick. Negative values rotate in the opposite direction.")]
		public readonly WAngle Speed = new(8);

		public override object Create(ActorInitializer init) { return new SpinsTurret(init, this); }
	}

	public sealed class SpinsTurret : PausableConditionalTrait<SpinsTurretInfo>, ITick
	{
		readonly Turreted turret;

		public SpinsTurret(ActorInitializer init, SpinsTurretInfo info)
			: base(info)
		{
			turret = init.Self.TraitsImplementing<Turreted>()
				.First(t => t.Name == info.Turret);
		}

		void ITick.Tick(Actor self)
		{
			if (!IsTraitDisabled && !IsTraitPaused)
				turret.Rotate(Info.Speed);
		}
	}
}
