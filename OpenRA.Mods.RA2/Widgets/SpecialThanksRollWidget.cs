using System;
using System.Linq;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Mods.RA2.Widgets
{
	// Four visible names inside the cloth with touch/mouse drag and slow automatic movement.
	public sealed class SpecialThanksRollWidget : Widget
	{
		const int VisibleRows = 4;
		long started = -1;
		long lastAdvance = -1;
		long resumeAfter;
		int dragStartY;
		float dragStartOffset;
		float scrollOffset;
		bool dragging;
		public SpecialThanksRollWidget() { }
		SpecialThanksRollWidget(SpecialThanksRollWidget other) : base(other) { }
		public override Widget Clone() => new SpecialThanksRollWidget(this);

		public static int MaximumScrollOffset(int count, int height)
		{
			var rowHeight = Math.Max(1, height / VisibleRows);
			return Math.Max(0, Math.Max(0, count) * rowHeight - Math.Max(0, height));
		}

		public static int ClampScrollOffset(int offset, int count, int height)
			=> Math.Clamp(offset, 0, MaximumScrollOffset(count, height));

		public static (int Page, float Alpha) PageAt(int count, int capacity, double seconds)
		{
			var pages = (Math.Max(0, count) + Math.Max(1, capacity) - 1) / Math.Max(1, capacity);
			if (pages <= 1) return (0, 1);
			seconds = double.IsFinite(seconds) ? Math.Max(0, seconds) : 0;
			var phase = seconds % 8;
			return ((int)(seconds / 8 % pages), (float)Math.Min(1, Math.Min(phase, 8 - phase)));
		}

		public override void DrawOuter()
		{
			if (!IsVisible()) return;
			if (started < 0) started = Game.RunTime;
			var labels = Children.OfType<LabelWidget>().ToArray();
			if (labels.Length == 0) return;
			var font = Game.Renderer.Fonts[labels[0].Font];
			var names = labels.Select(l => l.GetText()).Where(s => !string.IsNullOrEmpty(s)).Distinct().ToArray();
			var rowHeight = Math.Max(1, Bounds.Height / VisibleRows);
			var maximumOffset = MaximumScrollOffset(names.Length, Bounds.Height);
			var now = Game.RunTime;
			if (lastAdvance < 0)
				lastAdvance = now;
			var elapsed = Math.Min(100, Math.Max(0, now - lastAdvance));
			lastAdvance = now;
			if (!dragging && now >= resumeAfter && maximumOffset > 0)
			{
				scrollOffset += elapsed * .018f;
				if (scrollOffset > maximumOffset)
					scrollOffset = 0;
			}
			scrollOffset = ClampScrollOffset((int)Math.Round(scrollOffset), names.Length, Bounds.Height);
			Game.Renderer.EnableScissor(RenderBounds);
			try
			{
				for (var i = 0; i < names.Length; i++)
				{
					var y = i * rowHeight - (int)scrollOffset;
					if (y + rowHeight <= 0 || y >= Bounds.Height)
						continue;
					var text = WidgetUtils.TruncateText(names[i], Math.Max(1, Bounds.Width - 12), font);
					var measured = font.Measure(text);
					var x = (Bounds.Width - measured.X) / 2;
					font.DrawText(text, RenderOrigin + new int2(x, y + (rowHeight - measured.Y) / 2), labels[0].TextColor);
				}
			}
			finally { Game.Renderer.DisableScissor(); }
		}

		public override bool HandleMouseInput(MouseInput mi)
		{
			var names = Children.OfType<LabelWidget>().Select(l => l.GetText())
				.Where(s => !string.IsNullOrEmpty(s)).Distinct().Count();
			if (mi.Event == MouseInputEvent.Scroll)
			{
				scrollOffset = ClampScrollOffset((int)scrollOffset - mi.Delta.Y * 24, names, Bounds.Height);
				resumeAfter = Game.RunTime + 8000;
				return true;
			}

			if (mi.Event == MouseInputEvent.Down && mi.Button == MouseButton.Left)
			{
				if (!TakeMouseFocus(mi))
					return false;
				dragging = true;
				dragStartY = mi.Location.Y;
				dragStartOffset = scrollOffset;
				return true;
			}

			if (!HasMouseFocus)
				return false;

			if (mi.Event == MouseInputEvent.Move && dragging)
			{
				scrollOffset = ClampScrollOffset(
					(int)Math.Round(dragStartOffset + dragStartY - mi.Location.Y), names, Bounds.Height);
				return true;
			}

			if (mi.Event == MouseInputEvent.Up || mi.Event == MouseInputEvent.Cancel)
			{
				dragging = false;
				resumeAfter = Game.RunTime + 8000;
				return YieldMouseFocus(mi);
			}

			return true;
		}

		public override bool YieldMouseFocus(MouseInput mi)
		{
			dragging = false;
			return base.YieldMouseFocus(mi);
		}
	}
}
