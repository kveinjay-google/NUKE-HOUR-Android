#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software. It is
 * made available under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version.
 */
#endregion

using System;
using System.Linq;
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Mods.Common.Widgets.Logic
{
	public sealed class IosTouchMenuLogic : ChromeLogic
	{
		readonly Widget widget;
		Size lastResolution;
		Size lastNativePointSize;
		Rectangle lastSafeBounds;
		WidgetBounds lastWidgetBounds;
		Widget lastOptionsFirstRow;
		int lastOptionsRowCount = -1;
		Widget lastServerFirstRow;
		int lastServerRowCount = -1;
		bool layoutInitialized;
		bool lastNoticeVisible;
		Widget lastClientFirstRow;
		int lastClientRowCount = -1;

		bool IsMultiplayerScreen => MultiplayerScreenLayout.SupportsDesktop(
			widget.Id, widget.GetOrNull("MULTIPLAYER_CONTENT") != null);

		bool IsMultiplayerLobbyChild
		{
			get
			{
				for (var parent = widget.Parent; parent != null; parent = parent.Parent)
					if (parent.Id == "SERVER_LOBBY" && parent.GetOrNull("LOBBY_CONTENT") != null)
						return true;

				return false;
			}
		}

		[ObjectCreator.UseCtor]
		public IosTouchMenuLogic(Widget widget)
		{
			this.widget = widget;
			ApplyLayout();
		}

		public override void Tick()
		{
			if (widget.GetOrNull("COMMAND_MUSIC_SHELL") != null)
				return;
			if (!Platform.UsesMobileLayout && !IsMultiplayerScreen && !IsMultiplayerLobbyChild)
				return;

			var resolution = Game.Renderer.Resolution;
			var snapshot = IosScreenMetrics.SnapshotFor(resolution);
			if (!layoutInitialized || resolution != lastResolution ||
				snapshot.NativePointSize != lastNativePointSize || snapshot.SafeBounds != lastSafeBounds ||
				!SameBounds(widget.Bounds, lastWidgetBounds))
			{
				ApplyLayout();
				return;
			}

			var options = widget.GetOrNull("LOBBY_OPTIONS");
			if (options != null && OptionsRowsChanged(options))
			{
				LayoutLobbyOptionsRows(options, IosMenuLayoutPolicy.Create(true, snapshot));
				if (!Platform.UsesMobileLayout && IsMultiplayerLobbyChild)
					LobbyLogic.ApplySovietLobbyStyle(widget, false);
			}

			var serverList = widget.GetOrNull<ScrollPanelWidget>("SERVER_LIST");
			if (serverList != null)
			{
				var policy = IosMenuLayoutPolicy.Create(true, snapshot);
				var noticeVisible = EvaluateDynamicVisibility(widget.GetOrNull("NOTICE_CONTAINER"));
				if (ServerRowsChanged(serverList) || noticeVisible != lastNoticeVisible)
					ApplyServerBrowserLayout(widget, policy);

				// Selection callbacks can replace the client list and reposition Join on desktop.
				// Keep their data and visibility bindings while restoring the responsive geometry.
				if (IsMultiplayerScreen)
				{
					var selected = widget.GetOrNull("SELECTED_SERVER");
					var clients = selected?.GetOrNull("MULTIPLAYER_CLIENT_LIST");
					if (clients != null && (clients.Children.Count != lastClientRowCount ||
						clients.Children.FirstOrDefault() != lastClientFirstRow))
					{
						ApplyMultiplayerSkin(clients);
						lastClientFirstRow = clients.Children.FirstOrDefault();
						lastClientRowCount = clients.Children.Count;
					}

					IosMultiplayerDetailsLayout.Apply(selected, policy,
						EvaluateDynamicVisibility(clients));
				}
			}
		}

		void ApplyLayout()
		{
			if (widget.GetOrNull("COMMAND_MUSIC_SHELL") != null)
				return;
			if (!Platform.UsesMobileLayout && !IsMultiplayerScreen && !IsMultiplayerLobbyChild)
				return;

			var resolution = Game.Renderer.Resolution;
			var snapshot = IosScreenMetrics.SnapshotFor(resolution);
			var policy = IosMenuLayoutPolicy.Create(true, snapshot);
			if (IsMultiplayerScreen)
			{
				widget.Bounds = policy.ViewportBounds;
				var maximumWidth = widget.Id == "DIRECTCONNECT_PANEL" ? 760 :
					widget.Id == "MULTIPLAYER_CREATESERVER_PANEL" ? 1120 : 0;
				var maximumHeight = widget.Id == "DIRECTCONNECT_PANEL" ? 320 :
					widget.Id == "MULTIPLAYER_CREATESERVER_PANEL" ? 700 : 0;
				widget.Get("MULTIPLAYER_CONTENT").Bounds =
					MultiplayerScreenLayout.ContentBounds(snapshot, maximumWidth, maximumHeight, compactPhone: Platform.UsesMobileLayout);
			}

			ApplyIosFonts(widget);
			AdaptTree(widget, policy);
			if (IsMultiplayerScreen)
			{
				ApplyMultiplayerSkin(widget);
				var serverTitle = widget.GetOrNull("SERVER_TEMPLATE")?.GetOrNull<LabelWidget>("TITLE");
				if (serverTitle != null)
					serverTitle.Font = "IosRegular";
			}

			var options = widget.GetOrNull("LOBBY_OPTIONS");
			if (options != null)
				LayoutLobbyOptionsRows(options, policy);

			if (IsServerCreationPanel(widget))
				ApplyServerCreationLayout(widget, policy);
			else if (IsServerBrowserPanel(widget))
				ApplyServerBrowserLayout(widget, policy);
			else if (widget.Id == "DIRECTCONNECT_PANEL")
				ApplyDirectConnectLayout(widget, policy);

			if (IsMultiplayerScreen || IsMultiplayerLobbyChild)
			{
				if (!Platform.UsesMobileLayout && IsMultiplayerLobbyChild)
					LobbyLogic.ApplySovietLobbyStyle(widget, false);

				var filters = widget.GetOrNull<DropDownButtonWidget>("FILTERS_DROPDOWNBUTTON");
				if (filters != null)
				{
					filters.PreparePanel = PrepareMultiplayerFilters;
					filters.GetPopupSafeBounds = () => IosScreenMetrics.SnapshotFor(Game.Renderer.Resolution).SafeBounds;
				}
			}

			lastResolution = resolution;
			lastNativePointSize = snapshot.NativePointSize;
			lastSafeBounds = snapshot.SafeBounds;
			lastWidgetBounds = widget.Bounds;
			layoutInitialized = true;
		}

		static void PrepareMultiplayerFilters(Widget popup)
		{
			if (popup is not ScrollPanelWidget scroll)
				return;

			var snapshot = IosScreenMetrics.SnapshotFor(Game.Renderer.Resolution);
			var policy = IosMenuLayoutPolicy.Create(true, snapshot);
			var rows = scroll.Children.OfType<CheckboxWidget>().ToArray();
			scroll.Bounds = MultiplayerScreenLayout.FilterPanelBounds(snapshot, popup.Bounds.Width, rows.Length);
			scroll.ScrollBar = ScrollBar.Hidden;
			scroll.ScrollbarWidth = 0;
			scroll.EnableContentDragging = true;
			scroll.ContentDragThreshold = policy.ContentDragThreshold;
			for (var i = 0; i < rows.Length; i++)
			{
				rows[i].Bounds = MultiplayerScreenLayout.FilterRowBounds(scroll.Bounds.Width, i, policy);
				rows[i].Font = Platform.UsesMobileLayout ? "IosRegular" : "SettingsRegular";
			}

			scroll.ContentHeight = rows.Length * (policy.MinimumTarget + policy.Gap) + policy.Gap;
			ApplyMultiplayerSkin(scroll);
		}

		static void ApplyMultiplayerSkin(Widget root)
		{
			switch (root)
			{
				case ScrollPanelWidget scroll:
					scroll.Background = "cc-mp-surface";
					scroll.ScrollBarBackground = "cc-mp-surface";
					scroll.Button = "cc-mp-control";
					break;
				case ScrollItemWidget item:
					item.SetBackground("cc-mp-control");
					break;
				case CheckboxWidget checkbox:
					checkbox.Background = "cc-mp-control";
					break;
				case ButtonWidget button:
					button.Background = "cc-mp-control";
					break;
				case TextFieldWidget field:
					field.Background = "cc-mp-field";
					break;
				case BackgroundWidget background:
					background.Background = background.Id is "SERVER_TABLE_FRAME" or "SERVER_DETAILS_FRAME"
						? "cc-mp-control" : "cc-mp-surface";
					break;
			}

			foreach (var child in root.Children)
				ApplyMultiplayerSkin(child);
		}

		static void AdaptTree(Widget root, IosMenuLayoutPolicy policy)
		{
			if (root is ButtonWidget or TextFieldWidget or SliderWidget)
				root.Bounds = policy.EnsureTouchTarget(root.Bounds);

			if (root is SliderWidget slider)
				ConfigureStepSlider(slider);

			if (root is ScrollPanelWidget scrollPanel)
			{
				scrollPanel.EnableContentDragging = true;
				scrollPanel.ContentDragThreshold = policy.ContentDragThreshold;
				if (policy.IsPhone)
				{
					scrollPanel.ScrollBar = ScrollBar.Hidden;
					scrollPanel.ScrollbarWidth = 0;
				}
				else if (scrollPanel.ScrollBar != ScrollBar.Hidden)
				{
					scrollPanel.ScrollbarWidth = Math.Max(scrollPanel.ScrollbarWidth, policy.MinimumTarget);
					scrollPanel.MinimumThumbSize = Math.Max(scrollPanel.MinimumThumbSize, policy.MinimumTarget);
				}
			}

			foreach (var child in root.Children.ToArray())
				AdaptTree(child, policy);
		}

		public static void ApplyIosFonts(Widget root)
		{
			if (root is LabelWidget label)
				label.Font = IosMenuLayoutPolicy.TouchFont(
					label.Font, label.Id is "TITLE" or "SERVER_NAME", false);
			else if (root is ButtonWidget button)
				button.Font = ButtonFont(button.Id, button.Font);
			else if (root is TextFieldWidget textField)
				textField.Font = IosMenuLayoutPolicy.TouchFont(textField.Font, false, false);
			else if (root is HotkeyEntryWidget hotkeyEntry)
				hotkeyEntry.Font = IosMenuLayoutPolicy.TouchFont(hotkeyEntry.Font, false, false);
			else if (root is SliderWidget slider)
				slider.TouchFont = IosMenuLayoutPolicy.TouchFont(slider.TouchFont, false, true);

			foreach (var child in root.Children)
				ApplyIosFonts(child);
		}

		public static string ButtonFont(string id, string currentFont)
		{
			var primaryRoute = id is
				"SINGLEPLAYER_BUTTON" or "MULTIPLAYER_BUTTON" or "CAMPAIGN_BUTTON" or
				"SETTINGS_BUTTON" or
				"SKIRMISH_BUTTON" or "MISSIONS_BUTTON" or "LOAD_BUTTON";
			return IosMenuLayoutPolicy.TouchFont(currentFont, primaryRoute, true);
		}

		void LayoutLobbyOptionsRows(Widget options, IosMenuLayoutPolicy policy)
		{
			if (options.Parent is not ScrollPanelWidget panel)
				return;

			var title = widget.GetOrNull<LabelWidget>("TITLE");
			var panelY = 0;
			var phoneGrid = Platform.UsesMobileLayout && policy.IsPhone && widget.Id == "LOBBY_OPTIONS_BIN";
			if (title != null && phoneGrid)
				title.IsVisible = () => false;
			if (EvaluateDynamicVisibility(title))
			{
				title.Bounds = new WidgetBounds(0, 0, widget.Bounds.Width, policy.MinimumTarget);
				panelY = title.Bounds.Bottom + policy.Gap;
			}

			panel.Bounds = new WidgetBounds(0, panelY, widget.Bounds.Width,
				Math.Max(0, widget.Bounds.Height - panelY));
			panel.EnableContentDragging = true;
			panel.ContentDragThreshold = policy.ContentDragThreshold;
			if (phoneGrid)
				panel.ScrollBar = ScrollBar.Hidden;
			var scrollbarWidth = panel.ScrollBar == ScrollBar.Hidden ? 0 : panel.ScrollbarWidth;
			var contentWidth = Math.Max(policy.MinimumTarget,
				panel.Bounds.Width - scrollbarWidth - 2 * policy.Gap);
			options.Bounds = new WidgetBounds(policy.Gap, phoneGrid ? 0 : policy.Gap, contentWidth, 0);

			if (phoneGrid)
			{
				options.Bounds.Height = LayoutPhoneOptionGrid(options, contentWidth,
					policy.MinimumTarget, policy.MinimumReadableTextHeight, Math.Max(1, (policy.Gap - 1) / 2));
				panel.ContentHeight = options.Bounds.Bottom;
				lastOptionsFirstRow = options.Children.FirstOrDefault();
				lastOptionsRowCount = options.Children.Count;
				return;
			}

			var y = 0;
			foreach (var row in options.Children)
			{
				var dropdown = row.Children.Any(child => child is DropDownButtonWidget ||
					child is ButtonWidget && child.Id == "RESET_OPTIONS_BUTTON");
				row.Bounds = new WidgetBounds(0, y, contentWidth, policy.OptionRowHeight(dropdown));
				var controls = row.Children.Where(child => child is CheckboxWidget or DropDownButtonWidget ||
					child is ButtonWidget && child.Id == "RESET_OPTIONS_BUTTON").ToArray();
				for (var i = 0; i < controls.Length; i++)
				{
					controls[i].Bounds = policy.OptionControlBounds(i, contentWidth, dropdown);
					var label = row.GetOrNull<LabelWidget>(controls[i].Id + "_DESC");
					if (label != null)
						label.Bounds = policy.OptionLabelBounds(i, contentWidth);
				}

				y = row.Bounds.Bottom + policy.Gap;
			}

			options.Bounds.Height = Math.Max(0, y - (options.Children.Count > 0 ? policy.Gap : 0));
			panel.ContentHeight = options.Bounds.Bottom + policy.Gap;
			lastOptionsFirstRow = options.Children.FirstOrDefault();
			lastOptionsRowCount = options.Children.Count;
		}

		public static int LayoutPhoneOptionGrid(Widget options, int width, int target, int labelHeight, int gap)
		{
			var y = 0;
			foreach (var dropdown in new[] { false, true })
			{
				var controls = options.Children.SelectMany(row => row.Children
					.Where(child => (dropdown ? child is DropDownButtonWidget ||
						child is ButtonWidget && child.Id == "RESET_OPTIONS_BUTTON" : child is CheckboxWidget) &&
						EvaluateDynamicVisibility(child)).Select(child => (Row: row, Control: child))).ToArray();
				var cellHeight = target + (dropdown ? labelHeight + gap : 0);
				for (var i = 0; i < controls.Length; i++)
				{
					var (row, control) = controls[i];
					var column = i % 4;
					var left = column * (width + gap) / 4;
					var right = (column + 1) * (width + gap) / 4 - gap;
					var top = y + i / 4 * (cellHeight + gap);
					control.Bounds = new WidgetBounds(left, top + (dropdown ? labelHeight + gap : 0), right - left, target);
					var label = row.GetOrNull<LabelWidget>(control.Id + "_DESC");
					if (dropdown && label != null)
						label.Bounds = new WidgetBounds(left, top, right - left, labelHeight);
				}
				if (controls.Length > 0)
					y += (controls.Length + 3) / 4 * (cellHeight + gap);
			}
			var height = Math.Max(0, y - gap);
			// Retain each original row and its bound actions/labels, but share the
			// grid origin so template boundaries no longer introduce empty rows.
			foreach (var row in options.Children)
				row.Bounds = new WidgetBounds(0, 0, width, height);
			return height;
		}

		static void ApplyServerCreationLayout(Widget panel, IosMenuLayoutPolicy policy)
		{
			var content = panel.GetOrNull("MULTIPLAYER_CONTENT");
			var layout = content == null ? IosServerCreationLayout.Create(policy) :
				IosServerCreationLayout.Create(content.Bounds.Width, content.Bounds.Height, policy);
			if (content == null)
				panel.Bounds = policy.ContentBounds;
			else
			{
				panel = content;
				var scroll = panel.Get<ScrollPanelWidget>("CREATE_FORM_SCROLL");
				scroll.Bounds = layout.Form;
				scroll.ScrollBar = ScrollBar.Hidden;
				scroll.ScrollbarWidth = 0;
				scroll.ContentHeight = layout.FormContentHeight + 2 * policy.Gap;
				panel.Get("CREATE_FORM").Bounds = new WidgetBounds(
					policy.Gap, policy.Gap, layout.Form.Width - 2 * policy.Gap, layout.FormContentHeight);
			}

			SetBounds(panel, "TITLE", layout.Header);
			LayoutLabeledField(panel, "SERVER_NAME_LABEL", "SERVER_NAME", layout.ServerNameRow, policy);
			LayoutPasswordRow(panel, layout.PasswordRow, policy);
			LayoutPortRow(panel, layout.PortRow, policy);
			LayoutNotices(panel, layout, policy);
			SetBounds(panel, "MAP_PREVIEW_ROOT", layout.MapPreview);
			SetBounds(panel, "MAP_BUTTON", layout.ChangeMap);
			SetBounds(panel, "CREATE_BUTTON", layout.CreateButton);
			SetBounds(panel, "BACK_BUTTON", layout.Back);

			var mapRoot = panel.GetOrNull("MAP_PREVIEW_ROOT");
			if (mapRoot != null)
				IosMapPreviewLayout.Apply(mapRoot, policy);
		}

		static void LayoutLabeledField(
			Widget panel, string labelId, string fieldId, WidgetBounds row, IosMenuLayoutPolicy policy)
		{
			if (policy.IsPhone)
			{
				var labelHeight = Math.Min(policy.MinimumReadableTextHeight, row.Height);
				var fieldY = Math.Min(row.Bottom, row.Y + labelHeight + policy.Gap);
				var fieldHeight = Math.Max(0, row.Bottom - fieldY);
				SetBounds(panel, labelId, new WidgetBounds(row.X, row.Y, row.Width, labelHeight));
				SetBounds(panel, fieldId, new WidgetBounds(row.X, fieldY, row.Width, fieldHeight));
				return;
			}

			var labelWidth = Math.Max(policy.MinimumTarget, row.Width / 4);
			var label = panel.GetOrNull(labelId);
			if (label != null)
				label.Bounds = new WidgetBounds(row.X, row.Y, labelWidth, row.Height);

			var fieldX = Math.Min(row.Right, row.X + labelWidth + policy.Gap);
			var field = panel.GetOrNull(fieldId);
			if (field != null)
				field.Bounds = new WidgetBounds(fieldX, row.Y, Math.Max(0, row.Right - fieldX), row.Height);
		}

		static void LayoutPasswordRow(Widget panel, WidgetBounds row, IosMenuLayoutPolicy policy)
		{
			if (policy.IsPhone)
			{
				var labelHeight = Math.Min(policy.MinimumReadableTextHeight, row.Height);
				var fieldY = Math.Min(row.Bottom, row.Y + labelHeight + policy.Gap);
				var fieldHeight = Math.Max(0, row.Bottom - fieldY);
				var phoneSuffixWidth = Math.Min(row.Width / 3, Math.Max(0, row.Width - policy.MinimumTarget - policy.Gap));
				var phoneFieldWidth = Math.Max(0, row.Width - phoneSuffixWidth - policy.Gap);
				SetBounds(panel, "PASSWORD_LABEL", new WidgetBounds(row.X, row.Y, row.Width, labelHeight));
				SetBounds(panel, "PASSWORD", new WidgetBounds(row.X, fieldY, phoneFieldWidth, fieldHeight));
				SetBounds(panel, "AFTER_PASSWORD_LABEL", new WidgetBounds(
					row.X + phoneFieldWidth + policy.Gap, fieldY, phoneSuffixWidth, fieldHeight));
				return;
			}

			var labelWidth = Math.Max(policy.MinimumTarget, row.Width / 4);
			SetBounds(panel, "PASSWORD_LABEL", new WidgetBounds(row.X, row.Y, labelWidth, row.Height));
			var fieldX = Math.Min(row.Right, row.X + labelWidth + policy.Gap);
			var available = Math.Max(0, row.Right - fieldX);
			var suffixWidth = Math.Min(available / 3, Math.Max(policy.MinimumTarget, available / 4));
			var fieldWidth = Math.Max(0, available - suffixWidth - policy.Gap);
			SetBounds(panel, "PASSWORD", new WidgetBounds(fieldX, row.Y, fieldWidth, row.Height));
			SetBounds(panel, "AFTER_PASSWORD_LABEL", new WidgetBounds(
				Math.Min(row.Right, fieldX + fieldWidth + policy.Gap), row.Y, suffixWidth, row.Height));
		}

		static void LayoutPortRow(Widget panel, WidgetBounds row, IosMenuLayoutPolicy policy)
		{
			if (policy.IsPhone)
			{
				var labelHeight = Math.Min(policy.MinimumReadableTextHeight, row.Height);
				var fieldY = Math.Min(row.Bottom, row.Y + labelHeight + policy.Gap);
				var fieldHeight = Math.Max(0, row.Bottom - fieldY);
				var phoneFieldWidth = Math.Min(policy.MinimumTarget, row.Width);
				var phoneAdvertiseX = Math.Min(row.Right, row.X + phoneFieldWidth + policy.Gap);
				SetBounds(panel, "LISTEN_PORT_LABEL", new WidgetBounds(row.X, row.Y, row.Width, labelHeight));
				SetBounds(panel, "LISTEN_PORT", new WidgetBounds(row.X, fieldY, phoneFieldWidth, fieldHeight));
				SetBounds(panel, "ADVERTISE_CHECKBOX", new WidgetBounds(
					phoneAdvertiseX, fieldY, Math.Max(0, row.Right - phoneAdvertiseX), fieldHeight));
				return;
			}

			var labelWidth = policy.IsPhone
				? Math.Min(policy.MinimumTarget, Math.Max(0, row.Width / 5))
				: Math.Max(policy.MinimumTarget, row.Width / 4);
			SetBounds(panel, "LISTEN_PORT_LABEL", new WidgetBounds(row.X, row.Y, labelWidth, row.Height));
			var fieldX = Math.Min(row.Right, row.X + labelWidth + policy.Gap);
			var fieldWidth = Math.Min(policy.IsPhone ? policy.MinimumTarget : policy.MinimumTarget * 2,
				Math.Max(0, row.Right - fieldX));
			SetBounds(panel, "LISTEN_PORT", new WidgetBounds(fieldX, row.Y, fieldWidth, row.Height));
			var advertiseX = Math.Min(row.Right, fieldX + fieldWidth + policy.Gap);
			SetBounds(panel, "ADVERTISE_CHECKBOX", new WidgetBounds(
				advertiseX, row.Y, Math.Max(0, row.Right - advertiseX), row.Height));
		}

		static void LayoutNotices(Widget panel, IosServerCreationLayout layout, IosMenuLayoutPolicy policy)
		{
			var header = layout.NoticesHeader;
			var content = layout.NoticesBody;
			var a = panel.GetOrNull<LabelWidget>("NOTICES_HEADER_A");
			var b = panel.GetOrNull<LabelWidget>("NOTICES_HEADER_B");
			var c = panel.GetOrNull<LabelWidget>("NOTICES_HEADER_C");
			LayoutNoticeHeader(a, b, c, header);

			foreach (var id in new[] { "NOTICES_LAN", "NOTICES_NO_UPNP", "NOTICES_UPNP" })
			{
				var notice = panel.GetOrNull(id);
				if (notice == null)
					continue;

				notice.Bounds = content;
				var lines = notice.Children.OfType<LabelWidget>().ToArray();
				if (lines.Length == 0)
					continue;

				var lineHeight = Math.Max(1, content.Height / lines.Length);
				var y = 0;
				for (var i = 0; i < lines.Length; i++)
				{
					var indent = lines[i].Bounds.X > 0 ? policy.Gap / 2 : 0;
					var height = i == lines.Length - 1 ? Math.Max(0, content.Height - y) : lineHeight;
					lines[i].Bounds = new WidgetBounds(indent, y,
						Math.Max(0, content.Width - indent), height);
					lines[i].WordWrap = true;
					y += lineHeight;
				}
			}
		}

		public static void RefreshServerNoticeHeader(Widget panel)
		{
			var content = panel.GetOrNull("MULTIPLAYER_CONTENT");
			if (!Platform.UsesMobileLayout && content == null)
				return;

			var snapshot = IosScreenMetrics.SnapshotFor(Game.Renderer.Resolution);
			var policy = IosMenuLayoutPolicy.Create(true, snapshot);
			var layout = content == null ? IosServerCreationLayout.Create(policy) :
				IosServerCreationLayout.Create(content.Bounds.Width, content.Bounds.Height, policy);
			LayoutNoticeHeader(
				panel.GetOrNull<LabelWidget>("NOTICES_HEADER_A"),
				panel.GetOrNull<LabelWidget>("NOTICES_HEADER_B"),
				panel.GetOrNull<LabelWidget>("NOTICES_HEADER_C"),
				layout.NoticesHeader);
		}

		static void LayoutNoticeHeader(
			LabelWidget a, LabelWidget b, LabelWidget c, WidgetBounds header)
		{
			var labels = new[] { a, b, c };
			var widths = labels.Select(label =>
				!EvaluateDynamicVisibility(label) ? 0 :
				Game.Renderer.Fonts[label.Font].Measure(label.GetText() ?? "").X).ToArray();
			var segments = IosServerCreationLayout.HeaderSegments(header.Width, widths);
			for (var i = 0; i < labels.Length; i++)
				if (labels[i] != null)
					labels[i].Bounds = new WidgetBounds(
						header.X + segments[i].X, header.Y, segments[i].Width, header.Height);
		}

		static bool IsServerCreationPanel(Widget panel)
		{
			return panel.Id == "MULTIPLAYER_CREATESERVER_PANEL" ||
				(panel.GetOrNull("CREATE_BUTTON") != null && panel.GetOrNull("MAP_PREVIEW_ROOT") != null &&
					panel.GetOrNull("SERVER_NAME") != null);
		}

		static bool IsServerBrowserPanel(Widget panel)
		{
			return panel.Id is "MULTIPLAYER_PANEL" or "LOBBY_SERVERS_BIN" ||
				(panel.GetOrNull("SERVER_LIST") != null && panel.GetOrNull("SELECTED_SERVER") != null);
		}

		void ApplyServerBrowserLayout(Widget panel, IosMenuLayoutPolicy policy)
		{
			var standalone = panel.Id == "MULTIPLAYER_PANEL";
			var content = panel.GetOrNull("MULTIPLAYER_CONTENT");
			if (content != null)
				panel = content;
			else if (standalone)
				panel.Bounds = policy.ContentBounds;

			var width = Math.Max(1, panel.Bounds.Width);
			var height = Math.Max(1, panel.Bounds.Height);
			var layout = IosMultiplayerBrowserLayout.Create(width, height, policy, content != null);
			if (standalone)
			{
				SetBounds(panel, "TITLE", layout.Header);
				SetBounds(panel, "LOCAL_MODE_BUTTON", layout.LocalMode);
				SetBounds(panel, "ONLINE_MODE_BUTTON", layout.OnlineMode);
			}

			if (standalone)
			{
				SetBounds(panel, "SERVER_TABLE_FRAME", layout.Table);
				SetBounds(panel, "SERVER_DETAILS_FRAME", layout.Details);
			}

			var table = standalone ? layout.TableContent : new WidgetBounds(
				0, 0, layout.Table.Width, Math.Max(0, height - policy.MinimumTarget - policy.Gap));
			var details = standalone ? layout.DetailsContent : new WidgetBounds(
				table.Right + policy.Gap, 0, layout.Details.Width, height);
			var headerHeight = Math.Min(policy.MinimumReadableTextHeight, table.Height);
			var listY = Math.Min(table.Height, headerHeight + policy.Gap);
			var listBounds = new WidgetBounds(table.X, table.Y + listY,
				table.Width, Math.Max(0, table.Height - listY));
			var noticeBounds = new WidgetBounds(
				listBounds.X, listBounds.Y, listBounds.Width,
				Math.Min(policy.MinimumReadableTextHeight, listBounds.Height));
			var notice = panel.GetOrNull("NOTICE_CONTAINER");
			if (notice != null)
			{
				notice.Bounds = noticeBounds;
				foreach (var label in notice.Children.OfType<LabelWidget>())
				{
					label.Bounds = new WidgetBounds(
						policy.Gap, 0, Math.Max(1, noticeBounds.Width - 2 * policy.Gap), noticeBounds.Height);
					label.WordWrap = false;
					label.VAlign = TextVAlign.Middle;
					IosResponsiveText.Configure(label, false);
				}
			}

			var effectiveListBounds = IosMultiplayerBrowserLayout.ServerListWithNotice(
				listBounds, noticeBounds.Height, EvaluateDynamicVisibility(notice));
			lastNoticeVisible = EvaluateDynamicVisibility(notice);

			var labels = panel.GetOrNull("LABEL_CONTAINER");
			if (labels != null)
			{
				labels.Bounds = new WidgetBounds(table.X, table.Y, table.Width, headerHeight);
				IosMultiplayerBrowserLayout.ApplyServerRow(labels,
					IosMultiplayerBrowserLayout.ServerColumnBounds(table.Width, policy.MinimumTarget),
					headerHeight, policy);
			}

			var list = panel.GetOrNull<ScrollPanelWidget>("SERVER_LIST");
			if (list != null)
			{
				list.Bounds = effectiveListBounds;
				list.EnableContentDragging = true;
				list.ContentDragThreshold = policy.ContentDragThreshold;
				if (policy.IsPhone)
				{
					list.ScrollBar = ScrollBar.Hidden;
					list.ScrollbarWidth = 0;
				}
				else
				{
					list.ScrollBar = ScrollBar.Right;
					list.ScrollbarWidth = policy.MinimumTarget;
					list.MinimumThumbSize = policy.MinimumTarget;
				}

				var contentWidth = Math.Max(0, list.Bounds.Width - list.ScrollbarWidth);
				var columns = IosMultiplayerBrowserLayout.ServerColumnBounds(contentWidth, policy.MinimumTarget);
				foreach (var row in list.Children)
				{
					if (content != null)
						ApplyMultiplayerSkin(row);

					row.Bounds = new WidgetBounds(0, row.Bounds.Y, contentWidth, policy.MinimumTarget);
					IosMultiplayerBrowserLayout.ApplyServerRow(
						row, columns, policy.MinimumTarget, policy);
				}

				list.Layout.AdjustChildren();
				lastServerFirstRow = list.Children.FirstOrDefault();
				lastServerRowCount = list.Children.Count;
			}

			SetBounds(panel, "PROGRESS_LABEL", effectiveListBounds);
			var selected = panel.GetOrNull("SELECTED_SERVER");
			if (selected != null)
			{
				selected.Bounds = details;
				var dynamicClients = selected.GetOrNull("MULTIPLAYER_CLIENT_LIST");
				IosMultiplayerDetailsLayout.Apply(
					selected, policy, EvaluateDynamicVisibility(dynamicClients));
			}

			if (standalone)
			{
				SetBounds(panel, "FILTERS_DROPDOWNBUTTON", Offset(layout.Filters, layout.Footer.Y));
				SetBounds(panel, "RELOAD_BUTTON", Offset(layout.Reload, layout.Footer.Y));
				SetBounds(panel, "PLAYER_COUNT", Offset(layout.PlayerCount, layout.Footer.Y));
				SetBounds(panel, "ROOM_CODE_INPUT", Offset(layout.RoomCodeInput, layout.Footer.Y));
				SetBounds(panel, "ROOM_CODE_BUTTON", Offset(layout.RoomCodeButton, layout.Footer.Y));
				SetBounds(panel, "DIRECTCONNECT_BUTTON", Offset(layout.DirectConnect, layout.Footer.Y));
				SetBounds(panel, "CREATE_BUTTON", Offset(layout.CreateButton, layout.Footer.Y));
				SetBounds(panel, "BACK_BUTTON", Offset(layout.Back, layout.Footer.Y));
			}
			else
			{
				var footerY = Math.Max(0, height - policy.MinimumTarget);
				var filtersWidth = Math.Max(policy.MinimumTarget, (table.Width - policy.Gap) * 3 / 4);
				SetBounds(panel, "FILTERS_DROPDOWNBUTTON",
					new WidgetBounds(0, footerY, filtersWidth, policy.MinimumTarget));
				SetBounds(panel, "RELOAD_BUTTON", new WidgetBounds(
					filtersWidth + policy.Gap, footerY,
					Math.Max(0, table.Width - filtersWidth - policy.Gap), policy.MinimumTarget));
			}

			var reload = panel.GetOrNull("RELOAD_BUTTON");
			var reloadImage = reload?.GetOrNull("IMAGE_RELOAD");
			if (reloadImage != null)
				reloadImage.Bounds = new WidgetBounds(
					(reload.Bounds.Width - reloadImage.Bounds.Width) / 2,
					(reload.Bounds.Height - reloadImage.Bounds.Height) / 2,
					reloadImage.Bounds.Width, reloadImage.Bounds.Height);
		}

		static void ApplyDirectConnectLayout(Widget panel, IosMenuLayoutPolicy policy)
		{
			var content = panel.GetOrNull("MULTIPLAYER_CONTENT");
			if (content != null)
				panel = content;
			else
				panel.Bounds = policy.ContentBounds;
			var width = panel.Bounds.Width;
			var height = panel.Bounds.Height;
			var title = panel.GetOrNull<LabelWidget>("DIRECTCONNECT_LABEL_TITLE");
			if (title != null)
				title.Font = "IosTitle";
			SetBounds(panel, "DIRECTCONNECT_LABEL_TITLE", new WidgetBounds(0, 0, width, policy.HeaderHeight));
			var rowY = Math.Min(height, policy.HeaderHeight + policy.Gap);
			var rowHeight = Math.Min(policy.MinimumReadableTextHeight + policy.Gap + policy.MinimumTarget,
				Math.Max(0, height - rowY - policy.FooterHeight - 2 * policy.Gap));
			var addressWidth = Math.Max(0, width * 3 / 4 - policy.Gap);
			var portX = Math.Min(width, addressWidth + policy.Gap);
			var labelHeight = Math.Min(policy.MinimumReadableTextHeight, rowHeight);
			var fieldY = Math.Min(rowY + rowHeight, rowY + labelHeight + policy.Gap);
			var fieldHeight = Math.Max(0, rowY + rowHeight - fieldY);
			SetBounds(panel, "ADDRESS_LABEL", new WidgetBounds(0, rowY, addressWidth, labelHeight));
			SetBounds(panel, "IP", new WidgetBounds(0, fieldY, addressWidth, fieldHeight));
			SetBounds(panel, "PORT_LABEL", new WidgetBounds(portX, rowY, Math.Max(0, width - portX), labelHeight));
			SetBounds(panel, "PORT", new WidgetBounds(portX, fieldY, Math.Max(0, width - portX), fieldHeight));
			var footerY = Math.Max(rowY + rowHeight + policy.Gap, height - policy.FooterHeight);
			var buttonWidth = Math.Max(0, (width - policy.Gap) / 2);
			SetBounds(panel, "JOIN_BUTTON", new WidgetBounds(0, footerY, buttonWidth, Math.Max(0, height - footerY)));
			SetBounds(panel, "BACK_BUTTON", new WidgetBounds(
				buttonWidth + policy.Gap, footerY, Math.Max(0, width - buttonWidth - policy.Gap),
				Math.Max(0, height - footerY)));
		}

		static WidgetBounds Offset(WidgetBounds bounds, int y)
		{
			return new WidgetBounds(bounds.X, bounds.Y + y, bounds.Width, bounds.Height);
		}

		bool OptionsRowsChanged(Widget options)
		{
			return options.Children.Count != lastOptionsRowCount ||
				options.Children.FirstOrDefault() != lastOptionsFirstRow;
		}

		bool ServerRowsChanged(ScrollPanelWidget list)
		{
			return list.Children.Count != lastServerRowCount || list.Children.FirstOrDefault() != lastServerFirstRow;
		}

		static void SetBounds(Widget root, string id, WidgetBounds bounds)
		{
			var child = root.GetOrNull(id);
			if (child != null)
				child.Bounds = bounds;
		}

		static bool SameBounds(WidgetBounds a, WidgetBounds b)
		{
			return a.X == b.X && a.Y == b.Y && a.Width == b.Width && a.Height == b.Height;
		}

		public static bool EvaluateDynamicVisibility(Widget widget)
		{
			return widget != null && widget.IsVisible();
		}

		static void ConfigureStepSlider(SliderWidget slider)
		{
			var step = slider.Ticks > 1
				? (slider.MaximumValue - slider.MinimumValue) / (slider.Ticks - 1)
				: (slider.MaximumValue - slider.MinimumValue) / 10f;

			slider.TouchStep = step;
			slider.UseTouchStepControls = true;
		}
	}
}
