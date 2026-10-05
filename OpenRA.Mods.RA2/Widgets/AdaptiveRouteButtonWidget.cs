#region Copyright & License Information
/*
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using OpenRA.Graphics;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Mods.Common.Widgets.Logic;
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Mods.RA2.Widgets
{
	/// <summary>
	/// Selects the Scheme C route artwork authored for the active screen shape.
	/// This keeps the runtime hover plate pixel-aligned with its generated shell.
	/// </summary>
	public sealed class AdaptiveRouteButtonWidget : ButtonWidget
	{
		const double RouteTopExpansion = .38;
		const double RouteBottomExpansion = .18;
		public int ArtworkRow = 0;

		[ObjectCreator.UseCtor]
		public AdaptiveRouteButtonWidget(ModData modData)
			: base(modData) { }

		AdaptiveRouteButtonWidget(AdaptiveRouteButtonWidget other)
			: base(other) { ArtworkRow = other.ArtworkRow; }

		public override void DrawBackground(
			Rectangle rect, bool disabled, bool pressed, bool hover, bool highlighted)
		{
			// The final shell already owns the complete metal route plate. Normal
			// state must therefore draw nothing; generated selected-material deltas
			// are composited for hover/press/disabled feedback.
			if (!disabled && !pressed && !hover && !highlighted)
				return;

			var resolution = Game.Renderer.Resolution;
			var suffix = IosMainMenuLayout.ProfileFor(resolution.Width, resolution.Height) switch
			{
				IosMainMenuShellProfile.Tablet => "-tablet",
				IosMainMenuShellProfile.Ultrawide => "-ultrawide",
				_ => "-standard",
			};

			var imageName = WidgetUtils.GetStatefulImageName(
				Background + suffix + (ArtworkRow == 0 ? "" : "-row" + ArtworkRow), disabled, pressed, hover || highlighted);
			var sprites = ChromeProvider.TryGetPanelImages(imageName);
			var sprite = sprites != null && sprites.Length > 4 ? sprites[4] : null;
			if (sprite == null)
				return;

			// This is a single 1024x384 crop of the shell's light sources. The panel
			// renderer tiles/crops its center at native pixel size, displacing the
			// lamp and clipping the rim. Map the complete crop back to the plate.
			var phone = Platform.UsesMobileLayout && IosMenuLayoutPolicy.Create(true,
				IosScreenMetrics.SnapshotFor(resolution)).IsPhone;
			var artworkRect = CalculateArtworkBounds(rect, resolution, phone, ArtworkRow);
			var expandedRect = CalculateIlluminationBounds(artworkRect);
			WidgetUtils.DrawSprite(sprite, new float2(expandedRect.X, expandedRect.Y), expandedRect.Size);
		}

		public static Rectangle CalculateArtworkBounds(Rectangle hitBounds, Size viewport, bool phone, int row)
		{
			if (!phone)
				return hitBounds;

			// The full-bleed shell never moves with the notch. Use its authored
			// viewport layout for paint, while retaining safe-area bounds for input.
			var artwork = IosMainMenuLayout.Create(IosMenuLayoutPolicy.Create(false, viewport.Width, viewport.Height));
			return artwork.Controls[System.Math.Clamp(row, 0, 2)].ToRectangle();
		}

		public static Rectangle CalculateIlluminationBounds(Rectangle rect)
		{
			var topExpansion = (int)System.Math.Round(rect.Height * RouteTopExpansion);
			var bottomExpansion = (int)System.Math.Round(rect.Height * RouteBottomExpansion);
			return new Rectangle(rect.X, rect.Y - topExpansion, rect.Width, rect.Height + topExpansion + bottomExpansion);
		}

		public override Widget Clone() { return new AdaptiveRouteButtonWidget(this); }
	}
}
