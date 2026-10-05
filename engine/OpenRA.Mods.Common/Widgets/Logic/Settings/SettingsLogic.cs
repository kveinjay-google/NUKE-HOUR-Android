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
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Mods.Common.Widgets.Logic
{
	public class SettingsLogic : ChromeLogic
	{
		static readonly ConditionalWeakTable<LabelWidget, Func<string>> UnwrappedLabelText = new();
		[FluentReference]
		const string SettingsSaveTitle = "dialog-settings-save.title";

		[FluentReference]
		const string SettingsSavePrompt = "dialog-settings-save.prompt";

		[FluentReference]
		const string SettingsSaveCancel = "dialog-settings-save.cancel";

		[FluentReference]
		const string RestartTitle = "dialog-settings-restart.title";

		[FluentReference]
		const string RestartPrompt = "dialog-settings-restart.prompt";

		[FluentReference]
		const string RestartAccept = "dialog-settings-restart.confirm";

		[FluentReference]
		const string RestartCancel = "dialog-settings-restart.cancel";

		[FluentReference("panel")]
		const string ResetTitle = "dialog-settings-reset.title";

		[FluentReference]
		const string ResetPrompt = "dialog-settings-reset.prompt";

		[FluentReference]
		const string ResetAccept = "dialog-settings-reset.confirm";

		[FluentReference]
		const string ResetCancel = "dialog-settings-reset.cancel";

		readonly Dictionary<string, Func<bool>> leavePanelActions = new();
		readonly Dictionary<string, Action> resetPanelActions = new();

		readonly Widget panelContainer, tabContainer;
		readonly ButtonWidget tabTemplate;
		readonly int2 buttonStride;
		readonly List<ButtonWidget> buttons = new();
		readonly Dictionary<string, string> panels = new();
		readonly Dictionary<Widget, WidgetBounds> iosOriginalBounds = new();
		readonly Dictionary<ScrollPanelWidget, (int TopBottom, int Item, int Scrollbar, ScrollBar Position)> iosOriginalScrollMetrics = new();
		readonly Widget settingsWidget;
		readonly ContainerWidget panelTemplate;
		readonly LabelWidget contextLabel;
		Size lastViewportSize;
		Size lastIosNativePointSize;
		Rectangle lastIosSafeBounds;
		string activePanel;

		bool needsRestart = false;

		static SettingsLogic() { }

		[ObjectCreator.UseCtor]
		public SettingsLogic(Widget widget, Action onExit, WorldRenderer worldRenderer, Dictionary<string, MiniYaml> logicArgs, ModData modData)
		{
			settingsWidget = widget;
			contextLabel = widget.GetOrNull<LabelWidget>("SETTINGS_LABEL_CONTEXT");
			if (contextLabel != null)
				contextLabel.GetText = () => activePanel != null && panels.TryGetValue(activePanel, out var label) ?
					FluentProvider.GetMessage(label) : "";
			panelContainer = widget.Get("PANEL_CONTAINER");
			panelTemplate = panelContainer.Get<ContainerWidget>("PANEL_TEMPLATE");
			panelContainer.RemoveChild(panelTemplate);

			tabContainer = widget.Get("SETTINGS_TAB_CONTAINER");
			tabTemplate = tabContainer.Get<ButtonWidget>("BUTTON_TEMPLATE");
			tabContainer.RemoveChild(tabTemplate);

			if (logicArgs.TryGetValue("ButtonStride", out var buttonStrideNode))
				buttonStride = FieldLoader.GetValue<int2>("ButtonStride", buttonStrideNode.Value);

			if (logicArgs.TryGetValue("Panels", out var settingsPanels))
			{
				panels = settingsPanels.ToDictionary(kv => kv.Value);

				foreach (var panel in panels)
				{
					var container = panelTemplate.Clone() as ContainerWidget;
					container.Id = panel.Key;
					panelContainer.AddChild(container);

					Game.LoadWidget(worldRenderer.World, panel.Key, container, new WidgetArgs()
					{
						{ "registerPanel", (Action<string, string, Func<Widget, Func<bool>>, Func<Widget, Action>>)RegisterSettingsPanel },
						{ "panelID", panel.Key },
						{ "label", panel.Value }
					});
					ApplySettingsVisualStyle(container);
				}
			}

			CaptureIosOriginalLayout(widget);
			iosOriginalBounds.Add(panelTemplate, panelTemplate.Bounds);
			if (TouchLayoutFor(Game.Renderer.Resolution).Enabled)
			{
				ApplyIosLayout();
			}
			else
			{
				foreach (var panel in panels.Keys)
					ApplyPlatformVisibility(panelContainer.Get(panel), false);
				ApplyDesktopLayout();
			}

			widget.Get<ButtonWidget>("BACK_BUTTON").OnClick = () =>
			{
				needsRestart |= leavePanelActions[activePanel]();
				var current = Game.Settings;
				current.Save();

				void CloseAndExit() { Ui.CloseWindow(); onExit(); }
				if (needsRestart)
				{
					void NoRestart() => ConfirmationDialogs.ButtonPrompt(modData,
						title: SettingsSaveTitle,
						text: SettingsSavePrompt,
						onCancel: CloseAndExit,
						cancelText: SettingsSaveCancel);

					if (!Game.ExternalMods.TryGetValue(ExternalMod.MakeKey(Game.ModData.Manifest), out var external))
					{
						NoRestart();
						return;
					}

					ConfirmationDialogs.ButtonPrompt(modData,
						title: RestartTitle,
						text: RestartPrompt,
						onConfirm: () => Game.SwitchToExternalMod(external, null, NoRestart),
						confirmText: RestartAccept,
						onCancel: CloseAndExit,
						cancelText: RestartCancel);
				}
				else
					CloseAndExit();
			};

			widget.Get<ButtonWidget>("RESET_BUTTON").OnClick = () =>
			{
				void Reset()
				{
					resetPanelActions[activePanel]();
					Game.Settings.Save();
				}

				ConfirmationDialogs.ButtonPrompt(modData,
					title: ResetTitle,
					text: ResetPrompt,
					titleArguments: new object[] { "panel", FluentProvider.GetMessage(panels[activePanel]) },
					onConfirm: Reset,
					confirmText: ResetAccept,
					onCancel: () => { },
					cancelText: ResetCancel);
			};
		}

		static void ApplySettingsVisualStyle(Widget root)
		{
			foreach (var widget in DescendantsAndSelf(root))
			{
				if (widget is CheckboxWidget checkbox)
				{
					checkbox.Background = "settings-v2-checkbox";
					checkbox.VisualHeight = 0;
				}
				else if (widget is ButtonWidget button)
				{
					button.Background = "settings-v2-control";
					button.VisualHeight = 0;
				}
				else if (widget is TextFieldWidget textField)
					textField.Background = "settings-v2-control";
				else if (widget is HotkeyEntryWidget hotkeyEntry)
					hotkeyEntry.Background = "settings-v2-control";
				else if (widget is SliderWidget slider)
				{
					slider.Track = "settings-v2-slider-track";
					slider.Thumb = "settings-v2-control";
					slider.TouchBackground = "settings-v2-control";
				}
				else if (widget is ScrollPanelWidget scrollPanel)
				{
					scrollPanel.Background = "settings-v2-scrollpanel";
					scrollPanel.ScrollBarBackground = "settings-v2-scrollpanel";
					scrollPanel.Button = "settings-v2-control";
				}
			}
		}

		void CaptureIosOriginalLayout(Widget widget)
		{
			iosOriginalBounds.Add(widget, widget.Bounds);
			if (widget is ScrollPanelWidget scrollPanel)
				iosOriginalScrollMetrics.Add(scrollPanel,
					(scrollPanel.TopBottomSpacing, scrollPanel.ItemSpacing, scrollPanel.ScrollbarWidth, scrollPanel.ScrollBar));

			foreach (var child in widget.Children)
				CaptureIosOriginalLayout(child);
		}

		void RestoreIosOriginalLayout()
		{
			foreach (var entry in iosOriginalBounds)
				entry.Key.Bounds = entry.Value;

			foreach (var entry in iosOriginalScrollMetrics)
			{
				entry.Key.TopBottomSpacing = entry.Value.TopBottom;
				entry.Key.ItemSpacing = entry.Value.Item;
				entry.Key.ScrollbarWidth = entry.Value.Scrollbar;
				entry.Key.ScrollBar = entry.Value.Position;
			}
		}

		void ApplyIosLayout()
		{
			RestoreIosOriginalLayout();

			var viewport = Game.Renderer.Resolution;
			lastViewportSize = viewport;
			var snapshot = Platform.UsesMobileLayout ? IosScreenMetrics.SnapshotFor(viewport) : default;
			var layout = Platform.UsesMobileLayout ?
				IosSettingsLayout.ForSnapshot(true, snapshot) :
				IosSettingsLayout.ForPreview(Environment.GetEnvironmentVariable("NUKEHOUR_SETTINGS_PREVIEW"), viewport);
			lastIosNativePointSize = snapshot.NativePointSize;
			lastIosSafeBounds = snapshot.SafeBounds;
			settingsWidget.Bounds = ToWidgetBounds(layout.Window);

			var title = settingsWidget.Get<LabelWidget>("SETTINGS_LABEL_TITLE");
			var header = RelativeTo(layout.Header, layout.Window);
			title.Bounds = header;
			title.Align = TextAlign.Left;
			title.Font = "IosTitle";
			title.Visible = !layout.IsPhone;
			if (contextLabel != null)
			{
				title.Visible = false;
				contextLabel.Visible = !layout.IsPhone;
				contextLabel.Bounds = header;
				contextLabel.Align = TextAlign.Left;
				contextLabel.Font = "IosTitle";
			}

			tabContainer.Bounds = RelativeTo(layout.Tabs, layout.Window);
			tabTemplate.Font = "IosBold";
			for (var i = 0; i < buttons.Count; i++)
			{
				buttons[i].Bounds = RelativeTo(layout.TabBounds(i), layout.Tabs);
				buttons[i].Font = "IosBold";
			}

			panelContainer.Bounds = new WidgetBounds(0, 0, layout.Window.Width, layout.Window.Height);
			var contentWell = settingsWidget.GetOrNull("SETTINGS_CONTENT_WELL");
			if (contentWell != null)
			{
				contentWell.Bounds = RelativeTo(layout.Content, layout.Window);
				contentWell.Visible = layout.IsPhone;
			}
			foreach (var panel in panels.Keys)
			{
				var panelWidget = panelContainer.Get(panel);
				panelWidget.Bounds = RelativeTo(layout.Content, layout.Window);
				ApplyIosPanelContent(panelWidget, layout);
			}

			panelTemplate.Bounds = RelativeTo(layout.Content, layout.Window);

			var reset = settingsWidget.Get<ButtonWidget>("RESET_BUTTON");
			reset.Bounds = RelativeTo(layout.Reset, layout.Window);
			reset.Font = "IosBold";

			var back = settingsWidget.Get<ButtonWidget>("BACK_BUTTON");
			back.Bounds = RelativeTo(layout.Back, layout.Window);
			back.Font = "IosBold";
		}

		static IosSettingsLayout TouchLayoutFor(Size viewport) => Platform.UsesMobileLayout ?
			IosSettingsLayout.ForSnapshot(true, IosScreenMetrics.SnapshotFor(viewport)) :
			IosSettingsLayout.ForPreview(Environment.GetEnvironmentVariable("NUKEHOUR_SETTINGS_PREVIEW"), viewport);

		void ApplyDesktopLayout()
		{
			lastViewportSize = Game.Renderer.Resolution;
			foreach (var panelId in panels.Keys)
			{
				var panel = panelContainer.Get(panelId);
				if (panelId == "HOTKEYS_PANEL")
					ApplyDesktopHotkeyPanel(panel);
				foreach (var widget in DescendantsAndSelf(panel))
					ApplyDesktopFont(widget);

				var scroll = panel.GetOrNull<ScrollPanelWidget>("SETTINGS_SCROLLPANEL");
				if (scroll == null)
					continue;

				scroll.TopBottomSpacing = 8;
				scroll.ItemSpacing = 10;
				foreach (var row in scroll.Children)
				{
					var section = row.Id != null && row.Id.EndsWith("SECTION_HEADER", StringComparison.Ordinal);
					var sourceHeight = Math.Max(1, row.Bounds.Height);
					ScaleDesktopWidgetTree(row, section ? 36d / sourceHeight : 1.6);
					if (section)
						PrepareSectionHeader(row, "SettingsBold", 10);
					else
						row.Bounds.Height = Math.Max(row.Bounds.Height, VisibleContentHeight(row));
				}

				scroll.Layout.AdjustChildren();
			}
		}

		static void ApplyDesktopHotkeyPanel(Widget panel)
		{
			var width = panel.Bounds.Width;
			const int Gap = 8;
			var filterWidth = SettingsLabelWidth(panel, "FILTER_INPUT_LABEL", "SettingsBold", 90, Gap);
			var contextWidth = SettingsLabelWidth(panel, "CONTEXT_DROPDOWN_LABEL", "SettingsBold", 90, Gap);
			var fieldWidth = Math.Max(80, (width - filterWidth - contextWidth - 3 * Gap) / 2);
			SetBounds(panel.GetOrNull("FILTER_INPUT_LABEL"), new Rectangle(0, 0, filterWidth, 36));
			SetBounds(panel.GetOrNull("FILTER_INPUT"), new Rectangle(filterWidth + Gap, 0, fieldWidth, 36));
			SetBounds(panel.GetOrNull("CONTEXT_DROPDOWN_LABEL"), new Rectangle(width - fieldWidth - contextWidth - Gap, 0, contextWidth, 36));
			SetBounds(panel.GetOrNull("CONTEXT_DROPDOWN"), new Rectangle(width - fieldWidth, 0, fieldWidth, 36));
			var footer = new Rectangle(0, panel.Bounds.Height - 76, width, 76);
			var listBounds = new Rectangle(0, 48, width, Math.Max(1, footer.Top - 60));
			var list = panel.Get<ScrollPanelWidget>("HOTKEY_LIST");
			SetBounds(list, listBounds);
			LayoutIosHotkeyEmptyList(panel.GetOrNull("HOTKEY_EMPTY_LIST"), listBounds);
			SetBounds(panel.GetOrNull("HOTKEY_REMAP_BGND"), footer);
			SetBounds(panel.GetOrNull("HOTKEY_REMAP_DIALOG"), new Rectangle(0, 0, footer.Width, footer.Height));
			ApplyIosHotkeyFooter(panel, footer, IosSettingsLayout.ForPlatform(false));
			LayoutDesktopHotkeyList(list);
		}

		static void ScaleDesktopWidgetTree(Widget widget, double verticalScale)
		{
			widget.Bounds.Y = ScaleHorizontal(widget.Bounds.Y, verticalScale);
			widget.Bounds.Height = ScaleHorizontal(widget.Bounds.Height, verticalScale);
			if (IsTouchTarget(widget))
				widget.Bounds.Height = Math.Max(32, widget.Bounds.Height);
			ApplyDesktopFont(widget);
			foreach (var child in widget.Children)
				ScaleDesktopWidgetTree(child, verticalScale);
		}

		static void ApplyDesktopFont(Widget widget)
		{
			if (widget is DropDownButtonWidget dropdown)
				dropdown.PreparePanel = popup => PrepareSettingsDropDownPanel(popup, IosSettingsLayout.ForPlatform(false));

			if (widget is LabelWidget label)
				label.Font = label.Font != null && label.Font.Contains("Bold", StringComparison.Ordinal) ? "SettingsBold" : "SettingsRegular";
			else if (widget is ButtonWidget button)
				button.Font = "SettingsBold";
			else if (widget is TextFieldWidget textField)
				textField.Font = "SettingsRegular";
			else if (widget is HotkeyEntryWidget hotkeyEntry)
				hotkeyEntry.Font = "SettingsRegular";
		}

		static void PrepareSectionHeader(Widget section, string font, int inset)
		{
			var label = section.GetOrNull<LabelWidget>("LABEL");
			if (label == null)
				return;
			label.Align = TextAlign.Left;
			label.Font = font;
			label.Bounds = new WidgetBounds(inset, 0, Math.Max(1, section.Bounds.Width - 2 * inset), section.Bounds.Height);
		}

		void ApplyIosPanelContent(Widget panel, IosSettingsLayout layout)
		{
			ApplyIosVisibility(panel);

			var settingsScrollPanel = panel.GetOrNull<ScrollPanelWidget>("SETTINGS_SCROLLPANEL");
			if (settingsScrollPanel != null)
				ApplyIosSettingsScrollPanel(panel, settingsScrollPanel, layout);

			if (panel.Id == "HOTKEYS_PANEL")
				ApplyIosHotkeyPanel(panel, layout);

			ConfigureIosTouchWidgets(panel, layout);
		}

		static void ConfigureIosTouchWidgets(Widget widget, IosSettingsLayout layout)
		{
			if (widget is DropDownButtonWidget dropdown)
			{
				dropdown.PreparePanel = popup => PrepareSettingsDropDownPanel(popup, layout);
				dropdown.GetPopupSafeBounds = () => layout.SafeBounds;
			}

			if (widget is SliderWidget slider)
			{
				slider.TouchStep = IosTouchWidgetPolicy.StepForRange(
					slider.MinimumValue, slider.MaximumValue, slider.Ticks);
				slider.UseTouchStepControls = true;
				slider.TouchFont = "IosBold";
			}
			else if (widget is ScrollPanelWidget scrollPanel)
			{
				scrollPanel.EnableContentDragging = true;
				scrollPanel.ContentDragThreshold = IosTouchWidgetPolicy.ContentDragThreshold(layout.LogicalPerPoint);
			}

			foreach (var child in widget.Children)
				ConfigureIosTouchWidgets(child, layout);
		}

		static void PrepareSettingsDropDownPanel(Widget widget, IosSettingsLayout layout)
		{
			if (widget is not ScrollPanelWidget panel)
				return;

			var rowHeight = layout.Enabled ? layout.MinimumTarget : 32;
			var inset = layout.Enabled ? layout.Scale(8) : 8;
			var font = layout.Enabled ? "IosRegular" : "SettingsRegular";
			panel.Background = "settings-v2-control";
			panel.ScrollBarBackground = "settings-v2-scrollpanel";
			panel.Button = "settings-v2-control";
			panel.ScrollBar = layout.Enabled && layout.IsPhone ? ScrollBar.Hidden : ScrollBar.Right;
			panel.ScrollbarWidth = layout.Enabled ? layout.SettingsScrollbarWidth : 24;
			panel.TopBottomSpacing = 3;
			panel.ItemSpacing = 0;
			panel.EnableContentDragging = layout.Enabled;
			if (layout.Enabled)
			{
				panel.ContentDragThreshold = IosTouchWidgetPolicy.ContentDragThreshold(layout.LogicalPerPoint);
				panel.Bounds.Width = Math.Min(panel.Bounds.Width, layout.SafeBounds.Width);
			}

			foreach (var item in panel.Children.OfType<ScrollItemWidget>())
			{
				item.SetBackground("settings-v2-control");
				item.Font = font;
				item.Bounds.X = 3;
				item.Bounds.Width = Math.Max(1, panel.Bounds.Width - panel.ScrollbarWidth - 6);
				item.Bounds.Height = Math.Max(rowHeight, item.Bounds.Height);
				foreach (var label in item.Children.OfType<LabelWidget>())
				{
					label.Font = font;
					label.Bounds = new WidgetBounds(inset, 0, Math.Max(1, item.Bounds.Width - 2 * inset), item.Bounds.Height);
				}
			}

			panel.Layout.AdjustChildren();
			panel.Bounds.Height = Math.Min(panel.ContentHeight, Math.Max(panel.Bounds.Height, 3 * rowHeight + 6));
		}

		static void ApplyIosVisibility(Widget panel) => ApplyPlatformVisibility(panel, true);

		static void ApplyPlatformVisibility(Widget panel, bool isIos)
		{
			foreach (var widget in DescendantsAndSelf(panel))
			{
				if (IosSettingsVisibilityPolicy.ShouldShowContainer(isIos, widget.Id))
					continue;

				widget.Visible = false;
				widget.IsVisible = () => false;
			}

			var scrollPanel = panel.GetOrNull<ScrollPanelWidget>("SETTINGS_SCROLLPANEL");
			if (scrollPanel == null)
				return;

			foreach (var row in scrollPanel.Children)
			{
				if (IosSettingsVisibilityPolicy.ShouldCollapseRow(isIos, row.Id))
				{
					row.Visible = false;
					row.IsVisible = () => false;
					continue;
				}

				var columns = row.Children.Where(ContainsTouchTarget).ToArray();
				if (columns.Length == 0 || columns.Any(column => column.IsVisible()))
					continue;

				row.Visible = false;
				row.IsVisible = () => false;
			}

			scrollPanel.Layout.AdjustChildren();
		}

		static IEnumerable<Widget> DescendantsAndSelf(Widget widget)
		{
			yield return widget;
			foreach (var child in widget.Children)
				foreach (var descendant in DescendantsAndSelf(child))
					yield return descendant;
		}

		void ApplyIosSettingsScrollPanel(Widget panel, ScrollPanelWidget scrollPanel, IosSettingsLayout layout)
		{
			if (layout.IsPhone && panel.Id == "DISPLAY_PANEL")
			{
				var profileHeader = scrollPanel.Children.FindIndex(c => c.Id == "PROFILE_SECTION_HEADER");
				var displayHeader = scrollPanel.Children.FindIndex(c => c.Id == "DISPLAY_SECTION_HEADER");
				if (profileHeader >= 0 && displayHeader > profileHeader)
				{
					var profile = scrollPanel.Children.GetRange(profileHeader, displayHeader - profileHeader);
					scrollPanel.Children.RemoveRange(profileHeader, profile.Count);
					scrollPanel.Children.AddRange(profile);
				}
			}
			var sourceScrollPanel = OriginalBounds(scrollPanel);
			var originalScrollbarWidth = iosOriginalScrollMetrics.TryGetValue(scrollPanel, out var originalMetrics) ?
				originalMetrics.Scrollbar : scrollPanel.ScrollbarWidth;
			var horizontalScale = layout.ScrollContentHorizontalScale(
				sourceScrollPanel.Width, originalScrollbarWidth, panel.Bounds.Width);
			scrollPanel.Bounds = new WidgetBounds(0, 0, panel.Bounds.Width, panel.Bounds.Height);
			scrollPanel.ScrollbarWidth = layout.SettingsScrollbarWidth;
			scrollPanel.ScrollBar = layout.IsPhone ? ScrollBar.Hidden : originalMetrics.Position;
			scrollPanel.TopBottomSpacing = layout.Scale(4);
			scrollPanel.ItemSpacing = layout.Scale(6);
			if (layout.IsPhone && panel.Id == "AUDIO_PANEL")
			{
				MobileAudioSettingsLayout.Apply(scrollPanel, layout);
				return;
			}

			foreach (var item in scrollPanel.Children)
			{
				var source = OriginalBounds(item);
				if (source.Height <= 0 || (layout.IsPhone && item.Id?.Contains("SPACER", StringComparison.Ordinal) == true))
				{
					item.Bounds = new WidgetBounds(
						ScaleHorizontal(source.X, horizontalScale), source.Y,
						ScaleHorizontal(source.Width, horizontalScale), layout.Scale(8));
					continue;
				}

				var minimumTouchHeight = MinimumOriginalTouchHeight(item);
				var isSectionHeader = item.Id != null && item.Id.EndsWith("SECTION_HEADER", StringComparison.Ordinal);
				var verticalScale = layout.RowVerticalScale(source.Height, minimumTouchHeight, isSectionHeader);
				ScaleWidgetTreeFromOriginal(item, layout, horizontalScale, verticalScale, false);
				ReflowIosVisibleColumns(item, layout);
				if (layout.IsPhone && item.Children.Any(IsTouchTarget))
					CompactPhoneSettingColumn(item, layout);
				if (isSectionHeader)
					PrepareSectionHeader(item, "IosBold", layout.Scale(10));
			}

			if (!layout.IsPhone && scrollPanel.Layout is PhoneSettingsRowsLayout)
				scrollPanel.Layout = new ListLayout(scrollPanel);
			scrollPanel.Layout.AdjustChildren();
			if (layout.IsPhone) PackPhoneSingleRows(scrollPanel, layout);
		}

		sealed class PhoneSettingsRowsLayout : ILayout
		{
			readonly ScrollPanelWidget scroll;
			readonly IosSettingsLayout layout;
			readonly ListLayout list;
			public PhoneSettingsRowsLayout(ScrollPanelWidget scroll, IosSettingsLayout layout)
			{
				this.scroll = scroll;
				this.layout = layout;
				list = new ListLayout(scroll);
			}
			public void AdjustChild(Widget child) => list.AdjustChild(child);
			public void AdjustChildren()
			{
				list.AdjustChildren();
				PackPhoneSingleRows(scroll, layout);
			}
		}

		internal static void PackPhoneSingleRows(ScrollPanelWidget scroll, IosSettingsLayout layout)
		{
			scroll.Layout = new PhoneSettingsRowsLayout(scroll, layout);
			var gap = layout.Scale(10);
			var width = scroll.Bounds.Width;
			var y = scroll.TopBottomSpacing;
			Widget pending = null;
			foreach (var row in scroll.Children.Where(c => c.IsVisible()))
			{
				var cells = row.Children.Where(c => c is ContainerWidget && c.IsVisible() && ContainsTouchTarget(c)).ToArray();
				var direct = row.Children.Count(c => c.IsVisible() && IsTouchTarget(c));
				var single = row is ContainerWidget && (direct == 1 || (direct == 0 && cells.Length == 1));
				if (!single)
				{
					if (pending != null) { y += pending.Bounds.Height + gap; pending = null; }
					row.Bounds.Y = y;
					y += row.Bounds.Height + scroll.ItemSpacing;
					continue;
				}

				ResizeColumnHorizontally(row, pending == null ? 0 : (width + gap) / 2, (width - gap) / 2);
				var cell = direct == 1 ? row : cells[0];
				if (cell != row) ResizeColumnHorizontally(cell, 0, row.Bounds.Width);
				CompactPhoneSettingColumn(cell, layout);
				row.Bounds.Y = y;
				row.Bounds.Height = cell.Bounds.Height;
				if (pending == null) pending = row;
				else { y += Math.Max(pending.Bounds.Height, row.Bounds.Height) + gap; pending = null; }
			}
			if (pending != null) y += pending.Bounds.Height + gap;
			scroll.ContentHeight = y + scroll.TopBottomSpacing;
		}

		void ScaleWidgetTreeFromOriginal(
			Widget widget, IosSettingsLayout layout, double horizontalScale, double verticalScale, bool scalePosition)
		{
			var bounds = OriginalBounds(widget);
			var transformed = layout.TransformBoundsFromOriginal(
				bounds.ToRectangle(), horizontalScale, verticalScale, scalePosition, IsTouchTarget(widget));
			widget.Bounds = ToWidgetBounds(transformed);
			ApplyIosFont(widget);

			foreach (var child in widget.Children)
				ScaleWidgetTreeFromOriginal(child, layout, horizontalScale, verticalScale, true);
		}

		WidgetBounds OriginalBounds(Widget widget) =>
			iosOriginalBounds.TryGetValue(widget, out var bounds) ? bounds : widget.Bounds;

		int MinimumOriginalTouchHeight(Widget widget)
		{
			if (!widget.IsVisible())
				return 0;

			var minimum = IsTouchTarget(widget) ? OriginalBounds(widget).Height : 0;
			foreach (var child in widget.Children)
			{
				var childMinimum = MinimumOriginalTouchHeight(child);
				if (childMinimum > 0 && (minimum == 0 || childMinimum < minimum))
					minimum = childMinimum;
			}

			return minimum;
		}

		static void ReflowIosVisibleColumns(Widget row, IosSettingsLayout layout)
		{
			var columns = row.Children.Where(column => column is ContainerWidget && column.IsVisible() && ContainsTouchTarget(column)).ToArray();
			if (columns.Length == 0)
				return;

			var inset = layout.Scale(10);
			var gap = layout.Scale(10);
			if (layout.IsPhone)
			{
				const int columnsPerRow = 2;
				var y = 0;
				for (var start = 0; start < columns.Length; start += columnsPerRow)
				{
					var rowCount = Math.Min(columnsPerRow, columns.Length - start);
					var rowHeight = 0;
					for (var i = 0; i < rowCount; i++)
					{
						var column = columns[start + i];
						var target = IosSettingsVisibilityPolicy.ColumnBounds(
							row.Bounds.Width, rowCount, i, inset, gap);
						ResizeColumnHorizontally(column, target.X, target.Width);
						CompactPhoneSettingColumn(column, layout);
						column.Bounds.Y = y;
						column.Bounds.Height = Math.Max(layout.MinimumTarget, VisibleContentHeight(column));
						rowHeight = Math.Max(rowHeight, column.Bounds.Height);
					}

					y += rowHeight + gap;
				}

				row.Bounds.Height = Math.Max(layout.MinimumTarget, y - gap);
				return;
			}

			// Some desktop rows have two stacked controls on the left and a slider on the right.
			// Preserve that grouping on tablets instead of treating each control as a third column.
			if (columns.Length > 2)
				return;

			for (var i = 0; i < columns.Length; i++)
			{
				var target = IosSettingsVisibilityPolicy.ColumnBounds(
					row.Bounds.Width, columns.Length, i, inset, gap);
				ResizeColumnHorizontally(columns[i], target.X, target.Width);
			}
		}

		internal static void CompactPhoneSettingColumn(Widget column, IosSettingsLayout layout)
		{
			var controls = column.Children.Where(c => c.IsVisible() && IsTouchTarget(c)).ToArray();
			var labels = column.Children.OfType<LabelWidget>().Where(c => c.IsVisible()).ToArray();
			if (controls.Length != 1 || labels.Length > 1) return;
			var control = controls[0];
			if (control is CheckboxWidget && labels.Length == 1)
			{
				control.Bounds = new WidgetBounds(0, 0, column.Bounds.Width, layout.MinimumTarget);
				labels[0].Bounds = new WidgetBounds(layout.Scale(8), layout.MinimumTarget,
					column.Bounds.Width - layout.Scale(16), layout.Scale(24));
				labels[0].WordWrap = true;
				var font = Game.Renderer.Fonts[labels[0].Font];
				var wrapped = WidgetUtils.WrapText(labels[0].GetText(), labels[0].Bounds.Width, font);
				labels[0].Bounds.Height = Math.Max(layout.Scale(24), font.Measure(wrapped).Y);
				column.Bounds.Height = labels[0].Bounds.Bottom;
				return;
			}
			var labelWidth = labels.Length == 0 ? 0 : column.Bounds.Width * 42 / 100;
			var inset = labels.Length == 0 ? 0 : layout.Scale(6);
			control.Bounds = new WidgetBounds(labelWidth + inset, 0,
				Math.Max(layout.MinimumTarget, column.Bounds.Width - labelWidth - inset), layout.MinimumTarget);
			if (labels.Length == 1)
			{
				labels[0].Bounds = new WidgetBounds(0, 0, labelWidth, layout.MinimumTarget);
				labels[0].WordWrap = true;
			}
			column.Bounds.Height = layout.MinimumTarget;
			// The color swatch is relative to its resized dropdown, not the old desktop cell.
			var swatch = control.GetOrNull("COLORBLOCK");
			if (swatch != null)
				swatch.Bounds = new WidgetBounds(layout.Scale(5), layout.Scale(6),
					Math.Max(1, control.Bounds.Width - layout.Scale(35)), Math.Max(1, control.Bounds.Height - layout.Scale(12)));
		}

		static int VisibleContentHeight(Widget widget) => Math.Max(widget.Bounds.Height,
			widget.Children.Where(child => child.IsVisible())
				.Select(child => child.Bounds.Y + VisibleContentHeight(child)).DefaultIfEmpty(0).Max());

		static void ResizeColumnHorizontally(Widget column, int x, int width)
		{
			var sourceWidth = Math.Max(1, column.Bounds.Width);
			var scale = width / (double)sourceWidth;
			column.Bounds = new WidgetBounds(x, column.Bounds.Y, width, column.Bounds.Height);
			foreach (var child in column.Children)
				ScaleCurrentHorizontal(child, scale);
		}

		static void ScaleCurrentHorizontal(Widget widget, double scale)
		{
			widget.Bounds = new WidgetBounds(
				ScaleHorizontal(widget.Bounds.X, scale), widget.Bounds.Y,
				ScaleHorizontal(widget.Bounds.Width, scale), widget.Bounds.Height);

			foreach (var child in widget.Children)
				ScaleCurrentHorizontal(child, scale);
		}

		static bool ContainsTouchTarget(Widget widget)
		{
			if (IsTouchTarget(widget))
				return true;

			return widget.Children.Any(ContainsTouchTarget);
		}

		static bool IsTouchTarget(Widget widget) =>
			widget is ButtonWidget || widget is TextFieldWidget || widget is SliderWidget || widget is HotkeyEntryWidget;

		static void ApplyIosFont(Widget widget)
		{
			if (widget is LabelWidget label)
				label.Font = label.Font != null && label.Font.Contains("Bold", StringComparison.Ordinal) ? "IosBold" : "IosRegular";
			else if (widget is ButtonWidget button)
				button.Font = "IosBold";
			else if (widget is TextFieldWidget textField)
				textField.Font = "IosRegular";
			else if (widget is HotkeyEntryWidget hotkeyEntry)
				hotkeyEntry.Font = "IosRegular";
		}

		void ApplyIosHotkeyPanel(Widget panel, IosSettingsLayout layout)
		{
			var header = RelativeRectangle(layout.HotkeyHeader, layout.Content);
			var list = RelativeRectangle(layout.HotkeyList, layout.Content);
			var footer = RelativeRectangle(layout.HotkeyFooter, layout.Content);
			var gap = layout.Scale(4);
			var filterLabelWidth = SettingsLabelWidth(panel, "FILTER_INPUT_LABEL", "IosBold", layout.Scale(70), gap);
			var contextLabelWidth = SettingsLabelWidth(panel, "CONTEXT_DROPDOWN_LABEL", "IosBold", layout.Scale(90), gap);
			var fieldWidth = Math.Max(layout.MinimumTarget,
				(header.Width - filterLabelWidth - contextLabelWidth - 3 * gap) / 2);

			SetBounds(panel.GetOrNull("FILTER_INPUT_LABEL"),
				new Rectangle(header.X, header.Y, filterLabelWidth, header.Height));
			SetBounds(panel.GetOrNull("FILTER_INPUT"),
				new Rectangle(header.X + filterLabelWidth + gap, header.Y, fieldWidth, header.Height));
			var contextLabelX = header.Right - contextLabelWidth - gap - fieldWidth;
			SetBounds(panel.GetOrNull("CONTEXT_DROPDOWN_LABEL"),
				new Rectangle(contextLabelX, header.Y, contextLabelWidth, header.Height));
			SetBounds(panel.GetOrNull("CONTEXT_DROPDOWN"),
				new Rectangle(header.Right - fieldWidth, header.Y, fieldWidth, header.Height));

			var hotkeyList = panel.Get<ScrollPanelWidget>("HOTKEY_LIST");
			hotkeyList.Bounds = ToWidgetBounds(list);
			var originalScrollBar = iosOriginalScrollMetrics.TryGetValue(hotkeyList, out var originalMetrics) ?
				originalMetrics.Position : hotkeyList.ScrollBar;
			hotkeyList.ScrollBar = layout.HotkeyScrollbarVisible ? originalScrollBar : ScrollBar.Hidden;
			hotkeyList.ScrollbarWidth = layout.HotkeyScrollbarWidth;
			hotkeyList.TopBottomSpacing = layout.Scale(4);
			hotkeyList.ItemSpacing = layout.Scale(6);
			LayoutIosHotkeyEmptyList(panel.GetOrNull("HOTKEY_EMPTY_LIST"), list);
			ApplyIosHotkeyListChildren(hotkeyList, layout);

			var remapBackground = panel.GetOrNull("HOTKEY_REMAP_BGND");
			SetBounds(remapBackground, footer);
			SetBounds(panel.GetOrNull("HOTKEY_REMAP_DIALOG"), new Rectangle(0, 0, footer.Width, footer.Height));
			ApplyIosHotkeyFooter(panel, footer, layout);

			foreach (var child in panel.Children)
				ApplyIosFontTree(child);
		}

		static void LayoutIosHotkeyEmptyList(Widget emptyList, Rectangle bounds)
		{
			if (emptyList == null)
				return;

			SetBounds(emptyList, bounds);
			SetBounds(emptyList.GetOrNull("HOTKEY_EMPTY_LIST_MESSAGE"),
				new Rectangle(0, 0, bounds.Width, bounds.Height));
		}

		static void ApplyIosHotkeyFooter(Widget panel, Rectangle footer, IosSettingsLayout layout)
		{
			var gap = layout.Scale(4);
			var labelWidth = Math.Max(layout.MinimumTarget, footer.Width / 5);
			var buttonsWidth = Math.Max(3 * layout.MinimumTarget + 2 * gap, footer.Width / 3);
			buttonsWidth = Math.Min(buttonsWidth, footer.Width - labelWidth - 3 * gap - layout.MinimumTarget);
			var buttonStart = footer.Width - buttonsWidth;
			var entryX = labelWidth + gap;
			var entryWidth = Math.Max(layout.MinimumTarget, buttonStart - gap - entryX);
			SetBounds(panel.GetOrNull("HOTKEY_LABEL"),
				new Rectangle(0, 0, labelWidth, layout.MinimumTarget));
			SetBounds(panel.GetOrNull("HOTKEY_ENTRY"),
				new Rectangle(entryX, 0, entryWidth, layout.MinimumTarget));
			SetBounds(panel.GetOrNull("NOTICES"),
				new Rectangle(entryX, layout.MinimumTarget, entryWidth, Math.Max(1, footer.Height - layout.MinimumTarget)));

			var buttonWidth = (buttonsWidth - 2 * gap) / 3;
			SetBounds(panel.GetOrNull("OVERRIDE_HOTKEY_BUTTON"),
				new Rectangle(buttonStart, 0, buttonWidth, layout.HotkeyActionHeight));
			SetBounds(panel.GetOrNull("CLEAR_HOTKEY_BUTTON"),
				new Rectangle(buttonStart + buttonWidth + gap, 0, buttonWidth, layout.HotkeyActionHeight));
			SetBounds(panel.GetOrNull("RESET_HOTKEY_BUTTON"),
				Rectangle.FromLTRB(buttonStart + 2 * (buttonWidth + gap), 0, footer.Width, layout.HotkeyActionHeight));
		}

		static void ApplyIosHotkeyListChildren(ScrollPanelWidget list, IosSettingsLayout layout)
		{
			var gap = layout.Scale(6);
			var columns = layout.HotkeyColumns;
			var itemWidth = layout.HotkeyItemWidth(list.Bounds.Width);
			var availableWidth = columns * itemWidth + (columns - 1) * gap;
			var y = list.TopBottomSpacing;
			var column = 0;
			var rowHeight = 0;

			foreach (var item in list.Children)
			{
				if (item.Id == "HEADER")
				{
					if (column != 0)
					{
						y += rowHeight + gap;
						column = 0;
						rowHeight = 0;
					}

					item.Bounds = new WidgetBounds(0, y, availableWidth, layout.SectionHeaderHeight);
					PrepareHotkeyHeader(item, availableWidth, layout.SectionHeaderHeight);
					y += layout.SectionHeaderHeight + gap;
					continue;
				}

				item.Bounds = new WidgetBounds(column * (itemWidth + gap), y, itemWidth, layout.MinimumTarget);
				PrepareHotkeyItem(item, itemWidth, layout.MinimumTarget, layout);
				rowHeight = Math.Max(rowHeight, item.Bounds.Height);
				column++;
				if (column == columns)
				{
					y += rowHeight + gap;
					column = 0;
					rowHeight = 0;
				}
			}

			if (column != 0)
				y += rowHeight + gap;

			list.ContentHeight = y + list.TopBottomSpacing;
		}

		internal static void LayoutIosHotkeyList(ScrollPanelWidget list)
		{
			var layout = TouchLayoutFor(Game.Renderer.Resolution);
			if (!layout.Enabled)
			{
				LayoutDesktopHotkeyList(list);
				return;
			}

			ApplyIosHotkeyListChildren(list, layout);
		}

		internal static void PrepareIosHotkeyTemplates(Widget headerTemplate, Widget itemTemplate)
		{
			var layout = TouchLayoutFor(Game.Renderer.Resolution);
			if (!layout.Enabled)
			{
				PrepareDesktopHotkeyItem(headerTemplate, headerTemplate.Bounds.Width, true);
				PrepareDesktopHotkeyItem(itemTemplate, itemTemplate.Bounds.Width, false);
				return;
			}

			var gap = layout.Scale(6);
			var columns = layout.HotkeyColumns;
			var itemWidth = layout.HotkeyItemWidth(layout.Content.Width);
			var availableWidth = columns * itemWidth + (columns - 1) * gap;
			headerTemplate.Bounds = new WidgetBounds(0, 0, availableWidth, layout.SectionHeaderHeight);
			itemTemplate.Bounds = new WidgetBounds(0, 0, itemWidth, layout.MinimumTarget);
			PrepareHotkeyHeader(headerTemplate, availableWidth, layout.SectionHeaderHeight);
			PrepareHotkeyItem(itemTemplate, itemWidth, layout.MinimumTarget, layout);
		}

		static void LayoutDesktopHotkeyList(ScrollPanelWidget list)
		{
			const int Gap = 8;
			var width = Math.Max(1, list.Bounds.Width - list.ScrollbarWidth - Gap);
			var itemWidth = (width - Gap) / 2;
			var y = list.TopBottomSpacing;
			var column = 0;
			var rowHeight = 0;
			foreach (var item in list.Children)
			{
				var header = item.Id == "HEADER";
				if (header && column != 0)
				{
					y += rowHeight + Gap;
					column = 0;
					rowHeight = 0;
				}

				PrepareDesktopHotkeyItem(item, header ? width : itemWidth, header);
				item.Bounds.X = column * (itemWidth + Gap);
				item.Bounds.Y = y;
				rowHeight = Math.Max(rowHeight, item.Bounds.Height);
				if (header || ++column == 2)
				{
					y += rowHeight + Gap;
					column = 0;
					rowHeight = 0;
				}
			}

			list.ContentHeight = y + (column == 0 ? 0 : rowHeight + Gap) + list.TopBottomSpacing;
		}

		static void PrepareDesktopHotkeyItem(Widget item, int width, bool header)
		{
			ApplySettingsVisualStyle(item);
			item.Bounds.Width = width;
			item.Bounds.Height = 36;
			if (header)
			{
				SetBounds(item.GetOrNull("BACKGROUND"), new Rectangle(0, 0, width, 36));
				PrepareSectionHeader(item, "SettingsBold", 10);
			}
			else
			{
				var buttonWidth = width * 2 / 5;
				SetBounds(item.GetOrNull("FUNCTION"), new Rectangle(0, 0, width - buttonWidth - 8, 36));
				SetBounds(item.GetOrNull("HOTKEY"), new Rectangle(width - buttonWidth, 0, buttonWidth, 36));
			}

			foreach (var widget in DescendantsAndSelf(item))
				ApplyDesktopFont(widget);
			if (!header)
				PrepareHotkeyFunctionLabel(item, 8);
		}

		static void PrepareHotkeyHeader(Widget header, int width, int height)
		{
			SetBounds(header.GetOrNull("BACKGROUND"), new Rectangle(0, 0, width, height));
			SetBounds(header.GetOrNull("LABEL"), new Rectangle(0, 0, width, height));
			ApplyIosFontTree(header);
			PrepareSectionHeader(header, "IosBold", 10);
		}

		static void PrepareHotkeyItem(Widget item, int width, int height, IosSettingsLayout layout)
		{
			ApplySettingsVisualStyle(item);
			var gap = layout.Scale(4);
			var buttonWidth = Math.Max(layout.MinimumTarget, width * 2 / 5);
			SetBounds(item.GetOrNull("FUNCTION"),
				new Rectangle(0, 0, Math.Max(1, width - buttonWidth - gap), height));
			SetBounds(item.GetOrNull("HOTKEY"),
				new Rectangle(width - buttonWidth, 0, buttonWidth, height));
			ApplyIosFontTree(item);
			PrepareHotkeyFunctionLabel(item, layout.Scale(8));
		}

		static void PrepareHotkeyFunctionLabel(Widget item, int padding)
		{
			var label = item.GetOrNull<LabelWidget>("FUNCTION");
			if (label == null)
				return;
			label.Align = TextAlign.Left;
			BindWrappedSettingsLabel(label);
			var textHeight = Game.Renderer.Fonts[label.Font].Measure(label.GetText()).Y + padding;
			item.Bounds.Height = Math.Max(item.Bounds.Height, textHeight);
			label.Bounds.Height = item.Bounds.Height;
			var button = item.GetOrNull("HOTKEY");
			if (button != null)
				button.Bounds.Y = (item.Bounds.Height - button.Bounds.Height) / 2;
		}

		static int SettingsLabelWidth(Widget panel, string id, string font, int minimum, int padding)
		{
			var label = panel.Get<LabelWidget>(id);
			label.Font = font;
			return Math.Max(minimum, Game.Renderer.Fonts[font].Measure(label.GetText()).X + padding);
		}

		static void BindWrappedSettingsLabel(LabelWidget label)
		{
			var original = UnwrappedLabelText.GetValue(label, source => source.GetText);
			var cache = new CachedTransform<(string Text, int Width, string Font), string>(value =>
				WrapSettingsText(value.Text, value.Width, text => Game.Renderer.Fonts[value.Font].Measure(text).X));
			// The engine's general word wrapper only splits ASCII spaces. Keep this
			// multilingual fallback local to settings and cache measurements between draws.
			label.WordWrap = false;
			label.GetText = () => cache.Update((original(), label.Bounds.Width, label.Font));
		}

		public static string WrapSettingsText(string text, int width, Func<string, int> measure)
		{
			if (string.IsNullOrEmpty(text))
				return text ?? "";

			var lines = new List<string>();
			var line = "";
			var elements = StringInfo.GetTextElementEnumerator(text);
			while (elements.MoveNext())
			{
				var element = elements.GetTextElement();
				if (element == "\n")
				{
					lines.Add(line.TrimEnd());
					line = "";
					continue;
				}

				if (line.Length > 0 && measure(line + element) > width)
				{
					var space = line.LastIndexOf(' ');
					if (!string.IsNullOrWhiteSpace(element) && space > 0)
					{
						lines.Add(line[..space]);
						line = line[(space + 1)..];
					}
					else
					{
						lines.Add(line.TrimEnd());
						line = "";
					}
				}

				if (line.Length != 0 || !string.IsNullOrWhiteSpace(element))
					line += element;
			}

			lines.Add(line.TrimEnd());
			return string.Join("\n", lines);
		}

		static void ApplyIosFontTree(Widget widget)
		{
			if (widget == null)
				return;

			ApplyIosFont(widget);
			foreach (var child in widget.Children)
				ApplyIosFontTree(child);
		}

		static void SetBounds(Widget widget, Rectangle bounds)
		{
			if (widget != null)
				widget.Bounds = ToWidgetBounds(bounds);
		}

		static WidgetBounds RelativeTo(Rectangle bounds, Rectangle parent) =>
			new(bounds.X - parent.X, bounds.Y - parent.Y, bounds.Width, bounds.Height);

		static Rectangle RelativeRectangle(Rectangle bounds, Rectangle parent) =>
			new(bounds.X - parent.X, bounds.Y - parent.Y, bounds.Width, bounds.Height);

		static WidgetBounds ToWidgetBounds(Rectangle bounds) =>
			new(bounds.X, bounds.Y, bounds.Width, bounds.Height);

		static int ScaleHorizontal(int value, double scale) =>
			(int)Math.Round(value * scale, MidpointRounding.AwayFromZero);

		public override void Tick()
		{
			var viewport = Game.Renderer.Resolution;
			if (!Platform.UsesMobileLayout)
			{
				if (viewport != lastViewportSize && TouchLayoutFor(viewport).Enabled)
					ApplyIosLayout();

				return;
			}

			var snapshot = IosScreenMetrics.SnapshotFor(viewport);
			if (NeedsIosRelayout(viewport, lastViewportSize, snapshot,
				lastIosNativePointSize, lastIosSafeBounds))
				ApplyIosLayout();
		}

		public static bool NeedsIosRelayout(
			Size viewport, Size previousViewport, IosScreenSnapshot snapshot,
			Size previousNativePointSize, Rectangle previousSafeBounds) =>
			viewport != previousViewport || snapshot.NativePointSize != previousNativePointSize ||
			snapshot.SafeBounds != previousSafeBounds;

		public void RegisterSettingsPanel(string panelID, string label, Func<Widget, Func<bool>> init, Func<Widget, Action> reset)
		{
			var panel = panelContainer.Get(panelID);

			activePanel ??= panelID;

			panel.IsVisible = () => activePanel == panelID;

			leavePanelActions.Add(panelID, init(panel));
			resetPanelActions.Add(panelID, reset(panel));

			AddSettingsTab(panelID, label);
		}

		ButtonWidget AddSettingsTab(string id, string label)
		{
			var tab = tabTemplate.Clone() as ButtonWidget;
			var lastButton = buttons.LastOrDefault();
			if (lastButton != null)
			{
				tab.Bounds.X = lastButton.Bounds.X + buttonStride.X;
				tab.Bounds.Y = lastButton.Bounds.Y + buttonStride.Y;
			}

			tab.Id = id;
			// Panel labels in chrome YAML are Fluent keys (e.g. button-settings-tab-display).
			var resolvedLabel = FluentProvider.GetMessage(
				TouchLayoutFor(Game.Renderer.Resolution).Enabled && id == "INPUT_PANEL" ? "button-settings-tab-touch" : label);
			tab.GetText = () => resolvedLabel;
			tab.IsHighlighted = () => activePanel == id;
			tab.OnClick = () =>
			{
				needsRestart |= leavePanelActions[activePanel]();
				Game.Settings.Save();
				activePanel = id;
			};

			tabContainer.AddChild(tab);
			buttons.Add(tab);

			return tab;
		}
	}
}
