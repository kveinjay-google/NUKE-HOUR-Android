#region Copyright & License Information
/*
 * This file is part of OpenRA, which is free software. It is made
 * available under the terms of the GNU General Public License as published
 * by the Free Software Foundation, either version 3 of the License, or (at
 * your option) any later version. For more information, see COPYING.
 */
#endregion

using System;
using System.Collections.Generic;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Mods.RA2.Widgets.Logic
{
	/// <summary>Uniformly enlarges RA2's complete sidebar artwork for Android phones.</summary>
	public sealed class AndroidMobileIngameLayoutLogic : ChromeLogic
	{
		readonly Widget widget;
		readonly Dictionary<Widget, WidgetBounds> originalBounds = new();
		Size lastResolution;

		[ObjectCreator.UseCtor]
		public AndroidMobileIngameLayoutLogic(Widget widget)
		{
			this.widget = widget;
		}

		public override void Tick()
		{
			ApplyIfNeeded();
		}

		void ApplyIfNeeded()
		{
			if (!Platform.IsAndroid || Game.Renderer == null || Game.Renderer.Resolution == lastResolution)
				return;

			var top = widget.GetOrNull("SIDEBAR_BACKGROUND_TOP");
			var sidebar = widget.GetOrNull<ContainerWidget>("SIDEBAR_PRODUCTION");
			var palette = widget.GetOrNull<ProductionPaletteWidget>("PRODUCTION_PALETTE");
			if (top == null || sidebar == null || palette == null)
				return;

			CaptureOriginalTree(top);
			CaptureOriginalTree(sidebar);

			var layout = AndroidIngameMobileLayout.Create(Game.Renderer.Resolution);
			top.Bounds = ToWidgetBounds(layout.TopBarBounds);
			if (top is ImageWidget topImage)
				topImage.StretchToFit = true;
			ScaleTree(top, layout.Scale);

			sidebar.Bounds = ToWidgetBounds(layout.SidebarBounds);
			ScaleTree(sidebar, layout.Scale);
			palette.Bounds = ToWidgetBounds(layout.PaletteBounds);
			palette.ApplyMobileLayout(layout.IconSize, layout.IconMargin, layout.Rows);

			lastResolution = Game.Renderer.Resolution;
		}

		void CaptureOriginalTree(Widget root)
		{
			foreach (var child in root.Children)
			{
				if (IsDynamicPaletteChrome(child))
					continue;

				if (!originalBounds.ContainsKey(child))
					originalBounds.Add(child, child.Bounds);
				CaptureOriginalTree(child);
			}
		}

		void ScaleTree(Widget root, double scale)
		{
			foreach (var child in root.Children)
			{
				if (IsDynamicPaletteChrome(child))
					continue;

				if (!originalBounds.TryGetValue(child, out var original))
					continue;

				child.Bounds = Scale(original, scale);
				if (child is ButtonWidget button)
				{
					button.StretchBackgroundToFit = true;
					if (button.Font == "TinyBold" || button.Font == "Bold")
						button.Font = "MediumBold";
				}
				if (child is LabelWidget label && (label.Font == "TinyBold" || label.Font == "Bold"))
					label.Font = "MediumBold";
				if (child is ImageWidget image)
					ScaleImage(image, original, scale);

				ScaleTree(child, scale);
			}
		}

		static bool IsDynamicPaletteChrome(Widget widget)
		{
			return widget.Id == "PALETTE_BACKGROUND" || widget.Id == "PALETTE_FOREGROUND";
		}

		static void ScaleImage(ImageWidget image, WidgetBounds original, double scale)
		{
			if (original.Width > 0 && original.Height > 0)
			{
				image.StretchToFit = true;
				return;
			}

			if (string.IsNullOrEmpty(image.GetImageCollection()) || string.IsNullOrEmpty(image.GetImageName()))
				return;

			var sprite = image.GetSprite();
			if (sprite == null)
				return;

			image.Bounds.Width = Math.Max(1, (int)Math.Round(sprite.Size.X * scale));
			image.Bounds.Height = Math.Max(1, (int)Math.Round(sprite.Size.Y * scale));
			image.StretchToFit = true;
		}

		static WidgetBounds Scale(WidgetBounds bounds, double scale)
		{
			return new WidgetBounds(
				(int)Math.Round(bounds.X * scale),
				(int)Math.Round(bounds.Y * scale),
				(int)Math.Round(bounds.Width * scale),
				(int)Math.Round(bounds.Height * scale));
		}

		static WidgetBounds ToWidgetBounds(Rectangle bounds)
		{
			return new WidgetBounds(bounds.X, bounds.Y, bounds.Width, bounds.Height);
		}
	}
}
