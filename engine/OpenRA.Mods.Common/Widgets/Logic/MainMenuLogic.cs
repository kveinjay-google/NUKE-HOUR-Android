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
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using OpenRA.Graphics;
using OpenRA.Mods.Common.FileSystem;
using OpenRA.Network;
using OpenRA.Primitives;
using OpenRA.Support;
using OpenRA.Widgets;

namespace OpenRA.Mods.Common.Widgets.Logic
{
	public enum IosMainMenuShellProfile { Tablet, Standard, Ultrawide }

	public readonly struct IosMainMenuLayout
	{
		public WidgetBounds Panel { get; }
		public WidgetBounds Brand { get; }
		public WidgetBounds Situation { get; }
		public WidgetBounds[] Controls { get; }
		public IosMainMenuShellProfile Profile { get; }

		IosMainMenuLayout(
			WidgetBounds panel,
			WidgetBounds brand,
			WidgetBounds situation,
			WidgetBounds[] controls,
			IosMainMenuShellProfile profile)
		{
			Panel = panel;
			Brand = brand;
			Situation = situation;
			Controls = controls;
			Profile = profile;
		}

		public static IosMainMenuLayout Create(IosMenuLayoutPolicy policy, int controlCount = 7)
		{
			if (policy == null)
				throw new ArgumentNullException(nameof(policy));

			var count = Math.Max(0, controlCount);
			var content = policy.ContentBounds;
			var panel = policy.ViewportBounds;
			var profile = ProfileFor(panel.Width, panel.Height);

			var controls = new WidgetBounds[count];
			if (count == 0)
				return new IosMainMenuLayout(panel, default, default, controls, profile);

			var primaryCount = Math.Min(3, count);
			var auxiliaryCount = Math.Max(0, count - primaryCount);
			var routeLeft = Ratio(panel.Width, profile switch
			{
				IosMainMenuShellProfile.Ultrawide => 0.059,
				IosMainMenuShellProfile.Tablet => 0.054,
				_ => 0.059,
			});
			var routeRight = Ratio(panel.Width, profile switch
			{
				IosMainMenuShellProfile.Ultrawide => 0.414,
				IosMainMenuShellProfile.Tablet => 0.414,
				_ => 0.414,
			});
			routeLeft = Math.Max(routeLeft, content.X - panel.X);
			routeRight = Math.Min(routeRight, content.Right - panel.X);
			var routeWidth = Math.Max(policy.MinimumTarget, routeRight - routeLeft);
			var routeHeightRatio = profile switch
			{
				IosMainMenuShellProfile.Ultrawide => 0.145,
				IosMainMenuShellProfile.Tablet => 0.122,
				_ => 0.145,
			};
			var routeHeight = Math.Max(policy.MinimumTarget, Ratio(panel.Height, routeHeightRatio));
			var routeY = profile switch
			{
				IosMainMenuShellProfile.Ultrawide => new[] { 0.286, 0.446, 0.627 },
				IosMainMenuShellProfile.Tablet => new[] { 0.336, 0.474, 0.607 },
				_ => new[] { 0.286, 0.446, 0.627 },
			};
			var brand = profile switch
			{
				IosMainMenuShellProfile.Ultrawide => Bounds(panel, 0.055, 0.025, 0.360, 0.195),
				IosMainMenuShellProfile.Tablet => Bounds(panel, 0.045, 0.115, 0.370, 0.185),
				_ => Bounds(panel, 0.055, 0.025, 0.360, 0.195),
			};
			var situation = new WidgetBounds(routeRight + policy.Gap, 0,
				Math.Max(0, panel.Width - routeRight - policy.Gap), panel.Height);
			for (var i = 0; i < primaryCount; i++)
				controls[i] = new WidgetBounds(
					routeLeft,
					Ratio(panel.Height, routeY[i]),
					routeWidth,
					routeHeight);

			if (auxiliaryCount > 0)
			{
				// Phone artwork slots do not move with the notch. Clip edge buttons to
				// the safe area individually instead of redistributing all four slots.
				var fixedPhoneSlots = policy.IsPhone && auxiliaryCount == 4;
				var utilityLeft = fixedPhoneSlots ? Ratio(panel.Width, 0.025) :
					Math.Max(Ratio(panel.Width, 0.025), content.X - panel.X);
				var utilityRight = fixedPhoneSlots ? Ratio(panel.Width, 0.976) :
					Math.Min(Ratio(panel.Width, 0.976), content.Right - panel.X);
				var utilityGap = Math.Max(2, policy.Gap / 3);

				// The artwork has four physical slots, even when mobile omits desktop actions.
				var slotCount = Math.Max(4, auxiliaryCount);
				var auxiliaryWidth = Math.Max(policy.MinimumTarget,
					(utilityRight - utilityLeft - (slotCount - 1) * utilityGap) / slotCount);
				var auxiliaryHeight = Math.Max(policy.MinimumTarget, Ratio(panel.Height,
					profile == IosMainMenuShellProfile.Tablet ? 0.075 : 0.095));
				var targetY = Ratio(panel.Height,
					profile == IosMainMenuShellProfile.Tablet ? 0.844 : 0.846);
				var auxiliaryY = Math.Min(targetY, panel.Height - auxiliaryHeight);
				for (var i = 0; i < auxiliaryCount; i++)
				{
					var left = utilityLeft + i * (auxiliaryWidth + utilityGap);
					var right = left + auxiliaryWidth;
					if (fixedPhoneSlots)
					{
						left = Math.Max(left, content.X - panel.X);
						right = Math.Min(right, content.Right - panel.X);
					}

					controls[primaryCount + i] = new WidgetBounds(
						left, auxiliaryY, Math.Max(0, right - left), auxiliaryHeight);
				}
				// The desktop standard shell has a lower footer than the phone artwork.
				// Keep the four measured slots, including their unequal middle widths.
				if (!policy.Enabled && profile == IosMainMenuShellProfile.Standard && auxiliaryCount == 4)
				{
					var slotLeft = new[] { .031, .277, .507, .737 };
					var slotWidth = new[] { .230, .230, .220, .230 };
					for (var i = 0; i < 4; i++)
						controls[primaryCount + i] = new WidgetBounds(
							Ratio(panel.Width, slotLeft[i]), Ratio(panel.Height, .878),
							Ratio(panel.Width, slotWidth[i]), Ratio(panel.Height, .082));
				}
			}

			return new IosMainMenuLayout(panel, brand, situation, controls, profile);
		}

		static WidgetBounds Bounds(WidgetBounds panel, double x, double y, double width, double height) =>
			new(Ratio(panel.Width, x), Ratio(panel.Height, y),
				Ratio(panel.Width, width), Ratio(panel.Height, height));

		public static IosMainMenuShellProfile ProfileFor(int width, int height)
		{
			var aspect = height > 0 ? (double)width / height : 1;
			return aspect >= 1.95 ? IosMainMenuShellProfile.Ultrawide :
				aspect <= 1.55 ? IosMainMenuShellProfile.Tablet : IosMainMenuShellProfile.Standard;
		}

		static int Ratio(int value, double ratio) => Math.Max(0, (int)Math.Round(value * ratio));

		public static bool MatchesAppliedBounds(
			WidgetBounds expectedPanel,
			WidgetBounds actualPanel,
			IReadOnlyList<WidgetBounds> expectedControls,
			IReadOnlyList<Widget> actualControls)
		{
			if (!SameBounds(expectedPanel, actualPanel) || expectedControls == null || actualControls == null ||
				expectedControls.Count != actualControls.Count)
				return false;

			for (var i = 0; i < expectedControls.Count; i++)
				if (!SameBounds(expectedControls[i], actualControls[i].Bounds))
					return false;

			return true;
		}

		static bool SameBounds(WidgetBounds first, WidgetBounds second) =>
			first.X == second.X && first.Y == second.Y &&
			first.Width == second.Width && first.Height == second.Height;
	}

	public class MainMenuLogic : ChromeLogic
	{
		[FluentReference]
		const string LoadingNews = "label-loading-news";

		[FluentReference("message")]
		const string NewsRetrivalFailed = "label-news-retrieval-failed";

		[FluentReference("message")]
		const string NewsParsingFailed = "label-news-parsing-failed";

		[FluentReference("author", "datetime")]
		const string AuthorDateTime = "label-author-datetime";

		[FluentReference]
		const string InterfaceStyleClassic = "button-main-menu-ui-style-classic";

		[FluentReference]
		const string InterfaceStyleHighDefinition = "button-main-menu-ui-style-hd";

		protected enum MenuType { Main, Singleplayer, MapEditor, StartupPrompts, None }

		protected enum MenuPanel { None, Missions, Skirmish, Multiplayer, MapEditor, Replays, GameSaves }

		protected MenuType menuType = MenuType.Main;
		readonly Widget rootMenu;
		readonly ScrollPanelWidget newsPanel;
		readonly int maxNewsHeight;
		readonly Widget newsTemplate;
		readonly LabelWidget newsStatus;
		readonly ModData modData;
		readonly World world;
		readonly ContentInstallerFileSystemLoader contentInstaller;
		readonly Widget mainMenu;
		readonly Widget[] mainMenuControls;
		readonly ButtonWidget languageButton;
		readonly bool hasSingleplayerMenu;
		readonly bool iosMainMenuLayoutEnabled;
		Size lastIosResolution;
		Size lastIosNativePointSize;
		Rectangle lastIosSafeBounds;
		int2 lastIosParentRenderOrigin;
		WidgetBounds lastIosPanelBounds;
		WidgetBounds[] lastIosControlBounds;
		bool iosLayoutInitialized;

		// Update news once per game launch
		static bool fetchedNews;

		protected static MenuPanel lastGameState = MenuPanel.None;
		static string pendingRankedResultMatchId;
		static string pendingRankedResultOpponent;
		MenuType SingleplayerOrMain => hasSingleplayerMenu ? MenuType.Singleplayer : MenuType.Main;

		bool newsOpen;
		readonly Stopwatch skirmishTransition = new();

		void SwitchMenu(MenuType type)
		{
			menuType = type;

			DiscordService.UpdateStatus(DiscordState.InMenu);

			// Update button mouseover
			Game.RunAfterTick(Ui.ResetTooltips);
		}

		[ObjectCreator.UseCtor]
		public MainMenuLogic(Widget widget, World world, ModData modData)
		{
			this.modData = modData;

			if (Platform.IsAndroid && modData.Manifest.Id == "ra2")
			{
				// Prepare the last selected lobby map while the startup screen is still visible.
				var initialMap = modData.MapCache.ChooseInitialMap(modData.MapCache.PickLastModifiedMap(MapVisibility.Lobby) ?? Game.Settings.Server.Map, Game.CosmeticRandom);
				try { modData.MapCache[initialMap].LoadRuleset(); }
				catch (Exception e) { Log.Write("debug", $"Initial lobby rules warmup failed: {e.Message}"); }
				var screen = IosScreenMetrics.SnapshotFor(Game.Renderer.Resolution);
				if (IosMenuLayoutPolicy.Create(true, screen).IsPhone)
				{
					// Load the phone entry artwork before accepting menu taps, rather than in Draw.
					foreach (var panel in new[] { "cc-ingame-command-shell", "cc-mp-control", "cc-ranked-phone-hub-v7", "cc-ranked-desktop-secondary",
						"cc-ranked-armor-ranked-icon", "cc-ranked-armor-local-icon", "cc-ranked-armor-online-icon", "cc-ranked-armor-leaderboard-icon" })
						ChromeProvider.TryGetPanelImages(panel);
					ChromeProvider.TryGetImage("flags-hd", "Random");
				}
			}

			rootMenu = widget;
			ConfigureSchemeCBackgrounds(widget);
			var versionLabel = widget.GetOrNull("VERSION_LABEL");
			if (versionLabel != null && Platform.UsesMobileLayout)
				versionLabel.Visible = false;

			// Menu buttons
			mainMenu = widget.Get("MAIN_MENU");
			mainMenu.IsVisible = () => menuType == MenuType.Main;

			var singleplayerButton = mainMenu.Get<ButtonWidget>("SINGLEPLAYER_BUTTON");
			singleplayerButton.OnClick = StartSkirmishGame;

			var multiplayerButton = mainMenu.Get<ButtonWidget>("MULTIPLAYER_BUTTON");
			multiplayerButton.OnClick = OpenMultiplayerPanel;

			var hasMissions = modData.MapCache
				.Any(p => p.Status == MapStatus.Available && p.Visibility.HasFlag(MapVisibility.MissionSelector));
			var campaignButton = mainMenu.Get<ButtonWidget>("CAMPAIGN_BUTTON");
			campaignButton.OnClick = () => OpenMissionBrowserPanel(
				modData.MapCache.PickLastModifiedMap(MapVisibility.MissionSelector), MenuType.Main);
			campaignButton.Disabled = !hasMissions;

			contentInstaller = modData.FileSystemLoader as ContentInstallerFileSystemLoader;
			this.world = world;

			var settingsButton = mainMenu.Get<ButtonWidget>("SETTINGS_BUTTON");
			settingsButton.OnClick = OpenSettingsPanel;

			var thanksButton = mainMenu.Get<ButtonWidget>("THANKS_BUTTON");
			thanksButton.OnClick = OpenSpecialThanksPanel;

			var aboutButton = mainMenu.GetOrNull<ButtonWidget>("ABOUT_BUTTON");
			var showAboutButton = aboutButton != null && (Platform.CurrentPlatform == PlatformType.OSX || Platform.UsesMobileLayout);
			if (aboutButton != null)
			{
				aboutButton.Visible = showAboutButton;
				if (showAboutButton)
					aboutButton.OnClick = OpenAboutPanel;
			}

			var quitButton = mainMenu.Get<ButtonWidget>("QUIT_BUTTON");
			quitButton.Visible = true;
			if (quitButton.Visible)
				quitButton.OnClick = Game.Exit;

			languageButton = mainMenu.GetOrNull<ButtonWidget>("LANGUAGE_BUTTON");
			if (languageButton != null)
			{
				languageButton.GetText = () => Platform.UsesMobileLayout ?
					GetIosLanguageDisplayName(Game.Settings.Game.Language, FluentProvider.CurrentLanguage) :
					LanguageSelectionPolicy.GetDisplayName(
						Game.Settings.Game.Language, FluentProvider.CurrentLanguage);
				languageButton.OnClick = () =>
				{
					var currentMod = modData.Manifest.Id;
					ApplyLanguageSelection(
						Game.Settings.Game,
						Game.Settings.SystemLanguageTag,
						FluentProvider.CurrentLanguage,
						NextLanguagePreference(Game.Settings.Game.Language),
						Game.Settings.Save,
						() => Game.RunAfterTick(() => Game.InitializeMod(currentMod, new Arguments())));
				};
			}

			var interfaceStyleButton = mainMenu.GetOrNull<ButtonWidget>("UI_STYLE_BUTTON");
			if (interfaceStyleButton != null)
			{
				interfaceStyleButton.GetText = () => FluentProvider.GetMessage(
					InterfaceStyleLabelKey(Game.Settings.Game.EffectiveInterfaceStyle));
				interfaceStyleButton.OnClick = () =>
					ToggleInterfaceStyle(Game.Settings.Game, Game.Settings.Save);
			}

			ConfigureRasterWordmark(singleplayerButton, "singleplayer");
			ConfigureRasterWordmark(multiplayerButton, "multiplayer");
			ConfigureRasterWordmark(campaignButton, "campaign");
			ConfigureRasterWordmark(settingsButton, "settings");
			ConfigureRasterWordmark(thanksButton, "thanks");
			ConfigureRasterWordmark(languageButton, "language");
			ConfigureRasterWordmark(interfaceStyleButton, "interface",
				() => Game.Settings.Game.EffectiveInterfaceStyle != InterfaceStyleMode.Classic);
			ConfigureRasterWordmark(aboutButton, "about");
			ConfigureRasterWordmark(quitButton, "quit");

			var controls = new List<Widget>
			{
				singleplayerButton, multiplayerButton, campaignButton
			};
			controls.Add(settingsButton);
			controls.Add(thanksButton);
			if (languageButton != null)
				controls.Add(languageButton);

			if (interfaceStyleButton != null)
				controls.Add(interfaceStyleButton);

			if (aboutButton != null)
			{
				if (showAboutButton)
					controls.Add(aboutButton);
				else if (quitButton.Visible)
					quitButton.Bounds.Y = aboutButton.Bounds.Y;
			}

			if (quitButton.Visible)
				controls.Add(quitButton);
			mainMenuControls = controls.ToArray();
			iosMainMenuLayoutEnabled = mainMenuControls.Length >= 4;
			ApplyIosMainMenuLayout();

			// Singleplayer menu
			var hasMaps = modData.MapCache.Any(p => p.Visibility.HasFlag(MapVisibility.Lobby));
			singleplayerButton.Disabled = !hasMaps;
			var singleplayerMenu = widget.GetOrNull("SINGLEPLAYER_MENU");
			hasSingleplayerMenu = singleplayerMenu != null;
			if (singleplayerMenu != null)
			{
				singleplayerMenu.IsVisible = () => menuType == MenuType.Singleplayer;
				ConfigureRasterWordmarkImage(
					singleplayerMenu.GetOrNull<ImageWidget>("SINGLEPLAYER_MENU_TITLE"), "singleplayer");

				var missionsButton = singleplayerMenu.Get<ButtonWidget>("MISSIONS_BUTTON");
				ConfigureRasterWordmark(missionsButton, "campaign");
				missionsButton.OnClick = () => OpenMissionBrowserPanel(
					modData.MapCache.PickLastModifiedMap(MapVisibility.MissionSelector));
				missionsButton.Disabled = !hasMissions;

				var skirmishButton = singleplayerMenu.Get<ButtonWidget>("SKIRMISH_BUTTON");
				ConfigureRasterWordmark(skirmishButton, "skirmish");
				skirmishButton.OnClick = StartSkirmishGame;
				skirmishButton.Disabled = !hasMaps;

				var loadButton = singleplayerMenu.Get<ButtonWidget>("LOAD_BUTTON");
				ConfigureRasterWordmark(loadButton, "load");
				loadButton.IsDisabled = () => !GameSaveBrowserLogic.IsLoadPanelEnabled(modData.Manifest);
				loadButton.OnClick = OpenGameSaveBrowserPanel;

				var encyclopediaButton = singleplayerMenu.GetOrNull<ButtonWidget>("ENCYCLOPEDIA_BUTTON");
				if (encyclopediaButton != null)
					encyclopediaButton.OnClick = OpenEncyclopediaPanel;

				var singleplayerBackButton = singleplayerMenu.Get<ButtonWidget>("BACK_BUTTON");
				ConfigureRasterWordmark(singleplayerBackButton, "back");
				singleplayerBackButton.OnClick = () => SwitchMenu(MenuType.Main);
			}

			// Map editor menu
			var mapEditorMenu = widget.Get("MAP_EDITOR_MENU");
			mapEditorMenu.IsVisible = () => menuType == MenuType.MapEditor;

			// Loading into the map editor
			Game.BeforeGameStart += RemoveShellmapUI;

			var onSelect = new Action<string>(uid =>
			{
				if (modData.MapCache[uid].Status != MapStatus.Available)
					SwitchMenu(MenuType.Main);
				else
					LoadMapIntoEditor(modData.MapCache[uid].Uid);
			});

			var newMapButton = widget.Get<ButtonWidget>("NEW_MAP_BUTTON");
			newMapButton.OnClick = () =>
			{
				SwitchMenu(MenuType.None);
				Game.OpenWindow("NEW_MAP_BG", new WidgetArgs()
				{
					{ "onSelect", onSelect },
					{ "onExit", () => SwitchMenu(MenuType.MapEditor) }
				});
			};

			var loadMapButton = widget.Get<ButtonWidget>("LOAD_MAP_BUTTON");
			loadMapButton.OnClick = () =>
			{
				SwitchMenu(MenuType.None);
				Game.OpenWindow("MAPCHOOSER_PANEL", new WidgetArgs()
				{
					{ "initialMap", null },
					{ "remoteMapPool", null },
					{ "initialTab", MapClassification.User },
					{ "onExit", () => SwitchMenu(MenuType.MapEditor) },
					{ "onSelect", onSelect },
					{ "filter", MapVisibility.Lobby | MapVisibility.Shellmap | MapVisibility.MissionSelector },
				});
			};

			loadMapButton.Disabled = !hasMaps;

			mapEditorMenu.Get<ButtonWidget>("BACK_BUTTON").OnClick = () => SwitchMenu(MenuType.Main);

			var newsBG = widget.GetOrNull("NEWS_BG");
			if (newsBG != null)
			{
				newsBG.IsVisible = () => Game.Settings.Game.FetchNews && menuType != MenuType.None && menuType != MenuType.StartupPrompts;

				newsPanel = Ui.LoadWidget<ScrollPanelWidget>("NEWS_PANEL", null, new WidgetArgs());
				newsTemplate = newsPanel.Get("NEWS_ITEM_TEMPLATE");
				newsPanel.RemoveChild(newsTemplate);
				maxNewsHeight = newsPanel.Bounds.Height;

				newsStatus = newsPanel.Get<LabelWidget>("NEWS_STATUS");
				SetNewsStatus(FluentProvider.GetMessage(LoadingNews));
			}

			Game.OnRemoteDirectConnect += OnRemoteDirectConnect;

			// Check for updates in the background
			var webServices = modData.Manifest.Get<WebServices>();
			if (Game.Settings.Debug.CheckVersion)
				webServices.CheckModVersion();

			var updateLabel = rootMenu.GetOrNull("UPDATE_NOTICE");
			if (updateLabel != null)
				updateLabel.IsVisible = () => !newsOpen && menuType != MenuType.None &&
					menuType != MenuType.StartupPrompts &&
					webServices.ModVersionStatus == ModVersionStatus.Outdated;

			menuType = MenuType.StartupPrompts;

			void OnIntroductionComplete()
			{
				LoadAndDisplayNews(webServices, newsBG);
				SwitchMenu(MenuType.Main);
				LaunchIntoRequestedPanel();
			}

			if (IntroductionPromptLogic.ShouldShowPrompt())
			{
				Game.OpenWindow("MAINMENU_INTRODUCTION_PROMPT", new WidgetArgs
				{
					{ "onComplete", OnIntroductionComplete }
				});
			}
			else
				OnIntroductionComplete();

			Game.OnShellmapLoaded += OpenMenuBasedOnLastGame;

			DiscordService.UpdateStatus(DiscordState.InMenu);
		}

		public static bool ApplyLanguageSelection(
			GameSettings settings,
			string systemLanguageTag,
			string appliedLanguage,
			string selectedPreference,
			Action save,
			Action scheduleReload)
		{
			if (settings == null)
				throw new ArgumentNullException(nameof(settings));
			if (save == null)
				throw new ArgumentNullException(nameof(save));
			if (scheduleReload == null)
				throw new ArgumentNullException(nameof(scheduleReload));

			settings.Language = LanguageSelectionPolicy.NormalizePreference(selectedPreference);
			save();
			var requiresReload = LanguageSelectionPolicy.RequiresReload(
				appliedLanguage, settings.Language, systemLanguageTag);
			if (requiresReload)
				scheduleReload();

			return requiresReload;
		}

		public static void ToggleInterfaceStyle(GameSettings settings, Action save)
		{
			if (settings == null)
				throw new ArgumentNullException(nameof(settings));
			if (save == null)
				throw new ArgumentNullException(nameof(save));

			settings.InterfaceStyle = settings.EffectiveInterfaceStyle switch
			{
				InterfaceStyleMode.Classic => InterfaceStyleMode.ClassicHD,
				_ => InterfaceStyleMode.Classic,
			};
			settings.UseHighDefinitionUI = settings.InterfaceStyle != InterfaceStyleMode.Classic;
			save();
		}

		public static InterfaceStyleMode[] VisibleInterfaceStyles() =>
			new[] { InterfaceStyleMode.Classic, InterfaceStyleMode.ClassicHD };

		public static string InterfaceStyleLabelKey(InterfaceStyleMode style) => style switch
		{
			InterfaceStyleMode.Classic => "label-ui-style-classic",
			InterfaceStyleMode.ClassicHD => "label-ui-style-classic-hd",
			_ => "label-ui-style-modern-hd",
		};

		public static string GetIosLanguageDisplayName(string preference, string uiLanguage)
		{
			if (LanguageSelectionPolicy.NormalizePreference(preference) ==
				LanguageSelectionPolicy.SystemPreference)
				return LanguageSelectionPolicy.GetDisplayName(
					LanguageSelectionPolicy.Resolve(preference, uiLanguage), uiLanguage);

			return LanguageSelectionPolicy.GetDisplayName(preference, uiLanguage);
		}

		public static string NextLanguagePreference(string preference)
		{
			var preferences = LanguageSelectionPolicy.SupportedPreferences;
			var normalized = LanguageSelectionPolicy.NormalizePreference(preference);
			var index = 0;
			for (var i = 0; i < preferences.Count; i++)
				if (preferences[i] == normalized)
				{
					index = i;
					break;
				}

			return preferences[(index + 1) % preferences.Count];
		}

		static string RasterWordmarkCollection()
		{
			return FluentProvider.CurrentLanguage.StartsWith("zh", StringComparison.OrdinalIgnoreCase) ?
				"cc-soviet-wordmarks-zh" : "cc-soviet-wordmarks-en";
		}

		static string RasterWordmarkCollectionFor(string phrase)
		{
			if (phrase == "thanks")
				return FluentProvider.CurrentLanguage.StartsWith("zh", StringComparison.OrdinalIgnoreCase) ?
					"cc-soviet-compact-wordmarks-thanks-zh" : "cc-soviet-compact-wordmarks-thanks-en";

			return RasterWordmarkCollection();
		}

		static void ConfigureRasterWordmarkImage(ImageWidget image, string phrase)
		{
			if (image == null)
				return;

			var imageName = phrase + "-normal";
			image.GetImageCollection = RasterWordmarkCollection;
			image.GetImageName = () => imageName;
			image.IsVisible = () => ChromeProvider.TryGetImage(RasterWordmarkCollection(), imageName) != null;
		}

		static void ConfigureRasterWordmark(
			ButtonWidget button, string phrase, Func<bool> useRaster = null)
		{
			if (button == null)
				return;

			var image = button.GetOrNull<ImageWidget>("WORDMARK");
			if (image == null)
				return;

			useRaster ??= () => true;
			var fallbackText = button.GetText;
			bool HasRaster()
			{
				return useRaster() && ChromeProvider.TryGetImage(
					RasterWordmarkCollectionFor(phrase), phrase + "-normal") != null;
			}

			string State()
			{
				if (button.IsDisabled())
					return "disabled";

				var hovered = Ui.MouseOverWidget == button || button.Children.Contains(Ui.MouseOverWidget);
				return button.IsVisuallyPressed || hovered || button.IsHighlighted() ? "selected" : "normal";
			}

			image.GetImageCollection = () => RasterWordmarkCollectionFor(phrase);
			image.GetImageName = () => phrase + "-" + State();
			image.IsVisible = HasRaster;
			button.GetText = () => HasRaster() ? string.Empty : fallbackText();
			button.GetTooltipText = fallbackText;
		}

		void ApplyIosMainMenuLayout()
		{
			// The shell selects its artwork by aspect ratio on desktop too.
			// Keep hit regions and labels on that same coordinate system.
			if (!iosMainMenuLayoutEnabled || (!Platform.UsesMobileLayout && Game.ModData.Manifest.Id != "ra2"))
				return;

			var resolution = Game.Renderer.Resolution;
			var snapshot = IosScreenMetrics.SnapshotFor(resolution);
			var policy = IosMenuLayoutPolicy.Create(Platform.UsesMobileLayout, snapshot);
			var layout = IosMainMenuLayout.Create(policy, mainMenuControls.Length);
			var parentRenderOrigin = mainMenu.Parent.RenderOrigin;
			var relativePanel = IosTouchWidgetPolicy.RelativeToRoot(
				layout.Panel.ToRectangle(), parentRenderOrigin);
			mainMenu.Bounds = new WidgetBounds(
				relativePanel.X, relativePanel.Y, relativePanel.Width, relativePanel.Height);
			var brand = mainMenu.GetOrNull("BRAND");
			if (brand != null)
			{
				brand.Bounds = layout.Brand;
				LayoutBrand(brand, policy.Gap);
			}

			var situation = mainMenu.GetOrNull("SITUATION_PANEL");
			if (situation != null)
			{
				situation.Bounds = layout.Situation;
				var display = situation.GetOrNull("SITUATION_DISPLAY");
				if (display != null)
					display.Bounds = new WidgetBounds(
						policy.Gap, policy.Gap,
						Math.Max(0, layout.Situation.Width - 2 * policy.Gap),
						Math.Max(0, layout.Situation.Height - 2 * policy.Gap));
			}

			for (var i = 0; i < mainMenuControls.Length; i++)
			{
				mainMenuControls[i].Bounds = layout.Controls[i];
				LayoutControlDecorations(mainMenuControls[i], i < 3, policy.Gap);
			}

			if (Platform.UsesMobileLayout)
				IosTouchMenuLogic.ApplyIosFonts(mainMenu);

			lastIosResolution = resolution;
			lastIosNativePointSize = snapshot.NativePointSize;
			lastIosSafeBounds = snapshot.SafeBounds;
			lastIosParentRenderOrigin = parentRenderOrigin;
			lastIosPanelBounds = mainMenu.Bounds;
			lastIosControlBounds = new WidgetBounds[mainMenuControls.Length];
			for (var i = 0; i < mainMenuControls.Length; i++)
				lastIosControlBounds[i] = mainMenuControls[i].Bounds;

			iosLayoutInitialized = true;
		}

		static void LayoutBrand(Widget brand, int gap)
		{
			var lockup = brand.GetOrNull("BRAND_LOCKUP");
			if (lockup != null)
			{
				lockup.Bounds = new WidgetBounds(0, 0, brand.Bounds.Width, brand.Bounds.Height);
				return;
			}

			var icon = brand.GetOrNull("BRAND_MARK");
			var title = brand.GetOrNull("BRAND_TITLE");
			var subtitle = brand.GetOrNull("BRAND_SUBTITLE");
			var iconSize = Math.Max(1, Math.Min(brand.Bounds.Height, brand.Bounds.Width / 3));
			if (icon != null)
				icon.Bounds = new WidgetBounds(0, 0, iconSize, iconSize);

			var textX = iconSize + gap;
			var textWidth = Math.Max(0, brand.Bounds.Width - textX);
			if (title != null)
				title.Bounds = new WidgetBounds(textX, 0, textWidth, Math.Max(1, brand.Bounds.Height * 2 / 3));
			if (subtitle != null)
				subtitle.Bounds = new WidgetBounds(textX, brand.Bounds.Height * 2 / 3,
					textWidth, Math.Max(1, brand.Bounds.Height / 3));
		}

		static void LayoutControlDecorations(Widget control, bool route, int gap)
		{
			var decorationGap = Math.Max(2, gap / 2);
			var icon = control.GetOrNull("ICON");
			if (icon != null)
			{
				var iconSize = Math.Max(1, (int)Math.Round(control.Bounds.Height * (route ? 0.58 : 0.52)));
				icon.Bounds = new WidgetBounds(
					Math.Max(decorationGap, control.Bounds.Width / 22),
					Math.Max(0, (control.Bounds.Height - iconSize) / 2), iconSize, iconSize);
			}

			var chevron = control.GetOrNull("CHEVRON");
			if (chevron != null)
				chevron.Bounds = new WidgetBounds(
					Math.Max(0, control.Bounds.Width - Math.Max(gap * 2, control.Bounds.Width / 8)),
					0, Math.Max(gap * 2, control.Bounds.Width / 8), control.Bounds.Height);

			var wordmark = control.GetOrNull("WORDMARK");
			if (wordmark != null)
			{
				var left = icon != null ? icon.Bounds.Right + decorationGap : decorationGap;
				var right = chevron != null ? chevron.Bounds.Left - decorationGap :
					control.Bounds.Width - decorationGap;
				wordmark.Bounds = new WidgetBounds(
					left, Math.Max(0, control.Bounds.Height / 10), Math.Max(1, right - left),
					Math.Max(1, control.Bounds.Height * 4 / 5));
			}
		}

		void ConfigureSchemeCBackgrounds(Widget widget)
		{
			var backgrounds = new[]
			{
				("MENU_BG_STANDARD", false, IosMainMenuShellProfile.Standard),
				("MENU_BG_TABLET", false, IosMainMenuShellProfile.Tablet),
				("MENU_BG_ULTRAWIDE", false, IosMainMenuShellProfile.Ultrawide),
				("SINGLEPLAYER_BG_STANDARD", true, IosMainMenuShellProfile.Standard),
				("SINGLEPLAYER_BG_TABLET", true, IosMainMenuShellProfile.Tablet),
				("SINGLEPLAYER_BG_ULTRAWIDE", true, IosMainMenuShellProfile.Ultrawide),
			};

			foreach (var (id, singleplayer, profile) in backgrounds)
			{
				var background = widget.GetOrNull(id);
				if (background == null)
					continue;

				background.IsVisible = () =>
				{
					var resolution = Game.Renderer.Resolution;
					var settingsLayout = Platform.UsesMobileLayout ?
						IosSettingsLayout.ForSnapshot(true, IosScreenMetrics.SnapshotFor(resolution)) :
						IosSettingsLayout.ForPreview(Environment.GetEnvironmentVariable("NUKEHOUR_SETTINGS_PREVIEW"), resolution);
					if (settingsLayout.Enabled && !settingsLayout.IsPhone && Ui.CurrentWindow()?.Id == "SETTINGS_PANEL")
						return false;
					var activeProfile = IosMainMenuLayout.ProfileFor(resolution.Width, resolution.Height);
					return (menuType == MenuType.Singleplayer) == singleplayer && activeProfile == profile;
				};
			}
		}

		public override void Tick()
		{
			if (!iosMainMenuLayoutEnabled || (!Platform.UsesMobileLayout && Game.ModData.Manifest.Id != "ra2"))
				return;

			var resolution = Game.Renderer.Resolution;
			var snapshot = IosScreenMetrics.SnapshotFor(resolution);
			if (!iosLayoutInitialized || resolution != lastIosResolution ||
				snapshot.NativePointSize != lastIosNativePointSize || snapshot.SafeBounds != lastIosSafeBounds ||
				mainMenu.Parent.RenderOrigin != lastIosParentRenderOrigin ||
				!IosMainMenuLayout.MatchesAppliedBounds(
					lastIosPanelBounds, mainMenu.Bounds,
					lastIosControlBounds, mainMenuControls))
				ApplyIosMainMenuLayout();
		}

		void LoadAndDisplayNews(WebServices webServices, Widget newsBG)
		{
			if (newsBG != null && Game.Settings.Game.FetchNews)
			{
				var cacheFile = Path.Combine(Platform.SupportDir, webServices.GameNewsFileName);
				var currentNews = ParseNews(cacheFile);
				if (currentNews != null)
					DisplayNews(currentNews);

				var newsButton = newsBG.GetOrNull<DropDownButtonWidget>("NEWS_BUTTON");
				if (newsButton != null)
				{
					if (!fetchedNews)
					{
						Task.Run(async () =>
						{
							try
							{
								var client = HttpClientFactory.Create();

								// Send the mod and engine version to support version-filtered news (update prompts)
								var url = new HttpQueryBuilder(webServices.GameNews)
								{
									{ "version", Game.EngineVersion },
									{ "mod", modData.Manifest.Id },
									{ "modversion", modData.Manifest.Metadata.DisplayVersionOrVersion }
								}.ToString();

								var response = await client.GetStringAsync(url);
								await File.WriteAllTextAsync(cacheFile, response);

								Game.RunAfterTick(() => // run on the main thread
								{
									fetchedNews = true;
									var newNews = ParseNews(cacheFile);
									if (newNews == null)
										return;

									DisplayNews(newNews);

									if (currentNews == null || newNews.Any(n => !currentNews.Select(c => c.DateTime).Contains(n.DateTime)))
										OpenNewsPanel(newsButton);
								});
							}
							catch (Exception e)
							{
								Game.RunAfterTick(() => // run on the main thread
									SetNewsStatus(FluentProvider.GetMessage(NewsRetrivalFailed, "message", e.Message)));
							}
						});
					}

					newsButton.OnClick = () => OpenNewsPanel(newsButton);
				}
			}
		}

		void OpenNewsPanel(DropDownButtonWidget button)
		{
			newsOpen = true;
			button.AttachPanel(newsPanel, () => newsOpen = false);
		}

		void OnRemoteDirectConnect(ConnectionTarget endpoint)
		{
			SwitchMenu(MenuType.None);
			Ui.OpenWindow("MULTIPLAYER_PANEL", new WidgetArgs
			{
				{ "onStart", RemoveShellmapUI },
				{ "onExit", () => SwitchMenu(MenuType.Main) },
				{ "directConnectEndPoint", endpoint },
				{ "initialMode", MultiplayerServerMode.Local },
			});
		}

		static void LoadMapIntoEditor(string uid)
		{
			Game.LoadEditor(uid);

			DiscordService.UpdateStatus(DiscordState.InMapEditor);

			lastGameState = MenuPanel.MapEditor;
		}

		void SetNewsStatus(string message)
		{
			message = WidgetUtils.WrapText(message, newsStatus.Bounds.Width, Game.Renderer.Fonts[newsStatus.Font]);
			newsStatus.GetText = () => message;
		}

		sealed class NewsItem
		{
			public string Title;
			public string Author;
			public DateTime DateTime;
			public string Content;
		}

		NewsItem[] ParseNews(string path)
		{
			if (!File.Exists(path))
				return null;

			try
			{
				return MiniYaml.FromFile(path).Select(node =>
				{
					var nodesDict = node.Value.ToDictionary();
					return new NewsItem
					{
						Title = nodesDict["Title"].Value,
						Author = nodesDict["Author"].Value,
						DateTime = FieldLoader.GetValue<DateTime>("DateTime", node.Key),
						Content = nodesDict["Content"].Value
					};
				}).ToArray();
			}
			catch (Exception ex)
			{
				SetNewsStatus(FluentProvider.GetMessage(NewsParsingFailed, "message", ex.Message));
			}

			return null;
		}

		void DisplayNews(IEnumerable<NewsItem> newsItems)
		{
			newsPanel.RemoveChildren();
			SetNewsStatus("");

			foreach (var item in newsItems)
			{
				var newsItem = newsTemplate.Clone();

				var titleLabel = newsItem.Get<LabelWidget>("TITLE");
				titleLabel.GetText = () => item.Title;

				var authorDateTimeLabel = newsItem.Get<LabelWidget>("AUTHOR_DATETIME");
				var authorDateTime = FluentProvider.GetMessage(AuthorDateTime,
					"author", item.Author,
					"datetime", item.DateTime.ToLocalTime().ToString(CultureInfo.CurrentCulture));

				authorDateTimeLabel.GetText = () => authorDateTime;

				var contentLabel = newsItem.Get<LabelWidget>("CONTENT");
				var content = item.Content.Replace("\\n", "\n");
				content = WidgetUtils.WrapText(content, contentLabel.Bounds.Width, Game.Renderer.Fonts[contentLabel.Font]);
				contentLabel.GetText = () => content;
				contentLabel.Bounds.Height = Game.Renderer.Fonts[contentLabel.Font].Measure(content).Y;
				newsItem.Bounds.Height += contentLabel.Bounds.Height;

				newsPanel.AddChild(newsItem);
				newsPanel.Layout.AdjustChildren();
				newsPanel.Bounds.Height = Math.Min(newsPanel.ContentHeight, maxNewsHeight);
			}
		}

		void RemoveShellmapUI()
		{
			rootMenu.Parent.RemoveChild(rootMenu);
		}

		void StartSkirmishGame()
		{
			skirmishTransition.Restart();
			SwitchMenu(MenuType.None);

			var map = modData.MapCache.ChooseInitialMap(modData.MapCache.PickLastModifiedMap(MapVisibility.Lobby) ?? Game.Settings.Server.Map, Game.CosmeticRandom);
			Game.Settings.Server.Map = map;
			Game.Settings.Save();
			Log.Write("debug", $"Skirmish transition: map selection {skirmishTransition.ElapsedMilliseconds} ms");
			var endpoint = Game.CreateLocalServer(map, isSkirmish: true);
			Log.Write("debug", $"Skirmish transition: local server {skirmishTransition.ElapsedMilliseconds} ms");

			ConnectionLogic.Connect(endpoint,
				"",
				OpenSkirmishLobbyPanel,
				() => { Game.CloseServer(); SwitchMenu(MenuType.Main); });
		}

		void OpenMissionBrowserPanel(string map, MenuType? returnMenu = null)
		{
			SwitchMenu(MenuType.None);
			Game.OpenWindow("MISSIONBROWSER_PANEL", new WidgetArgs
			{
				{ "onExit", () => { Game.Disconnect(); SwitchMenu(returnMenu ?? SingleplayerOrMain); } },
				{ "onStart", () => { RemoveShellmapUI(); lastGameState = MenuPanel.Missions; } },
				{ "initialMap", map }
			});
		}

		void OpenEncyclopediaPanel()
		{
			SwitchMenu(MenuType.None);
			Game.OpenWindow("ENCYCLOPEDIA_PANEL", new WidgetArgs
			{
				{ "onExit", () => SwitchMenu(SingleplayerOrMain) }
			});
		}

		void OpenSkirmishLobbyPanel()
		{
			Log.Write("debug", $"Skirmish transition: connected {skirmishTransition.ElapsedMilliseconds} ms");
			SwitchMenu(MenuType.None);
			Game.OpenWindow("SERVER_LOBBY", new WidgetArgs
			{
				{ "onExit", () => { Game.Disconnect(); SwitchMenu(MenuType.Main); } },
				{ "onStart", () => { RemoveShellmapUI(); lastGameState = MenuPanel.Skirmish; } },
				{ "skirmishMode", true }
			});
			Log.Write("debug", $"Skirmish transition: lobby constructed {skirmishTransition.ElapsedMilliseconds} ms");
			Game.RunAfterTick(() => Log.Write("debug", $"Skirmish transition: lobby first tick {skirmishTransition.ElapsedMilliseconds} ms"));
		}

		void OpenMultiplayerPanel()
		{
			SwitchMenu(MenuType.None);
			OpenMultiplayerHub();
		}

		void OpenMultiplayerHub()
		{
			var elapsed = Stopwatch.StartNew();
			Ui.OpenWindow("MULTIPLAYER_HUB", new WidgetArgs
			{
				{ "onExit", () => SwitchMenu(MenuType.Main) },
				{ "openOnline", () => OpenRoomBrowser(MultiplayerServerMode.Online) },
				{ "openLocal", () => OpenRoomBrowser(MultiplayerServerMode.Local) },
				{ "openRanked", OpenRankedPanel },
				{ "openLeaderboard", OpenRankedLeaderboard },
			});
			Log.Write("debug", $"Multiplayer transition: constructed {elapsed.ElapsedMilliseconds} ms");
			Game.RunAfterTick(() => Log.Write("debug", $"Multiplayer transition: first tick {elapsed.ElapsedMilliseconds} ms"));
		}

		void OpenRoomBrowser(MultiplayerServerMode mode)
		{
			Ui.OpenWindow("MULTIPLAYER_PANEL", new WidgetArgs
			{
				{ "onStart", () => { RemoveShellmapUI(); lastGameState = MenuPanel.Multiplayer; } },
				{ "onExit", OpenMultiplayerHub },
				{ "directConnectEndPoint", null },
				{ "initialMode", mode },
			});
		}

		void OpenRankedPanel()
		{
			Ui.OpenWindow("RANKED_MATCH_PANEL", new WidgetArgs
			{
				{ "onExit", OpenMultiplayerHub },
				{ "onAssigned", (Action<RankedMatchAssignment>)ConnectRankedAssignment },
			});
		}

		void OpenRankedLeaderboard()
		{
			Ui.OpenWindow("RANKED_LEADERBOARD_PANEL", new WidgetArgs
			{
				{ "onExit", OpenMultiplayerHub },
			});
		}

		void OpenRankedResult(string matchId, string opponent)
		{
			SwitchMenu(MenuType.None);
			Ui.OpenWindow("RANKED_RESULT_PANEL", new WidgetArgs
			{
				{ "matchId", matchId },
				{ "opponent", opponent },
				{ "onExit", OpenMultiplayerHub },
			});
		}

		void ConnectRankedAssignment(RankedMatchAssignment assignment)
		{
			void OnLobbyExit()
			{
				pendingRankedResultMatchId = null;
				pendingRankedResultOpponent = null;
				Game.Disconnect();
				SwitchMenu(MenuType.Main);
			}

			ConnectionLogic.ConnectRanked(assignment,
				() => Game.OpenWindow("SERVER_LOBBY", new WidgetArgs
				{
					{ "onStart", () =>
						{
							RemoveShellmapUI();
							pendingRankedResultMatchId = assignment.MatchId;
							pendingRankedResultOpponent = assignment.Opponent;
							lastGameState = MenuPanel.Multiplayer;
						} },
					{ "onExit", (Action)OnLobbyExit },
					{ "skirmishMode", false }
				}),
				() => SwitchMenu(MenuType.Main));
		}

		void OpenReplayBrowserPanel()
		{
			SwitchMenu(MenuType.None);
			Ui.OpenWindow("REPLAYBROWSER_PANEL", new WidgetArgs
			{
				{ "onExit", () => SwitchMenu(MenuType.Main) },
				{ "onStart", () => { RemoveShellmapUI(); lastGameState = MenuPanel.Replays; } }
			});
		}

		void OpenGameSaveBrowserPanel()
		{
			SwitchMenu(MenuType.None);
			Ui.OpenWindow("GAMESAVE_BROWSER_PANEL", new WidgetArgs
			{
				{ "onExit", () => SwitchMenu(SingleplayerOrMain) },
				{ "onStart", () => { RemoveShellmapUI(); lastGameState = MenuPanel.GameSaves; } },
				{ "isSavePanel", false },
				{ "world", null }
			});
		}

		void OpenSettingsPanel()
		{
			SwitchMenu(MenuType.None);
			Game.OpenWindow("SETTINGS_PANEL", new WidgetArgs
			{
				{ "onExit", () => SwitchMenu(MenuType.Main) }
			});
		}

		void OpenSpecialThanksPanel()
		{
			var panel = Game.OpenWindow("SPECIAL_THANKS_PANEL", new WidgetArgs());
			panel.Get<ButtonWidget>("SUPPORT_BUTTON").OnClick = () =>
				OpenSpecialThanksLink(Game.OpenSupportPage, "https://nukehour.com/supporters#support");
			panel.Get<ButtonWidget>("MORE_BUTTON").OnClick = () =>
				OpenSpecialThanksLink(Game.OpenSupportersPage, "https://nukehour.com/supporters");
			panel.Get<ButtonWidget>("BACK_BUTTON").OnClick = () =>
			{
				Ui.CloseWindow();
				SwitchMenu(MenuType.Main);
			};

			SwitchMenu(MenuType.None);
		}

		static void OpenSpecialThanksLink(Action openOnIOS, string url)
		{
			if (Platform.UsesMobileLayout)
			{
				openOnIOS();
				return;
			}

			OpenOfficialPage(url);
		}

		static void OpenOfficialPage(string url)
		{
			try
			{
				using var browser = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("/usr/bin/open")
				{
					UseShellExecute = false,
					ArgumentList = { url }
				});
			}
			catch (Exception ex)
			{
				Log.Write("debug", $"Unable to open NUKE HOUR website: {ex.Message}");
			}
		}

		void OpenAboutPanel()
		{
			var aboutPanel = Game.OpenWindow("ABOUT_PANEL", new WidgetArgs());
			var aboutScreen = IosScreenMetrics.SnapshotFor(Game.Renderer.Resolution);
			new AboutPanelLayout(aboutScreen).Apply(aboutPanel);
			aboutPanel.Get<LogicTickerWidget>("ABOUT_LAYOUT_WATCHER").OnTick = () =>
			{
				var current = IosScreenMetrics.SnapshotFor(Game.Renderer.Resolution);
				if (aboutScreen.Equals(current))
					return;
				aboutScreen = current;
				new AboutPanelLayout(current).Apply(aboutPanel);
			};
			aboutPanel.Get<ButtonWidget>("WEBSITE_BUTTON").OnClick = () => OpenOfficialPage("https://nukehour.com");
			aboutPanel.Get<ButtonWidget>("BACK_BUTTON").OnClick = () =>
			{
				Ui.CloseWindow();
				SwitchMenu(MenuType.Main);
			};

			SwitchMenu(MenuType.None);
		}

		void LaunchIntoRequestedPanel()
		{
			var target = Game.Settings.Game.LaunchInto;
			if (string.IsNullOrEmpty(target))
				return;

			Game.Settings.Game.LaunchInto = "";

			// Local patch: support deep-link targets of the form "<command> <argument>"
			// so external launchers can jump straight into a specific replay or saved game.
			var separator = target.IndexOf(' ');
			var command = separator < 0 ? target.ToLowerInvariant() : target.Substring(0, separator).ToLowerInvariant();
			var argument = separator < 0 ? null : target.Substring(separator + 1);

			switch (command)
			{
				case "replay" when !string.IsNullOrEmpty(argument):
					Game.JoinReplay(argument);
					break;
				case "loadsave" when !string.IsNullOrEmpty(argument):
					LoadSavedGame(argument);
					break;
				case "skirmish" when !string.IsNullOrEmpty(argument):
					StartSkirmishGameDeepLink(argument);
					break;
				case "connect" when !string.IsNullOrEmpty(argument):
					ConnectToServerDeepLink(argument);
					break;
				case "skirmish":
					StartSkirmishGame();
					break;
				case "multiplayer":
					OpenMultiplayerPanel();
					break;
				case "ranked":
					SwitchMenu(MenuType.None);
					OpenRankedPanel();
					break;
				case "leaderboard":
					SwitchMenu(MenuType.None);
					OpenRankedLeaderboard();
					break;
				case "load":
					OpenGameSaveBrowserPanel();
					break;
				case "settings":
					OpenSettingsPanel();
					break;
				case "thanks":
					OpenSpecialThanksPanel();
					break;
				case "replays":
					OpenReplayBrowserPanel();
					break;
				case "missions":
					var hasMissions = modData.MapCache
						.Any(p => p.Status == MapStatus.Available && p.Visibility.HasFlag(MapVisibility.MissionSelector));
					if (hasMissions)
						OpenMissionBrowserPanel(modData.MapCache.PickLastModifiedMap(MapVisibility.MissionSelector));

					break;
				case "editor":
					SwitchMenu(MenuType.MapEditor);
					break;
				case "assetbrowser":
					SwitchMenu(MenuType.None);
					Game.OpenWindow("ASSETBROWSER_PANEL", new WidgetArgs
					{
						{ "onExit", () => SwitchMenu(MenuType.Main) }
					});
					break;
				case "credits":
					Game.OpenWindow("CREDITS_PANEL", new WidgetArgs
					{
						{ "onExit", () => SwitchMenu(MenuType.Main) }
					});
					break;
				case "music":
					SwitchMenu(MenuType.None);
					Game.OpenWindow("MUSIC_PANEL", new WidgetArgs
					{
						{ "onExit", () => SwitchMenu(MenuType.Main) },
						{ "world", world }
					});
					break;
				case "content":
					if (contentInstaller != null)
						Game.RunAfterTick(() => Game.InitializeMod(contentInstaller.ContentInstallerMod, new Arguments()));

					break;
			}
		}

		// Local patch: replicate GameSaveBrowserLogic's load flow so LaunchInto
		// can deep-link into a specific saved game ("loadsave <filename>").
		void LoadSavedGame(string saveFilename)
		{
			var savePath = Path.Combine(
				Platform.SupportDir,
				"Saves",
				modData.Manifest.Id,
				modData.Manifest.Metadata.Version,
				saveFilename);

			if (!File.Exists(savePath))
				return;

			GameSave save;
			try
			{
				save = new GameSave(savePath);
			}
			catch (Exception)
			{
				return;
			}

			var map = modData.MapCache[save.GlobalSettings.Map];
			if (map.Status != MapStatus.Available)
				return;

			var orders = new List<Order>()
			{
				Order.FromTargetString("LoadGameSave", saveFilename, true),
				Order.Command($"state {Session.ClientState.Ready}")
			};

			Game.CreateAndStartLocalServer(map.Uid, orders);
		}

		// Local patch: deep-link straight into a running skirmish, bypassing the lobby UI.
		// Argument format: "<mapUid> <lobby command 1>|<lobby command 2>|..."
		// Lobby commands follow LobbyCommands grammar (option/slot_bot/faction/team/spawn...).
		// A final "state Ready" is appended, which auto-starts local (singleplayer-enabled) games.
		void StartSkirmishGameDeepLink(string argument)
		{
			SwitchMenu(MenuType.None);

			var parts = argument.Split(new[] { ' ' }, 2);
			var mapUid = parts[0];

			// Resolve by UID first, then by package folder / title so external launchers
			// can reference maps without replicating the SHA1-based UID algorithm.
			// PackageName is a full path; match on the final path segment like Game.Initialize.
			var preview = modData.MapCache.FirstOrDefault(p =>
				p.Status == MapStatus.Available &&
				(p.Uid == mapUid ||
				 string.Equals(Path.GetFileName(p.PackageName), mapUid, StringComparison.OrdinalIgnoreCase) ||
				 string.Equals(Path.GetFileNameWithoutExtension(p.PackageName), mapUid, StringComparison.OrdinalIgnoreCase) ||
				 string.Equals(p.Title, mapUid, StringComparison.OrdinalIgnoreCase)));
			if (preview == null)
			{
				// Do not fall back to Settings.Server.Map — that silently starts the wrong
				// (often smaller) map and then lobby commands like slot_bot Multi2 crash.
				Log.Write("debug", $"Skirmish deep-link map not found: '{mapUid}'");
				StartSkirmishGame();
				return;
			}

			mapUid = preview.Uid;

			Game.Settings.Server.Map = mapUid;
			Game.Settings.Save();

			var setupOrders = new List<Order>();
			if (parts.Length > 1)
				foreach (var cmd in parts[1].Split('|'))
					if (!string.IsNullOrWhiteSpace(cmd))
						setupOrders.Add(Order.Command(cmd.Trim()));

			setupOrders.Add(Order.Command($"state {Session.ClientState.Ready}"));
			Game.CreateAndStartLocalServer(mapUid, setupOrders);
		}

		// Local patch: deep-link straight into a multiplayer server lobby, bypassing
		// the server browser UI. Argument format: "<host>" or "<host>:<port>".
		void ConnectToServerDeepLink(string endpoint)
		{
			SwitchMenu(MenuType.None);

			var host = endpoint;
			var port = 1234;
			var idx = endpoint.LastIndexOf(':');
			if (idx > 0 && int.TryParse(endpoint.Substring(idx + 1), out var parsed))
			{
				host = endpoint.Substring(0, idx);
				port = parsed;
			}

			void OnLobbyExit()
			{
				Game.Disconnect();
				SwitchMenu(MenuType.Main);
			}

			ConnectionLogic.Connect(new ConnectionTarget(host, port), "",
				() => Game.OpenWindow("SERVER_LOBBY", new WidgetArgs
				{
					{ "onStart", () => { RemoveShellmapUI(); lastGameState = MenuPanel.Multiplayer; } },
					{ "onExit", (Action)OnLobbyExit },
					{ "skirmishMode", false }
				}),
				() => SwitchMenu(MenuType.Main));
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				Game.OnRemoteDirectConnect -= OnRemoteDirectConnect;
				Game.BeforeGameStart -= RemoveShellmapUI;
			}

			Game.OnShellmapLoaded -= OpenMenuBasedOnLastGame;
			base.Dispose(disposing);
		}

		void OpenMenuBasedOnLastGame()
		{
			switch (lastGameState)
			{
				case MenuPanel.Missions:
					OpenMissionBrowserPanel(null);
					break;

				case MenuPanel.Replays:
					OpenReplayBrowserPanel();
					break;

				case MenuPanel.Skirmish:
					SwitchMenu(MenuType.Main);
					break;

				case MenuPanel.Multiplayer:
					if (pendingRankedResultMatchId != null)
					{
						var matchId = pendingRankedResultMatchId;
						var opponent = pendingRankedResultOpponent;
						pendingRankedResultMatchId = null;
						pendingRankedResultOpponent = null;
						OpenRankedResult(matchId, opponent);
					}
					else
						OpenMultiplayerPanel();
					break;

				case MenuPanel.MapEditor:
					SwitchMenu(MenuType.MapEditor);
					break;

				case MenuPanel.GameSaves:
					SwitchMenu(SingleplayerOrMain);
					break;
			}

			lastGameState = MenuPanel.None;
		}
	}
}
