using System;
using System.Linq;
using OpenRA.Widgets;

namespace OpenRA.Mods.Common.Widgets.Logic
{
	public static class DesktopAiSettingsLayout
	{
		public static bool IsApplied(ScrollPanelWidget scroll) => scroll.Layout is AiRowsLayout;

		public static void Apply(ScrollPanelWidget scroll)
		{
			const int Inset = 12;
			const int Gap = 10;
			const int Height = DesktopSettingsLayout.ControlHeight;
			var width = Math.Max(1, scroll.Bounds.Width - 2 * Inset - scroll.ScrollbarWidth);
			var labelWidth = Math.Min(360, (int)(width * 0.42));
			var controlX = labelWidth + Gap;
			var y = scroll.TopBottomSpacing;
			foreach (var row in scroll.Children)
			{
				var labels = row.Children.OfType<LabelWidget>().ToArray();
				var controls = row.Children.Where(c => c is ButtonWidget || c is SliderWidget || c is TextFieldWidget).ToArray();
				var value = labels.FirstOrDefault(l => l.Id?.EndsWith("_VALUE", StringComparison.Ordinal) == true);
				var rowHeight = controls.Length == 0 ? (row.Id == "AI_NOTE_ROW" || row.Id == "AI_STATUS_ROW" ? 40 : 26) : 40;
				foreach (var label in labels)
				{
					label.Font = label == value ? "SettingsBold" : "SettingsRegular";
					label.WordWrap = true;
					label.Align = label == value ? TextAlign.Right : TextAlign.Left;
					label.Bounds = label == value ? new WidgetBounds(width - 52, 0, 52, Height)
						: new WidgetBounds(0, 0, controls.Length == 0 ? width : labelWidth, rowHeight);
				}

				foreach (var control in controls)
				{
					var available = Math.Max(1, width - controlX - (value == null ? 0 : 62));
					control.Bounds = new WidgetBounds(controlX, 0, labels.Length == 0 ? Math.Min(360, available) : available, Height);
					if (control is ButtonWidget button) button.Font = "SettingsBold";
					if (control is TextFieldWidget field) field.Font = "SettingsRegular";
					if (control is SliderWidget slider)
					{
						slider.UseTouchStepControls = true;
						slider.TouchFont = "SettingsBold";
					}
				}

				row.Bounds = new WidgetBounds(Inset, y, width, rowHeight);
				if (row.IsVisible()) y += rowHeight + scroll.ItemSpacing;
			}
			scroll.ContentHeight = y + scroll.TopBottomSpacing;
			scroll.Layout = new AiRowsLayout(scroll);
		}

		sealed class AiRowsLayout : ILayout
		{
			readonly ScrollPanelWidget scroll;
			public AiRowsLayout(ScrollPanelWidget scroll) { this.scroll = scroll; }
			public void AdjustChild(Widget child) => Apply(scroll);
			public void AdjustChildren() => Apply(scroll);
		}
	}
}
