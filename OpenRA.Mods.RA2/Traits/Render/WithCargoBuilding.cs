#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software. It is
 * made available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using OpenRA.Graphics;
using OpenRA.Mods.Common;
using OpenRA.Mods.Common.Graphics;
using OpenRA.Mods.Common.Traits;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.RA2.Traits
{
	[Desc("Renders cargo at a fixed facing on a building, including live turret facing.")]
	public class WithCargoBuildingInfo : TraitInfo, Requires<CargoInfo>, Requires<BodyOrientationInfo>
	{
		[Desc("Cargo positions relative to the building body in (forward, right, up) triples.")]
		public readonly WVec[] LocalOffset = { WVec.Zero };

		[Desc("Facing used by the displayed passenger body.")]
		public readonly WAngle Facing = WAngle.Zero;

		[Desc("Passenger CargoTypes to display.")]
		public readonly HashSet<string> DisplayTypes = new();

		[Desc("Sounds played when a displayed passenger enters.")]
		public readonly string[] EnterSounds = Array.Empty<string>();

		[Desc("Sounds played when a displayed passenger exits.")]
		public readonly string[] ExitSounds = Array.Empty<string>();

		[Desc("Whether enter and exit sounds play through shroud and fog.")]
		public readonly bool AudibleThroughFog = false;

		[Desc("Volume used for enter and exit sounds.")]
		public readonly float SoundVolume = 1;

		public override object Create(ActorInitializer init) { return new WithCargoBuilding(init.Self, this); }
	}

	public class WithCargoBuilding : ITick, IRender, INotifyPassengerEntered, INotifyPassengerExited
	{
		readonly WithCargoBuildingInfo info;
		readonly Cargo cargo;
		readonly BodyOrientation body;
		readonly Dictionary<Actor, IActorPreview[]> previews = new();

		public WithCargoBuilding(Actor self, WithCargoBuildingInfo info)
		{
			this.info = info;
			cargo = self.Trait<Cargo>();
			body = self.Trait<BodyOrientation>();
		}

		void ITick.Tick(Actor self)
		{
			foreach (var actorPreviews in previews.Values)
				if (actorPreviews != null)
					foreach (var preview in actorPreviews)
						preview.Tick();
		}

		IEnumerable<IRenderable> IRender.Render(Actor self, WorldRenderer wr)
		{
			var bodyOrientation = body.QuantizeOrientation(self.Orientation);
			var pos = self.CenterPosition;
			var missing = previews.Where(kv => kv.Value == null).Select(kv => kv.Key).ToList();

			foreach (var passenger in missing)
			{
				var passengerInits = new TypeDictionary
				{
					new OwnerInit(passenger.Owner),
					new FacingInit(info.Facing),
				};

				foreach (var modifier in passenger.TraitsImplementing<IActorPreviewInitModifier>())
					modifier.ModifyActorPreviewInit(passenger, passengerInits);

				var init = new ActorPreviewInitializer(passenger.Info, wr, passengerInits);
				previews[passenger] = passenger.Info.TraitInfos<IRenderActorPreviewInfo>()
					.SelectMany(renderer => renderer.RenderPreview(init))
					.ToArray();
			}

			var i = 0;
			foreach (var actorPreviews in previews.Values)
			{
				if (actorPreviews == null)
					continue;

				var index = cargo.PassengerCount > 1 ? i++ % info.LocalOffset.Length : info.LocalOffset.Length / 2;
				var localOffset = info.LocalOffset[index];
				var renderPosition = pos + body.LocalToWorld(localOffset.Rotate(bodyOrientation));
				foreach (var preview in actorPreviews)
					foreach (var renderable in preview.Render(wr, renderPosition))
						yield return renderable.WithZOffset(1);
			}
		}

		IEnumerable<Rectangle> IRender.ScreenBounds(Actor self, WorldRenderer wr)
		{
			var bodyOrientation = body.QuantizeOrientation(self.Orientation);
			var i = 0;
			foreach (var actorPreviews in previews.Values)
			{
				if (actorPreviews == null)
					continue;

				var index = cargo.PassengerCount > 1 ? i++ % info.LocalOffset.Length : info.LocalOffset.Length / 2;
				var localOffset = info.LocalOffset[index];
				var renderPosition = self.CenterPosition + body.LocalToWorld(localOffset.Rotate(bodyOrientation));
				foreach (var preview in actorPreviews)
					foreach (var bounds in preview.ScreenBounds(wr, renderPosition))
						yield return bounds;
			}
		}

		void INotifyPassengerEntered.OnPassengerEntered(Actor self, Actor passenger)
		{
			if (!info.DisplayTypes.Contains(passenger.Trait<Passenger>().Info.CargoType))
				return;

			var facing = passenger.TraitOrDefault<IFacing>();
			if (facing != null)
				facing.Facing = info.Facing;

			previews[passenger] = null;
			self.World.ScreenMap.AddOrUpdate(self);
			PlaySound(self, info.EnterSounds);
		}

		void INotifyPassengerExited.OnPassengerExited(Actor self, Actor passenger)
		{
			if (!previews.Remove(passenger))
				return;

			self.World.ScreenMap.AddOrUpdate(self);
			PlaySound(self, info.ExitSounds);
		}

		void PlaySound(Actor self, string[] sounds)
		{
			if (sounds.Length == 0)
				return;

			var pos = self.CenterPosition;
			if (info.AudibleThroughFog || (!self.World.ShroudObscures(pos) && !self.World.FogObscures(pos)))
				Game.Sound.Play(SoundType.World, sounds, self.World, pos, null, info.SoundVolume);
		}
	}
}
