using System;
using System.Linq;
using OpenRA.Mods.Common.Widgets.Logic.Ingame;
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Mods.Common.Widgets.Logic
{
	public sealed class AboutPanelLayout
	{
		public WidgetBounds Title { get; }
		public WidgetBounds Scroll { get; }
		public WidgetBounds Website { get; }
		public WidgetBounds Back { get; }
		readonly IosScreenSnapshot screen;
		readonly int gap;

		public AboutPanelLayout(IosScreenSnapshot screen)
		{
			this.screen = screen;
			var content = MultiplayerScreenLayout.ContentBounds(screen, maximumWidth: 1120);
			// The system safe area only protects against notches and the home
			// indicator. Keep the fixed actions above the painted metal frame too.
			var frameClearance = screen.LogicalPoints(screen.IsCompactPhone ? 32 : 24);
			content.Height = Math.Max(1, content.Height - frameClearance);
			gap = screen.LogicalPoints(screen.IsCompactPhone ? 12 : 20);
			var titleHeight = screen.LogicalPoints(56);
			var actionHeight = screen.LogicalPoints(56);
			Title = new WidgetBounds(content.X, content.Y, content.Width, titleHeight);
			var buttonWidth = (content.Width - gap) / 2;
			Website = new WidgetBounds(content.X, content.Bottom - actionHeight, buttonWidth, actionHeight);
			Back = new WidgetBounds(Website.Right + gap, Website.Y, content.Right - Website.Right - gap, actionHeight);
			Scroll = new WidgetBounds(content.X, Title.Bottom + gap, content.Width, Math.Max(1, Website.Y - Title.Bottom - 2 * gap));
		}

		public void Apply(Widget root)
		{
			root.Bounds = new WidgetBounds(0, 0, screen.EffectiveSize.Width, screen.EffectiveSize.Height);
			root.Get("ABOUT_TITLE").Bounds = Title;
			root.Get("WEBSITE_BUTTON").Bounds = Website;
			root.Get("BACK_BUTTON").Bounds = Back;
			var scroll = root.Get<ScrollPanelWidget>("ABOUT_SCROLL");
			scroll.Bounds = Scroll;
			scroll.ScrollbarWidth = screen.LogicalPoints(48);
			scroll.EnableContentDragging = true;
			scroll.ItemSpacing = gap;
			scroll.TopBottomSpacing = gap;
			foreach (var label in scroll.Children.OfType<LabelWidget>())
			{
				label.Bounds.X = gap;
				label.Bounds.Width = Math.Max(1, Scroll.Width - scroll.ScrollbarWidth - 2 * gap);
				var font = Game.Renderer.Fonts[label.Font];
				// Resolve from the localization key on every resize, never from
				// previously wrapped text. Keep VersionLabelLogic's live binding.
				if (!string.IsNullOrEmpty(label.Text))
				{
					var wrapped = IosSupportPowerTooltipPolicy.FitText(FluentProvider.GetMessage(label.Text),
						label.Bounds.Width, int.MaxValue, text => new Size(font.Measure(text).X, font.Measure(text).Y));
					label.GetText = () => wrapped;
					label.WordWrap = false;
				}
				var displayed = label.WordWrap ? WidgetUtils.WrapText(label.GetText(), label.Bounds.Width, font) : label.GetText();
				label.Bounds.Height = Math.Max(screen.LogicalPoints(28), font.Measure(displayed).Y + screen.LogicalPoints(6));
				label.VAlign = TextVAlign.Top;
			}
			scroll.Layout.AdjustChildren();
		}
	}
}
