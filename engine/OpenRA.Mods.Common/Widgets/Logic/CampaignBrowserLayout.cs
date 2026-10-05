using System;
using OpenRA.Widgets;

namespace OpenRA.Mods.Common.Widgets.Logic
{
	// Geometry is expressed in UI logical pixels, with native-point touch targets.
	public sealed class CampaignBrowserLayout
	{
		public static string NumberedTitle(int number, string title)
			=> number > 0 ? number.ToString("D2", System.Globalization.CultureInfo.InvariantCulture) + " " + title : title;
		// Read live bounds and measure with the current font: resize must never
		// permanently discard the full localized title.
		public static Func<string> CreateTitleGetter(Func<string> text, Func<int> width, Func<string, int> measure)
			=> () => Ingame.IosSupportPowerTooltipPolicy.FitSingleLine(text(), Math.Max(1, width()),
				value => new OpenRA.Primitives.Size(measure(value), 1));

		public static (WidgetBounds Title, WidgetBounds Status) MissionRowTextBounds(
			int rowWidth, int rowHeight, int gap, int statusWidth, int measuredTitleWidth)
		{
			var safeGap = Math.Max(0, gap);
			var safeStatusWidth = Math.Max(1, statusWidth);
			var maximumTitleWidth = Math.Max(1, rowWidth - 3 * safeGap - safeStatusWidth);
			var titleWidth = Math.Min(maximumTitleWidth, Math.Max(1, measuredTitleWidth));
			var title = new WidgetBounds(safeGap, 0, titleWidth, rowHeight);
			var status = new WidgetBounds(title.Right + safeGap, 0, safeStatusWidth, rowHeight);
			return (title, status);
		}

		public WidgetBounds Content { get; }
		public WidgetBounds List { get; }
		public WidgetBounds Summary { get; }
		public WidgetBounds Info { get; }
		public WidgetBounds Map { get; }
		public WidgetBounds Intel { get; }
		public WidgetBounds Start { get; }
		public WidgetBounds Back { get; }
		public int Gap { get; }
		public int RowHeight { get; }
		public int Target { get; }
		public int Heading { get; }

		public CampaignBrowserLayout(IosScreenSnapshot snapshot)
		{
			Content = MultiplayerScreenLayout.ContentBounds(snapshot);
			Gap = snapshot.LogicalPoints(snapshot.IsCompactPhone ? 8 : 16);
			Target = snapshot.LogicalPoints(48);
			RowHeight = snapshot.LogicalPoints(64);
			Heading = snapshot.LogicalPoints(snapshot.IsCompactPhone ? 32 : 44);
			var top = Content.Y + Heading + Gap;
			var footer = Content.Bottom - Target;
			var bodyHeight = footer - Gap - top;
			var listWidth = (Content.Width - Gap) * 35 / 100;
			var previewHeight = Math.Min(snapshot.LogicalPoints(190), bodyHeight * 35 / 100);
			Summary = new WidgetBounds(Content.X, top, listWidth, previewHeight);
			List = new WidgetBounds(Content.X, Summary.Bottom + Gap, listWidth, bodyHeight - previewHeight - Gap);
			Info = new WidgetBounds(List.Right + Gap, top, Content.Right - List.Right - Gap, bodyHeight);
			Map = new WidgetBounds(0, 0, Summary.Width * 44 / 100, previewHeight);
			Intel = new WidgetBounds(0, 0, Info.Width, bodyHeight);
			var buttonWidth = Math.Min(snapshot.LogicalPoints(240), (Content.Width - Gap) / 2);
			Back = new WidgetBounds(Content.Right - buttonWidth, footer, buttonWidth, Target);
			Start = new WidgetBounds(Back.X - Gap - buttonWidth, footer, buttonWidth, Target);
		}

		public void Apply(Widget root, IosScreenSnapshot snapshot, bool touch)
		{
			root.Bounds = new WidgetBounds(0, 0, snapshot.EffectiveSize.Width, snapshot.EffectiveSize.Height);
			Set(root, "CAMPAIGN_SHELL", root.Bounds);
			Set(root, "MISSIONBROWSER_TITLE", new WidgetBounds(Content.X, Content.Y, Content.Width, Heading));
			Set(root, "MISSION_LIST", List);
			Set(root, "CAMPAIGN_SUMMARY", Summary);
			Set(root, "MISSION_INFO", Info);
			Set(root, "MISSION_BG", Map);
			var mapHeading = snapshot.IsCompactPhone ? 0 : Math.Min(Heading, Map.Height / 3);
			root.Get("CAMPAIGN_MAP_HEADING").IsVisible = () => !snapshot.IsCompactPhone;
			Set(root, "CAMPAIGN_MAP_HEADING", new WidgetBounds(Gap, 0, Map.Width - 2 * Gap, mapHeading));
			Set(root, "MISSION_PREVIEW", new WidgetBounds(Gap, mapHeading, Math.Max(1, Map.Width - 2 * Gap), Math.Max(1, Map.Height - mapHeading - Gap)));
			var selectedTitleHeight = Math.Min(Heading, Map.Height / 3);
			var summaryTextWidth = Summary.Width - Map.Width - Gap;
			Set(root, "CAMPAIGN_SELECTED_TITLE", new WidgetBounds(Map.Right + Gap, 0, summaryTextWidth, selectedTitleHeight));
			Set(root, "CAMPAIGN_SELECTED_STATUS", new WidgetBounds(Map.Right + Gap, selectedTitleHeight, summaryTextWidth, selectedTitleHeight));
			Set(root, "CAMPAIGN_SELECTED_THEME", new WidgetBounds(Map.Right + Gap, 2 * selectedTitleHeight, summaryTextWidth, Map.Height - 2 * selectedTitleHeight));
			Set(root, "MISSION_DETAIL", Intel);
			Set(root, "CAMPAIGN_INTEL_HEADING", new WidgetBounds(Gap, 0, Intel.Width - 2 * Gap, Heading));
			Set(root, "MISSION_DESCRIPTION_PANEL", new WidgetBounds(0, Heading, Intel.Width, Math.Max(1, Intel.Height - Heading)));
			Set(root, "STARTGAME_BUTTON", Start);
			Set(root, "BACK_BUTTON", Back);
			Set(root, "MISSION_BIN", new WidgetBounds(Content.X, Info.Y, Content.Width, Info.Height));
			Set(root, "MISSION_VIDEO", new WidgetBounds(Gap, Gap, Content.Width - 2 * Gap, Info.Height - 2 * Gap));
			foreach (var id in new[] { "START_BRIEFING_VIDEO_BUTTON", "STOP_BRIEFING_VIDEO_BUTTON", "START_INFO_VIDEO_BUTTON", "STOP_INFO_VIDEO_BUTTON" })
				Set(root, id, new WidgetBounds(Content.X + (id.Contains("INFO") ? List.Width / 2 : 0), Start.Y,
					Math.Max(1, List.Width / 2 - Gap), Target));

			LobbyLogic.ApplySovietLobbyStyle(root, touch);
			root.Get<BackgroundWidget>("MISSION_BG").Background = "cc-mp-field";
			root.Get<BackgroundWidget>("MISSION_DETAIL").Background = "cc-mp-field";
			foreach (var id in new[] { "MISSIONBROWSER_TITLE", "CAMPAIGN_SELECTED_TITLE" })
				root.Get<OpenRA.Mods.Common.Widgets.LabelWidget>(id).Font = touch ? "IosBold" : "SettingsTitle";
			var list = root.Get<ScrollPanelWidget>("MISSION_LIST");
			list.ScrollbarWidth = touch ? Target : 32;
			list.ItemSpacing = Math.Max(2, Gap / 3);
			list.EnableContentDragging = true;
			foreach (var item in list.Children)
			{
				var group = item.Id == "HEADER";
				item.Bounds.X = 0;
				item.Bounds.Width = List.Width - list.ScrollbarWidth - Gap / 2;
				item.Bounds.Height = group ? Heading : RowHeight;
				if (group)
				{
					Set(item, "LABEL", new WidgetBounds(Gap, 0, item.Bounds.Width - 2 * Gap, Heading));
					item.Get<LabelWidget>("LABEL").Align = TextAlign.Left;
				}
				else
					ApplyMissionRow(item);
			}
			list.Layout.AdjustChildren();
			var intel = root.Get<ScrollPanelWidget>("MISSION_DESCRIPTION_PANEL");
			intel.ScrollbarWidth = touch ? Target : 32;
			intel.EnableContentDragging = true;
			intel.TopBottomSpacing = Gap;
		}

		public void ApplyMissionRow(Widget item)
		{
			var title = item.Get<LabelWithTooltipWidget>("TITLE");
			var status = item.Get<LabelWidget>("STATUS");
			var fullTitle = title.GetTooltipText();
			if (string.IsNullOrEmpty(fullTitle))
				fullTitle = title.GetText();
			var bounds = MissionRowTextBounds(item.Bounds.Width, RowHeight, Gap,
				Game.Renderer.Fonts[status.Font].Measure(status.GetText()).X,
				Game.Renderer.Fonts[title.Font].Measure(fullTitle).X);
			title.Bounds = bounds.Title;
			status.Bounds = bounds.Status;
			title.VAlign = status.VAlign = TextVAlign.Middle;
			title.WordWrap = status.WordWrap = false;
			status.Align = TextAlign.Left;
		}

		static void Set(Widget root, string id, WidgetBounds bounds)
		{
			var widget = root.GetOrNull(id);
			if (widget != null)
				widget.Bounds = bounds;
		}
	}
}
