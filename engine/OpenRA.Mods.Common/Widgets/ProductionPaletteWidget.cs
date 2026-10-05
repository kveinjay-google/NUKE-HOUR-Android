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
using OpenRA.Graphics;
using OpenRA.Mods.Common.Lint;
using OpenRA.Mods.Common.Orders;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Common.Traits.Render;
using OpenRA.Network;
using OpenRA.Primitives;
using OpenRA.Support;
using OpenRA.Widgets;

namespace OpenRA.Mods.Common.Widgets
{
	public class ProductionIcon
	{
		public ActorInfo Actor;
		public string Name;
		public HotkeyReference Hotkey;
		public Sprite Sprite;
		public PaletteReference Palette;
		public PaletteReference IconClockPalette;
		public PaletteReference IconDarkenPalette;
		public float2 Pos;
		public List<ProductionItem> Queued;
		public ProductionQueue ProductionQueue;
	}

	public static class ProductionPaletteMergePolicy
	{
		public static (TItem Item, TQueue Queue)[] MergeDistinct<TQueue, TItem>(
			IEnumerable<TQueue> queues,
			Func<TQueue, IEnumerable<TItem>> items,
			Func<TItem, string> key,
			Func<TItem, int> order)
		{
			var seen = new HashSet<string>(StringComparer.Ordinal);
			var merged = new List<(TItem Item, TQueue Queue)>();
			foreach (var queue in queues)
				foreach (var item in items(queue).OrderBy(order))
					if (seen.Add(key(item)))
						merged.Add((item, queue));

			return merged.ToArray();
		}
	}

	public class ProductionPaletteWidget : Widget, IWidgetTouchGestureTarget
	{
		public enum ReadyTextStyleOptions { Solid, AlternatingColor, Blinking }
		public readonly ReadyTextStyleOptions ReadyTextStyle = ReadyTextStyleOptions.AlternatingColor;
		public readonly Color TextColor = Color.White;
		public readonly Color ReadyTextAltColor = Color.Gold;
		public int Columns = 3;
		public int2 IconSize = new(64, 48);
		public int2 IconMargin = int2.Zero;
		public readonly int2 IconSpriteOffset = int2.Zero;
		public readonly int SwipeThreshold = 36;
		public readonly int TapSlop = 12;
		public readonly int TouchScrollStep = 24;

		public readonly float2 QueuedOffset = new(4, 2);
		public readonly TextAlign QueuedTextAlign = TextAlign.Left;

		public readonly string ClickSound = ChromeMetrics.Get<string>("ClickSound");
		public readonly string ClickDisabledSound = ChromeMetrics.Get<string>("ClickDisabledSound");
		public readonly string TooltipContainer;
		public readonly string TooltipTemplate = "PRODUCTION_TOOLTIP";

		// Note: LinterHotkeyNames assumes that these are disabled by default
		public readonly string HotkeyPrefix = null;
		public readonly int HotkeyCount = 0;
		public readonly HotkeyReference SelectProductionBuildingHotkey = new();

		public readonly string ClockAnimation = "clock";
		public readonly string ClockSequence = "idle";
		public readonly string ClockPalette = "chrome";

		public readonly string NotBuildableAnimation = "clock";
		public readonly string NotBuildableSequence = "idle";
		public readonly string NotBuildablePalette = "chrome";

		public readonly string OverlayFont = "TinyBold";
		public readonly string SymbolsFont = "Symbols";

		public readonly bool DrawTime = true;

		[FluentReference]
		public string ReadyText = "";

		[FluentReference]
		public string HoldText = "";

		public readonly string InfiniteSymbol = "\u221E";

		public int DisplayedIconCount { get; private set; }
		public int TotalIconCount { get; private set; }
		public int BuildCountMultiplier { get; set; } = 1;
		public event Action<int, int> OnIconCountChanged = (a, b) => { };
		public Func<ProductionSwipeDirection, bool> OnSwipeProductionType = direction => false;

		public ProductionIcon TooltipIcon { get; private set; }
		public Func<ProductionIcon> GetTooltipIcon;
		public readonly World World;
		readonly ModData modData;
		readonly OrderManager orderManager;
		readonly HashSet<string> buildableNames = new(StringComparer.Ordinal);

		public int MinimumRows = 4;
		public int MaximumRows = int.MaxValue;

		public int IconRowOffset = 0;
		public int MaxIconRowOffset = int.MaxValue;

		readonly Lazy<TooltipContainerWidget> tooltipContainer;
		ProductionQueue currentQueue;
		List<ProductionQueue> currentQueues = new();
		HotkeyReference[] hotkeys;

		public ProductionQueue CurrentQueue
		{
			get => currentQueue;
			set => SetCurrentQueues(value == null ? Array.Empty<ProductionQueue>() : new[] { value });
		}

		public IReadOnlyList<ProductionQueue> CurrentQueues => currentQueues;

		public void SetCurrentQueues(IEnumerable<ProductionQueue> queues)
		{
			currentQueues = queues?.Where(queue => queue != null).Distinct().ToList() ?? new List<ProductionQueue>();
			if (selectedTouchQueue != null && !currentQueues.Contains(selectedTouchQueue))
			{
				selectedTouchQueue = null;
				selectedTouchItem = null;
			}
			currentQueue = currentQueues.FirstOrDefault();
			if (currentQueue != null)
				UpdateCachedProductionIconOverlays();

			RefreshIcons();
		}

		public override Rectangle EventBounds
		{
			get
			{
				if (!Platform.UsesMobileLayout)
					return eventBounds;

				var rb = RenderBounds;
				return new Rectangle(rb.X, rb.Y,
					Columns * (IconSize.X + IconMargin.X) - IconMargin.X,
					MaximumRows * (IconSize.Y + IconMargin.Y) - IconMargin.Y);
			}
		}

		Dictionary<Rectangle, ProductionIcon> icons = new();
		Animation cantBuild;
		Animation clock;
		Rectangle eventBounds = Rectangle.Empty;

		readonly WorldRenderer worldRenderer;

		SpriteFont overlayFont, symbolFont;
		float2 iconOffset, holdOffset, readyOffset, timeOffset, infiniteOffset;

		Player cachedQueueOwner;
		IProductionIconOverlay[] pios;
		int2? touchStart;
		ProductionIcon touchIcon;
		MouseButton touchButton;
		Modifiers touchModifiers;
		bool touchExceededTapSlop;
		ProductionSwipeAxis touchAxis;
		int touchAppliedRowOffset;
		bool touchSingleColumnSwipeApplied;
		ProductionQueue selectedTouchQueue;
		string selectedTouchItem;
		ProductionQueue touchOriginQueue;
		string touchOriginItem;
		bool gridLayoutChanged;
		int EffectiveTouchScrollStep => Math.Max(TouchScrollStep, IconSize.Y / 3);

		[CustomLintableHotkeyNames]
		public static IEnumerable<string> LinterHotkeyNames(MiniYamlNode widgetNode, Action<string> emitError)
		{
			var prefix = "";
			var prefixNode = widgetNode.Value.NodeWithKeyOrDefault("HotkeyPrefix");
			if (prefixNode != null)
				prefix = prefixNode.Value.Value;

			var count = 0;
			var countNode = widgetNode.Value.NodeWithKeyOrDefault("HotkeyCount");
			if (countNode != null)
				count = FieldLoader.GetValue<int>("HotkeyCount", countNode.Value.Value);

			if (count == 0)
				return Array.Empty<string>();

			if (string.IsNullOrEmpty(prefix))
				emitError($"{widgetNode.Location} must define HotkeyPrefix if HotkeyCount > 0.");

			return Exts.MakeArray(count, i => prefix + (i + 1).ToStringInvariant("D2"));
		}

		[ObjectCreator.UseCtor]
		public ProductionPaletteWidget(ModData modData, OrderManager orderManager, World world, WorldRenderer worldRenderer)
		{
			this.modData = modData;
			this.orderManager = orderManager;
			World = world;
			this.worldRenderer = worldRenderer;
			GetTooltipIcon = () => TooltipIcon;
			tooltipContainer = Exts.Lazy(() =>
				Ui.Root.Get<TooltipContainerWidget>(TooltipContainer));
		}

		public override void Initialize(WidgetArgs args)
		{
			base.Initialize(args);

			clock = new Animation(World, ClockAnimation);
			cantBuild = new Animation(World, NotBuildableAnimation);
			cantBuild.PlayFetchIndex(NotBuildableSequence, () => 0);
			hotkeys = Exts.MakeArray(HotkeyCount,
				i => modData.Hotkeys[HotkeyPrefix + (i + 1).ToStringInvariant("D2")]);

			overlayFont = Game.Renderer.Fonts[OverlayFont];
			Game.Renderer.Fonts.TryGetValue(SymbolsFont, out symbolFont);

			HoldText = FluentProvider.GetMessage(HoldText);
			ReadyText = FluentProvider.GetMessage(ReadyText);
			UpdateIconOffsets();

			if (ChromeMetrics.TryGet("InfiniteOffset", out infiniteOffset))
				infiniteOffset += QueuedOffset;
			else
				infiniteOffset = QueuedOffset;
		}

		void UpdateIconOffsets()
		{
			// The legacy -1px cameo nudge must not shift enlarged cards onto their rim.
			iconOffset = 0.5f * IconSize.ToFloat2() + (Columns == 1 ? float2.Zero : IconSpriteOffset);
			holdOffset = iconOffset - overlayFont.Measure(HoldText) / 2;
			readyOffset = iconOffset - overlayFont.Measure(ReadyText) / 2;
		}

		public bool ExternalSidebarLayout;

		public bool ApplyGridLayout(int columns, int2 iconSize, int2 iconMargin)
		{
			columns = Math.Max(1, columns);
			iconSize = new int2(Math.Max(1, iconSize.X), Math.Max(1, iconSize.Y));
			iconMargin = new int2(Math.Max(0, iconMargin.X), Math.Max(0, iconMargin.Y));
			if (Columns == columns && IconSize == iconSize && IconMargin == iconMargin)
				return false;

			Columns = columns;
			gridLayoutChanged = true;
			IconSize = iconSize;
			IconMargin = iconMargin;
			IconRowOffset = 0;
			if (overlayFont != null)
				UpdateIconOffsets();
			return true;
		}

		public void ScrollDown()
		{
			if (CanScrollDown)
				IconRowOffset++;
		}

		public bool CanScrollDown
		{
			get
			{
				var totalRows = (TotalIconCount + Columns - 1) / Columns;

				return IconRowOffset < totalRows - MaxIconRowOffset;
			}
		}

		public void ScrollUp()
		{
			if (CanScrollUp)
				IconRowOffset--;
		}

		public bool CanScrollUp => IconRowOffset > 0;

		public void ScrollToTop()
		{
			IconRowOffset = 0;
		}

		public IEnumerable<ActorInfo> AllBuildables
		{
			get
			{
				return AllBuildableEntries().Select(entry => entry.Item);
			}
		}

		(ActorInfo Item, ProductionQueue Queue)[] AllBuildableEntries() =>
			ProductionPaletteMergePolicy.MergeDistinct(
				currentQueues,
				queue => queue.AllItems(),
				item => item.Name,
				item => item.TraitInfo<BuildableInfo>().BuildPaletteOrder);

		(ActorInfo Item, ProductionQueue Queue)[] DisplayBuildableEntries() =>
			AllBuildableEntries()
				.Where(entry => entry.Queue.MostLikelyProducer().Trait != null)
				.ToArray();

		public override void Tick()
		{
			TotalIconCount = DisplayBuildableEntries().Length;

			if (currentQueues.Any(queue => !queue.Actor.IsInWorld))
				SetCurrentQueues(currentQueues.Where(queue => queue.Actor.IsInWorld));

			if (CurrentQueue != null)
			{
				if (CurrentQueue.Actor.Owner != cachedQueueOwner)
					UpdateCachedProductionIconOverlays();

				RefreshIcons();
			}
			else if (gridLayoutChanged)
				RefreshIcons();
		}

		public override void MouseEntered()
		{
			if (TooltipContainer != null)
				tooltipContainer.Value.SetTooltip(TooltipTemplate,
					new WidgetArgs() { { "player", World.LocalPlayer }, { "getTooltipIcon", GetTooltipIcon }, { "world", World } });
		}

		public override void MouseExited()
		{
			if (TooltipContainer != null)
				tooltipContainer.Value.RemoveTooltip();
		}

		public override bool HandleMouseInput(MouseInput mi)
		{
			var icon = icons.Where(i => i.Key.Contains(mi.Location))
				.Select(i => i.Value).FirstOrDefault();

			if (Platform.UsesMobileLayout && (mi.Button == MouseButton.Left || HasMouseFocus))
			{
				if (mi.Event == MouseInputEvent.Down && mi.Button == MouseButton.Left)
				{
					if (!TakeMouseFocus(mi))
						return false;

					touchStart = mi.Location;
					touchIcon = icon;
					touchButton = mi.Button;
					touchModifiers = mi.Modifiers;
					touchExceededTapSlop = false;
					touchAxis = ProductionSwipeAxis.None;
					touchAppliedRowOffset = 0;
					touchSingleColumnSwipeApplied = false;
					return true;
				}

				if (HasMouseFocus && touchStart.HasValue && mi.Event == MouseInputEvent.Move)
				{
					TooltipIcon = null;
					var delta = mi.Location - touchStart.Value;
					if (!ProductionSwipePolicy.IsTap(delta, TapSlop))
						touchExceededTapSlop = true;

					if (touchAxis == ProductionSwipeAxis.None)
						touchAxis = ProductionSwipePolicy.ResolveAxis(delta, TapSlop + 1);

					if (touchAxis == ProductionSwipeAxis.Vertical)
						ApplyTouchVerticalScroll(delta.Y);

					return true;
				}

				if (HasMouseFocus && touchStart.HasValue && mi.Event == MouseInputEvent.Up)
				{
					var delta = mi.Location - touchStart.Value;
					var axis = touchAxis == ProductionSwipeAxis.None ?
						ProductionSwipePolicy.ResolveAxis(delta, TapSlop + 1) : touchAxis;
					if (axis == ProductionSwipeAxis.Vertical)
						ApplyTouchVerticalScroll(delta.Y);

					var direction = axis == ProductionSwipeAxis.Horizontal ?
						ProductionSwipePolicy.Resolve(delta, SwipeThreshold) : ProductionSwipeDirection.None;
					var tappedIcon = touchIcon;
					var tappedButton = touchButton;
					var tappedModifiers = touchModifiers;
					var wasTap = !touchExceededTapSlop && ProductionSwipePolicy.IsTap(delta, TapSlop);
					var wasVerticalDrag = axis == ProductionSwipeAxis.Vertical;

					ResetTouchGesture();
					YieldMouseFocus(mi);

					if (wasVerticalDrag)
						return true;

					if (direction != ProductionSwipeDirection.None)
					{
						var changed = OnSwipeProductionType(direction);
						if (changed)
							Game.Sound.PlayNotification(World.Map.Rules, World.LocalPlayer, "Sounds", ClickSound, null);

						return true;
					}

					if (!wasTap)
						return true;

					return tappedIcon != null && HandleEvent(tappedIcon, tappedButton, tappedModifiers);
				}

				if (HasMouseFocus)
					return true;
			}

			if (mi.Event == MouseInputEvent.Move)
				TooltipIcon = icon;

			if (mi.Event == MouseInputEvent.Scroll)
			{
				if (mi.Delta.Y < 0 && CanScrollDown)
				{
					ScrollDown();
					Ui.ResetTooltips();
					Game.Sound.PlayNotification(World.Map.Rules, World.LocalPlayer, "Sounds", ClickSound, null);
				}
				else if (mi.Delta.Y > 0 && CanScrollUp)
				{
					ScrollUp();
					Ui.ResetTooltips();
					Game.Sound.PlayNotification(World.Map.Rules, World.LocalPlayer, "Sounds", ClickSound, null);
				}
			}

			if (icon == null)
				return false;

			// Eat mouse-up events
			if (mi.Event != MouseInputEvent.Down)
				return true;

			return HandleEvent(icon, mi.Button, mi.Modifiers);
		}

		public bool AcceptsTouch(int2 position)
		{
			if (!Platform.UsesMobileLayout || World.LocalPlayer == null || !EventBounds.Contains(position))
				return false;
			for (Widget widget = this; widget != Ui.Root; widget = widget.Parent)
				if (!widget.IsVisible() || widget.Parent == null || !widget.Parent.Children.Contains(widget))
					return false;
			return true;
		}

		public void BeginTouch(int2 position)
		{
			var icon = icons.FirstOrDefault(entry => entry.Key.Contains(position)).Value;
			touchOriginQueue = icon?.ProductionQueue;
			touchOriginItem = icon?.Name;
		}

		ProductionIcon LongPressCancellationTarget(int2 origin, int2 position)
		{
			var first = icons.FirstOrDefault(entry => entry.Key.Contains(origin)).Value;
			var last = icons.FirstOrDefault(entry => entry.Key.Contains(position)).Value;
			if (touchOriginQueue == null || first == null || last == null ||
				first.Name != touchOriginItem || last.Name != touchOriginItem ||
				first.ProductionQueue != touchOriginQueue || last.ProductionQueue != touchOriginQueue)
				return null;

			return first;
		}

		public void HandleTouchLongPress(int2 origin, int2 position)
		{
			if (!AcceptsTouch(origin) || !AcceptsTouch(position))
				return;

			var icon = LongPressCancellationTarget(origin, position);
			if (icon == null)
				return;

			selectedTouchQueue = icon.ProductionQueue;
			selectedTouchItem = icon.Name;
			CancelProductionItem(selectedTouchQueue, selectedTouchItem);
		}

		public void HandleTouchPinch(int direction)
		{
			if (!Platform.UsesMobileLayout || direction == 0)
				return;
			var mode = direction > 0 ? IosProductionPaletteMode.LargeSingleColumn : IosProductionPaletteMode.CompactThreeColumns;
			if (Game.Settings.Game.IosProductionPaletteMode == mode)
				return;
			Game.Settings.Game.IosProductionPaletteMode = mode;
			Game.Settings.Save();
			Ui.ResetTooltips();
		}

		(ProductionQueue Queue, string Item) CancellationTarget()
		{
			bool Valid(ProductionQueue queue) => queue.Actor.IsInWorld && queue.Actor.Owner == World.LocalPlayer;
			foreach (var queue in currentQueues)
			{
				if (!Valid(queue))
					continue;
				var item = queue.CurrentItem();
				if (item != null)
					return (queue, item.Item);
			}
			return default;
		}

		public bool CanCancelSelectedProduction => Platform.UsesMobileLayout && World.LocalPlayer != null && CancellationTarget().Queue != null;

		public void CancelSelectedProduction()
		{
			if (!CanCancelSelectedProduction)
				return;
			var target = CancellationTarget();
			CancelProductionItem(target.Queue, target.Item);
		}

		Order CreateCancellationOrder(ProductionQueue queue, string name)
		{
			// Query live queue state: cameo objects are rebuilt and can be stale.
			if (queue == null || !currentQueues.Contains(queue) || !queue.Actor.IsInWorld ||
				queue.Actor.Owner != World.LocalPlayer || !queue.AllQueued().Any(item => item.Item == name))
				return null;

			return Order.CancelProduction(queue.Actor, name, 1);
		}

		void CancelProductionItem(ProductionQueue queue, string name)
		{
			var order = CreateCancellationOrder(queue, name);
			if (order == null)
				return;

			World.IssueOrder(order);
			Game.Sound.PlayNotification(World.Map.Rules, World.LocalPlayer, "Sounds", ClickSound, null);
			Game.Sound.PlayNotification(World.Map.Rules, World.LocalPlayer, "Speech", queue.Info.CancelledAudio, World.LocalPlayer.Faction.InternalName);
			TextNotificationsManager.AddTransientLine(World.LocalPlayer, queue.Info.CancelledTextNotification);
		}

		void ApplyTouchVerticalScroll(int deltaY)
		{
			var rows = ProductionSwipePolicy.ResolveVerticalRows(deltaY, EffectiveTouchScrollStep);
			if (Columns != 1)
			{
				ApplyTouchRowOffset(rows);
				return;
			}

			// One single-column gesture pages three items, regardless of drag length.
			if (touchSingleColumnSwipeApplied || rows == 0)
				return;

			touchSingleColumnSwipeApplied = true;
			ApplyTouchRowOffset(rows > 0 ? 3 : -3);
		}

		void ApplyTouchRowOffset(int desiredRowOffset)
		{
			var changed = false;
			while (touchAppliedRowOffset < desiredRowOffset && CanScrollDown)
			{
				ScrollDown();
				touchAppliedRowOffset++;
				changed = true;
			}

			while (touchAppliedRowOffset > desiredRowOffset && CanScrollUp)
			{
				ScrollUp();
				touchAppliedRowOffset--;
				changed = true;
			}

			if (changed)
				Ui.ResetTooltips();
		}

		void ResetTouchGesture()
		{
			touchStart = null;
			touchIcon = null;
			touchButton = MouseButton.None;
			touchModifiers = Modifiers.None;
			touchExceededTapSlop = false;
			touchAxis = ProductionSwipeAxis.None;
			touchAppliedRowOffset = 0;
			touchSingleColumnSwipeApplied = false;
		}

		public override bool YieldMouseFocus(MouseInput mi)
		{
			ResetTouchGesture();
			return base.YieldMouseFocus(mi);
		}

		protected bool PickUpCompletedBuildingIcon(ProductionItem item, ProductionQueue queue)
		{
			if (item == null)
				return false;

			var actor = World.Map.Rules.Actors[item.Item];

			if (item.Done && actor.HasTraitInfo<BuildingInfo>())
			{
				World.OrderGenerator = new PlaceBuildingOrderGenerator(queue, item.Item, worldRenderer);
				return true;
			}

			return false;
		}

		public void PickUpCompletedBuilding()
		{
			foreach (var queue in currentQueues)
				if (PickUpCompletedBuildingIcon(queue.CurrentItem(), queue))
					return;
		}

		bool HandleLeftClick(
			ProductionItem item, ProductionIcon icon, ProductionQueue queue, int handleCount, Modifiers modifiers)
		{
			if (PickUpCompletedBuildingIcon(item, queue))
			{
				Game.Sound.PlayNotification(World.Map.Rules, World.LocalPlayer, "Sounds", ClickSound, null);
				return true;
			}

			if (item != null && item.Paused)
			{
				// Resume a paused item
				Game.Sound.PlayNotification(World.Map.Rules, World.LocalPlayer, "Sounds", ClickSound, null);
				Game.Sound.PlayNotification(World.Map.Rules, World.LocalPlayer, "Speech", queue.Info.QueuedAudio, World.LocalPlayer.Faction.InternalName);
				TextNotificationsManager.AddTransientLine(World.LocalPlayer, queue.Info.QueuedTextNotification);

				World.IssueOrder(Order.PauseProduction(queue.Actor, icon.Name, false));
				return true;
			}

			var buildable = queue.BuildableItems().FirstOrDefault(a => a.Name == icon.Name);

			if (buildable != null)
			{
				if (queue.Info.PayUpFront &&
					queue.GetProductionCost(buildable) > queue.Actor.Owner.PlayerActor.Trait<PlayerResources>().GetCashAndResources())
					return false;
				Game.Sound.PlayNotification(World.Map.Rules, World.LocalPlayer, "Sounds", ClickSound, null);

				// Queue a new item
				var canQueue = queue.CanQueue(buildable, out var notification, out var textNotification);
				if (!canQueue && textNotification == PopulationMessages.Blocked)
				{
					// Touch input has no hover tooltip. Include counts and the blocking reason even for a nonempty queue.
					var status = World.WorldActor.TraitOrDefault<PopulationLimits>()?.Status(queue.Actor.Owner, buildable);
					TextNotificationsManager.AddFeedbackLine(PopulationMessages.Feedback, "status", status ?? "");
					return false;
				}


				if (!queue.AllQueued().Any())
				{
					Game.Sound.PlayNotification(World.Map.Rules, World.LocalPlayer, "Speech", notification, World.LocalPlayer.Faction.InternalName);
					TextNotificationsManager.AddTransientLine(World.LocalPlayer, textNotification);
				}

				if (canQueue)
				{
					var queued = !modifiers.HasModifier(Modifiers.Ctrl);
					World.IssueOrder(Order.StartProduction(queue.Actor, icon.Name, handleCount, queued));
					return true;
				}
			}

			return false;
		}

		bool HandleRightClick(ProductionItem item, ProductionIcon icon, ProductionQueue queue, int handleCount)
		{
			if (item == null)
				return false;

			Game.Sound.PlayNotification(World.Map.Rules, World.LocalPlayer, "Sounds", ClickSound, null);

			if (queue.Info.DisallowPaused || item.Paused || item.Done || item.TotalCost == item.RemainingCost || !item.Started)
			{
				// Instantly cancel items that haven't started, have finished, or if the queue doesn't support pausing
				Game.Sound.PlayNotification(World.Map.Rules, World.LocalPlayer, "Speech", queue.Info.CancelledAudio, World.LocalPlayer.Faction.InternalName);
				TextNotificationsManager.AddTransientLine(World.LocalPlayer, queue.Info.CancelledTextNotification);

				World.IssueOrder(Order.CancelProduction(queue.Actor, icon.Name, handleCount));
			}
			else
			{
				// Pause an existing item
				Game.Sound.PlayNotification(World.Map.Rules, World.LocalPlayer, "Speech", queue.Info.OnHoldAudio, World.LocalPlayer.Faction.InternalName);
				TextNotificationsManager.AddTransientLine(World.LocalPlayer, queue.Info.OnHoldTextNotification);

				World.IssueOrder(Order.PauseProduction(queue.Actor, icon.Name, true));
			}

			return true;
		}

		bool HandleMiddleClick(ProductionItem item, ProductionIcon icon, ProductionQueue queue, int handleCount)
		{
			if (item == null)
				return false;

			// Directly cancel, skipping "on-hold"
			Game.Sound.PlayNotification(World.Map.Rules, World.LocalPlayer, "Sounds", ClickSound, null);
			Game.Sound.PlayNotification(World.Map.Rules, World.LocalPlayer, "Speech", queue.Info.CancelledAudio, World.LocalPlayer.Faction.InternalName);
			TextNotificationsManager.AddTransientLine(World.LocalPlayer, queue.Info.CancelledTextNotification);

			World.IssueOrder(Order.CancelProduction(queue.Actor, icon.Name, handleCount));

			return true;
		}

		bool HandleEvent(ProductionIcon icon, MouseButton btn, Modifiers modifiers)
		{
			var queue = icon.ProductionQueue;
			if (Platform.UsesMobileLayout)
			{
				selectedTouchQueue = queue;
				selectedTouchItem = icon.Name;
			}
			var startCount = ProductionBatchPolicy.ResolveStartCount(modifiers, BuildCountMultiplier);

			// PERF: avoid an unnecessary enumeration by casting back to its known type
			var cancelCount = modifiers.HasModifier(Modifiers.Ctrl) ? ((List<ProductionItem>)queue.AllQueued()).Count : startCount;
			var item = icon.Queued.FirstOrDefault();
			var handled = btn == MouseButton.Left ? HandleLeftClick(item, icon, queue, startCount, modifiers)
				: btn == MouseButton.Right ? HandleRightClick(item, icon, queue, cancelCount)
				: btn == MouseButton.Middle && HandleMiddleClick(item, icon, queue, cancelCount);

			if (!handled)
				Game.Sound.PlayNotification(World.Map.Rules, World.LocalPlayer, "Sounds", ClickDisabledSound, null);

			return true;
		}

		public override bool HandleKeyPress(KeyInput e)
		{
			if (e.Event == KeyInputEvent.Up || CurrentQueue == null)
				return false;

			if (SelectProductionBuildingHotkey.IsActivatedBy(e))
				return SelectProductionBuilding();

			var batchModifiers = e.Modifiers.HasModifier(Modifiers.Shift) ? Modifiers.Shift : Modifiers.None;

			// HACK: enable production if the shift key is pressed
			e.Modifiers &= ~Modifiers.Shift;
			var toBuild = icons.Values.FirstOrDefault(i => i.Hotkey != null && i.Hotkey.IsActivatedBy(e));
			return toBuild != null && HandleEvent(toBuild, MouseButton.Left, batchModifiers);
		}

		bool SelectProductionBuilding()
		{
			var viewport = worldRenderer.Viewport;
			var selection = World.Selection;

			if (CurrentQueue == null)
				return true;

			var facility = CurrentQueue.MostLikelyProducer().Actor;

			if (facility == null || facility.OccupiesSpace == null)
				return true;

			if (selection.Actors.Count == 1 && selection.Contains(facility))
				viewport.Center(selection.Actors);
			else
				selection.Combine(World, new[] { facility }, false, true);

			Game.Sound.PlayNotification(World.Map.Rules, null, "Sounds", ClickSound, null);
			return true;
		}

		void UpdateCachedProductionIconOverlays()
		{
			cachedQueueOwner = CurrentQueue.Actor.Owner;
			pios = cachedQueueOwner.PlayerActor.TraitsImplementing<IProductionIconOverlay>().ToArray();
		}

		public void RefreshIcons()
		{
			icons = new Dictionary<Rectangle, ProductionIcon>();
			if (CurrentQueue == null)
			{
				if (DisplayedIconCount != 0 || gridLayoutChanged)
				{
					gridLayoutChanged = false;
					OnIconCountChanged(DisplayedIconCount, 0);
					DisplayedIconCount = 0;
				}

				return;
			}

			var oldIconCount = DisplayedIconCount;
			DisplayedIconCount = 0;

			var rb = RenderBounds;
			foreach (var entry in DisplayBuildableEntries().Skip(IconRowOffset * Columns).Take(MaxIconRowOffset * Columns))
			{
				var item = entry.Item;
				var queue = entry.Queue;
				var producer = queue.MostLikelyProducer();
				var faction = producer.Trait.Faction;
				var x = DisplayedIconCount % Columns;
				var y = DisplayedIconCount / Columns;
				var rect = new Rectangle(rb.X + x * (IconSize.X + IconMargin.X), rb.Y + y * (IconSize.Y + IconMargin.Y), IconSize.X, IconSize.Y);

				var bi = item.TraitInfo<BuildableInfo>();
				var hdImage = ProductionIconPresentation.ActorImage(item.Name);
				var wideImage = ProductionIconPresentation.WideActorImage(item.Name);
				var hasWideImage = Columns == 1 && ProductionIconPresentation.Has(World, wideImage);
				// Wide variants extend scenery while retaining the original unit design.
				var hasHdImage = ProductionIconPresentation.ShouldUseHighDefinition(
					Game.Settings.Game, World, hdImage);
				var rsi = item.TraitInfo<RenderSpritesInfo>();
				var iconImage = hasWideImage ? wideImage : hasHdImage ? hdImage : rsi.GetImage(item, faction);
				var icon = new Animation(World, iconImage);
				icon.Play(hasWideImage || hasHdImage ? "icon" : bi.Icon);

				var palette = bi.IconPaletteIsPlayerPalette ? bi.IconPalette + producer.Actor.Owner.InternalName : bi.IconPalette;

				var pi = new ProductionIcon()
				{
					Actor = item,
					Name = item.Name,
					Hotkey = DisplayedIconCount < HotkeyCount ? hotkeys[DisplayedIconCount] : null,
					Sprite = icon.Image,
					Palette = worldRenderer.Palette(palette),
					IconClockPalette = worldRenderer.Palette(ClockPalette),
					IconDarkenPalette = worldRenderer.Palette(NotBuildablePalette),
					Pos = new float2(rect.Location),
					Queued = queue.AllQueued().Where(a => a.Item == item.Name).ToList(),
					ProductionQueue = queue
				};

				icons.Add(rect, pi);
				DisplayedIconCount++;
			}

			eventBounds = icons.Keys.Union();

			if (oldIconCount != DisplayedIconCount || gridLayoutChanged)
			{
				gridLayoutChanged = false;
				OnIconCountChanged(oldIconCount, DisplayedIconCount);
			}
		}

		void DrawCameos()
		{
			var allowUpscaling = Columns == 1;
			foreach (var icon in icons.Values)
			{
				using var iconSample = PerformanceCategoryProfiler.Global.Measure("Production Icon Draw");
				var cameoScale = ProductionIconPresentation.Scale(
					new Size((int)icon.Sprite.Size.X, (int)icon.Sprite.Size.Y),
					new Size(IconSize.X, IconSize.Y), allowUpscaling);
				WidgetUtils.DrawSpriteCentered(icon.Sprite, icon.Palette, icon.Pos + iconOffset, cameoScale);

				// Draw the ProductionIconOverlay's sprites
				foreach (var pio in pios.Where(p => p.IsOverlayActive(icon.Actor)))
				{
					Game.Renderer.EnableScissor(new Rectangle((int)icon.Pos.X, (int)icon.Pos.Y, IconSize.X, IconSize.Y));
					try
					{
						WidgetUtils.DrawSpriteCentered(pio.Sprite, worldRenderer.Palette(pio.Palette), icon.Pos + iconOffset + pio.Offset(IconSize));
					}
					finally { Game.Renderer.DisableScissor(); }
				}
			}
		}

		void DrawClocksAndMasks()
		{
			var allowUpscaling = Columns == 1;
			foreach (var icon in icons.Values)
			{
				var cameo = new Size((int)icon.Sprite.Size.X, (int)icon.Sprite.Size.Y);
				var cell = new Size(IconSize.X, IconSize.Y);
				var scale = ProductionIconPresentation.Scale(cameo, cell, allowUpscaling);
				var center = icon.Pos + iconOffset;
				var left = (int)Math.Floor(center.X - cameo.Width * scale / 2);
				var top = (int)Math.Floor(center.Y - cameo.Height * scale / 2);
				var right = (int)Math.Ceiling(center.X + cameo.Width * scale / 2);
				var bottom = (int)Math.Ceiling(center.Y + cameo.Height * scale / 2);
				Game.Renderer.EnableScissor(Rectangle.Intersect(
					new Rectangle((int)icon.Pos.X, (int)icon.Pos.Y, IconSize.X, IconSize.Y),
					new Rectangle(left, top, right - left, bottom - top)));
				try
				{
				// Build progress
				if (icon.Queued.Count > 0)
				{
					var first = icon.Queued[0];
					clock.PlayFetchIndex(ClockSequence,
						() => (first.TotalTime - first.RemainingTime)
							* (clock.CurrentSequence.Length - 1) / Math.Max(1, first.TotalTime));
					clock.Tick();

					var clockScale = ProductionIconPresentation.MaskCoverScale(cameo, cell,
						new Size((int)clock.Image.Size.X, (int)clock.Image.Size.Y), allowUpscaling);
					WidgetUtils.DrawSpriteCentered(clock.Image, icon.IconClockPalette,
						icon.Pos + iconOffset, clockScale);
				}
				else if (!buildableNames.Contains(icon.Name))
				{
					var maskScale = ProductionIconPresentation.MaskCoverScale(cameo, cell,
						new Size((int)cantBuild.Image.Size.X, (int)cantBuild.Image.Size.Y), allowUpscaling);
					WidgetUtils.DrawSpriteCentered(cantBuild.Image, icon.IconDarkenPalette,
						icon.Pos + iconOffset, maskScale);
				}
				}
				finally { Game.Renderer.DisableScissor(); }
			}
		}

		public override void Draw()
		{
			if (CurrentQueue == null)
				return;

			// The iOS/HD shell supplies an explicit viewport; clips nest with mask clips.
			// Desktop legacy palettes have auto-sized zero Bounds and keep their old path.
			var clipped = Platform.UsesMobileLayout || ExternalSidebarLayout;
			if (clipped)
				Game.Renderer.EnableScissor(Rectangle.Intersect(RenderBounds,
					new Rectangle(0, 0, Game.Renderer.Resolution.Width, Game.Renderer.Resolution.Height)));
			try { DrawPalette(); }
			finally
			{
				if (clipped)
					Game.Renderer.DisableScissor();
			}
		}

		void DrawPalette()
		{
			using (PerformanceCategoryProfiler.Global.Measure("Production Caption"))
				timeOffset = iconOffset - overlayFont.Measure(WidgetUtils.FormatTime(0, World.Timestep)) / 2;

			if (CurrentQueue == null)
				return;

			buildableNames.Clear();
			foreach (var queue in currentQueues)
				foreach (var item in queue.BuildableItems())
					buildableNames.Add(item.Name);

			// Cells do not overlap. Group alpha cameos before multiplicative masks
			// instead of flushing the GPU batch twice for every unavailable icon.
			using (PerformanceCategoryProfiler.Global.Measure("Production AA Enable"))
				Game.Renderer.EnableAntialiasingFilter();
			DrawCameos();
			DrawClocksAndMasks();

			using (PerformanceCategoryProfiler.Global.Measure("Production AA Disable"))
				Game.Renderer.DisableAntialiasingFilter();

			// Overlays
			foreach (var icon in icons.Values)
			{
				var total = icon.Queued.Count;
				if (total > 0)
				{
					Game.Renderer.EnableScissor(new Rectangle((int)icon.Pos.X, (int)icon.Pos.Y, IconSize.X, IconSize.Y));
					try
					{
						var first = icon.Queued[0];
						var waiting = !icon.ProductionQueue.IsProducing(first) && !first.Done;
						if (first.Done)
						{
							if (ReadyTextStyle == ReadyTextStyleOptions.Solid || orderManager.LocalFrameNumber * worldRenderer.World.Timestep / 360 % 2 == 0)
								overlayFont.DrawTextWithContrast(ReadyText, icon.Pos + readyOffset, TextColor, Color.Black, 1);
							else if (ReadyTextStyle == ReadyTextStyleOptions.AlternatingColor)
								overlayFont.DrawTextWithContrast(ReadyText, icon.Pos + readyOffset, ReadyTextAltColor, Color.Black, 1);
						}
						else if (first.Paused)
							overlayFont.DrawTextWithContrast(HoldText,
								icon.Pos + holdOffset,
								TextColor, Color.Black, 1);
						else if (!waiting && DrawTime)
							overlayFont.DrawTextWithContrast(WidgetUtils.FormatTime(first.Queue.RemainingTimeActual(first), World.Timestep),
								icon.Pos + timeOffset,
								TextColor, Color.Black, 1);

						if (first.Infinite && symbolFont != null)
							symbolFont.DrawTextWithContrast(InfiniteSymbol,
								icon.Pos + infiniteOffset,
								TextColor, Color.Black, 1);
						else if (total > 1 || waiting)
						{
							var pos = QueuedOffset;
							if (QueuedTextAlign != TextAlign.Left)
							{
								var size = overlayFont.Measure(total.ToString(NumberFormatInfo.CurrentInfo));

								pos = QueuedTextAlign == TextAlign.Center ?
									new float2(QueuedOffset.X - size.X / 2, QueuedOffset.Y) :
									new float2(QueuedOffset.X - size.X, QueuedOffset.Y);
							}

							overlayFont.DrawTextWithContrast(total.ToString(NumberFormatInfo.CurrentInfo),
								icon.Pos + pos,
								TextColor, Color.Black, 1);
						}
					}
					finally { Game.Renderer.DisableScissor(); }
				}
			}
		}

		public override string GetCursor(int2 pos)
		{
			var icon = icons.Where(i => i.Key.Contains(pos))
				.Select(i => i.Value).FirstOrDefault();

			return icon != null ? base.GetCursor(pos) : null;
		}
	}
}
