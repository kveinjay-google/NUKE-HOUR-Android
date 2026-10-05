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
using System.Linq;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Traits;
using OpenRA.Network;
using OpenRA.Primitives;
using OpenRA.Traits;
using OpenRA.Widgets;

namespace OpenRA.Mods.Common.Widgets.Logic
{
	public class LobbyLogic : ChromeLogic, INotificationHandler<TextNotification>
	{
		[FluentReference]
		const string Add = "options-slot-admin.add-bots";

		[FluentReference]
		const string Remove = "options-slot-admin.remove-bots";

		[FluentReference]
		const string ConfigureBots = "options-slot-admin.configure-bots";

		[FluentReference("count")]
		const string NumberTeams = "options-slot-admin.teams-count";

		[FluentReference]
		const string HumanVsBots = "options-slot-admin.humans-vs-bots";

		[FluentReference]
		const string FreeForAll = "options-slot-admin.free-for-all";

		[FluentReference]
		const string ConfigureTeams = "options-slot-admin.configure-teams";

		[FluentReference]
		const string Back = "button-back";

		[FluentReference]
		const string TeamChat = "button-team-chat";

		[FluentReference]
		const string GeneralChat = "button-general-chat";

		[FluentReference("seconds")]
		const string ChatAvailability = "label-chat-availability";

		[FluentReference]
		const string ChatDisabled = "label-chat-disabled";

		static readonly Action DoNothing = () => { };

		readonly ModData modData;
		readonly Action onStart;
		readonly Action onExit;
		readonly OrderManager orderManager;
		readonly WorldRenderer worldRenderer;
		readonly bool skirmishMode;
		readonly Ruleset modRules;
		readonly WebServices services;

		internal enum PanelType { Players, Options, Music, Servers, Kick, ForceStart }
		PanelType panel = PanelType.Players;

		readonly Widget lobby;
		readonly Widget editablePlayerTemplate;
		readonly Widget nonEditablePlayerTemplate;
		readonly Widget emptySlotTemplate;

		readonly ScrollPanelWidget lobbyChatPanel;
		readonly Dictionary<TextNotificationPool, Widget> chatTemplates = new();
		readonly TextFieldWidget chatTextField;
		readonly CachedTransform<int, string> chatAvailableIn;
		readonly string chatDisabled;

		readonly ScrollPanelWidget players;

		readonly Dictionary<string, LobbyFaction> factions = new();

		readonly IColorPickerManagerInfo colorManager;

		readonly TabCompletionLogic tabCompletion = new();

		MapPreview map;
		Session.MapStatus mapStatus;

		bool chatEnabled;
		bool disableTeamChat;
		bool insufficientPlayerSpawns;
		bool teamChat;
		bool updateDiscordStatus = true;
		bool resetOptionsButtonEnabled;
		readonly LobbySafeStartRequestState safeStartState = new LobbySafeStartRequestState(0);
		int startStatusRevision;
		int cachedStartStatusRevision = -1;
		LobbyStartAvailability cachedStartAvailability;
		Dictionary<int, SpawnOccupant> spawnOccupants = new();

		readonly string chatLineSound;
		readonly string playerJoinedSound;
		readonly string playerLeftSound;
		readonly string lobbyOptionChangedSound;
		IosMenuLayoutPolicy iosLayoutPolicy;
		IosLobbyLayout iosLobbyLayout;
		Size iosLayoutResolution;
		Size iosNativePointSize;
		Rectangle iosSafeBounds;
		PanelType layoutPanel;
		string revealedLocalSlot;

		bool MapIsPlayable => (mapStatus & Session.MapStatus.Playable) == Session.MapStatus.Playable;
		internal static bool PanelShowsMap(PanelType activePanel) => activePanel == PanelType.Players;
		internal static bool PanelUsesFullWidth(PanelType activePanel) => activePanel is PanelType.Options or PanelType.Music;

		void InvalidateStartStatus()
		{
			startStatusRevision++;
		}

		void ClientMapReadinessChanged(int clientIndex)
		{
			safeStartState.ObserveReadinessChange(clientIndex);
			InvalidateStartStatus();
		}

		void SafeStartRejected(LobbyStartRejection rejection)
		{
			if (safeStartState.TryAcceptRejection(rejection, orderManager.LobbyInfo.GlobalSettings.Map))
				InvalidateStartStatus();
		}

		void StartStatusLobbyInfoChanged()
		{
			safeStartState.ObserveLobbySync(orderManager.LobbyInfo.GlobalSettings.Map);
			InvalidateStartStatus();
		}

		LobbyStartAvailability CurrentStartAvailability()
		{
			if (cachedStartStatusRevision == startStatusRevision)
				return cachedStartAvailability;

			cachedStartAvailability = safeStartState.Rejection != null ?
				safeStartState.Rejection.ToAvailability() :
				LobbyStartAvailability.Evaluate(new LobbyStartInputs
				{
					LobbyInfo = orderManager.LobbyInfo,
					Mode = LobbyStartMode.SafeDirect,
					RequesterIsAdmin = Game.IsHost,
					RequireCurrentMapReadiness = !skirmishMode,
					InsufficientEnabledSpawnPoints = insufficientPlayerSpawns
				}).ApplyLocalMapAvailability(map.Status == MapStatus.Available);
			cachedStartStatusRevision = startStatusRevision;
			return cachedStartAvailability;
		}

		void BeginSafeStart()
		{
			var availability = CurrentStartAvailability();
			if (!availability.CanStart || safeStartState.IsPending)
				return;

			var currentMapUid = orderManager.LobbyInfo.GlobalSettings.Map;
			var request = safeStartState.Begin(currentMapUid);
			InvalidateStartStatus();
			Game.RunAfterDelay(5000, () =>
			{
				if (!disposed && safeStartState.TryTimeout(request.RequestId))
					InvalidateStartStatus();
			});
			orderManager.IssueOrder(Order.Command(request.ToCommand()));
		}

		// Listen for connection failures
		void ConnectionStateChanged(OrderManager om, string password, NetworkConnection connection)
		{
			if (connection.ConnectionState == ConnectionState.NotConnected)
			{
				if (safeStartState.Reset())
					InvalidateStartStatus();

				// Show connection failed dialog
				Ui.CloseWindow();

				void OnConnect()
				{
					Game.OpenWindow("SERVER_LOBBY", new WidgetArgs()
					{
						{ "onExit", onExit },
						{ "onStart", onStart },
						{ "skirmishMode", false }
					});
				}

				Action<string> onRetry = pass => ConnectionLogic.Connect(connection.Target, pass, OnConnect, onExit);

				var switchPanel = CurrentServerSettings.ServerExternalMod != null ? "CONNECTION_SWITCHMOD_PANEL" : "CONNECTIONFAILED_PANEL";
				Ui.OpenWindow(switchPanel, new WidgetArgs()
				{
					{ "orderManager", om },
					{ "connection", connection },
					{ "password", password },
					{ "onAbort", onExit },
					{ "onQuit", null },
					{ "onRetry", onRetry }
				});
			}
		}

		[ObjectCreator.UseCtor]
		internal LobbyLogic(Widget widget, ModData modData, WorldRenderer worldRenderer, OrderManager orderManager,
			Action onExit, Action onStart, bool skirmishMode, Dictionary<string, MiniYaml> logicArgs)
		{
			map = MapCache.UnknownMap;
			lobby = widget;
			this.modData = modData;
			this.orderManager = orderManager;
			this.worldRenderer = worldRenderer;
			this.onStart = onStart;
			this.onExit = onExit;
			this.skirmishMode = skirmishMode;

			var commandCenterFamily = skirmishMode ? "cc-skirmish" : "cc-multiplayer";
			if (lobby is BackgroundWidget lobbyBackground)
				lobbyBackground.Background = $"{commandCenterFamily}-background";

			var commandCenterFrame = lobby.GetOrNull<BackgroundWidget>("COMMAND_CENTER_FRAME");
			if (commandCenterFrame != null)
				commandCenterFrame.Background = $"{commandCenterFamily}-frame";

			foreach (var buttonId in new[]
			{
				"SLOTS_DROPDOWNBUTTON", "RESET_OPTIONS_BUTTON", "CHANGEMAP_BUTTON",
				"START_GAME_BUTTON", "DISCONNECT_BUTTON"
			})
			{
				var button = lobby.GetOrNull<ButtonWidget>(buttonId);
				if (button != null)
					button.Background = $"{commandCenterFamily}-control";
			}

			// TODO: This needs to be reworked to support per-map tech levels, bots, etc.
			modRules = modData.DefaultRules;

			services = modData.Manifest.Get<WebServices>();

			Game.LobbyInfoChanged += UpdateCurrentMap;
			Game.LobbyInfoChanged += UpdatePlayerList;
			Game.LobbyInfoChanged += UpdateDiscordStatus;
			Game.LobbyInfoChanged += UpdateSpawnOccupants;
			Game.LobbyInfoChanged += UpdateOptions;
			Game.LobbyInfoChanged += StartStatusLobbyInfoChanged;
			Game.BeforeGameStart += OnGameStart;
			Game.ConnectionStateChanged += ConnectionStateChanged;
			orderManager.ClientMapReadinessChanged += ClientMapReadinessChanged;
			orderManager.SafeStartRejected += SafeStartRejected;

			ChromeMetrics.TryGet("ChatLineSound", out chatLineSound);
			ChromeMetrics.TryGet("PlayerJoinedSound", out playerJoinedSound);
			ChromeMetrics.TryGet("PlayerLeftSound", out playerLeftSound);
			ChromeMetrics.TryGet("LobbyOptionChangedSound", out lobbyOptionChangedSound);

			var name = lobby.GetOrNull<LabelWidget>("SERVER_NAME");
			if (name != null)
				name.GetText = () => orderManager.LobbyInfo.GlobalSettings.ServerName;

			var mapContainer = Ui.LoadWidget("MAP_PREVIEW", lobby.Get("MAP_PREVIEW_ROOT"), new WidgetArgs
			{
				{ "orderManager", orderManager },
				{ "getMap", (Func<(MapPreview, Session.MapStatus)>)(() => (map, mapStatus)) },
				{
					"onMouseDown", (Action<MapPreviewWidget, MapPreview, MouseInput>)((preview, mapPreview, mi) =>
						LobbyUtils.SelectSpawnPoint(orderManager, preview, mapPreview, mi))
				},
				{ "getSpawnOccupants", (Func<Dictionary<int, SpawnOccupant>>)(() => spawnOccupants) },
				{ "getDisabledSpawnPoints", (Func<HashSet<int>>)(() => orderManager.LobbyInfo.DisabledSpawnPoints) },
				{ "showUnoccupiedSpawnpoints", true },
				{ "mapUpdatesEnabled", true },
				{
					"onMapUpdate", (Action<string>)(uid =>
					{
						orderManager.RoomMapTransfer.Select(uid);
						Game.Settings.Server.Map = uid;
						Game.Settings.Save();
					})
				},
			});

			mapContainer.IsVisible = () => lobby.GetOrNull("LOBBY_CONTENT") != null ?
				PanelShowsMap(panel) : panel != PanelType.Servers;

			UpdateCurrentMap();

			var playerBin = Ui.LoadWidget("LOBBY_PLAYER_BIN", lobby.Get("TOP_PANELS_ROOT"), new WidgetArgs());
			playerBin.IsVisible = () => panel == PanelType.Players;

			players = playerBin.Get<ScrollPanelWidget>("LOBBY_PLAYERS");
			editablePlayerTemplate = players.Get("TEMPLATE_EDITABLE_PLAYER");
			nonEditablePlayerTemplate = players.Get("TEMPLATE_NONEDITABLE_PLAYER");
			emptySlotTemplate = players.Get("TEMPLATE_EMPTY");
			colorManager = modRules.Actors[SystemActors.World].TraitInfo<IColorPickerManagerInfo>();

			foreach (var f in modRules.Actors[SystemActors.World].TraitInfos<FactionInfo>())
				factions.Add(f.InternalName, new LobbyFaction { Selectable = f.Selectable, Name = f.Name, Side = f.Side, Description = f.Description });

			var gameStarting = false;
			Func<bool> configurationDisabled = () => !Game.IsHost || gameStarting ||
				panel == PanelType.Kick || panel == PanelType.ForceStart || !MapIsPlayable ||
				orderManager.LocalClient == null || orderManager.LocalClient.IsReady;

			var mapButton = lobby.GetOrNull<ButtonWidget>("CHANGEMAP_BUTTON");
			if (mapButton != null)
			{
				mapButton.IsVisible = () => lobby.GetOrNull("LOBBY_CONTENT") != null ?
					PanelShowsMap(panel) : panel != PanelType.Servers;
				mapButton.IsDisabled = () => gameStarting || panel == PanelType.Kick || panel == PanelType.ForceStart ||
					orderManager.LocalClient == null || orderManager.LocalClient.IsReady;
				mapButton.OnClick = () =>
				{
					var onSelect = new Action<string>(uid =>
					{
						// Don't select the same map again, and handle map becoming unavailable
						var status = modData.MapCache[uid].Status;
						if (uid == map.Uid || (status != MapStatus.Available && status != MapStatus.DownloadAvailable))
							return;

						orderManager.IssueOrder(Order.Command("map " + uid));
						Game.Settings.Server.Map = uid;
						Game.Settings.Save();
					});

					// Check for updated maps, if the user has edited a map we'll preselect it for them
					modData.MapCache.UpdateMaps();

					Ui.OpenWindow("MAPCHOOSER_PANEL", new WidgetArgs()
					{
						{ "initialMap", modData.MapCache.PickLastModifiedMap(MapVisibility.Lobby) ?? map.Uid },
						{ "remoteMapPool", orderManager.ServerMapPool },
						{ "initialTab", MapClassification.System },
						{ "onExit", modData.MapCache.UpdateMaps },
						{ "onSelect", Game.IsHost ? onSelect : null },
						{ "filter", MapVisibility.Lobby },
					});
				};
			}

			var slotsButton = lobby.GetOrNull<DropDownButtonWidget>("SLOTS_DROPDOWNBUTTON");
			if (slotsButton != null)
			{
			if (Game.ModData.Manifest.Id == "ra2")
			{
				slotsButton.PreferPopupAbove = true;
				slotsButton.GetPopupSafeBounds = () => IosScreenMetrics.SnapshotFor(Game.Renderer.Resolution).SafeBounds;
				slotsButton.PreparePanel = popup => LobbyPositionPickerLayout.Prepare((ScrollPanelWidget)popup,
					IosScreenMetrics.SnapshotFor(Game.Renderer.Resolution), Platform.UsesMobileLayout);
			}

				slotsButton.IsVisible = () => panel == PanelType.Players;
				slotsButton.IsDisabled = () => configurationDisabled() || panel != PanelType.Players ||
					(orderManager.LobbyInfo.Slots.Values.All(s => !s.AllowBots) &&
					!orderManager.LobbyInfo.Slots.Any(s => !s.Value.LockTeam && orderManager.LobbyInfo.ClientInSlot(s.Key) != null));

				slotsButton.OnMouseDown = _ =>
				{
					var botTypes = map.PlayerActorInfo.TraitInfos<IBotInfo>().Select(t => t.Type);
					var options = new Dictionary<string, IEnumerable<DropDownOption>>();

					var botController = orderManager.LobbyInfo.Clients.FirstOrDefault(c => c.IsAdmin);
					if (orderManager.LobbyInfo.Slots.Values.Any(s => s.AllowBots))
					{
						var botOptions = new List<DropDownOption>()
						{
							new()
							{
								Title = FluentProvider.GetMessage(Add),
								IsSelected = () => false,
								OnClick = () =>
								{
									foreach (var slot in orderManager.LobbyInfo.Slots)
									{
										var bot = botTypes.Random(Game.CosmeticRandom);
										var c = orderManager.LobbyInfo.ClientInSlot(slot.Key);
										if (slot.Value.AllowBots && (c == null || c.Bot != null))
											orderManager.IssueOrder(Order.Command($"slot_bot {slot.Key} {botController.Index} {bot}"));
									}
								}
							}
						};

						if (orderManager.LobbyInfo.Clients.Any(c => c.Bot != null))
						{
							botOptions.Add(new DropDownOption()
							{
								Title = FluentProvider.GetMessage(Remove),
								IsSelected = () => false,
								OnClick = () =>
								{
									foreach (var slot in orderManager.LobbyInfo.Slots)
									{
										var c = orderManager.LobbyInfo.ClientInSlot(slot.Key);
										if (c != null && c.Bot != null)
											orderManager.IssueOrder(Order.Command("slot_open " + slot.Value.PlayerReference));
									}
								}
							});
						}

						options.Add(FluentProvider.GetMessage(ConfigureBots), botOptions);
					}

					var teamCount = (orderManager.LobbyInfo.Slots.Count(s => !s.Value.LockTeam && orderManager.LobbyInfo.ClientInSlot(s.Key) != null) + 1) / 2;
					if (teamCount >= 1)
					{
						var teamOptions = Enumerable.Range(2, teamCount - 1).Reverse().Select(d => new DropDownOption
						{
							Title = FluentProvider.GetMessage(NumberTeams, "count", d),
							IsSelected = () => false,
							OnClick = () => orderManager.IssueOrder(Order.Command($"assignteams {d}"))
						}).ToList();

						if (orderManager.LobbyInfo.Slots.Any(s => s.Value.AllowBots))
						{
							teamOptions.Add(new DropDownOption
							{
								Title = FluentProvider.GetMessage(HumanVsBots),
								IsSelected = () => false,
								OnClick = () => orderManager.IssueOrder(Order.Command("assignteams 1"))
							});
						}

						teamOptions.Add(new DropDownOption
						{
							Title = FluentProvider.GetMessage(FreeForAll),
							IsSelected = () => false,
							OnClick = () => orderManager.IssueOrder(Order.Command("assignteams 0"))
						});

						options.Add(FluentProvider.GetMessage(ConfigureTeams), teamOptions);
					}

					ScrollItemWidget SetupItem(DropDownOption option, ScrollItemWidget template)
					{
						var item = ScrollItemWidget.Setup(template, option.IsSelected, option.OnClick);
						item.Get<LabelWidget>("LABEL").GetText = () => option.Title;
						return item;
					}

					slotsButton.ShowDropDown("LABEL_DROPDOWN_TEMPLATE", 175, options, SetupItem);
				};
			}

			var resetOptionsButton = lobby.GetOrNull<ButtonWidget>("RESET_OPTIONS_BUTTON");
			if (resetOptionsButton != null)
			{
				resetOptionsButton.IsVisible = () => panel == PanelType.Options;
				resetOptionsButton.IsDisabled = () => configurationDisabled() || !resetOptionsButtonEnabled;
				resetOptionsButton.OnMouseDown = _ => orderManager.IssueOrder(Order.Command("reset_options"));
			}

			var optionsBin = Ui.LoadWidget("LOBBY_OPTIONS_BIN", lobby.Get("TOP_PANELS_ROOT"), new WidgetArgs()
			{
				{ "orderManager", orderManager },
				{ "getMap", (Func<MapPreview>)(() => map) },
				{ "configurationDisabled", configurationDisabled }
			});
			if (skirmishMode && resetOptionsButton != null)
				optionsBin.Children.OfType<ScrollPanelWidget>().FirstOrDefault()?.LogicObjects
					.OfType<LobbyOptionsLogic>().FirstOrDefault()?.AttachInlineReset(resetOptionsButton);

			optionsBin.IsVisible = () => panel == PanelType.Options;
			var optionsTitle = optionsBin.GetOrNull<LabelWidget>("TITLE");
			if (optionsTitle != null)
				optionsTitle.IsVisible = () => !skirmishMode;

			var musicBin = Ui.LoadWidget("LOBBY_MUSIC_BIN", lobby.Get("TOP_PANELS_ROOT"), new WidgetArgs
			{
				{ "onExit", DoNothing },
				{ "world", worldRenderer.World }
			});
			musicBin.IsVisible = () => panel == PanelType.Music;

			ServerListLogic serverListLogic = null;
			if (!skirmishMode && lobby.GetOrNull("SERVERS_TAB") != null)
			{
				Action<GameServer> doNothingWithServer = _ => { };

				var serversBin = Ui.LoadWidget("LOBBY_SERVERS_BIN", lobby.Get("TOP_PANELS_ROOT"), new WidgetArgs
				{
					{ "onJoin", doNothingWithServer },
				});

				serverListLogic = serversBin.LogicObjects.Select(l => l as ServerListLogic).FirstOrDefault(l => l != null);
				serversBin.IsVisible = () => panel == PanelType.Servers;
			}

			var tabContainer = skirmishMode ? lobby.Get("SKIRMISH_TABS") : lobby.Get("MULTIPLAYER_TABS");
			tabContainer.IsVisible = () => true;

			var optionsTab = tabContainer.Get<ButtonWidget>("OPTIONS_TAB");
			optionsTab.IsHighlighted = () => panel == PanelType.Options;
			optionsTab.IsDisabled = OptionsTabDisabled;
			optionsTab.OnClick = () => panel = PanelType.Options;

			var playersTab = tabContainer.Get<ButtonWidget>("PLAYERS_TAB");
			playersTab.IsHighlighted = () => panel == PanelType.Players;
			playersTab.IsDisabled = () => panel == PanelType.Kick || panel == PanelType.ForceStart;
			playersTab.OnClick = () => panel = PanelType.Players;

			var musicTab = tabContainer.Get<ButtonWidget>("MUSIC_TAB");
			musicTab.IsHighlighted = () => panel == PanelType.Music;
			musicTab.IsDisabled = () => panel == PanelType.Kick || panel == PanelType.ForceStart;
			musicTab.OnClick = () => panel = PanelType.Music;

			var serversTab = tabContainer.GetOrNull<ButtonWidget>("SERVERS_TAB");
			if (serversTab != null)
			{
				serversTab.IsHighlighted = () => panel == PanelType.Servers;
				serversTab.IsDisabled = () => panel == PanelType.Kick || panel == PanelType.ForceStart;
				serversTab.OnClick = () =>
				{
					// Refresh the list when switching to the servers tab
					if (serverListLogic != null && panel != PanelType.Servers)
						serverListLogic.RefreshServerList();

					panel = PanelType.Servers;
				};
			}

			// Force start panel
			void StartGame()
			{
				// Refresh MapCache and check if the selected map is available before attempting to start the game
				if (modData.MapCache[map.Uid].Status == MapStatus.Available)
				{
					gameStarting = true;
					orderManager.IssueOrder(Order.Command("startgame"));
				}
				else
					modData.MapCache.UpdateMaps();
			}

			bool StartDisabled() => map.Status != MapStatus.Available ||
				orderManager.LobbyInfo.Slots.Any(sl => sl.Value.Required && orderManager.LobbyInfo.ClientInSlot(sl.Key) == null) ||
				orderManager.LobbyInfo.Slots.All(sl => orderManager.LobbyInfo.ClientInSlot(sl.Key) == null) ||
				(!orderManager.LobbyInfo.GlobalSettings.EnableSingleplayer && orderManager.LobbyInfo.NonBotPlayers.Count() < 2) ||
				insufficientPlayerSpawns;

			var startGameButton = lobby.GetOrNull<ButtonWidget>("START_GAME_BUTTON");
			if (startGameButton != null)
			{
				bool IosConfigurationDisabled() =>
					!Game.IsHost || gameStarting || safeStartState.IsPending ||
					panel == PanelType.ForceStart || panel == PanelType.Kick ||
					orderManager.LocalClient == null;

				startGameButton.IsDisabled = () => Platform.UsesMobileLayout
					? IosConfigurationDisabled() || !CurrentStartAvailability().CanStart
					: configurationDisabled() || StartDisabled();

				var originalStartText = startGameButton.GetText;
				startGameButton.GetText = () =>
				{
					if (!Platform.UsesMobileLayout || !Game.IsHost)
						return originalStartText();

					if (safeStartState.IsPending)
						return FluentProvider.GetMessage("button-ios-lobby-start-waiting");

					var availability = CurrentStartAvailability();
					if (availability.CanStart)
						return originalStartText();

					var width = Math.Max(0,
						startGameButton.Bounds.Width - startGameButton.LeftMargin - startGameButton.RightMargin);
					var font = Game.Renderer.Fonts[startGameButton.Font];
					return IosLobbyStartStatus.Format(availability, width, font,
						index => orderManager.LobbyInfo.ClientWithIndex(index)?.Name ?? string.Empty,
						(key, args) => FluentProvider.GetMessage(key, args));
				};

				startGameButton.OnClick = () =>
				{
					if (Platform.UsesMobileLayout)
					{
						BeginSafeStart();
						return;
					}

					// Bots and admins don't count
					if (orderManager.LobbyInfo.Clients.Any(c => c.Slot != null && !c.IsAdmin && c.Bot == null && !c.IsReady))
						panel = PanelType.ForceStart;
					else
						StartGame();
				};
			}

			var forceStartBin = Ui.LoadWidget("FORCE_START_DIALOG", lobby.Get("TOP_PANELS_ROOT"), new WidgetArgs());
			forceStartBin.IsVisible = () => panel == PanelType.ForceStart;
			forceStartBin.Get("KICK_WARNING").IsVisible = () => orderManager.LobbyInfo.Clients.Any(c => c.IsInvalid);
			var forceStartButton = forceStartBin.Get<ButtonWidget>("OK_BUTTON");
			forceStartButton.OnClick = StartGame;
			forceStartButton.IsDisabled = StartDisabled;

			forceStartBin.Get<ButtonWidget>("CANCEL_BUTTON").OnClick = () => panel = PanelType.Players;

			var disconnectButton = lobby.Get<ButtonWidget>("DISCONNECT_BUTTON");
			disconnectButton.OnClick = () =>
			{
				Ui.CloseWindow();
				onExit();
				Game.Sound.PlayNotification(modRules, null, "Sounds", playerLeftSound, null);
			};

			if (skirmishMode)
			{
				var disconnectButtonText = FluentProvider.GetMessage(Back);
				disconnectButton.GetText = () => disconnectButtonText;
			}

			if (logicArgs.TryGetValue("ChatTemplates", out var templateIds))
			{
				foreach (var item in templateIds.Nodes)
				{
					var key = FieldLoader.GetValue<TextNotificationPool>("key", item.Key);
					chatTemplates[key] = Ui.LoadWidget(item.Value.Value, null, new WidgetArgs());
				}
			}

			var chatMode = lobby.Get<ButtonWidget>("CHAT_MODE");
			var team = FluentProvider.GetMessage(TeamChat);
			var all = FluentProvider.GetMessage(GeneralChat);
			chatMode.GetText = () => teamChat ? team : all;
			chatMode.OnClick = () => teamChat ^= true;
			chatMode.IsDisabled = () => disableTeamChat || !chatEnabled;

			chatTextField = lobby.Get<TextFieldWidget>("CHAT_TEXTFIELD");
			chatTextField.IsDisabled = () => !chatEnabled;
			chatTextField.MaxLength = UnitOrders.ChatMessageMaxLength;

			chatTextField.OnEnterKey = _ =>
			{
				if (chatTextField.Text.Length == 0)
					return true;

				// Always scroll to bottom when we've typed something
				lobbyChatPanel.ScrollToBottom();

				var teamNumber = 0U;
				if (teamChat && orderManager.LocalClient != null)
					teamNumber = orderManager.LocalClient.IsObserver ? uint.MaxValue : (uint)orderManager.LocalClient.Team;

				orderManager.IssueOrder(Order.Chat(chatTextField.Text, teamNumber));
				chatTextField.Text = "";
				return true;
			};

			chatTextField.OnTabKey = e =>
			{
				if (!chatMode.Key.IsActivatedBy(e) || chatMode.IsDisabled())
				{
					chatTextField.Text = tabCompletion.Complete(chatTextField.Text);
					chatTextField.CursorPosition = chatTextField.Text.Length;
				}
				else
					chatMode.OnKeyPress(e);

				return true;
			};

			chatTextField.OnEscKey = _ => chatTextField.YieldKeyboardFocus();

			chatAvailableIn = new CachedTransform<int, string>(x => FluentProvider.GetMessage(ChatAvailability, "seconds", x));
			chatDisabled = FluentProvider.GetMessage(ChatDisabled);

			lobbyChatPanel = lobby.Get<ScrollPanelWidget>("CHAT_DISPLAY");
			lobbyChatPanel.RemoveChildren();

			var settingsButton = lobby.GetOrNull<ButtonWidget>("SETTINGS_BUTTON");
			if (settingsButton != null)
			{
				settingsButton.OnClick = () => Ui.OpenWindow("SETTINGS_PANEL", new WidgetArgs
				{
					{ "onExit", DoNothing },
					{ "worldRenderer", worldRenderer }
				});
			}

			if (logicArgs.TryGetValue("ChatLineSound", out var yaml))
				chatLineSound = yaml.Value;
			if (logicArgs.TryGetValue("PlayerJoinedSound", out yaml))
				playerJoinedSound = yaml.Value;
			if (logicArgs.TryGetValue("PlayerLeftSound", out yaml))
				playerLeftSound = yaml.Value;
			if (logicArgs.TryGetValue("LobbyOptionChangedSound", out yaml))
				lobbyOptionChangedSound = yaml.Value;

			ApplyIosLobbyLayout();
		}

		void ApplyIosLobbyLayout()
		{
			var content = lobby.GetOrNull("LOBBY_CONTENT");
			if (!Platform.UsesMobileLayout && content == null)
				return;

			var resolution = Game.Renderer.Resolution;
			var snapshot = IosScreenMetrics.SnapshotFor(resolution);
			var policy = IosMenuLayoutPolicy.Create(true, snapshot);
			var bounds = content == null ? policy.ContentBounds :
				MultiplayerScreenLayout.ContentBounds(snapshot, compactPhone: Platform.UsesMobileLayout);
			lobby.Bounds = content == null ? bounds : policy.ViewportBounds;
			if (content != null)
				content.Bounds = bounds;
			var skirmishOptions = skirmishMode && panel == PanelType.Options;
			var showHeader = Platform.UsesMobileLayout ? !policy.IsPhone : !skirmishOptions;
			IosTouchMenuLogic.ApplyIosFonts(lobby);
			if (content != null)
				ApplySovietLobbyStyle(lobby, Platform.UsesMobileLayout);
			var columnWidths = skirmishMode && !policy.IsPhone ? null : MeasurePlayerColumns(policy);
			var layout = IosLobbyLayout.Create(bounds.Width, bounds.Height, policy, skirmishMode,
				content != null && PanelUsesFullWidth(panel), content == null || PanelShowsMap(panel),
				!skirmishOptions, showHeader, columnWidths, Platform.UsesMobileLayout,
				hidePlayerUtility: Platform.UsesMobileLayout && policy.IsPhone && skirmishMode && panel == PanelType.Players);
			iosLayoutPolicy = policy;
			iosLobbyLayout = layout;
			iosLayoutResolution = resolution;
			iosNativePointSize = snapshot.NativePointSize;
			iosSafeBounds = snapshot.SafeBounds;
			layoutPanel = panel;

			var title = lobby.GetOrNull("SERVER_NAME");
			if (title != null)
			{
				title.Bounds = layout.Header;
				title.IsVisible = () => showHeader;
			}

			var mapRoot = lobby.GetOrNull("MAP_PREVIEW_ROOT");
			if (mapRoot != null)
				mapRoot.Bounds = layout.Map;

			var topRoot = lobby.GetOrNull("TOP_PANELS_ROOT");
			if (topRoot != null)
			{
				topRoot.Bounds = layout.Main;
				foreach (var child in topRoot.Children)
				{
					var surface = child.Id == "LOBBY_SERVERS_BIN" ? layout.Servers :
						child.Id is "LOBBY_MUSIC_BIN" or "FORCE_START_DIALOG" or "KICK_CLIENT_DIALOG" or "KICK_SPECTATORS_DIALOG" ?
							new WidgetBounds(layout.Players.X, layout.Main.Y, layout.Players.Width, layout.Main.Height) : layout.Players;
					child.Bounds = new WidgetBounds(
						surface.X - layout.Main.X, surface.Y - layout.Main.Y,
						surface.Width, surface.Height);
				}
			}

			LayoutTabRow(lobby.GetOrNull("SKIRMISH_TABS"), layout.Tabs, 3);
			var multiplayerTabs = lobby.GetOrNull("MULTIPLAYER_TABS");
			LayoutTabRow(multiplayerTabs, layout.Tabs,
				multiplayerTabs?.Children.OfType<ButtonWidget>().Count() ?? 0);

			var chatRoot = lobby.GetOrNull("LOBBYCHAT");
			if (chatRoot != null)
			{
				chatRoot.Bounds = layout.Chat;
				chatRoot.IsVisible = () => !layout.HidePlayerUtility && (panel == PanelType.Players ||
					(!skirmishMode && panel == PanelType.Options));
				LayoutIosChat(chatRoot, layout, policy);
			}

			PlaceFooterButton("START_GAME_BUTTON", layout.Start.X, layout.Start.Y,
				layout.Start.Width, layout.Start.Height);
			PlaceFooterButton("DISCONNECT_BUTTON", layout.Disconnect.X, layout.Disconnect.Y,
				layout.Disconnect.Width, layout.Disconnect.Height);
			PlaceFooterButton("CHANGEMAP_BUTTON", layout.ChangeMap.X, layout.ChangeMap.Y,
				layout.ChangeMap.Width, layout.ChangeMap.Height);
			PlaceFooterButton("SLOTS_DROPDOWNBUTTON", layout.Position.X, layout.Position.Y,
				layout.Position.Width, layout.Position.Height);
			var slotsButton = lobby.GetOrNull("SLOTS_DROPDOWNBUTTON");
			if (slotsButton != null)
				slotsButton.IsVisible = () => panel == PanelType.Players && !layout.HidePlayerUtility;
			if (!skirmishMode)
				PlaceFooterButton("RESET_OPTIONS_BUTTON", layout.Position.X, layout.Position.Y,
					layout.Position.Width, layout.Position.Height);

			LayoutIosPlayerRows(layout, policy, skirmishMode);
			IosMapPreviewLayout.Apply(mapRoot, policy);
			if (content != null)
			{
				LayoutLobbyMusic(lobby.GetOrNull("LOBBY_MUSIC_BIN"), policy);
				foreach (var id in new[] { "FORCE_START_DIALOG", "KICK_CLIENT_DIALOG", "KICK_SPECTATORS_DIALOG" })
				{
					var dialog = lobby.GetOrNull(id);
					EnsureConfirmationScrollBody(dialog);
					LayoutLobbyConfirmation(dialog, policy);
				}
			}
		}

		int[] MeasurePlayerColumns(IosMenuLayoutPolicy policy)
		{
			var widths = new[] { 2 * policy.MinimumTarget, policy.MinimumTarget,
				policy.MinimumTarget, policy.MinimumTarget, policy.MinimumTarget, policy.MinimumTarget };
			var ids = new[]
			{
				new[] { "LABEL_LOBBY_NAME", "NAME", "SLOT_OPTIONS" },
				new[] { "LABEL_LOBBY_COLOR", "COLOR" },
				new[] { "LABEL_LOBBY_FACTION", "FACTION" },
				new[] { "LABEL_LOBBY_TEAM", "TEAM", "TEAM_DROPDOWN" },
				new[] { "LABEL_LOBBY_SPAWN", "SPAWN", "SPAWN_DROPDOWN" },
				new[] { "LABEL_LOBBY_STATUS", "IOS_MAP_STATUS_TEXT" }
			};
			void Measure(Widget widget)
			{
				for (var i = 0; i < ids.Length; i++)
				{
					if (!ids[i].Contains(widget.Id))
						continue;
					// Do not measure already-truncated display callbacks, or cache their
					// text before the new row bounds have been applied.
					var rawPhoneContent = policy.IsPhone && (widget.Id == "NAME" ||
						widget.Id == "SLOT_OPTIONS" || widget.Id == "FACTION");
					var text = rawPhoneContent ? "" : widget is LabelWidget label ? label.GetText() :
						widget is ButtonWidget button ? button.GetText() :
						widget is TextFieldWidget field ? field.Text : "";
					var font = widget is LabelWidget l ? l.Font : widget is ButtonWidget b ? b.Font :
						widget is TextFieldWidget f ? f.Font : (Platform.UsesMobileLayout ? "IosRegular" : "SettingsRegular");
					var decoration = widget is DropDownButtonWidget ? Math.Max(16, policy.MinimumTarget / 2) : 0;
					if (policy.IsPhone && widget.Id == "COLOR")
						decoration = 0;
					if (widget.Id == "FACTION")
					{
						decoration += (int)Math.Ceiling(42 * policy.LogicalPerPoint);
						font = widget.GetOrNull<LabelWidget>("FACTIONNAME")?.Font ?? font;
					}
					var measured = Game.Renderer.Fonts[font].Measure(text ?? "").X;
					if (rawPhoneContent)
					{
						var values = widget.Id == "FACTION"
							? orderManager.LobbyInfo.Clients.Where(c => factions.ContainsKey(c.Faction))
								.Select(c => FluentProvider.GetMessage(factions[c.Faction].Name))
							: orderManager.LobbyInfo.Clients.Select(c => c.IsBot ? LobbyUtils.ResolveBotDisplayName(map, c.Name) : c.Name);
						measured = values.Select(value => Game.Renderer.Fonts[font].Measure(value ?? "").X).DefaultIfEmpty(0).Max();
					}
					if (widget.Id == "FACTION" && !policy.IsPhone)
						measured = Math.Max(measured, factions.Values.Where(f => f.Selectable)
							.Select(f => Game.Renderer.Fonts[font].Measure(FluentProvider.GetMessage(f.Name)).X)
							.DefaultIfEmpty(0).Max());
					if (i == 4)
						measured = Math.Max(measured, Game.Renderer.Fonts[font].Measure(
							FluentProvider.GetMessage("button-color-chooser-random")).X);
					widths[i] = Math.Max(widths[i], measured + decoration + 2 * policy.Gap);
				}
				foreach (var child in widget.Children)
					Measure(child);
			}
			Measure(players.Parent);
			if (!policy.IsPhone)
				widths[0] = Math.Min(widths[0], 4 * policy.MinimumTarget);
			return widths;
		}

		static void LayoutTabRow(Widget container, WidgetBounds bounds, int count)
		{
			if (container == null)
				return;

			container.Bounds = bounds;
			var buttons = container.Children.OfType<ButtonWidget>().ToArray();
			var cellWidth = bounds.Width / count;
			for (var i = 0; i < buttons.Length; i++)
			{
				var right = i == buttons.Length - 1 ? bounds.Width : (i + 1) * cellWidth;
				buttons[i].Bounds = new WidgetBounds(i * cellWidth, 0, right - i * cellWidth, bounds.Height);
			}
		}

		void PlaceFooterButton(string id, int x, int y, int width, int height)
		{
			var button = lobby.GetOrNull<ButtonWidget>(id);
			if (button != null)
				button.Bounds = new WidgetBounds(x, y, width, height);
		}

		void LayoutIosPlayerRows(IosLobbyLayout layout, IosMenuLayoutPolicy policy, bool isSkirmish)
		{
			var playerBin = players.Parent;
			var labelContainer = playerBin.GetOrNull("LABEL_CONTAINER");
			var headerHeight = layout.PlayerHeader.Height;
			if (labelContainer != null)
			{
				labelContainer.Bounds = new WidgetBounds(0, 0, layout.PlayerRow.Width, headerHeight);
				LayoutPlayerHeader(labelContainer, layout, isSkirmish);
			}

			ConfigurePlayerScrollPanel(players, layout, policy);

			foreach (var row in new[]
			{
				editablePlayerTemplate, nonEditablePlayerTemplate, emptySlotTemplate
			})
				LayoutIosPlayerRow(row, layout, policy, isSkirmish);

			foreach (var row in players.Children)
				LayoutIosPlayerRow(row, layout, policy, isSkirmish);

			players.Layout.AdjustChildren();
		}

		internal static void ConfigurePlayerScrollPanel(ScrollPanelWidget players, IosLobbyLayout layout, IosMenuLayoutPolicy policy)
		{
			// Put the header gap outside the panel, so the arrow and first row
			// start on the same baseline and share the same target height.
			var headerGap = layout.HidePlayerUtility ? 0 : policy.Gap;
			players.Bounds = new WidgetBounds(layout.PlayerList.X, layout.PlayerList.Y + headerGap,
				layout.Players.Width, ScrollPanelWidget.WholeRowHeight(
					Math.Max(0, layout.PlayerList.Height - headerGap), layout.PlayerRow.Height, layout.PlayerRowSpacing));
			players.WholeRowScrollStep = layout.PlayerRow.Height + layout.PlayerRowSpacing;
			players.BorderWidth = 0;
			players.EnableContentDragging = true;
			players.ContentDragThreshold = policy.ContentDragThreshold;
			players.ItemSpacing = layout.PlayerRowSpacing;
			players.TopBottomSpacing = 0;
			players.ScrollBar = layout.PlayerScrollbarWidth > 0 ? ScrollBar.Right : ScrollBar.Hidden;
			players.ScrollbarWidth = layout.PlayerScrollbarWidth;
			players.MinimumThumbSize = policy.MinimumTarget;
		}

		static void LayoutPlayerHeader(Widget header, IosLobbyLayout layout, bool isSkirmish)
		{
			SetDirectBounds(header, "LABEL_LOBBY_NAME", layout.PlayerName);
			SetDirectBounds(header, "LABEL_LOBBY_COLOR", layout.PlayerColor);
			SetDirectBounds(header, "LABEL_LOBBY_FACTION", layout.PlayerFaction);
			SetDirectBounds(header, "LABEL_LOBBY_TEAM", layout.PlayerTeam);
			SetDirectBounds(header, "LABEL_LOBBY_HANDICAP", layout.PlayerHandicap);
			SetDirectBounds(header, "LABEL_LOBBY_SPAWN", layout.PlayerSpawn);
			SetDirectBounds(header, "LABEL_LOBBY_STATUS", layout.PlayerReady);
			var handicap = DirectChild(header, "LABEL_LOBBY_HANDICAP");
			if (handicap != null)
				handicap.IsVisible = () => false;
			var status = DirectChild(header, "LABEL_LOBBY_STATUS");
			if (status != null)
				status.IsVisible = () => !isSkirmish;

			foreach (var label in header.Children.OfType<LabelWidget>().ToArray())
			{
				label.Bounds.Y = 0;
				label.Bounds.Height = header.Bounds.Height;
				label.WordWrap = false;
				label.VAlign = TextVAlign.Middle;
				if (Game.ModData?.Manifest.Id != "ra2")
					continue;
				label.Align = TextAlign.Center;
				label.TextColor = Color.FromArgb(242, 228, 205);
				var plateId = "COLUMN_PLATE_" + label.Id;
				var plate = header.GetOrNull<BackgroundWidget>(plateId);
				if (plate == null)
				{
					plate = new BackgroundWidget { Id = plateId, Background = "cc-mp-control" };
					header.AddChild(plate);
					header.Children.Remove(plate);
					header.Children.Insert(0, plate);
				}
				plate.Bounds = new WidgetBounds(label.Bounds.X, 0, Math.Max(0, label.Bounds.Width - 2), header.Bounds.Height);
				plate.IsVisible = label.IsVisible;
			}
		}

		static void LayoutIosPlayerRow(
			Widget row, IosLobbyLayout layout, IosMenuLayoutPolicy policy, bool isSkirmish)
		{
			row.Bounds.X = layout.PlayerRow.X;
			row.Bounds.Width = layout.PlayerRow.Width;
			row.Bounds.Height = layout.PlayerRow.Height;
			if (!isSkirmish || policy.IsPhone)
				foreach (var dropdown in row.Children.OfType<DropDownButtonWidget>())
					dropdown.ArrowWidth = Math.Max(16, row.Bounds.Height / 2);
			foreach (var id in new[] { "NAME", "SLOT_OPTIONS", "PLAYER_ACTION" })
				SetDirectBounds(row, id, layout.PlayerName);
			foreach (var id in new[] { "COLOR", "COLORBLOCK" })
				SetDirectBounds(row, id, layout.PlayerColor);
			var colorControl = DirectChild(row, "COLOR") as DropDownButtonWidget;
			if (colorControl != null)
			{
				colorControl.ArrowWidth = Math.Max(16, colorControl.Bounds.Height / 2);
				colorControl.HideArrow = policy.IsPhone;
			}
			var colorSwatch = colorControl != null ? DirectChild(colorControl, "COLORBLOCK") : DirectChild(row, "COLORBLOCK");
			if (colorSwatch != null)
			{
				var cell = colorControl?.Bounds ?? colorSwatch.Bounds;
				var inset = policy.IsPhone ? Math.Max(2, (int)Math.Round(2 * policy.LogicalPerPoint)) : Math.Max(2, cell.Height / 6);
				var contentWidth = colorControl?.UsableWidth ?? cell.Width;
				var insetX = Math.Max(1, Math.Min(inset, contentWidth / 8));
				colorSwatch.Bounds = new WidgetBounds(
					(colorControl == null ? cell.X : 0) + insetX,
					(colorControl == null ? cell.Y : 0) + inset,
					Math.Max(1, contentWidth - 2 * insetX), Math.Max(1, cell.Height - 2 * inset));
			}
			SetDirectBounds(row, "FACTION", layout.PlayerFaction);
			foreach (var id in new[] { "TEAM", "TEAM_DROPDOWN" })
				SetDirectBounds(row, id, layout.PlayerTeam);
			foreach (var id in new[] { "HANDICAP", "HANDICAP_DROPDOWN" })
				SetDirectBounds(row, id, layout.PlayerHandicap);
			foreach (var id in new[] { "SPAWN", "SPAWN_DROPDOWN" })
				SetDirectBounds(row, id, layout.PlayerSpawn);
			foreach (var id in new[]
			{
				"STATUS_CHECKBOX", "STATUS_IMAGE", "IOS_MAP_STATUS_TEXT", "IOS_MAP_STATUS_ICON"
			})
				SetDirectBounds(row, id, layout.PlayerReady);
			foreach (var id in new[] { "HANDICAP", "HANDICAP_DROPDOWN" })
			{
				var child = DirectChild(row, id);
				if (child != null)
					child.IsVisible = () => false;
			}
			foreach (var id in new[]
			{
				"STATUS_CHECKBOX", "STATUS_IMAGE", "IOS_MAP_STATUS_TEXT", "IOS_MAP_STATUS_ICON"
			})
			{
				var child = DirectChild(row, id);
				if (child != null && isSkirmish)
					child.IsVisible = () => false;
			}

			var actionBounds = new WidgetBounds(layout.PlayerColor.X, 0,
				Math.Max(0, layout.PlayerReady.Right - layout.PlayerColor.X), layout.PlayerRow.Height);
			foreach (var id in new[] { "JOIN" })
				SetDirectBounds(row, id, actionBounds);

			var latency = DirectChild(row, "LATENCY");
			if (latency != null)
				latency.Bounds = new WidgetBounds(layout.PlayerName.X, 0,
					Math.Min(layout.PlayerName.Width, layout.PlayerRow.Height), layout.PlayerRow.Height);
			var latencyRegion = DirectChild(row, "LATENCY_REGION");
			if (latencyRegion != null)
				latencyRegion.Bounds = new WidgetBounds(layout.PlayerName.X, 0,
					Math.Min(layout.PlayerName.Width, layout.PlayerRow.Height), layout.PlayerRow.Height);

			var faction = DirectChild(row, "FACTION");
			if (faction != null)
			{
				var flag = DirectChild(faction, "FACTIONFLAG");
				var highDefinitionFlag = Game.ModData?.Manifest?.Id == "ra2";
				var flagWidth = Math.Max(1, (int)Math.Ceiling((highDefinitionFlag ? 42 : 40) * policy.LogicalPerPoint));
				var flagHeight = Math.Max(1, (int)Math.Ceiling((highDefinitionFlag ? 42 : 20) * policy.LogicalPerPoint));
				flagWidth = Math.Min(flagWidth, faction.Bounds.Width);
				flagHeight = Math.Min(flagHeight, faction.Bounds.Height);
				if (flag != null)
				{
					flag.Bounds = new WidgetBounds(
						0, Math.Max(0, faction.Bounds.Height - flagHeight) / 2, flagWidth, flagHeight);
					if (flag is ImageWidget image)
						image.StretchToFit = true;
				}

				var name = DirectChild(faction, "FACTIONNAME");
				if (name != null)
				{
					var nameX = Math.Min(faction.Bounds.Width, flagWidth + policy.Gap);
					var usableWidth = faction is DropDownButtonWidget dropdown ?
						dropdown.UsableWidth : faction.Bounds.Width;
					name.Bounds = new WidgetBounds(nameX, 0,
						Math.Max(0, usableWidth - nameX), faction.Bounds.Height);
				}
			}
		}

		static void SetDirectBounds(Widget parent, string id, WidgetBounds bounds)
		{
			var child = DirectChild(parent, id);
			if (child != null)
				child.Bounds = bounds;
		}

		// Only the redesigned RA2 lobby opts into this skin. Common lobby templates
		// remain reusable by other mods and retain their network and visibility bindings.
		internal static void ApplySovietLobbyStyle(Widget root, bool touch)
		{
			if (root == null)
				return;

			var regular = touch ? "IosRegular" : "SettingsRegular";
			var bold = touch ? "IosBold" : "SettingsBold";
			if (root is LabelWidget label)
			{
				label.Font = label.Id == "SERVER_NAME" ? (touch ? "IosTitle" : "SettingsTitle") :
					label.Font.Contains("Bold", StringComparison.Ordinal) ? bold : regular;
			}
			else if (root is TextFieldWidget field)
			{
				field.Background = "cc-mp-field";
				field.Font = regular;
			}
			else if (root is CheckboxWidget checkbox)
			{
				checkbox.Background = "cc-mp-field";
				checkbox.Font = regular;
			}
			else if (root is ScrollItemWidget item)
			{
				if (item.Background != "cc-mp-control")
					item.SetBackground("cc-mp-control");

				item.Font = regular;
			}
			else if (root is ButtonWidget button)
			{
				if (button is DropDownButtonWidget dropdown)
				{
					dropdown.ShowSeparator = false;
					if (button.Background != "cc-mp-control" || dropdown.PreparePanel == null)
						dropdown.PreparePanel += popup => ApplySovietLobbyStyle(popup, touch);
				}
				button.Background = "cc-mp-control";
				button.Font = bold;
			}
			else if (root is ScrollPanelWidget scroll)
			{
				scroll.Background = "cc-mp-scrollpanel";
				scroll.ScrollBarBackground = "cc-mp-surface";
				scroll.Button = "cc-mp-control";
			}
			else if (root is SliderWidget slider)
			{
				slider.Track = "cc-mp-slider-track";
				slider.Thumb = "cc-mp-slider-thumb";
				slider.TouchBackground = "cc-mp-control";
				slider.TouchFont = bold;
				slider.TrackHeight = 4;
				slider.Ticks = 0;
			}
			else if (root is BackgroundWidget background)
				background.Background = "cc-mp-surface";

			foreach (var child in root.Children)
				ApplySovietLobbyStyle(child, touch);
		}

		void LayoutLobbyMusic(Widget music, IosMenuLayoutPolicy policy)
		{
			if (music == null)
				return;

			var layout = new LobbyMusicPlayerLayout(music.Bounds.Width, music.Bounds.Height, policy);
			WidgetBounds Bounds(Rectangle rect) => new(rect.X, rect.Y, rect.Width, rect.Height);
			var header = music.GetOrNull("LABEL_CONTAINER");
			if (header != null)
				header.IsVisible = () => false;

			var controls = music.GetOrNull<BackgroundWidget>("CONTROLS");
			if (controls != null)
			{
				var scroll = music.GetOrNull<ScrollPanelWidget>("MUSIC_CONTROLS_SCROLL");
				if (scroll == null)
				{
					scroll = new ScrollPanelWidget(modData)
					{
						Id = "MUSIC_CONTROLS_SCROLL", ScrollBar = ScrollBar.Hidden,
						ScrollbarWidth = 0, EnableContentDragging = true, Background = "cc-mp-surface"
					};
					music.HideChild(controls);
					music.AddChild(scroll);
					scroll.AddChild(controls);
				}

				scroll.Bounds = Bounds(layout.PlayerCard);
				scroll.ContentDragThreshold = policy.ContentDragThreshold;
				controls.Bounds = Bounds(layout.PlayerContent);
				controls.Background = "cc-mp-field";
				scroll.ContentHeight = layout.PlayerContent.Height;
				SetDirectBounds(controls, "MUTE_LABEL", Bounds(layout.Mute));
				SetDirectBounds(controls, "TITLE_LABEL", Bounds(layout.Title));
				SetDirectBounds(controls, "TIME_LABEL", Bounds(layout.Time));
				var buttons = controls.GetOrNull("BUTTONS");
				if (buttons != null)
				{
					buttons.Bounds = Bounds(layout.Buttons);
					foreach (var (id, index) in new[] { ("BUTTON_PREV", 0), ("BUTTON_PLAY", 1), ("BUTTON_PAUSE", 1), ("BUTTON_STOP", 2), ("BUTTON_NEXT", 3) })
					{
						var button = buttons.GetOrNull<ButtonWidget>(id);
						if (button == null)
							continue;

						button.Bounds = Bounds(layout.Button(index));
						foreach (var image in button.Children.OfType<ImageWidget>())
						{
							image.Bounds = Bounds(layout.Icon);
							image.StretchToFit = true;
						}
					}
				}

				SetDirectBounds(controls, "SHUFFLE", Bounds(layout.Shuffle));
				SetDirectBounds(controls, "REPEAT", Bounds(layout.Repeat));
				SetDirectBounds(controls, "VOLUME_LABEL", Bounds(layout.VolumeLabel));
				SetDirectBounds(controls, "MUSIC_SLIDER", Bounds(layout.VolumeSlider));
			}

			var list = music.GetOrNull<ScrollPanelWidget>("MUSIC_LIST");
			if (list != null)
			{
				list.Bounds = Bounds(layout.TrackList);
				list.EnableContentDragging = true;
				list.ContentDragThreshold = policy.ContentDragThreshold;
				list.ScrollbarWidth = policy.IsPhone ? 0 : layout.Target;
				list.ScrollBar = policy.IsPhone ? ScrollBar.Hidden : ScrollBar.Right;
				list.MinimumThumbSize = layout.Target;
				foreach (var row in list.Children)
				{
					row.Bounds = new WidgetBounds(0, row.Bounds.Y,
						Math.Max(0, list.Bounds.Width - list.ScrollbarWidth), layout.Target);
					SetDirectBounds(row, "TITLE", new WidgetBounds(layout.Gap, 0,
						Math.Max(0, row.Bounds.Width - layout.Target - 2 * layout.Gap), layout.Target));
					SetDirectBounds(row, "LENGTH", new WidgetBounds(Math.Max(0, row.Bounds.Width - layout.Target),
						0, layout.Target, layout.Target));
				}

				list.Layout.AdjustChildren();
			}

			var unavailable = music.GetOrNull("NO_MUSIC_LABEL");
			if (unavailable != null)
			{
				unavailable.Bounds = Bounds(layout.TrackList);
				var labelHeight = 2 * policy.MinimumReadableTextHeight;
				var y = Math.Max(0, (unavailable.Bounds.Height - 3 * labelHeight) / 2);
				foreach (var label in unavailable.Children.OfType<LabelWidget>())
				{
					label.Bounds = new WidgetBounds(0, y, unavailable.Bounds.Width, labelHeight);
					label.WordWrap = true;
					y += labelHeight;
				}
			}
		}

		void EnsureConfirmationScrollBody(Widget dialog)
		{
			if (dialog == null || dialog.GetOrNull("CONFIRMATION_BODY") != null)
				return;

			var body = new ScrollPanelWidget(modData)
			{
				Id = "CONFIRMATION_BODY", ScrollBar = ScrollBar.Hidden,
				ScrollbarWidth = 0, EnableContentDragging = true, Background = "cc-mp-surface"
			};
			foreach (var child in dialog.Children.ToArray())
			{
				if (child.Id is "OK_BUTTON" or "CANCEL_BUTTON")
					continue;

				dialog.HideChild(child);
				body.AddChild(child);
			}

			dialog.AddChild(body);
		}

		static void LayoutLobbyConfirmation(Widget dialog, IosMenuLayoutPolicy policy)
		{
			if (dialog == null)
				return;

			var body = dialog.GetOrNull<ScrollPanelWidget>("CONFIRMATION_BODY");
			if (body == null)
				return;

			var width = dialog.Bounds.Width;
			var target = policy.MinimumTarget;
			var textHeight = policy.MinimumReadableTextHeight;
			var actionY = Math.Max(0, dialog.Bounds.Height - target);
			body.Bounds = new WidgetBounds(0, 0, width, Math.Max(0, actionY - policy.Gap));
			body.ContentDragThreshold = policy.ContentDragThreshold;
			var y = 0;
			foreach (var label in body.Children.OfType<LabelWidget>())
			{
				label.Bounds = new WidgetBounds(policy.Gap, y, Math.Max(0, width - 2 * policy.Gap), textHeight);
				y += textHeight;
			}

			var warning = body.GetOrNull("KICK_WARNING");
			if (warning != null)
			{
				var visible = warning.IsVisible();
				warning.Bounds = new WidgetBounds(0, y, width, visible ? 2 * textHeight : 0);
				var warningY = 0;
				foreach (var label in warning.Children)
				{
					label.Bounds = new WidgetBounds(0, warningY, width, textHeight);
					warningY += textHeight;
				}

				if (visible)
					y = warning.Bounds.Bottom;
			}

			var preventRejoining = body.GetOrNull("PREVENT_REJOINING_CHECKBOX");
			if (preventRejoining != null)
			{
				preventRejoining.Bounds = new WidgetBounds(policy.Gap, y + policy.Gap, width - 2 * policy.Gap, target);
				y = preventRejoining.Bounds.Bottom;
			}

			body.ContentHeight = y;
			var actionWidth = Math.Max(0, (width - policy.Gap) / 2);
			SetDirectBounds(dialog, "OK_BUTTON", new WidgetBounds(0, actionY, actionWidth, target));
			SetDirectBounds(dialog, "CANCEL_BUTTON", new WidgetBounds(actionWidth + policy.Gap, actionY, actionWidth, target));
		}

		static Widget DirectChild(Widget parent, string id)
		{
			return parent.Children.FirstOrDefault(child => child.Id == id);
		}

		static void LayoutIosChat(Widget chatRoot, IosLobbyLayout layout, IosMenuLayoutPolicy policy)
		{
			var display = chatRoot.GetOrNull("CHAT_DISPLAY");
			if (display != null)
				display.Bounds = layout.ChatDisplay;
			if (display is ScrollPanelWidget scrollPanel)
			{
				scrollPanel.EnableContentDragging = true;
				scrollPanel.ContentDragThreshold = policy.ContentDragThreshold;
				if (policy.IsPhone || chatRoot.Parent?.Id == "LOBBY_CONTENT")
				{
					scrollPanel.ScrollBar = ScrollBar.Hidden;
					scrollPanel.ScrollbarWidth = 0;
				}
				else
				{
					scrollPanel.ScrollBar = ScrollBar.Right;
					scrollPanel.ScrollbarWidth = policy.MinimumTarget;
					scrollPanel.MinimumThumbSize = policy.MinimumTarget;
				}
			}

			var modeWidth = Math.Min(layout.ChatInput.Width, 2 * policy.MinimumTarget);
			var mode = chatRoot.GetOrNull("CHAT_MODE");
			if (mode != null)
				mode.Bounds = new WidgetBounds(
					layout.ChatInput.X, layout.ChatInput.Y, modeWidth, layout.ChatInput.Height);
			var textX = Math.Min(layout.ChatInput.Right,
				layout.ChatInput.X + modeWidth + policy.Gap);
			var text = chatRoot.GetOrNull("CHAT_TEXTFIELD");
			if (text != null)
				text.Bounds = new WidgetBounds(textX, layout.ChatInput.Y,
					Math.Max(0, layout.ChatInput.Right - textX), layout.ChatInput.Height);
		}

		bool disposed;
		protected override void Dispose(bool disposing)
		{
			if (disposing && !disposed)
			{
				disposed = true;
				safeStartState.Reset();
				Game.LobbyInfoChanged -= UpdateCurrentMap;
				Game.LobbyInfoChanged -= UpdatePlayerList;
				Game.LobbyInfoChanged -= UpdateDiscordStatus;
				Game.LobbyInfoChanged -= UpdateSpawnOccupants;
				Game.LobbyInfoChanged -= StartStatusLobbyInfoChanged;
				Game.BeforeGameStart -= OnGameStart;
				Game.ConnectionStateChanged -= ConnectionStateChanged;
				orderManager.ClientMapReadinessChanged -= ClientMapReadinessChanged;
				orderManager.SafeStartRejected -= SafeStartRejected;
			}

			base.Dispose(disposing);
		}

		bool OptionsTabDisabled()
		{
			return !MapIsPlayable || panel == PanelType.Kick || panel == PanelType.ForceStart;
		}

		bool IosLobbyLayoutChanged()
		{
			if (!Platform.UsesMobileLayout && lobby.GetOrNull("LOBBY_CONTENT") == null)
				return false;

			if (iosLobbyLayout == null || layoutPanel != panel)
				return true;

			var resolution = Game.Renderer.Resolution;
			var snapshot = IosScreenMetrics.SnapshotFor(resolution);
			return resolution != iosLayoutResolution || snapshot.NativePointSize != iosNativePointSize ||
				snapshot.SafeBounds != iosSafeBounds;
		}

		public override void Tick()
		{
			if (IosLobbyLayoutChanged())
				ApplyIosLobbyLayout();

			if (lobby.GetOrNull("LOBBY_CONTENT") != null && panel == PanelType.Options)
				ApplySovietLobbyStyle(lobby.GetOrNull("LOBBY_OPTIONS_BIN"), Platform.UsesMobileLayout);

			if (iosLayoutPolicy != null && panel == PanelType.ForceStart)
				LayoutLobbyConfirmation(lobby.GetOrNull("FORCE_START_DIALOG"), iosLayoutPolicy);

			if (panel == PanelType.Options && OptionsTabDisabled())
				panel = PanelType.Players;

			var chatWasEnabled = chatEnabled;
			chatEnabled =
				worldRenderer.World.IsReplay ||
				(Game.RunTime >= TextNotificationsManager.ChatDisabledUntil && TextNotificationsManager.ChatDisabledUntil != uint.MaxValue);

			if (chatEnabled && !chatWasEnabled)
			{
				chatTextField.Text = "";
				if (Ui.KeyboardFocusWidget == null)
					chatTextField.TakeKeyboardFocus();
			}
			else if (!chatEnabled)
			{
				var remaining = 0;
				if (TextNotificationsManager.ChatDisabledUntil != uint.MaxValue)
					remaining = (int)(TextNotificationsManager.ChatDisabledUntil - Game.RunTime + 999) / 1000;

				chatTextField.Text = remaining == 0 ? chatDisabled : chatAvailableIn.Update(remaining);
			}
		}

		public static bool ShouldDisplayNotification(bool skirmishMode, TextNotificationPool pool)
		{
			return !skirmishMode || pool != TextNotificationPool.Join;
		}

		void INotificationHandler<TextNotification>.Handle(TextNotification notification)
		{
			if (!ShouldDisplayNotification(skirmishMode, notification.Pool))
				return;

			var chatLine = chatTemplates[notification.Pool].Clone();
			if (lobby.GetOrNull("LOBBY_CONTENT") != null)
			{
				ApplySovietLobbyStyle(chatLine, Platform.UsesMobileLayout);
				foreach (var label in chatLine.Children.OfType<LabelWidget>())
				{
					var font = Game.Renderer.Fonts[label.Font];
					label.Bounds.Height = font.Measure("Ag").Y;
					if (label.Id == "TIME")
						label.Bounds.Width = font.Measure("00:00").X;
					chatLine.Bounds.Height = Math.Max(chatLine.Bounds.Height, label.Bounds.Height);
				}
			}

			WidgetUtils.SetupTextNotification(chatLine, notification, lobbyChatPanel.Bounds.Width - lobbyChatPanel.ScrollbarWidth, true);

			var scrolledToBottom = lobbyChatPanel.ScrolledToBottom;
			lobbyChatPanel.AddChild(chatLine);
			if (scrolledToBottom)
				lobbyChatPanel.ScrollToBottom(smooth: true);

			switch (notification.Pool)
			{
				case TextNotificationPool.Chat:
					Game.Sound.PlayNotification(modRules, null, "Sounds", chatLineSound, null);
					break;
				case TextNotificationPool.System:
					Game.Sound.PlayNotification(modRules, null, "Sounds", lobbyOptionChangedSound, null);
					break;
				case TextNotificationPool.Join:
					Game.Sound.PlayNotification(modRules, null, "Sounds", playerJoinedSound, null);
					break;
				case TextNotificationPool.Leave:
					Game.Sound.PlayNotification(modRules, null, "Sounds", playerLeftSound, null);
					break;
			}
		}

		void UpdateCurrentMap()
		{
			mapStatus = orderManager.LobbyInfo.GlobalSettings.MapStatus;
			var uid = orderManager.LobbyInfo.GlobalSettings.Map;
			if (map.Uid == uid)
				return;

			map = modData.MapCache[uid];

			// We don't have the map
			if (map.Status != MapStatus.Available && map.Status != MapStatus.DownloadAvailable &&
				Game.Settings.Game.AllowDownloading && !orderManager.RoomMapTransfer.Supported)
				modData.MapCache.QueryRemoteMapDetails(services.MapRepository, new[] { uid });
		}

		void UpdatePlayerList()
		{
			if (orderManager.LocalClient == null)
				return;

			// Check if we are not assigned to any team, and are no spectator
			// If we are a spectator, check if there are more and enable spectator chat
			// Otherwise check if our assigned team has more players
			if (orderManager.LocalClient.Team == 0 && !orderManager.LocalClient.IsObserver)
				disableTeamChat = true;
			else if (orderManager.LocalClient.IsObserver)
				disableTeamChat = !orderManager.LobbyInfo.Clients.Any(c => c != orderManager.LocalClient && c.IsObserver);
			else
				disableTeamChat = !orderManager.LobbyInfo.Clients.Any(c =>
					c != orderManager.LocalClient &&
					c.Bot == null &&
					c.Team == orderManager.LocalClient.Team);

			insufficientPlayerSpawns = LobbyUtils.InsufficientEnabledSpawnPoints(map, orderManager.LobbyInfo);

			if (disableTeamChat)
				teamChat = false;

			var isHost = Game.IsHost;
			var idx = 0;
			Widget localRow = null;
			foreach (var kv in orderManager.LobbyInfo.Slots)
			{
				var key = kv.Key;
				var slot = kv.Value;
				var client = orderManager.LobbyInfo.ClientInSlot(key);
				Widget template = null;

				// get template for possible reuse
				if (idx < players.Children.Count)
					template = players.Children[idx];

				if (client == null)
				{
					// Empty slot
					if (template == null || template.Id != emptySlotTemplate.Id)
						template = emptySlotTemplate.Clone();

					if (isHost)
						LobbyUtils.SetupEditableSlotWidget(template, slot, client, orderManager, map, modData);
					else
						LobbyUtils.SetupSlotWidget(template, modData, slot, client);

					var join = template.Get<ButtonWidget>("JOIN");
					join.IsVisible = () => !slot.Closed;
					join.IsDisabled = () => orderManager.LocalClient.IsReady;
					join.OnClick = () => orderManager.IssueOrder(Order.Command("slot " + key));
				}
				else if ((client.Index == orderManager.LocalClient.Index) ||
						 (client.Bot != null && isHost))
				{
					// Editable player in slot
					if (template == null || template.Id != editablePlayerTemplate.Id)
						template = editablePlayerTemplate.Clone();

					LobbyUtils.SetupLatencyWidget(template, client, orderManager);

					if (client.Bot != null)
						LobbyUtils.SetupEditableSlotWidget(template, slot, client, orderManager, map, modData);
					else
						LobbyUtils.SetupEditableNameWidget(template, client, orderManager, worldRenderer);

					LobbyUtils.SetupEditableClassicColorWidget(template, slot, client, orderManager, colorManager);
					LobbyUtils.SetupEditableFactionWidget(template, slot, client, orderManager, factions);
					LobbyUtils.SetupEditableTeamWidget(template, slot, client, orderManager, map);
					LobbyUtils.SetupEditableSpawnWidget(template, slot, client, orderManager, map);
					if (!skirmishMode)
						LobbyUtils.SetupEditableReadyWidget(template, client, orderManager, map, MapIsPlayable,
							orderManager.LobbyInfo.GlobalSettings.Map, Platform.UsesMobileLayout);
				}
				else
				{
					// Non-editable player in slot
					if (template == null || template.Id != nonEditablePlayerTemplate.Id)
						template = nonEditablePlayerTemplate.Clone();

					LobbyUtils.SetupLatencyWidget(template, client, orderManager);
					LobbyUtils.SetupColorWidget(template, client);
					LobbyUtils.SetupFactionWidget(template, client, factions);

					if (isHost)
					{
						LobbyUtils.SetupEditableTeamWidget(template, slot, client, orderManager, map);
						LobbyUtils.SetupEditableSpawnWidget(template, slot, client, orderManager, map);
						LobbyUtils.SetupPlayerActionWidget(template, client, orderManager, worldRenderer,
							lobby, () => panel = PanelType.Kick, () => panel = PanelType.Players);
					}
					else
					{
						LobbyUtils.SetupNameWidget(template, client, orderManager, worldRenderer, map);
						LobbyUtils.SetupTeamWidget(template, client);
						LobbyUtils.SetupSpawnWidget(template, client);
					}

					if (!skirmishMode)
						LobbyUtils.SetupReadyWidget(template, client,
							orderManager.LobbyInfo.GlobalSettings.Map, Platform.UsesMobileLayout);
				}

				template.IsVisible = () => true;
				if (client != null && client.Index == orderManager.LocalClient.Index)
					localRow = template;

				if (idx >= players.Children.Count)
					players.AddChild(template);
				else if (players.Children[idx].Id != template.Id)
					players.ReplaceChild(players.Children[idx], template);

				idx++;
			}

			while (players.Children.Count > idx)
				players.RemoveChild(players.Children[idx]);

			if (iosLobbyLayout != null)
			{
				LayoutIosPlayerRows(iosLobbyLayout, iosLayoutPolicy, skirmishMode);
				if (lobby.GetOrNull("LOBBY_CONTENT") != null)
					ApplySovietLobbyStyle(players, Platform.UsesMobileLayout);
			}

			// Joining clients may occupy a row below the small-screen viewport.
			// Reveal only on joining/changing slot, never on ordinary lobby updates.
			var localSlot = orderManager.LocalClient.Slot;
			if (localRow != null && localSlot != revealedLocalSlot)
			{
				players.ScrollToItem(localRow);
				revealedLocalSlot = localSlot;
			}

			tabCompletion.Names = orderManager.LobbyInfo.Clients.Where(c => !c.IsBot).Select(c => c.Name).Distinct().ToList();
		}

		void UpdateDiscordStatus()
		{
			var numberOfPlayers = 0;
			var slots = 0;

			if (!skirmishMode)
			{
				foreach (var kv in orderManager.LobbyInfo.Slots)
				{
					if (kv.Value.Closed)
						continue;

					slots++;
					var client = orderManager.LobbyInfo.ClientInSlot(kv.Key);

					if (client != null)
						numberOfPlayers++;
				}
			}

			// Add extra slots to keep the join button active for spectators
			if (numberOfPlayers == slots && orderManager.LobbyInfo.GlobalSettings.AllowSpectators)
				slots = numberOfPlayers + 1;

			var details = map.Title + " - " + orderManager.LobbyInfo.GlobalSettings.ServerName;
			if (updateDiscordStatus)
			{
				string secret = null;
				if (orderManager.LobbyInfo.GlobalSettings.Dedicated)
				{
					var endpoint = CurrentServerSettings.Target.GetConnectEndPoints().First();
					secret = string.Concat(endpoint.Address, "|", endpoint.Port);
				}

				var state = skirmishMode ? DiscordState.InSkirmishLobby : DiscordState.InMultiplayerLobby;

				DiscordService.UpdateStatus(state, details, secret, numberOfPlayers, slots);
				updateDiscordStatus = false;
			}
			else
			{
				if (!skirmishMode)
					DiscordService.UpdatePlayers(numberOfPlayers, slots);

				DiscordService.UpdateDetails(details);
			}
		}

		void UpdateSpawnOccupants()
		{
			spawnOccupants = orderManager.LobbyInfo.Clients
				.Where(c => c.SpawnPoint != 0)
				.ToDictionary(c => c.SpawnPoint, c => new SpawnOccupant(c));
		}

		void UpdateOptions()
		{
			if (map == null || map.WorldActorInfo == null)
				return;

			var mapOptions = map.PlayerActorInfo.TraitInfos<ILobbyOptions>()
				.Concat(map.WorldActorInfo.TraitInfos<ILobbyOptions>())
				.SelectMany(t => t.LobbyOptions(map))
				.Where(o => o.IsVisible)
				.OrderBy(o => o.DisplayOrder)
				.ToArray();

			resetOptionsButtonEnabled = mapOptions.Any(o =>
				o.DefaultValue != orderManager.LobbyInfo.GlobalSettings.OptionOrDefault(o.Id, o.DefaultValue));
		}

		void OnGameStart()
		{
			Ui.CloseWindow();

			var state = skirmishMode ? DiscordState.PlayingSkirmish : DiscordState.PlayingMultiplayer;
			var details = map.Title + " - " + orderManager.LobbyInfo.GlobalSettings.ServerName;
			DiscordService.UpdateStatus(state, details);

			onStart();
		}
	}

	public class LobbyFaction
	{
		public bool Selectable;
		public string Name;
		public string Description;
		public string Side;
	}

	sealed class DropDownOption
	{
		public string Title;
		public Func<bool> IsSelected = () => false;
		public Action OnClick;
	}
}
