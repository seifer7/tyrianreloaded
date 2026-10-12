# C# conversion workflow

This document describes the order and evidence required for the port. It is a
development workflow, not an automated conversion script. No conversion is
performed by adding this document.

## 1. Establish the baseline

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
  item-data fixtures; separately supplied real-asset verification is pending.
- Build with `dotnet build OpenTyrian.slnx` and run the regression harness
  with `dotnet run --project tests/OpenTyrian.Core.Tests`.
- Verify the solution builds on each intended development platform before
  translating platform-dependent behavior.

## 3. Port in dependency order

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
samples. SDL conversion, FM waveform synthesis, and hardware integration remain
conversion/release gates.
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
Synthetic OPL rendering verifies archive/controller/LDS/mixer integration but
does not establish FM waveform parity. Invalid LDS indices/truncation throw
instead of accessing native memory; managed teardown also stops playback.
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
The attack oracle adds 192 enemy update frames, 16 hostile shot boundary cases,
90 weapon-special cases, and 128 aimed-shot cases, comparing full enemy/shot/
explosion/player-velocity/sound/draw/RNG traces. It records player damage at the
call boundary; separate damage/HUD parity and a real combat integration test
cover that dependency. Missing turret shot IDs throw rather than reusing stale
native lookup scratch state. Level events and whole-game callers remain pending.
SDL surfaces/events and whole-game behavior remain outside these oracle checks.

## 4. Verify parity continuously

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
  explicit, reviewed reason to remain unchanged; all 739 baseline function
  entries are accounted for.
- Runtime assets remain external and are neither required in source control
  nor redistributed by the build.
- Documentation, CI, release packaging, and the progress summary describe
  the delivered C# build accurately.

Only after these gates pass should native code and native-only build support
be considered for removal.
