using System;
using System.Linq;
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Mods.Common.Widgets.Logic
{
	// Frame coordinates follow the existing metal artwork; controls keep desktop-sized heights.
	public sealed class DesktopSettingsLayout
	{
		public const int ControlHeight = 36;
		public Rectangle Window { get; }
		public Rectangle Header { get; }
		public Rectangle Tabs { get; }
		public Rectangle Content { get; }
		public Rectangle Reset { get; }
		public Rectangle Back { get; }

		DesktopSettingsLayout(Size viewport)
		{
			var width = Math.Max(1, viewport.Width);
			var height = Math.Max(1, viewport.Height);
			Window = new Rectangle(0, 0, width, height);
			var left = width * 21 / 100;
			var right = width * 94 / 100;
			var top = height * 18 / 100;
			Header = new Rectangle(left, height * 11 / 100, right - left, Math.Min(38, top - height * 11 / 100));
			Tabs = new Rectangle(width * 4 / 100, height * 13 / 100, width * 13 / 100, height * 65 / 100);
			Content = Rectangle.FromLTRB(left, top, right, height * 83 / 100);
			var footerY = height * 86 / 100;
			var actionHeight = Math.Min(42, height - footerY);
			var actionWidth = Math.Min(240, (right - left - 16) / 2);
			Reset = new Rectangle(left, footerY, actionWidth, actionHeight);
			Back = new Rectangle(right - actionWidth, footerY, actionWidth, actionHeight);
		}

		public static DesktopSettingsLayout ForViewport(Size viewport) => new(viewport);

		public Rectangle TabBounds(int index, int count)
		{
			if (count <= 0 || index < 0 || index >= count)
				throw new ArgumentOutOfRangeException(nameof(index));
			const int Gap = 8;
			var height = Math.Max(1, Math.Min(44, (Tabs.Height - Gap * (count - 1)) / count));
			return new Rectangle(Tabs.X, Tabs.Y + index * (height + Gap), Tabs.Width, height);
		}

		public static void ScaleRowFromOriginal(Widget row, Func<Widget, WidgetBounds> original, double horizontalScale)
		{
			void Scale(Widget widget)
			{
				var source = original(widget);
				var height = widget is ButtonWidget || widget is SliderWidget || widget is TextFieldWidget || widget is HotkeyEntryWidget
					? ControlHeight : (int)Math.Round(source.Height * 1.2);
				widget.Bounds = new WidgetBounds((int)Math.Round(source.X * horizontalScale), (int)Math.Round(source.Y * 1.2),
					Math.Max(1, (int)Math.Round(source.Width * horizontalScale)), Math.Max(1, height));
				foreach (var child in widget.Children) Scale(child);
			}
			Scale(row);
			// Hidden children may become visible later; reserve their geometry without growing on repeat calls.
			int RequiredHeight(Widget widget) => Math.Max(widget.Bounds.Height,
				widget.Children.Select(c => c.Bounds.Y + RequiredHeight(c)).DefaultIfEmpty(0).Max());
			row.Bounds.Height = RequiredHeight(row);
		}
	}
}
