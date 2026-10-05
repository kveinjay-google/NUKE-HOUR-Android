#region Copyright & License Information
/*
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Mods.Common.Widgets.Logic;
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Mods.RA2.Widgets
{
	public class CustomCommandBarWidget : BackgroundWidget, IIosViewportControlsObstacle
	{
		const int Pad = 6;
		const int Gap = 2;
		const int ExpandedButtonHeight = 52;
		const int DesktopCompactButtonHeight = 28;
		const int DesktopCompactButtonWidth = 34;
		const int TouchCompactButtonSize = 44;
		const int EditDeadzone = 8;
		const int RowGap = 6;
		const int EditButtonWidth = 72;
		const int DesktopCollapseButtonWidth = 34;

		readonly Dictionary<string, ButtonWidget> buttons = new();
		readonly Dictionary<string, Action> originalClicks = new();
		readonly Dictionary<string, Action<MouseInput>> originalMouseDowns = new();
		readonly Dictionary<string, LabelWidget> labels = new();
		readonly Dictionary<string, ImageWidget> icons = new();

		List<string> visibleOrder = new();
		bool editMode;
		bool expanded;
		bool setupDone;
		string lastTouchSkin;
		bool lastTouchExpanded;
		bool touchLayoutInitialized;
		bool? lastTouchCtrlEnabled;
		Size lastTouchResolution;
		Size lastTouchNativePointSize;
		Rectangle lastTouchSafeBounds;
		int lastTouchJoystickPoints;
		Rectangle touchViewportObstacle;
		readonly List<Rectangle> touchPanelBounds = new();
		readonly List<Rectangle> touchActivePanelBounds = new();
		public Rectangle ViewportObstacleBounds => touchLayoutInitialized ? touchViewportObstacle : RenderBounds;

		string dragId;
		int2 dragStart;
		bool dragMoved;

		ButtonWidget editButton;
		ButtonWidget collapseButton;
		ImageWidget collapseIcon;
		ImageWidget editIcon;
		LabelWidget editHint;
		MacCommandDock macDock;
		readonly World world;

		[ObjectCreator.UseCtor]
		public CustomCommandBarWidget(World world) : this()
		{
			this.world = world;
		}

		public CustomCommandBarWidget()
		{
			IsVisible = () => !Platform.UsesMobileLayout || IosFloatingControlsPreferences.Visible;
		}

		void EnsureSetup()
		{
			if (setupDone)
				return;

			var slots = GetOrNull("COMMAND_SLOTS");
			if (slots == null)
				return;

			foreach (var def in CommandBarCatalog.All)
			{
				var button = slots.GetOrNull<ButtonWidget>(def.Id);
				if (button == null)
					continue;

				buttons[def.Id] = button;
				labels[def.Id] = button.GetOrNull<LabelWidget>("LABEL");
				icons[def.Id] = button.GetOrNull<ImageWidget>("ICON");
			}

			if (buttons.Count == 0)
				return;

			editButton = GetOrNull<ButtonWidget>("EDIT_TOGGLE");
			collapseButton = GetOrNull<ButtonWidget>("COLLAPSE_TOGGLE");
			editHint = GetOrNull<LabelWidget>("EDIT_HINT");

			if (MacCommandDockLayout.Enabled(Platform.CurrentPlatform) && collapseButton != null)
			{
				macDock = new MacCommandDock(this, buttons, collapseButton, editButton, editHint, world);
				setupDone = true;
				return;
			}

			if (editButton != null)
			{
				editButton.OnClick = ToggleEditMode;
				editButton.IsHighlighted = () => editMode;
				editIcon = editButton.GetOrNull<ImageWidget>("ICON");
				if (editIcon != null)
				{
					editIcon.IsVisible = () => UseTouchLayout;
					editIcon.GetImageName = () => TouchFactionSkin.Active;
					var desktopText = editButton.GetText;
					editButton.GetText = () => UseTouchLayout ? string.Empty : desktopText();
				}
			}

			if (collapseButton != null)
			{
				collapseIcon = collapseButton.GetOrNull<ImageWidget>("ICON");
				collapseButton.OnClick = ToggleExpanded;
				collapseButton.IsHighlighted = () => !expanded;
				collapseButton.GetText = () => "";
				if (collapseIcon != null)
					collapseIcon.GetImageName = () => expanded ? "collapse" : "expand";
				collapseButton.GetTooltipText = () => expanded
					? FluentProvider.GetMessage("button-command-bar-collapse.tooltip")
					: FluentProvider.GetMessage("button-command-bar-expand.tooltip");
			}

			if (UseTouchLayout)
				foreach (var button in buttons.Values)
					button.OnLongPress = () => { if (!editMode) EnterEditMode(); };

			var prefs = CommandBarPreferences.Load();
			visibleOrder = CommandBarLayoutPolicy.NormalizeVisibleOrder(
				prefs.VisibleOrder, UseTouchLayout, prefs.Version).Where(id => buttons.ContainsKey(id)).ToList();
			if (visibleOrder.Count == 0)
				visibleOrder = CommandBarLayoutPolicy.NormalizeVisibleOrder(
					CommandBarCatalog.DefaultVisibleIds(), UseTouchLayout, CommandBarLayoutPolicy.CurrentVersion)
					.Where(id => buttons.ContainsKey(id)).ToList();

			if (UseTouchLayout)
			{
				foreach (var pair in icons)
					if (pair.Value != null)
						buttons[pair.Key].AddChild(new ButtonActivationOverlayWidget(buttons[pair.Key], pair.Value, () => TouchFactionSkin.Active));
				if (collapseIcon != null)
					collapseButton.AddChild(new ButtonActivationOverlayWidget(collapseButton, collapseIcon, () => TouchFactionSkin.Active));
				if (editIcon != null)
					editButton.AddChild(new ButtonActivationOverlayWidget(editButton, editIcon, () => TouchFactionSkin.Active));
			}

			expanded = prefs.Expanded;

			setupDone = true;
			ApplyLayout(false);
		}

		void ToggleExpanded()
		{
			if (editMode)
				ExitEditMode(true);

			expanded = !expanded;
			CommandBarPreferences.Save(visibleOrder, expanded);
			ApplyLayout(false);
		}

		void ToggleEditMode()
		{
			if (!expanded)
			{
				expanded = true;
				CommandBarPreferences.Save(visibleOrder, expanded);
			}

			if (editMode)
				ExitEditMode(true);
			else
				EnterEditMode();
		}

		readonly Dictionary<string, Func<bool>> originalDisabled = new();

		void EnterEditMode()
		{
			editMode = true;
			foreach (var kv in buttons)
			{
				originalClicks[kv.Key] = kv.Value.OnClick;
				originalDisabled[kv.Key] = kv.Value.IsDisabled;
				if (UseTouchLayout) kv.Value.IsDisabled = () => false;
				originalMouseDowns[kv.Key] = kv.Value.OnMouseDown;

				var id = kv.Key;
				kv.Value.OnMouseDown = _ =>
				{
					dragId = id;
					dragStart = Viewport.LastMousePos;
					dragMoved = false;
				};
				kv.Value.OnClick = () =>
				{
					if (!dragMoved)
						ToggleSlot(id);
				};
			}

			ApplyLayout(true);
		}

		void ExitEditMode(bool save)
		{
			editMode = false;
			dragId = null;
			dragMoved = false;

			foreach (var kv in buttons)
			{
				if (originalDisabled.TryGetValue(kv.Key, out var disabled))
					kv.Value.IsDisabled = disabled;
				if (originalClicks.TryGetValue(kv.Key, out var click))
					kv.Value.OnClick = click;
				if (originalMouseDowns.TryGetValue(kv.Key, out var down))
					kv.Value.OnMouseDown = down;
			}

			originalDisabled.Clear();
			originalClicks.Clear();
			originalMouseDowns.Clear();

			if (save)
				CommandBarPreferences.Save(visibleOrder, expanded);

			ApplyLayout(false);
		}

		void ToggleSlot(string id)
		{
			if (visibleOrder.Contains(id))
			{
				if (!CommandBarLayoutPolicy.CanHide(id, UseTouchLayout))
					return;

				if (visibleOrder.Count <= 1)
					return;
				visibleOrder.Remove(id);
			}
			else
				visibleOrder.Add(id);

			ApplyLayout(true);
		}

		IEnumerable<string> HiddenIds()
		{
			return CommandBarLayoutPolicy.AvailableIds(CommandBarCatalog.All.Select(s => s.Id), UseTouchLayout)
				.Where(id => buttons.ContainsKey(id) && !visibleOrder.Contains(id));
		}

		static readonly string[] DesktopCompactIds =
		{
			"ATTACK_MOVE", "FORCE_MOVE", "FORCE_ATTACK", "GUARD",
			"DEPLOY", "SCATTER", "REPAIR", "AUTO_REPAIR", "SELL", "BEACON", "STOP", "QUEUE_ORDERS",
			"STANCE_ATTACKANYTHING", "STANCE_DEFEND", "STANCE_RETURNFIRE", "STANCE_HOLDFIRE"
		};

		static readonly string[] TouchCompactIds =
		{
			"CTRL_TOGGLE", "GROUP_01", "GROUP_02", "GROUP_03", "GROUP_04", "GROUP_05", "PRODUCTION_X5",
			"REPAIR", "AUTO_REPAIR", "SELL", "BEACON", "STOP", "CYCLE_BASE"
		};

		static bool UseTouchLayout => Platform.UsesMobileLayout;
		static int CompactButtonHeight => UseTouchLayout ? TouchCompactButtonSize : DesktopCompactButtonHeight;
		static int CompactButtonWidth => UseTouchLayout ? TouchCompactButtonSize : DesktopCompactButtonWidth;
		static int CollapseButtonWidth => UseTouchLayout ? TouchCompactButtonSize : DesktopCollapseButtonWidth;

		public static string PanelBackgroundFor(bool isIos, bool expanded, string skin) =>
			isIos ? expanded ? TouchFactionSkin.QuickbarPanelCollection(skin, true) : string.Empty :
			expanded ? "commandbar-panel" : "commandbar-panel-compact";

		public static string ButtonBackgroundFor(bool isIos, string desktopBackground, string skin) =>
			isIos ? string.Empty : desktopBackground;

		public static string TouchSlotBackgroundFor(
			bool isIos, string id, string desktopBackground, string skin) =>
			ButtonBackgroundFor(isIos, desktopBackground, skin);

		public static string TouchCtrlBackgroundFor(bool enabled, string skin) =>
			string.Empty; // Normal/latched chrome is part of the circular icon itself.

		public static string TouchIconCollectionFor(string id, string skin) =>
			id == "STOP" || id == "CYCLE_BASE" ? "mobile-quickbar-actions-v5" :
			id == "CTRL_TOGGLE" ? TouchFactionSkin.ActionCollection(skin).Replace("actions-", "ctrl-", StringComparison.Ordinal) :
			TouchFactionSkin.CommandGlyphCollection(skin).Replace("ios-commandbar-glyphs-", "mobile-commandbar-glyphs-", StringComparison.Ordinal) + "-v5";

		public static string ToggleCollectionFor(bool isIos, string desktopCollection, string skin) =>
			isIos ? TouchFactionSkin.QuickbarToggleCollection(skin) : desktopCollection;

		public static bool ShouldRefreshTouchChrome(
			bool isIos, string previousSkin, bool previousExpanded, string skin, bool expanded) =>
			isIos && (previousSkin != skin || previousExpanded != expanded);

		public static bool ShouldRefreshTouchLayout(
			bool isIos,
			bool initialized,
			Size previousResolution,
			Size previousNativePointSize,
			Rectangle previousSafeBounds,
			IosScreenSnapshot snapshot) =>
			isIos && (!initialized ||
				previousResolution != snapshot.EffectiveSize ||
				previousNativePointSize != snapshot.NativePointSize ||
				previousSafeBounds != snapshot.SafeBounds);

		public static void ApplyTouchChromeToTargets<T>(
			string skin,
			bool expanded,
			IEnumerable<T> buttons,
			Action<string> applyPanelBackground,
			Action<T, string> applyButtonBackground,
			Action<string> applyToggleCollection)
		{
			if (buttons == null)
				throw new ArgumentNullException(nameof(buttons));
			if (applyPanelBackground == null)
				throw new ArgumentNullException(nameof(applyPanelBackground));
			if (applyButtonBackground == null)
				throw new ArgumentNullException(nameof(applyButtonBackground));
			if (applyToggleCollection == null)
				throw new ArgumentNullException(nameof(applyToggleCollection));

			applyPanelBackground(PanelBackgroundFor(true, expanded, skin));
			var buttonBackground = string.Empty;
			foreach (var button in buttons)
				applyButtonBackground(button, buttonBackground);
			applyToggleCollection(TouchFactionSkin.QuickbarToggleCollection(skin));
		}

		public static IEnumerable<T> TouchChromeTargets<T>(
			IEnumerable<T> slotButtons, T editButton, T collapseButton)
			where T : class
		{
			if (slotButtons == null)
				throw new ArgumentNullException(nameof(slotButtons));

			var targets = new List<T>(slotButtons);
			if (editButton != null)
				targets.Add(editButton);
			if (collapseButton != null)
				targets.Add(collapseButton);

			return targets;
		}

		int ButtonHeight => UseTouchLayout
			? TouchCommandBarSlotPolicy.Create(
				IosScreenMetrics.SnapshotFor(Game.Renderer.Resolution), "STOP", expanded).ButtonSize.Height
			: expanded ? ExpandedButtonHeight : CompactButtonHeight;

		int SlotWidth(string id)
		{
			return UseTouchLayout
				? TouchCommandBarSlotPolicy.Create(
					IosScreenMetrics.SnapshotFor(Game.Renderer.Resolution), id, expanded).ButtonSize.Width
				: expanded ? CommandBarCatalog.Get(id).Width : CompactButtonWidth;
		}

		IEnumerable<string> ActiveIds()
		{
			if (expanded)
				return CommandBarLayoutPolicy.AvailableIds(visibleOrder, UseTouchLayout);

			// Compact strip mirrors the classic unit-command bar.
			var compactIds = UseTouchLayout ? TouchCompactIds : DesktopCompactIds;
			var compact = compactIds.Where(id => buttons.ContainsKey(id) && visibleOrder.Contains(id)).ToList();
			if (compact.Count == 0)
				compact = compactIds.Where(id => buttons.ContainsKey(id)).ToList();

			return compact;
		}

		public override void Tick()
		{
			base.Tick();
			EnsureSetup();
			if (macDock != null)
			{
				macDock.Refresh();
				return;
			}
			if (setupDone)
				RefreshTouchChromeIfNeeded();
			if (setupDone && UseTouchLayout)
				RefreshTouchCtrlChrome();

			if (setupDone && UseTouchLayout)
			{
				var snapshot = IosScreenMetrics.SnapshotFor(Game.Renderer.Resolution);
				if (ShouldRefreshTouchLayout(
					true,
					touchLayoutInitialized,
					lastTouchResolution,
					lastTouchNativePointSize,
					lastTouchSafeBounds,
					snapshot) || lastTouchJoystickPoints != IosViewportControlsLayout.NormalizeJoystickPoints(Game.Settings.Game.IosVirtualJoystickSize))
					ApplyTouchLayout(snapshot, editMode);
			}

			if (!setupDone || !editMode || dragId == null)
				return;

			if (!buttons.TryGetValue(dragId, out var button) || !button.HasMouseFocus)
			{
				if (dragMoved)
					CommandBarPreferences.Save(visibleOrder, expanded);
				dragId = null;
				dragMoved = false;
				ApplyLayout(editMode);
				return;
			}

			var mouse = Viewport.LastMousePos;
			if (!dragMoved && (mouse - dragStart).Length <= EditDeadzone)
				return;

			dragMoved = true;
			var localX = mouse.X - RenderOrigin.X;
			var onActiveRow = UseTouchLayout ? touchActivePanelBounds.Any(rect => rect.Contains(mouse)) :
				mouse.Y < RenderOrigin.Y + Pad + ButtonHeight + RowGap / 2;

			if (onActiveRow)
			{
				visibleOrder.Remove(dragId);
				var insertAt = UseTouchLayout ? InsertIndexFromTouchPoint(mouse) : InsertIndexFromLocalX(localX);
				visibleOrder.Insert(insertAt, dragId);
			}
			else
			{
				if (!CommandBarLayoutPolicy.CanHide(dragId, UseTouchLayout))
				{
					ApplyLayout(true);
					return;
				}

				visibleOrder.Remove(dragId);
			}

			ApplyLayout(true, dragId);
			button.Bounds.X = Math.Max(Pad, mouse.X - RenderOrigin.X - button.Bounds.Width / 2);
			button.Bounds.Y = UseTouchLayout ? mouse.Y - RenderOrigin.Y - button.Bounds.Height / 2 :
				onActiveRow ? Pad : Pad + ButtonHeight + RowGap;
		}

		int InsertIndexFromTouchPoint(int2 point)
		{
			var row = touchActivePanelBounds.OrderBy(rect => Math.Abs(rect.Y + rect.Height / 2 - point.Y)).First();
			var after = visibleOrder.Count;
			for (var i = 0; i < visibleOrder.Count; i++)
			{
				if (!buttons.TryGetValue(visibleOrder[i], out var button) || !row.Contains(button.RenderBounds)) continue;
				if (point.X < button.RenderBounds.X + button.Bounds.Width / 2) return i;
				after = i + 1;
			}
			return after;
		}

		int InsertIndexFromLocalX(int localX)
		{
			var x = Pad;
			for (var i = 0; i < visibleOrder.Count; i++)
			{
				if (!buttons.ContainsKey(visibleOrder[i]))
					continue;

				var width = SlotWidth(visibleOrder[i]);
				if (localX < x + width / 2)
					return i;
				x += width + Gap;
			}

			return visibleOrder.Count;
		}

		IEnumerable<string> ActiveTouchIds()
		{
			if (expanded)
				return CommandBarLayoutPolicy.AvailableIds(visibleOrder, true)
					.OrderBy(id => id == "CYCLE_BASE" ? 1 : 0);
			return Array.Empty<string>();
		}

		IEnumerable<string> HiddenTouchIds()
		{
			return CommandBarLayoutPolicy.AvailableIds(CommandBarCatalog.All.Select(slot => slot.Id), true)
				.Where(id => buttons.ContainsKey(id) && !visibleOrder.Contains(id));
		}

		void ApplyTouchLayout(IosScreenSnapshot snapshot, bool editing, string skipId = null)
		{
			var pad = TouchCommandBarSlotPolicy.Metric(snapshot, Pad);
			var gap = TouchCommandBarSlotPolicy.Metric(snapshot, Gap);
			var rowGap = 2 * pad + TouchCommandBarSlotPolicy.Metric(snapshot, RowGap);
			var rowHeight = TouchCommandBarSlotPolicy.Create(snapshot, "STOP", true).ButtonSize.Height;
			var collapseWidth = rowHeight;
			var editWidth = 0;
			var safe = snapshot.SafeBounds;
			var clearance = TouchCommandBarSlotPolicy.Metric(snapshot, 8);
			var footerTop = safe.Bottom - TouchCommandBarSlotPolicy.Metric(snapshot, 12) - rowHeight - 2 * pad;
			// The viewport reserves one fixed footer even when the command strip is hidden.
			touchViewportObstacle = Rectangle.FromLTRB(safe.Left, footerTop, safe.Right, safe.Bottom);
			var joystickPoints = IosViewportControlsLayout.NormalizeJoystickPoints(Game.Settings?.Game?.IosVirtualJoystickSize ?? IosViewportControlsLayout.DefaultJoystickPoints);
			var viewport = IosViewportControlsLayout.Create(snapshot, joystickPoints, touchViewportObstacle);
			var left = viewport.JoystickBounds.X + viewport.JoystickBounds.Width / 2 - collapseWidth / 2 - pad;
			var right = Math.Min(safe.Right, IosIngameSidebarLayoutPolicy.Create(snapshot).TopBounds.Left - clearance);
			var upperLeft = Math.Max(left + pad, viewport.ActionsBounds.Right + clearance);
			var activeIds = ActiveTouchIds().ToArray();
			var rowWidth = right - left - 2 * pad;
			var fittedWidth = TouchCommandBarSlotPolicy.FitRowButtonSize(rowWidth - collapseWidth - gap, activeIds.Length, gap, rowHeight);
			var row = 0;
			var x = left + pad + collapseWidth + gap;
			var y = footerTop + pad;
			touchPanelBounds.Clear();
			touchPanelBounds.Add(new Rectangle(left, footerTop, collapseWidth + 2 * pad, rowHeight + 2 * pad));
			var positions = new Dictionary<string, Rectangle>();
			foreach (var button in buttons.Values) button.IsVisible = () => false;

			void NextRow()
			{
				row++;
				x = upperLeft;
				y = footerTop + pad - row * (rowHeight + rowGap);
				touchPanelBounds.Add(Rectangle.Empty);
			}

			void PlaceSlot(string id, bool active = false)
			{
				if (!buttons.TryGetValue(id, out var button)) return;
				var slot = TouchCommandBarSlotPolicy.Create(snapshot, id, true);
				var width = active ? fittedWidth : slot.ButtonSize.Width;
				if (!active && x + width + pad > right) NextRow();
				var iconSize = Math.Min(slot.IconBounds.Width, Math.Max(1, width - 2 * gap));
				slot = new TouchCommandBarSlotLayout(new Size(width, rowHeight),
					new Rectangle((width - iconSize) / 2, (rowHeight - iconSize) / 2, iconSize, iconSize),
					slot.LabelBounds, slot.Font, slot.ShowLabel);
				var rect = new Rectangle(x, y, width, rowHeight);
				positions[id] = rect;
				button.IsVisible = () => true;
				var panel = new Rectangle(x - pad, y - pad, rect.Width + 2 * pad, rect.Height + 2 * pad);
				touchPanelBounds[row] = touchPanelBounds[row].IsEmpty ? panel : Rectangle.Union(touchPanelBounds[row], panel);
				x += slot.ButtonSize.Width + gap;
				LayoutTouchSlotChrome(id, slot);
			}

			foreach (var id in activeIds) PlaceSlot(id, true);
			touchActivePanelBounds.Clear();
			if (expanded) touchActivePanelBounds.AddRange(touchPanelBounds);
			if (editing && expanded)
			{
				NextRow();
				foreach (var id in HiddenTouchIds()) PlaceSlot(id);
			}

			var placed = touchPanelBounds.Where(rect => !rect.IsEmpty).Aggregate(Rectangle.Union);
			var hintHeight = TouchCommandBarSlotPolicy.Metric(snapshot, 16);
			var hintTop = placed.Top - hintHeight - gap;
			if (editing && expanded && editHint != null)
				placed = Rectangle.Union(placed, new Rectangle(upperLeft, hintTop, right - upperLeft, hintHeight));
			var parentOrigin = Parent == null ? int2.Zero : Parent.ChildOrigin;
			Bounds = new WidgetBounds(placed.X - parentOrigin.X, placed.Y - parentOrigin.Y, placed.Width, placed.Height);
			foreach (var pair in positions)
			{
				var button = buttons[pair.Key];
				var rect = pair.Value;
				if (pair.Key != skipId) button.Bounds = new WidgetBounds(rect.X - placed.X, rect.Y - placed.Y, rect.Width, rect.Height);
			}

			if (collapseButton != null)
			{
				collapseButton.Bounds = new WidgetBounds(left + pad - placed.X, footerTop + pad - placed.Y, collapseWidth, rowHeight);
				collapseButton.IsVisible = () => true;
				if (collapseIcon != null)
				{
					var iconSize = TouchCommandBarSlotPolicy.Metric(snapshot, snapshot.IsCompactPhone ? 32 : 36);
					collapseIcon.Bounds = new WidgetBounds((collapseWidth - iconSize) / 2, (rowHeight - iconSize) / 2, iconSize, iconSize);
					collapseIcon.StretchToFit = true;
				}
			}

			if (editButton != null)
			{
				editButton.Bounds = new WidgetBounds(left + pad + collapseWidth + gap - placed.X, footerTop + pad - placed.Y, editWidth, rowHeight);
				editButton.IsVisible = () => false;
				if (editIcon != null)
				{
					var iconSize = TouchCommandBarSlotPolicy.Metric(snapshot, 36);
					editIcon.Bounds = new WidgetBounds((editWidth - iconSize) / 2, (rowHeight - iconSize) / 2, iconSize, iconSize);
					editIcon.StretchToFit = true;
				}
			}

			if (editHint != null)
			{
				editHint.IsVisible = () => editing && expanded;
				editHint.Bounds = new WidgetBounds(upperLeft - placed.X, hintTop - placed.Y, right - upperLeft, hintHeight);
			}
			var slots = GetOrNull("COMMAND_SLOTS");
			if (slots != null)
			{
				slots.IsVisible = () => expanded;
				slots.Bounds = new WidgetBounds(0, 0, placed.Width, placed.Height);
			}
			touchLayoutInitialized = true;
			lastTouchResolution = snapshot.EffectiveSize;
			lastTouchNativePointSize = snapshot.NativePointSize;
			lastTouchSafeBounds = snapshot.SafeBounds;
			lastTouchJoystickPoints = joystickPoints;
		}

		void ApplyLayout(bool editing, string skipId = null)
		{
			if (UseTouchLayout)
			{
				RefreshTouchChromeIfNeeded();
				ApplyTouchLayout(IosScreenMetrics.SnapshotFor(Game.Renderer.Resolution), editing, skipId);
				return;
			}

			Background = PanelBackgroundFor(false, expanded, null);

			foreach (var kv in buttons)
				kv.Value.IsVisible = () => false;

			var x = Pad;
			foreach (var id in ActiveIds())
			{
				if (!buttons.TryGetValue(id, out var button))
					continue;

				var width = SlotWidth(id);
				button.IsVisible = () => true;
				if (id != skipId)
				{
					button.Bounds.X = x;
					button.Bounds.Y = Pad;
				}

				button.Bounds.Width = width;
				button.Bounds.Height = ButtonHeight;
				LayoutSlotChrome(id, width);
				x += width + Gap;
			}

			var controlsWidth = CollapseButtonWidth + Gap + (expanded ? EditButtonWidth + Gap : 0);
			var activeWidth = x + Pad + controlsWidth;

			var trayX = Pad;
			if (editing && expanded)
			{
				foreach (var id in HiddenIds())
				{
					if (!buttons.TryGetValue(id, out var button))
						continue;

					var width = SlotWidth(id);
					button.IsVisible = () => true;
					if (id != skipId)
					{
						button.Bounds.X = trayX;
						button.Bounds.Y = Pad + ButtonHeight + RowGap;
					}

					button.Bounds.Width = width;
					button.Bounds.Height = ButtonHeight;
					LayoutSlotChrome(id, width);
					trayX += width + Gap;
				}
			}

			var trayWidth = editing && expanded ? trayX + Pad : 0;
			var minWidth = expanded ? 360 : 220;
			var totalWidth = Math.Max(Math.Max(activeWidth, trayWidth), minWidth);
			var totalHeight = editing && expanded
				? Pad + ButtonHeight + RowGap + ButtonHeight + Pad + 16
				: Pad + ButtonHeight + Pad;

			Bounds.Width = totalWidth;
			Bounds.Height = totalHeight;
			Bounds.X = (Game.Renderer.Resolution.Width - totalWidth) / 2;
			Bounds.Y = Game.Renderer.Resolution.Height - totalHeight - 12;

			if (collapseButton != null)
			{
				collapseButton.Bounds.Width = CollapseButtonWidth;
				collapseButton.Bounds.Height = UseTouchLayout ? TouchCompactButtonSize : Math.Min(34, ButtonHeight);
				collapseButton.Bounds.X = totalWidth - Pad - CollapseButtonWidth;
				collapseButton.Bounds.Y = Pad + Math.Max(0, (ButtonHeight - collapseButton.Bounds.Height) / 2);
				collapseButton.IsVisible = () => true;
				if (collapseIcon != null)
				{
					collapseIcon.Bounds.X = (collapseButton.Bounds.Width - 26) / 2;
					collapseIcon.Bounds.Y = (collapseButton.Bounds.Height - 26) / 2;
				}
			}

			if (editButton != null)
			{
				editButton.Bounds.Width = EditButtonWidth;
				editButton.Bounds.Height = 34;
				editButton.Bounds.X = totalWidth - Pad - CollapseButtonWidth - Gap - EditButtonWidth;
				editButton.Bounds.Y = Pad + Math.Max(0, (ButtonHeight - 34) / 2);
				editButton.IsVisible = () => expanded;
			}

			if (editHint != null)
			{
				editHint.IsVisible = () => editing && expanded;
				editHint.Bounds.X = Pad;
				editHint.Bounds.Y = totalHeight - 18;
				editHint.Bounds.Width = Math.Max(0, totalWidth - 2 * Pad);
				editHint.Bounds.Height = 16;
			}

			var slots = GetOrNull("COMMAND_SLOTS");
			if (slots != null)
			{
				slots.Bounds.X = 0;
				slots.Bounds.Y = 0;
				slots.Bounds.Width = totalWidth;
				slots.Bounds.Height = totalHeight;
			}
		}

		void RefreshTouchChromeIfNeeded()
		{
			RefreshTouchChrome(UseTouchLayout);
		}

		void RefreshTouchChrome(bool isIos)
		{
			if (!isIos)
				return;

			var skin = TouchFactionSkin.Active;
			if (ShouldRefreshTouchChrome(true, lastTouchSkin, lastTouchExpanded, skin, expanded))
				ApplyTouchChrome();
		}

		IEnumerable<ButtonWidget> TouchChromeButtons()
		{
			return TouchChromeTargets(buttons.Values, editButton, collapseButton);
		}

		void ApplyTouchChrome()
		{
			var skin = TouchFactionSkin.Active;
			ApplyTouchChromeToTargets(
				skin,
				expanded,
				TouchChromeButtons(),
				background => Background = background,
				(button, background) => button.Background = background,
				collection =>
				{
					if (collapseIcon != null)
						collapseIcon.ImageCollection = "mobile-quickbar-actions-v5";
				});

			foreach (var target in TouchChromeButtons()) target.DisableKeySound = false;

			foreach (var pair in icons)
			{
				if (pair.Value == null || !buttons.TryGetValue(pair.Key, out var button))
					continue;

				pair.Value.ImageCollection = TouchIconCollectionFor(pair.Key, skin);
				WidgetUtils.BindButtonIcon(button);
			}

			RefreshTouchCtrlChrome(true);

			lastTouchSkin = skin;
			lastTouchExpanded = expanded;
		}

		void RefreshTouchCtrlChrome(bool force = false)
		{
			var enabled = TouchModifierOverride.Enabled;
			if (!force && lastTouchCtrlEnabled == enabled)
				return;

			if (buttons.TryGetValue("CTRL_TOGGLE", out var ctrlToggle))
				ctrlToggle.Background = TouchCtrlBackgroundFor(enabled, TouchFactionSkin.Active);

			lastTouchCtrlEnabled = enabled;
		}

		void LayoutTouchSlotChrome(string id, TouchCommandBarSlotLayout slot)
		{
			if (labels.TryGetValue(id, out var label) && label != null)
			{
				var showLabel = slot.ShowLabel;
				label.IsVisible = () => showLabel;
				label.Bounds.X = slot.LabelBounds.X;
				label.Bounds.Y = slot.LabelBounds.Y;
				label.Bounds.Width = slot.LabelBounds.Width;
				label.Bounds.Height = slot.LabelBounds.Height;
				label.Font = slot.Font;
			}

			if (icons.TryGetValue(id, out var icon) && icon != null)
			{
				icon.Bounds.X = slot.IconBounds.X;
				icon.Bounds.Y = slot.IconBounds.Y;
				icon.Bounds.Width = slot.IconBounds.Width;
				icon.Bounds.Height = slot.IconBounds.Height;
				icon.StretchToFit = true;
			}
		}

		void LayoutSlotChrome(string id, int width)
		{
			if (labels.TryGetValue(id, out var label) && label != null)
			{
				var showLabel = expanded;
				label.IsVisible = () => showLabel;
				label.Bounds.Y = 36;
				label.Bounds.Width = width;
				label.Bounds.Height = 14;
			}

			if (icons.TryGetValue(id, out var icon) && icon != null)
			{
				icon.Bounds.X = Math.Max(0, (width - 26) / 2);
				icon.Bounds.Y = expanded ? 6 : Math.Max(1, (CompactButtonHeight - 26) / 2);
				icon.Bounds.Width = 26;
				icon.Bounds.Height = 26;
				icon.StretchToFit = true;
			}
		}

		public override bool HandleMouseInput(MouseInput mi)
		{
			EnsureSetup();

			if (editMode && expanded && mi.Button == MouseButton.Right && mi.Event == MouseInputEvent.Up)
			{
				var id = HitTestSlotId(mi.Location);
				if (id != null)
				{
					ToggleSlot(id);
					return true;
				}
			}

			return UseTouchLayout && touchLayoutInitialized
				? expanded && touchPanelBounds.Any(rect => rect.Contains(mi.Location))
				: base.HandleMouseInput(mi);
		}

		public override bool EventBoundsContains(int2 location)
		{
			if (!UseTouchLayout || !touchLayoutInitialized) return base.EventBoundsContains(location);
			return collapseButton?.RenderBounds.Contains(location) == true ||
				expanded && touchPanelBounds.Any(rect => rect.Contains(location));
		}

		public override bool HandleKeyPress(KeyInput input) => macDock?.Key(input) == true || base.HandleKeyPress(input);

		public override void Removed()
		{
			macDock?.Removed();
			base.Removed();
		}

		public override void Draw()
		{
			EnsureSetup();
			if (macDock != null)
				macDock.Draw();
			else if (UseTouchLayout && touchLayoutInitialized)
			{
				foreach (var rect in touchPanelBounds)
					if (!rect.IsEmpty) WidgetUtils.DrawPanel(Background, rect);
			}
			else
				base.Draw();
		}

		string HitTestSlotId(int2 screenPos)
		{
			foreach (var kv in buttons)
			{
				if (!kv.Value.IsVisible())
					continue;
				if (kv.Value.RenderBounds.Contains(screenPos))
					return kv.Key;
			}

			return null;
		}
	}
}
