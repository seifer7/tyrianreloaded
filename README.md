# OpenTyrian C# Conversion

This branch is the preparation point for converting the complete OpenTyrian
game and its supporting tools from C to C#. The intended end state is a
buildable, playable C# version with behavior compatible with the current game.

**Conversion has not started.** The existing C implementation remains the
baseline and compatibility reference. This setup adds migration instructions
and a detailed progress inventory only.

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
