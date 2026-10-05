using System;
using System.Collections.Generic;
using System.Linq;
using OpenRA.Mods.Common.Widgets.Logic.Ingame;
using OpenRA.Primitives;
using OpenRA.Graphics;
using OpenRA.Widgets;

namespace OpenRA.Mods.Common.Widgets.Logic
{
	public sealed class ConnectionDialogLayout
	{
		public WidgetBounds Panel { get; }
		public WidgetBounds Title { get; }
		public WidgetBounds Details { get; }
		public WidgetBounds Actions { get; }
		public int Gap { get; }

		public ConnectionDialogLayout(IosScreenSnapshot screen, bool connecting = false, bool fullPage = false, WidgetBounds? artworkBounds = null)
		{
			if (fullPage)
			{
				var safe = screen.SafeBounds;
				var border = screen.IsCompactPhone ? screen.LogicalPoints(2) : 0;
				Panel = artworkBounds ?? new WidgetBounds(safe.X + border, safe.Y + border,
					Math.Max(1, safe.Width - 2 * border), Math.Max(1, safe.Height - 2 * border));
				Gap = screen.LogicalPoints(12);
				Title = new WidgetBounds(Panel.Width * 36 / 100, Panel.Height / 100, Panel.Width * 50 / 100, screen.LogicalPoints(40));
				Details = new WidgetBounds(Panel.Width * 54 / 100, Panel.Height * 24 / 100, Panel.Width * 39 / 100, Panel.Height * 40 / 100);
				Actions = new WidgetBounds(Panel.Width * 54 / 100, Panel.Height * 70 / 100, Panel.Width * 39 / 100, screen.LogicalPoints(56));
				return;
			}
			Panel = MultiplayerScreenLayout.ContentBounds(screen, 680, connecting ? 280 : 400);
			Gap = screen.LogicalPoints(12);
			var inset = screen.LogicalPoints(screen.IsCompactPhone ? 24 : 32);
			var width = Math.Max(1, Panel.Width - 2 * inset);
			Title = new WidgetBounds(inset, inset, width, screen.LogicalPoints(40));
			Actions = new WidgetBounds(inset, Panel.Height - inset - screen.LogicalPoints(56), width, screen.LogicalPoints(56));
			Details = new WidgetBounds(inset, Title.Bottom + Gap, width, Math.Max(1, Actions.Y - Title.Bottom - 2 * Gap));
		}
	}

	// Presentation only: existing connection logic owns state, password and callbacks.
	public sealed class ConnectionDialogLayoutLogic : ChromeLogic
	{
		readonly Widget root;
		readonly ScrollPanelWidget details;
		readonly Dictionary<LabelWidget, Func<string>> originalText = new();
		string previousState;

		[ObjectCreator.UseCtor]
		public ConnectionDialogLayoutLogic(Widget widget)
		{
			root = widget;
			details = root.Get<ScrollPanelWidget>("CONNECTION_DETAILS");
		}

		public override void Tick()
		{
			// Capture after all sibling constructors have installed live text bindings.
			if (originalText.Count == 0)
				foreach (var label in details.Children.OfType<LabelWidget>())
					originalText.Add(label, label.GetText);

			var screen = IosScreenMetrics.SnapshotFor(Game.Renderer.Resolution);
			var actions = root.Get("CONNECTION_ACTIONS");
			var buttons = actions.Children.OfType<ButtonWidget>().Where(b => b.IsVisible()).ToArray();
			var state = screen.EffectiveSize + "/" + screen.NativePointSize + "/" + screen.SafeBounds + "/" +
				string.Join("|", originalText.Select(p => p.Key.IsVisible() + ":" + p.Value())) + "/" +
				string.Join("|", buttons.Select(b => b.Id));
			if (state == previousState)
				return;
			previousState = state;

			var fullPage = root.GetOrNull("FULL_ART") != null;
			WidgetBounds? artworkBounds = null;
			if (fullPage && Platform.UsesMobileLayout && !screen.IsCompactPhone)
			{
				var wide = (double)screen.EffectiveSize.Width / screen.EffectiveSize.Height >= 1.39;
				var images = ChromeProvider.TryGetPanelImages(wide ? "cc-ranked-tablet-wide-match" : "cc-ranked-tablet-match");
				if (images != null && images.Length > 4 && images[4] != null)
				{
					var fit = ButtonWidget.AspectFitBounds(new Rectangle(0, 0, screen.EffectiveSize.Width, screen.EffectiveSize.Height), images[4].Bounds.Size);
					artworkBounds = new WidgetBounds(fit.X, fit.Y, fit.Width, fit.Height);
				}
			}
			var layout = new ConnectionDialogLayout(screen, root.Id == "CONNECTING_PANEL", fullPage, artworkBounds);
			if (fullPage)
			{
				var phone = IosMenuLayoutPolicy.Create(Platform.UsesMobileLayout, screen).IsPhone;
				var art = root.Get("FULL_ART");
				art.Bounds = new WidgetBounds(0, 0, layout.Panel.Width, layout.Panel.Height);
				art.IsVisible = () => true;
				var scene = root.Get("SCENE");
				scene.Bounds = new WidgetBounds(layout.Panel.Width * 3 / 100, layout.Panel.Height * 20 / 100,
					layout.Panel.Width * 44 / 100, layout.Panel.Height * 58 / 100);
				scene.IsVisible = () => false;
			}
			root.Bounds = layout.Panel;
			(root.GetOrNull("CONNECTING_TITLE") ?? root.Get("TITLE")).Bounds = layout.Title;
			details.Bounds = layout.Details;
			actions.Bounds = layout.Actions;
			details.ItemSpacing = layout.Gap;
			details.TopBottomSpacing = 0;
			details.CollapseHiddenChildren = true;
			details.ScrollbarWidth = screen.LogicalPoints(48);
			var width = Math.Max(1, layout.Details.Width - details.ScrollbarWidth - layout.Gap);
			foreach (var child in details.Children)
			{
				child.Bounds.X = 0;
				child.Bounds.Width = width;
				if (child is LabelWidget label)
				{
					var font = Game.Renderer.Fonts[label.Font];
					var wrapped = IosSupportPowerTooltipPolicy.FitText(originalText[label](), width, int.MaxValue,
						text => new Size(font.Measure(text).X, font.Measure(text).Y));
					label.GetText = () => wrapped;
					label.WordWrap = false;
					label.VAlign = TextVAlign.Top;
					label.Bounds.Height = Math.Max(screen.LogicalPoints(28), font.Measure(wrapped).Y + screen.LogicalPoints(6));
				}
				else if (child is TextFieldWidget)
					child.Bounds.Height = screen.LogicalPoints(48);
			}

			details.Layout.AdjustChildren();
			details.ScrollBar = details.ContentHeight > details.Bounds.Height ? ScrollBar.Right : ScrollBar.Hidden;
			var buttonWidth = buttons.Length == 0 ? 0 : (layout.Actions.Width - layout.Gap * (buttons.Length - 1)) / buttons.Length;
			for (var i = 0; i < buttons.Length; i++)
				buttons[i].Bounds = new WidgetBounds(i * (buttonWidth + layout.Gap), 0, buttonWidth, layout.Actions.Height);
		}
	}
}
