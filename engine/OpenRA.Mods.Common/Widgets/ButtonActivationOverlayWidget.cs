using System;
using OpenRA.Graphics;
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Mods.Common.Widgets
{
	// Lighting is separate from the original artwork: activation never changes a symbol.
	public sealed class ButtonActivationOverlayWidget : Widget
	{
		readonly Widget visual;
		readonly Func<string> faction;
		readonly string shape;

		public ButtonActivationOverlayWidget(ButtonWidget button, Widget visual, Func<string> faction, string shape = "round")
		{
			this.visual = visual;
			this.faction = faction;
			this.shape = shape;
			IgnoreMouseOver = true;
			IsVisible = () => !button.IsDisabled() && (button.IsHighlighted() || button.IsVisuallyPressed);
		}

		public override void Draw() => DrawLight(visual.RenderBounds, faction(), shape);

		public static void DrawLight(Rectangle rect, string faction, string shape = "round")
		{
			if (rect.Width <= 0 || rect.Height <= 0) return;
			var light = ChromeProvider.GetImage("button-activation-light-" + faction, shape);
			DrawLightSprite(light, rect, .38f);
		}

		public static void DrawLightSprite(Sprite light, Rectangle rect, float opacity)
		{
			Game.Renderer.RgbaSpriteRenderer.DrawSprite(light,
				new float3(rect.Left, rect.Top, 0), new float3(rect.Right, rect.Top, 0),
				new float3(rect.Right, rect.Bottom, 0), new float3(rect.Left, rect.Bottom, 0),
				new float3(1, 1, 1), opacity);
		}
	}
}
