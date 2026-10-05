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
using OpenRA.GameRules;
using OpenRA.Mods.Common;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Common.Warheads;
using OpenRA.Mods.RA2.Traits;

namespace OpenRA.Mods.RA2.Warheads
{
	[Desc("Accumulates temporal erasure damage without reducing conventional health.")]
	public sealed class TemporalDamageWarhead : TargetDamageWarhead
	{
		protected override void InflictDamage(Actor victim, Actor firedBy, HitShape shape, WarheadArgs args)
		{
			var affected = victim.TraitOrDefault<AffectedByTemporal>();
			if (affected == null || affected.IsTraitDisabled)
				return;

			var damage = Util.ApplyPercentageModifiers(
				Damage, args.DamageModifiers.Append(DamageVersus(victim, shape, args)));
			affected.AddDamage(damage, firedBy, DamageTypes);
		}
	}
}
