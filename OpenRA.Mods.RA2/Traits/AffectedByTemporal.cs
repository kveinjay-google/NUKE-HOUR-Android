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
using OpenRA.Mods.Common.Traits;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.RA2.Traits
{
	[Desc("Allows this actor to be frozen and erased by temporal weapons.")]
	public sealed class AffectedByTemporalInfo : ConditionalTraitInfo, Requires<IHealthInfo>
	{
		[GrantedConditionReference]
		[Desc("Condition granted while temporal erasure is in progress.")]
		public readonly string Condition = null;

		[Desc("Ticks without a temporal hit before erasure progress begins to recover.")]
		public readonly int RevokeDelay = 8;

		[Desc("Temporal damage removed per tick after RevokeDelay. Use 0 to disable recovery.")]
		public readonly int RecoveryRate = 8;

		[Desc("Fixed temporal damage required to erase the actor. Use -1 to derive it from maximum health.")]
		public readonly int EraseDamage = -1;

		[Desc("Percentage of maximum health required when EraseDamage is -1.")]
		public readonly int EraseDamageMultiplier = 500;

		[Desc("Damage types used for kill attribution, objectives, and death-effect filtering.")]
		public readonly BitSet<DamageType> EraseDamageTypes = new("Temporal");

		[Desc("Sounds played when temporal erasure completes.")]
		public readonly string[] EraseSounds = Array.Empty<string>();

		[Desc("Allow the erase sound through shroud and fog.")]
		public readonly bool AudibleThroughFog = false;

		[Desc("Erase sound volume.")]
		public readonly float SoundVolume = 1f;

		[Desc("Display temporal erasure progress in the selection bars.")]
		public readonly bool ShowSelectionBar = true;

		[Desc("Color of the temporal erasure selection bar.")]
		public readonly Color SelectionBarColor = Color.White;

		public override object Create(ActorInitializer init) { return new AffectedByTemporal(init, this); }
	}

	public sealed class AffectedByTemporal : ConditionalTrait<AffectedByTemporalInfo>, ITick, ISelectionBar
	{
		readonly Actor self;
		readonly int requiredDamage;

		[Sync]
		int temporalDamage;

		[Sync]
		int recoveryDelay;

		int conditionToken = Actor.InvalidConditionToken;
		Actor lastDamager;

		public AffectedByTemporal(ActorInitializer init, AffectedByTemporalInfo info)
			: base(info)
		{
			self = init.Self;
			var health = self.Info.TraitInfo<IHealthInfo>();
			var healthScaledDamage = (long)health.MaxHP * Math.Max(0, info.EraseDamageMultiplier) / 100;
			requiredDamage = info.EraseDamage >= 0 ? Math.Max(1, info.EraseDamage) :
				(int)Math.Clamp(healthScaledDamage, 1, int.MaxValue);
		}

		public void AddDamage(int damage, Actor damager, BitSet<DamageType> damageTypes)
		{
			if (IsTraitDisabled || damage <= 0 || self.IsDead || self.Disposed)
				return;

			lastDamager = damager;
			recoveryDelay = Math.Max(0, Info.RevokeDelay);
			temporalDamage = (int)Math.Min(requiredDamage, (long)temporalDamage + damage);

			if (conditionToken == Actor.InvalidConditionToken && !string.IsNullOrEmpty(Info.Condition))
				conditionToken = self.GrantCondition(Info.Condition);

			if (temporalDamage < requiredDamage)
				return;

			// A real kill (rather than Dispose) preserves score, campaign objective,
			// and attacker attribution while the Temporal damage type suppresses
			// conventional death animations and wrecks.
			self.Kill(lastDamager, damageTypes.IsEmpty ? Info.EraseDamageTypes : damageTypes);
			PlayEraseSound();
		}

		void ITick.Tick(Actor self)
		{
			if (temporalDamage <= 0 || self.IsDead)
				return;

			// RejectsOrders clears the activity when freezing starts. Keep clearing
			// any internally generated AutoTarget activity until time resumes too.
			self.CancelActivity();

			if (recoveryDelay > 0)
			{
				recoveryDelay--;
				return;
			}

			if (Info.RecoveryRate > 0)
				temporalDamage = Math.Max(0, temporalDamage - Info.RecoveryRate);

			if (temporalDamage == 0)
				RevokeCondition();
		}

		void PlayEraseSound()
		{
			if (Info.EraseSounds.Length == 0)
				return;

			var position = self.CenterPosition;
			if (!Info.AudibleThroughFog && (self.World.ShroudObscures(position) || self.World.FogObscures(position)))
				return;

			Game.Sound.Play(SoundType.World, Info.EraseSounds.Random(self.World.LocalRandom), position, Info.SoundVolume);
		}

		void RevokeCondition()
		{
			if (conditionToken != Actor.InvalidConditionToken)
				conditionToken = self.RevokeCondition(conditionToken);
		}

		float ISelectionBar.GetValue()
		{
			if (!Info.ShowSelectionBar || temporalDamage <= 0)
				return 0;

			return Math.Clamp((float)temporalDamage / requiredDamage, 0f, 1f);
		}

		bool ISelectionBar.DisplayWhenEmpty => false;

		Color ISelectionBar.GetColor() { return Info.SelectionBarColor; }

		protected override void TraitDisabled(Actor self)
		{
			temporalDamage = 0;
			recoveryDelay = 0;
			lastDamager = null;
			RevokeCondition();
		}
	}
}
