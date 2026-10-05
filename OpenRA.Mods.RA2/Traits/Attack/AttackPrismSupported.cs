// Copyright 2015- OpenRA.Mods.RA2 Developers (see AUTHORS)
// This file is a part of a third-party plugin for OpenRA, which is
// free software. It is made available to you under the terms of the
// GNU General Public License as published by the Free Software
// Foundation. For more information, see COPYING.

using System;
using System.Collections.Generic;
using System.Linq;
using OpenRA.Activities;
using OpenRA.Mods.Cnc.Traits;
using OpenRA.Mods.Common.Activities;
using OpenRA.Mods.Common.Traits;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.RA2.Traits
{
	internal readonly struct PrismSupportCandidate<T>
	{
		public readonly T Value;
		public readonly uint ActorId;
		public readonly long DistanceSquared;
		public readonly bool Eligible;

		public PrismSupportCandidate(T value, uint actorId, long distanceSquared, bool eligible)
		{
			Value = value;
			ActorId = actorId;
			DistanceSquared = distanceSquared;
			Eligible = eligible;
		}
	}

	internal static class PrismTowerForwardingPolicy
	{
		public static IEnumerable<PrismSupportCandidate<T>> Select<T>(
			IEnumerable<PrismSupportCandidate<T>> candidates, int maxSupporters)
		{
			if (maxSupporters < 0)
				throw new ArgumentOutOfRangeException(nameof(maxSupporters));

			return candidates
				.Where(candidate => candidate.Eligible)
				.OrderBy(candidate => candidate.DistanceSquared)
				.ThenBy(candidate => candidate.ActorId)
				.Take(maxSupporters);
		}

		public static int SynchronizedSupportReloadDelay(int reloadDelay, int forwardingLead)
		{
			return checked(reloadDelay + forwardingLead);
		}
	}

	[Desc("Implements the charge-and-forward attack used by the RA2 Prism Tower.")]
	public class AttackPrismSupportedInfo : AttackBaseInfo
	{
		[Desc("How many attacks are available after recharging.")]
		public readonly int MaxCharges = 1;

		[Desc("Reload time for all charges (in ticks).")]
		public readonly int ReloadDelay = 60;

		[Desc("Delay before the firing animation completes (in ticks).")]
		public readonly int InitialChargeDelay = 22;

		[Desc("Delay after firing (in ticks).")]
		public readonly int ChargeDelay = 3;

		[Desc("Extra delay reserved for the forwarding beam to arrive (in ticks).")]
		public readonly int TicksPerHop = 3;

		[Desc("Maximum number of nearby idle towers that may forward power.")]
		public readonly int MaxSupporters = 3;

		[Desc("Armament used to draw the forwarding beam.")]
		public readonly string SupportArmament = "support";

		[Desc("Only actors with a matching support type may forward power.")]
		public readonly string SupportType = "prism";

		[Desc("Forwarding beam target offset on the receiving tower.")]
		public readonly WVec ReceiverOffset = WVec.Zero;

		[GrantedConditionReference]
		[Desc("Stacked condition granted for each successful forwarding beam.")]
		public readonly string BuffCondition = "prism-stack";

		[Desc("Sound played while charging.")]
		public readonly string ChargeAudio = null;

		public override object Create(ActorInitializer init)
		{
			return new AttackPrismSupported(init.Self, this);
		}
	}

	public class AttackPrismSupported : AttackBase, ITick, INotifyAttack, INotifyBecomingIdle
	{
		readonly AttackPrismSupportedInfo info;
		readonly Stack<int> buffTokens = new();
		readonly Lazy<Armament> supportArmament;

		[Sync]
		int charges;

		[Sync]
		int timeToRecharge;

		public AttackPrismSupported(Actor self, AttackPrismSupportedInfo info)
			: base(self, info)
		{
			this.info = info;
			charges = info.MaxCharges;
			supportArmament = new Lazy<Armament>(() => self.TraitsImplementing<Armament>()
				.First(a => a.Info.Name == info.SupportArmament));
		}

		protected override bool CanAttack(Actor self, in Target target)
		{
			return IsReachableTarget(target, true) && base.CanAttack(self, target);
		}

		void ITick.Tick(Actor self)
		{
			if (--timeToRecharge <= 0)
				charges = info.MaxCharges;
		}

		void INotifyAttack.Attacking(Actor self, in Target target, Armament armament, Barrel barrel)
		{
			if (armament.Info.Name != info.SupportArmament)
				ConsumeCharge();
		}

		void INotifyAttack.PreparingAttack(Actor self, in Target target, Armament armament, Barrel barrel) { }

		void ConsumeCharge(int forwardingLead = 0)
		{
			charges = Math.Max(0, charges - 1);
			timeToRecharge = PrismTowerForwardingPolicy.SynchronizedSupportReloadDelay(
				info.ReloadDelay, forwardingLead);
		}

		bool MaySupport(Actor self, Actor receiver, bool mustBeIdle)
		{
			if (self.IsDead || !self.IsInWorld || receiver.IsDead || !receiver.IsInWorld)
				return false;

			if (self.Owner != receiver.Owner || IsTraitDisabled || IsTraitPaused || charges == 0)
				return false;

			if (mustBeIdle && !self.IsIdle)
				return false;

			var receiverAttack = receiver.TraitOrDefault<AttackPrismSupported>();
			return receiverAttack != null && receiverAttack.info.SupportType == info.SupportType;
		}

		bool CheckSupportRange(Actor self, Actor receiver)
		{
			return Target.FromActor(receiver).IsInRange(self.CenterPosition, supportArmament.Value.MaxRange());
		}

		bool FireSupportArmament(Actor self, Actor receiver, int forwardingLead)
		{
			if (!MaySupport(self, receiver, false) || !CheckSupportRange(self, receiver))
				return false;

			var receiverAttack = receiver.Trait<AttackPrismSupported>();
			var target = Target.FromPos(receiver.CenterPosition + receiverAttack.info.ReceiverOffset);
			if (!supportArmament.Value.CheckFire(self, facing, target))
				return false;

			ConsumeCharge(forwardingLead);
			receiverAttack.AddBuffStack(receiver);
			return true;
		}

		void AddBuffStack(Actor self)
		{
			buffTokens.Push(self.GrantCondition(info.BuffCondition));
		}

		void ClearBuffStack(Actor self)
		{
			while (buffTokens.Count > 0)
				self.RevokeCondition(buffTokens.Pop());
		}

		void INotifyBecomingIdle.OnBecomingIdle(Actor self)
		{
			ClearBuffStack(self);
		}

		public override Activity GetAttackActivity(
			Actor self, AttackSource source, in Target newTarget, bool allowMove, bool forceAttack,
			Color? targetLineColor = null)
		{
			return new ChargeSupportedAttack(this, newTarget, forceAttack, targetLineColor);
		}

		sealed class ChargeSupportedAttack : Activity, IActivityNotifyStanceChanged
		{
			readonly AttackPrismSupported attack;
			readonly Target target;
			readonly bool forceAttack;
			readonly Color? targetLineColor;

			public ChargeSupportedAttack(
				AttackPrismSupported attack, in Target target, bool forceAttack, Color? targetLineColor)
			{
				this.attack = attack;
				this.target = target;
				this.forceAttack = forceAttack;
				this.targetLineColor = targetLineColor;
			}

			IEnumerable<Actor> RecruitSupporters(Actor self)
			{
				var candidates = self.World.ActorsHavingTrait<AttackPrismSupported>()
					.Where(actor => actor != self)
					.Select(actor =>
					{
						var candidateAttack = actor.Trait<AttackPrismSupported>();
						var eligible = candidateAttack.MaySupport(actor, self, true)
							&& candidateAttack.CheckSupportRange(actor, self);
						return new PrismSupportCandidate<Actor>(actor, actor.ActorID,
							(actor.CenterPosition - self.CenterPosition).HorizontalLengthSquared, eligible);
					});

				return PrismTowerForwardingPolicy.Select(candidates, attack.info.MaxSupporters)
					.Select(candidate => candidate.Value);
			}

			public override bool Tick(Actor self)
			{
				if (IsCanceling || !attack.CanAttack(self, target))
					return true;

				if (attack.charges == 0)
					return false;

				foreach (var notify in self.TraitsImplementing<INotifyTeslaCharging>())
					notify.Charging(self, target);

				if (!string.IsNullOrEmpty(attack.info.ChargeAudio))
					Game.Sound.Play(SoundType.World, attack.info.ChargeAudio, self.CenterPosition);

				foreach (var supporter in RecruitSupporters(self))
				{
					var supporterAttack = supporter.Trait<AttackPrismSupported>();
					supporter.QueueActivity(new FireSupportingWeapon(
						supporterAttack, self, attack.info.TicksPerHop, Color.OrangeRed));
				}

				QueueChild(new Wait(attack.info.InitialChargeDelay + attack.info.TicksPerHop));
				QueueChild(new ChargeFireBuffed(attack, target));
				return false;
			}

			protected override void OnLastRun(Actor self)
			{
				attack.ClearBuffStack(self);
				base.OnLastRun(self);
			}

			void IActivityNotifyStanceChanged.StanceChanged(
				Actor self, AutoTarget autoTarget, UnitStance oldStance, UnitStance newStance)
			{
				if (newStance > oldStance || forceAttack)
					return;

				if (target.Type == TargetType.Actor)
				{
					var actor = target.Actor;
					if (!autoTarget.HasValidTargetPriority(self, actor.Owner, actor.GetEnabledTargetTypes()))
						Cancel(self, true);
				}
				else if (target.Type == TargetType.FrozenActor)
				{
					var frozenActor = target.FrozenActor;
					if (!autoTarget.HasValidTargetPriority(self, frozenActor.Owner, frozenActor.TargetTypes))
						Cancel(self, true);
				}
			}

			public override IEnumerable<TargetLineNode> TargetLineNodes(Actor self)
			{
				if (targetLineColor != null)
					yield return new TargetLineNode(target, targetLineColor.Value);
			}
		}

		sealed class FireSupportingWeapon : Activity
		{
			readonly AttackPrismSupported attack;
			readonly Actor receiver;
			readonly int forwardingDelay;
			readonly Color? targetLineColor;

			public FireSupportingWeapon(
				AttackPrismSupported attack, Actor receiver, int forwardingDelay, Color? targetLineColor)
			{
				this.attack = attack;
				this.receiver = receiver;
				this.forwardingDelay = forwardingDelay;
				this.targetLineColor = targetLineColor;
			}

			public override bool Tick(Actor self)
			{
				if (IsCanceling || !attack.MaySupport(self, receiver, false) || receiver.IsIdle)
					return true;

				var target = Target.FromActor(receiver);
				foreach (var notify in self.TraitsImplementing<INotifyTeslaCharging>())
					notify.Charging(self, target);

				if (!string.IsNullOrEmpty(attack.info.ChargeAudio))
					Game.Sound.Play(SoundType.World, attack.info.ChargeAudio, self.CenterPosition);

				QueueChild(new Wait(attack.info.InitialChargeDelay));
				QueueChild(new ChargeAndFireSupportWeapon(attack, receiver, forwardingDelay));
				return true;
			}

			public override IEnumerable<TargetLineNode> TargetLineNodes(Actor self)
			{
				if (targetLineColor != null)
					yield return new TargetLineNode(Target.FromActor(receiver), targetLineColor.Value);
			}
		}

		sealed class ChargeAndFireSupportWeapon : Activity
		{
			readonly AttackPrismSupported attack;
			readonly Actor receiver;
			readonly int forwardingDelay;

			public ChargeAndFireSupportWeapon(AttackPrismSupported attack, Actor receiver, int forwardingDelay)
			{
				this.attack = attack;
				this.receiver = receiver;
				this.forwardingDelay = forwardingDelay;
			}

			public override bool Tick(Actor self)
			{
				if (IsCanceling || receiver.IsIdle)
					return true;

				if (!attack.FireSupportArmament(self, receiver, forwardingDelay))
					return true;

				QueueChild(new Wait(forwardingDelay));
				return true;
			}
		}

		sealed class ChargeFireBuffed : Activity
		{
			readonly AttackPrismSupported attack;
			readonly Target target;

			public ChargeFireBuffed(AttackPrismSupported attack, in Target target)
			{
				this.attack = attack;
				this.target = target;
			}

			public override bool Tick(Actor self)
			{
				if (IsCanceling || !attack.CanAttack(self, target) || attack.charges == 0)
					return true;

				attack.DoAttack(self, target);
				attack.ClearBuffStack(self);
				QueueChild(new Wait(attack.info.ChargeDelay));
				return false;
			}
		}
	}
}
