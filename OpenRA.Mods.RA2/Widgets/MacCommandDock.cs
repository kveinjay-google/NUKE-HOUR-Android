using System;
using System.Collections.Generic;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Mods.RA2.Widgets
{
	// Presentation only: existing buttons continue to own all commands and hotkeys.
	sealed class MacCommandDock
	{
		readonly CustomCommandBarWidget owner;
		readonly Dictionary<string, ButtonWidget> buttons;
		readonly Dictionary<string, Func<string>> captions = new();
		readonly ButtonWidget more;
		readonly ButtonWidget[] tabs = new ButtonWidget[3];
		readonly Widget slots;
		readonly ButtonWidget options;
		readonly Action<KeyInput> originalOptionsKey;
		Rectangle lastAvailable;
		MacCommandDockLayout layout;
		bool open;
		int page;
		readonly MacCommandDockArt art;
		static readonly Color Border = Color.FromArgb(255, 77, 79, 75);
		static readonly Color Ink = Color.FromArgb(255, 229, 229, 215);
		static readonly Color Muted = Color.FromArgb(255, 136, 142, 140);
		Color Accent => art.Accent;

		public MacCommandDock(CustomCommandBarWidget owner, Dictionary<string, ButtonWidget> buttons,
			ButtonWidget more, ButtonWidget edit, LabelWidget hint, World world)
		{
			art = new MacCommandDockArt(world);
			this.owner = owner;
			this.buttons = buttons;
			this.more = more;
			slots = owner.Get("COMMAND_SLOTS");
			// The sidebar handles Escape before this sibling in the widget tree.
			// Intercept only that keyboard action; clicking Options stays unchanged.
			options = Ui.Root.GetOrNull<ButtonWidget>("OPTIONS_BUTTON");
			if (options != null)
			{
				originalOptionsKey = options.OnKeyPress;
				options.OnKeyPress = input => { if (!Key(input)) originalOptionsKey(input); };
			}
			foreach (var pair in buttons)
			{
				var label = pair.Value.GetOrNull<LabelWidget>("LABEL");
				captions[pair.Key] = label?.GetText ?? (() => "");
				if (pair.Key == "SELECT_ALL") captions[pair.Key] = () => Text("全选", "Select all");
				if (pair.Key.StartsWith("GROUP_", StringComparison.Ordinal)) captions[pair.Key] = () => Text("编队", "Group");
				Prepare(pair.Value);
			}

			if (edit != null) edit.IsVisible = () => false;
			if (hint != null) hint.IsVisible = () => false;
			Prepare(more);
			more.OnClick = () => { open = !open; Refresh(true); };
			more.IsHighlighted = () => open;
			more.GetTooltipText = () => Text(open ? "收起指令" : "更多指令", open ? "Close commands" : "More commands");
			more.GetTooltipDesc = () => Text("编队、战术与导航 · Esc 收起", "Groups, tactics and navigation · Esc to close");
			for (var i = 0; i < tabs.Length; i++)
			{
				var index = i;
				var tab = new ButtonWidget(Game.ModData) { Id = "MAC_DOCK_TAB_" + i, Background = "", VisualHeight = 0 };
				tab.GetText = () => "";
				tab.OnClick = () => { page = index; Refresh(true); };
				tab.IsVisible = () => open;
				tabs[i] = tab;
				owner.AddChild(tab);
			}

			Refresh(true);
		}

		static void Prepare(ButtonWidget button)
		{
			button.Background = "";
			button.GetText = () => "";
			button.VisualHeight = 0;
			foreach (var child in button.Children)
				child.IsVisible = () => false;
		}

		static string Text(string chinese, string english) => LanguageSelectionPolicy.Resolve(
			Game.Settings.Game.Language, Game.Settings.SystemLanguageTag) == LanguageSelectionPolicy.SimplifiedChinese ? chinese : english;

		public void Refresh(bool force = false)
		{
			var resolution = Game.Renderer.Resolution;
			var sidebar = Ui.Root.GetOrNull("SIDEBAR_PRODUCTION");
			var right = sidebar != null && sidebar.IsVisible() ? sidebar.RenderBounds.Left - 12 : resolution.Width - 12;
			var available = new Rectangle(12, 0, Math.Max(240, right - 12), resolution.Height);
			if (!force && available == lastAvailable)
				return;
			lastAvailable = available;
			layout = new MacCommandDockLayout(available, open, page);
			var parent = owner.Parent?.ChildOrigin ?? int2.Zero;
			owner.Bounds = new WidgetBounds(layout.Bounds.X - parent.X, layout.Bounds.Y - parent.Y,
				layout.Bounds.Width, layout.Bounds.Height);
			slots.Bounds = new WidgetBounds(0, 0, layout.Bounds.Width, layout.Bounds.Height);
			foreach (var button in buttons.Values)
				button.IsVisible = () => false;
			for (var i = 0; i < MacCommandDockLayout.Primary.Length; i++)
				Place(MacCommandDockLayout.Primary[i], layout.PrimarySlot(i));
			if (open)
				for (var i = 0; i < MacCommandDockLayout.Pages[page].Length; i++)
					Place(MacCommandDockLayout.Pages[page][i], layout.DrawerSlot(i));
			SetBounds(more, layout.PrimarySlot(7));
			more.IsVisible = () => true;
			var tabWidth = (layout.Bounds.Width - 20) / 3;
			for (var i = 0; i < tabs.Length; i++)
				SetBounds(tabs[i], new Rectangle(10 + i * tabWidth, 7, tabWidth, 27));
			Ui.ResetTooltips();
		}

		void Place(string id, Rectangle rect)
		{
			if (!buttons.TryGetValue(id, out var button)) return;
			SetBounds(button, rect);
			button.IsVisible = () => true;
		}

		static void SetBounds(Widget widget, Rectangle rect) => widget.Bounds = new WidgetBounds(rect.X, rect.Y, rect.Width, rect.Height);

		public bool Key(KeyInput input)
		{
			if (!open || input.Event != KeyInputEvent.Down || input.Key != Keycode.ESCAPE) return false;
			open = false;
			Refresh(true);
			return true;
		}

		public void Removed()
		{
			if (options != null) options.OnKeyPress = originalOptionsKey;
		}

		public void Draw()
		{
			art.Refresh();
			var bounds = owner.RenderBounds;
			WidgetUtils.FillRectWithColor(new Rectangle(bounds.X + 3, bounds.Y + 4, bounds.Width, bounds.Height), Color.FromArgb(100, 0, 0, 0));
			art.DrawPanel(bounds);
			var renderer = Game.Renderer.RgbaColorRenderer;
			if (open)
			{
				var names = new[] { Text("编队", "Groups"), Text("战术", "Tactics"), Text("导航", "Navigation") };
				for (var i = 0; i < tabs.Length; i++)
				{
					var tab = tabs[i].RenderBounds;
					if (i == page || Ui.MouseOverWidget == tabs[i])
						WidgetUtils.FillRectWithColor(tab, Color.FromArgb(255, 47, 48, 43));
					if (i == page)
						WidgetUtils.FillRectWithColor(new Rectangle(tab.X + 12, tab.Bottom - 2, tab.Width - 24, 2), Accent);
					Caption(names[i], tab, i == page ? Accent : Muted, 6);
				}

				var y = bounds.Y + layout.MainY - 5;
				renderer.DrawLine(new float3(bounds.X + 10, y, 0), new float3(bounds.Right - 10, y, 0), 1, Border);
			}

			foreach (var pair in buttons)
				if (pair.Value.IsVisible())
					DrawButton(pair.Value, pair.Key, captions[pair.Key]());
			DrawButton(more, open ? "CLOSE" : "MORE", Text(open ? "收起" : "更多", open ? "Close" : "More"));
		}

		static void Caption(string text, Rectangle bounds, Color color, int y)
		{
			var font = Game.Renderer.Fonts["TinyBold"];
			text = WidgetUtils.TruncateText(text, Math.Max(1, bounds.Width - 4), font);
			font.DrawText(text, new float2(bounds.X + (bounds.Width - font.Measure(text).X) / 2, bounds.Y + y), color);
		}

		void DrawButton(ButtonWidget button, string id, string caption)
		{
			var bounds = button.RenderBounds;
			var disabled = button.IsDisabled();
			var active = !disabled && button.IsHighlighted();
			var hover = !disabled && Ui.MouseOverWidget == button;
			var pressed = !disabled && button.IsVisuallyPressed;
			var ink = disabled ? Color.FromArgb(255, 91, 98, 99) : active || hover ? Accent : Ink;
			if (active || hover || pressed)
				WidgetUtils.FillRectWithColor(bounds, Color.FromArgb(pressed ? 110 : 65, Accent));
			if (active)
				WidgetUtils.FillRectWithColor(new Rectangle(bounds.X + 10, bounds.Bottom - 3, Math.Max(1, bounds.Width - 20), 2), Accent);
			var size = Math.Min(36, bounds.Width - 6);
			var x = bounds.X + (bounds.Width - size) / 2;
			var y = bounds.Y + 2 + (pressed ? 1 : 0);
			art.DrawIcon(id, new Rectangle(x, y, size, size), disabled, active || pressed);
			if (id.StartsWith("GROUP_", StringComparison.Ordinal))
				caption = (int.Parse(id.Substring(6)) % 10) + " " + caption;
			Caption(caption, bounds, disabled ? Muted : ink, 38 + (pressed ? 1 : 0));
		}
	}
}
