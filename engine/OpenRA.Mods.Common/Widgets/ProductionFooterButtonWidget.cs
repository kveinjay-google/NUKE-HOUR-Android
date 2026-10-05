using OpenRA.Primitives;
using OpenRA.Graphics;
using OpenRA.Widgets;
using OpenRA.Mods.Common.Widgets.Logic;

namespace OpenRA.Mods.Common.Widgets
{
	// One continuous face occupies exactly the union of the original paging slots.
	public class ProductionFooterButtonWidget : ButtonWidget
	{
		public bool ExternalCancelBackground;
		public Rectangle? ExternalCancelLabelBounds;
		[ObjectCreator.UseCtor]
		public ProductionFooterButtonWidget(ModData modData) : base(modData) { }
		protected ProductionFooterButtonWidget(ProductionFooterButtonWidget other) : base(other) { }
		public override Widget Clone() => new ProductionFooterButtonWidget(this);
		public static Rectangle LabelFaceBounds(Rectangle rect, bool modern) => new(
			rect.X + rect.Width * 18 / 100, rect.Y + rect.Height * 8 / 100,
			rect.Width * 64 / 100, rect.Height * (modern ? 42 : 60) / 100);
		public static float2 CenterGlyphs(Rectangle face, Rectangle glyphs) => new(
			face.X + (face.Width - glyphs.Width) / 2f - glyphs.X,
			face.Y + (face.Height - glyphs.Height) / 2f - glyphs.Y);
		public static void DrawCancelSprite(Sprite sprite, Rectangle rect, bool pressed)
		{
			var brightness = pressed ? .68f : 1f;
			Game.Renderer.RgbaSpriteRenderer.DrawSprite(sprite,
				new float3(rect.Left, rect.Top, 0), new float3(rect.Right, rect.Top, 0),
				new float3(rect.Right, rect.Bottom, 0), new float3(rect.Left, rect.Bottom, 0),
				new float3(brightness, brightness, brightness), 1f);
		}
		public override void Draw()
		{
			if (!Platform.UsesMobileLayout || !Game.Settings.Game.IosProductionCancelButtons)
			{
				base.Draw();
				return;
			}

			var rect = RenderBounds;
			var disabled = IsDisabled();
			DrawBackground(rect, disabled, IsFooterPressActive, Ui.MouseOverWidget == this, IsHighlighted());
			rect = ExternalCancelLabelBounds ?? rect;
			rect = LabelFaceBounds(rect, ExternalCancelBackground);
			var text = GetText();
			if (string.IsNullOrEmpty(text)) return;
			var font = Game.Renderer.Fonts[Font];
			var size = font.MeasureGlyphBounds(text);
			// Reserve the curved end caps and keep the label clear of the outer trim.
			var width = rect.Width - 4;
			var height = rect.Height - 4;
			if (size.Width > width || size.Height > height)
			{
				SpriteFont fitting = null;
				foreach (var candidate in Game.Renderer.Fonts.Values)
				{
					var measured = candidate.MeasureGlyphBounds(text);
					if (measured.Width <= width && measured.Height <= height &&
						(fitting == null || measured.Height > fitting.MeasureGlyphBounds(text).Height))
						fitting = candidate;
				}
				if (fitting == null) return;
				font = fitting;
				size = font.MeasureGlyphBounds(text);
			}
			var position = CenterGlyphs(rect, size);
			font.DrawTextWithContrast(text, position, Color.Gold, Color.Black, 1);
		}
		public override void DrawBackground(Rectangle rect, bool disabled, bool pressed, bool hover, bool highlighted)
		{
			if (!Platform.UsesMobileLayout || !Game.Settings.Game.IosProductionCancelButtons)
			{
				base.DrawBackground(rect, disabled, pressed, hover, highlighted);
				return;
			}
			if (!ExternalCancelBackground)
			{
				var skin = TouchFactionSkin.Active == "allies" ? "allies" : "soviets";
				DrawCancelSprite(ChromeProvider.GetImage("production-cancel-classic-" + skin, "button"), rect, pressed);
			}
			if (pressed) ButtonActivationOverlayWidget.DrawLight(
				LabelFaceBounds(ExternalCancelLabelBounds ?? rect, ExternalCancelBackground), TouchFactionSkin.Active, "wide");
		}
	}
}
