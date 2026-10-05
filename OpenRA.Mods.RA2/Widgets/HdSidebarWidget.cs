using System;
using System.Collections.Generic;
using System.Linq;
using OpenRA.Graphics;
using OpenRA.Mods.RA2.Widgets.Logic;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Mods.Common.Widgets.Logic;
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Mods.RA2.Widgets
{
	// The classic control strip is one coherent raster. Invisible widgets provide input.
	public sealed class HdSidebarWidget : ImageWidget
	{
		enum SidebarSkinMode { None, Classic, ClassicHD, ModernHD }

		readonly World world;
		readonly List<Action> restore = new();
		readonly List<(ButtonWidget Button, string Glyph, Func<string> Text)> controls = new();
		Sprite topPanel;
		Sprite footerPanel;
		Sprite controlPanel;
		string feedbackFaction;
		readonly Dictionary<string, Sprite> glyphs = new();
		SidebarSkinMode skinMode;
		ProductionPaletteWidget palette;
		Widget production;

		[ObjectCreator.UseCtor]
		public HdSidebarWidget(World world) { this.world = world; }

		void Remember(Widget w)
		{
			var bounds = w.Bounds;
			var visible = w.IsVisible;
			restore.Add(() => { w.Bounds = bounds; w.IsVisible = visible; });
			if (w is ImageWidget image)
			{
				var stretch = image.StretchToFit;
				restore.Add(() => image.StretchToFit = stretch);
			}
			foreach (var child in w.Children) Remember(child);
		}

		void Bind(string id, string glyph)
		{
			var button = GetOrNull<ButtonWidget>(id) ?? production.GetOrNull<ButtonWidget>(id);
			if (button == null) return;
			var background = button.Background;
			var text = button.GetText;
			restore.Add(() => { button.Background = background; button.GetText = text; });
			button.Background = "";
			button.GetText = () => "";
			if (button is ProductionFooterButtonWidget footer)
			{
				footer.ExternalCancelBackground = true;
				restore.Add(() => { footer.ExternalCancelBackground = false; footer.ExternalCancelLabelBounds = null; });
			}
			if (glyph == "up" || glyph == "down") button.IsVisible = () => true;
			var icon = button.GetOrNull("ICON");
			if (icon != null) icon.IsVisible = () => false;
			controls.Add((button, glyph, text));
		}

		void InitializeSkin(SidebarSkinMode mode)
		{
			Remember(this);
			Remember(production);
			if (mode == SidebarSkinMode.ModernHD)
			{
				var columns = palette.Columns;
				var iconSize = palette.IconSize;
				var iconMargin = palette.IconMargin;
				var maxRows = palette.MaximumRows;
				var maxOffset = palette.MaxIconRowOffset;
				restore.Add(() =>
				{
					palette.ExternalSidebarLayout = false;
					palette.ApplyGridLayout(columns, iconSize, iconMargin);
					palette.MaximumRows = maxRows;
					palette.MaxIconRowOffset = maxOffset;
				});
			}
			var faction = TouchFactionSkin.Resolve(world.LocalPlayer?.Faction.InternalName);
			feedbackFaction = faction;
			controlPanel = ChromeProvider.GetImage("classic-sidebar-controls-" + faction, "panel");
			if (mode == SidebarSkinMode.ModernHD)
			{
				topPanel = ChromeProvider.GetImage("hd-sidebar-" + faction, "top");
				footerPanel = ChromeProvider.GetImage("hd-sidebar-" + faction, "footer");
				var names = new[] { "building", "support", "infantry", "vehicle", "aircraft", "ship",
					"chat", "controls", "menu", "repair", "auto", "beacon", "sell", "up", "down", "cancel", "pause", "power" };
				for (var i = 0; i < names.Length; i++)
				{
					// Generated images need measured sprite bounds, not guessed equal cells.
					// Four visible category silhouettes remain legible at phone width.
					var collection = (i < 6 ? "hd-sidebar-categories-" : "hd-sidebar-icons-") + faction;
					glyphs[names[i]] = ChromeProvider.GetImage(collection, names[i]);
				}
			}

			if (mode == SidebarSkinMode.ModernHD)
			{
				foreach (var id in new[] { "PALETTE_BACKGROUND", "PALETTE_FOREGROUND" })
				{
					var w = production.GetOrNull(id);
					if (w != null) w.IsVisible = () => false;
				}
				foreach (var child in production.Children.OfType<PowerMeterWidget>()) child.IsVisible = () => false;
				Bind("FPS_BUTTON", "chat"); Bind("FLOATING_CONTROLS_BUTTON", "controls"); Bind("OPTIONS_BUTTON", "menu");
				Bind("SCROLL_UP_BUTTON", "up"); Bind("SCROLL_DOWN_BUTTON", "down");
			}

			if (mode == SidebarSkinMode.ModernHD)
			{
				foreach (var (id, glyph) in new[]
				{
					("REPAIR_BUTTON", "repair"), ("SELL_BUTTON", "sell")
				})
				{
					Bind(id, glyph);
				}

				foreach (var id in new[] { "BUILDING", "SUPPORT", "INFANTRY", "VEHICLE" })
				{
					var button = production.Get("PRODUCTION_TYPES").Get<ProductionTypeButtonWidget>(id);
					Bind(button.Id, button.ProductionGroup.ToLowerInvariant());
				}
			}
			else
			{
				Bind("REPAIR_BUTTON", "repair");
				Bind("SELL_BUTTON", "sell");
				foreach (var id in new[] { "BUILDING", "SUPPORT", "INFANTRY", "VEHICLE" })
				{
					var button = production.Get("PRODUCTION_TYPES").Get<ProductionTypeButtonWidget>(id);
					Bind(button.Id, button.ProductionGroup.ToLowerInvariant());
				}
			}
			foreach (var id in new[] { "AUTO_REPAIR_BUTTON", "BEACON_BUTTON", "AIRCRAFT", "NAVAL" })
			{
				var hidden = GetOrNull(id) ?? production.GetOrNull(id);
				if (hidden != null)
					hidden.IsVisible = () => false;
			}
			skinMode = mode;
		}

		void ResetSkin()
		{
			foreach (var action in restore)
				action();
			foreach (var control in controls)
				if (control.Glyph == "up" || control.Glyph == "down")
					IosProductionFooterPolicy.RestoreCurrentMode(control.Button);
			restore.Clear();
			controls.Clear();
			skinMode = SidebarSkinMode.None;
			if (palette != null)
				palette.ExternalSidebarLayout = false;
		}

		void Place(Widget parent, string id, Rectangle rect)
		{
			var w = parent.GetOrNull(id);
			if (w != null) w.Bounds = new WidgetBounds(rect.X, rect.Y, rect.Width, rect.Height);
		}

		void Layout(HdSidebarLayout l)
		{
			Bounds = new WidgetBounds(l.Shell.X, 0, l.Shell.Width, l.Shell.Height);
			production.Bounds = new WidgetBounds(l.Shell.X, 0, l.Shell.Width, l.Shell.Height);
			var topButtons = Get("TOP_BUTTONS");
			topButtons.Bounds.X = topButtons.Bounds.Y = 0;
			void Row(Widget parent, string[] ids, Rectangle area)
			{
				ids = ids.Where(id => parent.GetOrNull<ButtonWidget>(id)?.IsVisible() == true).ToArray();
				if (ids.Length == 0)
					return;

				var hitTargets = HdSidebarLayout.SplitRow(area, ids.Length);
				for (var i = 0; i < ids.Length; i++)
					Place(parent, ids[i], hitTargets[i]);
			}
			Row(topButtons, new[] { "FPS_BUTTON", "FLOATING_CONTROLS_BUTTON", "OPTIONS_BUTTON" }, l.Menu);
			Place(topButtons, "DEBUG_BUTTON", new Rectangle(l.Menu.X, l.Menu.Y, 0, 0));
			Row(topButtons, new[] { "REPAIR_BUTTON", "SELL_BUTTON" }, l.Actions);
			var radar = Get("RADAR");
			radar.Bounds.X = radar.Bounds.Y = 0;
			foreach (var id in new[] { "INSIGNIA", "RADAR_FADETOBLACK", "RADAR_MINIMAP", "PLAYER" }) Place(radar, id, l.Radar);
			var insignia = radar.GetOrNull<ImageWidget>("INSIGNIA");
			if (insignia != null && !insignia.CoverToFit)
			{
				// Preserve the emblem's original aspect, rather than stretching it with the radar well.
				var sprite = insignia.GetSprite();
				var factor = Math.Min(l.Radar.Width / sprite.Size.X, l.Radar.Height / sprite.Size.Y);
				var w = (int)(sprite.Size.X * factor);
				var h = (int)(sprite.Size.Y * factor);
				insignia.Bounds = new WidgetBounds(l.Radar.X + (l.Radar.Width - w) / 2, l.Radar.Y + (l.Radar.Height - h) / 2, w, h);
				insignia.StretchToFit = true;
			}
			Place(this, "GAME_TIMER", l.Timer);
			Place(this, "CASH", new Rectangle(l.Cash.X + 20, l.Cash.Y,
				Math.Max(1, l.Cash.Width - 20), l.Cash.Height));
			Place(this, "POWER", new Rectangle(l.Power.X, l.Power.Y,
				Math.Max(1, l.Power.Width - 20), l.Power.Height));
			var categories = production.Get("PRODUCTION_TYPES");
			categories.Bounds = new WidgetBounds(0, 0, l.Shell.Width, l.Shell.Height);
			Row(categories, new[] { "BUILDING", "SUPPORT", "INFANTRY", "VEHICLE" }, l.Categories);
			Row(categories, new[] { "SCROLL_UP_BUTTON", "SCROLL_DOWN_BUTTON" }, l.Footer);
			palette.ExternalSidebarLayout = true;
			palette.ApplyGridLayout(l.Columns, new int2(l.CellWidth, l.CellHeight), new int2(l.Gap, l.Gap));
			palette.Bounds = new WidgetBounds(l.Palette.X, l.Palette.Y, l.Palette.Width,
				l.Rows * l.CellHeight + (l.Rows - 1) * l.Gap);
			palette.MaximumRows = palette.MaxIconRowOffset = l.Rows;
		}

		void LayoutClassicHd()
		{
			var topButtons = Get("TOP_BUTTONS");
			var bounds = HdSidebarLayout.ClassicActionVisualBounds(Bounds.Width);
			var touchBounds = HdSidebarLayout.ClassicActionTouchBounds(Bounds.Width);
			var ids = new[] { "REPAIR_BUTTON", "SELL_BUTTON" };
			for (var i = 0; i < ids.Length; i++)
			{
				Place(topButtons, ids[i], bounds[i]);
				var button = topButtons.GetOrNull<ButtonWidget>(ids[i]);
				button.ParentEventBounds = Platform.UsesMobileLayout ? touchBounds[i] : null;
				var icon = button?.GetOrNull("ICON");
				if (icon != null)
				{
					icon.Bounds.X = (button.Bounds.Width - icon.Bounds.Width) / 2;
					icon.Bounds.Y = (button.Bounds.Height - icon.Bounds.Height) / 2;
				}
			}
		}

		void DrawControls()
		{
			foreach (var control in controls)
			{
				var button = control.Button;
				button.Background = "";
				button.GetText = EmptyText;
				if (!button.IsVisible()) continue;
				var rect = button.RenderBounds;
				var selected = !button.IsDisabled() &&
					(button.IsHighlighted() || button.IsVisuallyPressed);
				var cancel = Platform.UsesMobileLayout && Game.Settings.Game.IosProductionCancelButtons &&
					(control.Glyph == "up" || control.Glyph == "down");
				// Each raster sprite combines the faction button frame and its symbol.
				if (cancel)
				{
					button.GetText = control.Text;
					button.Draw();
					continue;
				}
				var glyph = glyphs[control.Glyph];
				var factor = Math.Max(.001f, Math.Min((rect.Width - 2) / glyph.Size.X, (rect.Height - 2) / glyph.Size.Y));
				var size = new float2(glyph.Size.X * factor, glyph.Size.Y * factor);
				var pos = new float3(rect.X + (rect.Width - size.X) / 2, rect.Y + (rect.Height - size.Y) / 2, 0);
				var brightness = button.IsDisabled() ? .45f : 1f;
				Game.Renderer.RgbaSpriteRenderer.DrawSprite(glyph, pos, factor,
					new float3(brightness, brightness, brightness), 1f);
				if (selected) ButtonActivationOverlayWidget.DrawLight(new Rectangle((int)pos.X, (int)pos.Y, (int)size.X, (int)size.Y), feedbackFaction);
			}
		}

		void DrawClassicActivation(Rectangle panel)
		{
			// Bounds measured from the original 936 x 312 panel, not the larger touch targets.
			foreach (var control in controls)
			{
				if (control.Button.IsDisabled() || !control.Button.IsVisible() ||
					!(control.Button.IsHighlighted() || control.Button.IsVisuallyPressed)) continue;
				var source = ClassicActivationBounds(control.Glyph);
				if (source.Width == 0) continue;
				var symbol = ClassicGoldSymbolBounds(control.Glyph);
				WidgetUtils.DrawSprite(ChromeProvider.GetImage("classic-button-gold-symbols", control.Glyph),
					new float2(panel.X + symbol.X * panel.Width / 936f, panel.Y + symbol.Y * panel.Height / 312f),
					new float2(symbol.Width * panel.Width / 936f, symbol.Height * panel.Height / 312f));
				var rect = new Rectangle(panel.X + source.X * panel.Width / 936,
					panel.Y + source.Y * panel.Height / 312,
					source.Width * panel.Width / 936, source.Height * panel.Height / 312);
				ButtonActivationOverlayWidget.DrawLightSprite(
					ChromeProvider.GetImage("classic-button-activation-light", control.Glyph), rect, .65f);
			}
		}

		public static Rectangle ClassicActivationBounds(string glyph) => glyph switch
		{
			"repair" => new Rectangle(148, 43, 314, 94),
			"sell" => new Rectangle(474, 43, 314, 94),
			"building" => new Rectangle(47, 158, 194, 103),
			"support" => new Rectangle(267, 158, 185, 103),
			"infantry" => new Rectangle(487, 158, 183, 103),
			"vehicle" => new Rectangle(696, 158, 190, 103),
			_ => Rectangle.Empty
		};

		public static Rectangle ClassicGoldSymbolBounds(string glyph) => glyph switch
		{
			"repair" => new Rectangle(274, 49, 99, 82),
			"sell" => new Rectangle(580, 49, 82, 85),
			"building" => new Rectangle(93, 168, 110, 88),
			"support" => new Rectangle(315, 167, 88, 88),
			"infantry" => new Rectangle(534, 165, 96, 91),
			"vehicle" => new Rectangle(708, 182, 157, 68),
			_ => Rectangle.Empty
		};

		public override void Draw()
		{
			production ??= Ui.Root.GetOrNull("SIDEBAR_PRODUCTION");
			palette ??= production?.GetOrNull<ProductionPaletteWidget>("PRODUCTION_PALETTE");
			if (palette == null) { base.Draw(); return; }
			var style = Game.Settings.Game.EffectiveInterfaceStyle;
			var desiredMode = style == InterfaceStyleMode.ModernHD ? SidebarSkinMode.ModernHD :
				style == InterfaceStyleMode.ClassicHD ? SidebarSkinMode.ClassicHD : SidebarSkinMode.Classic;
			if (skinMode != desiredMode)
			{
				if (skinMode != SidebarSkinMode.None)
					ResetSkin();
				if (desiredMode != SidebarSkinMode.None)
					InitializeSkin(desiredMode);
			}

			if (desiredMode == SidebarSkinMode.None)
			{
				base.Draw();
				return;
			}

			if (desiredMode == SidebarSkinMode.Classic || desiredMode == SidebarSkinMode.ClassicHD)
			{
				LayoutClassicHd();
				base.Draw();
				var classicSnapshot = IosScreenMetrics.SnapshotFor(Game.Renderer.Resolution);
				var panel = HdSidebarLayout.ClassicControlPanelBounds(Bounds.Width, Bounds.Height,
					Platform.UsesMobileLayout && classicSnapshot.IsCompactPhone);
				var render = RenderBounds;
				WidgetUtils.DrawSprite(controlPanel, new float2(render.X + panel.X, render.Y + panel.Y),
					new float2(panel.Width, panel.Height));
				DrawClassicActivation(new Rectangle(render.X + panel.X, render.Y + panel.Y, panel.Width, panel.Height));
				return;
			}

			var snapshot = IosScreenMetrics.SnapshotFor(Game.Renderer.Resolution);
			var touch = Platform.UsesMobileLayout;
			var scale = touch ? snapshot.LogicalPerPoint : Math.Max(1, Game.Renderer.Resolution.Height / 1080.0);
			var l = HdSidebarLayout.Create(Game.Renderer.Resolution, touch, touch && snapshot.IsCompactPhone, scale,
				touch ? snapshot.LogicalPoints(snapshot.SafeAreaInsets.Right) : 0,
				touch ? snapshot.LogicalPoints(snapshot.SafeAreaInsets.Bottom) : 0,
				touch && Game.Settings.Game.IosProductionPaletteMode == IosProductionPaletteMode.LargeSingleColumn,
				touch && Game.Settings.Game.IosProductionPaletteMode == IosProductionPaletteMode.CompactThreeColumns);
			l.FitProductionContent(palette.TotalIconCount);
			Layout(l);
			var b = RenderBounds;
			// Preserve the original detached top console and bottom cap. The production
			// cameos and category buttons remain independent modules between them.
			var topHeight = Math.Min(b.Height, l.Actions.Bottom + Math.Max(2, l.Gap));
			WidgetUtils.DrawSprite(topPanel, new float2(b.X, b.Y), new float2(b.Width, topHeight));
			var footerTop = b.Y + Math.Max(l.Categories.Bottom, l.Footer.Top - l.Footer.Height / 3);
			var footerHeight = Math.Min(b.Bottom - footerTop, l.Footer.Height + l.Footer.Height / 3);
			var footerSprite = touch && Game.Settings.Game.IosProductionCancelButtons
				? ChromeProvider.GetImage("production-cancel-hd-" + feedbackFaction, "button") : footerPanel;
			if (touch && Game.Settings.Game.IosProductionCancelButtons)
				ProductionFooterButtonWidget.DrawCancelSprite(footerSprite,
					new Rectangle(b.X, (int)footerTop, b.Width, (int)footerHeight),
					controls.Any(c => c.Button is ProductionFooterButtonWidget && c.Button.IsFooterPressActive));
			else
				WidgetUtils.DrawSprite(footerSprite, new float2(b.X, footerTop), new float2(b.Width, footerHeight));
			foreach (var control in controls)
				if (control.Button is ProductionFooterButtonWidget footer)
					footer.ExternalCancelLabelBounds = new Rectangle(b.X, (int)footerTop, b.Width, (int)footerHeight);
			DrawControls();
		}

		static string EmptyText() => "";
	}
}
