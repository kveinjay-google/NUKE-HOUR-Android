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
using System.Linq;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Traits;
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Mods.Common.Widgets.Logic
{
	public class IntroductionPromptLogic : ChromeLogic
	{
		// Increment the version number when adding new stats
		const int IntroductionVersion = 1;

		[FluentReference]
		const string Classic = "options-control-scheme.classic";

		[FluentReference]
		const string Modern = "options-control-scheme.modern";

		readonly string classic;
		readonly string modern;

		public static bool ShouldShowPrompt()
		{
			return ShouldShowPrompt(Game.ModData.Manifest.Id, Game.Settings.Game.IntroductionPromptVersion);
		}

		public static bool ShouldShowPrompt(string modId, int completedVersion) =>
			modId != "ra2" && completedVersion < IntroductionVersion;

		static bool ApplyNukeHourDesktopPresentation(Widget widget, ModData modData)
		{
			if (Platform.UsesMobileLayout || modData.Manifest.Id != "ra2")
				return false;

			const int MaximumWidth = 860;
			const int MaximumHeight = 520;
			const int Inset = 32;
			const int ColumnGap = 28;
			const int HeaderHeight = 24;
			const int LabelHeight = 20;
			const int ControlHeight = 38;
			const int RowGap = 12;

			var resolution = Game.Renderer.Resolution;
			var promptWidth = Math.Max(1, Math.Min(MaximumWidth, resolution.Width - 80));
			var promptHeight = Math.Max(1, Math.Min(MaximumHeight, resolution.Height - 70));
			var panelX = (resolution.Width - promptWidth) / 2;
			var panelY = (resolution.Height - promptHeight) / 2;
			var prompt = (BackgroundWidget)widget;
			prompt.Bounds = new WidgetBounds(0, 0, resolution.Width, resolution.Height);
			prompt.Background = "cc-introduction-clean-background";

			var panel = widget.Get<BackgroundWidget>("NUKE_HOUR_DESKTOP_PANEL");
			panel.Bounds = new WidgetBounds(panelX, panelY, promptWidth, promptHeight);
			panel.Background = "settings-v2-panel";
			panel.IsVisible = () => true;

			var title = widget.Get<LabelWidget>("PROMPT_TITLE");
			title.Bounds = new WidgetBounds(panelX + Inset, panelY + 24, promptWidth - 2 * Inset, 34);
			title.Font = "SettingsTitle";
			title.Align = TextAlign.Left;

			var descriptionA = widget.Get<LabelWidget>("DESC_A");
			descriptionA.Bounds = new WidgetBounds(panelX + Inset, panelY + 61, promptWidth - 2 * Inset, 22);
			descriptionA.Font = "SettingsRegular";
			descriptionA.Align = TextAlign.Left;

			var descriptionB = widget.Get<LabelWidget>("DESC_B");
			descriptionB.IsVisible = () => false;

			var scroll = widget.Get<ScrollPanelWidget>("SETTINGS_SCROLLPANEL");
			scroll.Bounds = new WidgetBounds(panelX + Inset, panelY + 98, promptWidth - 2 * Inset, promptHeight - 176);
			scroll.EnableContentDragging = false;
			var columnWidth = (scroll.Bounds.Width - ColumnGap) / 2;
			var rightColumn = columnWidth + ColumnGap;

			foreach (var id in new[] { "PROFILE_SECTION_HEADER", "INPUT_SECTION_HEADER", "DISPLAY_SECTION_HEADER" })
			{
				var header = widget.Get<BackgroundWidget>(id);
				header.Background = "cc-mp-surface";
				var label = header.Get<LabelWidget>("LABEL");
				label.Font = "SettingsBold";
				label.Align = TextAlign.Left;
				label.Bounds = new WidgetBounds(10, 0, Math.Max(1, columnWidth - 20), HeaderHeight);
			}

			var profileHeader = widget.Get("PROFILE_SECTION_HEADER");
			profileHeader.Bounds = new WidgetBounds(0, 0, columnWidth, HeaderHeight);
			var inputHeader = widget.Get("INPUT_SECTION_HEADER");
			inputHeader.Bounds = new WidgetBounds(0, 116, columnWidth, HeaderHeight);
			var displayHeader = widget.Get("DISPLAY_SECTION_HEADER");
			displayHeader.Bounds = new WidgetBounds(rightColumn, 0, columnWidth, HeaderHeight);

			var playerContainer = widget.Get("PLAYER_CONTAINER");
			var colorContainer = widget.Get("PLAYERCOLOR_CONTAINER");
			playerContainer.Parent.Bounds = new WidgetBounds(0, HeaderHeight + RowGap, columnWidth, 68);
			var colorWidth = Math.Min(116, columnWidth / 3);
			playerContainer.Bounds = new WidgetBounds(0, 0, columnWidth - colorWidth - RowGap, 68);
			colorContainer.Bounds = new WidgetBounds(columnWidth - colorWidth, 0, colorWidth, 68);
			LayoutLabeledControl(playerContainer, "PLAYER", "PLAYERNAME", LabelHeight, ControlHeight);
			LayoutLabeledControl(colorContainer, "COLOR", "PLAYERCOLOR", LabelHeight, ControlHeight);

			var nameTextfield = widget.Get<TextFieldWidget>("PLAYERNAME");
			nameTextfield.Background = "cc-mp-field";
			var colorBlock = widget.Get<ColorBlockWidget>("COLORBLOCK");
			colorBlock.Bounds = new WidgetBounds(9, 8, Math.Max(1, colorWidth - 38), Math.Max(1, ControlHeight - 16));

			foreach (var id in new[] { "PLAYERCOLOR", "MOUSE_CONTROL_DROPDOWN", "BATTLEFIELD_CAMERA_DROPDOWN", "UI_SCALE_DROPDOWN" })
			{
				var dropDown = widget.Get<DropDownButtonWidget>(id);
				dropDown.Background = "cc-mp-field";
				dropDown.ShowSeparator = false;
			}

			foreach (var id in new[] { "EDGESCROLL_CHECKBOX", "CURSORDOUBLE_CHECKBOX" })
			{
				var checkbox = widget.Get<CheckboxWidget>(id);
				checkbox.Background = "cc-mp-field";
			}

			var mouseContainer = widget.Get("MOUSE_CONTROL_CONTAINER");
			mouseContainer.Parent.Bounds = new WidgetBounds(0, 150, columnWidth, 68);
			mouseContainer.Bounds = new WidgetBounds(0, 0, columnWidth, 68);
			LayoutLabeledControl(mouseContainer, "MOUSE_CONTROL_LABEL", "MOUSE_CONTROL_DROPDOWN", LabelHeight, ControlHeight);

			var edgeScrollContainer = widget.Get("EDGESCROLL_CHECKBOX_CONTAINER");
			edgeScrollContainer.Parent.Bounds = new WidgetBounds(0, 230, columnWidth, ControlHeight);
			edgeScrollContainer.Bounds = new WidgetBounds(0, 0, columnWidth, ControlHeight);
			widget.Get("EDGESCROLL_CHECKBOX").Bounds = new WidgetBounds(0, 0, columnWidth, ControlHeight);

			var cameraContainer = widget.Get("BATTLEFIELD_CAMERA_DROPDOWN_CONTAINER");
			var scaleContainer = widget.Get("UI_SCALE_DROPDOWN_CONTAINER");
			cameraContainer.Parent.Bounds = new WidgetBounds(rightColumn, HeaderHeight + RowGap, columnWidth, 142);
			cameraContainer.Bounds = new WidgetBounds(0, 0, columnWidth, 68);
			scaleContainer.Bounds = new WidgetBounds(0, 74, columnWidth, 68);
			LayoutLabeledControl(cameraContainer, "BATTLEFIELD_CAMERA", "BATTLEFIELD_CAMERA_DROPDOWN", LabelHeight, ControlHeight);
			LayoutLabeledControl(scaleContainer, "UI_SCALE", "UI_SCALE_DROPDOWN", LabelHeight, ControlHeight);

			var cursorContainer = widget.Get("CURSORDOUBLE_CHECKBOX_CONTAINER");
			cursorContainer.Parent.Bounds = new WidgetBounds(rightColumn, 190, columnWidth, ControlHeight);
			cursorContainer.Bounds = new WidgetBounds(0, 0, columnWidth, ControlHeight);
			widget.Get("CURSORDOUBLE_CHECKBOX").Bounds = new WidgetBounds(0, 0, columnWidth, ControlHeight);

			var continueButton = widget.Get<ButtonWidget>("CONTINUE_BUTTON");
			continueButton.Bounds = new WidgetBounds(panelX + Inset, panelY + promptHeight - 64, promptWidth - 2 * Inset, 42);
			continueButton.Background = "cc-mp-control-pressed";
			continueButton.Font = "SettingsBold";
			return true;
		}

		internal static void ApplyMobilePresentation(Widget widget, IosSettingsLayout layout)
		{
			var border = layout.Scale(2);
			var safe = layout.IsPhone ? new Rectangle(layout.SafeBounds.X + border, layout.SafeBounds.Y + border,
				Math.Max(1, layout.SafeBounds.Width - 2 * border), Math.Max(1, layout.SafeBounds.Height - 2 * border)) : layout.Window;
			var inset = layout.Scale(12);
			var gap = layout.Scale(layout.IsPhone ? 8 : 12);
			var target = layout.MinimumTarget;
			var labelHeight = layout.Scale(24);
			var rowHeight = labelHeight + target;
			var titleHeight = layout.Scale(layout.IsPhone ? 32 : 36);
			var descriptionHeight = layout.Scale(layout.IsPhone ? 24 : 40);
			var width = Math.Max(1, safe.Width - 2 * inset);
			var columnWidth = Math.Max(1, (width - gap) / 2);
			var prompt = (BackgroundWidget)widget;
			prompt.Bounds = new WidgetBounds(safe.X, safe.Y, safe.Width, safe.Height);
			prompt.Background = layout.IsPhone ? "mobile-first-run-panel" : "settings-v2-panel";
			widget.Get("NUKE_HOUR_DESKTOP_PANEL").IsVisible = () => false;
			var title = widget.Get<LabelWidget>("PROMPT_TITLE");
			title.Bounds = new WidgetBounds(inset, inset, width, titleHeight);
			title.Font = "IosBold";
			title.Align = TextAlign.Left;
			var description = widget.Get<LabelWidget>("DESC_A");
			description.Bounds = new WidgetBounds(inset, inset + titleHeight, width, descriptionHeight);
			description.Font = "IosRegular";
			description.WordWrap = true;
			description.Align = TextAlign.Left;
			widget.Get("DESC_B").IsVisible = () => false;
			var scroll = widget.Get<ScrollPanelWidget>("SETTINGS_SCROLLPANEL");
			var top = inset + titleHeight + descriptionHeight + gap;
			scroll.Bounds = new WidgetBounds(inset, top, width, Math.Max(target, safe.Height - top - target - 2 * gap));
			scroll.EnableContentDragging = true;
			scroll.Background = "settings-v2-scrollpanel";
			scroll.ScrollBar = ScrollBar.Hidden;
			scroll.TopBottomSpacing = 0;
			scroll.Layout = new FixedIntroductionLayout();

			void Attach(Widget child)
			{
				child.Parent.Children.Remove(child);
				scroll.AddChild(child);
			}
			var groups = new[] {
				new[] { "PROFILE_SECTION_HEADER", "PLAYER_CONTAINER", "PLAYERCOLOR_CONTAINER" },
				new[] { "INPUT_SECTION_HEADER", "MOUSE_CONTROL_CONTAINER", "EDGESCROLL_CHECKBOX_CONTAINER" },
				new[] { "DISPLAY_SECTION_HEADER", "BATTLEFIELD_CAMERA_DROPDOWN_CONTAINER", "UI_SCALE_DROPDOWN_CONTAINER" }
			};
			if (layout.IsPhone) groups = groups.Where(g => g[0] != "INPUT_SECTION_HEADER").ToArray();
			var y = 0;
			foreach (var group in groups)
			{
				var header = widget.Get<BackgroundWidget>(group[0]);
				Attach(header);
				header.Background = "cc-mp-surface";
				header.Bounds = new WidgetBounds(0, y, width, labelHeight);
				var label = header.Get<LabelWidget>("LABEL");
				label.Font = "IosBold"; label.Align = TextAlign.Left;
				label.Bounds = new WidgetBounds(gap, 0, width - gap, labelHeight);
				y += labelHeight + gap;
				for (var i = 1; i < group.Length; i++)
				{
					var column = widget.Get(group[i]);
					Attach(column);
					column.Bounds = new WidgetBounds((i - 1) * (columnWidth + gap), y, columnWidth, rowHeight);
					foreach (var control in column.Children)
					{
						if (control is LabelWidget fieldLabel)
						{
							fieldLabel.Font = "IosRegular";
							fieldLabel.Bounds = new WidgetBounds(0, 0, columnWidth, labelHeight);
						}
						else
						{
							control.Bounds = new WidgetBounds(0, labelHeight, columnWidth, target);
							if (control is TextFieldWidget text) { text.Background = "cc-mp-field"; text.Font = "IosRegular"; }
							if (control is ButtonWidget button) { button.Background = "cc-mp-field"; button.Font = "IosRegular"; }
							if (control is DropDownButtonWidget drop) drop.ShowSeparator = false;
							if (control is CheckboxWidget checkbox) checkbox.Background = "settings-v2-checkbox";
						}
					}
				}
				y += rowHeight + gap;
			}
			var cursor = widget.Get("CURSORDOUBLE_CHECKBOX_CONTAINER");
			Attach(cursor);
			cursor.Bounds = new WidgetBounds(0, y, width, target);
			var cursorCheck = cursor.Get<CheckboxWidget>("CURSORDOUBLE_CHECKBOX");
			cursorCheck.Bounds = new WidgetBounds(0, 0, width, target);
			cursorCheck.Background = "settings-v2-checkbox"; cursorCheck.Font = "IosRegular";
			cursor.IsVisible = () => !layout.IsPhone;
			var color = widget.Get<ColorBlockWidget>("COLORBLOCK");
			color.Bounds = new WidgetBounds(gap, gap, columnWidth - target, target - 2 * gap);
			foreach (var child in scroll.Children.ToArray())
				if (!groups.Any(g => g.Contains(child.Id)) && child != cursor) child.IsVisible = () => false;
			scroll.ContentHeight = y + (layout.IsPhone ? 0 : target + gap);
			var continueButton = widget.Get<ButtonWidget>("CONTINUE_BUTTON");
			continueButton.Bounds = new WidgetBounds(inset, safe.Height - target - gap, width, target);
			continueButton.Background = "cc-mp-control-pressed";
			continueButton.Font = "IosBold";
		}

		sealed class FixedIntroductionLayout : ILayout
		{
			public void AdjustChild(Widget child) { }
			public void AdjustChildren() { }
		}

		static void LayoutLabeledControl(Widget container, string labelId, string controlId, int labelHeight, int controlHeight)
		{
			container.Get(labelId).Bounds = new WidgetBounds(0, 0, container.Bounds.Width, labelHeight);
			container.Get(controlId).Bounds = new WidgetBounds(0, labelHeight + 4, container.Bounds.Width, controlHeight);
		}

		[ObjectCreator.UseCtor]
		public IntroductionPromptLogic(Widget widget, ModData modData, WorldRenderer worldRenderer, Action onComplete)
		{
			var ps = Game.Settings.Player;
			var ds = Game.Settings.Graphics;
			var gs = Game.Settings.Game;
			var nukeHourMobile = Platform.UsesMobileLayout && modData.Manifest.Id == "ra2";
			var nukeHourDesktop = ApplyNukeHourDesktopPresentation(widget, modData);
			if (nukeHourMobile)
				ApplyMobilePresentation(widget, IosSettingsLayout.ForSnapshot(true, IosScreenMetrics.SnapshotFor(Game.Renderer.Resolution)));

			classic = FluentProvider.GetMessage(Classic);
			modern = FluentProvider.GetMessage(Modern);

			var escPressed = false;
			var nameTextfield = widget.Get<TextFieldWidget>("PLAYERNAME");
			nameTextfield.IsDisabled = () => worldRenderer.World.Type != WorldType.Shellmap;
			nameTextfield.Text = Settings.SanitizedPlayerName(ps.Name);

			var itchIntegration = modData.Manifest.Get<ItchIntegration>();
			itchIntegration.GetPlayerName(name => nameTextfield.Text = Settings.SanitizedPlayerName(name));

			nameTextfield.OnLoseFocus = () =>
			{
				if (escPressed)
				{
					escPressed = false;
					return;
				}

				nameTextfield.Text = nameTextfield.Text.Trim();
				if (nameTextfield.Text.Length == 0)
					nameTextfield.Text = Settings.SanitizedPlayerName(ps.Name);
				else
				{
					nameTextfield.Text = Settings.SanitizedPlayerName(nameTextfield.Text);
					ps.Name = nameTextfield.Text;
				}
			};

			nameTextfield.OnEnterKey = _ => { nameTextfield.YieldKeyboardFocus(); return true; };
			nameTextfield.OnEscKey = _ =>
			{
				nameTextfield.Text = Settings.SanitizedPlayerName(ps.Name);
				escPressed = true;
				nameTextfield.YieldKeyboardFocus();
				return true;
			};

			var mouseControlDescClassic = widget.Get("MOUSE_CONTROL_DESC_CLASSIC");
			mouseControlDescClassic.IsVisible = () => !nukeHourDesktop && !nukeHourMobile && gs.UseClassicMouseStyle;

			var mouseControlDescModern = widget.Get("MOUSE_CONTROL_DESC_MODERN");
			mouseControlDescModern.IsVisible = () => !nukeHourDesktop && !nukeHourMobile && !gs.UseClassicMouseStyle;

			var mouseControlDropdown = widget.Get<DropDownButtonWidget>("MOUSE_CONTROL_DROPDOWN");
			mouseControlDropdown.OnMouseDown = _ => InputSettingsLogic.ShowMouseControlDropdown(mouseControlDropdown, gs);
			mouseControlDropdown.GetText = () => gs.UseClassicMouseStyle ? classic : modern;

			foreach (var container in new[] { mouseControlDescClassic, mouseControlDescModern })
			{
				var classicScrollRight = container.Get("DESC_SCROLL_RIGHT");
				classicScrollRight.IsVisible = () => gs.UseClassicMouseStyle ^ gs.UseAlternateScrollButton;

				var classicScrollMiddle = container.Get("DESC_SCROLL_MIDDLE");
				classicScrollMiddle.IsVisible = () => !gs.UseClassicMouseStyle ^ gs.UseAlternateScrollButton;

				var zoomDesc = container.Get("DESC_ZOOM");
				zoomDesc.IsVisible = () => gs.ZoomModifier == Modifiers.None;

				var zoomDescModifier = container.Get<LabelWidget>("DESC_ZOOM_MODIFIER");
				zoomDescModifier.IsVisible = () => gs.ZoomModifier != Modifiers.None;

				var zoomDescModifierTemplate = zoomDescModifier.GetText();
				var zoomDescModifierLabel = new CachedTransform<Modifiers, string>(
					mod => zoomDescModifierTemplate.Replace("MODIFIER", mod.ToString()));
				zoomDescModifier.GetText = () => zoomDescModifierLabel.Update(gs.ZoomModifier);

				var edgescrollDesc = container.Get<LabelWidget>("DESC_EDGESCROLL");
				edgescrollDesc.IsVisible = () => gs.ViewportEdgeScroll;
			}

			SettingsUtils.BindCheckboxPref(widget, "EDGESCROLL_CHECKBOX", gs, "ViewportEdgeScroll");

			var colorManager = modData.DefaultRules.Actors[SystemActors.World].TraitInfo<IColorPickerManagerInfo>();

			var colorDropdown = widget.Get<DropDownButtonWidget>("PLAYERCOLOR");
			colorDropdown.IsDisabled = () => worldRenderer.World.Type != WorldType.Shellmap;
			colorDropdown.OnMouseDown = _ => colorManager.ShowColorDropDown(colorDropdown, ps.Color, null, worldRenderer, color =>
			{
				ps.Color = color;
				Game.Settings.Save();
			});
			colorDropdown.Get<ColorBlockWidget>("COLORBLOCK").GetColor = () => ps.Color;

			var viewportSizes = modData.Manifest.Get<WorldViewportSizes>();
			var battlefieldCameraDropDown = widget.Get<DropDownButtonWidget>("BATTLEFIELD_CAMERA_DROPDOWN");
			var battlefieldCameraLabel = new CachedTransform<WorldViewport, string>(vs => DisplaySettingsLogic.GetViewportSizeName(modData, vs));
			battlefieldCameraDropDown.OnMouseDown = _ => DisplaySettingsLogic.ShowBattlefieldCameraDropdown(
				modData, battlefieldCameraDropDown, viewportSizes, ds, worldRenderer.Viewport.Tick);
			battlefieldCameraDropDown.GetText = () => battlefieldCameraLabel.Update(ds.ViewportDistance);

			var uiScaleDropdown = widget.Get<DropDownButtonWidget>("UI_SCALE_DROPDOWN");
			var uiScaleLabel = new CachedTransform<float, string>(s => $"{(int)(100 * s)}%");
			uiScaleDropdown.OnMouseDown = _ => DisplaySettingsLogic.ShowUIScaleDropdown(uiScaleDropdown, ds);
			uiScaleDropdown.GetText = () => uiScaleLabel.Update(ds.UIScale);

			var minResolution = viewportSizes.MinEffectiveResolution;
			var resolution = Game.Renderer.Resolution;
			var disableUIScale = worldRenderer.World.Type != WorldType.Shellmap ||
				resolution.Width * ds.UIScale < 1.25f * minResolution.Width ||
				resolution.Height * ds.UIScale < 1.25f * minResolution.Height;

			uiScaleDropdown.IsDisabled = () => disableUIScale;

			SettingsUtils.BindCheckboxPref(widget, "CURSORDOUBLE_CHECKBOX", ds, "CursorDouble");

			widget.Get<ButtonWidget>("CONTINUE_BUTTON").OnClick = () =>
			{
				Game.Settings.Game.IntroductionPromptVersion = IntroductionVersion;
				Game.Settings.Save();
				Ui.CloseWindow();
				onComplete();
			};

			if (!nukeHourDesktop && !nukeHourMobile)
				SettingsUtils.AdjustSettingsScrollPanelLayout(widget.Get<ScrollPanelWidget>("SETTINGS_SCROLLPANEL"));
		}
	}
}
