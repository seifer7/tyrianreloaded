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
dispatch, sound-bank loading and voice-tail trimming with an explicit audio
conversion boundary, eight-channel fixed-point PCM mixing, music archive and
song controls, and the complete LDS music sequencer with native OPL register
effects, keyboard/mouse/text/focus events and input waits, native viewport
coordinate mapping, and all five software mouse cursors with clipped/filter
sprite rendering and pixel restoration. SDL2 sample conversion and real
audio-device callback playback are integrated with the sound bank and mixer.
Real SDL2 event queues and window/renderer/texture presentation are integrated
with managed input, indexed palettes/scalers, fullscreen and viewport state.
HQ2x/HQ3x/HQ4x now have managed kernels with exact native pixel parity and
real SDL presentation across all nine scaler choices. FM waveform synthesis
remains pending. These systems are integrated into the
library and regression harness, not yet into playable game flows. Native
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
presentation/events; FM synthesis and the title-menu caller remain pending.

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
The complete level/game loop and playable host remain pending.
Player/enemy collision damage, linked enemy destruction and shared armor
pickups now compose actual combat/HUD and explosion systems; collision pickup
dispatch and the enclosing collision loop remain pending.
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
external; actual-asset extraction still requires separately supplied Tyrian
2.1 data.
