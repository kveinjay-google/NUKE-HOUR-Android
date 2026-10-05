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
using System.IO;
using System.Linq;
using System.Threading;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Common.Widgets.Logic.Ingame;
using OpenRA.Network;
using OpenRA.Traits;
using OpenRA.Widgets;

namespace OpenRA.Mods.Common.Widgets.Logic
{
	public class MissionBrowserLogic : ChromeLogic
	{
		readonly Dictionary<string, int> missionNumbers = new();
		enum PlayingVideo { None, Info, Briefing, GameStart }
		enum PanelType { MissionInfo, Options }

		[FluentReference]
		const string NoVideoTitle = "dialog-no-video.title";

		[FluentReference]
		const string NoVideoPrompt = "dialog-no-video.prompt";

		[FluentReference]
		const string NoVideoCancel = "dialog-no-video.cancel";

		[FluentReference]
		const string CantPlayTitle = "dialog-cant-play-video.title";

		[FluentReference]
		const string CantPlayPrompt = "dialog-cant-play-video.prompt";

		[FluentReference]
		const string CantPlayCancel = "dialog-cant-play-video.cancel";

		[FluentReference]
		const string NotAvailable = "label-not-available";

		[FluentReference]
		const string MissionAvailable = "label-missionbrowser-available";

		[FluentReference]
		const string MissionCompleted = "label-missionbrowser-completed";

		[FluentReference]
		const string MissionLocked = "label-missionbrowser-locked";

		[FluentReference]
		const string IntelTheme = "label-missionbrowser-intel-theme";

		[FluentReference]
		const string IntelHistory = "label-missionbrowser-intel-history";

		[FluentReference]
		const string IntelObjectives = "label-missionbrowser-intel-objectives";

		[FluentReference]
		const string IntelAssets = "label-missionbrowser-intel-assets";

		[FluentReference]
		const string IntelNotes = "label-missionbrowser-intel-notes";

		readonly ModData modData;
		readonly Action onStart;
		readonly Widget missionDetail;
		readonly Widget optionsContainer;
		readonly Widget checkboxRowTemplate;
		readonly Widget dropdownRowTemplate;
		readonly ScrollPanelWidget descriptionPanel;
		readonly LabelWidget description;
		readonly SpriteFont descriptionFont;
		readonly int defaultDescriptionPanelHeight;
		readonly ButtonWidget startBriefingVideoButton;
		readonly ButtonWidget stopBriefingVideoButton;
		readonly ButtonWidget startInfoVideoButton;
		readonly ButtonWidget stopInfoVideoButton;
		readonly VideoPlayerWidget videoPlayer;
		readonly BackgroundWidget fullscreenVideoPlayer;

		readonly ScrollPanelWidget missionList;
		readonly ScrollItemWidget headerTemplate;
		readonly ScrollItemWidget template;

		readonly Widget miniOptions;
		readonly DropDownButtonWidget difficultyButton;
		readonly DropDownButtonWidget gameSpeedButton;
		readonly string unsetDifficulty;
		readonly string defaultTooltop;

		// For remembering options
		// TODO: this should be persistent across game sessions
		string selectedDifficulty;
		string selectedGameSpeed;

		bool minifiedOptions = true;
		MapPreview selectedMap;
		PlayingVideo playingVideo;
		readonly Dictionary<string, string> missionOptions = new();
		PanelType panel = PanelType.MissionInfo;
		readonly Widget campaignRoot;
		IosScreenSnapshot campaignSnapshot;
		string campaignIntel = "";

		void LayoutCampaign()
		{
			campaignSnapshot = IosScreenMetrics.SnapshotFor(Game.Renderer.Resolution);
			new CampaignBrowserLayout(campaignSnapshot).Apply(campaignRoot, campaignSnapshot, Platform.UsesMobileLayout);
		}

		public override void Tick()
		{
			if (campaignRoot == null)
				return;
			var next = IosScreenMetrics.SnapshotFor(Game.Renderer.Resolution);
			if (next.EffectiveSize != campaignSnapshot.EffectiveSize || next.SafeBounds != campaignSnapshot.SafeBounds ||
				next.NativePointSize != campaignSnapshot.NativePointSize)
			{
				LayoutCampaign();
				RenderCampaignIntel();
			}
		}

		void RenderCampaignIntel()
		{
			if (campaignRoot == null)
				return;
			foreach (var child in descriptionPanel.Children.Where(c => c.Id == "CAMPAIGN_INTEL_TEXT").ToArray())
				descriptionPanel.RemoveChild(child);
			description.IsVisible = () => false;
			description.Bounds.Height = 0;
			descriptionPanel.CollapseHiddenChildren = true;
			var gap = campaignSnapshot.LogicalPoints(campaignSnapshot.IsCompactPhone ? 8 : 16);
			var width = Math.Max(1, descriptionPanel.Bounds.Width - descriptionPanel.ScrollbarWidth - 2 * gap);
			foreach (var section in campaignIntel.Split(new[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries))
			{
				var lines = section.Split(new[] { '\n' }, 2);
				for (var i = 0; i < lines.Length; i++)
				{
					var font = Platform.UsesMobileLayout ? (i == 0 ? "IosBold" : "IosRegular") : (i == 0 ? "SettingsBold" : "SettingsRegular");
					var rendererFont = Game.Renderer.Fonts[font];
					var text = IosSupportPowerTooltipPolicy.FitText(lines[i], width, int.MaxValue,
						value => new OpenRA.Primitives.Size(rendererFont.Measure(value).X, rendererFont.Measure(value).Y));
					var visualLines = text.Split('\n');
					for (var lineIndex = 0; lineIndex < visualLines.Length; lineIndex++)
					{
						var visualLine = visualLines[lineIndex];
						var height = Math.Max(1, rendererFont.Measure(visualLine).Y);
						var padding = i == lines.Length - 1 && lineIndex == visualLines.Length - 1 ? 2 * gap : gap / 2;
						descriptionPanel.AddChild(new LabelWidget(modData)
						{
							Id = "CAMPAIGN_INTEL_TEXT", Font = font, Align = TextAlign.Center, GetText = () => visualLine,
							VAlign = TextVAlign.Top, Bounds = new WidgetBounds(gap, 0, width, height + padding)
						});
					}
				}
			}
			descriptionPanel.Layout.AdjustChildren();
			var themeLabel = campaignRoot.Get<LabelWidget>("CAMPAIGN_SELECTED_THEME");
			var themeFont = Game.Renderer.Fonts[themeLabel.Font];
			var theme = ResolveMissionText(ProgressData(selectedMap)?.Theme);
			var fittedTheme = IosSupportPowerTooltipPolicy.FitText(theme, themeLabel.Bounds.Width, themeLabel.Bounds.Height,
				value => new OpenRA.Primitives.Size(themeFont.Measure(value).X, themeFont.Measure(value).Y));
			themeLabel.GetText = () => fittedTheme;
		}

		[ObjectCreator.UseCtor]
		public MissionBrowserLogic(Widget widget, ModData modData, World world, Action onStart, Action onExit, string initialMap)
		{
			this.modData = modData;
			this.onStart = onStart;
			Game.BeforeGameStart += OnGameStart;
			if (widget.GetOrNull("CAMPAIGN_SHELL") != null)
			{
				campaignRoot = widget;
				LayoutCampaign();
				var selectedTitle = widget.Get<LabelWidget>("CAMPAIGN_SELECTED_TITLE");
				selectedTitle.GetText = CampaignBrowserLayout.CreateTitleGetter(() => MissionTitle(selectedMap),
					() => selectedTitle.Bounds.Width, text => Game.Renderer.Fonts[selectedTitle.Font].Measure(text).X);
				widget.Get<LabelWidget>("CAMPAIGN_SELECTED_STATUS").GetText = () => selectedMap == null ? "" : MissionStatus(selectedMap);
			}

			missionList = widget.Get<ScrollPanelWidget>("MISSION_LIST");

			headerTemplate = widget.Get<ScrollItemWidget>("HEADER");
			template = widget.Get<ScrollItemWidget>("TEMPLATE");

			var title = widget.GetOrNull<LabelWidget>("MISSIONBROWSER_TITLE");
			if (title != null)
			{
				var titleText = title.GetText();
				title.GetText = () => playingVideo != PlayingVideo.None ? MissionTitle(selectedMap) : titleText;
			}

			widget.Get("MISSION_INFO").IsVisible = () => selectedMap != null;

			var previewWidget = widget.Get<MapPreviewWidget>("MISSION_PREVIEW");
			previewWidget.Preview = () => selectedMap;
			previewWidget.IsVisible = () => playingVideo == PlayingVideo.None;

			videoPlayer = widget.Get<VideoPlayerWidget>("MISSION_VIDEO");
			widget.Get("MISSION_BIN").IsVisible = () => playingVideo != PlayingVideo.None;
			fullscreenVideoPlayer = Ui.LoadWidget<BackgroundWidget>("FULLSCREEN_PLAYER", Ui.Root, new WidgetArgs { { "world", world } });

			missionDetail = widget.Get("MISSION_DETAIL");

			descriptionPanel = missionDetail.Get<ScrollPanelWidget>("MISSION_DESCRIPTION_PANEL");
			descriptionPanel.IsVisible = () => panel == PanelType.MissionInfo;

			description = descriptionPanel.Get<LabelWidget>("MISSION_DESCRIPTION");
			descriptionFont = Game.Renderer.Fonts[description.Font];
			defaultDescriptionPanelHeight = descriptionPanel.Bounds.Height;

			optionsContainer = missionDetail.Get("MISSION_OPTIONS");
			optionsContainer.IsVisible = () => panel == PanelType.Options;
			checkboxRowTemplate = optionsContainer.Get("CHECKBOX_ROW_TEMPLATE");
			dropdownRowTemplate = optionsContainer.Get("DROPDOWN_ROW_TEMPLATE");

			startBriefingVideoButton = widget.Get<ButtonWidget>("START_BRIEFING_VIDEO_BUTTON");
			stopBriefingVideoButton = widget.Get<ButtonWidget>("STOP_BRIEFING_VIDEO_BUTTON");
			stopBriefingVideoButton.IsVisible = () => playingVideo == PlayingVideo.Briefing;
			stopBriefingVideoButton.OnClick = () => StopVideo(videoPlayer);

			startInfoVideoButton = widget.Get<ButtonWidget>("START_INFO_VIDEO_BUTTON");
			stopInfoVideoButton = widget.Get<ButtonWidget>("STOP_INFO_VIDEO_BUTTON");
			stopInfoVideoButton.IsVisible = () => playingVideo == PlayingVideo.Info;
			stopInfoVideoButton.OnClick = () => StopVideo(videoPlayer);

			miniOptions = widget.GetOrNull("MISSION_MINIFIED_OPTIONS");
			if (miniOptions != null)
			{
				miniOptions.IsVisible = () => minifiedOptions &&
					MissionBrowserPresentationPolicy.ShowCustomOptions(UsesFixedRules(selectedMap));
				difficultyButton = miniOptions.GetOrNull<DropDownButtonWidget>("DIFFICULTY");
				gameSpeedButton = miniOptions.GetOrNull<DropDownButtonWidget>("GAMESPEED");
				unsetDifficulty = FluentProvider.GetMessage(difficultyButton.Text);
				defaultTooltop = FluentProvider.GetMessage(difficultyButton.TooltipText);
			}

			var allPreviews = new List<MapPreview>();
			missionList.RemoveChildren();

			// Add a group for each campaign
			if (modData.Manifest.Missions.Length > 0)
			{
				var stringPool = new HashSet<string>(); // Reuse common strings in YAML
				var yaml = MiniYaml.Merge(modData.Manifest.Missions.Select(
					m => MiniYaml.FromStream(modData.DefaultFileSystem.Open(m), m, stringPool: stringPool)));

				foreach (var kv in yaml)
				{
					var missionMapOrder = kv.Value.Nodes
						.Select((node, index) => new
						{
							Name = Path.GetFileNameWithoutExtension(node.Key),
							Index = index,
						})
						.ToDictionary(entry => entry.Name, entry => entry.Index, StringComparer.OrdinalIgnoreCase);

					var previews = modData.MapCache
						.Where(p => p.Class == MapClassification.System && p.Status == MapStatus.Available)
						.Select(p =>
						{
							var name = Path.GetFileNameWithoutExtension(p.PackageName);
							return new
							{
								Preview = p,
								Index = missionMapOrder.TryGetValue(name, out var index) ? index : -1,
							};
						})
						.Where(x => x.Index != -1)
						.OrderBy(x => x.Index)
						.Select(x => x.Preview)
						.ToList();

					if (previews.Count != 0)
					{
						CreateMissionGroup(kv.Key, previews, onExit);
						allPreviews.AddRange(previews);
					}
				}
			}

			// Add an additional group for loose missions
			var loosePreviews = modData.MapCache
				.Where(p => p.Status == MapStatus.Available &&
					p.Visibility.HasFlag(MapVisibility.MissionSelector) &&
					!allPreviews.Any(a => a.Uid == p.Uid))
				.ToList();

			if (loosePreviews.Count != 0)
			{
				CreateMissionGroup("Missions", loosePreviews, onExit);
				allPreviews.AddRange(loosePreviews);
			}

			if (allPreviews.Count > 0)
			{
				var uid = modData.MapCache.GetUpdatedMap(initialMap);
				var map = uid == null ? null : modData.MapCache[uid];
				if (map != null && map.Visibility.HasFlag(MapVisibility.MissionSelector) && CanStartMission(map))
				{
					SelectMap(map);
					missionList.ScrollToSelectedItem();
				}
				else
					SelectMap(allPreviews.FirstOrDefault(CanStartMission));
			}

			// Preload map preview to reduce jank
			new Thread(() =>
			{
				foreach (var p in allPreviews)
					p.GetMinimap();
			}).Start();

			var startButton = widget.Get<ButtonWidget>("STARTGAME_BUTTON");
			startButton.OnClick = () => StartMissionClicked(onExit);
			startButton.IsDisabled = () => selectedMap == null || !CanStartMission(selectedMap);

			widget.Get<ButtonWidget>("BACK_BUTTON").OnClick = () =>
			{
				StopVideo(videoPlayer);
				Ui.CloseWindow();
				onExit();
			};

			var tabContainer = widget.Get("MISSION_TABS");
			tabContainer.IsVisible = () => !minifiedOptions &&
				MissionBrowserPresentationPolicy.ShowCustomOptions(UsesFixedRules(selectedMap));

			var optionsTab = tabContainer.Get<ButtonWidget>("OPTIONS_TAB");
			optionsTab.IsHighlighted = () => panel == PanelType.Options;
			optionsTab.OnClick = () => panel = PanelType.Options;

			var missionTab = tabContainer.Get<ButtonWidget>("MISSIONINFO_TAB");
			missionTab.IsHighlighted = () => panel == PanelType.MissionInfo;
			missionTab.OnClick = () => panel = PanelType.MissionInfo;
		}

		void OnGameStart()
		{
			Ui.CloseWindow();

			DiscordService.UpdateStatus(DiscordState.PlayingCampaign);

			onStart();
		}

		bool disposed;
		protected override void Dispose(bool disposing)
		{
			if (disposing && !disposed)
			{
				disposed = true;
				Game.BeforeGameStart -= OnGameStart;
			}

			base.Dispose(disposing);
		}

		void CreateMissionGroup(string title, IEnumerable<MapPreview> previews, Action onExit)
		{
			var header = ScrollItemWidget.Setup(headerTemplate, () => false, () => { });
			var groupTitle = FluentProvider.TryGetMessage(title, out var translatedTitle) ? translatedTitle : title;
			header.Get<LabelWidget>("LABEL").GetText = () => groupTitle;
			missionList.AddChild(header);

			var missionNumber = 0;
			foreach (var preview in previews)
			{
				missionNumbers[preview.Uid] = ++missionNumber;
				var item = ScrollItemWidget.Setup(template,
					() => selectedMap != null && selectedMap.Uid == preview.Uid,
					() => SelectMap(preview),
					() =>
					{
						SelectMap(preview);
						if (CanStartMission(preview))
							StartMissionClicked(onExit);
					});
				// Unlock state gates launching, never browsing intelligence.
				item.IsDisabled = () => false;
				item.Cursor = "default";

				var label = item.Get<LabelWithTooltipWidget>("TITLE");
				if (campaignRoot != null)
				{
					var fullTitle = MissionTitle(preview);
					label.GetText = CampaignBrowserLayout.CreateTitleGetter(() => fullTitle,
						() => label.Bounds.Width, text => Game.Renderer.Fonts[label.Font].Measure(text).X);
					label.GetTooltipText = () => label.GetText() == fullTitle ? "" : fullTitle;
				}
				else
					WidgetUtils.TruncateLabelToTooltip(label, MissionTitle(preview));
				item.Get<LabelWidget>("STATUS").GetText = () => MissionStatus(preview);
				if (campaignRoot != null)
					new CampaignBrowserLayout(campaignSnapshot).ApplyMissionRow(item);

				missionList.AddChild(item);
			}
		}

		static MissionDataInfo ProgressData(MapPreview preview)
			=> preview?.WorldActorInfo?.TraitInfoOrDefault<MissionDataInfo>();

		static bool CanStartMission(MapPreview preview)
			=> preview != null && CampaignProgress.IsUnlocked(ProgressData(preview)?.UnlockPrerequisite);

		static bool UsesFixedRules(MapPreview preview)
			=> ProgressData(preview)?.FixedRules == true;

		string MissionTitle(MapPreview preview)
		{
			if (preview == null)
				return "";

			var titleKey = ProgressData(preview)?.BrowserTitle;
			var title = !string.IsNullOrWhiteSpace(titleKey) && FluentProvider.TryGetMessage(titleKey, out var translated)
				? translated : preview.Title;
			return CampaignBrowserLayout.NumberedTitle(missionNumbers.TryGetValue(preview.Uid, out var number) ? number : 0, title);
		}

		static string MissionStatus(MapPreview preview)
		{
			var missionId = ProgressData(preview)?.MissionId;
			if (CampaignProgress.IsCompleted(missionId))
				return FluentProvider.GetMessage(MissionCompleted);

			return CanStartMission(preview)
				? FluentProvider.GetMessage(MissionAvailable)
				: FluentProvider.GetMessage(MissionLocked);
		}

		void SelectMap(MapPreview preview)
		{
			if (preview == null)
				return;

			selectedMap = preview;

			var briefingVideo = "";
			var briefingVideoVisible = false;

			var infoVideo = "";
			var infoVideoVisible = false;

			new Thread(() =>
			{
				var missionData = preview.WorldActorInfo.TraitInfoOrDefault<MissionDataInfo>();
				if (missionData != null)
				{
					briefingVideo = missionData.BriefingVideo;
					briefingVideoVisible = briefingVideo != null;

					infoVideo = missionData.BackgroundVideo;
					infoVideoVisible = infoVideo != null;

					var intel = BuildMissionIntel(missionData);
					Game.RunAfterTick(() =>
					{
						if (preview == selectedMap)
						{
							var normalizedIntel = intel.Replace("\\n", "\n");
							var briefing = WidgetUtils.WrapText(normalizedIntel, description.Bounds.Width, descriptionFont);
							var height = descriptionFont.Measure(briefing).Y;
							description.GetText = () => briefing;
							description.Bounds.Height = height;
							descriptionPanel.Layout.AdjustChildren();
							campaignIntel = normalizedIntel;
							RenderCampaignIntel();
							panel = PanelType.MissionInfo;
						}
					});
				}
			}).Start();

			startBriefingVideoButton.IsVisible = () => briefingVideoVisible && playingVideo != PlayingVideo.Briefing;
			startBriefingVideoButton.OnClick = () => PlayVideo(videoPlayer, briefingVideo, PlayingVideo.Briefing);

			startInfoVideoButton.IsVisible = () => infoVideoVisible && playingVideo != PlayingVideo.Info;
			startInfoVideoButton.OnClick = () => PlayVideo(videoPlayer, infoVideo, PlayingVideo.Info);

			descriptionPanel.ScrollToTop();

			RebuildOptions();
		}

		void RebuildOptions()
		{
			if (selectedMap == null || selectedMap.WorldActorInfo == null)
				return;

			missionOptions.Clear();
			optionsContainer.RemoveChildren();

			var allOptions = selectedMap.PlayerActorInfo.TraitInfos<ILobbyOptions>()
					.Concat(selectedMap.WorldActorInfo.TraitInfos<ILobbyOptions>())
					.SelectMany(t => t.LobbyOptions(selectedMap))
					.Where(o => o.IsVisible)
					.OrderBy(o => o.DisplayOrder).ToArray();
			var fixedRules = UsesFixedRules(selectedMap);
			if (campaignRoot == null)
				descriptionPanel.Bounds.Height = fixedRules ? missionDetail.Bounds.Height : defaultDescriptionPanelHeight;
			if (!MissionBrowserPresentationPolicy.ShowCustomOptions(fixedRules))
			{
				foreach (var option in allOptions)
					missionOptions[option.Id] = option.DefaultValue;

				minifiedOptions = true;
				panel = PanelType.MissionInfo;
				descriptionPanel.Layout.AdjustChildren();
				return;
			}

			minifiedOptions = allOptions.All(o => o.Id == "difficulty" || o.Id == "gamespeed");
			if (minifiedOptions)
				BuildMinifiedOptions(allOptions);
			else
				BuildOptions(allOptions);
		}

		static string BuildMissionIntel(MissionDataInfo missionData)
		{
			if (missionData == null)
				return "";

			var sections = new[]
			{
				(IntelTheme, missionData.Theme),
				(IntelHistory, missionData.HistoricalBackground),
				(IntelObjectives, missionData.PrimaryObjectives),
				(IntelAssets, missionData.StartingAssets),
				(IntelNotes, missionData.OperationalNotes),
			};
			var resolved = sections
				.Select(section => (Heading: FluentProvider.GetMessage(section.Item1), Body: ResolveMissionText(section.Item2)))
				.Where(section => !string.IsNullOrWhiteSpace(section.Body))
				.Select(section => section.Heading + "\n" + section.Body)
				.ToArray();
			if (resolved.Length > 0)
				return string.Join("\n\n", resolved);

			return ResolveMissionText(missionData.Briefing);
		}

		static string ResolveMissionText(string textOrKey)
		{
			if (string.IsNullOrWhiteSpace(textOrKey))
				return "";

			return FluentProvider.TryGetMessage(textOrKey, out var translated) ? translated : textOrKey;
		}

		void SetMapDifficulty(LobbyOption option)
		{
			selectedDifficulty ??= option.DefaultValue;
			if (option.Values.ContainsKey(selectedDifficulty))
				missionOptions[option.Id] = selectedDifficulty;
			else
				missionOptions[option.Id] = option.DefaultValue;
		}

		void SetMapSpeed(LobbyOption option)
		{
			selectedGameSpeed ??= option.DefaultValue;
			if (option.Values.ContainsKey(selectedGameSpeed))
				missionOptions[option.Id] = selectedGameSpeed;
			else
				missionOptions[option.Id] = option.DefaultValue;
		}

		void OnOptionSelected(string optionId, string value)
		{
			// Only remember when the user manually changes the value
			if (optionId == "difficulty")
				selectedDifficulty = value;
			else if (optionId == "gamespeed")
				selectedGameSpeed = value;

			missionOptions[optionId] = value;
		}

		void BuildOptions(LobbyOption[] allOptions)
		{
			Widget row = null;
			var checkboxColumns = new Queue<CheckboxWidget>();
			var dropdownColumns = new Queue<DropDownButtonWidget>();

			var yOffset = 0;
			foreach (var option in allOptions.Where(o => o is LobbyBooleanOption))
			{
				missionOptions[option.Id] = option.DefaultValue;

				if (checkboxColumns.Count == 0)
				{
					row = checkboxRowTemplate.Clone();
					row.Bounds.Y = yOffset;
					yOffset += row.Bounds.Height;
					foreach (var child in row.Children)
						if (child is CheckboxWidget childCheckbox)
							checkboxColumns.Enqueue(childCheckbox);

					optionsContainer.AddChild(row);
				}

				var checkbox = checkboxColumns.Dequeue();

				checkbox.GetText = () => option.Name;
				if (option.Description != null)
				{
					var (text, desc) = LobbyUtils.SplitOnFirstToken(option.Description);
					checkbox.GetTooltipText = () => text;
					checkbox.GetTooltipDesc = () => desc;
				}

				checkbox.IsVisible = () => true;
				checkbox.IsChecked = () => missionOptions[option.Id] == "True";
				checkbox.IsDisabled = () => option.IsLocked;
				checkbox.OnClick = () =>
				{
					if (missionOptions[option.Id] == "True")
						missionOptions[option.Id] = "False";
					else
						missionOptions[option.Id] = "True";
				};
			}

			foreach (var option in allOptions.Where(o => o is not LobbyBooleanOption))
			{
				if (option.Id == "difficulty")
					SetMapDifficulty(option);
				else if (option.Id == "gamespeed")
					SetMapSpeed(option);
				else
					missionOptions[option.Id] = option.DefaultValue;

				if (dropdownColumns.Count == 0)
				{
					row = dropdownRowTemplate.Clone();
					row.Bounds.Y = yOffset;
					yOffset += row.Bounds.Height;
					foreach (var child in row.Children)
						if (child is DropDownButtonWidget dropDown)
							dropdownColumns.Enqueue(dropDown);

					optionsContainer.AddChild(row);
				}

				var val = dropdownColumns.Dequeue();
				SetupDropdown(val, option);

				var label = row.GetOrNull<LabelWidget>(val.Id + "_DESC");
				if (label != null)
				{
					label.GetText = () => option.Name + ":";
					label.IsVisible = () => true;
				}
			}
		}

		void BuildMinifiedOptions(LobbyOption[] allOptions)
		{
			if (difficultyButton != null)
			{
				var mapDifficulty = allOptions.FirstOrDefault(sld => sld.Id == "difficulty");
				if (mapDifficulty != null)
				{
					SetMapDifficulty(mapDifficulty);
					SetupDropdown(difficultyButton, mapDifficulty);
				}
				else
				{
					difficultyButton.IsDisabled = () => true;
					difficultyButton.GetText = () => unsetDifficulty;
					difficultyButton.GetTooltipText = () => defaultTooltop;
				}
			}

			if (gameSpeedButton != null)
			{
				var gameSpeed = allOptions.FirstOrDefault(sld => sld.Id == "gamespeed");
				if (gameSpeed != null)
				{
					SetMapSpeed(gameSpeed);
					SetupDropdown(gameSpeedButton, gameSpeed);
				}
				else
				{
					gameSpeedButton.IsDisabled = () => true;
					gameSpeedButton.GetText = () => FluentProvider.GetMessage(NotAvailable);
				}
			}
		}

		void SetupDropdown(DropDownButtonWidget dropdown, LobbyOption option)
		{
			dropdown.GetText = () =>
			{
				if (option.Values.TryGetValue(missionOptions[option.Id], out var value))
					return value;

				return FluentProvider.GetMessage(NotAvailable);
			};

			if (option.Description != null)
			{
				var (text, desc) = LobbyUtils.SplitOnFirstToken(option.Description);
				dropdown.GetTooltipText = () => text;
				dropdown.GetTooltipDesc = () => desc;
			}

			dropdown.IsVisible = () => true;
			dropdown.IsDisabled = () => option.IsLocked;

			dropdown.OnMouseDown = _ =>
			{
				ScrollItemWidget SetupItem(KeyValuePair<string, string> c, ScrollItemWidget template)
				{
					bool IsSelected() => missionOptions[option.Id] == c.Key;
					void OnClick() => OnOptionSelected(option.Id, c.Key);

					var item = ScrollItemWidget.Setup(template, IsSelected, OnClick);
					item.Get<LabelWidget>("LABEL").GetText = () => c.Value;
					return item;
				}

				dropdown.ShowDropDown("LABEL_DROPDOWN_TEMPLATE", option.Values.Count * 30, option.Values, SetupItem);
			};
		}

		float cachedSoundVolume;
		float cachedMusicVolume;
		void MuteSounds()
		{
			cachedSoundVolume = Game.Sound.SoundVolume;
			cachedMusicVolume = Game.Sound.MusicVolume;
			Game.Sound.SoundVolume = Game.Sound.MusicVolume = 0;
		}

		void UnMuteSounds()
		{
			if (cachedSoundVolume > 0)
				Game.Sound.SoundVolume = cachedSoundVolume;

			if (cachedMusicVolume > 0)
				Game.Sound.MusicVolume = cachedMusicVolume;
		}

		void PlayVideo(VideoPlayerWidget player, string video, PlayingVideo pv, Action onComplete = null)
		{
			if (!modData.DefaultFileSystem.Exists(video))
			{
				ConfirmationDialogs.ButtonPrompt(modData,
					title: NoVideoTitle,
					text: NoVideoPrompt,
					onCancel: () => { },
					cancelText: NoVideoCancel);
			}
			else
			{
				StopVideo(player);

				playingVideo = pv;
				player.LoadAndPlay(video);

				if (player.Video == null)
				{
					StopVideo(player);

					ConfirmationDialogs.ButtonPrompt(modData,
						title: CantPlayTitle,
						text: CantPlayPrompt,
						onCancel: () => { },
						cancelText: CantPlayCancel);
				}
				else
				{
					// video playback runs asynchronously
					player.PlayThen(() =>
					{
						StopVideo(player);
						onComplete?.Invoke();
					});

					// Mute other distracting sounds
					MuteSounds();
				}
			}
		}

		void StopVideo(VideoPlayerWidget player)
		{
			if (playingVideo == PlayingVideo.None)
				return;

			UnMuteSounds();
			player.Stop();
			playingVideo = PlayingVideo.None;
		}

		void StartMissionClicked(Action onExit)
		{
			StopVideo(videoPlayer);
			if (selectedMap == null || !CanStartMission(selectedMap))
				return;

			// If selected mission becomes unavailable, exit MissionBrowser to refresh
			var map = modData.MapCache.GetUpdatedMap(selectedMap.Uid);
			if (map == null)
			{
				Game.Disconnect();
				Ui.CloseWindow();
				onExit();
				return;
			}

			selectedMap = modData.MapCache[map];
			var orders = new List<Order>();

			foreach (var option in missionOptions)
				orders.Add(Order.Command($"option {option.Key} {option.Value}"));

			orders.Add(Order.Command($"state {Session.ClientState.Ready}"));

			var missionData = selectedMap.WorldActorInfo.TraitInfoOrDefault<MissionDataInfo>();
			if (missionData != null && missionData.StartVideo != null && modData.DefaultFileSystem.Exists(missionData.StartVideo))
			{
				var fsPlayer = fullscreenVideoPlayer.Get<VideoPlayerWidget>("PLAYER");
				fullscreenVideoPlayer.Visible = true;
				PlayVideo(fsPlayer, missionData.StartVideo, PlayingVideo.GameStart,
					() => Game.CreateAndStartLocalServer(selectedMap.Uid, orders));
			}
			else
				Game.CreateAndStartLocalServer(selectedMap.Uid, orders);
		}
	}
}
