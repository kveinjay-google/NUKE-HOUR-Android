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
using System.Text;
using OpenRA.Primitives;

namespace OpenRA.Mods.Common.Widgets
{
	public static class ProductionCameoScalePolicy
	{
		public static float Resolve(float spriteWidth, float spriteHeight, float cellWidth, float cellHeight)
		{
			if (spriteWidth <= 0 || spriteHeight <= 0)
				return 1f;

			return Math.Min(1f, Math.Min(cellWidth / spriteWidth, cellHeight / spriteHeight));
		}
	}

	public static class ProductionIconPresentation
	{
		public static string ActorImage(string actorName)
		{
			return $"hd-production-{actorName.ToLowerInvariant()}";
		}

		public static string LargeActorImage(string actorName)
		{
			return $"large-production-{actorName.ToLowerInvariant()}";
		}

		public static string WideActorImage(string actorName)
		{
			return $"wide-production-{actorName.ToLowerInvariant()}";
		}

		public static string SupportImage(string ownerActor, string traitKey)
		{
			return $"hd-support-{Normalize(ownerActor)}-{Normalize(traitKey)}";
		}

		public static bool Has(World world, string image)
		{
			return world.Map.Sequences.HasImage(image) && world.Map.Sequences.HasSequence(image, "icon");
		}

		public static bool IsHighDefinitionEnabled(GameSettings settings)
		{
			return settings == null || settings.EffectiveInterfaceStyle != InterfaceStyleMode.Classic;
		}

		public static bool ShouldUseHighDefinition(GameSettings settings, World world, string image)
		{
			return IsHighDefinitionEnabled(settings) && Has(world, image);
		}

		public static float Scale(Size source, Size destination, bool allowUpscaling = false)
		{
			if (source.Width <= 0 || source.Height <= 0)
				return 1f;

			var scale = Math.Min(
				(float)destination.Width / source.Width,
				(float)destination.Height / source.Height);
			return allowUpscaling ? scale : Math.Min(1f, scale);
		}

		// Legacy mask sprites must cover the rendered cameo even in multi-column HD mode.
		// The caller clips this cover transform to that same rendered cameo rectangle.
		public static float MaskCoverScale(Size cameo, Size cell, Size mask, bool allowUpscaling)
		{
			if (mask.Width <= 0 || mask.Height <= 0) return 1;
			var scale = Scale(cameo, cell, allowUpscaling);
			return Math.Max(cameo.Width * scale / mask.Width, cameo.Height * scale / mask.Height);
		}

		static string Normalize(string value)
		{
			var result = new StringBuilder(value.Length);
			var separator = false;
			foreach (var character in value)
			{
				if (char.IsLetterOrDigit(character))
				{
					result.Append(char.ToLowerInvariant(character));
					separator = false;
				}
				else if (!separator && result.Length > 0)
				{
					result.Append('-');
					separator = true;
				}
			}

			if (result.Length > 0 && result[^1] == '-')
				result.Length--;

			return result.ToString();
		}
	}
}
