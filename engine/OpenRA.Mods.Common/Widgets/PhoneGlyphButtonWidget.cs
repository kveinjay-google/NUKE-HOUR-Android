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
using OpenRA.Graphics;
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Mods.Common.Widgets
{
	/// <summary>
	/// Frameless colored-glyph button for the phone music player: it draws a
	/// thick vector shape (back/play/pause/stop/next triangles and bars) with no
	/// chrome/box, keeps a full-cell touch target and activates on release
	/// inside its bounds. The visible glyph is dynamic through <see cref="GetKind"/>.
	/// </summary>
	public sealed class PhoneGlyphButtonWidget : Widget
	{
		public enum Kind
		{
			Back,
			Play,
			Pause,
			Stop,
			Next
		}

		public Action OnActivate = () => { };
		public Func<Kind> GetKind = () => Kind.Play;

		bool pressed;

		[ObjectCreator.UseCtor]
		public PhoneGlyphButtonWidget(ModData modData)
		{
			Visible = true;
		}

		public override Widget Clone()
		{
			throw new InvalidOperationException("Phone glyph buttons are not cloneable");
		}

		public override bool HandleMouseInput(MouseInput mi)
		{
			if (mi.Event == MouseInputEvent.Down && mi.Button == MouseButton.Left)
			{
				pressed = true;
				return TakeMouseFocus(mi);
			}

			if (mi.Event == MouseInputEvent.Up && HasMouseFocus)
			{
				pressed = false;
				var activate = RenderBounds.Contains(mi.Location);
				YieldMouseFocus(mi);
				if (activate)
					OnActivate();
				return true;
			}

			if (mi.Event == MouseInputEvent.Move && HasMouseFocus)
			{
				pressed = RenderBounds.Contains(mi.Location);
				return true;
			}

			return false;
		}

		public override void Draw()
		{
			var kind = GetKind?.Invoke() ?? Kind.Play;
			var centerX = RenderOrigin.X + Bounds.Width / 2f;
			var centerY = RenderOrigin.Y + Bounds.Height / 2f;
			var radius = Math.Min(Bounds.Width, Bounds.Height) * 0.3f;
			var stroke = Math.Max(3f, radius * 0.32f);

			Color color;
			switch (kind)
			{
				case Kind.Back:
				case Kind.Next:
					color = Color.FromArgb(255, 80, 210, 255);
					break;
				case Kind.Play:
				case Kind.Pause:
					color = Color.FromArgb(255, 90, 235, 130);
					break;
				default:
					color = Color.FromArgb(255, 255, 190, 70);
					break;
			}

			if (pressed)
				color = Color.FromArgb(255, 255, 255, 255);

			switch (kind)
			{
				case Kind.Back:
					DrawTriangle(centerX - radius * 0.55f, centerY, radius, -1, stroke, color);
					DrawTriangle(centerX + radius * 0.25f, centerY, radius, -1, stroke, color);
					break;
				case Kind.Play:
					DrawTriangle(centerX + radius * 0.15f, centerY, radius, 1, stroke, color);
					break;
				case Kind.Pause:
					DrawBar(centerX - radius * 0.45f, centerY, radius, stroke, color);
					DrawBar(centerX + radius * 0.45f, centerY, radius, stroke, color);
					break;
				case Kind.Stop:
					DrawFilledRect(centerX - radius * 0.55f, centerY - radius * 0.55f,
						centerX + radius * 0.55f, centerY + radius * 0.55f, color);
					break;
				case Kind.Next:
					DrawTriangle(centerX - radius * 0.45f, centerY, radius, 1, stroke, color);
					DrawTriangle(centerX + radius * 0.35f, centerY, radius, 1, stroke, color);
					break;
			}
		}

		void DrawTriangle(float cx, float cy, float r, float dir, float stroke, Color color)
		{
			var h = r * 0.95f;
			var w = r * 0.8f;
			var pts = new[]
			{
				new float3(cx + dir * w * 0.45f, cy, 0),
				new float3(cx - dir * w * 0.45f, cy - h, 0),
				new float3(cx - dir * w * 0.45f, cy + h, 0)
			};

			Game.Renderer.RgbaColorRenderer.DrawPolygon(pts, stroke, color);
		}

		void DrawBar(float cx, float cy, float r, float stroke, Color color)
		{
			var w = r * 0.26f;
			var h = r * 1.2f;
			Game.Renderer.RgbaColorRenderer.FillRect(
				new float3(cx - w / 2, cy - h / 2, 0),
				new float3(cx + w / 2, cy + h / 2, 0),
				color);
		}

		void DrawFilledRect(float x0, float y0, float x1, float y1, Color color)
		{
			Game.Renderer.RgbaColorRenderer.FillRect(
				new float3(x0, y0, 0), new float3(x1, y1, 0), color);
		}
	}
}
