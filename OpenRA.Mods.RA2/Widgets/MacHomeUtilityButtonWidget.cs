using System;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Mods.Common.Widgets.Logic;
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Mods.RA2.Widgets
{
	// Feedback only: retain button geometry, localized children and click handlers.
	public sealed class MacHomeUtilityButtonWidget : ButtonWidget
	{
		[ObjectCreator.UseCtor]
		public MacHomeUtilityButtonWidget(ModData modData) : base(modData) { }
		MacHomeUtilityButtonWidget(MacHomeUtilityButtonWidget other) : base(other) { }

		public override void DrawBackground(Rectangle rect, bool disabled, bool pressed, bool hover, bool highlighted)
		{
			// The mobile shell already contains the four complete metal plates.
			if (!Platform.UsesMobileLayout)
				base.DrawBackground(rect, disabled, pressed, hover, highlighted);
			if ((Platform.CurrentPlatform != PlatformType.OSX && !Platform.UsesMobileLayout) ||
				disabled || (!hover && !pressed && !highlighted))
				return;

			// Reuse the generated material's emissive rim, never its red enamel center.
			// The hit target includes the outer chassis; illumination belongs to its inset face.
			var policy = IosMenuLayoutPolicy.Create(Platform.UsesMobileLayout,
				IosScreenMetrics.SnapshotFor(Game.Renderer.Resolution));
			rect = CalculateArtworkBounds(rect, policy, Id);
			var insetX = Math.Max(1, rect.Width * 3 / 100);
			var insetY = Math.Max(1, rect.Height * 8 / 100);
			var face = new Rectangle(rect.X + insetX, rect.Y + insetY,
				Math.Max(0, rect.Width - insetX), Math.Max(0, rect.Height - 2 * insetY));
			WidgetUtils.DrawPanel("cc-soviet-utility-light", face);
		}

		public static Rectangle CalculateArtworkBounds(Rectangle hitBounds, IosMenuLayoutPolicy policy, string id)
		{
			if (!policy.IsPhone)
				return hitBounds;

			var slot = id switch
			{
				"SETTINGS_BUTTON" => 0,
				"THANKS_BUTTON" => 1,
				"ABOUT_BUTTON" => 2,
				"QUIT_BUTTON" => 3,
				_ => -1
			};
			if (slot < 0)
				return hitBounds;

			// Match the shell's fixed slots before safe-area clipping of input.
			var viewport = policy.ViewportBounds;
			var left = (int)Math.Round(viewport.Width * .025);
			var right = (int)Math.Round(viewport.Width * .976);
			var gap = Math.Max(2, policy.Gap / 3);
			var width = Math.Max(policy.MinimumTarget, (right - left - 3 * gap) / 4);
			var height = Math.Max(policy.MinimumTarget, (int)Math.Round(viewport.Height * .095));
			var y = Math.Min((int)Math.Round(viewport.Height * .846), viewport.Height - height);
			return new Rectangle(left + slot * (width + gap), y, width, height);
		}

		public override Widget Clone() => new MacHomeUtilityButtonWidget(this);
	}
}
