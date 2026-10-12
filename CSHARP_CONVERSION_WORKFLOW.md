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

## 2. Agree the C# foundation before porting

- Select and document the supported .NET target, project/solution layout,
  package policy, SDL 2 binding, and test strategy.
- Create the C# game and supporting-tool projects plus automated test
  projects. Keep this decision explicit; the current repository does not yet
  specify a C# target framework or binding.
- Establish formatting, analyzer, build, and test commands and wire them into
  CI once a buildable C# project exists.
- Verify the empty project builds on the intended development platforms
  before translating game behavior.

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
