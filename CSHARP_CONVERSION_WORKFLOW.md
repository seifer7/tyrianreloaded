# C# conversion workflow

This document describes the order and evidence required for the port. It is a
development workflow, not an automated conversion script. No conversion is
performed by adding this document.

## 1. Establish the baseline

- Run `python -B tests/native/check_conversion_inventory.py` after inventory
  changes. Both CI platforms check native definitions, added file coverage and
  summary counts. The lexical audit ignores comments/string literals, includes
  pointer-return signatures and records conditional definitions once per file.
  An audit restored 27 existing definitions omitted from the initial tracker;
  all 766 native functions must receive an explicit verified disposition.
- Build the current native game and data extractor using the supported
  Makefile and Visual Studio paths where the required toolchains are
  available.
- Record the runtime environment, required SDL libraries, launch arguments,
  and externally supplied Tyrian data files.
- Capture reproducible behavior for startup, menus, representative gameplay,
  input, audio/video, saving/loading, data extraction, and network play.
- Record known platform-specific behavior and existing limitations; do not
  treat a pre-existing limitation as a regression or silently expand scope.
- The Windows conversion environment has .NET SDK 10 installed and Visual
  Studio 18 Community with MSVC outside PATH. Asset-free native RNG/parser
  reference tests have been compiled using its `vcvars64.bat`.
  The installed SDL 2/SDL2_net development dependencies also support the full
  unchanged native Release x64 game build, verified with MSVC's `v145` toolset
  override. The network-enabled executable's captured `--help` output and
  exit status pass in an isolated external artifact directory. Original
  runtime assets and whole-game behavioral captures are still required.
  Keep both build/intermediate outputs outside the repository. On this
  Windows environment, run `msbuild visualc\opentyrian.vcxproj` after
  `vcvars64.bat` with `/p:Configuration=Release /p:Platform=x64`
  `/p:PlatformToolset=v145 /p:WITH_NETWORK=true` and external
  `/p:ProjectOutputDirectory=...`/`/p:ProjectIntermediateDirectory=...`
  paths. Default SDL path properties locate the already-installed SDKs;
  other environments should pass their own `SDL2BaseDir`/`SDL2netBaseDir`.

## 2. Agree the C# foundation before porting

- Use .NET 10, `OpenTyrian.slnx`, and the projects under `src-csharp/` and
  `tests/`. Keep the initial foundation free of NuGet dependencies.
- Bind the platform layer to the existing SDL 2 and SDL2_net native
  libraries using C# interop; do not replace the SDL-based platform behavior.
- Use the dependency-free core regression harness under `tests/` until a
  test framework is justified and selected.
- The game executable still scaffolds its entrypoint. The standalone data
  extractor is functional and compared against native C on synthetic complete
  item-data fixtures and separately supplied Tyrian 2.1 assets; all eight
  real-asset category outputs matched byte-for-byte.
- Build with `dotnet build OpenTyrian.slnx` and run the regression harness
  with `dotnet run --project tests/OpenTyrian.Core.Tests`.
- Verify the solution builds on each intended development platform before
  translating platform-dependent behavior.

## 3. Port in dependency order

Credits playback is library-integrated and verified over 24,483 native frames,
including scrolling completion and its held final frame. Generate
`tests/native/build_credits_reference.py` with `--output` outside the repository,
compile the generated C with `src/mtrand.c` and the `src` include directory,
then pass the executable to `--native-credits`. Synthetic encrypted text and
concrete sprite/font/palette/file/input/PIT services are also exercised.
Playable-host credits wiring and real-asset release checks remain pending.

High-score entry is library-integrated with 512 native qualification/edit/
presentation scenarios, including signed-score boundaries and exact tie/cancel
behavior. Generate `tests/native/build_high_scores_reference.py`, compile its
output with `src/mtrand.c`, the `src` include directory and `-lm` on Linux,
then use `--native-high-scores`. Native shifts copy scores/names but not
difficulty, and cancellation preserves those prior shifts; both behaviors
are intentionally retained. The concrete adapter preserves the captured
cursor button and shared legacy mouse-position words rather than substituting
current SDL mouse coordinates. Playable-host integration remains pending.

Player ship initialization and optional edited-ship archives are verified with
`--native-ship-info` and `--native-edited-ships`. Generate
`tests/native/build_ship_info_reference.py` and
`tests/native/build_edited_ships_reference.py` into external artifact files and
compile them as standalone C references. The managed loader retains the
native ushort size truncation and zero-size read rejection, but publishes
state only after successful reads/checksums. It keeps the 150 defined record
bytes, not the four uninitialized bytes copied from native decrypt scratch.
The selected sheets and graphic array compose existing player-frame rendering.

The complete in-game keyboard dispatcher is verified across 4,096 native
mode/chord scenarios and concrete gamma, ship/combat HUD, help/text and PIT
service bindings. Generate `tests/native/build_game_keyboard_reference.py`,
compile its output with the `src` include directory and `src/mtrand.c`, then
use `--native-game-keyboard`. Both player selection passes share key state,
so the first pass consumes number keys before the second; native armor and
speed-byte narrowing and chord precedence are retained. Enclosing playable
level-loop integration remains pending.

In-game settings are verified over 2,048 native draw/input/action/wait
scenarios, 16 complete menu loops and concrete managed services.
Generate `tests/native/build_ingame_setup_reference.py` into an external
artifact and compile it as standalone C, then use `--native-ingame-setup`.
Exact slider endpoints, half-open row bounds, sample/mute ordering and the
native ushort comparison of previous mouse coordinates are retained.
The local/network settings wrapper is verified across 64 native flag
combinations, concrete primary-surface waiting pixels and actual SDL2_net
two-peer resume/quit synchronization. Generate the standalone native wrapper
with `tests/native/build_game_setup_reference.py` and use
`--native-game-setup`. Repeated presentations during packet polling are
coalesced in wrapper comparisons; packet primitives and actual peer
synchronization are separately verified. Error/cancellation restores relative
mouse mode and clears the local request. The enclosing playable loop remains
pending.

High-score page presentation and interaction are verified over 256 complete
native scripts, every byte difficulty rank, signed score boundaries and
actual SDL2 event/video/palette services. Generate the standalone oracle
with `tests/native/build_high_score_screen_reference.py` and pass
`--native-high-score-screen` to the managed harness. Preserve the native
three-episode limit, keyboard wrap versus non-wrapping mouse paging,
unbounded lower control hit-box Y, mouse precedence over simultaneous keys
and unsorted archive ownership. The final title-menu caller remains pending.

New-game modes are verified against 2,048 normal, 1,792 super-arcade and 256
SuperTyrian native starts plus actual managed selection/render/palette/audio/
input adapters. Generate `tests/native/build_new_game_reference.py`, compile
with `src/mtrand.c` and `-lm` on Linux, and use `--native-new-game`.
Cancellation preserves the native early ship assignment and normal-mode
cash/difficulty side effects; do not make these flows transactional. Rich
mode applies even after cancelled episode/difficulty selection, while normal
initial cash requires the shared loaded flag. Final title/game-loop wiring
remains pending.

Title drawing, waits, input, dispatch, secret codes and composition are
verified against fourteen draw traces, 4,096 inputs, 28 actions, eight wait
scenarios, 136 secret cases and 144 complete native loops. Generate
`tests/native/build_title_screen_reference.py` as standalone C and use
`--native-title-screen`. Actual SDL2 integration traverses animated title,
score viewer, title restart and normal new-game selections. Retain the strict
30-second timeout, unsigned elapsed ticks, ushort previous mouse coordinates,
independent click/hover positions, non-overlapping secret matching and
native action/secret precedence. Final startup and remaining child-flow
implementations still gate the playable executable.

Startup logos and SuperTyrian screen bleed are verified using
`tests/native/build_game_intro_reference.py` and `--native-game-intro`.
The standalone oracle checks 1,264 frames at native/padded pitches and six
exit timings, including full final bytes of primary and both scratch
buffers. Preserve in-place neighbor updates, scratch swap ordering and the
native asymmetric copy length (size minus bottom border, not both borders).
Managed geometry/alias validation replaces raw-pointer assertions.
Actual SDL integration traverses logo fades, input waits and screen bleed
before the animated title/high-score/new-game path.

The saved-game load screen is covered by
`tests/native/build_load_screen_reference.py` and `--native-load-screen`:
960 drawing states, 8,192 inputs, 7,680 actions, eight waits and 160 full
loops. Retain sparse-page selection, separate hover/click coordinates,
mouse/key precedence, truncated labels and the native load-result behavior
(return the existing game-loaded flag rather than the loader result).
Actual SDL tests traverse title -> load -> next page -> persisted versioned
save, verifying catalog enumeration, loaded player/level/episode state and
perform-save clearing. Managed list lifetime replaces explicit native free.

Help topic menus and all eight pages are verified by
`tests/native/build_help_system_reference.py` and `--native-help-system`.
The oracle covers 144 page scripts and 304 complete menu scripts, including
160 row-edge/independent hover-and-click/simultaneous key snapshots.
Preserve topic-boundary returns, directional cursors, footer text, original
page layouts and brightness/line-height restoration before fades/input.
Actual SDL coverage traverses title -> help -> all eight pages -> menu ->
title exit with the expected music, palette and help-style state.

Setup menus are verified using `tests/native/build_setup_menu_reference.py`
and `--native-setup-menu`. The standalone fixture retains all sixteen native
functions and the volume helper, covering 16 catalogs, 256 draws, 16,384
menu/picker inputs, 1,024 picker actions, 156 activations, 2,816 menu actions,
eight waits and 64 complete loops. Retain the native picker click-Y/X-limit
quirk and volume/toggle sample ordering. Failed scaler selection must restore
the previous scaler or report an explicit failure. SDL texture failures
already preserve the old texture/scaler and propagate platform diagnostics.
Real SDL coverage traverses title/setup, fullscreen/window and scaler/layout
changes, audio toggles/volume, jukebox, shop settings/save and title return;
reload the persisted configuration to verify active and stored settings.

Christmas detection and snow prompts are verified by
`tests/native/build_christmas_prompt_reference.py` and
`--native-christmas-prompt`: twelve local-month cases, 300 complete prompt
scripts and twelve long 350-frame waits that exercise snowflake respawn.
Keep the original fixed-point snow accumulator/layering and the distinct
drawn-choice versus clickable-choice positions. The oracle explicitly
injects the same reproducible random service on both sides; do not treat
that as platform C-runtime `rand()` verification. Final startup must supply
the platform-compatible random stream, seasonal asset fallback and mode
ownership without replacing `rand()` with the gameplay Mersenne Twister.

Network startup is verified using `tests/native/build_network_start_reference.py`
and `--native-network-start` over 112 controller/peer flows, signed difficulty/
wire truncation and canceled selectors. The fixture accepts the managed
Silver Ship index as its sole argument to avoid assuming item-table layout.
Real SDL2_net tests compose the existing connection handshake with start-screen
details and peer initialization, and exercise both canceled episode and canceled
difficulty selection. Preserve native initial-difficulty-before-increment
ordering and final acknowledgment synchronization; do not clear unrelated
mode/game-loaded/input-device flags during this screen.

Warning text composition uses `tests/native/build_warning_text_reference.py`
and `--native-warning-text` across 2,816 line/prompt/warning/escape/halt/skip
traces. Concrete host checks compose the existing character glow, warning bars,
fonts, input and PIT services; actual SDL checks cover mouse continuation and
Return/Escape animation skips. Preserve the native `wasAnimated` snapshot
*after* the warning lines, the unconditional prompt call and do/while wait.
Escape key input is an ordinary skip button; it does not automatically mutate
the separate legacy escape flag. Native text synchronization is entirely
disabled and remains a no-op, not an invented network exchange.

Game shutdown is verified with the standalone
`tests/native/build_game_shutdown_reference.py` and `--native-game-shutdown`
over all 256 byte exit codes and both farewell flags. Real SDL teardown checks
run in isolated managed child processes so global `SDL_Quit` cannot invalidate
the regression runner's other live windows. They verify device disposal,
zero initialized subsystems, cleared shapes/sounds, configuration reload,
code-9 skip-save, failed-save cleanup and warning-text halt composition.
Managed cleanup failures propagate explicitly after mandatory platform/SDL
cleanup; no successful exit is synthesized on failure. The final executable
must catch `GameTerminationException` and return its exit code rather than
reporting it as a crash. The concrete shutdown host assumes sole application
ownership of SDL; do not use it for a single menu or borrowed video device.

Shared text tokens use `tests/native/build_text_tokens_reference.py` and
`--native-text-tokens`. Preserve C whitespace for decimal numbers but only
space/tab separators for references, NUL termination, prefix consumption and
native `strtol` saturation before narrowing to int. Windows uses 32-bit long;
Linux/macOS use 64-bit long and retain two's-complement int narrowing.
The 192 native scripts compare repeated reads and byte-capacity errors.
Typed item arrays/references reuse the reader; reference capacity is a byte
limit, including for UTF-8 mod identifiers, not a managed character count.

Level input uses `tests/native/build_level_input_reference.py` and
`--native-level-input` over 32,768 complete native dispatch snapshots.
Keyboard/joystick producers set `PausePressed`/`InGameMenuPressed`; only the
dispatcher turns those latches into network requests or local modal calls.
Preserve keyboard-help early return, game-over input clearing, demo interruption,
focus-loss pause and local settings presentation skip. Actual SDL tests compose
the existing keyboard/pause/settings implementations. The dummy video backend
cannot enable relative mouse mode while focused, so modal exit fixtures send
focus loss before return; logical relative-mode restoration remains verified.

Next-level preparation is checked against the unchanged `prepare_next_level`
routine using `tests/native/build_level_preparation_reference.py` and
`--native-level-preparation`. Its 49,152 traces cover every combination of
Galaga/two-player state, demo flags/file ownership, player survival, normal and
bonus levels, end-level and save-recovery flags, across all six fast-play
settings. Preserve relative-mouse/keyboard/sprite cleanup order, normal-speed
reset, demo close/stop/wait behavior, old-difficulty restoration, bonus/game-over
routing, and the deliberate distinction between a normal level transition and
bonus-level level-number restoration after loading a recovery save.

The in-level HUD routine is compared to the unchanged `draw_level_hud` through
`tests/native/build_level_hud_reference.py` and `--native-level-hud`. Its 4,096
traces verify end-level early returns, message-bar expiry, Galaga shield/reward
and two-player normalization, byte-sized shield regeneration timing, mutable
weapon-bar values and ordered rectangles, power-bar arithmetic, and final map
offset/enemy state. Preserve unsigned power wrap before the 900 cap and the
Galaga reward threshold's unsigned narrowing.

The level-background composition routine is checked against unchanged
`draw_level_backgrounds` with `tests/native/build_level_background_scene_reference.py`
and `--native-level-background-scene`. Its 4,096 traces exercise map-delay and
wrap arithmetic, layer order/priority/blending, starfield and filter ordering,
enemy strata and spawn selection, wild-background darkening, sound selection,
and screen/map/enemy state. Rendering, filters, enemy drawing/spawning and RNG
remain explicit host operations so the frame orchestration can be integrated
with the game loop without obscuring their ordering.

Armor warnings use `tests/native/build_armor_warning_reference.py` compiled
with `src/mtrand.c` and the `src` include path, and `--native-armor-warning`.
The 16,384 traces cover byte color arithmetic, sound cadence, allocation/RNG,
multiplayer ownership and Galaga. Preserve the first low-armor player's
warning intensity even when only player two receives a local network warning.
The host composes real enemy creation, item-ID resolution, velocity narrowing
and warning rectangle/font rendering; full-pool creation does not consume
drop RNG and still resets the 500-frame rescue delay.

Level presentation uses `tests/native/build_level_presentation_reference.py`
and `--native-level-presentation`. Its 2,048 cases cover padded/asymmetric
surfaces, aliased buffers, inversion, searchlight coordinates, PIT timing,
processor gating and skipped frames. The searchlight deliberately preserves
the native primary-pitch source stride, unlike normal/inverted copies.
`LevelPresentation` shares the keyboard/setup skip/quit flags directly; real
SDL presentation and invalid-viewport/failed-present propagation are checked.

Level debug/timer overlays use `tests/native/build_level_timer_reference.py`
(link `-lm` on Unix) and `--native-level-timer`. The 16,384 multi-frame traces
cover native float FPS rounding/formatting and signed high ticks, pickup flashes,
timer warnings/clicks/expiration, and Pentium frame-count alternation. Concrete
checks cover real event jumps, fonts/SDL, invariant-culture output, failure
propagation and shared frame-count delivery to the presentation PIT timer.
Do not duplicate keyboard debug histories or pickup display-time state.

Level player/display initialization is compared with the native function by
`tests/native/build_level_player_init_reference.py` and
`--native-level-player-init`. The 512 cases capture player trail histories,
last-shot positions, alive flags, old/current demo difficulty and clamping,
plus two-player picture selection and display/gamma/fade/lazy-shape ordering.
Its concrete host uses the shared indexed font/palette and the corrected
palette across the native 50-step fade. Preserve sequential mutations if a
later presentation or asset-loading action fails.

Level map/ship-state initialization is verified against the unchanged
`initialize_level_map_and_ship_state` body using
`tests/native/build_level_map_ship_init_reference.py` and
`--native-level-map-ship-init`. The 1,024 cases compare row-relative map
pointers at each native layer stride, retained map X indices, background
positions and delays, music fade, shot power, starfield speed, and both player
velocity/invulnerability resets. Current and third-layer player-frame camera
accumulators are reset while previous and secondary accumulators remain
untouched. Keep map loading/application separate from this reset routine and
preserve the loaded tile arrays.

Level-entry flags are compared against the unchanged
`initialize_level_flags` body by `tests/native/build_level_flags_reference.py`
and `--native-level-flags`. Its 16,384 cases cover all reset fields, signed and
unsigned state boundaries, shared quit-request ownership, button clearing and
boss-link resets while preserving boss armor/color. Keep these reset fields on
their existing gameplay, input, renderer, event, warning and player-frame
owners rather than introducing duplicate global state.

Level status initialization is compared with the unchanged
`initialize_level_status_display` body through
`tests/native/build_level_status_display_reference.py` and
`--native-level-status`. The 256 startup traces cover byte-narrowed shield
recharge timing, both players' shield state, armor clamping, superbomb/cube/
flash/notice resets, and text/song/port-button host-action order. Preserve the
native `levelSong - 1` conversion to an unsigned song index, including the
zero-byte wraparound, and reuse the existing combat, player, pickup, timer and
save-state owners.

Episode picture display is compared with the unchanged
`show_episode_picture` routine through
`tests/native/build_episode_picture_reference.py` and
`--native-episode-picture`. Its 256 cases preserve the constant-play early
return, C-style decimal-prefix parsing, PCX palette selection above picture
900, PCX-versus-archive loading for other values, and clear/present/fade order.
Keep the native PCX-palette index mapping and do not access it for unsupported
picture identifiers.

Episode warning loading is compared with the unchanged
`show_episode_warning` routine through
`tests/native/build_episode_warning_reference.py` and
`--native-episode-warning`. Its 256 cases preserve both early-return gates,
warning-state initialization, native decimal parsing and 16-bit narrowing,
decrypted Pascal warning lines, and display/key-clear order. Keep the warning
text presenter behind its existing host boundary and reject inputs that would
overrun the native fixed-size line buffers.

Episode completion and super-arcade unlock presentation are compared with the
unchanged `show_episode_completion` routine through
`tests/native/build_episode_completion_reference.py` and
`--native-episode-completion`. Its 48 cases verify encrypted hint-section
selection, score/cash warning text, line counts and stream position, early
returns, SuperTyrian difficulty mapping, unlock text and ship graphics,
presentation/fade/input order, and network synchronization. Keep special-name
and ship tables supplied through the completion data object, and preserve
native table indexing rather than translating the legacy one-based-looking
values as managed indices.

Conditional episode-level jumps are compared with the unchanged
`handle_episode_level_jump` routine through
`tests/native/build_episode_level_jump_reference.py` and
`--native-episode-level-jump`. The 2,048 cases cover all six conditional jump
codes, unknown-code preservation, the `H`-specific argument offset, native
decimal-prefix parsing, byte narrowing, and the original state when a gate
fails.

Episode mode configuration is compared with the unchanged
`configure_episode_mode` routine through
`tests/native/build_episode_mode_reference.py` and `--native-episode-mode`.
The 256 seeded traces cover Galaga loadout copying (including independent item
arrays), alternate-ship reset, extra-game and SuperTyrian flags, and all
modified player item/cash fields.

Episode shop availability is compared with the unchanged
`read_episode_item_availability` routine through
`tests/native/build_episode_item_availability_reference.py` and
`--native-episode-item-availability`. The 15 traces cover encrypted Pascal
rows, all shop-setting gates, typed item lookup and aliases, the ten-item row
limit, state clearing, stream consumption, and item-screen handoff. Random
offers remain supplied by the existing typed item database helper, with an
additional deterministic managed integration case confirming that generated
offers update the row contents and count.

The episode map transition is compared with the unchanged
`play_map_transition` routine through
`tests/native/build_map_transition_reference.py` and `--native-map-transition`.
Sixteen complete traces compare every presented frame and final pixel buffers
for upper, lower, horizontal and fallback reveals, two surface pitches,
constant-play early exit, key skipping, picture loading and event/timing order.

Episode presentation dispatch is compared with the unchanged
`handle_episode_presentation` routine through
`tests/native/build_episode_presentation_reference.py` and
`--native-episode-presentation`. Forty cases verify command routing to picture,
map-transition, warning and completion services; network fade gates; clear,
present and palette ordering; completion return propagation; and unknown
command behavior.

Combat-state initialization is compared with the unchanged
`initialize_level_combat_state` body through
`tests/native/build_level_combat_state_reference.py` and
`--native-level-combat-state`. The 256 traces cover the gameplay-owned combat,
player/enemy projectile, enemy payload, effects, special-weapon, level, and
background reset groups; network-only enemy availability; Galaga difficulty;
the option-draw callback; and all three map wrap boundaries. Keep enemy
availability separate from enemy payload clearing, and route reset state
through its existing subsystem owners.

Keep each change small enough to compare with the C baseline. Convert the
corresponding C functions and update their checkboxes as behavior is verified.
Pair each `.c` implementation with its declarations and related header inline
functions.

1. **Core and data foundations:** shared types, configuration, file/path
   handling, command-line parsing, random number behavior, data structures,
   and the standalone data extractor.
2. **Platform services:** timing, keyboard/mouse/joystick input, video,
   scaling, palettes, sprites/fonts, and audio/music/sound effects.
3. **Game systems:** level/episode loading, player and enemy state, projectiles,
   collisions, effects, scoring, and network synchronization.
4. **User-facing flows:** title and episode selection, menus, in-game UI,
   save/load screens, help, credits, and special modes.
5. **Entrypoints and delivery:** game startup/shutdown, command-line behavior,
   data extraction invocation, project/build packaging, and supported
   platform integration.

The ordering is a guide, not permission to defer a dependency: follow actual
callers and data ownership when a module depends on another subsystem.
Retain the native implementation until each replacement is buildable and
covered by parity evidence.

The initial core conversion is the native Mersenne Twister in
`src/mtrand.c`: preserve the 32-bit output sequence, default seed, reseeding,
and float ranges before using it in gameplay.

For native differential tests, compile `tests/native/core_reference.c` with
`src/arg_parse.c`, `src/mtrand.c`, and `src/config_file.c`, adding `src` to the
include search path.
On Linux, for example:

```sh
cc -std=c99 -I src tests/native/core_reference.c src/arg_parse.c src/mtrand.c src/config_file.c -o /tmp/core_reference
dotnet run --project tests/OpenTyrian.Core.Tests -- --native-reference /tmp/core_reference
```

On Windows use `cl /TC /I src` in an environment initialized by Visual
Studio's `vcvars64.bat`, then pass the resulting executable's absolute path
to `--native-reference`. Keep temporary executables and objects out of source
control.

For extractor parity, `tests/native/build_extractor_reference.py` generates
an SDL-free oracle using unchanged native extractor, file-helper, header, and
legacy-reader source regions. Only SDL integer types/endian constants and
no-op shutdown are supplied; this does not verify SDL or full-game integration.
The generator currently supports little-endian hosts. On Linux:

```sh
python3 tests/native/build_extractor_reference.py --output /tmp/extractor_reference.c
cc -std=c99 -Wall -Wextra /tmp/extractor_reference.c -o /tmp/extractor_reference
dotnet run --project tests/OpenTyrian.Core.Tests -- --native-extractor /tmp/extractor_reference
```

On Windows generate the source outside the repository and compile it with
`cl /TC /D_CRT_SECURE_NO_WARNINGS` after initializing `vcvars64.bat`.
The harness compares all eight output files byte-for-byte for 1,785 synthetic
records and checks native CLI status/output. Managed output additionally
removes temporary files on truncated input and never overwrites a category
created concurrently; native process exits or rename semantics can differ in
these failure cases. Existing unreadable category files are also preserved
rather than treated as absent.

Background and item-definition parity use analogous source-region generators:

```sh
python3 tests/native/build_background_reference.py --output /tmp/background_reference.c
cc -std=c99 -Wall -Wextra -I src /tmp/background_reference.c src/mtrand.c -o /tmp/background_reference
python3 tests/native/build_items_reference.py --output /tmp/items_reference.c
cc -std=c99 -Wall -Wextra -I src /tmp/items_reference.c src/mtrand.c -o /tmp/items_reference
dotnet run --project tests/OpenTyrian.Core.Tests -- --native-background /tmp/background_reference --native-items /tmp/items_reference
python3 tests/native/build_help_reference.py --output /tmp/help_reference.c
cc -std=c99 -Wall -Wextra /tmp/help_reference.c -o /tmp/help_reference
dotnet run --project tests/OpenTyrian.Core.Tests -- --native-help /tmp/help_reference
python3 -B tests/native/build_shots_reference.py --output /tmp/shots_reference.c
cc -std=c99 -Wall -Wextra -I src /tmp/shots_reference.c src/mtrand.c -lm -o /tmp/shots_reference
dotnet run --project tests/OpenTyrian.Core.Tests -- --native-shots /tmp/shots_reference
python3 -B tests/native/build_effects_reference.py --output /tmp/effects_reference.c
cc -std=c99 -Wall -Wextra -I src /tmp/effects_reference.c src/mtrand.c -lm -o /tmp/effects_reference
python3 -B tests/native/build_combat_reference.py --output /tmp/combat_reference.c
cc -std=c99 -Wall -Wextra -I src /tmp/combat_reference.c src/mtrand.c -lm -o /tmp/combat_reference
dotnet run --project tests/OpenTyrian.Core.Tests -- --native-effects /tmp/effects_reference --native-combat /tmp/combat_reference
python3 -B tests/native/build_enemy_reference.py --output /tmp/enemy_reference.c
cc -std=c99 -Wall -Wextra -I src /tmp/enemy_reference.c src/mtrand.c -lm -o /tmp/enemy_reference
dotnet run --project tests/OpenTyrian.Core.Tests -- --native-enemies /tmp/enemy_reference
python3 -B tests/native/build_enemy_attacks_reference.py --output /tmp/attacks_reference.c
cc -std=c99 -Wall -Wextra -I src /tmp/attacks_reference.c src/mtrand.c -lm -o /tmp/attacks_reference
dotnet run --project tests/OpenTyrian.Core.Tests -- --native-attacks /tmp/attacks_reference
python3 -B tests/native/build_enemy_events_reference.py --output /tmp/events_reference.c
cc -std=c99 -Wall -Wextra -I src /tmp/events_reference.c src/mtrand.c -lm -o /tmp/events_reference
python3 -B tests/native/build_level_events_reference.py --output /tmp/dispatch_reference.c
cc -std=c99 -Wall -Wextra -I src /tmp/dispatch_reference.c src/mtrand.c -lm -o /tmp/dispatch_reference
dotnet run --project tests/OpenTyrian.Core.Tests -- --native-events /tmp/events_reference --native-dispatch /tmp/dispatch_reference
python3 -B tests/native/build_level_map_reference.py --output /tmp/level_map_reference.c
cc -std=c99 -Wall -Wextra /tmp/level_map_reference.c -o /tmp/level_map_reference
dotnet run --project tests/OpenTyrian.Core.Tests -- --native-level-map /tmp/level_map_reference
python3 -B tests/native/build_boss_bar_reference.py --output /tmp/boss_bar_reference.c
cc -std=c99 -Wall -Wextra -I src /tmp/boss_bar_reference.c src/mtrand.c -lm -o /tmp/boss_bar_reference
python3 -B tests/native/build_level_state_reference.py --output /tmp/level_state_reference.c
cc -std=c99 -Wall -Wextra -I src /tmp/level_state_reference.c src/mtrand.c -lm -o /tmp/level_state_reference
dotnet run --project tests/OpenTyrian.Core.Tests -- --native-boss-bars /tmp/boss_bar_reference --native-level-state /tmp/level_state_reference
python3 -B tests/native/build_sound_bank_reference.py --output /tmp/sound_bank_reference.c
cc -std=c99 -Wall -Wextra /tmp/sound_bank_reference.c -o /tmp/sound_bank_reference
dotnet run --project tests/OpenTyrian.Core.Tests -- --native-sounds /tmp/sound_bank_reference
python3 -B tests/native/build_audio_mixer_reference.py --output /tmp/audio_mixer_reference.c
cc -std=c99 -Wall -Wextra /tmp/audio_mixer_reference.c -lm -o /tmp/audio_mixer_reference
python3 -B tests/native/build_music_reference.py --output /tmp/music_reference.c
cc -std=c99 -Wall -Wextra /tmp/music_reference.c -o /tmp/music_reference
python3 -B tests/native/build_lds_reference.py --output /tmp/lds_reference.c
cc -std=c99 -Wall -Wextra /tmp/lds_reference.c -o /tmp/lds_reference
dotnet run --project tests/OpenTyrian.Core.Tests -- --native-mixer /tmp/audio_mixer_reference --native-music /tmp/music_reference --native-lds /tmp/lds_reference
python3 -B tests/native/build_input_reference.py --output /tmp/input_reference.c
cc -std=c99 -Wall -Wextra /tmp/input_reference.c -o /tmp/input_reference
python3 -B tests/native/build_mouse_reference.py --output /tmp/mouse_reference.c
cc -std=c99 -Wall -Wextra /tmp/mouse_reference.c -o /tmp/mouse_reference
dotnet run --project tests/OpenTyrian.Core.Tests -- --native-input /tmp/input_reference --native-mouse /tmp/mouse_reference
```

Background comparisons include darkening, lava, water, iced/ordinary blur,
hue/brightness filtering, tile clipping/blending, and seeded starfields across
two pitches. The item oracle compares parsed records, ordering, diagnostics,
every typed gameplay field, required indices, forward/late references, and
seeded shop selection, including fixed-width boundaries and >255 slots.
The help oracle captures shaded lines to compare character-count wrapping,
including native narrow-word truncation and byte-counter behavior. Native
infinite-loop and uninitialized-buffer cases are rejected with explicit errors
rather than reproduced.
The projectile oracle records complete slot state and ordered rendering/effect
calls for creation, gameplay movement, preview simulation, and directional
updates. Explosions/superpixels are captured at the call boundary, not simulated
by this oracle. The effects oracle additionally compares explosion/particle
state, sprite order, indexed pixel hashes, and subsequent RNG state. Player
damage/HUD parity covers 8,640 shield/armor/damage/mode cases across two pitches.
The enemy oracle compares creation/scaling, hit boxes, linked marking and damage
animation, pickup spawning, rewards/flags/jumps, and integrated projectile
collisions against full native state/effect/RNG traces. It retains negative
random-start divisors and stale fields on reused slots. The native reserved
Zinglon stripe does not initialize its kill-credit player number; the managed
implementation assigns its rewards to player one rather than reading undefined
stack memory. Native differential stripe traces cover nonlethal hits; managed
tests additionally cover stripe kill credit.
Motion checks additionally cover seeded acceleration, oscillation, position/
bounce/visibility, animation and ordered single/four-tile draw calls over 256
frames, and 1,000 aimed velocity calculations. Invalid native graphics-array
indices and unrepresentable float-to-word velocities throw explicit errors.
The enemy-event oracle covers spawning, jump/return cursors, last-match searches,
Galaga search effects, scripted enemy controls, and random death drops. The full
dispatcher oracle compares 336 cases across every event type, all gameplay state,
host-action ordering, warnings, and subsequent RNG. Shape/text/music callbacks
are recorded; managed checks additionally exercise actual synthetic shape
loading, unloading, and rendering. Native out-of-range event arrays, missing
item IDs, invalid text/flag/smoothie slots, and a random-link search that can loop
forever are rejected with explicit data errors. Map-wrap positions are tracked
as flat indices; whole-game scroll/wrap scheduling remains pending.
The binary-level oracle covers six full levels including zero/maximum event and
enemy counts, signed field boundaries, selected archive offsets, big-endian
shape IDs within otherwise little-endian files, blank/excluded/duplicate shape
slots, tile pixel hashes/fill flags, and every map entry in all three layers.
Managed checks additionally render loaded tiles and reject truncation, oversized
lists/maps, and undefined references instead of reading native uninitialized
pointers. The native end-event sentinel is represented by a bounds-aware event
array and the `LevelMap.SentinelTime` constant; it is not dispatched as gameplay.
Boss-bar parity compares 512 linked-health/placement/flash cases and ordered
shading calls; managed checks additionally exercise pitched surfaces and event
links. Level-end parity covers all 1,024 combinations of scrolling, end flags,
enemy presence, explosion gates, and timers. Sound dispatch compares 76 queues
including first-game-over gating, all channels and sample volume rules; playback
is an explicit callback, not yet SDL audio. Invalid IDs throw, and a failed
playback callback leaves its sound pending instead of reporting success.
The bank-loader oracle compares six synthetic normal/Christmas banks, all
38 sample indices, signed byte input, zero/maximum-size samples, and native
100-byte voice-tail trimming. Conversion is an explicitly required platform
service. A recording deterministic converter in the oracle verifies the exact
conversion inputs, output counts/ownership, and playback routing; it does not
claim SDL resampling or device parity. Managed read/conversion failures propagate
and retain the previous complete bank instead of publishing partial or zeroed
samples. SDL conversion and OPL2 FM waveform synthesis now have separate
native-parity coverage; conditional OPL3 output and hardware compatibility
remain conversion/release gates.
The mixer oracle verifies exact signed-16 PCM output across 256 flag/volume/
channel combinations, five sample rates, and six buffer sizes; it covers
fractional 69.5 Hz LDS timing, clipping, channel replacement/consumption, and
disabled/stopped gates. A deterministic synthesizer isolates mixing from FM
waveform generation. Music-control parity compares four archives and 192
selection/stop/restart/fade transitions; invalid selections and load failures
leave music stopped and throw rather than replaying a stale track after a warning.
The full LDS oracle compares 24 songs, 9,216 update frames, 1,536 direct notes,
all packed commands, signed transpose, delayed notes, fades/jumps/loops,
instrument effects, and ordered OPL initialization/cache/register writes.
The OPL2 renderer is separately compared with native PCM vectors for FM and
additive voices, tremolo, feedback, multiple channels, block boundaries, and
deterministic percussion. `SdlAudioDevice.TryInitializeOpl` creates the OPL2
chip at the SDL-negotiated sample rate and runs an LDS fixture through the
actual dummy SDL callback; percussion uses an injected platform random source.
Conditional OPL3 four-operator output remains unimplemented. Invalid LDS
indices/truncation throw instead of accessing native memory; managed teardown
also stops playback.
Input parity compares 512 viewport/coordinate calculations and 1,792 state
snapshots across 128 queued event/wait scenarios, including exact event-return
ordering, Alt+Enter, focus-dependent relative mode and accumulator resets,
cursor visibility, UTF-8 byte buffers, native key-release quirks, joystick
keyboard/release polling, and network servicing. `InputState` integrates with
the managed PIT timer. Required SDL/joystick/network host callbacks remain
pending; quit requests return to a host responsible for shutdown/unwinding
active waits rather than directly calling native process exit.
Mouse parity compares 2,560 normal/filter/inactive/disabled cursor starts and
restores across all five shapes, 64 edge/offscreen positions, and two pitches.
It uses the unchanged real native clipped sprite renderer and synthetic
compressed shapes, and checks both drawn and restored pixel buffers.
The native filtered-start no-poll behavior and 24x28 capture's scanline
crossing are preserved. No runtime game assets are used.

Real SDL2 audio conversion now implements the sound-bank conversion boundary;
192 byte-hash/length comparisons cover six device rates, zero through 65535
input samples, signed extrema, silence, and alternating/ramp input. Native CVT
ABI assertions validate the managed prefix offsets and buffer storage sizes.
The same check exercises all 38 bank entries through the mixer and actual
SDL dummy-driver callbacks (paused creation, start/pause/resume/close, rooted
delegate under GC, callback/factory failure propagation and device reopening).
Keep physical audio off during this check:

```sh
cc -std=c99 -Wall -Wextra $(sdl2-config --cflags) tests/native/sdl_audio_reference.c $(sdl2-config --libs) -o /tmp/sdl_audio_reference
SDL_AUDIODRIVER=dummy dotnet run --project tests/OpenTyrian.Core.Tests -- --native-sdl-audio /tmp/sdl_audio_reference
```

On Windows, compile with the installed SDL2 include/lib paths and `SDL2.lib`,
keep `SDL2.dll` beside the oracle, set `$env:SDL_AUDIODRIVER='dummy'`, and use
`SDL2_LIBRARY_PATH` to point the managed process at that same absolute DLL.
The device is deliberately created paused so the host can load music/samples
first. Managed callback exceptions cannot cross SDL's native thread: they
silence output and retain their exception/stack for `ThrowIfFaulted` and
start/pause/dispose. A game host must check faults every frame and stop/close
the device before releasing LDS/OPL state. OPL2 sample synthesis and startup
are covered; physical hardware compatibility, conditional OPL3 output, and
complete game-host audio lifetime remain release gates.
To exercise the managed device lifecycle and initialization policy without
building the native oracle, set `$env:SDL_AUDIODRIVER='dummy'` and run
`dotnet run --project tests/OpenTyrian.Core.Tests -- --test-sdl-audio-device`.

The game entrypoint now runs the complete native option parser behavior before
the explicit unplayable-game exit. The option oracle compares 51 argument lists,
complete launch/network state, permuted/uppercased legacy arguments, exact help/
diagnostics, and exit codes. Numeric prefixes match native `atoi`/`sscanf`;
undefined native signed-overflow inputs instead produce the relevant option
diagnostic. Options are owned per parse and do not mutate caller arguments.

```sh
python3 -B tests/native/build_game_options_reference.py --output /tmp/game_options_reference.c
cc -std=c99 -Wall -Wextra -I src /tmp/game_options_reference.c src/arg_parse.c -o /tmp/game_options_reference
dotnet run --project tests/OpenTyrian.Core.Tests -- --native-game-options /tmp/game_options_reference
python3 -B tests/native/build_sdl_video_reference.py --output /tmp/sdl_video_reference.c
cc -std=c99 -Wall -Wextra $(sdl2-config --cflags) /tmp/sdl_video_reference.c $(sdl2-config --libs) -o /tmp/sdl_video_reference
cc -std=c99 -Wall -Wextra $(sdl2-config --cflags) tests/native/sdl_input_reference.c $(sdl2-config --libs) -o /tmp/sdl_input_reference
SDL_VIDEODRIVER=dummy SDL_RENDER_DRIVER=software dotnet run --project tests/OpenTyrian.Core.Tests -- --native-sdl-video /tmp/sdl_video_reference --native-sdl-input /tmp/sdl_input_reference
```

Real SDL video parity compares 48 framebuffer/window/viewport/fullscreen states
against unchanged native video and all nearest/Scale/HQ scaler functions.
All four layout modes, factors 1..4, minimum resize sizes, display
clamping, fullscreen toggle and indexed clear pass. Managed surfaces replace software SDL
surfaces; RGB888 palette mapping is managed and verified through framebuffer
bytes. Native handle/subsystem cleanup is deterministic and reopen is tested.
Real SDL input parity verifies 640 queued native-union events and actual managed
key/text state, resize and Alt+Enter fullscreen effects, quit callbacks, SDL
timing, and required joystick/network services. Ignored SDL-owned drop/extended
text pointers are freed rather than leaked. SDL video/event pumping follows
SDL's main-thread requirement. Physical display/input/audio/controllers,
conditional OPL3 synthesis, network gameplay integration and whole-game flows
remain release gates.

Joystick conversion now covers all native module functions. Its scripted
oracle compares 22 code/partial-scan cases, 484 assignment comparisons, 640
defaults, 4,608 state snapshots, 1,536 analog/reduction results, 32 exact native
configuration outputs, and eight assignment-capture scenarios. Integer state,
repeat wrap/strict comparison, key order and configuration are exact; angle
rounding allows 1e-6 radians between platform math libraries. Undefined native
overflow/divide-by-zero/code-number inputs throw; valid negative thresholds
and native six-character code truncation are preserved.

```sh
python3 -B tests/native/build_joystick_reference.py --output /tmp/joystick_reference.c
cc -std=c99 -Wall -Wextra -I src /tmp/joystick_reference.c src/config_file.c -lm -o /tmp/joystick_reference
dotnet run --project tests/OpenTyrian.Core.Tests -- --native-joystick /tmp/joystick_reference
```

The `--native-sdl-input` group also attaches a real SDL virtual joystick and
verifies enumeration/capabilities, axes/buttons/hats, menu events, config
save/reopen, ignored-input gates, teardown and post-close guards. No original
assets or physical-controller actions are required. SDL subsystem-init errors
throw instead of silently disabling requested joystick support; individual
open failures produce warnings and neutral indexed state. Host platform
integration is functional, but physical-controller checks and playable-game
use remain release gates.
Selection menu parity compares 120 gameplay/episode/difficulty scenarios with
unchanged native functions: complete draw arguments/frame hashes, navigation,
wrapping, mouse bounds/priority, disabled entries, cancellation, Shift-G/Shift-]
and held/sequential LORD unlocks, native difficulty values and ordered sounds,
fades, waits, keep-alives and state commits. The SDL input group also runs the
concrete managed presentation with real event queues, live SDL modifiers,
PIT-timed palette fades and renderer output. Menu entrypoints accept required
host services rather than substituting unported networking or episode loading.
Whole title/new-game callers remain pending.

Shadowed-gauge parity compares 256 complete surface/warning traces, including
pitch, edges, unsigned zero-dimension wrap, color truncation and the native
`12 / resolution * partial` division order. Zero resolution throws because
native division by zero is undefined. Sixteen button cases verify joystick
poll/event ordering, held mouse versus new mouse, new keys and the native
keyboard-clear no-op.

```sh
python3 -B tests/native/build_selection_menus_reference.py --output /tmp/selection_menus_reference.c
cc -std=c99 -Wall -Wextra /tmp/selection_menus_reference.c -o /tmp/selection_menus_reference
python3 -B tests/native/build_ui_controls_reference.py --output /tmp/ui_controls_reference.c
cc -std=c99 -Wall -Wextra /tmp/ui_controls_reference.c -o /tmp/ui_controls_reference
dotnet run --project tests/OpenTyrian.Core.Tests -- --native-selection-menus /tmp/selection_menus_reference --native-ui-controls /tmp/ui_controls_reference
```

Legacy save parity covers 32 encrypted/decoded 2,502-byte buffers, 256 twelve-
byte item mappings, 16 complete 22-slot archives, 12 C-string XOR cases and 176
game-state save/load cases across every slot and eight modes. Required save
and episode host callbacks are exercised, including real temporary-file
persistence. Save/load retains fixed-width signedness, platform-width unsigned
cash conversion, raw fourteen-byte names, Pascal strings, old sidekick
normalization and completed-episode transitions.

The legacy editor-level writer stores high then low bytes while the reader
interprets low then high; preserve this observed quirk. The four weak native
checksums are preserved, including a verified collision. Invalid checksums
produce exact native diagnostics and throw instead of terminating the process.
Decryption does not reproduce native uninitialized stack bytes beyond the
payload. Malformed Pascal lengths, truncated files and names exceeding their
native fields throw instead of reproducing out-of-bounds memory writes.
The legacy archive codec extracts verified serialization regions from the
configuration functions. Complete configuration composition is now verified
separately; newer `.tyrsav` game-state save/load has its own reference gate below.

Processor-detail and speed parity adds 432 states and exact native PIT target
comparisons. Unknown processor/game-speed values preserve defined native
defaults/prior fast-play values; invalid fast-play throws instead of the
native assertion/fall-through.

```sh
python3 -B tests/native/build_legacy_saves_reference.py --output /tmp/legacy_saves_reference.c
cc -std=c99 -Wall -Wextra /tmp/legacy_saves_reference.c -o /tmp/legacy_saves_reference
python3 -B tests/native/build_runtime_settings_reference.py --output /tmp/runtime_settings_reference.c
cc -std=c99 -Wall -Wextra /tmp/runtime_settings_reference.c -o /tmp/runtime_settings_reference
dotnet run --project tests/OpenTyrian.Core.Tests -- --native-legacy-saves /tmp/legacy_saves_reference --native-runtime-settings /tmp/runtime_settings_reference
```

Text configuration parity adds 32 complete parsed-state/serialized-text cases
linked to the unchanged native configuration implementation and actual SDL
keyboard-name APIs. Unknown keys/settings, all scaler/layout names, retained
joystick sections and unknown-binding removal preserve native behavior.
Real file checks cover missing defaults, roundtrip persistence, durable flush
and locked-file/invalid-directory errors. Missing config paths are diagnosed
and return false; other I/O failures propagate instead of silently returning
false. Unix-created directories use user read/write/execute permissions.
All stored HQ choices now render through the verified managed kernels.
Complete legacy configuration parity compares eight startup/save scenarios
against the unchanged full native functions: exact 28-byte DOS fields, missing/
invalid-size defaults, both existing and generated save archives, seeded
high-score/name RNG ordering, processor background overrides and bounded volume
callbacks. Real-file roundtrips and locked-file failures are covered. Invalid
binary input is disposed rather than retaining the native leaked stream;
save failures throw rather than returning success after silently skipped writes.
User-directory resolution caches the native Windows/Unix environment precedence.
Paths exceeding the native 499-byte UTF-8 buffer throw instead of truncating.

New-format metadata parity uses real files and the unchanged native reader/
list builder with actual tinydir sorting: six version/mode/name headers and four
player filters. Managed checks reject every truncated header and diagnose skipped
corrupt/unsupported files. Unlike native empty-metadata success on unsupported
versions, those files are explicitly rejected. Latin-1 basenames are required;
separator/NUL filenames are rejected instead of allowing traversal or undefined
string reads. Native path behavior is deliberately retained: lookup uses the
literal `saves\` prefix and 59-byte path limit, while listing uses `./saves`.
These existing paths disagree on Unix and with some non-current user directories;
fixing that baseline bug requires a documented compatibility decision before
whole-game integration. Managed streams are closed instead of leaking native
metadata handles.

Versioned save parity compares 57 complete file bytes and restored game/player
states across 64 fixtures, preserving current/previous equipment, byte powers,
mode flags, cube counts, level names, completed-episode transitions and loaded
flags. The other seven cases trigger the native writer's XOR/strlen defect:
the terminal marker encrypts to NUL, so native output silently loses its marker.
Managed writing rejects those cases before changing an existing file. IDs that
introduce earlier cipher NULs are rejected for the same reason. Full native
loader code is unchanged in the oracle, but its allocator fixture appends one
zero byte because native allocates an unterminated string and reads beyond it;
these checks do not validate undefined heap reads. Managed parsing is bounded
to the native 1023-byte payload, player count, ten-character fields and terminal
marker. Missing item IDs and signed-int numeric overflow throw instead of
invalid indices, native buffer overruns or undefined atoi overflow. Malformed
loads are validated before mutating live state, and unsupported versions are not
reported as successful empty loads. Real-file persistence, metadata integration
and locked files are covered; the eventual save/menu host must report failures.

```sh
python3 -B tests/native/build_game_config_reference.py --output /tmp/game_config_reference.c
cc -std=c99 -Wall -Wextra -I src $(sdl2-config --cflags) /tmp/game_config_reference.c src/config_file.c $(sdl2-config --libs) -o /tmp/game_config_reference
dotnet run --project tests/OpenTyrian.Core.Tests -- --native-game-config /tmp/game_config_reference
python3 -B tests/native/build_settings_store_reference.py --output /tmp/settings_store_reference.c
cc -std=c99 -Wall -Wextra -I src /tmp/settings_store_reference.c src/mtrand.c -o /tmp/settings_store_reference
python3 -B tests/native/build_save_metadata_reference.py --output /tmp/save_metadata_reference.c
cc -std=c99 -Wall -Wextra -I src /tmp/save_metadata_reference.c -o /tmp/save_metadata_reference
dotnet run --project tests/OpenTyrian.Core.Tests -- --native-settings-store /tmp/settings_store_reference --native-save-metadata /tmp/save_metadata_reference
python3 -B tests/native/build_versioned_saves_reference.py --output /tmp/versioned_saves_reference.c
cc -std=c99 -Wall -Wextra /tmp/versioned_saves_reference.c -o /tmp/versioned_saves_reference
dotnet run --project tests/OpenTyrian.Core.Tests -- --native-versioned-saves /tmp/versioned_saves_reference
```
The attack oracle adds 192 enemy update frames, 16 hostile shot boundary cases,
90 weapon-special cases, and 128 aimed-shot cases, comparing full enemy/shot/
explosion/player-velocity/sound/draw/RNG traces. It records player damage at the
call boundary; separate damage/HUD parity and a real combat integration test
cover that dependency. Missing turret shot IDs throw rather than reusing stale
native lookup scratch state. Level events and whole-game callers remain pending.
SDL surfaces/events and whole-game behavior remain outside these oracle checks.

Animation playback compares 72 complete native traces using a two-page synthetic
LPF file, six starting positions, four speeds and three input-interruption
positions. Every palette color, frame hash and ordered delay/presentation/event/
network/wait call is compared, excluding the final first-frame loop delta.
Managed tests additionally verify exact PIT timing, single-record animations,
owned/borrowed streams, missing/malformed pages and failed rendering/presentation.
Those failures propagate instead of silently aborting playback. A real SDL
integration check presents decoded frames and palettes, posts a keyboard event
after the third frame and verifies immediate interruption before keep-alive/wait.
Whole-game ending callers and original-asset playback remain release gates.

```sh
python3 -B tests/native/build_animation_reference.py --output /tmp/animation_reference.c
cc -std=c99 -Wall -Wextra /tmp/animation_reference.c -o /tmp/animation_reference
dotnet run --project tests/OpenTyrian.Core.Tests -- --native-animation /tmp/animation_reference
```

Jukebox starfield parity covers all 15 patterns, 21 complete scenes, 840
rendered frames and 10,080 direct star generations, comparing full star/pixel
hashes, exact float bits, signed/byte/word wrapping, controls and RNG state.
The complete jukebox oracle compares 32 music/sample/control/fade/quit traces
and the original 41 music/38 sound titles. Managed checks additionally exercise
concrete palette/font/star presentation, music archive/controller/PCM/sample
routing, disabled audio, and volume restoration after failed presentation.
Real dummy SDL integration drives keyboard exit, 16 presented/mixed frames,
PIT waits and the final black palette. Native star initialization is persistent
across jukebox runs; the empty `JE_wackyCol` stub has no managed counterpart.
Whole-game title-menu wiring, conditional OPL3 waveform output and
original-asset presentation remain outstanding.

```sh
python3 -B tests/native/build_jukebox_stars_reference.py --output /tmp/jukebox_stars_reference.c
cc -std=c99 -Wall -Wextra -I src /tmp/jukebox_stars_reference.c src/mtrand.c -lm -o /tmp/jukebox_stars_reference
python3 -B tests/native/build_jukebox_reference.py --output /tmp/jukebox_reference.c
cc -std=c99 -Wall -Wextra -I src /tmp/jukebox_reference.c src/mtrand.c -o /tmp/jukebox_reference
dotnet run --project tests/OpenTyrian.Core.Tests -- --native-jukebox-stars /tmp/jukebox_stars_reference --native-jukebox /tmp/jukebox_reference
```

## 4. Verify parity continuously

Network parity compares nine complete acknowledged-control/retry/state/XOR
traces across delays 1..7, including packet loss, exact recovered payloads,
retransmission, resets and 65,542 control sequence transitions. A further
32 unchanged native connect traces cover duplicates, version/delay/player
conflicts, episode intersections, 20-byte player-name truncation and Escape.
Actual SDL2_net tests compare 128 loopback datagrams and echoes byte-for-byte,
then connect two concurrent peers and exchange acknowledged controls and delayed
states. Ports are ephemeral and peers are strictly local; no remote network or
whole-game multiplayer parity is claimed.
Native promoted sync subtraction and reconstructed XOR packet headers are
preserved. Malformed packet sizes/names throw before queue publication; UDP
failures propagate rather than returning a success-shaped result or spinning.
Waits support cancellation. Disconnects/negotiation errors raise typed termination
exceptions for the host, replacing native process exit. The shutdown continuation
now preserves error normalization, picture 2, centered small-font text, three
ten-step fades, optional sync/timeout, nonzero-error button confirmation and
transport release before game halt code 5. Thirty-two unchanged native traces
cover every message, invalid-code fallback, timeout, already-synced and actual
acknowledgment paths; 32 request-mask cases preserve the peer-menu flag.
Actual SDL frames, picture decoding, font pixels, palette endpoints, keyboard
acknowledgment and actual SDL2_net disposal are exercised. Cancellation/rendering
errors release the transport in a finally block and propagate without reporting
a normal halt. Shared gameplay globals and main/menu/gameplay synchronization
callers remain pending.
The gameplay network update now composes the actual session and level/combat/
player/item state, with 5,120 complete native traces and paired SDL2_net gameplay
frames. All local/remote request pairs use the native XOR rule; equal simultaneous
requests cancel. Menu ownership, halt, skip countdown and NortShip item changes
are retained. Diagnostics deliberately preserve native overlapping coordinate
offsets rather than silently correcting them. A location mismatch throws before
request actions instead of relying on a debug-only assertion. Setup exceptions
clear peer-menu context and propagate. The top-level loop and menu implementation
remain pending.
Pause parity covers twelve complete native traces: normal/super pause, Ctrl/
Alt modifier filtering, mouse confirmation, network handshake and remote resume.
The real SDL path checks pause-font pixels and queued input; paired SDL2_net
peers exercise acknowledged pause/resume. PIT targets preserve the native float
calculation. Failures/cancellation restore volume and relative mouse state;
input release waits support cancellation with unchanged normal service ordering.

Player lifecycle parity covers 8,192 native death/respawn/continuous-death cases,
including exact HUD pixels, RNG, explosions, sounds and arcade/Galaga rules.
The player movement oracle compares 4,096 full linking/inertia/history/warp/
attachment traces with actual typed items and native projectile creation.
It also covers front/rear weapons, literal-index superbombs, charge weapons,
sidekick firing, limited/unlimited ammo refill, animation and charge timers.
Charge/ammo HUD pixels are hashed across the complete frame using unchanged
native rectangle/gauge routines; sprite calls and external effects are traced.
Native unsigned charge-timer underflow is preserved. Invalid managed
power/mode/charge/animation/ammo values throw explicit errors rather than
accessing outside native tables. The top-level player loop remains pending.
Mode-switching parity additionally compares the request/readiness latch,
rear/special multiposition resets, normal configuration wraparound, all seven
arcade special pairs through native ID lookup, and single/two-player pattern
indicator sprite calls. The enclosing loop still owns readiness-latch release.
Ship parity adds 2,048 unchanged native traces spanning clamping/banking,
network Y bounds, shadows/super-wild outlines, invulnerability, DragonWing
sidekick positions and actual NortSpark projectiles at raw shot index 671.
RNG consumption follows the supported native toolchains' Y-before-X argument
evaluation. The concrete compressed-sheet adapter checks copy/blend/shadow pixels
and preserves padded rows; no original assets are used.
Special-command recognition compares 42,432 complete progress/execution states
across all 26 keyboard combos, normal/SuperTyrian ship maps, both players,
release/diagonal/repeat/wrong-direction input, and ignored higher ship indices.
Native normal ship index 14 has no table row; managed code reports it explicitly.
The linking API now invokes the real recognizer even when movement is disabled.
Special-weapon parity adds 12,288 complete traces: standalone completion,
per-frame updates and full native-ordered attack integration. It compares
shield/all/half/armor costs and affordability, repulsor/attractor velocity
changes including signed overflow, all flare/filter/astral/Zinglon/spray/
invulnerability/repair/sidekick effects, held fire, waits/cooldowns and RNG.
Actual native/managed projectiles include raw shot 707; full main/HUD hashes
include padded rows. Sidekick spawning runs real native initialization, icons,
gauges and option-level indicators rather than a no-op callback. Managed
concrete sprite adapters check readiness, icons and rear-pattern buttons.
Enclosing individual and paired player frames are now library-integrated.
Another 4,096 individual scenarios and 4,096 paired scenarios (two consecutive
updates each) compare unchanged native player/death/command/movement/render/
attack routines, full projectiles, HUD/frame hashes and RNG. They cover respawn
retry and mode changes, shared special coordinates, demo text, local/remote
delayed network input, reset gates, previous-map/parallax state and signed warp
overflow. The frame oracle supplies bounded keyboard/demo input and network
packet fixtures; actual native input and network queues are independently
compared by their existing selectors. Frame explosion requests are deferred
for state comparison; explosion animation remains covered by the lifecycle
oracle. Asset-dependent sprites use traced calls and separately verified
concrete adapters. Full level/game-loop and playable-host integration is pending.
Player input collection compares 16,384 complete native traces with actual
joystick polling/axis reduction/angles and native mouse distance scaling.
It verifies input-device selection, local/remote network ownership, keyboard/
mouse/analog/digital controls, short narrowing, acceleration/clamping, inversion,
menu/pause requests and exact prepared packet bytes/reset. Demo recording checks
big-endian wait/key records and ushort wait wrap. Actual binary key playback
adds 8,192 native replay frames and 8,192 collector-integrated replay frames,
checking zero-wait records, countdown, all key/button bits, partial/EOF state,
stream positions and EOF/level-end ordering. Managed reads also exercise
fragmented streams. Demo headers/loading and recording are library-integrated;
the full level/game loop remains pending. Invalid bindings/local owner/buttons/prepared
state and missing replay services produce explicit errors.

`--native-demo` compares 15 load attempts and 64 recording headers/filenames
against unchanged native demo routines and numeric item-ID lookup. It covers
original and compact headers, demo number byte wrap, missing-next fallback,
missing-first failure, episode initialization, optional/partial wait bytes,
high-bit/NUL names, all item fields and input resets. Managed checks exercise
actual episode archive initialization, every mandatory header truncation,
invalid item IDs and exhausted recording slots. Missing files use the standard
open warning; new recordings use atomic creation to preserve concurrently
created files, and all 256 occupied slots raise an explicit error instead of
the native infinite search. Streams are owned/disposed by `DemoSession`, then
supplied to `PlayerFrame` for actual input playback/recording.

```sh
python3 -B tests/native/build_demo_reference.py --output /tmp/demo_reference.c
cc -std=c99 -Wall -Wextra -I src /tmp/demo_reference.c src/mtrand.c -o /tmp/demo_reference
dotnet run --project tests/OpenTyrian.Core.Tests -- --native-demo /tmp/demo_reference
```

`--native-game-hud` compares 4,096 unchanged native HUD/cube rendering traces:
cash, special icons, lives (including unsigned underflow and signed display),
local/network names and text widths, superbombs and cheating text. Managed
tests also exercise actual compressed sprites, option shapes and font shadows.

```sh
python3 -B tests/native/build_game_hud_reference.py --output /tmp/game_hud_reference.c
cc -std=c99 -Wall -Wextra /tmp/game_hud_reference.c -o /tmp/game_hud_reference
dotnet run --project tests/OpenTyrian.Core.Tests -- --native-game-hud /tmp/game_hud_reference
```

`--native-text-glow` compares 2,048 consecutive glow calls: shadow ordering,
all three fonts, initial/pressed-button skipping, ascending/descending
brightness, warning hue, empty text, network keep-alive and delay/presentation
ordering. Actual managed font/input/PIT integration is also exercised.

```sh
python3 -B tests/native/build_text_glow_reference.py --output /tmp/text_glow_reference.c
cc -std=c99 -Wall -Wextra /tmp/text_glow_reference.c -o /tmp/text_glow_reference
dotnet run --project tests/OpenTyrian.Core.Tests -- --native-text-glow /tmp/text_glow_reference
```

`--native-game-text` compares 32,768 timed warning updates and 40 text-window
updates, including signed color-change wrapping, full stripe pixel hashes,
sound cadence, erase ordering, lifetime and Latin-1/NUL text. Concrete window
font/option-shape pixels are also exercised.

```sh
python3 -B tests/native/build_game_text_reference.py --output /tmp/game_text_reference.c
cc -std=c99 -Wall -Wextra /tmp/game_text_reference.c -o /tmp/game_text_reference
dotnet run --project tests/OpenTyrian.Core.Tests -- --native-game-text /tmp/game_text_reference
```

`--native-game-help` compares 32 single/two-player help scenarios: exact panel/
sprite/help-box/text layout and repeated keyboard/mouse/cursor/joystick/network
wait ordering. Managed help/font/shape pixels and real input/timing integration
are also checked. Explicit primary-surface ownership replaces temporary global
screen-pointer switching.

```sh
python3 -B tests/native/build_game_help_reference.py --output /tmp/game_help_reference.c
cc -std=c99 -Wall -Wextra /tmp/game_help_reference.c -o /tmp/game_help_reference
dotnet run --project tests/OpenTyrian.Core.Tests -- --native-game-help /tmp/game_help_reference
```

`--native-level-results` compares 512 end-level flows with actual item
progression/difficulty: unlock bytes, snapshots, completed/exiting/bonus
messages, cash/kill percentages, editor-level wrapping, cube cap and animation,
skip/continue waits and audio/fade order. Real managed glow/font/palette/input/
PIT services are also exercised. Unlock offsets overlapping regenerated
checksum bytes are retained as transient scratch, not persisted as item data.

```sh
python3 -B tests/native/build_level_results_reference.py --output /tmp/level_results_reference.c
cc -std=c99 -Wall -Wextra -I src /tmp/level_results_reference.c src/mtrand.c -lm -o /tmp/level_results_reference
dotnet run --project tests/OpenTyrian.Core.Tests -- --native-level-results /tmp/level_results_reference
```

`--native-episode-transition` compares 1,024 next-episode transitions:
availability/wrap/repeated-game flags, credits/high-score gates, exact RNG
and SuperCarrot/BananaBlast rewards, snapshots and title/audio/input/fade
ordering. Real synthetic episode archives and concrete palette/font/input
services are exercised. Credits and high-score entry are explicit callbacks;
their playback implementations and full host integration remain pending.

```sh
python3 -B tests/native/build_episode_transition_reference.py --output /tmp/episode_transition_reference.c
cc -std=c99 -Wall -Wextra -I src /tmp/episode_transition_reference.c src/mtrand.c -lm -o /tmp/episode_transition_reference
dotnet run --project tests/OpenTyrian.Core.Tests -- --native-episode-transition /tmp/episode_transition_reference
```

`--native-character-glow` compares 1,024 character-wave scenarios: width and
16-bit position handling, glyph/shadow order, hue/brightness, missing/Latin-1
glyphs, skip/escape, warnings, network and waits. Concrete managed input/font/
PIT/warning integration verifies the delay already scheduled before a skip
button, full pixels and warning sound-delay state. Text beyond the native
60-character arrays is rejected explicitly.

```sh
python3 -B tests/native/build_character_glow_reference.py --output /tmp/character_glow_reference.c
cc -std=c99 -Wall -Wextra /tmp/character_glow_reference.c -o /tmp/character_glow_reference
dotnet run --project tests/OpenTyrian.Core.Tests -- --native-character-glow /tmp/character_glow_reference
```

`--native-game-settings` checks all byte gamma levels/channel values, 1,024
F11/palette states and 512 selected/full high-score sorting cases. It verifies
real managed palette publication without modifying the source picture palette.

```sh
python3 -B tests/native/build_game_settings_reference.py --output /tmp/game_settings_reference.c
cc -std=c99 -Wall -Wextra /tmp/game_settings_reference.c -lm -o /tmp/game_settings_reference
dotnet run --project tests/OpenTyrian.Core.Tests -- --native-game-settings /tmp/game_settings_reference
```

`--native-player-progression` compares 512 new-game/shop/score cases, 72 signed
upgrade arithmetic cases and 147,456 threshold-driven difficulty evaluations.
It preserves native-width long/unsigned long arithmetic, snapshots before power
reset, player-two defaults, real known-item lookup, RNG and nondecreasing
difficulty. Undefined native zero-power loops and out-of-range score conversion
are explicit managed errors, not silent approximations.

```sh
python3 -B tests/native/build_player_progression_reference.py --output /tmp/player_progression_reference.c
cc -std=c99 -Wall -Wextra -I src /tmp/player_progression_reference.c src/mtrand.c -lm -o /tmp/player_progression_reference
dotnet run --project tests/OpenTyrian.Core.Tests -- --native-player-progression /tmp/player_progression_reference
```

`--native-enemy-collision` compares 8,192 player/enemy collision damage,
linked destruction and armor pickup scenarios at pitches 320/327. It uses
unchanged native player damage, HUD and explosion setup, comparing both
players, all 100 enemies, 200 explosions, 20 repeating explosions, sounds,
HUD hashes and RNG. Coverage includes armor 255, capped damage, signed
velocity impulses, collision-specific link rules, ground/large effects,
pickup rounding and armor caps. Pickup dispatch and the enclosing player
collision loop are now covered by `--native-pickups`, which compares 8,192
direct pickup and complete collision dispatch scenarios. It preserves player
name selection, text/song/options ordering, item-ID versus raw-name lookup,
arcade tables, sidekick tiers, unsigned negative linked cash rewards, exact
projectile-slot clearing, actual combat/HUD/explosion state and RNG.

```sh
python3 -B tests/native/build_pickups_reference.py --output /tmp/pickups_reference.c
cc -std=c99 -Wall -Wextra -I src /tmp/pickups_reference.c src/mtrand.c -lm -o /tmp/pickups_reference
dotnet run --project tests/OpenTyrian.Core.Tests -- --native-pickups /tmp/pickups_reference
```

```sh
python3 -B tests/native/build_enemy_collision_reference.py --output /tmp/enemy_collision_reference.c
cc -std=c99 -Wall -Wextra -I src /tmp/enemy_collision_reference.c src/mtrand.c -lm -o /tmp/enemy_collision_reference
dotnet run --project tests/OpenTyrian.Core.Tests -- --native-enemy-collision /tmp/enemy_collision_reference
```

```sh
python3 -B tests/native/build_network_reference.py --output /tmp/network_reference.c
cc -std=c99 -Wall -Wextra /tmp/network_reference.c -o /tmp/network_reference
cc -std=c99 -Wall -Wextra $(sdl2-config --cflags) tests/native/sdl_net_reference.c $(sdl2-config --libs) -lSDL2_net -o /tmp/sdl_net_reference
dotnet run --project tests/OpenTyrian.Core.Tests -- --native-network /tmp/network_reference --native-sdl-net /tmp/sdl_net_reference
python3 -B tests/native/build_level_network_reference.py --output /tmp/level_network_reference.c
cc -std=c99 -Wall -Wextra /tmp/level_network_reference.c -o /tmp/level_network_reference
dotnet run --project tests/OpenTyrian.Core.Tests -- --native-level-network /tmp/level_network_reference
python3 -B tests/native/build_pause_reference.py --output /tmp/pause_reference.c
cc -std=c99 -Wall -Wextra /tmp/pause_reference.c -o /tmp/pause_reference
dotnet run --project tests/OpenTyrian.Core.Tests -- --native-pause /tmp/pause_reference
python3 -B tests/native/build_player_lifecycle_reference.py --output /tmp/player_lifecycle_reference.c
cc -std=c99 -Wall -Wextra -I src /tmp/player_lifecycle_reference.c src/mtrand.c -lm -o /tmp/player_lifecycle_reference
dotnet run --project tests/OpenTyrian.Core.Tests -- --native-player-lifecycle /tmp/player_lifecycle_reference
python3 -B tests/native/build_player_movement_reference.py --output /tmp/player_movement_reference.c
cc -std=c99 -Wall -Wextra -I src /tmp/player_movement_reference.c src/mtrand.c -lm -o /tmp/player_movement_reference
dotnet run --project tests/OpenTyrian.Core.Tests -- --native-player-movement /tmp/player_movement_reference
python3 -B tests/native/build_player_special_reference.py --output /tmp/player_special_reference.c
cc -std=c99 -Wall -Wextra -I src /tmp/player_special_reference.c src/mtrand.c -lm -o /tmp/player_special_reference
dotnet run --project tests/OpenTyrian.Core.Tests -- --native-player-special /tmp/player_special_reference
python3 -B tests/native/build_player_input_reference.py --output /tmp/player_input_reference.c
cc -std=c99 -Wall -Wextra -I src /tmp/player_input_reference.c src/config_file.c -lm -o /tmp/player_input_reference
dotnet run --project tests/OpenTyrian.Core.Tests -- --native-player-input /tmp/player_input_reference
```

HQ scaler parity compares every output byte in 12,372 synthetic cases,
including all 256 pattern seeds, varied color/threshold relationships and
unsigned interpolation overflow, 1-pixel boundaries, source/destination padding,
and complete 320x200 frames. Native HQ vertical neighbors intentionally use
visible width rather than source pitch; this pre-existing behavior is preserved.
The real SDL video oracle now compiles the complete unchanged nearest/Scale/HQ
modules, comparing 48 rendered viewport/resize/fullscreen frames across all nine
scalers. Managed pixel recipes are generated as readable switch statements,
with strict grammar/case validation and reproducibility checks; no native game
code is required by the managed executable.

```sh
python3 -B tools/generate_hq_kernels.py --check --output src-csharp/OpenTyrian.Core/HqPatternKernels.cs
python3 -B tests/native/build_hq_scaler_reference.py --output /tmp/hq_scaler_reference.c
cc -std=c99 -w /tmp/hq_scaler_reference.c -o /tmp/hq_scaler_reference
dotnet run --project tests/OpenTyrian.Core.Tests -- --native-hq /tmp/hq_scaler_reference
```

- Add focused tests for converted functions and data formats, especially
  integer-width behavior, binary parsing, save/config files, ordering rules,
  and malformed-input errors.
- Compare deterministic outputs and state transitions against the native
  implementation. For visual, audio, timing, and multiplayer behavior, use
  reproducible scenarios and document any unavoidable tolerance.
- Exercise representative full-game paths on each supported platform and
  confirm the game still uses the original external asset layout.
- For each completed function, mark it in `CONVERSION_PROGRESS.md` only after
  its implementation and relevant verification pass.

## 5. Completion gates

The conversion is not complete until all of the following are true:

- The C# game and supporting tools build from a clean checkout using the
  documented commands, without requiring the native game executable.
- Automated tests pass, and documented manual parity scenarios have been
  exercised on the supported platforms.
- Every baseline file has a verified converted/replaced disposition or an
  explicit, reviewed reason to remain unchanged; all 766 baseline function
  entries are accounted for.
- Runtime assets remain external and are neither required in source control
  nor redistributed by the build.
- Documentation, CI, release packaging, and the progress summary describe
  the delivered C# build accurately.

Only after these gates pass should native code and native-only build support
be considered for removal.
