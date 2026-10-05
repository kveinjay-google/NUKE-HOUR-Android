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
using System.Diagnostics;
using OpenRA.Widgets;

namespace OpenRA.Mods.Common.Widgets
{
	public class LabelWithTooltipWidget : LabelWidget
	{
		public readonly string TooltipTemplate;
		public readonly string TooltipContainer;
		protected Lazy<TooltipContainerWidget> tooltipContainer;

		public Func<string> GetTooltipText = () => "";
		public bool ScrollOverflow;
		public Func<double> GetMarqueeSeconds;
		readonly Stopwatch marqueeClock = new();
		string marqueeText;
		int marqueeWidth;

		public static int MarqueeOffset(int textWidth, int width, double seconds)
		{
			var distance = Math.Max(0, textWidth - width);
			if (distance == 0 || !double.IsFinite(seconds) || seconds < 0)
				return 0;
			const double pause = 1.5;
			const double speed = 28;
			var travel = distance / speed;
			var phase = seconds % (2 * (pause + travel));
			if (phase <= pause) return 0;
			if (phase <= pause + travel) return (int)((phase - pause) * speed);
			if (phase <= 2 * pause + travel) return distance;
			return Math.Clamp(distance - (int)((phase - 2 * pause - travel) * speed), 0, distance);
		}

		public override void Draw()
		{
			if (!ScrollOverflow) { base.Draw(); return; }
			var text = GetText() ?? "";
			if (text != marqueeText || marqueeWidth != Bounds.Width)
			{
				marqueeText = text;
				marqueeWidth = Bounds.Width;
				marqueeClock.Restart();
			}
			var font = Game.Renderer.Fonts[Font];
			var size = font.Measure(text);
			var offset = MarqueeOffset(size.X, Bounds.Width, GetMarqueeSeconds?.Invoke() ?? marqueeClock.Elapsed.TotalSeconds);
			Game.Renderer.EnableScissor(RenderBounds);
			try
			{
				DrawInner(text, font, GetColor(), RenderOrigin + new int2(-offset,
					(Bounds.Height - size.Y - font.TopOffset) / 2));
			}
			finally { Game.Renderer.DisableScissor(); }
		}

		[ObjectCreator.UseCtor]
		public LabelWithTooltipWidget(ModData modData)
			: base(modData)
		{
			tooltipContainer = Exts.Lazy(() =>
				Ui.Root.Get<TooltipContainerWidget>(TooltipContainer));
		}

		protected LabelWithTooltipWidget(LabelWithTooltipWidget other)
			: base(other)
		{
			TooltipTemplate = other.TooltipTemplate;
			TooltipContainer = other.TooltipContainer;

			tooltipContainer = Exts.Lazy(() =>
				Ui.Root.Get<TooltipContainerWidget>(TooltipContainer));

			GetTooltipText = other.GetTooltipText;
			ScrollOverflow = other.ScrollOverflow;
			GetMarqueeSeconds = other.GetMarqueeSeconds;
		}

		public override Widget Clone() { return new LabelWithTooltipWidget(this); }

		public override void MouseEntered()
		{
			if (TooltipContainer == null)
				return;

			if (GetTooltipText != null)
				tooltipContainer.Value.SetTooltip(TooltipTemplate, new WidgetArgs() { { "getText", GetTooltipText } });
		}

		public override void MouseExited()
		{
			// Only try to remove the tooltip if we know it has been created
			// This avoids a crash if the widget (and the container it refers to) are being removed
			if (TooltipContainer != null && tooltipContainer.IsValueCreated)
				tooltipContainer.Value.RemoveTooltip();
		}
	}
}
