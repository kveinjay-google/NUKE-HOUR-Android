#region Copyright & License Information
/*
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Mods.Common.Widgets.Logic;
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Mods.RA2.Widgets
{
	// Full-bleed background that stretches a single chrome sprite to the
	// widget bounds. BackgroundWidget's 9-slice panel path tiles/crops and
	// cannot fill ultrawide menus without seams or empty pad regions.
	public class StretchBackgroundWidget : Widget
	{
		public string Background = "";
		public string WideBackground = "";
		public string TabletBackground = "";
		public string TabletWideBackground = "";
		public string PhoneBackground = "";
		public bool TabletFillViewport = true;
		public float WideAspectThreshold = 1.75f;
		public bool ClickThrough = true;
		public bool PreserveAspectRatio = true;
		public bool CompactPhoneSurface;
		public bool OpaquePhoneSurface;
		public bool OpaqueSurface;
		public bool PhoneFullScreenFrame;
		public int PhoneFrameHeaderHeight = 60;
		public int PhoneFrameFooterHeight = 100;
		public bool PhoneFrameReserveBottomInset = true;
		Sprite cachedSource;
		Sprite cachedCrop;
		Size cachedTargetSize;

		public StretchBackgroundWidget() { }

		public override void Draw()
		{
			if (OpaqueSurface)
				WidgetUtils.FillRectWithColor(RenderBounds, Color.FromArgb(255, 12, 12, 12));
			if (CompactPhoneSurface && Platform.UsesMobileLayout)
			{
				var screen = IosScreenMetrics.SnapshotFor(Game.Renderer.Resolution);
				var phonePolicy = IosMenuLayoutPolicy.Create(true, screen);
				if (phonePolicy.IsPhone)
				{
					// Phone content fills the safe area with a two-point dark border.
					// Crop the decorative rails out of the shared material so the
					// background cannot put a second, wider frame beneath the controls.
					WidgetUtils.FillRectWithColor(new Rectangle(0, 0, screen.EffectiveSize.Width,
						screen.EffectiveSize.Height), Color.FromArgb(255, 12, 12, 12));
					var frame = ChromeProvider.TryGetPanelImages("cc-ingame-command-shell");
					if (frame != null && frame.Length > 4 && frame[4] != null)
					{
						var material = frame[4];
						var insetX = Math.Max(1, material.Bounds.Width / 20);
						var insetY = Math.Max(1, material.Bounds.Height / 20);
						var interior = new Rectangle(material.Bounds.X + insetX, material.Bounds.Y + insetY,
							material.Bounds.Width - 2 * insetX, material.Bounds.Height - 2 * insetY);
						var surface = MultiplayerScreenLayout.ContentBounds(screen, compactPhone: true).ToRectangle();
						var crop = CalculateAspectFillCrop(interior, surface.Size);
						var steel = new Sprite(material.Sheet, crop, material.ZRamp, material.Offset,
							material.Channel, material.BlendMode, material.Size.X / material.Bounds.Width);
						WidgetUtils.DrawSprite(steel, new float2(surface.X, surface.Y), surface.Size);
					}
					return;
				}
			}
			var bounds = RenderBounds;
			var preserveAspect = PreserveAspectRatio;
			var fullPhoneFrame = false;
			var bottomInset = 0;
			var background = !string.IsNullOrEmpty(WideBackground) && RenderBounds.Height > 0 &&
				(float)RenderBounds.Width / RenderBounds.Height >= WideAspectThreshold ? WideBackground : Background;
			if (!string.IsNullOrEmpty(TabletBackground) || !string.IsNullOrEmpty(PhoneBackground))
			{
				var viewport = Game.Renderer.Resolution;
				var layout = Platform.UsesMobileLayout ? IosSettingsLayout.ForSnapshot(true, IosScreenMetrics.SnapshotFor(viewport)) :
					IosSettingsLayout.ForPreview(Environment.GetEnvironmentVariable("NUKEHOUR_SETTINGS_PREVIEW"), viewport);
				if (layout.Enabled && !layout.IsPhone)
				{
					background = !string.IsNullOrEmpty(TabletWideBackground) &&
						(double)viewport.Width / viewport.Height >= 1.39 ? TabletWideBackground : TabletBackground;
					if (TabletFillViewport)
						bounds = new Rectangle(0, 0, viewport.Width, viewport.Height);
					preserveAspect = true;
				}
				else if (layout.Enabled && layout.IsPhone && !string.IsNullOrEmpty(PhoneBackground))
				{
					background = PhoneBackground;
					preserveAspect = true;
					if (PhoneFullScreenFrame)
					{
						var screen = IosScreenMetrics.SnapshotFor(viewport);
						bounds = new Rectangle(0, 0, viewport.Width, viewport.Height);
						bottomInset = Math.Max(0, viewport.Height - screen.SafeBounds.Bottom);
						fullPhoneFrame = true;
					}
				}
			}
			var sprites = ChromeProvider.TryGetPanelImages(background);
			var sprite = sprites != null && sprites.Length > 4 ? sprites[4] : null;
			if (sprite == null)
				return;

			if (fullPhoneFrame)
			{
				var slices = CalculatePhoneFrameSlices(sprite.Bounds, bounds, PhoneFrameReserveBottomInset ? bottomInset : 0, PhoneFrameHeaderHeight, PhoneFrameFooterHeight);
				for (var i = 0; i < slices.Length; i++)
				{
					var source = i == 4 ? CalculateAspectFillCrop(slices[i].Source, slices[i].Target.Size) : slices[i].Source;
					var part = new Sprite(sprite.Sheet, source, sprite.ZRamp, sprite.Offset,
						sprite.Channel, sprite.BlendMode, sprite.Size.X / sprite.Bounds.Width);
					WidgetUtils.DrawSprite(part, new float2(slices[i].Target.X, slices[i].Target.Y), slices[i].Target.Size);
				}

				return;
			}

			if (sprite != cachedSource || cachedTargetSize != bounds.Size)
			{
				var crop = preserveAspect ?
					CalculateAspectFillCrop(sprite.Bounds, bounds.Size) : sprite.Bounds;
				var scale = sprite.Size.X / sprite.Bounds.Width;
				cachedSource = sprite;
				cachedTargetSize = bounds.Size;
				cachedCrop = new Sprite(sprite.Sheet, crop, sprite.ZRamp, sprite.Offset,
					sprite.Channel, sprite.BlendMode, scale);
			}

			WidgetUtils.DrawSprite(cachedCrop, new float2(bounds.X, bounds.Y), bounds.Size);
		}

		// Preserve the outside rails and corners. Only the scenic center uses an
		// aspect-fill crop; the flexible steel footer absorbs the home-gesture inset.
		public static (Rectangle Source, Rectangle Target)[] CalculatePhoneFrameSlices(Rectangle source, Rectangle target, int bottomInset, int headerHeight = 60, int footerHeight = 100)
		{
			var scale = Math.Min((double)target.Width / source.Width, (double)target.Height / source.Height);
			var side = Math.Min(36, source.Width / 4);
			var top = Math.Min(headerHeight, source.Height / 4);
			var bottom = Math.Min(footerHeight, source.Height / 4);
			var targetSide = Math.Max(1, (int)Math.Round(side * scale));
			var targetTop = Math.Max(1, (int)Math.Round(top * scale));
			var targetBottom = Math.Min(target.Height - targetTop - 1, Math.Max(1, (int)Math.Round(bottom * scale)) + Math.Max(0, bottomInset));
			var sx = new[] { source.Left, source.Left + side, source.Right - side, source.Right };
			var sy = new[] { source.Top, source.Top + top, source.Bottom - bottom, source.Bottom };
			var tx = new[] { target.Left, target.Left + targetSide, target.Right - targetSide, target.Right };
			var ty = new[] { target.Top, target.Top + targetTop, target.Bottom - targetBottom, target.Bottom };
			var result = new (Rectangle Source, Rectangle Target)[9];
			for (var y = 0; y < 3; y++)
				for (var x = 0; x < 3; x++)
					result[y * 3 + x] = (new Rectangle(sx[x], sy[y], sx[x + 1] - sx[x], sy[y + 1] - sy[y]),
						new Rectangle(tx[x], ty[y], tx[x + 1] - tx[x], ty[y + 1] - ty[y]));
			return result;
		}

		public static Rectangle CalculateAspectFillCrop(Rectangle source, Size target)
		{
			if (source.Width <= 0 || source.Height <= 0 || target.Width <= 0 || target.Height <= 0)
				return source;

			var sourceAspect = (double)source.Width / source.Height;
			var targetAspect = (double)target.Width / target.Height;
			if (sourceAspect > targetAspect)
			{
				var width = Math.Clamp((int)Math.Round(source.Height * targetAspect), 1, source.Width);
				return new Rectangle(source.X + (source.Width - width) / 2, source.Y, width, source.Height);
			}

			var height = Math.Clamp((int)Math.Round(source.Width / targetAspect), 1, source.Height);
			return new Rectangle(source.X, source.Y + (source.Height - height) / 2, source.Width, height);
		}

		public override bool HandleMouseInput(MouseInput mi)
		{
			return !ClickThrough && EventBounds.Contains(mi.Location);
		}

		protected StretchBackgroundWidget(StretchBackgroundWidget other)
			: base(other)
		{
			Background = other.Background;
			WideBackground = other.WideBackground;
			TabletBackground = other.TabletBackground;
			TabletWideBackground = other.TabletWideBackground;
			PhoneBackground = other.PhoneBackground;
			TabletFillViewport = other.TabletFillViewport;
			WideAspectThreshold = other.WideAspectThreshold;
			ClickThrough = other.ClickThrough;
			PreserveAspectRatio = other.PreserveAspectRatio;
			CompactPhoneSurface = other.CompactPhoneSurface;
			OpaquePhoneSurface = other.OpaquePhoneSurface;
			OpaqueSurface = other.OpaqueSurface;
			PhoneFullScreenFrame = other.PhoneFullScreenFrame;
			PhoneFrameHeaderHeight = other.PhoneFrameHeaderHeight;
			PhoneFrameFooterHeight = other.PhoneFrameFooterHeight;
			PhoneFrameReserveBottomInset = other.PhoneFrameReserveBottomInset;
		}

		public override Widget Clone() { return new StretchBackgroundWidget(this); }
	}
}
