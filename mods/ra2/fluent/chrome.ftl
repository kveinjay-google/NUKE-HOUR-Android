
## mainmenu.yaml
notification-enemy-defeated = Enemy defeated.
dialog-save-error-title = Save operation failed
label-save-busy = Saving… Please keep the game open.
notification-save-load-failed = This save is damaged or unreadable. The current game has not been closed.
notification-save-map-unavailable = The map required by this save is unavailable. Import the original map and try again.
notification-save-write-failed = Save failed. Check available storage and try again. An existing save has not been replaced.

button-mapchooser-panel-third-party-maps-tab = View Third-Party Maps
button-main-menu-ui-style-classic = Interface Style: Classic
button-main-menu-ui-style-hd = Interface Style: HD
label-ui-style-classic = Classic
label-ui-style-classic-hd = Classic HD
label-ui-style-modern-hd = Modern HD
map-category-all = All maps
map-category-players-2 = 2 players
map-category-players-3-4 = 3–4 players
map-category-players-5-6 = 5–6 players
map-category-players-7plus = 7+ players
map-category-naval = Naval maps
map-category-snow = Snow maps
map-category-temperate = Temperate maps
label-campaign-title = Campaign

## ranked.yaml
label-ranked-hub-title = Multiplayer
button-ranked-match = Ranked Match
button-online-rooms = Online Multiplayer
button-local-multiplayer = LAN Multiplayer
button-season-leaderboard = Season Leaderboard
label-ranked-match-title = Ranked Match
label-ranked-username = Username
label-ranked-password = Password
label-ranked-account = Signed in as { $username }
button-ranked-login = Sign In
button-ranked-register = Create Account
button-ranked-queue = Find 1v1 Match
button-ranked-cancel = Cancel Search
button-ranked-accept = Accept Match
button-ranked-decline = Decline
button-ranked-logout = Sign Out
label-ranked-status-signed-out = Sign in or create an account. Username: 3–24 characters; password: 12–128 characters.
label-ranked-status-restoring = Restoring your secure ranked session…
label-ranked-status-binding = Verifying this installation…
label-ranked-status-keygen = Preparing this installation's secure key…
label-ranked-status-ready = Ready for official 1v1 matchmaking.
label-ranked-status-queued = Searching for a compatible opponent…
label-ranked-status-found = Match found. Accept before the timer expires.
label-ranked-status-waiting-opponent = Accepted. Waiting for your opponent…
label-ranked-status-connecting = Official server assigned. Connecting…
label-ranked-status-cooldown = Queue cooldown active. Please wait briefly.
label-ranked-status-error = Ranked is temporarily unavailable.
label-ranked-error-credentials = The username or password was not accepted.
label-ranked-error-service = Ranked service is unavailable. Ordinary multiplayer is unaffected.
label-ranked-recovery-codes = Recovery codes — save these now: { $codes }
label-ranked-leaderboard-title = Season Leaderboard
label-ranked-leaderboard-loading = Loading current season…
label-ranked-leaderboard-empty = No ranked players are visible yet.
label-ranked-rank-column = Rank
label-ranked-player-column = Player
label-ranked-rating-column = Rating
label-ranked-record-column = W–L
label-ranked-result-title = Ranked Match Result
label-ranked-result-opponent = Opponent: { $opponent }
label-ranked-result-pending = Result Pending
label-ranked-result-settling = Waiting for the official server to settle this match…
label-ranked-result-win = Victory
label-ranked-result-loss = Defeat
label-ranked-result-rating = Rating: { $before } → { $after } ({ $delta })
label-ranked-result-complete = Rating updated from the official match record.
label-ranked-result-void = Match Voided
label-ranked-result-void-detail = No rating change was applied.
label-ranked-result-sign-in = Sign in again to view your rating change.
label-ranked-result-retrying = Settlement is still pending. Retrying automatically…
button-ranked-result-continue = Continue

## ingame-debug.yaml
label-debug-panel-title = Debug Options
checkbox-debug-panel-instant-build = Instant Build Speed
checkbox-debug-panel-enable-tech = Build Everything
checkbox-debug-panel-build-anywhere = Build Anywhere
checkbox-debug-panel-unlimited-power = Unlimited Power
checkbox-debug-panel-instant-charge = Instant Charge Time
checkbox-debug-panel-disable-visibility-checks = Disable Visibility Checks
button-debug-panel-give-cash = Give $20,000
button-debug-panel-grow-resources = Grow Resources
button-debug-panel-give-exploration = Clear Shroud
button-debug-panel-reset-exploration = Reset Shroud
label-debug-panel-visualizations-title = Visualizations
checkbox-debug-panel-show-unit-paths = Show Unit Paths
checkbox-debug-panel-show-customterrain-overlay = Show Custom Terrain
checkbox-debug-panel-show-actor-tags = Show Actor Tags
checkbox-debug-panel-show-depth-preview = Show Depth Data
checkbox-debug-panel-show-combatoverlay = Show Combat Geometry
checkbox-debug-panel-show-geometry = Show Render Geometry
checkbox-debug-panel-show-terrain-overlay = Show Terrain Geometry
checkbox-debug-panel-show-screenmap = Show Screen Map

## ingame-observer.yaml
button-observer-widgets-options = Options (Esc)
button-replay-player-pause-tooltip = Pause
button-replay-player-play-tooltip = Play

button-replay-player-slow =
    .tooltip = Slow speed
    .label = 50%

button-replay-player-regular =
    .tooltip = Regular speed
    .label = 100%

button-replay-player-fast =
    .tooltip = Fast speed
    .label = 200%

button-replay-player-maximum =
    .tooltip = Maximum speed
    .label = MAX

label-basic-stats-player-header = Player
label-basic-stats-cash-header = Cash
label-basic-stats-power-header = Power
label-basic-stats-kills-header = Kills
label-basic-stats-deaths-header = Deaths
label-basic-stats-assets-destroyed-header = Destroyed
label-basic-stats-assets-lost-header = Lost
label-basic-stats-experience-header = Score
label-basic-stats-actions-min-header = APM
label-economy-stats-player-header = Player
label-economy-stats-cash-header = Cash
label-economy-stats-income-header = Income
label-economy-stats-assets-header = Assets
label-economy-stats-earned-header = Earned
label-economy-stats-spent-header = Spent
label-economy-stats-harvesters-header = Harvesters
label-production-stats-player-header = Player
label-production-stats-header = Production
label-support-powers-player-header = Player
label-support-powers-header = Support Powers
label-army-player-header = Player
label-army-header = Army
label-combat-stats-player-header = Player
label-combat-stats-assets-destroyed-header = Destroyed
label-combat-stats-assets-lost-header = Lost
label-combat-stats-units-killed-header = U. Killed
label-combat-stats-units-dead-header = U. Lost
label-combat-stats-buildings-killed-header = B. Killed
label-combat-stats-buildings-dead-header = B. Lost
label-combat-stats-army-value-header = Army Value
label-combat-stats-vision-header = Vision

## ingame-player.yaml
supportpowers-support-powers-palette =
    .ready = READY
    .hold = ON HOLD

button-command-bar-production-x5 =
    .short = ×5
    .tooltip = Produce Five
    .tooltipdesc = Toggle five-at-a-time production. While enabled, each tap queues five units or standard buildings. Unique structures still obey their build limit.

button-command-bar-touch-ctrl =
    .short = CTRL
    .tooltip = Ctrl Modifier
    .tooltipdesc = Toggle Ctrl mode. When enabled, number keys overwrite control groups and battlefield commands force-attack.

button-command-bar-attack-move =
    .label = Atk Move
    .tooltip = Attack Move
    .tooltipdesc = Selected units will move to the desired location
    and attack any enemies they encounter en route.
    
    Hold <(Ctrl)> while targeting to order an Assault Move
    that attacks any units or structures encountered en route.
    
    Left-click icon then right-click on target location.

button-command-bar-force-move =
    .label = Force
    .tooltip = Force Move
    .tooltipdesc = Selected units will move to the desired location
     - Default activity for the target is suppressed
     - Vehicles will attempt to crush enemies at the target location
    
    Left-click icon then right-click on target.
    Hold <(Alt)> to activate temporarily while commanding units.

button-command-bar-force-attack =
    .label = Force Atk
    .tooltip = Force Attack
    .tooltipdesc = Selected units will attack the targeted unit or location
    ignoring their default activity for the target.
    
    Left-click icon then right-click on target.
    Hold <(Ctrl)> to activate temporarily while commanding units.

button-command-bar-guard =
    .label = Guard
    .tooltip = Guard
    .tooltipdesc = Selected units will follow the targeted unit.
    
    Left-click icon then right-click on target unit.

button-command-bar-deploy =
    .label = Deploy
    .tooltip = Deploy
    .tooltipdesc = Selected units will perform their default deploy activity
     - MCVs will unpack into a Construction Yard
     - Construction Yards will re-pack into a MCV
     - Transports will unload their passengers
     - Demolition Trucks and MAD Tanks will self-destruct
     - Aircraft will return to base
    
    Acts immediately on selected units.

button-command-bar-scatter =
    .label = Scatter
    .tooltip = Scatter
    .tooltipdesc = Selected units will stop their current activity
    and move to a nearby location.
    
    Acts immediately on selected units.

button-command-bar-form-up =
    .label = Form Up
    .tooltip = Form Up
    .tooltipdesc = Selected mobile units will immediately assemble
    into a compact formation around their current position.

    Acts immediately without requiring a map target.

button-command-bar-stop =
    .label = Stop
    .tooltip = Stop
    .tooltipdesc = Selected units will stop their current activity.
    Selected buildings will reset their rally point.
    
    Acts immediately on selected targets.

button-ios-viewport-action-stop =
    .label = Stop
button-ios-viewport-action-deploy =
    .label = Deploy
button-ios-viewport-action-select-type =
    .label = Type
button-ios-viewport-action-force-attack =
    .label = Force
button-ios-viewport-action-return-base =
    .label = Base

button-command-bar-queue-orders =
    .label = Waypoint
    .tooltip = Waypoint Mode
    .tooltipdesc = Use Waypoint Mode to give multiple linking commands
    to the selected units. Units will execute the commands
    immediately upon receiving them.
    
    Left-click icon then give commands in the game world.
    Hold <(Shift)> to activate temporarily while commanding units.

button-stance-bar-attackanything =
    .label = Attack
    .tooltip = Attack Anything Stance
    .tooltipdesc = Set the selected units to Attack Anything stance:
     - Units will attack enemy units and structures on sight
     - Units will pursue attackers across the battlefield

button-stance-bar-defend =
    .label = Defend
    .tooltip = Defend Stance
    .tooltipdesc = Set the selected units to Defend stance:
     - Units will attack enemy units on sight
     - Units will not move or pursue enemies

button-stance-bar-returnfire =
    .label = Return
    .tooltip = Return Fire Stance
    .tooltipdesc = Set the selected units to Return Fire stance:
     - Units will retaliate against enemies that attack them
     - Units will not move or pursue enemies

button-stance-bar-holdfire =
    .label = Hold
    .tooltip = Hold Fire Stance
    .tooltipdesc = Set the selected units to Hold Fire stance:
     - Units will not fire upon enemies
     - Units will not move or pursue enemies

button-top-buttons-fps-tooltip = Toggle FPS Display
button-top-buttons-debug-tooltip = Debug Menu
button-top-buttons-floating-controls-tooltip = Show/Hide Touch Controls
button-top-buttons-options-tooltip = Options
button-top-buttons-repair-tooltip = Repair (click buildings)
button-top-buttons-auto-repair-tooltip = Auto-Repair
button-top-buttons-power-tooltip = Auto-Repair
button-top-buttons-beacon-tooltip = Place Beacon
button-top-buttons-sell-tooltip = Sell
labelwithtooltip-player-widgets-cash = <0>
labelwithtooltip-player-widgets-power = <0>

productionpalette-sidebar-production-palette =
    .ready = READY
    .hold = ON HOLD

button-production-types-building-tooltip = Buildings
button-production-types-support-tooltip = Support
button-production-types-infantry-tooltip = Infantry
button-production-types-vehicle-tooltip = Vehicles
button-production-types-aircraft-tooltip = Aircraft
button-production-types-naval-tooltip = Naval
button-production-types-scroll-up-tooltip = Scroll up
button-production-types-scroll-down-tooltip = Scroll down
dropdownbutton-hpf-overlay-locomotor = Select Locomotor
dropdownbutton-hpf-overlay-check = Select BlockedByActor

## mainmenu-prerelease-notification.yaml
label-mainmenu-prerelease-notification-prompt-title = NUKE HOUR developer preview
label-mainmenu-prerelease-notification-prompt-text-a = This pre-alpha build of NUKE HOUR is made available
label-mainmenu-prerelease-notification-prompt-text-b = for the community to follow development and as example for modders.
label-mainmenu-prerelease-notification-prompt-text-c = Many features are missing or incomplete, performance has not been
label-mainmenu-prerelease-notification-prompt-text-d = optimized, and balance will not be addressed until a future beta.
button-mainmenu-prerelease-notification-continue = I Understand

## mainmenu.yaml
label-menu-subtitle = NUKE HOUR


## settings.yaml tabs
button-settings-title = GAME SETTINGS
button-settings-tab-display = Display
button-settings-tab-audio = Audio
button-settings-tab-input = Input
button-settings-tab-touch = Touch Controls
button-settings-tab-hotkeys = Hotkeys
button-settings-tab-advanced = Advanced
label-settings-profile-section = COMMANDER PROFILE
label-settings-resource-section = GAME RESOURCES
label-settings-network-section = NETWORK SERVICES
label-settings-diagnostics-section = DIAGNOSTICS
## settings-advanced.yaml
label-forum-account-section-header = Forum Account

## ingame-player.yaml selection bar
button-selection-bar-group-01 =
    .label = 1
button-selection-bar-group-02 =
    .label = 2
button-selection-bar-group-03 =
    .label = 3
button-selection-bar-group-04 =
    .label = 4
button-selection-bar-group-05 =
    .label = 5
button-selection-bar-group-06 =
    .label = 6
button-selection-bar-group-07 =
    .label = 7
button-selection-bar-group-08 =
    .label = 8
button-selection-bar-group-09 =
    .label = 9
button-selection-bar-group-10 =
    .label = 0
button-selection-bar-group =
    .tooltip = Control Group
    .tooltipdesc = Left-click to select this group.
    Double-click or Alt+click to jump the camera to the group.

    Ctrl+click to assign the current selection to this group.
    Shift+click to add this group to the current selection.
    Ctrl+Shift+click to add the selection into this group.
button-selection-bar-select-all =
    .label = Select All
    .tooltip = Select All Combat Units
    .tooltipdesc = First click selects combat units on screen.
    Click again to expand the selection across the whole map.
button-selection-bar-select-by-type =
    .label = Same Type
    .tooltip = Select Same Type
    .tooltipdesc = Expand the selection to other units of the same type.
    First click covers the screen, second click covers the whole map.

button-selection-bar-cycle-base =
    .label = Base
    .tooltip = Jump to Base
    .tooltipdesc = Cycle through and center on your base buildings.
button-selection-bar-to-selection =
    .label = Focus
    .tooltip = Jump to Selection
    .tooltipdesc = Center the viewport on the current selection.
button-selection-bar-to-last-event =
    .label = Event
    .tooltip = Jump to Last Radar Event
    .tooltipdesc = Center the viewport on the most recent radar ping.
button-selection-bar-cycle-harvesters =
    .label = Miner
    .tooltip = Cycle Harvesters
    .tooltipdesc = Cycle through and center on your harvesters.
button-selection-bar-remove-from-group =
    .label = Ungroup
    .tooltip = Remove from Control Group
    .tooltipdesc = Remove the selected units from their control group.
button-selection-bar-sell =
    .label = Sell
    .tooltip = Sell Mode
    .tooltipdesc = Enter sell mode and left-click a building to sell it.
button-selection-bar-repair =
    .label = Repair
    .tooltip = Repair Mode
    .tooltipdesc = Enter repair mode and left-click a building to repair it.
button-selection-bar-beacon =
    .label = Beacon
    .tooltip = Place Beacon
    .tooltipdesc = Place a beacon marker on the map.

button-selection-bar-auto-repair =
    .label = Auto-Repair
    .tooltip = Auto-Repair Mode
    .tooltipdesc = Toggle automatic building repair on or off.

button-command-bar-edit =
    .label = Edit
    .tooltip = Edit Command Bar
    .tooltipdesc = Enter edit mode: drag to reorder, click or right-click to show/hide commands.
    Click again to save and exit.
button-command-bar-collapse =
    .label = ▾
    .tooltip = Collapse Command Bar
    .tooltipdesc = Collapse the command bar to free more screen space.
button-command-bar-expand =
    .label = ▴
    .tooltip = Expand Command Bar
    .tooltipdesc = Expand to the full command bar with labels.
label-command-bar-edit-hint = Drag to reorder · Click to toggle · Right-click also toggles

# Original Red Alert 2 campaign mission names used by the private imported mission browser.
campaign-allied-title = Allied Campaign
campaign-soviet-title = Soviet Campaign
mission-allied-01-title = Lone Guardian
mission-allied-02-title = Eagle Dawn
mission-allied-03-title = Hail to the Chief
mission-allied-04-title = Last Chance
mission-allied-05-title = Dark Night
mission-allied-06-title = Liberty
mission-allied-07-title = Deep Sea
mission-allied-08-title = Free Gateway
mission-allied-09-title = Sun Temple
mission-allied-10-title = Mirage
mission-allied-11-title = Fallout
mission-allied-12-title = Chrono Storm
mission-soviet-01-title = Red Dawn
mission-soviet-02-title = Hostile Shore
mission-soviet-03-title = Big Apple
mission-soviet-04-title = Home Front
mission-soviet-05-title = City of Lights
mission-soviet-06-title = Sub-Divide
mission-soviet-07-title = Chrono Defense
mission-soviet-08-title = Desecration
mission-soviet-09-title = The Fox and the Hound
mission-soviet-10-title = Weathered Alliance
mission-soviet-11-title = Red Revolution
mission-soviet-12-title = Polar Storm

# Independently written NUKE HOUR intelligence for privately imported campaign missions.
mission-allied-01-theme = Harbor counterstrike and emergency defense of New York.
mission-allied-01-history = In this alternate 1970s, a surprise Soviet Atlantic crossing opens with a symbolic strike on Liberty Island. With the coastal command network disrupted, a small field team must buy time for the city to organize its defense.
mission-allied-01-objectives = Destroy the attacking Dreadnought group; reach and secure Fort Bradley; eliminate the local invasion base.
mission-allied-01-assets = Tanya is the initial field asset. A permanent base and additional Allied defenders become available only after contact with Fort Bradley.
mission-allied-01-notes = The Statue of Liberty and Tanya are mission-critical. This is a staged operation: no construction base is available during the opening phase.

mission-allied-02-theme = Airfield recovery and rapid airborne counterattack.
mission-allied-02-history = Soviet spearheads have occupied a key American air academy, cutting off a major reinforcement route across the continent. Recovering the field would restore Allied control of the central air corridor.
mission-allied-02-objectives = Penetrate the occupied academy; restore Allied control of the airfield; remove the Soviet forces holding the sector.
mission-allied-02-assets = Tanya and a limited insertion force begin the operation. Airborne reinforcements and captured infrastructure expand the force after the perimeter is breached.
mission-allied-02-notes = Preserve the airfield facilities and use mobility instead of attempting a frontal battle with the opening force.

mission-allied-03-theme = Defense of the national command authority in Washington.
mission-allied-03-history = A Soviet psychological-warfare detachment has reached the capital and is attempting to break the government without a prolonged siege. The defense must hold long enough to expose and dismantle the control network.
mission-allied-03-objectives = Protect the national leadership; locate the source of Soviet mind control; destroy the hostile command presence in Washington.
mission-allied-03-assets = An established Allied base, local defensive units, engineers, and conventional reinforcements are available.
mission-allied-03-notes = Civilian landmarks share the battlefield with military targets. Losing the protected command site ends the operation.

mission-allied-04-theme = Time-critical strike against the Chicago control complex.
mission-allied-04-history = Chicago has become the test site for a strategic psychic transmitter capable of subduing an entire metropolitan area. Allied command has one narrow window to land a force before the device reaches full power.
mission-allied-04-objectives = Establish a foothold; break through the city defenses; destroy the Psychic Amplifier before activation.
mission-allied-04-assets = An amphibious landing group, an MCV, and limited naval support form the initial force.
mission-allied-04-notes = The operation is timed. Protect the MCV and prioritize the amplifier over nonessential enemy positions.

mission-allied-05-theme = Covert sabotage of an occupied nuclear launch network.
mission-allied-05-history = Intelligence has located Soviet missile silos hidden inside an occupied European region. A conventional offensive would trigger an immediate launch, so a small night team must disable the complex from within.
mission-allied-05-objectives = Infiltrate the restricted zone; sabotage both nuclear missile sites; extract the surviving operatives.
mission-allied-05-assets = Tanya and covert-support personnel are available. There is no construction base and replacements are extremely limited.
mission-allied-05-notes = Detection can bring overwhelming reinforcements. Use disguises, demolition, and terrain cover rather than direct combat.

mission-allied-06-theme = Combined-arms liberation of the American capital.
mission-allied-06-history = With the Soviet control network weakened, Allied forces can attempt a full return to Washington. The battle is both military and political: reopening the capital will reconnect isolated resistance groups across the country.
mission-allied-06-objectives = Establish a functioning base; recover key districts; destroy the Soviet occupation command and its remaining forces.
mission-allied-06-assets = An MCV, infantry, armor, and scheduled Allied reinforcements support the offensive.
mission-allied-06-notes = Expect layered urban defenses. Secure power and production capacity before committing armor into the city center.

mission-allied-07-theme = Defense of Hawaii and destruction of an ocean-going invasion force.
mission-allied-07-history = The struggle has shifted into the Pacific, where Hawaii controls the sea lanes linking Allied industry and forward bases. A Soviet fleet is preparing to isolate the islands and open a route toward the west coast.
mission-allied-07-objectives = Hold the island installations; build a viable naval force; destroy the Soviet fleet and its supporting bases.
mission-allied-07-assets = A coastal base, shipyard access, ground defenses, and naval production are available.
mission-allied-07-notes = Anti-submarine coverage and ship repair are essential. Losing the island production core will make recovery difficult.

mission-allied-08-theme = Liberation of a mind-controlled city and reopening of the inland corridor.
mission-allied-08-history = Soviet control equipment has turned a major river city into a barrier between the eastern and western Allied commands. Local resistance can act only after the transmitter is disabled.
mission-allied-08-objectives = Establish a base outside the controlled district; destroy the Psychic Beacon; clear the Soviet garrison from the transport corridor.
mission-allied-08-assets = An MCV, a mobile ground detachment, and resistance support become available during the advance.
mission-allied-08-notes = Avoid unnecessary civilian losses. The city changes allegiance when the control signal is removed.

mission-allied-09-theme = Jungle raid on a Soviet archaeological weapons project.
mission-allied-09-history = Soviet researchers have occupied an ancient temple complex and adapted its unusual energy geometry for battlefield use. A small Allied expedition must prevent the site from becoming a new strategic weapon.
mission-allied-09-objectives = Reach the temple zone; disable the Soviet research operation; destroy the military garrison protecting the site.
mission-allied-09-assets = A compact special-operations force and limited local reinforcements are available; base construction is restricted.
mission-allied-09-notes = Dense terrain conceals both patrols and alternate routes. Preserve specialist units needed to breach the research area.

mission-allied-10-theme = Defense of Einstein's Black Forest research network.
mission-allied-10-history = Allied research teams have dispersed prototype work across the Black Forest to keep it beyond the reach of strategic bombardment. Soviet armored columns are now converging on the laboratories from several directions.
mission-allied-10-objectives = Keep the research sites operational; stop each Soviet attack group; eliminate the bases coordinating the offensive.
mission-allied-10-assets = Multiple Allied positions, defensive forces, advanced armor, and production facilities are available.
mission-allied-10-notes = The front is wide. Reinforce threatened laboratories instead of concentrating every unit at one base.

mission-allied-11-theme = Chronosphere-assisted assault on a Caribbean nuclear complex.
mission-allied-11-history = The remaining Soviet nuclear force has been consolidated on fortified islands close to the American mainland. Allied command plans to bypass the surrounding fleet by using experimental displacement technology.
mission-allied-11-objectives = Establish the forward base; use the Chronosphere to cross the defenses; destroy the nuclear launch capability.
mission-allied-11-assets = An Allied base group, naval support, advanced technology, and Chronosphere access are available.
mission-allied-11-notes = Coordinate transported units carefully: displacement without anti-air and repair support can strand the assault force.

mission-allied-12-theme = Final strategic assault on the Soviet command center in Moscow.
mission-allied-12-history = With Soviet expeditionary armies cut off, the war can be ended only by removing the command structure that launched the invasion. The Allies prepare a high-risk strike into the most heavily defended city in Europe.
mission-allied-12-objectives = Create a secure arrival zone; break the defenses surrounding the capital; neutralize the Soviet high command.
mission-allied-12-assets = A veteran combined-arms force, advanced Allied production, and strategic displacement support are available.
mission-allied-12-notes = Expect superweapons and elite defenders. Maintain power redundancy and protect the technology enabling reinforcement.

mission-soviet-01-theme = Decapitation strike against the American military command.
mission-soviet-01-history = In this alternate opening of the war, Soviet forces land near Washington before the Allied coalition can fully mobilize. Capturing the military center would turn a surprise attack into a continental campaign.
mission-soviet-01-objectives = Establish the invasion force; breach Washington's defenses; destroy the Pentagon command complex.
mission-soviet-01-assets = Soviet infantry, armor, engineers, and a deployable construction force support the opening attack.
mission-soviet-01-notes = Advance quickly before Allied production stabilizes. Preserve engineers for infrastructure that can accelerate the assault.

mission-soviet-02-theme = Amphibious seizure of a fortified Florida coast.
mission-soviet-02-history = The southern coastline protects naval routes and airfields needed for the Soviet advance inland. An amphibious landing must secure enough ground for heavy reinforcements to arrive.
mission-soviet-02-objectives = Land the initial force; establish a coastal base; destroy the Allied fleet and defenders controlling the sector.
mission-soviet-02-assets = Landing craft, naval units, an MCV, and follow-on ground reinforcements are available.
mission-soviet-02-notes = Secure anti-air and production before moving away from the beachhead. The transport fleet cannot replace a lost MCV.

mission-soviet-03-theme = Capture of New York and deployment of a psychic occupation system.
mission-soviet-03-history = Rather than destroy the largest American city, Soviet command intends to use it as a demonstration of controlled occupation. The plan depends on acquiring Allied research and protecting a newly installed transmitter.
mission-soviet-03-objectives = Capture the designated Allied laboratory; construct the Psychic Beacon; defend it until the city is subdued.
mission-soviet-03-assets = A Soviet base force, engineers, armor, and urban reinforcements are available.
mission-soviet-03-notes = The laboratory must be captured, not destroyed. Prepare layered defenses before the beacon becomes operational.

mission-soviet-04-theme = Coastal defense of Vladivostok against an Allied-backed Korean attack.
mission-soviet-04-history = The Allied coalition attempts to open a second front in the Soviet Far East by striking the Pacific fleet at anchor. Holding Vladivostok is essential to keep the eastern sea routes open.
mission-soviet-04-objectives = Defend the harbor installations; repel the landing forces; destroy the hostile fleet and its shore support.
mission-soviet-04-assets = A Soviet coastal base, naval production, submarines, and defensive ground forces are available.
mission-soviet-04-notes = Watch both sea approaches. Repair damaged ships and prevent enemy carriers from operating unchallenged.

mission-soviet-05-theme = Urban power projection through an improvised Tesla network.
mission-soviet-05-history = Paris has become a communications hub for the European Allied resistance. Soviet engineers propose turning the city's tallest landmark into a continent-scale symbol of electrical domination.
mission-soviet-05-objectives = Reach and energize the Eiffel Tower; use the resulting Tesla field to break the city defenses; eliminate organized Allied resistance.
mission-soviet-05-assets = Tesla Troopers, engineers, armored support, and a limited Soviet base are available.
mission-soviet-05-notes = Protect the engineers and power supply. The landmark is useful only after it is successfully energized.

mission-soviet-06-theme = Naval encirclement and division of the Pacific defense line.
mission-soviet-06-history = Allied fleets are using island bases to split Soviet shipping into isolated groups. A concentrated counterattack must destroy the naval screen before it can be reinforced.
mission-soviet-06-objectives = Build naval superiority; destroy the Allied fleet; remove the island bases supporting the blockade.
mission-soviet-06-assets = A developed Soviet base, shipyards, submarines, Dreadnought access, and ground production are available.
mission-soviet-06-notes = Scout for hidden naval units and protect the shipyards. Long-range bombardment still requires screening vessels.

mission-soviet-07-theme = Defense of a mountain research base against Chronosphere raids.
mission-soviet-07-history = Allied displacement experiments now allow strike groups to appear inside supposedly secure territory. A Soviet technology complex in the Urals has become the first target of this new doctrine.
mission-soviet-07-objectives = Keep the research complex intact; defeat successive Chronosphere incursions; destroy or disable the Allied staging operation.
mission-soviet-07-assets = A fortified Soviet base, advanced defenses, armor, and local production are available.
mission-soviet-07-notes = Attacks can bypass the perimeter. Maintain mobile reserves inside the base and keep power coverage redundant.

mission-soviet-08-theme = Return to Washington and destruction of the Allied political symbol.
mission-soviet-08-history = Allied forces have reoccupied the capital and present it as proof that the invasion has failed. Soviet command orders a renewed strike designed to shatter that confidence before it spreads.
mission-soviet-08-objectives = Re-enter Washington; overcome the restored defenses; destroy the White House command site.
mission-soviet-08-assets = A Soviet construction group, veteran armor, infantry, and specialist support are available.
mission-soviet-08-notes = Allied urban defenses are stronger than during the first attack. Preserve siege assets for the central district.

mission-soviet-09-theme = Covert capture of the American president.
mission-soviet-09-history = The Allied coalition is held together by a single public leader. Soviet intelligence plans a quiet operation: control the surrounding security detail and take the president alive before an evacuation can begin.
mission-soviet-09-objectives = Penetrate the protected compound; neutralize or control the guards; capture the president without killing him.
mission-soviet-09-assets = Yuri, psychic infantry, covert personnel, and a tightly limited support force are available.
mission-soviet-09-notes = The target must survive. Avoid indiscriminate weapons near the compound and protect the units capable of mind control.

mission-soviet-10-theme = Temporary battlefield alliance against Yuri's breakaway army.
mission-soviet-10-history = Evidence of a private psychic army forces former enemies into a narrow tactical cooperation. Neither side trusts the other, but allowing the control network to mature would make conventional victory irrelevant.
mission-soviet-10-objectives = Stabilize the joint front; destroy Yuri's control facilities; eliminate the breakaway forces in the region.
mission-soviet-10-assets = A Soviet base group operates alongside limited Allied support and captured technology opportunities.
mission-soviet-10-notes = Friendly factions have separate positions and vulnerabilities. Do not rely on allied units to defend the Soviet production core.

mission-soviet-11-theme = Assault on Yuri's fortified command inside Moscow.
mission-soviet-11-history = Yuri has converted the heart of the Soviet state into a personal stronghold and surrounded it with loyal troops and psychic defenses. Retaking Moscow is now an internal war for control of the entire arsenal.
mission-soviet-11-objectives = Establish a loyalist base; break Yuri's defensive rings; destroy his Kremlin command and remaining forces.
mission-soviet-11-assets = Veteran Soviet armor, elite infantry, advanced production, and strategic weapons are available.
mission-soviet-11-notes = Expect mind control and attacks from several directions. Use expendable screens and long-range fire against psychic defenses.

mission-soviet-12-theme = Final Arctic offensive against the Allied Chronosphere network.
mission-soviet-12-history = The last Allied strategic complex lies on the Alaskan coast, where it can transport armies directly into Soviet territory. A polar assault must cross heavily defended ground before the next displacement wave begins.
mission-soviet-12-objectives = Secure an Arctic beachhead; destroy the Allied bases guarding the coast; eliminate the Chronosphere complex.
mission-soviet-12-assets = A full Soviet invasion force with an MCV, armor, aircraft, naval support, and late-war technology is available.
mission-soviet-12-notes = The enemy can reinforce distant sectors instantly. Protect production and power while advancing on the Chronosphere itself.
campaign-browser-title = Campaign Operations
campaign-browser-map = Operational Area
campaign-browser-intel = Mission Intelligence

## Campaign runtime prompts follow the selected UI language, not imported CSF language.
retail-mission-prefix = Mission
retail-mission-all01a = Objective 1: Destroy the four Soviet Dreadnoughts.
retail-mission-all01b = Objective 2: Reach Fort Bradley.
retail-mission-all01h = Objective 3: Destroy the Soviet supply base.
retail-mission-all01i = Warning: Soviet forces are approaching.
retail-mission-all01q = Battlefield control is online.
retail-mission-all01f = Train an Engineer at the Barracks, then send them into the bridge repair hut.
retail-mission-all01l = SEALs can swim and use demolition charges, like Tanya.
retail-mission-all01m = Transports carry infantry. Use the deploy command to unload passengers.
retail-mission-all01n = Chrono Miners collect ore and teleport back to the refinery.
retail-mission-all01o = Open the infantry production category to see available units.
retail-mission-all01p = Send an Engineer into the bridge repair hut to restore the bridge.
retail-mission-all01s = Use the deploy command on a garrisoned building to evacuate its infantry.
retail-mission-all01t = Use the deploy command to switch GIs between mobile and fortified positions.
retail-mission-all01u = These GIs have deployed their heavy machine guns.
retail-mission-all05g = An Allied patrol has been located.
retail-mission-all05k = Tanya has been killed. A critical unit has been lost.
retail-mission-objective1 = Objective 1 complete.
retail-mission-sov01a = Objective 1: Destroy the Pentagon.
retail-mission-sov1b = Send an Engineer into a bridge repair hut to repair the bridge.
retail-mission-sov1c = Garrison civilian buildings with infantry to establish defensive positions.
retail-mission-sov1e = Your base is low on power. Build more power plants.
retail-mission-sov1g = Capture the airport with an Engineer to gain access to paratroopers.
retail-mission-sov1h = Victory monument, Washington, D.C.
retail-mission-sov11e = You have reached a hidden area.
retail-mission-sov11g = An upgrade crate has been found.
retail-mission-sov11k = Hidden-area marker: TSC & ACC.
retail-mission-sov11f = Hidden-area code: 13567.
notification-system-prefix = Battlefield Control
button-server-information = Server info
label-server-information-empty = No room description has been provided by this server.

population-unlimited = Unlimited
population-player-label = Units per player
population-total-label = Units in room
population-description = Optional unit limits, including queued units. Presets are not device safety guarantees. Unlimited is the default; missions are unaffected.
population-status = Units: { $current } + { $reserved } queued / { $limit }
population-total-status = Room: { $current } + { $reserved } queued / { $limit }
population-player-blocked = Player unit limit reached. Cancel orders or free capacity.
population-total-blocked = Room unit limit reached. Wait for capacity.
population-blocked = Unit limit reached. Production waits for capacity.
population-feedback = { $status }

label-ranked-heading-ready = Ranked Match
label-ranked-heading-queued = Searching
label-ranked-heading-found = Match Found
label-ranked-heading-accepted = Waiting for Opponent
label-ranked-heading-connecting = Connecting

## ranked registration
label-ranked-registration-title = Create Account
label-ranked-confirm-password = Confirm password
button-ranked-register-submit = Confirm Create
button-ranked-return-login = Back to Sign In
label-ranked-registration-hint = Username: 3–24. Password: 12–128. Confirm it below.
label-ranked-registering = Creating your account…
label-ranked-error-username-length = Username must contain 3–24 characters.
label-ranked-error-username-format = Start with a letter. Use letters, Chinese characters, digits or _.
label-ranked-error-password-length = Password must contain 12–128 characters.
label-ranked-error-password-match = The two passwords do not match.
label-ranked-error-username-taken = This username is already in use. Choose another.
label-ranked-error-account-input = Account details were rejected. Check the username and password requirements.
label-ranked-error-rate-limit = Too many attempts. Please wait and try again.
label-ranked-recovery-heading = Account created. Save these recovery codes before continuing.
button-ranked-recovery-continue = Saved — Continue

label-ranked-error-device-after-register = Account created, but device verification failed. Return to sign-in and try logging in.

population-runtime-description = The host can change the room total during play. Existing units remain; production waits when over the new limit.

loadscreen-preparing-maps = Preparing maps
loadscreen-preparing-campaign = Preparing campaign missions

## AI difficulty presets
ai-settings-tab = AI Difficulty
ai-settings-preset = Official and custom presets
ai-settings-official = Select a preset; copy official tiers to customize.
ai-settings-editing = Editing a copy. Save to keep your changes.
ai-settings-bonuses-static = Mining income / production speed
ai-settings-bonuses = Mining: { $income }%    Production: { $speed }%
ai-settings-next-game = Changes apply when selected for a new match. Host controls multiplayer AI.
ai-settings-create = Create a named copy
ai-settings-edit = Edit / rename custom preset
ai-settings-delete = Delete custom preset
ai-settings-delete-title = Delete AI preset
ai-settings-delete-prompt = Delete this saved preset? Existing matches keep their settings.
ai-settings-default = Use as default when filling bot slots
ai-settings-name = Custom preset name
ai-settings-style = Play style
ai-style-balanced = Balanced
ai-style-defensive = Defensive
ai-style-rush = Rush
ai-settings-interval = Attack interval (seconds; requires available troops)
ai-settings-wave = Target troops per attack wave
ai-settings-expansion = Expansion level (1–4)
ai-settings-income = Mining income (%)
ai-settings-speed = Production speed (%)
ai-settings-reset = Restore base tier parameters
ai-settings-save = Save preset
ai-settings-cancel = Cancel editing
ai-settings-invalid-name = Use a unique, non-empty name (maximum 32 presets).
ai-settings-saved = Preset saved. Select it in a bot slot to use it.
ai-settings-copy-name = Copy
ai-difficulty-custom = Custom
ai-difficulty-beginner = Beginner
ai-difficulty-easy = Easy
ai-difficulty-normal = Normal
ai-difficulty-hard = Hard
ai-difficulty-brutal = Brutal
ai-difficulty-expert = Expert
ai-difficulty-master = Master
ai-difficulty-nightmare = Nightmare

ai-settings-custom-status = Custom (based on { $base })
ai-lobby-summary-title = AI configuration
ai-lobby-summary-view = View AI configuration
ai-lobby-summary-close = Close
ai-lobby-summary-body = { $name }
    Base: { $base } · { $style } · Attack interval: { $interval }s
    Wave: { $wave } units · Expansion: { $expansion }/4
    Mining: { $income }% · Production speed: { $speed }%

checkbox-build-tech =
    .label = Build tech facilities
    .description = Complete a Battle Lab to build a Tech Machine Shop, Secret Lab and Tech Power Plant. One of each per player.
