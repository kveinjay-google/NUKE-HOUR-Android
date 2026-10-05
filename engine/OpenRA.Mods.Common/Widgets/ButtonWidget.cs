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
using OpenRA.Graphics;
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Mods.Common.Widgets
{
	public class ButtonWidget : InputWidget
	{
		public readonly string TooltipContainer;
		public readonly string TooltipTemplate = "BUTTON_TOOLTIP";

		public HotkeyReference Key = new();
		public bool DisableKeyRepeat = false;
		public bool DisableKeySound = false;
		public bool ConsumeDisabledInput = false;

		[FluentReference]
		public string Text = "";
		public TextAlign Align = TextAlign.Center;
		public int LeftMargin = 5;
		public int RightMargin = 5;
		public string Background = "button";
		// Dedicated illustrated navigation states must fit once, never tile.
		public bool StretchBackground = false;
		public bool PreserveBackgroundAspectRatio = false;
		// Optional input rectangle in parent coordinates. This lets touch layouts
		// enlarge the hit target without stretching fixed-size button artwork.
		public Rectangle? ParentEventBounds;
		public bool Depressed = false;
		public int VisualHeight = ChromeMetrics.Get<int>("ButtonDepth");
		public string Font = ChromeMetrics.Get<string>("ButtonFont");
		public Color TextColor = ChromeMetrics.Get<Color>("ButtonTextColor");
		public Color TextColorDisabled = ChromeMetrics.Get<Color>("ButtonTextColorDisabled");
		public bool Contrast = ChromeMetrics.Get<bool>("ButtonTextContrast");
		public bool Shadow = ChromeMetrics.Get<bool>("ButtonTextShadow");
		public Color ContrastColorDark = ChromeMetrics.Get<Color>("ButtonTextContrastColorDark");
		public Color ContrastColorLight = ChromeMetrics.Get<Color>("ButtonTextContrastColorLight");
		public int ContrastRadius = ChromeMetrics.Get<int>("ButtonTextContrastRadius");
		public string ClickSound = ChromeMetrics.Get<string>("ClickSound");
		public string ClickDisabledSound = ChromeMetrics.Get<string>("ClickDisabledSound");
		public bool Highlighted = false;
		public Func<string> GetText;
		public Func<Color> GetColor;
		public Func<Color> GetColorDisabled;
		public Func<Color> GetContrastColorDark;
		public Func<Color> GetContrastColorLight;
		public Func<bool> IsHighlighted;
		public Action OnLongPress;
		long longPressStarted;
		int2 longPressStart;
		bool longPressTracking, longPressCancelled, longPressConsumed;

		public static bool ShouldActivateLongPress(long elapsed, int movement) => elapsed >= 600 && movement <= 12;

		public override void Tick()
		{
			base.Tick();
			if (OnLongPress != null && longPressTracking && !longPressCancelled && !longPressConsumed && HasMouseFocus &&
				ShouldActivateLongPress(Game.RunTime - longPressStarted, (Viewport.LastMousePos - longPressStart).Length))
			{
				longPressConsumed = true;
				Depressed = false;
				OnLongPress();
			}
		}

		public Action OnHorizontalSwipe;
		int2 horizontalSwipeStart;
		bool horizontalSwipeTracking;

		public Action<MouseInput> OnMouseDown = _ => { };
		public Action<MouseInput> OnMouseUp = _ => { };

		protected Lazy<TooltipContainerWidget> tooltipContainer;

		[FluentReference]
		public string TooltipText;
		public Func<string> GetTooltipText;

		[FluentReference]
		public string TooltipDesc;
		public Func<string> GetTooltipDesc;

		// Equivalent to OnMouseUp, but without an input arg
		public Action OnClick = () => { };
		public Action OnDoubleClick = null;
		public Action<KeyInput> OnKeyPress = _ => { };

		public string Cursor = ChromeMetrics.Get<string>("ButtonCursor");

		protected readonly Ruleset ModRules;
		bool touchPressDeferred;
		MouseInput deferredTouchPressInput;

		[ObjectCreator.UseCtor]
		public ButtonWidget(ModData modData)
		{
			ModRules = modData.DefaultRules;

			var textCache = new CachedTransform<string, string>(s => !string.IsNullOrEmpty(s) ? FluentProvider.GetMessage(s) : "");
			var tooltipTextCache = new CachedTransform<string, string>(s => !string.IsNullOrEmpty(s) ? FluentProvider.GetMessage(s) : "");
			var tooltipDescCache = new CachedTransform<string, string>(s => !string.IsNullOrEmpty(s) ? FluentProvider.GetMessage(s) : "");

			GetText = () => textCache.Update(Text);
			GetColor = () => TextColor;
			GetColorDisabled = () => TextColorDisabled;
			GetContrastColorDark = () => ContrastColorDark;
			GetContrastColorLight = () => ContrastColorLight;
			OnMouseUp = _ => OnClick();
			OnKeyPress = _ => OnClick();
			IsHighlighted = () => Highlighted;
			GetTooltipText = () => tooltipTextCache.Update(TooltipText);
			GetTooltipDesc = () => tooltipDescCache.Update(TooltipDesc);
			tooltipContainer = Exts.Lazy(() =>
				Ui.Root.Get<TooltipContainerWidget>(TooltipContainer));
		}

		protected ButtonWidget(ButtonWidget other)
			: base(other)
		{
			ModRules = other.ModRules;

			Text = other.Text;
			Align = other.Align;
			LeftMargin = other.LeftMargin;
			RightMargin = other.RightMargin;
			Font = other.Font;
			TextColor = other.TextColor;
			TextColorDisabled = other.TextColorDisabled;
			Contrast = other.Contrast;
			Shadow = other.Shadow;
			Depressed = other.Depressed;
			Background = other.Background;
			StretchBackground = other.StretchBackground;
			PreserveBackgroundAspectRatio = other.PreserveBackgroundAspectRatio;
			ParentEventBounds = other.ParentEventBounds;
			VisualHeight = other.VisualHeight;
			GetText = other.GetText;
			GetColor = other.GetColor;
			GetColorDisabled = other.GetColorDisabled;
			ContrastColorDark = other.ContrastColorDark;
			ContrastColorLight = other.ContrastColorLight;
			ContrastRadius = other.ContrastRadius;
			GetContrastColorDark = other.GetContrastColorDark;
			GetContrastColorLight = other.GetContrastColorLight;
			OnMouseDown = other.OnMouseDown;
			OnHorizontalSwipe = other.OnHorizontalSwipe;
			OnLongPress = other.OnLongPress;
			Disabled = other.Disabled;
			ConsumeDisabledInput = other.ConsumeDisabledInput;
			Highlighted = other.Highlighted;
			IsHighlighted = other.IsHighlighted;

			OnMouseUp = mi => OnClick();
			OnKeyPress = _ => OnClick();

			TooltipTemplate = other.TooltipTemplate;
			TooltipText = other.TooltipText;
			GetTooltipText = other.GetTooltipText;
			TooltipDesc = other.TooltipDesc;
			GetTooltipDesc = other.GetTooltipDesc;
			TooltipContainer = other.TooltipContainer;
			tooltipContainer = Exts.Lazy(() =>
				Ui.Root.Get<TooltipContainerWidget>(TooltipContainer));
		}

		public override Rectangle EventBounds
		{
			get
			{
				if (ParentEventBounds == null)
					return base.EventBounds;

				var bounds = ParentEventBounds.Value;
				var origin = Parent?.ChildOrigin ?? int2.Zero;
				return new Rectangle(origin.X + bounds.X, origin.Y + bounds.Y, bounds.Width, bounds.Height);
			}
		}

		public override bool YieldMouseFocus(MouseInput mi)
		{
			horizontalSwipeTracking = false;
			longPressTracking = false;
			touchPressDeferred = false;
			Depressed = false;
			return base.YieldMouseFocus(mi);
		}

		public override bool HandleKeyPress(KeyInput e)
		{
			if (!Key.IsActivatedBy(e) || e.Event != KeyInputEvent.Down || (DisableKeyRepeat && e.IsRepeat))
				return false;

			if (!IsDisabled())
			{
				activationFeedbackUntil = Game.RunTime + 220;
				OnKeyPress(e);
				if (!DisableKeySound)
					Game.Sound.PlayNotification(ModRules, null, "Sounds", ClickSound, null);
			}
			else if (!DisableKeySound)
				Game.Sound.PlayNotification(ModRules, null, "Sounds", ClickDisabledSound, null);

			return true;
		}

		public override bool HandleMouseInput(MouseInput mi)
		{
			if (mi.Event == MouseInputEvent.Cancel && HasMouseFocus && (OnHorizontalSwipe != null || OnLongPress != null))
			{
				horizontalSwipeTracking = false;
				return YieldMouseFocus(mi);
			}
			if (mi.Button != MouseButton.Left)
				return false;

			if (OnLongPress != null)
			{
				if (mi.Event == MouseInputEvent.Down)
				{
					if (!TakeMouseFocus(mi)) return false;
					longPressStart = mi.Location;
					longPressStarted = Game.RunTime;
					longPressTracking = true;
					longPressCancelled = longPressConsumed = false;
					if (!IsDisabled()) RunMouseDown(mi);
					return true;
				}
				if (HasMouseFocus && longPressTracking)
				{
					if (mi.Event == MouseInputEvent.Cancel) return YieldMouseFocus(mi);
					if ((mi.Location - longPressStart).Length > 12) longPressCancelled = true;
					if (mi.Event == MouseInputEvent.Up)
					{
						if (!longPressConsumed && !longPressCancelled && EventBounds.Contains(mi.Location) && !IsDisabled())
						{
							activationFeedbackUntil = Game.RunTime + 220;
							if (mi.MultiTapCount == 2 && OnDoubleClick != null) OnDoubleClick();
							else OnMouseUp(mi);
						}
						return YieldMouseFocus(mi);
					}
					return true;
				}
			}

			// Footer gestures must remain available even when its current action is disabled.
			if (OnHorizontalSwipe != null)
			{
				if (mi.Event == MouseInputEvent.Down)
				{
					if (!TakeMouseFocus(mi)) return false;
					horizontalSwipeStart = mi.Location;
					horizontalSwipeTracking = true;
					Depressed = true;
					PlayMouseSound(IsDisabled() ? ClickDisabledSound : ClickSound);
					return true;
				}
				if (HasMouseFocus && horizontalSwipeTracking)
				{
					if (mi.Event == MouseInputEvent.Up)
					{
						var delta = mi.Location - horizontalSwipeStart;
						horizontalSwipeTracking = false;
						if (IsHorizontalSwipe(delta)) OnHorizontalSwipe();
						else if (Math.Abs(delta.X) < 24 && Math.Abs(delta.Y) < 24 && EventBounds.Contains(mi.Location))
						{
							activationFeedbackUntil = Game.RunTime + 220;
							if (!IsDisabled()) OnMouseUp(mi);
						}
						return YieldMouseFocus(mi);
					}
					return true;
				}
			}

			var disabled = IsDisabled();
			if (ShouldConsumeDisabledInput(ConsumeDisabledInput, disabled, mi.Button))
			{
				if (mi.Event == MouseInputEvent.Down)
					PlayMouseSound(ClickDisabledSound);

				return true;
			}

			var touchScrollPanel = GetTouchScrollPanel();
			var deferTouchPress = false;
			if (mi.Event == MouseInputEvent.Down)
			{
				touchPressDeferred = false;
				deferTouchPress = touchScrollPanel?.BeginContentDragFromChild(mi) == true;
				if (!TakeMouseFocus(mi))
				{
					touchScrollPanel?.CancelContentDragFromChild();
					return false;
				}
			}

			if (HasMouseFocus && mi.Event == MouseInputEvent.Move &&
				touchScrollPanel?.TryTakeContentDragFromChild(mi) == true)
				return true;

			if (HasMouseFocus && mi.Event == MouseInputEvent.Up && touchScrollPanel != null)
			{
				// Some touch backends can deliver a displaced release without an
				// intermediate move. Give the panel one last chance to take the drag.
				if (touchScrollPanel.TryTakeContentDragFromChild(mi))
				{
					touchScrollPanel.YieldMouseFocus(mi);
					return true;
				}

				touchScrollPanel.CancelContentDragFromChild();
			}

			if (HasMouseFocus && mi.Event == MouseInputEvent.Up && touchPressDeferred)
			{
				var activateDeferredPress = Depressed && EventBounds.Contains(mi.Location) && !disabled;
				var mouseDownInput = deferredTouchPressInput;
				touchPressDeferred = false;
				if (activateDeferredPress)
					RunMouseDown(mouseDownInput);
			}

			if (HasMouseFocus && mi.Event == MouseInputEvent.Up && mi.MultiTapCount == 2 && OnDoubleClick != null)
			{
				if (!disabled)
				{
					OnDoubleClick();
					return YieldMouseFocus(mi);
				}
			}
			else if (HasMouseFocus && mi.Event == MouseInputEvent.Up)
			{
				// Only fire the onMouseUp event if we successfully lost focus, and were pressed
				if (Depressed && !disabled)
				{
					activationFeedbackUntil = Game.RunTime + 220;
					OnMouseUp(mi);
				}

				return YieldMouseFocus(mi);
			}

			if (mi.Event == MouseInputEvent.Down)
			{
				// OnMouseDown returns false if the button shouldn't be pressed
				if (!disabled)
				{
					if (deferTouchPress)
					{
						touchPressDeferred = true;
						deferredTouchPressInput = mi;
						Depressed = true;
					}
					else
						RunMouseDown(mi);
				}
				else
				{
					touchScrollPanel?.CancelContentDragFromChild();
					YieldMouseFocus(mi);
					PlayMouseSound(ClickDisabledSound);
				}
			}
			else if (mi.Event == MouseInputEvent.Move && HasMouseFocus)
				Depressed = RenderBounds.Contains(mi.Location);

			return Depressed;
		}

		public static bool IsHorizontalSwipe(int2 delta) =>
			Math.Abs(delta.X) >= 24 && Math.Abs(delta.X) > Math.Abs(delta.Y);

		void RunMouseDown(MouseInput mi)
		{
			OnMouseDown(mi);
			Depressed = true;
			PlayMouseSound(ClickSound);
		}

		void PlayMouseSound(string sound)
		{
			if (sound != null)
				Game.Sound.PlayNotification(ModRules, null, "Sounds", sound, null);
		}

		ScrollPanelWidget GetTouchScrollPanel()
		{
			for (var ancestor = Parent; ancestor != null; ancestor = ancestor.Parent)
				if (ancestor is ScrollPanelWidget scrollPanel && scrollPanel.EnableContentDragging)
					return scrollPanel;

			return null;
		}

		public override void MouseEntered()
		{
			if (TooltipContainer == null)
				return;

			if (GetTooltipText != null)
				tooltipContainer.Value.SetTooltip(TooltipTemplate, new WidgetArgs { { "button", this }, { "getText", GetTooltipText }, { "getDesc", GetTooltipDesc } });
		}

		public override void MouseExited()
		{
			if (TooltipContainer == null || !tooltipContainer.IsValueCreated)
				return;

			tooltipContainer.Value.RemoveTooltip();
		}

		public override string GetCursor(int2 pos) { return Cursor; }
		public static bool ShouldConsumeDisabledInput(bool consumeDisabledInput, bool disabled, MouseButton button)
		{
			return consumeDisabledInput && disabled && button == MouseButton.Left;
		}

		long activationFeedbackUntil;
		public bool IsFooterPressActive => Depressed ||
			(activationFeedbackUntil > 0 && Game.RunTime < activationFeedbackUntil);

		public static bool IsActivationFeedbackVisible(bool disabled, long now, long until) =>
			!disabled && now < until;

		public bool IsVisuallyPressed => Depressed ||
			(activationFeedbackUntil > 0 && IsActivationFeedbackVisible(IsDisabled(), Game.RunTime, activationFeedbackUntil)) ||
			(Platform.UsesMobileLayout && !IsDisabled() && TouchPressFeedback.IsActiveFor(this));

		public override int2 ChildOrigin =>
			RenderOrigin +
			(IsVisuallyPressed ? new int2(VisualHeight, VisualHeight) : new int2(0, 0));

		public override void Draw()
		{
			var rb = RenderBounds;
			var disabled = IsDisabled();
			var highlighted = IsHighlighted();
			var font = Game.Renderer.Fonts[Font];
			var text = GetText();
			var color = GetColor();
			var colordisabled = GetColorDisabled();
			var bgDark = GetContrastColorDark();
			var bgLight = GetContrastColorLight();

			var visuallyPressed = IsVisuallyPressed;
			var stateOffset = visuallyPressed ? new int2(VisualHeight, VisualHeight) : new int2(0, 0);

			var position = GetTextPosition(text, font, rb);

			var hover = Ui.MouseOverWidget == this || Children.FirstOrDefault(c => c == Ui.MouseOverWidget) != null;
			DrawBackground(rb, disabled, visuallyPressed, hover, highlighted);
			if (Contrast)
				font.DrawTextWithContrast(text, position + stateOffset,
					disabled ? colordisabled : color, bgDark, bgLight, ContrastRadius);
			else if (Shadow)
				font.DrawTextWithShadow(text, position, color, bgDark, bgLight, 1);
			else
				font.DrawText(text, position + stateOffset,
					disabled ? colordisabled : color);
		}

		int2 GetTextPosition(string text, SpriteFont font, Rectangle rb)
		{
			var textSize = font.Measure(text);
			var y = rb.Y + (Bounds.Height - textSize.Y - font.TopOffset) / 2;

			switch (Align)
			{
				case TextAlign.Left:
					return new int2(rb.X + LeftMargin, y);
				case TextAlign.Center:
				default:
					return new int2(rb.X + (UsableWidth - textSize.X) / 2, y);
				case TextAlign.Right:
					return new int2(rb.X + UsableWidth - textSize.X - RightMargin, y);
			}
		}

		public override Widget Clone() { return new ButtonWidget(this); }
		public virtual int UsableWidth => Bounds.Width;

		public virtual void DrawBackground(Rectangle rect, bool disabled, bool pressed, bool hover, bool highlighted)
		{
			if (StretchBackground && !string.IsNullOrEmpty(Background))
			{
				var name = highlighted ? Background + "-highlighted" : Background;
				var images = ChromeProvider.TryGetPanelImages(WidgetUtils.GetStatefulImageName(name, disabled, pressed, hover));
				if (images != null && images.Length > 4 && images[4] != null)
				{
					var bounds = PreserveBackgroundAspectRatio ? AspectFitBounds(rect, images[4].Bounds.Size) : rect;
					WidgetUtils.DrawSprite(images[4], new float2(bounds.X, bounds.Y), bounds.Size);
				}
				return;
			}

			DrawBackground(Background, rect, disabled, pressed, hover, highlighted);
		}

		public static Rectangle AspectFitBounds(Rectangle target, Size source)
		{
			if (source.Width <= 0 || source.Height <= 0 || target.Width <= 0 || target.Height <= 0)
				return target;

			var scale = Math.Min(target.Width / (double)source.Width, target.Height / (double)source.Height);
			var width = Math.Clamp((int)Math.Round(source.Width * scale), 1, target.Width);
			var height = Math.Clamp((int)Math.Round(source.Height * scale), 1, target.Height);
			return new Rectangle(target.X + (target.Width - width) / 2, target.Y + (target.Height - height) / 2, width, height);
		}

		public static void DrawBackground(string baseName, Rectangle rect, bool disabled, bool pressed, bool hover, bool highlighted)
		{
			if (string.IsNullOrEmpty(baseName))
				return;

			var variantName = highlighted ? baseName + "-highlighted" : baseName;
			var imageName = WidgetUtils.GetStatefulImageName(variantName, disabled, pressed, hover);

			WidgetUtils.DrawPanel(imageName, rect);
		}
	}
}
