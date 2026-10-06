using System;
using System.Linq;
using OpenRA.Widgets;

namespace OpenRA.Mods.Common.Widgets.Logic
{
	// AI selection and editing use different visible rows. Size every row before collapsing hidden rows.
	public static class MobileAiSettingsLayout
	{
		public static bool IsApplied(ScrollPanelWidget scroll) => scroll.Layout is AiRowsLayout;

		public static void Apply(ScrollPanelWidget scroll, IosSettingsLayout layout)
		{
			var inset = layout.Scale(10);
			var gap = layout.Scale(4);
			var width = Math.Max(1, scroll.Bounds.Width - 2 * inset);
			var labelHeight = layout.Scale(24);
			var valueWidth = Math.Min(layout.Scale(66), width / 3);
			var y = scroll.TopBottomSpacing;

			foreach (var row in scroll.Children)
			{
				var labels = row.Children.OfType<LabelWidget>().ToArray();
				var controls = row.Children.Where(c => c is SliderWidget || c is ButtonWidget || c is TextFieldWidget).ToArray();
				var value = labels.FirstOrDefault(l => l.Id?.EndsWith("_VALUE", StringComparison.Ordinal) == true);
				var hintHeight = row.Id == "AI_STATUS_ROW" || row.Id == "AI_NOTE_ROW" ? layout.Scale(48) : labelHeight;
				var headerHeight = controls.Length == 0 ? hintHeight : labels.Length > 0 ? labelHeight : 0;
				foreach (var label in labels)
				{
					label.WordWrap = true;
					label.Font = label == value ? "IosBold" : "IosRegular";
					label.Align = label == value ? TextAlign.Right : TextAlign.Left;
					label.Bounds = label == value
						? new WidgetBounds(width - valueWidth, 0, valueWidth, headerHeight)
						: new WidgetBounds(0, 0, value == null ? width : Math.Max(1, width - valueWidth - gap), headerHeight);
				}

				var controlY = headerHeight > 0 && controls.Length > 0 ? headerHeight + gap : 0;
				foreach (var control in controls)
				{
					control.Bounds = new WidgetBounds(0, controlY, width, layout.MinimumTarget);
					if (control is ButtonWidget button) button.Font = "IosBold";
					if (control is TextFieldWidget field) field.Font = "IosRegular";
					if (control is SliderWidget slider)
					{
						slider.UseTouchStepControls = true;
						slider.TouchFont = "IosBold";
					}
					controlY += layout.MinimumTarget + gap;
				}

				var bottom = row.Children.Count == 0 ? 0 : row.Children.Max(c => c.Bounds.Y + c.Bounds.Height);
				row.Bounds = new WidgetBounds(inset, y, width, bottom + gap);
				if (row.IsVisible())
					y += row.Bounds.Height + scroll.ItemSpacing;
			}

			scroll.ContentHeight = y + scroll.TopBottomSpacing;
			scroll.Layout = new AiRowsLayout(scroll, layout);
		}

		sealed class AiRowsLayout : ILayout
		{
			readonly ScrollPanelWidget scroll;
			readonly IosSettingsLayout layout;
			public AiRowsLayout(ScrollPanelWidget scroll, IosSettingsLayout layout) { this.scroll = scroll; this.layout = layout; }
			public void AdjustChild(Widget child) => Apply(scroll, layout);
			public void AdjustChildren() => Apply(scroll, layout);
		}
	}
}
