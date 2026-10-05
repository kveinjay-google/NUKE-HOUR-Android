// Copyright (c) The OpenRA Developers and Contributors
// This file is available under the GNU General Public License, version 3 or later.

using System;
using System.Collections.Generic;
using System.Linq;
using OpenRA.Mods.Common.Traits;
using OpenRA.Support;
using OpenRA.Traits;

namespace OpenRA.Mods.RA2.Traits
{
	[Desc("Grants one foreign special-unit production permission to the owner of a captured secret lab.")]
	public sealed class SecretLabInfo : TraitInfo, ITechTreePrerequisiteInfo
	{
		IEnumerable<string> ITechTreePrerequisiteInfo.Prerequisites(ActorInfo info)
		{
			return SecretLab.UnitPool.Select(unit => "can-build-" + unit);
		}

		public override object Create(ActorInitializer init) { return new SecretLab(); }
	}

	public sealed class SecretLab : ITechTreePrerequisite, INotifyCreated, INotifyOwnerChanged, ISync, IGameSaveTraitData
	{
		internal static readonly string[] UnitPool = { "snipe", "terror", "deso", "yuri", "tnkd", "ttnk", "dtruck", "gtgcan" };
		static readonly HashSet<string> AlliedFactions = new() { "america", "germany", "england", "france", "korea" };
		static readonly HashSet<string> SovietFactions = new() { "cuba", "libya", "iraq", "russia" };
		static readonly HashSet<string> AlliedUnits = new() { "snipe", "tnkd", "gtgcan" };

		// The existing `yuri` actor is the Soviet Psi-Corps unit, not Yuri faction's `yurix`.
		[Sync]
		int selectedIndex = -1;

		public string UnlockedUnit => selectedIndex < 0 ? null : UnitPool[selectedIndex];

		public IEnumerable<string> ProvidesPrerequisites
		{
			get
			{
				if (selectedIndex >= 0)
					yield return "can-build-" + UnitPool[selectedIndex];
			}
		}

		public static string[] EligibleUnits(string faction)
		{
			if (AlliedFactions.Contains(faction))
				return UnitPool.Where(unit => !AlliedUnits.Contains(unit)).ToArray();
			if (SovietFactions.Contains(faction))
				return UnitPool.Where(AlliedUnits.Contains).ToArray();
			return faction == "yuri" ? (string[])UnitPool.Clone() : Array.Empty<string>();
		}

		public static string SelectUnit(string faction, MersenneTwister random)
		{
			var eligible = EligibleUnits(faction);
			return eligible.Length == 0 ? null : eligible[random.Next(eligible.Length)];
		}

		void SelectForOwner(Actor self)
		{
			var unit = self.Owner.NonCombatant ? null : SelectUnit(self.Owner.Faction.InternalName, self.World.SharedRandom);
			selectedIndex = unit == null ? -1 : Array.IndexOf(UnitPool, unit);
		}

		void INotifyCreated.Created(Actor self) { SelectForOwner(self); }

		void INotifyOwnerChanged.OnOwnerChanged(Actor self, Player oldOwner, Player newOwner)
		{
			// Actor.ChangeOwnerSync removes/re-adds the lab, updating both players' tech trees.
			SelectForOwner(self);
		}

		List<MiniYamlNode> IGameSaveTraitData.IssueTraitData(Actor self)
		{
			return new List<MiniYamlNode> { new("SelectedIndex", selectedIndex.ToStringInvariant()) };
		}

		void IGameSaveTraitData.ResolveTraitData(Actor self, MiniYaml data)
		{
			var node = data.NodeWithKeyOrDefault("SelectedIndex");
			if (node == null)
				return;
			var restored = Exts.ParseInt32Invariant(node.Value.Value);
			if (restored < -1 || restored >= UnitPool.Length)
				throw new YamlException("Invalid saved secret-lab unit index.");
			selectedIndex = restored;
			if (self.IsInWorld)
				self.Owner.PlayerActor.Trait<TechTree>().Update();
		}
	}
}
