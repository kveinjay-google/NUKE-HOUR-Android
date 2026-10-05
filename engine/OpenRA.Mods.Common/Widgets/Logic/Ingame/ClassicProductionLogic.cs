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
using System.Runtime.CompilerServices;
using OpenRA.Mods.Common.Traits;
using OpenRA.Widgets;

namespace OpenRA.Mods.Common.Widgets.Logic
{
	public static class IosProductionFooterPolicy
	{
		public static WidgetBounds CombinedBounds(WidgetBounds first, WidgetBounds second) => new(
			Math.Min(first.X, second.X), Math.Min(first.Y, second.Y),
			Math.Max(first.X + first.Width, second.X + second.Width) - Math.Min(first.X, second.X),
			Math.Max(first.Y + first.Height, second.Y + second.Height) - Math.Min(first.Y, second.Y));

		static readonly ConditionalWeakTable<ButtonWidget, Action> SkinRestorers = new();

		public static void RestoreCurrentMode(ButtonWidget button)
		{
			if (button != null && SkinRestorers.TryGetValue(button, out var apply))
				apply();
		}

		public static Action Bind(ButtonWidget button, bool isIos, GameSettings settings,
			Func<bool> canCancel, Action cancel, Func<string> cancelText)
		{
			if (!isIos || button == null)
				return () => { };

			var background = button.Background;
			var text = button.GetText;
			var tooltip = button.GetTooltipText;
			var description = button.GetTooltipDesc;
			var visible = button.IsVisible;
			var disabled = button.IsDisabled;
			var click = button.OnClick;
			var localizedCancelText = new Lazy<string>(cancelText);
			Func<string> getCancelText = () => localizedCancelText.Value;
			Func<bool> cancelDisabled = () => !canCancel();
			Func<bool> cancelVisible = () => true;
			Func<string> cancelDescription = () => "";
			var showingCancel = false;

			void Apply(bool force)
			{
				var enabled = settings.IosProductionCancelButtons;
				if (!force && showingCancel == enabled)
					return;

				showingCancel = enabled;
				button.Background = enabled ? "button" : background;
				button.GetText = enabled ? getCancelText : text;
				button.GetTooltipText = enabled ? getCancelText : tooltip;
				button.GetTooltipDesc = enabled ? cancelDescription : description;
				button.IsVisible = enabled ? cancelVisible : visible;
				button.IsDisabled = enabled ? cancelDisabled : disabled;
				button.OnClick = enabled ? cancel : click;
			}

			void Refresh() => Apply(false);
			SkinRestorers.Remove(button);
			SkinRestorers.Add(button, () => Apply(true));
			Refresh();
			return Refresh;
		}
	}

	public static class IosProductionCategoryPolicy
	{
		// Faction button sprites already contain the category glyph. They must be
		// drawn once, not tiled at their original 30px width or overlaid with ICON.
		public static void ApplySingleImageChrome(ButtonWidget button)
		{
			button.StretchBackground = true;
			var icon = button.GetOrNull<ImageWidget>("ICON");
			if (icon != null)
				icon.IsVisible = () => false;
		}

		static readonly string[] DesktopGroups = { "Building", "Support", "Infantry", "Vehicle", "Aircraft", "Ship" };
		static readonly string[] PhoneGroups = { "Building", "Support", "Infantry", "Vehicle" };
		static readonly string[] MobileVehicleGroups = { "Vehicle", "Aircraft", "Ship" };

		public static bool UsesConsolidatedCategories(InterfaceStyleMode style) =>
			style == InterfaceStyleMode.Classic || style == InterfaceStyleMode.ClassicHD;

		public static string[] VisibleGroups(bool compactPhone, bool consolidated = false) =>
			(compactPhone || consolidated ? PhoneGroups : DesktopGroups).ToArray();

		public static string[] SourceGroups(string presentationGroup, bool compactPhone, bool consolidated = false)
		{
			if (!compactPhone && !consolidated)
				return new[] { presentationGroup };

			if (presentationGroup == "Vehicle")
				return MobileVehicleGroups.ToArray();

			if (presentationGroup == "Aircraft" || presentationGroup == "Ship")
				return Array.Empty<string>();

			return new[] { presentationGroup };
		}

		public static int ButtonHeight(int availableWidth, bool compactPhone) =>
			compactPhone ? Math.Max(48, availableWidth / PhoneGroups.Length) : 32;

		public static WidgetBounds[] ButtonBounds(
			int availableWidth, bool compactPhone, bool consolidatedHd = false)
		{
			var count = compactPhone || consolidatedHd ? PhoneGroups.Length : DesktopGroups.Length;
			var height = consolidatedHd && !compactPhone ? 38 : ButtonHeight(availableWidth, compactPhone);
			var bounds = new WidgetBounds[count];
			for (var i = 0; i < count; i++)
			{
				var left = availableWidth * i / count;
				var right = availableWidth * (i + 1) / count;
				bounds[i] = new WidgetBounds(left, 0, right - left, height);
			}

			return bounds;
		}

		public static WidgetBounds[] TouchButtonBounds(
			int availableWidth, bool compactPhone, bool consolidatedHd = false)
		{
			var bounds = ButtonBounds(availableWidth, compactPhone, consolidatedHd);
			for (var i = 0; i < bounds.Length; i++)
			{
				var height = Math.Max(44, bounds[i].Height);
				bounds[i] = new WidgetBounds(bounds[i].X, (bounds[i].Height - height) / 2,
					bounds[i].Width, height);
			}

			return bounds;
		}
	}

	public class ClassicProductionLogic : ChromeLogic
	{
		[FluentReference]
		const string CancelProduction = "button-touch-production-cancel";

		readonly ProductionPaletteWidget palette;
		readonly World world;
		readonly ProductionTypeButtonWidget[] productionTypeButtons;

		void SetupProductionGroupButton(ProductionTypeButtonWidget button, bool compactPhone)
		{
			if (button == null)
				return;

			var consolidatedCategories = IosProductionCategoryPolicy.UsesConsolidatedCategories(
				Game.Settings.Game.EffectiveInterfaceStyle);
			var sourceGroups = IosProductionCategoryPolicy.SourceGroups(
				button.ProductionGroup, compactPhone, consolidatedCategories);
			if (sourceGroups.Length == 0)
			{
				button.IsVisible = () => false;
				button.IsDisabled = () => true;
				return;
			}

			// Classic production queues are initialized at game start, and then never change.
			var queues = world.LocalPlayer.PlayerActor.TraitsImplementing<ProductionQueue>()
				.Where(q => sourceGroups.Contains(q.Info.Group ?? q.Info.Type))
				.OrderBy(q => Array.IndexOf(sourceGroups, q.Info.Group ?? q.Info.Type))
				.ToArray();

			void SelectTab(bool reverse)
			{
				// Prefer an enabled queue; fall back so categories with no producer yet still switch the palette.
				palette.SetCurrentQueues(queues);

				// When a tab is selected, scroll to the top because the current row position may be invalid for the new tab
				palette.ScrollToTop();

				// Attempt to pick up a completed building (if there is one) so it can be placed
				palette.PickUpCompletedBuilding();
			}

			// Always keep category tabs clickable and show their icons. Empty queues simply show an empty palette.
			button.IsDisabled = () => queues.Length == 0;
			button.OnMouseUp = mi => SelectTab(mi.Modifiers.HasModifier(Modifiers.Shift));
			button.OnKeyPress = e => SelectTab(e.Modifiers.HasModifier(Modifiers.Shift));
			button.OnClick = () => SelectTab(false);
			button.IsHighlighted = () => ProductionAlertBlinkPolicy.ShouldHighlightCategory(
				queues.Contains(palette.CurrentQueue),
				queues.Any(q => q.AllQueued().Any(i => i.Done)),
				Game.RunTime);

			var chromeName = button.ProductionGroup.ToLowerInvariant();
			var icon = button.Get<ImageWidget>("ICON");
			icon.GetImageName = () =>
				ProductionAlertBlinkPolicy.ShowAlertFrame(
					queues.Any(q => q.AllQueued().Any(i => i.Done)), Game.RunTime) ?
					chromeName + "-alert" : chromeName;
		}

		[ObjectCreator.UseCtor]
		public ClassicProductionLogic(Widget widget, World world)
		{
			this.world = world;
			palette = widget.Get<ProductionPaletteWidget>("PRODUCTION_PALETTE");
			ApplyIosSidebarLayout(widget, palette);

			var background = widget.GetOrNull("PALETTE_BACKGROUND");
			var foreground = widget.GetOrNull("PALETTE_FOREGROUND");
			if (background != null || foreground != null)
			{
				Widget backgroundTemplate = null;
				Widget oneColumnBackgroundTemplate = null;
				Widget twoColumnBackgroundTemplate = null;
				Widget threeColumnBackgroundTemplate = null;
				Widget backgroundBottom = null;
				Widget foregroundTemplate = null;

				if (background != null)
				{
					backgroundTemplate = background.Get("ROW_TEMPLATE");
					oneColumnBackgroundTemplate = background.GetOrNull("ONE_COLUMN_ROW_TEMPLATE");
					twoColumnBackgroundTemplate = background.GetOrNull("TWO_COLUMN_ROW_TEMPLATE");
					threeColumnBackgroundTemplate = background.GetOrNull("THREE_COLUMN_ROW_TEMPLATE");
					backgroundBottom = background.GetOrNull("BOTTOM_CAP");
					foreach (var template in new[]
					{
						oneColumnBackgroundTemplate,
						twoColumnBackgroundTemplate,
						threeColumnBackgroundTemplate
					})
						if (template is ImageWidget image)
							image.GetImageCollection = () => "classic-production-grid-" +
								TouchFactionSkin.Resolve(world.LocalPlayer?.Faction.InternalName);
				}

				if (foreground != null)
					foregroundTemplate = foreground.Get("ROW_TEMPLATE");

				void UpdateBackground(int _, int icons)
				{
					if (palette.ExternalSidebarLayout)
						return;
					var iosLayout = Platform.UsesMobileLayout
						? IosIngameSidebarLayoutPolicy.Create(
							IosScreenMetrics.SnapshotFor(Game.Renderer.Resolution))
						: default;
					var iosPaletteLayout = Platform.UsesMobileLayout
						? IosProductionPaletteLayoutPolicy.Create(
							IosScreenMetrics.SnapshotFor(Game.Renderer.Resolution), iosLayout,
							Game.Settings.Game.IosProductionPaletteMode)
						: default;
					var selectedTemplate = backgroundTemplate;
					if (Platform.UsesMobileLayout)
						selectedTemplate = palette.Columns switch
						{
							1 => oneColumnBackgroundTemplate ?? backgroundTemplate,
							2 => twoColumnBackgroundTemplate ?? backgroundTemplate,
							_ => threeColumnBackgroundTemplate ?? backgroundTemplate
						};
					var rows = Platform.UsesMobileLayout ?
						Math.Min(palette.MaximumRows, Math.Max(1, (icons + palette.Columns - 1) / palette.Columns)) :
						Math.Min(palette.MaximumRows,
							Math.Max(palette.MinimumRows, (icons + palette.Columns - 1) / palette.Columns));
					var rowHeights = Platform.UsesMobileLayout ?
						IosProductionPaletteLayoutPolicy.BackgroundRowHeights(iosPaletteLayout, iosLayout.BottomCapY)
						: Enumerable.Repeat(backgroundTemplate?.Bounds.Height ?? foregroundTemplate.Bounds.Height, rows).ToArray();

					if (background != null)
					{
						background.RemoveChildren();

						var y = 0;
						foreach (var height in rowHeights)
						{
							var row = CloneProductionRow(selectedTemplate, y, height,
								Platform.UsesMobileLayout ? widget.Bounds.Width : selectedTemplate.Bounds.Width);
							background.AddChild(row);
							y += height;
						}

						if (backgroundBottom == null)
							return;

						backgroundBottom.Bounds.Y = Platform.UsesMobileLayout ? iosLayout.BottomCapY : y;
						if (Platform.UsesMobileLayout)
						{
							backgroundBottom.Bounds.Width = widget.Bounds.Width;
							if (backgroundBottom is ImageWidget cap)
								cap.StretchToFit = true;
						}
						background.AddChild(backgroundBottom);
					}

					if (foreground != null)
					{
						foreground.RemoveChildren();

						var y = 0;
						foreach (var height in rowHeights)
						{
							var row = CloneProductionRow(foregroundTemplate, y, height,
								Platform.UsesMobileLayout ? widget.Bounds.Width : foregroundTemplate.Bounds.Width);
							foreground.AddChild(row);
							y += height;
						}
					}
				}

				palette.OnIconCountChanged += UpdateBackground;

				// Set the initial palette state
				UpdateBackground(0, 0);
			}

			var typesContainer = widget.Get("PRODUCTION_TYPES");
			productionTypeButtons = typesContainer.Children.OfType<ProductionTypeButtonWidget>().ToArray();
			var compactPhone = Platform.UsesMobileLayout && IosScreenMetrics.SnapshotFor(Game.Renderer.Resolution).IsCompactPhone;
			foreach (var button in productionTypeButtons)
				SetupProductionGroupButton(button, compactPhone);
			ApplyProductionCategoryLayout(typesContainer, productionTypeButtons);

			palette.OnSwipeProductionType = SelectAdjacentProductionGroup;

			// Hook up scroll up and down buttons on the palette before installing the
			// ticker so their authored bottom-cap slots can be refreshed after any
			// renderer or widget relayout.
			var scrollDown = widget.GetOrNull<ButtonWidget>("SCROLL_DOWN_BUTTON");
			if (scrollDown != null)
			{
				scrollDown.OnClick = palette.ScrollDown;
				scrollDown.IsVisible = () => Platform.UsesMobileLayout || palette.TotalIconCount > palette.MaxIconRowOffset * palette.Columns;
				scrollDown.IsDisabled = () => !palette.CanScrollDown;
			}

			var scrollUp = widget.GetOrNull<ButtonWidget>("SCROLL_UP_BUTTON");
			if (scrollUp != null)
			{
				scrollUp.OnClick = palette.ScrollUp;
				scrollUp.IsVisible = () => Platform.UsesMobileLayout || palette.TotalIconCount > palette.MaxIconRowOffset * palette.Columns;
				scrollUp.IsDisabled = () => !palette.CanScrollUp;
			}

			RefreshIosSidebarLayout(widget, palette, typesContainer, scrollUp, scrollDown);

			Action BindCancelButton(ButtonWidget button) => IosProductionFooterPolicy.Bind(
				button, Platform.UsesMobileLayout, Game.Settings.Game,
				() => palette.CanCancelSelectedProduction, palette.CancelSelectedProduction,
				() => FluentProvider.GetMessage(CancelProduction));

			var refreshUp = BindCancelButton(scrollUp);
			var refreshDown = BindCancelButton(scrollDown);
			if (Platform.UsesMobileLayout)
			{
				// Begin each battle in queue-cancellation mode, including profiles from older builds.
				Game.Settings.Game.IosProductionCancelButtons = true;
				void ToggleFooter()
				{
					Game.Settings.Game.IosProductionCancelButtons = !Game.Settings.Game.IosProductionCancelButtons;
					refreshUp();
					refreshDown();
				}
				if (scrollUp != null) scrollUp.OnHorizontalSwipe = ToggleFooter;
				if (scrollDown != null) scrollDown.OnHorizontalSwipe = ToggleFooter;
				refreshUp();
				refreshDown();
			}

			var ticker = widget.Get<LogicTickerWidget>("PRODUCTION_TICKER");
			ticker.OnTick = () =>
			{
				RefreshIosSidebarLayout(widget, palette, typesContainer, scrollUp, scrollDown);
				refreshUp();
				refreshDown();
				if (Platform.UsesMobileLayout && scrollUp != null && scrollDown != null)
				{
					scrollDown.IsVisible = () => !Game.Settings.Game.IosProductionCancelButtons;
					if (Game.Settings.Game.IosProductionCancelButtons && !palette.ExternalSidebarLayout)
						scrollUp.Bounds = IosProductionFooterPolicy.CombinedBounds(scrollUp.Bounds, scrollDown.Bounds);
				}

				// Only auto-select when nothing is selected. Do not leave an intentionally empty category tab.
				if (palette.CurrentQueue == null)
				{
					foreach (var b in typesContainer.Children)
					{
						if (b is not ProductionTypeButtonWidget button || !button.IsVisible() || button.IsDisabled())
							continue;

						button.OnClick();
						break;
					}
				}
			};

			SetMaximumVisibleRows(palette);
		}

		static void PositionIosScrollButton(
			Widget typesContainer, ButtonWidget button, IosIngameSidebarLayout layout)
		{
			if (!Platform.UsesMobileLayout || button == null)
				return;

			// Both axes must account for the category container: phone tabs start at
			// X=0, while the original tablet strip starts at X=27.
			button.Bounds = layout.ScrollButtonLocalBounds(
				typesContainer.Bounds.Y, button.Id == "SCROLL_UP_BUTTON", typesContainer.Bounds.X);
		}

		static void RefreshIosSidebarLayout(
			Widget productionSidebar,
			ProductionPaletteWidget productionPalette,
			Widget typesContainer,
			ButtonWidget scrollUp,
			ButtonWidget scrollDown)
		{
			if (!Platform.UsesMobileLayout || productionPalette.ExternalSidebarLayout)
				return;

			var snapshot = IosScreenMetrics.SnapshotFor(Game.Renderer.Resolution);
			var layout = IosIngameSidebarLayoutPolicy.Create(snapshot);
			ApplyIosSidebarLayout(productionSidebar, productionPalette, snapshot, layout);
			ApplyProductionCategoryLayout(typesContainer,
				typesContainer.Children.OfType<ProductionTypeButtonWidget>().ToArray());
			PositionIosScrollButton(typesContainer, scrollUp, layout);
			PositionIosScrollButton(typesContainer, scrollDown, layout);
		}

		static void ApplyProductionCategoryLayout(
			Widget typesContainer, ProductionTypeButtonWidget[] buttons)
		{
			var touch = Platform.UsesMobileLayout;
			var compactPhone = touch && IosScreenMetrics.SnapshotFor(Game.Renderer.Resolution).IsCompactPhone;
			var consolidatedCategories = IosProductionCategoryPolicy.UsesConsolidatedCategories(
				Game.Settings.Game.EffectiveInterfaceStyle);
			if (!compactPhone && !consolidatedCategories)
				return;

			var parentWidth = typesContainer.Parent?.Bounds.Width ?? 234;
			var width = consolidatedCategories ? Math.Max(1, parentWidth - 20) :
				compactPhone ? parentWidth : typesContainer.Bounds.Width;
			var visibleGroups = IosProductionCategoryPolicy.VisibleGroups(compactPhone, consolidatedCategories);
			var bounds = IosProductionCategoryPolicy.ButtonBounds(width, compactPhone, consolidatedCategories);
			var touchBounds = IosProductionCategoryPolicy.TouchButtonBounds(width, compactPhone, consolidatedCategories);
			if (consolidatedCategories)
				typesContainer.Bounds = new WidgetBounds(10, -bounds[0].Height, width, bounds[0].Height);
			else if (touch)
			{
				if (compactPhone)
					typesContainer.Bounds = new WidgetBounds(0, -bounds[0].Height, width, bounds[0].Height);
			}
			for (var i = 0; i < visibleGroups.Length; i++)
			{
				var button = buttons.First(candidate => candidate.ProductionGroup == visibleGroups[i]);
				var slot = bounds[i];
				button.Bounds = slot;
				button.ParentEventBounds = touch ? touchBounds[i].ToRectangle() : null;
				var icon = button.GetOrNull("ICON");
				if (icon != null)
				{
					icon.Bounds.X = (button.Bounds.Width - icon.Bounds.Width) / 2;
					icon.Bounds.Y = (button.Bounds.Height - icon.Bounds.Height) / 2;
				}
			}
		}

		public static Widget CloneProductionRow(Widget template, int y, int height, int width)
		{
			var row = template.Clone();
			row.Bounds.Y = y;
			row.Bounds.Height = height;
			row.Bounds.Width = width;
			if (row is ImageWidget image)
				image.StretchToFit |= height != template.Bounds.Height || width != template.Bounds.Width;
			return row;
		}

		static void ApplyIosSidebarLayout(Widget productionSidebar, ProductionPaletteWidget productionPalette)
		{
			if (!Platform.UsesMobileLayout)
				return;

			var snapshot = IosScreenMetrics.SnapshotFor(Game.Renderer.Resolution);
			var layout = IosIngameSidebarLayoutPolicy.Create(snapshot);
			ApplyIosSidebarLayout(productionSidebar, productionPalette, snapshot, layout);
		}

		static void ApplyIosSidebarLayout(
			Widget productionSidebar,
			ProductionPaletteWidget productionPalette,
			IosScreenSnapshot snapshot,
			IosIngameSidebarLayout layout)
		{
			productionSidebar.Bounds.X = layout.ProductionBounds.X;
			productionSidebar.Bounds.Y = layout.ProductionBounds.Y;
			productionSidebar.Bounds.Width = layout.ProductionBounds.Width;
			productionSidebar.Bounds.Height = layout.ProductionBounds.Height;
			var paletteLayout = IosProductionPaletteLayoutPolicy.Create(
				snapshot, layout, Game.Settings.Game.IosProductionPaletteMode);
			productionPalette.ApplyGridLayout(
				paletteLayout.Columns, paletteLayout.IconSize, paletteLayout.IconMargin);
			productionPalette.Bounds.X = paletteLayout.PaletteX;
			productionPalette.Bounds.Y = paletteLayout.PaletteY;
			productionPalette.Bounds.Width = paletteLayout.GridWidth;
			productionPalette.Bounds.Height = paletteLayout.MaximumRows * paletteLayout.IconSize.Y +
				Math.Max(0, paletteLayout.MaximumRows - 1) * paletteLayout.IconMargin.Y;
			productionPalette.MaximumRows = paletteLayout.MaximumRows;
			productionPalette.MaxIconRowOffset = paletteLayout.MaximumRows;

			var top = Ui.Root.GetOrNull("SIDEBAR_BACKGROUND_TOP");
			if (top != null)
			{
				top.Bounds.X = layout.TopBounds.X;
				top.Bounds.Y = layout.TopBounds.Y;
				top.Bounds.Width = layout.TopBounds.Width;
				top.Bounds.Height = layout.TopBounds.Height;
			}
		}

		bool SelectAdjacentProductionGroup(ProductionSwipeDirection direction)
		{
			if (productionTypeButtons.Length < 2)
				return false;

			var current = Array.FindIndex(productionTypeButtons, button => button.IsHighlighted());
			if (current < 0)
				return false;

			var enabled = productionTypeButtons.Select(button => button.IsVisible() && !button.IsDisabled()).ToArray();
			var adjacent = ProductionSwipePolicy.FindAdjacentIndex(current, enabled, direction);
			if (adjacent < 0)
				return false;

			productionTypeButtons[adjacent].OnClick();
			return true;
		}

		static void SetMaximumVisibleRows(ProductionPaletteWidget productionPalette)
		{
			if (productionPalette.ExternalSidebarLayout)
				return;
			if (Platform.UsesMobileLayout)
			{
				var container = Ui.Root.GetOrNull<ContainerWidget>("SIDEBAR_PRODUCTION");
				if (container == null)
					return;

				var snapshot = IosScreenMetrics.SnapshotFor(Game.Renderer.Resolution);
				var shell = IosIngameSidebarLayoutPolicy.Create(snapshot);
				var layout = IosProductionPaletteLayoutPolicy.Create(
					snapshot, shell, Game.Settings.Game.IosProductionPaletteMode);
				productionPalette.MaximumRows = layout.MaximumRows;
				productionPalette.MaxIconRowOffset = layout.MaximumRows;
				return;
			}

			var screenHeight = Game.Renderer.Resolution.Height;

			// Get height of currently displayed icons
			var containerWidget = Ui.Root.GetOrNull<ContainerWidget>("SIDEBAR_PRODUCTION");

			if (containerWidget == null)
				return;

			var sidebarProductionHeight = containerWidget.Bounds.Y;

			// Check if icon heights exceed y resolution
			var maxItemsHeight = screenHeight - sidebarProductionHeight;

			var maxIconRowOffest = maxItemsHeight / productionPalette.IconSize.Y - 1;
			productionPalette.MaxIconRowOffset = Math.Min(maxIconRowOffest, productionPalette.MaximumRows);
		}
	}
}
