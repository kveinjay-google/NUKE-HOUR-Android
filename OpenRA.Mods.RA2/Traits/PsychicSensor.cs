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

using System.Collections.Generic;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Traits;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.RA2.Traits
{
	[Desc("Reveals the current destinations and targets of nearby enemy units.")]
	public class PsychicSensorInfo : ConditionalTraitInfo
	{
		[Desc("Maximum horizontal distance at which enemy orders are revealed.")]
		public readonly WDist Range = WDist.FromCells(15);

		[Desc("Color used to draw revealed enemy orders.")]
		public readonly Color Color = Color.FromArgb(224, 196, 64, 255);

		[Desc("Width in pixels of revealed order lines.")]
		public readonly int LineWidth = 2;

		[Desc("Radius in pixels of revealed order markers.")]
		public readonly int MarkerWidth = 3;

		public override object Create(ActorInitializer init) { return new PsychicSensor(this); }
	}

	public class PsychicSensor : ConditionalTrait<PsychicSensorInfo>, IRenderAnnotations
	{
		public PsychicSensor(PsychicSensorInfo info)
			: base(info) { }

		IEnumerable<IRenderable> IRenderAnnotations.RenderAnnotations(Actor self, WorldRenderer wr)
		{
			if (IsTraitDisabled || !self.IsInWorld || self.IsDead ||
				!self.Owner.IsAlliedWith(self.World.RenderPlayer))
				yield break;

			foreach (var enemy in self.World.Actors)
			{
				if (!enemy.IsInWorld || enemy.IsDead || enemy.CurrentActivity == null ||
					enemy.Owner.RelationshipWith(self.Owner) != PlayerRelationship.Enemy ||
					(enemy.CenterPosition - self.CenterPosition).HorizontalLengthSquared > Info.Range.LengthSquared ||
					!IsPrimarySensorFor(self, enemy))
					continue;

				var previous = enemy.CenterPosition;
				var activity = enemy.CurrentActivity;
				for (; activity != null; activity = activity.NextActivity)
				{
					if (activity.IsCanceling)
						continue;

					foreach (var node in activity.TargetLineNodes(enemy))
					{
						if (node.Target.Type == TargetType.Invalid || node.Tile != null)
							continue;

						var target = node.Target.CenterPosition;
						yield return new TargetLineRenderable(
							new[] { previous, target }, Info.Color, Info.LineWidth, Info.MarkerWidth);
						previous = target;
					}
				}
			}
		}

		static bool IsPrimarySensorFor(Actor self, Actor enemy)
		{
			foreach (var pair in self.World.ActorsWithTrait<PsychicSensor>())
			{
				if (pair.Actor.ActorID >= self.ActorID || pair.Trait.IsTraitDisabled ||
					!pair.Actor.IsInWorld || pair.Actor.IsDead || pair.Actor.Owner != self.Owner)
					continue;

				if ((enemy.CenterPosition - pair.Actor.CenterPosition).HorizontalLengthSquared <= pair.Trait.Info.Range.LengthSquared)
					return false;
			}

			return true;
		}

		bool IRenderAnnotations.SpatiallyPartitionable => false;
	}
}
