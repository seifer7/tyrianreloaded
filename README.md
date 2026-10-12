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
executable remains a scaffold and explicitly reports that gameplay has not yet
been ported. The data extractor is functional, with native byte-output parity
verified on synthetic asset records. The eventual platform layer will
bind to SDL 2 and SDL2_net through the existing native libraries to preserve
the current SDL-based capabilities without adding a package dependency.

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
sprite rendering and pixel restoration. SDL event/device integration,
conversion/device playback and FM waveform synthesis remain
pending. These systems are integrated into the
library and regression harness, not yet into playable game flows. Native
differential checks compare parsing, RNG, configuration, backgrounds, typed
items, help layouts, projectiles, effects, combat/HUD, enemy/collision/attack traces,
all level-event groups, binary level loading, boss bars, completion/sound queues,
sound-bank data, PCM mixing, music controls, LDS register sequencing, input
queues/host callbacks, viewport mapping, and cursor pixels
against unchanged C implementations;
see the conversion workflow for reference-harness build instructions.

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
