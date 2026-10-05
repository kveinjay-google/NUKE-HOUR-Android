using System;
using System.Linq;
using OpenRA.Widgets;

namespace OpenRA.Mods.Common.Widgets.Logic
{
	// Keep each volume slider paired with its related switch on phone screens.
	public static class MobileAudioSettingsLayout
	{
		static readonly string[] Volumes = { "SOUND", "MUSIC", "VIDEO" };
		static readonly string[] Switches = { "MUTE_SOUND", "MUTE_BACKGROUND_MUSIC", "CASH_TICKS" };

		public static void Apply(ScrollPanelWidget scroll, IosSettingsLayout layout)
		{
			if (scroll.GetOrNull("PHONE_AUDIO_ROW_0") == null)
			{
				var header = scroll.GetOrNull("AUDIO_SECTION_HEADER");
				var notice = scroll.GetOrNull("NO_AUDIO_DEVICE_CONTAINER");
				var device = scroll.GetOrNull("AUDIO_DEVICE_CONTAINER");
				var restart = scroll.GetOrNull("RESTART_REQUIRED_CONTAINER");
				var volumes = Volumes.Select(id => scroll.GetOrNull(id + "_VOLUME_CONTAINER")).ToArray();
				var switches = Switches.Select(id => scroll.GetOrNull(id + "_CONTAINER")).ToArray();
				scroll.Children.Clear();
				void Attach(Widget parent, Widget child)
				{
					if (child == null) return;
					child.Parent?.Children.Remove(child);
					parent.AddChild(child);
				}
				Attach(scroll, header); Attach(scroll, notice);
				for (var i = 0; i < Volumes.Length; i++)
				{
					var row = new ContainerWidget { Id = "PHONE_AUDIO_ROW_" + i };
					Attach(row, volumes[i]); Attach(row, switches[i]); scroll.AddChild(row);
				}
				Attach(scroll, device); Attach(scroll, restart);
				if (restart != null) restart.IsVisible = () => restart.Children.Any(c => c.IsVisible());
			}

			var inset = layout.Scale(10);
			var gap = layout.Scale(10);
			var width = Math.Max(1, scroll.Bounds.Width - 2 * inset);
			var columnWidth = Math.Max(1, (width - gap) / 2);
			var labelWidth = Math.Min(layout.Scale(120), columnWidth / 3);
			var y = scroll.TopBottomSpacing;
			foreach (var child in scroll.Children)
			{
				if (child.Id?.StartsWith("PHONE_AUDIO_ROW_", StringComparison.Ordinal) == true)
				{
					child.IsVisible = () => child.Children.Any(c => c.IsVisible());
					child.Bounds = new WidgetBounds(inset, y, width, layout.MinimumTarget);
					for (var i = 0; i < child.Children.Count; i++)
					{
						var column = child.Children[i];
						column.Bounds = new WidgetBounds(i * (columnWidth + gap), 0, columnWidth, layout.MinimumTarget);
						foreach (var control in column.Children)
						{
							if (control is LabelWidget label)
							{
								label.Bounds = new WidgetBounds(0, 0, labelWidth, layout.MinimumTarget);
								label.Font = "IosRegular"; label.WordWrap = true;
							}
							else if (control is SliderWidget)
								control.Bounds = new WidgetBounds(labelWidth + gap, 0, Math.Max(1, columnWidth - labelWidth - gap), layout.MinimumTarget);
							else if (control is CheckboxWidget checkbox)
							{
								checkbox.Bounds = new WidgetBounds(0, 0, columnWidth, layout.MinimumTarget);
								checkbox.Font = "IosRegular";
							}
						}
					}
				}
				else
				{
					var header = child.Id == "AUDIO_SECTION_HEADER";
					child.Bounds = new WidgetBounds(inset, y, width, header ? layout.SectionHeaderHeight : layout.MinimumTarget);
					foreach (var control in child.Children)
					{
						control.Bounds = new WidgetBounds(0, 0, width, child.Bounds.Height);
						if (control is LabelWidget label) { label.Font = header ? "IosBold" : "IosRegular"; label.WordWrap = true; }
					}
					if (child.Id == "AUDIO_DEVICE_CONTAINER")
					{
						var label = child.GetOrNull("AUDIO_DEVICE_LABEL");
						var dropdown = child.GetOrNull("AUDIO_DEVICE");
						if (label != null) label.Bounds.Width = labelWidth;
						if (dropdown != null) dropdown.Bounds = new WidgetBounds(labelWidth + gap, 0, width - labelWidth - gap, layout.MinimumTarget);
					}
				}
				if (child.IsVisible()) y += child.Bounds.Height + scroll.ItemSpacing;
			}
			scroll.ContentHeight = y + scroll.TopBottomSpacing;
			scroll.Layout = new AudioRowsLayout(scroll, layout);
		}

		sealed class AudioRowsLayout : ILayout
		{
			readonly ScrollPanelWidget scroll;
			readonly IosSettingsLayout layout;
			public AudioRowsLayout(ScrollPanelWidget scroll, IosSettingsLayout layout) { this.scroll = scroll; this.layout = layout; }
			public void AdjustChild(Widget child) => Apply(scroll, layout);
			public void AdjustChildren() => Apply(scroll, layout);
		}
	}
}
