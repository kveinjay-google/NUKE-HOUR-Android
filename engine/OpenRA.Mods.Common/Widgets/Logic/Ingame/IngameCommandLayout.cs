using System;
using System.Collections.Generic;
using System.Linq;
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Mods.Common.Widgets.Logic
{
	public sealed class IngameCommandLayout
	{
		public WidgetBounds Content { get; }
		public WidgetBounds Header { get; }
		public WidgetBounds Navigation { get; }
		public WidgetBounds Information { get; }
		public int ActionHeight { get; }
		public int TabHeight { get; }
		readonly int gap;
		readonly IosScreenSnapshot screen;
		readonly Dictionary<LabelWidget, Func<string>> originalText = new();
		readonly Dictionary<ButtonWidget, Func<string>> originalButtonText = new();

		public static WidgetBounds[] StatsColumns(int width, int target)
		{
			var actions = Math.Min(width, 2 * target);
			var text = width - actions;
			var name = text * 40 / 100;
			var faction = text * 35 / 100;
			return new[] { new WidgetBounds(0, 0, name, target), new WidgetBounds(name, 0, faction, target),
				new WidgetBounds(name + faction, 0, text - name - faction, target), new WidgetBounds(text, 0, actions, target) };
		}

		public void RestoreTextBindings()
		{
			foreach (var entry in originalText) entry.Key.GetText = entry.Value;
			originalText.Clear();
			foreach (var entry in originalButtonText) entry.Key.GetText = entry.Value;
			originalButtonText.Clear();
		}

		public void ApplyMusic(Widget music, Widget shell, ModData modData)
		{
			if (music.GetOrNull("COMMAND_MUSIC_SHELL") == null)
			{
				var art = shell.Clone();
				art.Id = "COMMAND_MUSIC_SHELL";
				music.AddChild(art);
				music.Children.Remove(art);
				music.Children.Insert(0, art);
			}
			music.Bounds = new WidgetBounds(0, 0, screen.EffectiveSize.Width, screen.EffectiveSize.Height);
			Set(music, "COMMAND_MUSIC_SHELL", 0, 0, music.Bounds.Width, music.Bounds.Height);
			var listWidth = Content.Width * 55 / 100;
			var rightX = Content.X + listWidth + gap;
			var rightWidth = Content.Right - rightX;
			var header = music.Get("LABEL_CONTAINER");
			header.Bounds = new WidgetBounds(Content.X, Content.Y, listWidth, TabHeight);
			Set(header, "TITLE", gap, 0, listWidth - 2 * gap, TabHeight);
			var type = header.GetOrNull("TYPE");
			if (type != null) type.IsVisible = () => false;
			var list = music.Get<ScrollPanelWidget>("MUSIC_LIST");
			Scroll(list, listWidth, Content.Height - 2 * TabHeight - 2 * gap);
			list.Bounds.X = Content.X;
			list.Bounds.Y = Header.Bottom + gap;
			foreach (var row in list.Children)
			{
				row.Bounds.Width = listWidth - TabHeight - 2 * gap;
				row.Bounds.Height = ActionHeight;
				Set(row, "TITLE", gap, 0, row.Bounds.Width - 2 * TabHeight - gap, ActionHeight);
				Set(row, "LENGTH", row.Bounds.Width - 2 * TabHeight, 0, 2 * TabHeight - gap, ActionHeight);
				var label = row.Get<LabelWithTooltipWidget>("TITLE");
				if (!originalText.ContainsKey(label))
				{
					var fullTitle = label.GetTooltipText?.Invoke();
					if (string.IsNullOrEmpty(fullTitle)) fullTitle = label.GetText();
					label.GetText = () => fullTitle;
					label.GetTooltipText = () => fullTitle;
				}
				Fit(label);
			}
			list.Layout.AdjustChildren();
			var controlScroll = music.GetOrNull<ScrollPanelWidget>("COMMAND_MUSIC_SCROLL");
			if (controlScroll == null)
			{
				controlScroll = new ScrollPanelWidget(modData) { Id = "COMMAND_MUSIC_SCROLL", ScrollBar = ScrollBar.Hidden };
				var body = new ContainerWidget { Id = "COMMAND_MUSIC_BODY" };
				music.AddChild(controlScroll);
				controlScroll.AddChild(body);
				foreach (var id in new[] { "TIME_LABEL", "BUTTONS", "SHUFFLE", "REPEAT" })
				{
					var child = music.Get(id);
					child.Parent.Children.Remove(child);
					body.AddChild(child);
				}
			}
			Scroll(controlScroll, rightWidth, Content.Height - TabHeight - gap);
			controlScroll.Bounds.X = rightX;
			controlScroll.Bounds.Y = Content.Y;
			controlScroll.ScrollbarWidth = 0;
			Set(music, "TIME_LABEL", 0, 0, rightWidth, TabHeight);
			var buttons = music.Get("BUTTONS");
			var toggleRows = screen.IsCompactPhone ? 2 : 1;
			buttons.Bounds = new WidgetBounds(0, TabHeight + gap, rightWidth, ActionHeight + (toggleRows + 1) * TabHeight + 2 * gap);
			foreach (var (id, index) in new[] { ("BUTTON_PREV", 0), ("BUTTON_PLAY", 1), ("BUTTON_PAUSE", 1), ("BUTTON_STOP", 2), ("BUTTON_NEXT", 3) })
			{
				var button = buttons.Get<ButtonWidget>(id);
				var bw = (rightWidth - 3 * gap) / 4;
				button.Bounds = new WidgetBounds(index * (bw + gap), 0, bw, ActionHeight);
				foreach (var image in button.Children)
				{
					image.Bounds.X = (bw - image.Bounds.Width) / 2;
					image.Bounds.Y = (ActionHeight - image.Bounds.Height) / 2;
				}
			}
			var togglesY = buttons.Bounds.Y + ActionHeight + gap;
			Set(music, "SHUFFLE", 0, togglesY, toggleRows == 2 ? rightWidth : rightWidth / 2, TabHeight);
			Set(music, "REPEAT", toggleRows == 2 ? 0 : rightWidth / 2, togglesY + (toggleRows - 1) * TabHeight,
				toggleRows == 2 ? rightWidth : rightWidth / 2, TabHeight);
			Set(buttons, "MUSIC_SLIDER", 0, ActionHeight + toggleRows * TabHeight + 2 * gap, rightWidth, TabHeight);
			Set(music, "COMMAND_MUSIC_BODY", 0, 0, rightWidth, buttons.Bounds.Bottom + gap);
			controlScroll.ContentHeight = buttons.Bounds.Bottom + gap;
			Set(music, "BACK_BUTTON", Content.Right - rightWidth, Content.Bottom - TabHeight, rightWidth, TabHeight);
			Set(music, "MUTE_LABEL", Content.X, Content.Bottom - TabHeight, listWidth, TabHeight);
			Set(music, "NO_MUSIC_LABEL", Content.X, Header.Bottom + gap, listWidth, 3 * TabHeight);
			LobbyLogic.ApplySovietLobbyStyle(music, Platform.UsesMobileLayout);
			var empty = music.Get("NO_MUSIC_LABEL");
			var line = 0;
			foreach (var label in empty.Children.OfType<LabelWidget>())
			{
				label.Bounds = new WidgetBounds(gap, line++ * TabHeight, listWidth - 2 * gap, TabHeight);
				Fit(label, true);
			}
			Fit(music.Get<LabelWidget>("MUTE_LABEL"));
		}

		public IngameCommandLayout(IosScreenSnapshot screen)
		{
			this.screen = screen;
			Content = MultiplayerScreenLayout.ContentBounds(screen, compactPhone: screen.IsCompactPhone);
			gap = screen.LogicalPoints(screen.IsCompactPhone ? 6 : 16);
			ActionHeight = screen.LogicalPoints(56);
			TabHeight = screen.LogicalPoints(48);
			Header = new WidgetBounds(Content.X, Content.Y, Content.Width,
				screen.IsCompactPhone ? 0 : screen.LogicalPoints(80));
			var navWidth = screen.IsCompactPhone ? Math.Min(screen.LogicalPoints(160), Content.Width * 22 / 100) :
				Math.Min(screen.LogicalPoints(280), Content.Width * 28 / 100);
			var y = Header.Bottom + gap;
			var bottomClearance = screen.LogicalPoints(screen.IsCompactPhone ? 6 : 80);
			var contentHeight = Math.Max(1, Content.Bottom - bottomClearance - y);
			Navigation = new WidgetBounds(Content.X, y, navWidth, contentHeight);
			Information = new WidgetBounds(Navigation.Right + gap, y, Content.Right - Navigation.Right - gap, contentHeight);
		}

		static void Set(Widget root, string id, int x, int y, int width, int height)
		{
			var child = root.GetOrNull(id);
			if (child != null)
				child.Bounds = new WidgetBounds(x, y, Math.Max(1, width), Math.Max(1, height));
		}

		void Scroll(ScrollPanelWidget scroll, int width, int height)
		{
			scroll.Bounds = new WidgetBounds(0, 0, Math.Max(1, width), Math.Max(1, height));
			scroll.ScrollBar = ScrollBar.Hidden;
			scroll.ScrollbarWidth = 0;
			scroll.EnableContentDragging = true;
			scroll.ContentDragThreshold = screen.LogicalPoints(8);
			scroll.TopBottomSpacing = gap;
			scroll.ItemSpacing = gap;
		}

		void Fit(LabelWidget label, bool multiline = false)
		{
			if (!originalText.TryGetValue(label, out var getText))
				originalText[label] = getText = label.GetText;
			label.GetText = () =>
			{
				var font = Game.Renderer.Fonts[label.Font];
				Size Measure(string text) => new Size(font.Measure(text).X, font.Measure(text).Y);
				return multiline ? Ingame.IosSupportPowerTooltipPolicy.FitText(getText(), label.Bounds.Width, label.Bounds.Height, Measure) :
					Ingame.IosSupportPowerTooltipPolicy.FitSingleLine(getText(), label.Bounds.Width, Measure);
			};
		}

		int ObjectiveText(Widget row, string id, int y, int width, ModData modData)
		{
			var checkbox = row.Get<CheckboxWidget>(id);
			var label = row.GetOrNull<LabelWidget>(id + "_COPY");
			if (label == null)
			{
				label = new LabelWidget(modData) { Id = id + "_COPY", VAlign = TextVAlign.Top };
				label.GetText = checkbox.GetText;
				row.AddChild(label);
			}
			checkbox.GetText = () => "";
			label.Font = Platform.UsesMobileLayout ? "IosRegular" : "SettingsRegular";
			label.Bounds = new WidgetBounds(TabHeight + gap, y, Math.Max(1, width - TabHeight - 2 * gap), int.MaxValue);
			Fit(label, true);
			var height = Math.Max(TabHeight, Game.Renderer.Fonts[label.Font].Measure(label.GetText()).Y);
			label.Bounds.Height = height;
			checkbox.Bounds = new WidgetBounds(0, y, TabHeight, TabHeight);
			return height;
		}

		public void Apply(Widget root, ModData modData)
		{
			root.Bounds = new WidgetBounds(0, 0, screen.EffectiveSize.Width, screen.EffectiveSize.Height);
			Set(root, "MENU_SHELL", 0, 0, root.Bounds.Width, root.Bounds.Height);
			Set(root, "INGAME_SOVIET_SHELL", 0, 0, root.Bounds.Width, root.Bounds.Height);
			var brandWidth = screen.LogicalPoints(420);
			var brand = root.GetOrNull("SHELL_BRAND");
			if (brand != null)
				brand.IsVisible = () => !screen.IsCompactPhone;
			Set(root, "SHELL_BRAND", (root.Bounds.Width - brandWidth) / 2,
				Header.Y - screen.LogicalPoints(16), brandWidth, screen.LogicalPoints(112));
			Set(root, "PANEL_ROOT", Information.X, Information.Y, Information.Width, Information.Height);
			var nav = root.Get<ScrollPanelWidget>("MENU_NAV");
			Scroll(nav, Navigation.Width, Navigation.Height);
			nav.Bounds.X = Navigation.X;
			nav.Bounds.Y = Navigation.Y;
			var commands = root.Get("MENU_BUTTONS");
			commands.Bounds = new WidgetBounds(gap, gap, nav.Bounds.Width - nav.ScrollbarWidth - 2 * gap, 0);
			var commandButtons = commands.Children.OfType<ButtonWidget>().ToArray();
			var columns = 1;
			var commandHeight = screen.IsCompactPhone && commandButtons.Length > 0 ?
				Math.Max(screen.LogicalPoints(40), (Navigation.Height - (commandButtons.Length + 1) * gap) / commandButtons.Length) : ActionHeight;
			var cellWidth = (commands.Bounds.Width - (columns - 1) * gap) / columns;
			for (var i = 0; i < commandButtons.Length; i++)
			{
				var button = commandButtons[i];
				button.Bounds = new WidgetBounds(
					i % columns * (cellWidth + gap),
					i / columns * (commandHeight + gap), cellWidth, commandHeight);
				button.IsHighlighted = () => button.Id == "RESUME";
				if (!originalButtonText.TryGetValue(button, out var text)) originalButtonText[button] = text = button.GetText;
				button.GetText = () => Ingame.IosSupportPowerTooltipPolicy.FitSingleLine(text(), button.Bounds.Width - 2 * gap,
					value => new Size(Game.Renderer.Fonts[button.Font].Measure(value).X, 1));
			}
			var rows = (commandButtons.Length + columns - 1) / columns;
			commands.Bounds.Height = rows == 0 ? 0 : rows * commandHeight + (rows - 1) * gap;
			nav.ContentHeight = commands.Bounds.Bottom + gap;
			var info = root.GetOrNull("GAME_INFO_PANEL");
			if (info != null)
				LayoutInfo(info, modData);
			LobbyLogic.ApplySovietLobbyStyle(root, Platform.UsesMobileLayout);
			var chat = root.GetOrNull("CHAT_CONTAINER");
			if (chat?.LogicObjects != null)
				foreach (var logic in chat.LogicObjects.OfType<IngameChatLogic>())
					logic.ApplyCommandMenuLayout(Platform.UsesMobileLayout);
		}

		void LayoutInfo(Widget info, ModData modData)
		{
			info.Bounds = new WidgetBounds(0, 0, Information.Width, Information.Height);
			Set(info, "TITLE", 0, 0, info.Bounds.Width, TabHeight);
			Fit(info.Get<LabelWidget>("TITLE"));
			var tabsHeight = ActionHeight;
			foreach (var tabs in info.Children.Where(c => c.Id.StartsWith("TAB_CONTAINER_", StringComparison.Ordinal)))
			{
				var buttons = tabs.Children.OfType<ButtonWidget>().ToArray();
				var columns = screen.IsCompactPhone ? Math.Max(1, buttons.Length) :
					Math.Min(buttons.Length, Math.Max(1, info.Bounds.Width / screen.LogicalPoints(120)));
				var height = ((buttons.Length + columns - 1) / columns) * (ActionHeight + gap) - gap;
				tabs.Bounds = new WidgetBounds(0, gap, info.Bounds.Width, height);
				for (var i = 0; i < buttons.Length; i++)
				{
					var left = i % columns * (info.Bounds.Width + gap) / columns;
					var right = (i % columns + 1) * (info.Bounds.Width + gap) / columns - gap;
					buttons[i].Bounds = new WidgetBounds(left, i / columns * (ActionHeight + gap), right - left, ActionHeight);
					var icon = buttons[i].GetOrNull("INGAME_INFO_TAB_ICON");
					if (icon != null)
					{
						var size = Math.Min(screen.LogicalPoints(32), (right - left) / 3);
						icon.Bounds = new WidgetBounds(gap, (ActionHeight - size) / 2, size, size);
						buttons[i].LeftMargin = size + 2 * gap;
						buttons[i].RightMargin = gap;
						buttons[i].Align = TextAlign.Left;
						if (!originalButtonText.TryGetValue(buttons[i], out var text))
							originalButtonText[buttons[i]] = text = buttons[i].GetText;
						var button = buttons[i];
						button.GetText = () => Ingame.IosSupportPowerTooltipPolicy.FitSingleLine(text(),
							button.Bounds.Width - button.LeftMargin - button.RightMargin,
							value => new Size(Game.Renderer.Fonts[button.Font].Measure(value).X, 1));
					}
				}
				if (tabs.IsVisible())
					tabsHeight = height;
			}
			var bodyY = tabsHeight + 2 * gap;
			foreach (var id in new[] { "STATS_PANEL", "MAP_PANEL", "OBJECTIVES_PANEL", "DEBUG_PANEL", "CHAT_PANEL", "LOBBY_OPTIONS_PANEL" })
			{
				var container = info.Children.FirstOrDefault(c => c.Id == id);
				if (container == null)
					continue;
				container.Bounds = new WidgetBounds(0, bodyY, info.Bounds.Width, Math.Max(1, info.Bounds.Height - bodyY));
				foreach (var page in container.Children.ToArray())
				{
					if (page.Id == "COMMAND_PAGE_SCROLL" || page.Id is "SKIRMISH_STATS" or "SCRIPT_ERROR_PANEL" or "MISSION_OBJECTIVES" or "MAP_PANEL")
					{
						var scroll = page as ScrollPanelWidget;
						var body = page;
						if (scroll == null)
						{
							// Reparent without Removed(): keep world event subscriptions alive.
							container.Children.Remove(page);
							scroll = new ScrollPanelWidget(modData) { Id = "COMMAND_PAGE_SCROLL" };
							container.AddChild(scroll);
							scroll.AddChild(page);
						}
						else body = scroll.Children[0];
						Scroll(scroll, container.Bounds.Width, container.Bounds.Height);
						body.Bounds = new WidgetBounds(gap, gap, container.Bounds.Width - TabHeight - 2 * gap,
							Math.Max(screen.LogicalPoints(400), container.Bounds.Height - 2 * gap));
						LayoutPage(body, modData);
						scroll.ContentHeight = body.Bounds.Bottom + gap;
						continue;
					}
					page.Bounds = new WidgetBounds(0, 0, container.Bounds.Width, container.Bounds.Height);
					LayoutPage(page, modData);
				}
			}
		}

		void LayoutPage(Widget page, ModData modData)
		{
			var width = page.Bounds.Width;
			var height = page.Bounds.Height;
			if (page.Id == "DEBUG_PANEL")
			{
				var scroll = page.GetOrNull<ScrollPanelWidget>("COMMAND_DEBUG_SCROLL");
				if (scroll == null)
				{
					var children = page.Children.ToArray();
					page.Children.Clear();
					scroll = new ScrollPanelWidget(modData) { Id = "COMMAND_DEBUG_SCROLL" };
					page.AddChild(scroll);
					var body = new ContainerWidget { Id = "COMMAND_DEBUG_BODY" };
					scroll.AddChild(body);
					foreach (var child in children)
						body.AddChild(child);
				}
				Scroll(scroll, width, height);
				var content = scroll.Get("COMMAND_DEBUG_BODY");
				var columns = width >= screen.LogicalPoints(850) ? 3 :
					width >= screen.LogicalPoints(screen.IsCompactPhone ? 360 : 560) ? 2 : 1;
				var cellWidth = (width - TabHeight - (columns + 1) * gap) / columns;
				var cy = 0;
				var col = 0;
				foreach (var child in content.Children)
				{
					if (child is LabelWidget)
					{
						if (col != 0) { cy += ActionHeight + gap; col = 0; }
						child.Bounds = new WidgetBounds(gap, cy, width - TabHeight - 2 * gap, TabHeight);
						cy += TabHeight + gap;
					}
					else if (child is ButtonWidget)
					{
						child.Bounds = new WidgetBounds(gap + col * (cellWidth + gap), cy, cellWidth, ActionHeight);
						if (++col == columns) { cy += ActionHeight + gap; col = 0; }
					}
				}
				if (col != 0) cy += ActionHeight + gap;
				content.Bounds = new WidgetBounds(0, gap, width - TabHeight, cy);
				scroll.ContentHeight = content.Bounds.Bottom + gap;
			}
			else if (page.Id == "LOBBY_OPTIONS_PANEL")
			{
				var scroll = page.Children.OfType<ScrollPanelWidget>().FirstOrDefault();
				if (scroll == null) return;
				Scroll(scroll, width, height);
				var options = scroll.Get("LOBBY_OPTIONS");
				var contentWidth = width - TabHeight - 2 * gap;
				var y = 0;
				foreach (var row in options.Children)
				{
					var controls = row.Children.OfType<ButtonWidget>().ToArray();
					var columns = width >= screen.LogicalPoints(screen.IsCompactPhone ? 360 : 640) ? 2 : 1;
					var dropdown = controls.Any(c => c is DropDownButtonWidget);
					var stride = ActionHeight + gap + (dropdown ? TabHeight : 0);
					row.Bounds = new WidgetBounds(0, y, contentWidth, ((controls.Length + columns - 1) / columns) * stride);
					for (var i = 0; i < controls.Length; i++)
					{
						var x = i % columns * ((contentWidth + gap) / columns);
						var cy = i / columns * stride;
						var cw = (contentWidth - (columns - 1) * gap) / columns;
						controls[i].Bounds = new WidgetBounds(x, cy + (dropdown ? TabHeight : 0), cw, ActionHeight);
						Set(row, controls[i].Id + "_DESC", x, cy, cw, TabHeight);
					}
					y += row.Bounds.Height + gap;
				}
				options.Bounds = new WidgetBounds(gap, gap, contentWidth, y);
				scroll.ContentHeight = y + 2 * gap;
			}
			else if (page.Id == "CHAT_CONTAINER")
			{
				Set(page, "CHAT_CHROME", 0, 0, width, height);
				Set(page, "CHAT_MODE", 0, height - ActionHeight, 2 * TabHeight, ActionHeight);
				Set(page, "CHAT_TEXTFIELD", 2 * TabHeight + gap, height - ActionHeight, width - 2 * TabHeight - gap, ActionHeight);
				Set(page, "CHAT_SCROLLPANEL", 0, 0, width, height - ActionHeight - gap);
			}
			else if (page.Id == "MAP_PANEL")
			{
				var mapHeight = Math.Min(screen.LogicalPoints(160), height / 3);
				Set(page, "PREVIEW_BG", 0, 0, width, mapHeight);
				Set(page, "MAP_PREVIEW", gap, gap, width - 2 * gap, mapHeight - 2 * gap);
				Set(page, "MAP_DESCRIPTION_PANEL", 0, mapHeight + gap, width, height - mapHeight - gap);
				var description = page.Get<LabelWidget>("MAP_DESCRIPTION");
				description.Bounds.Width = width - TabHeight - 2 * gap;
				description.Bounds.Height = screen.LogicalPoints(2000);
				Fit(description, true);
				description.Bounds.Height = Game.Renderer.Fonts[description.Font].Measure(description.GetText()).Y + gap;
				page.Get<ScrollPanelWidget>("MAP_DESCRIPTION_PANEL").Layout.AdjustChildren();
			}
			else if (page.Id == "MISSION_OBJECTIVES")
			{
				Set(page, "MISSION", 0, 0, width / 3, TabHeight);
				Set(page, "MISSION_STATUS", width / 3, 0, width * 2 / 3, TabHeight);
				var scroll = page.Get<ScrollPanelWidget>("OBJECTIVES_PANEL");
				Scroll(scroll, width, height - TabHeight - gap);
				scroll.Bounds.Y = TabHeight + gap;
				foreach (var row in scroll.Children)
				{
					row.Bounds.Width = width - TabHeight;
					Set(row, "OBJECTIVE_TYPE", gap, 0, row.Bounds.Width - 2 * gap, TabHeight);
					row.Bounds.Height = TabHeight + ObjectiveText(row, "OBJECTIVE_STATUS", TabHeight, row.Bounds.Width, modData) + gap;
				}
					scroll.Layout.AdjustChildren();
			}
			else if (page.Id == "SCRIPT_ERROR_PANEL")
			{
				var y = 0;
				foreach (var id in new[] { "DESCA", "DESCB", "DESCC" })
				{
					var label = page.Get<LabelWidget>(id);
					label.Bounds = new WidgetBounds(gap, y, width - 2 * gap, TabHeight * 2);
					Fit(label, true);
					y += 2 * TabHeight + gap;
				}
				Set(page, "SCRIPT_ERROR_MESSAGE_PANEL", 0, y, width, Math.Max(TabHeight, height - y));
			}
			else if (page.Id == "SKIRMISH_STATS")
			{
				var objective = page.Get("OBJECTIVE");
				var objectiveHeight = TabHeight + ObjectiveText(objective, "STATS_CHECKBOX", TabHeight, width, modData) + gap;
				Set(page, "OBJECTIVE", 0, 0, width, objectiveHeight);
				Set(page, "MISSION", TabHeight + gap, 0, width / 3 - TabHeight - gap, TabHeight);
				Set(page, "STATS_STATUS", width / 3, 0, width * 2 / 3 - gap, TabHeight);
				Set(page, "STATS_HEADERS", 0, objectiveHeight + gap, width - TabHeight, TabHeight);
				var scroll = page.Get<ScrollPanelWidget>("PLAYER_LIST");
				Scroll(scroll, width, Math.Max(2 * TabHeight, height - objectiveHeight - TabHeight - 2 * gap));
				scroll.Bounds.Y = objectiveHeight + TabHeight + 2 * gap;
				page.Bounds.Height = Math.Max(height, scroll.Bounds.Bottom);
				foreach (var row in scroll.Children.Append(page.Get("STATS_HEADERS")))
				{
					row.Bounds.Width = width - TabHeight;
					row.Bounds.Height = TabHeight;
					var w = row.Bounds.Width;
					var cols = StatsColumns(w, TabHeight);
					Set(row, "PROFILE", 0, (TabHeight - 16) / 2, 16, 16);
					Set(row, "PROFILE_TOOLTIP", 0, (TabHeight - 16) / 2, 16, 16);
					Set(row, "NAME", 20, 0, cols[0].Width - 20, TabHeight);
					var flag = row.GetOrNull("FACTIONFLAG");
					if (flag != null) flag.IsVisible = () => false;
					Set(row, "FACTION", cols[1].X, 0, cols[1].Width, TabHeight);
					Set(row, "SCORE", cols[2].X, 0, cols[2].Width, TabHeight);
					Set(row, "TEAM", gap, 0, cols[2].X - gap, TabHeight);
					Set(row, "TEAM_SCORE", cols[2].X, 0, cols[2].Width, TabHeight);
					Set(row, "ACTIONS", cols[3].X, 0, cols[3].Width, TabHeight);
					Set(row, "MUTE", w - 2 * TabHeight, 0, TabHeight, TabHeight);
					Set(row, "KICK", w - TabHeight, 0, TabHeight, TabHeight);
					foreach (var label in row.Children.OfType<LabelWidget>()) Fit(label);
				}
				scroll.Layout.AdjustChildren();
			}
		}
	}
}
