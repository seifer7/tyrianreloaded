# OpenTyrian C# Conversion

This branch is the preparation point for converting the complete OpenTyrian
game and its supporting tools from C to C#. The intended end state is a
buildable, playable C# version with behavior compatible with the current game.

**The C# conversion is in progress.** The existing C implementation remains
the baseline and compatibility reference. Migration work is tracked in
[`CONVERSION_PROGRESS.md`](CONVERSION_PROGRESS.md).

## Migration files

- [`AGENTS.md`](AGENTS.md) gives coding agents the repository-specific rules
  for the conversion.
- [`CSHARP_CONVERSION_WORKFLOW.md`](CSHARP_CONVERSION_WORKFLOW.md) defines
  the staged workflow and completion gates.
- [`CONVERSION_PROGRESS.md`](CONVERSION_PROGRESS.md) tracks every file present
  at the start of this effort and every C function implementation, including
  inline functions in headers.

## Compatibility goal

The C# build must preserve the current game's supported gameplay, menus,
controls, graphics, sound, save/configuration behavior, data loading and
extraction, command-line behavior, and network multiplayer. Treat the C
implementation and its existing build as the reference until equivalent C#
behavior has been demonstrated. Conversion is complete only when the game and
supporting tools build and the tracked native implementation has an explicit
converted, replaced, or intentionally retained disposition.

The game expects the original Tyrian 2.1 freeware data files at runtime. Those
assets are not tracked in this repository and must not be added as part of the
conversion.

## Existing baseline

The game sources are under `src/`. The repository currently builds the native
game with the root `Makefile` or the Visual Studio solution in `visualc/`;
both use SDL 2, with SDL2_net enabled for network multiplayer. Preserve these
capabilities in the C# design, and document any intentional implementation
differences before replacing the native baseline.

## C# foundation

The C# projects target .NET 10, with no NuGet dependencies in the initial
foundation. `OpenTyrian.slnx` contains the game host, core library, data
extractor host, and dependency-free core regression harness. The game
executable now parses native game options and provides compatible help/errors,
but explicitly reports that playable game flows have not yet been ported.
The data extractor is functional, with native byte-output parity verified on
synthetic asset records. The platform layer now binds real SDL 2 audio,
video, event and joystick services through the installed native library.
SDL2_net transport, control/state queues and connection negotiation are now
library-integrated; gameplay/menu synchronization remains pending.
No package dependency or native game logic
has been added to the managed build.

Build the solution and run the core regression harness with:

```powershell
dotnet build OpenTyrian.slnx
dotnet run --project tests/OpenTyrian.Core.Tests
```

Current core ports cover the RNG, bounded buffers, argument parsing,
configuration, binary I/O, encrypted strings, animation containers/decoding,
indexed raster drawing, palettes/fades, PCX/picture loading, nearest and
Scale2x/Scale3x scaling, player upgrade rules, sprite loading/blitting, and
font metrics/rendering, level archives/episode selection, binary level headers,
events, shape banks and tile maps, background layers
and filters, starfields, encrypted help archives, PIT timing, volume adjustment,
and typed item loading/mod ordering, reference resolution, and random shop
selection, native-compatible help-box wrapping/rendering, and player projectile
creation/movement/preview simulation with sprite/effect integration. Additional
ports include explosion/repeating explosion and superpixel scene systems,
player shield/armor damage and death state, HUD rendering, enemy allocation and
difficulty scaling, enemy movement/animation/aiming, and projectile collisions with linked enemy damage,
death pickups, rewards, and event flags, enemy attack/child-launch scheduling,
and hostile projectile collisions with player damage, enemy-event spawning and
controls, and full level-event dispatch with explicit shape/text/music host
actions, linked boss health bars, level-completion gates, and queued-sound
dispatch. Level-entry initialization also sets shield/HUD/armor state, clears
pickup/cube/flash display state, starts the level song, and initializes port
buttons through explicit host actions. Its combat-state reset also clears
player/enemy projectiles, combat/effect/special-weapon state and level flags,
preserves the network-only enemy-availability reset and Galaga difficulty
behavior, and restores all three map wrap boundaries. The in-level HUD now
preserves shield regeneration cadence, Galaga rewards,
weapon-bar redraws, power-bar arithmetic and frame-final state, with 4,096
unchanged-native state and drawing-call comparisons. Level-background frame
composition now preserves map timing, layer/filter priority, enemy strata and
spawn ordering, with 4,096 native state and host-call comparisons. Episode
picture display preserves PCX palette selection, picture loading and fade
ordering. Episode warning loading preserves encrypted warning lines, C-style frame-count parsing,
warning colors and timing; the warning path is compared with the unchanged
native routine across 256 scenarios. Conditional episode-level jumps preserve
the native multiplayer, ship, timer, alive-player and difficulty gates plus
byte narrowing; 2,048 cases compare against the unchanged routine. The
Galaga, extra-game and SuperTyrian episode modes also preserve player loadouts,
flags and cash transitions, with native parity across 256 seeded states.
Episode shop availability now decrypts and parses all nine typed rows, preserves
the shop-setting gates and item-screen handoff, and is compared with the native
reader across 15 input/settings scenarios.
The episode map's upper, lower and horizontal reveals now preserve the native
per-frame pixel copies and timing/event handoffs, verified against native frame
hashes across both standard and padded surface pitches.
The episode presentation dispatcher routes picture, transition, warning and
completion sections and preserves offline/network fade gates and clear/palette
ordering, compared against the unchanged C dispatcher across 40 cases.
Episode completion now preserves encrypted hint selection, score/cash lines,
SuperTyrian unlock text and ship graphics, network synchronization and return
state, with 48 unchanged-native scenarios.
Sound-bank loading and voice-tail trimming use an explicit audio
conversion boundary, eight-channel fixed-point PCM mixing, music archive and
song controls, and the complete LDS music sequencer with native OPL register
effects, keyboard/mouse/text/focus events and input waits, native viewport
coordinate mapping, and all five software mouse cursors with clipped/filter
sprite rendering and pixel restoration. SDL2 sample conversion and real
audio-device callback playback are integrated with the sound bank and mixer.
Real SDL2 event queues and window/renderer/texture presentation are integrated
with managed input, indexed palettes/scalers, fullscreen and viewport state.
HQ2x/HQ3x/HQ4x now have managed kernels with exact native pixel parity and
real SDL presentation across all nine scaler choices. OPL2 FM sample rendering
and SDL audio initialization are implemented: the device negotiates its sample
rate before creating the LDS/OPL2 synthesizer, and PCM vectors match native
FM/additive voices, tremolo, feedback, percussion, multiple channels, and block
boundaries. Rhythm noise accepts a platform random-source callback.
Conditional OPL3 four-operator sample output remains unimplemented, and full
game-host audio/title-menu integration is still pending. These systems are
integrated into the library and regression harness, not yet into playable game flows. Native
differential checks compare parsing, RNG, configuration, backgrounds, typed
items, help layouts, projectiles, effects, combat/HUD, enemy/collision/attack traces,
all level-event groups, binary level loading, boss bars, completion/sound queues,
sound-bank data, PCM mixing, music controls, LDS register sequencing, input
queues/host callbacks, viewport mapping, and cursor pixels
against unchanged C implementations;
see the conversion workflow for reference-harness build instructions.
Animation playback now composes LPF loading, palette updates, delta-frame decoding,
SDL presentation/input interruption, PIT waits and required network keep-alive.
It retains native starting-frame and final-loop-delta behavior; malformed
playback raises explicit errors while closing its owned stream.
The full jukebox loop now combines all 15 animated star patterns, original
music/sample title tables, keyboard/music/sample controls, automatic song fades,
and mouse/key exit with palette fading. Its concrete font/palette/star renderer
and music-controller/PCM/sample adapters are exercised through real SDL
presentation/events; OPL2 synthesis is available through the core LDS interface,
but the title-menu caller remains pending.

The game host supports the native `--help`, modern/legacy options, and network
argument validation. Actual launch remains an explicit incomplete-game error;
parsing `--net` does not claim working multiplayer.
The network library preserves native big-endian packets, acknowledgments,
retry/keep-alive/timeouts, delayed state/XOR recovery and player/episode/name
negotiation. Actual SDL2_net loopback exercises both peers and the protocol.
Malformed packets and transport errors throw, and disconnects unwind with
typed errors. The library now composes the native error screen, palette fades,
acknowledgment wait, input confirmation and transport cleanup; main-host/menu/
gameplay wiring remains pending. Use the installed
`SDL2_net.dll`, `libSDL2_net-2.0.so.0`, or `libSDL2_net.dylib`, or set an absolute
`SDL2_NET_LIBRARY_PATH`; the managed protocol uses native SDL2_net only for
platform transport services.
Gameplay state payloads and delayed request handling now run through
`LevelEvents` and the real transport: pause/setup context, skip timing,
NortShip upgrades and native desynchronization diagnostics are preserved.
The top-level playable loop and its menu callbacks are still incomplete.
Pause/resume now includes native text, audio attenuation, modifier filtering,
mouse/joystick waits, PIT timing and acknowledged two-player synchronization.
Player death/respawn now shares managed combat/effects/HUD state, including
arcade lives, Galaga/episode armor rules, linked resets and continuous-death mode.
Player inertia, position history, linking/turret shots, warp trails and all
sidekick positioning styles are integrated with the managed gameplay libraries.
Front/rear weapons and superbombs now use the actual projectile/item systems.
Player-two charge weapons and sidekick ammo/refill/firing/animation preserve
native cooldowns, charge arithmetic, sprite calls and HUD gauges.
Weapon-mode switching preserves the readiness latch, rear-pattern indicators
and all seven arcade special-weapon pairs using item IDs rather than raw indices.
Ship rendering now preserves banking, shadows, invulnerability, movement bounds,
DragonWing sidekick positions and NortShip spark projectiles/RNG.
Street-fighter command recognition now preserves all 26 combos, normal and
SuperTyrian mappings, per-player progress and execution state.
Special weapons now preserve costs, repulsion/attraction, flares, Zinglon/astral
effects, repair, invulnerability and sidekick spawning. The attack routine
composes them in native order with main/charge/bomb/sidekick firing, and sidekick
HUD drawing initializes ammo/refill/style/animation state. Player input collection now
combines actual keyboard/mouse/joystick services, analog turret aiming, native
movement/acceleration, menu/pause requests, demo-record bytes and network payloads.
Binary demo key playback now preserves waits, key/button mapping, truncated/EOF
state and collector/level-end ordering. Enclosing individual and paired player
frames now compose input, death/respawn, movement, linking/commands, rendering and
weapons in native order, including delayed network input, shared special-weapon
coordinates and parallax scrolling. Demo loading/recording now preserves original
and compact headers, native item-ID resolution, episode initialization,
selection/wrap/fallback, binary recording headers and first-free filenames.
Next-level preparation now preserves cleanup ordering, Galaga normalization,
speed restoration, demo shutdown, level/bonus/game-over routing and failed-save
recovery; 49,152 cases compare against the unchanged native routine.
The complete level/game loop and playable host remain pending.
Player/enemy collision damage, linked enemy destruction and shared armor
pickups now compose actual combat/HUD and explosion systems. Arcade, score,
sidekick, special and bonus pickups and the complete ordered player collision
dispatcher now match native state/effect/HUD traces. The enclosing playable
level loop remains pending. The in-game cash, lives, names, special-weapon and
superbomb HUD and cube renderer are now verified with concrete sprite/font
presentation.
Gamma cycling, legacy high-score sorting, initial player items, shop
costs/values and score-driven difficulty now have native parity coverage.
These remain core-library integrations; title/new-game/level-loop host flows
are not yet playable.
Glowing text now uses the ported font/input/PIT services with native frame,
brightness, skip-input and network ordering.
Character-by-character glow, timed warning bars/sounds and the in-game text
window now compose actual fonts, sprites, input and timers.
Single/two-player in-game help now composes the verified help/font/shape/input/
cursor services with native layout and wait ordering.
End-level result/unlock/cube flows and next-episode selection/rewards now
compose actual progression, archives, glow, font/sprites, palette, audio
callbacks and input/timers. Credits playback now preserves encrypted text,
scrolling/shadows, picture fades, seeded ship motion, music transitions and
button exit with concrete sprite/font/palette/file/input/timer integration.
High-score qualification and name entry now preserve player ordering,
score narrowing, glyph filtering, blinking/cursor input, cancellation and
result glow/palette/music ordering. Both flows have concrete managed service
adapters; top-level host wiring remains pending, and the game is still unplayable.
Player ship initialization now supplies generator power, initial armor and hit
areas, shared frame graphics and standard/edited sprite-sheet selection.
Optional `newsh$.shp` archives now load and decrypt with native four-checksum
and ushort size behavior; malformed archives fail explicitly without replacing
prior managed state. No custom ship assets are included.
The in-game keyboard dispatcher now composes these services with help,
network requests, cheat/debug/skip/pause/menu chords, smoothing/filter/speed
controls and random music, retaining native key consumption and ordering.
The enclosing playable level loop remains pending.
In-game settings now preserve all six rows, hover/click volume sliders,
keyboard navigation, mute toggles, detail/game-speed changes and return/quit
behavior. The concrete menu adapter uses actual indexed surfaces/fonts,
cursor/input, audio mixer, configuration and PIT settings. The local/network
entry and exit wrapper now synchronizes settings requests, peer waiting and
quit propagation over SDL2_net and restores relative mouse mode on failure
or cancellation. The enclosing playable caller remains pending.
The high-score viewer now displays both player modes for the first three
episodes, including unavailable-episode headings, difficulty ranks, paging
arrows and keyboard/mouse navigation. Score viewing leaves saved entries
unchanged; its concrete renderer/input/palette flow is exercised through
actual SDL2. Title-menu integration remains pending.
Normal, two-player, rich, super-arcade and SuperTyrian new-game initialization
now preserve the native selection/cancellation effects, item loadouts, cash,
input devices and difficulty rules. The concrete adapter uses the existing
selection menus and real rendering/palette/audio/input services; enclosing
game-loop integration remains pending. The managed title screen now binds
its animation, navigation, action dispatch, nine secret codes and idle-demo
behavior. An actual SDL2 integration path traverses title animation, score
viewing, title restart and new-game selections; final startup and remaining
title child flows still require integration before the executable is playable.
Startup logo sequencing and the SuperTyrian screen-bleed transition now use
managed surfaces and existing SDL2/palette/input timing services, with native
frame and final-buffer compatibility coverage.
The saved-game load screen also preserves native drawing, sparse-page
navigation and input precedence, and is integrated with the save catalog
and versioned-save loader through the SDL title-screen path.
The help topic menu and all eight original help pages likewise run through
the SDL title-screen path, preserving page layouts, input precedence,
cursor direction, music, fades and text-style restoration.
Title setup menus now integrate SDL display/scaler/scaling-mode pickers,
audio toggles and volumes, jukebox playback, and persisted shop-content
settings while retaining native input and action ordering.
The Christmas startup prompt now includes the native snow animation,
choice controls and local December detection, with an explicit random
service boundary and SDL rendering/input integration.
The network start screen now composes connection negotiation, host
episode/difficulty selection, peer details and synchronized player setup;
both selection-cancellation paths propagate quit to the remote peer.
Level warning text now shares the existing character glow, timed warning
bars, fonts/input and keep-alive services, preserving the line/prompt layout
and animation-skip wait behavior. The native disabled text synchronization
remains a no-op.
Ordered shutdown now closes SDL devices, releases managed shape/sample storage,
saves configuration except for native code 9, and preserves the farewell and
exit-code rules. Library termination uses a typed exception; the final playable
executable still needs to own these resources and map termination to process exit.
Shared byte-token readers now preserve native integer-prefix/overflow and
reference-capacity rules and are reused by typed item loading.
The level input dispatcher now composes game-over/demo interruption, keyboard
help/cheats, local pause/settings and network requests. Local pressed latches
are separate from delayed protocol requests, preserving the native dispatch order.
Low-armor warnings now share warning colors/sound timing and spawn rescue
ships through the real enemy/item systems, including multiplayer-owner rules.
Level presentation copies the native viewport with inversion/searchlight modes,
smooth-scroll timing and shared modal skip/quit flags.
Level debug overlays, pickup-notice flashing, alternating Pentium frame pacing
and timed event jumps also share the existing gameplay state.
Level startup now initializes both ships' trails and difficulty and composes
the level title, palette fade and lazy explosion-sheet loading. The subsequent
map/ship-state initializer preserves loaded map/header data while resetting
native row pointers, current/third-layer camera offsets, parallax offsets,
layer delays, player power, starfield speed, and both players'
velocities/invulnerability while retaining prior/secondary camera accumulators;
1,024 extracted-native traces verify its state.
Level-entry flags now reset input latches, movement/end-level state, warning
cadence, event/background/filter modes, boss links, pickups and shared quit
state while preserving unrelated boss-bar details; 16,384 extracted-native
cases compare every reset and retained field.
Joystick ports include signed analog controls, dual bindings, repeat timing,
menu key synthesis, native defaults/configuration and interactive assignment
capture. The real SDL device adapter is verified with a synthetic virtual
controller; physical-controller compatibility remains a release gate.
Gameplay, episode and difficulty selection menus now have managed interaction
and font/cursor/palette presentation, including the original hidden difficulty
unlocks and disabled-entry behavior. Their SDL integration is exercised with
synthetic fixtures; the title/new-game flows that call them are still pending.
Shadowed HUD gauges and any-button polling preserve native rendering/input
semantics, including partial-bar arithmetic and held mouse-button behavior.
The original `tyrian.sav` archive, encryption/checksums and slot save/load
state are now ported with synthetic byte-for-byte parity and real file
persistence. The newer `.tyrsav` game-state save/load, metadata and sorted,
player-filtered catalogs are also ported; save-menu wiring remains pending.
Versioned saves retain ID-based item lookup, both players and previous equipment,
native mode/episode rules, and compatible headers. Malformed files and native
NUL-cipher truncations are explicitly rejected rather than corrupting game state.
Processor-detail and game-speed controls now use the
managed PIT timer with native-compatible settings and timing.
Text `opentyrian.cfg` settings also have native-compatible defaults, SDL key
names, and durable file load/save while retaining unrelated joystick sections.
Legacy startup configuration now composes the exact 28-byte `tyrian.cfg`,
text settings, volume controls, default high scores, and encrypted archive
persistence. It remains library-integrated, not a playable startup flow.
Save-menu integration remains outstanding.

SDL audio checks use the real installed SDL2 runtime and a dummy device, not
physical speakers. The platform loader uses `SDL2.dll`, `libSDL2-2.0.so.0`, or
`libSDL2.dylib`; `SDL2_LIBRARY_PATH` may specify an absolute library filename.
Audio devices start paused, allowing bank/music initialization before playback.
The eventual host must check `SdlAudioDevice.ThrowIfFaulted()` each frame and
dispose the device before releasing its music/synthesizer state.
Video/input integration checks similarly require `SDL_VIDEODRIVER=dummy` and
`SDL_RENDER_DRIVER=software`, so they create no visible windows. All nine
nearest, Scale2x/Scale3x and HQ2x/HQ3x/HQ4x choices use the managed ports.
HQ output preserves the original six-bit RGB mask and YUV thresholds, including
the native width-based vertical-neighbor addressing on padded source surfaces.
`tools\generate_hq_kernels.py` reproduces the readable managed HQ pattern tables;
the game does not load or compile native game logic at runtime.

## C# data extractor

Run from the directory where the extracted `data` tree should be created:

```powershell
dotnet run --project src-csharp\OpenTyrian.DataExtractor -- --data C:\TYRIAN
```

The extractor reads the original `tyrian.hdt` and writes eight `.tyrr` category
files, retaining native record/field ordering, Latin-1 bytes, derived metadata,
and platform text-file newlines. Existing category files are preserved.
`--data=DIR` and `--help` are supported; without `--data`, the native search
order checks the installed data directory, `data`, and the current directory
for `tyrian1.lvl`. It does not extract shapes or levels. Runtime assets remain
external. All eight extractor outputs have been compared byte-for-byte with
the unchanged native extractor using separately supplied Tyrian 2.1 data.
