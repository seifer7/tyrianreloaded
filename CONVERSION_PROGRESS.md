# C# conversion progress

This inventory is the baseline of **126 files tracked before this setup**:
50 `.c` files, 50 `.h` files, and 26 other tracked files. It records **723
function definitions in C files** plus **43 inline function definitions in
headers**, for **766 baseline functions**. Function names are listed once per
source file; the two function-like initializer macros in `src/episodes.c`
(`ITEM_INT` and `ITEM_REF`) are not functions and are intentionally omitted.
The inventory audit restored sixteen existing OPL definitions and eleven
existing inline directory helpers omitted by the original scanner; these are
not newly added native functions and remain unchecked until dispositioned.

**Baseline conversion status: 121/126 files dispositioned; 766/766 functions
converted and verified.** Twenty-five unchanged documentation, licensing,
reference, artwork, and native-build/package files are explicitly retained;
the native build remains until the compatibility and release gates pass. The
C# foundation also adds one solution, shared
build properties, five projects, one hundred forty-seven application source
files, one hundred twenty-eight managed test sources, four native harnesses,
ninety-one oracle generators, one inventory checker, and one managed kernel
generator (391 added implementation/build/test files).
The game entrypoint parses native
options but remains unplayable; the data extractor has synthetic native parity.

## How to update this tracker

- Check off each baseline file only when its C# counterpart is verified, it
  has been intentionally replaced, or it has a documented and verified
  unchanged disposition. Keep notes on non-obvious retained/replaced files in
  the relevant change or documentation.
- Check off each function only after its behavior has been implemented in C#
  and relevant tests or parity checks pass. Do not check off inspection or
  mechanical translation alone.
- Update summary counts and this inventory whenever files or functions are
  added, removed, split, merged, or renamed. For a split/merge, account for
  every original function and identify its destination.
- The binary assets, copyright/license text, and external game data may be
  retained rather than translated; they still need an explicit verified
  disposition in the file checklist.

## C# foundation files

- [x] `Directory.Build.props` — .NET 10, nullable reference types, implicit
  usings, and warnings-as-errors; verified by solution build.
- [x] `OpenTyrian.slnx` — solution containing all initial C# projects;
  verified by solution build.
- [x] `.github/workflows/dotnet.yml` — Windows/Linux build, formatting, and
  regression-test CI; commands verified locally.
- [x] `src-csharp/OpenTyrian.Core/OpenTyrian.Core.csproj` — core library;
  verified by solution build.
- [x] `src-csharp/OpenTyrian.Core/MersenneTwister.cs` — 32-bit-compatible
  Mersenne Twister port; verified by regression vectors.
- [x] `src-csharp/OpenTyrian.Core/SizeBuffer.cs` — bounded byte-buffer
  operations, seek behavior, and little-endian word reads; verified by
  regression tests.
- [x] `src-csharp/OpenTyrian.Core/ArgumentParser.cs` - `OptionDefinition`,
  `ParsedOption`, and `ArgumentParser` replace the declarations and five
  implementations in `arg_parse.h`/`arg_parse.c`. Parser state is per instance
  rather than static, permitting independent argument lists. The native
  permutation, clustered/attached options, abbreviations, and diagnostics are
  preserved. Verified against compiled native C.
- [x] `src-csharp/OpenTyrian.Core/AnimationDecoder.cs` - `Decode` replaces
  `JE_playRunSkipDump` using managed frame memory and the ported `SizeBuffer`.
  Verified by synthetic operation, bounds, and full-frame tests.
- [x] `src-csharp/OpenTyrian.Core/BinaryFile.cs` - stream length, exact/partial
  byte reads, signed/unsigned little-endian I/O, booleans, and PCX header
  output. Read/write failures throw rather than terminating SDL and the
  process; future hosts must report these errors and perform their cleanup.
  Partial writes use the managed Stream contract (success or exception),
  rather than returning C stdio short-write counts.
- [x] `src-csharp/OpenTyrian.Game/OpenTyrian.Game.csproj` — game host project,
  scaffold only; verified to build.
- [x] `src-csharp/OpenTyrian.Game/Program.cs` - integrated native-compatible
  command-line parsing into typed startup state, help and invalid-option exits,
  followed by an explicit not-yet-playable error for actual game launch;
  executable paths verified.
- [x] `src-csharp/OpenTyrian.DataExtractor/OpenTyrian.DataExtractor.csproj` —
  data-extractor host project referencing the managed core; verified to build.
- [x] `src-csharp/OpenTyrian.DataExtractor/Program.cs` — explicit
  extraction CLI entrypoint; verified by build and core CLI parity checks.
- [x] `src-csharp/OpenTyrian.HdtWeaponTool/OpenTyrian.HdtWeaponTool.csproj` —
  standalone managed HDT conversion CLI; verified by solution build.
- [x] `src-csharp/OpenTyrian.HdtWeaponTool/Program.cs` — HDT tool entrypoint;
  verified with the core converter regression harness.
- [x] `tests/OpenTyrian.Core.Tests/OpenTyrian.Core.Tests.csproj` — core
  regression harness project; verified to build.
- [x] `tests/OpenTyrian.Core.Tests/Program.cs` — deterministic RNG
  and size-buffer compatibility regression tests; verified by test run.
- [x] `tests/OpenTyrian.Core.Tests/ArgumentParserTests.cs` - parser regression
  scenarios, including independent instances.
- [x] `tests/OpenTyrian.Core.Tests/AnimationDecoderTests.cs` - all six
  run/skip/copy variants, stop, malformed input, and exact full-frame writes.
- [x] `tests/OpenTyrian.Core.Tests/NativeCompatibilityTests.cs` - compiled-C
  differential comparison of 15 parser cases and 15,000 RNG integer/float
  results across four seeds.
- [x] `tests/OpenTyrian.Core.Tests/BinaryFileTests.cs` - signedness, exact
  binary output, PCX header layout, fragmented/partial reads, EOF/write
  failures, and stream position preservation.
- [x] `tests/OpenTyrian.Core.Tests/HdtWeaponArchiveTests.cs` - all 781 HDT
  records, signed/unsigned layout, XML field ordering, binary roundtrip,
  preservation of surrounding bytes, truncation, invalid values and CLI usage.
- [x] `src-csharp/OpenTyrian.Core/HdtWeaponArchive.cs` - managed replacement
  for the HDT weapon binary/XML conversion utility; preserves 80-byte
  little-endian records, sorted XML fields, all 781 weapons and in-place
  archive updates. Verified by byte-for-byte synthetic roundtrip and tests.
- [x] `src-csharp/OpenTyrian.Core/Configuration.cs` - ordered scalar/list
  configuration model, quoted text codec, typed settings, and case-sensitive
  section/option lookup. Native serialization and numeric-reading fixtures
  match compiled C. The ten unmanaged string/allocation lifecycle helpers are
  left pending their explicit managed-ownership disposition.
- [x] `src-csharp/OpenTyrian.Core/LegacyConfigurationTypes.cs` - exact
  difficulty, keyboard-setting, and player-shot enum ordinals from
  `config.h`; consumed by typed keyboard configuration accessors and all
  gameplay, enemy, pickup, special-weapon, and selection-menu projectile
  creation, and verified against every native enum value.
- [x] `tests/OpenTyrian.Core.Tests/LegacyConfigurationTypesTests.cs` - verifies
  all native enum ordinals and typed keyboard slot/scancode bounds.
- [x] `tests/OpenTyrian.Core.Tests/ConfigurationTests.cs` - configuration
  roundtrips, model operations, typed settings, and invalid directives.
- [x] `src-csharp/OpenTyrian.Core/DestructUnitNames.cs` - typed Destruct unit
  values and exact native string-to-unit lookup.
- [x] `tests/OpenTyrian.Core.Tests/DestructUnitNameTests.cs` - all names,
  case/whitespace rejection, unknown values, and null handling.
- [x] `src-csharp/OpenTyrian.Core/DestructUnits.cs` - Destruct unit/shot and
  player state, native CPU initialization, weapon availability/cycling,
  health validity/reset, animation frame updates, sound dispatch,
  destruction scoring,     terrain stability checks and aliasing, temporary-screen fade/copy processing,
  Destruct explosion setup, sound routing, super-pixel rendering, and help-screen
  cool-pixel/wall/crosshair
  rendering, pause presentation, terrain generation/drawing/rings/unit
  placement and walls, terrain orchestration, explosion collision/ticks,
  projectile creation/trails, and angle/power
  controls, HUD rendering, unit sprite selection, and gravity movement/
  dispatch.
- [x] `tests/OpenTyrian.Core.Tests/DestructUnitTests.cs` - native validity
  boundaries, both players' reset slots, untouched excess slots, and argument
  validation.
- [x] `src-csharp/OpenTyrian.Core/EncryptedText.cs` - byte-preserving Pascal
  string decryption, truncation/NUL termination, and skip operations.
- [x] `src-csharp/OpenTyrian.Core/AnimationFile.cs` - LPF header, palette,
  page lookup/loading, and frame decode; owns its stream unless leaveOpen.
  Invalid native out-of-bounds page/record cases now throw explicit errors.
  Playback timing, presentation, input, and networking remain native.
- [x] `tests/OpenTyrian.Core.Tests/AssetReaderTests.cs` - synthetic encrypted
  strings and LPF page/frame fixtures, disposal, and malformed headers/pages.
- [x] `src-csharp/OpenTyrian.Core/IndexedSurface.cs` - pitch-aware indexed
  frame storage and drawing primitives; managed clipping prevents native
  negative-coordinate memory corruption, and rectangle warnings are retained.
  Fill operations follow SDL's visible-width clipping; direct pixel and shade
  operations retain native pitch-based bounds for valid coordinates.
- [x] `tests/OpenTyrian.Core.Tests/IndexedSurfaceTests.cs` - outline/fill,
  pitch padding, brightness saturation, hue retention, gauges, and warnings.
- [x] `src-csharp/OpenTyrian.Core/PaletteSystem.cs` - 23 VGA palettes, RGB/YUV
  caches, inclusive palette ranges, fade deltas and step ordering. SDL RGB
  mapping and delay/present/wait are injected pending platform integration.
- [x] `tests/OpenTyrian.Core.Tests/PaletteTests.cs` - six-bit expansion,
  signed-shift YUV, exact fade targets, partial ranges, presentation order.
- [x] `src-csharp/OpenTyrian.Core/PictureLoader.cs` - PCX and tyrian.pic
  indexed RLE loading, pitch behavior, working/active palette updates, and
  native picture/face palette indices. Picture positions are read per stream
  rather than cached globally; file bytes and lookup indices are unchanged.
- [x] `tests/OpenTyrian.Core.Tests/PictureLoaderTests.cs` - synthetic PCX and
  13-picture archive fixtures; palette-marker and storepal semantics.
- [x] `src-csharp/OpenTyrian.Core/VideoScaler.cs` - shared 16/32-bit nearest,
  Scale2x, Scale3x kernels and native scaler-name table. Tests verify integer
  factors, source/destination pitch, mapped-color comparisons, and output
  truncation. All HQ kernels and real SDL scaler presentation are now verified
  by the additional HQ ports below.
- [x] `tests/OpenTyrian.Core.Tests/VideoScalerTests.cs` - replication and
  edge-aware fixtures, pitch padding, and 16-bit color truncation.
- [x] `src-csharp/OpenTyrian.Core/PlayerState.cs` - player/item/sidekick types,
  lives alias to the player-index weapon power, item snapshots, purple-ball
  thresholds/upgrades, cash consolation, and alive/dead predicates. Cash
  retains native unsigned-long widths (32-bit Windows/32-bit processes,
  64-bit Unix). Gameplay movement, damage, and save integration remain pending.
- [x] `tests/OpenTyrian.Core.Tests/PlayerRulesTests.cs` - every threshold,
  player-index power/lives aliasing, front/rear upgrades, max/empty weapons,
  cash overflow, independent snapshots, and all alive/dead combinations.
- [x] `src-csharp/OpenTyrian.Core/Sprites.cs` - bounded sprite tables,
  compressed one-based sprite sheets, main archive loading, transparent runs,
  hue/value and blend effects, dark/black silhouettes, pitch-based clipping,
  and four-tile composites. The non-clipping native variants intentionally
  retain linear-address row wrapping; primary-screen pitch is an explicit
  optional argument for compressed sprites. Invalid reads throw instead of
  touching native memory. File lookup takes resolved paths/directories pending
  the data-directory service.
- [x] `tests/OpenTyrian.Core.Tests/SpriteTests.cs` - both encodings, effects,
  metadata, padding, composites, main archive, file loading, and malformed data.
- [x] `src-csharp/OpenTyrian.Core/FontRenderer.cs` - byte-indexed native font
  map, text measurement/centering, alignment, hue/highlight/blend/dark text,
  drop shadows, and four-direction outlines. Byte spans retain original
  CP437 asset indices without implicit Unicode/UTF-8 conversion. The native
  width contribution of an absent glyph is retained, though drawing skips it.
- [x] `tests/OpenTyrian.Core.Tests/FontTests.cs` - glyph map, NUL termination,
  absent glyph metrics, highlight toggles, alignment, blends, and shadow order.
- [x] `src-csharp/OpenTyrian.Core/DataFiles.cs` - cached asset-directory
  selection, native search priority, raw/warning/required open behavior, and
  open-based existence checks. Installed-directory default is C:\TYRIAN on
  Windows and /usr/local/share/games/tyrian on Unix; builds with a different
  native TYRIAN_DIR must supply that value. Required-file errors propagate
  for host reporting rather than halting SDL in the core.
- [x] `src-csharp/OpenTyrian.Core/ItemDataExtractor.cs` - complete legacy
  item-data reader, all eight ordered category writers, native derived fields,
  CLI/options/messages, category preservation, and temporary-file finalization.
  Byte-for-byte native checks cover 1,785 synthetic records, high-bit/name/NUL
  bytes, signed values, embedded text newlines, and hardcoded lookup tables.
  All eight category outputs were also byte-for-byte compared with unchanged
  native extraction from the separately supplied Tyrian 2.1 `tyrian.hdt`.
  Failure handling removes incomplete temporary output and refuses concurrent
  overwrites; generated categories and source assets remain outside the repo.
- [x] `tests/OpenTyrian.Core.Tests/ExtractorTests.cs` - synthetic complete
  asset generation, exact category counts, native byte/CLI comparisons,
  preservation, directory lookup/cache, malformed offsets/truncation, missing
  input, and output-directory errors.
- [x] `tests/native/build_extractor_reference.py` - extracts unchanged native
  source regions into an SDL-free oracle; MSVC compilation and parity verified.
- [x] `src-csharp/OpenTyrian.Core/LevelArchive.cs` - native 43-slot level
  directory, EOF position, episode filename setup, availability scanning,
  wrap/repeat flags, and same-episode no-op. Rejects oversized directories
  and the native uninitialized/no-assets infinite-loop case.
- [x] `tests/OpenTyrian.Core.Tests/LevelTests.cs` - level offsets/stream
  position, bounds, episode scan, naming, selection, wrap, and no-op behavior.
- [x] `src-csharp/OpenTyrian.Core/LevelMaster.cs` - native level capacity
  constants and 34-entry shape-file table.
- [x] `tests/OpenTyrian.Core.Tests/LevelMasterTests.cs` - exact native capacity
  constants and all shape-file bytes.
- [x] `src-csharp/OpenTyrian.Core/MusicMaster.cs` - canonical native song
  titles and song-selection constants plus shared mutable `songBuy` and
  `musicFade` state used by episode, level-event and level-frame flows.
- [x] `tests/OpenTyrian.Core.Tests/MusicMasterTests.cs` - verifies that episode
  selection, level events and frame processing share native music globals.
- [x] `src-csharp/OpenTyrian.Core/SoundMaster.cs` - native sound IDs, title
  table, counts and level-window voice table, shared by jukebox and event code.
- [x] `src-csharp/OpenTyrian.Core/LegacyGameTables.cs` - native arcade ship,
  arcade weapon/special, sidekick-option, keyboard special-code and ship
  special-code, planet sprite/animation, enemy-launch sound and SuperTyrian
  special-drop, SuperArcade progression, starting loadout and mode-switch
  tables centralized for new-game, pickup, player-input, map, enemy and
  level-event flows.
- [x] `tests/OpenTyrian.Core.Tests/LegacyGameTablesTests.cs` - checks all
  arcade and sidekick table entries plus out-of-range guards.
- [x] `src-csharp/OpenTyrian.Core/JukeboxPalette.cs` - all 256 native VGA
  colors, wired into jukebox presentation and checked against the native RGB
  table digest.
- [x] `tests/OpenTyrian.Core.Tests/JukeboxPaletteTests.cs` - exact palette
  length, sentinel entries and all RGB bytes verified against native digest.
- [x] `src-csharp/OpenTyrian.Core/BackgroundRenderer.cs` - three tile layers,
  transparency/blending, delay/movement ordering, water synchronization,
  darkening, screen hue/brightness fade, lava/water/blur/iced effects, processor
  gates, and 100-star state. Managed map offsets replace native pointers.
  Native linear-address clipping, pitch padding, in-place traversal, ushort
  star wrap, and global RNG ordering are preserved; invalid maps/surfaces throw.
- [x] `tests/OpenTyrian.Core.Tests/BackgroundTests.cs` - twenty native pixel
  comparisons (ten modes, two pitches), movement/delay byte wrapping, processor
  thresholds, fades, padding, and occupied-star pixels.
- [x] `tests/native/build_background_reference.py` - unchanged native
  background code with minimal surface/SDL fill shim; linked with native RNG.
- [x] `src-csharp/OpenTyrian.Core/HelpTextData.cs` - native encrypted text
  group ordering, field capacities, delimiters, ship information, menu-help
  map, and shared item-data offset. Text retains byte indices/NUL termination.
- [x] `tests/OpenTyrian.Core.Tests/HelpTextTests.cs` - every group and slot at
  maximum string capacity, decrypt ordering, final stream position, unused
  fields, map constants, and truncated input.
- [x] `src-csharp/OpenTyrian.Core/DelayTimer.cs` - two independent PIT delay
  targets, native single-precision target arithmetic, polling/service/input
  waits, and volume clamping/callback ordering. Platform callbacks remain
  unwired until SDL input/timing/audio ports. Undefined C float-to-uint
  conversions outside 0..uint.MaxValue now throw explicitly.
- [x] `tests/OpenTyrian.Core.Tests/DelayTests.cs` - PIT timing, target
  independence, poll order, key/mouse interruption, zero speed, precision,
  invalid targets, and volume boundaries.
- [x] `src-csharp/OpenTyrian.Core/ItemDefinitions.cs` - Latin-1 bundle parser,
  BOM handling, ASCII whitespace/IDs, schema/integer/reference validation,
  source-line diagnostics, whole-record replacement, stable slot ordering,
  zero-slot reservation, aliased ID lookup, and deterministic mod file order.
  Managed dictionaries replace slot/allocation internals.
- [x] `tests/OpenTyrian.Core.Tests/ItemDefinitionTests.cs` - replacement,
  zero-slot ordering, aliases, whitespace/ID rules, field validation, sorted
  mods, and fourteen native record/diagnostic comparisons.
- [x] `tests/native/build_items_reference.py` - unchanged native bundle
  parser, typed loader/field application, runtime lookup, and shop selection,
  with native RNG, minimal typedef/shutdown prelude, and generated field dumps.
- [x] `src-csharp/OpenTyrian.Core/GameItems.cs` - fixed-width shot, ship,
  weapon, generator, shield, sidekick, special, and enemy models; separate
  typed collections replace native slot-zero count members. KnownItem enum
  preserves the native special-index table ordering.
- [x] `src-csharp/OpenTyrian.Core/ItemDatabase.cs` - integrated base/mod
  loading, zero-slot allocation, all field application, immediate/late cross
  references, required special indices, aliased runtime lookup, and seeded
  random shop selection. Preserves partial duplicate arrays, byte/word wrap,
  signed sbyte/short fields, reference capacity/truncation, sidekick truthiness
  versus enemyground == 1, and candidate index byte wrapping above slot 255.
  Managed error exceptions retain native file/line diagnostics. Integer-ID
  formatting safely handles int.MinValue instead of native buffer overflow.
- [x] `tests/OpenTyrian.Core.Tests/ItemDatabaseTests.cs` - every typed field,
  six boundary-value fixtures, native required-reference diagnostics,
  zero-slot and special-index order, forward references, mod replacement,
  probability/position gates, and native shops including >255 slots.
- [x] `src-csharp/OpenTyrian.Core/HelpTextRenderer.cs` - native character-count
  wrapping, byte-counter edge behavior, shaded line drawing, one-based Help
  archive lookup, vertical spacing, and color/shade reset. Retains narrow-word
  truncation and empty-line behavior. Explicitly rejects native infinite loops
  (one-byte and >255-byte strings) and uninitialized zero-capacity substrings.
- [x] `tests/OpenTyrian.Core.Tests/HelpTextRendererTests.cs` - 106 native
  line-layout comparisons, byte-indexed text, NUL/empty strings, state reset,
  help index bounds, and unsupported native-undefined/infinite-loop lengths.
- [x] `tests/native/build_help_reference.py` - unchanged helpBox region,
  SDL_strlcpy equivalent, and shaded-line capture oracle.
- [x] `src-csharp/OpenTyrian.Core/PlayerShots.cs` - projectile allocation,
  power consumption/sound channel selection, multi-pattern cycling, mouse and
  player-following motion, circles, target acquisition/homing, lifetime,
  shop preview simulation, directional graphics, collision outputs, and draw/
  explosion/superpixel callbacks. Native short/ushort/byte wrapping, scratch
  distance width, movement/draw order, and reserved slot 80 are preserved.
  Sprite adapter integrates existing compressed/regular sprite renderers;
  explosion/superpixel handlers and every managed projectile-creation caller
  use the typed native shot-slot ordinals.
  Native invalid item-count equality, array offsets, and undefined floating
  conversions throw rather than accessing invalid memory.
- [x] `tests/OpenTyrian.Core.Tests/PlayerShotTests.cs` - six native state/
  effect traces, all 81 slots, homing target disappearance, mouse control,
  capacity/power rejection, zero graphics, preview behavior, reserved slot,
  typed slot routing/range rejection, short wrapping, special collision radii,
  and 68 directional updates per trace. SpriteTests additionally exercise the
  rendering/callback adapter.
- [x] `tests/native/build_shots_reference.py` - unchanged projectile source,
  shared native typed item loader, and recording sprite/effect shims.
- [x] `src-csharp/OpenTyrian.Core/SceneEffects.cs` - explosion allocation,
  type table and smoke offsets, four-part large explosions, repeat scheduling,
  sound queues, scroll/follow motion, lifetime/draw order, and circular-buffer
  superpixels with native RNG float/trigonometric ordering and indexed blends.
  Projectile callbacks now wire into this scene system. Rejects invalid
  explosion types and native nonterminating superpixel counts above 255.
- [x] `tests/OpenTyrian.Core.Tests/SceneEffectsTests.cs` - four complete
  state/draw/pixel-hash/RNG traces over 35 frames, every explosion type, pool
  exhaustion, repeat spawning, corner clipping, padding, particle ring wrap,
  and callback integration through SpriteTests.
- [x] `tests/native/build_effects_reference.py` - unchanged explosion setup,
  scene update, and superpixel code; recording sprite calls and pixel hashes.
- [x] `src-csharp/OpenTyrian.Core/PlayerCombat.cs` - shield/armor damage,
  exact-zero survival, death/cheat state, explosion/sound integration, HUD
  clearing, shield maxima, both-player armor clamp, two-player/Galaga gauges,
  rear port configuration lookup, and shaded vertical gauge drawing. Managed
  surface ownership replaces the native screen-pointer swap. Rejects native
  nonterminating byte gauges >=255 and undefined floating gauge conversions.
- [x] `tests/OpenTyrian.Core.Tests/PlayerCombatTests.cs` - 8,640 native
  damage/state/explosion/HUD comparisons across two pitches and eight
  combinations of two-player, Galaga, and cheat flags; death/survival boundaries.
- [x] `tests/native/build_combat_reference.py` - unchanged native player
  damage/HUD/gauge functions with an SDL fill shim and scene-state capture.
- [x] `src-csharp/OpenTyrian.Core/EnemyState.cs` - fixed-width enemy runtime
  and boss bar state, sharing position/availability with projectile targeting.
- [x] `src-csharp/OpenTyrian.Core/EnemyMovement.cs` - seeded pursuit
  acceleration, signed-byte oscillators, bouncing/movement/visibility,
  pickup bounds, animation, filtered single/four-tile drawing, and aimed
  velocity calculation. Rejects invalid native animation indices and
  undefined float-to-word velocity conversions.
- [x] `src-csharp/OpenTyrian.Core/EnemySystem.cs` - enemy sprite selection,
  25-slot allocation, initialization, difficulty scaling, projectile hit tests,
  linked damage/filter/ice/boss bars, damage animation, linked destruction,
  death pickups, arcade powerup rotation, event flags/jumps, rewards, and
  projectile collision processing. Requires shared enemy/player/sound state.
  Native reused animation/score/sprite fields and scalar narrowing are retained.
  Invalid flags and missing legacy IDs throw. The native reserved stripe leaves
  its kill-credit player uninitialized; managed stripe rewards go to player one.
  Enemy AI, enemy projectile collisions, event interpreter, and game loop remain
  pending.
- [x] `tests/OpenTyrian.Core.Tests/EnemySystemTests.cs` - 156 initialization
  cases plus legacy/arcade lookup, 5,000 hit tests, 16 full linked-marker sweeps,
  eight linked animation/death traces, and thirteen integrated projectile traces.
  Compares complete enemy state, sprite/definition identities, rewards/flags,
  queued explosions/repeats/particles, rendering order, projectile state, and RNG.
  Additionally checks 256 native movement/animation frames and 1,000 aim
  calculations, plus actual compressed-sprite rendering/filter reset.
- [x] `tests/native/build_enemy_reference.py` - unchanged enemy creation,
  scaling, collision helpers, projectile processing, shot movement/creation, and
  explosion/particle setup, motion/animation/aim functions, and shared typed
  native item loading.
- [x] `src-csharp/OpenTyrian.Core/EnemyAttacks.cs` - hostile shot allocation,
  turret patterns/orientations, aiming and tracking, sounds, weapon specials,
  difficulty/Galaga fire scheduling, ice/damaged gates, child launches, shared
  enemy draw/update flow, and hostile shot motion/collision/animation/rendering.
  Player coordinates are restored if a renderer fails. Real player damage/HUD
  and impulses are integrated through `PlayerCombat`, using shared player/effect
  state. Missing turret IDs throw instead of reusing native stale lookup scratch
  state; invalid pattern indices throw instead of reading past arrays.
- [x] `tests/OpenTyrian.Core.Tests/EnemyAttackTests.cs` - 192 full native
  enemy update/attack/launch frames, 16 hostile collision/clipping/wrap cases,
  90 special-weapon cases, and 128 two-player aimed shot cases. Complete enemy,
  hostile projectile, explosion, player velocity, sound, draw order, and RNG
  traces include pool exhaustion, aimed/random child launches, Galaga,
  damaged/iced enemies, and live/dead player targeting. Direct checks cover real
  player damage/HUD integration and coordinate restoration after rendering fails.
- [x] `tests/native/build_enemy_attacks_reference.py` - unchanged attack,
  launch, full enemy update, and hostile shot update functions; recorded sprite
  and player-damage callbacks (damage/HUD parity is verified separately).
- [x] `src-csharp/OpenTyrian.Core/LevelEvents.cs` - typed mutable events,
  enemy placement, jumps/returns, last-match searches, linked movement,
  acceleration, animation, armor, firing, death drops, and enemy-event dispatch.
  Shared enemy/player/background/effect state preserves event side effects.
- [x] `src-csharp/OpenTyrian.Core/LevelEventActions.cs` - environment, level,
  and miscellaneous event groups and complete dispatch; explicit host callbacks
  for shape loads, text windows, music, and volume. Scripted filters, background
  positions/wraps, difficulty, flags/skips/timers, random links, boss bars,
  and special-weapon shot resets retain native fixed-width behavior. Malformed
  indices, missing item IDs, and native infinite-loop cases report data errors.
  Level-completion checks use shared enemy counts and explosion state to restart
  paused scrolling and end levels; queued sounds preserve channel order, native
  volume weights, and first-game-over gating through explicit playback callbacks.
- [x] `tests/OpenTyrian.Core.Tests/LevelEventTests.cs` - 64 event spawns,
  six jump/return cases, eight last-match/Galaga searches, 300 enemy controls,
  and 96 random drops; complete enemy/event/RNG traces plus malformed jumps.
- [x] `tests/OpenTyrian.Core.Tests/LevelEventDispatchTests.cs` - all 84 event
  type values across four modes (336 cases), full state and ordered host-action/
  diagnostic/RNG parity, real synthetic shape loading/unloading and rendering,
  and explicit malformed/infinite-loop event failures.
- [x] `tests/native/build_enemy_events_reference.py` - unchanged enemy-event
  helpers and dispatch, with shared typed item/enemy oracle support.
- [x] `tests/native/build_level_events_reference.py` - unchanged complete
  event dispatcher and action groups, native-shaped map pointer arrays, recorded
  shape/text/music/volume callbacks, full state and diagnostic snapshots.
- [x] `src-csharp/OpenTyrian.Core/LevelMap.cs` - binary level headers, enemy
  lists, signed event records, big-endian shape references, all 600 shape
  records, per-slot tile ownership and native fill flags, three map layers,
  selected-level archive seeking, and shared background/event integration.
  Native uninitialized reference slots and oversized lists/events/maps throw
  explicit data errors; the native sentinel is represented by bounds-aware
  event arrays and the `SentinelTime` contract rather than an executable event.
- [x] `tests/OpenTyrian.Core.Tests/LevelMapTests.cs` - six complete native
  level cases covering zero/maximum counts, fixed-width signed fields,
  selected archive offsets, blank/excluded/duplicate shapes, exact tile hashes,
  all 21,600 map entries per level, real background rendering, and malformed/
  truncated files. Synthetic assets only.
- [x] `tests/native/build_level_map_reference.py` - unchanged native level
  loaders and map structures; complete event, shape/hash/fill, and map-slot
  snapshots using externally generated fixtures.
- [x] `src-csharp/OpenTyrian.Core/EnemyHud.cs` - most-damaged linked boss
  selection, dead/missing/invulnerable handling, single-bar migration,
  timer-aware placement, three-tone shading, and byte-width hit-flash decay;
  integrated with shared event links and indexed surfaces.
- [x] `tests/OpenTyrian.Core.Tests/EnemyHudTests.cs` - 512 native state/draw
  traces plus real pitched-surface shading and event-to-HUD integration.
- [x] `tests/native/build_boss_bar_reference.py` - unchanged boss-bar
  selection/rendering and ordered rectangle callbacks.
- [x] `tests/OpenTyrian.Core.Tests/LevelStateTests.cs` - 1,024 level-end
  combinations with real repeating-explosion state and shared scrolling/timer
  state; 76 queued-sound traces, explicit invalid IDs, and playback failures
  preserving pending sounds.
- [x] `tests/native/build_level_state_reference.py` - unchanged completion
  and sound-dispatch functions with complete state and ordered playback traces.
- [x] `src-csharp/OpenTyrian.Core/SoundBank.cs` - native 29-effect/nine-voice
  directories, Christmas voice selection, voice-tail trimming, 64 KiB input
  limits, mandatory signed-8-bit/16-bit conversion boundary, indexed sample
  playback and queue-to-bank integration. SDL conversion and device playback
  remain platform dependencies, not replaced by a managed resampler. Failed
  reads/conversions throw and preserve the previous bank instead of native
  partially updated/zeroed samples.
- [x] `tests/OpenTyrian.Core.Tests/SoundBankTests.cs` - six complete synthetic
  banks, signed byte/size/tail boundaries, all 38 playback indices, queued
  routing, malformed/truncated banks, and transactional conversion failures.
- [x] `tests/native/build_sound_bank_reference.py` - unchanged bank loader
  and sample-number playback, with a deterministic recording converter to
  compare input bytes, sample ownership/counts, and output routing. This
  verifies the conversion boundary, not SDL resampling or hardware audio.
- [x] `src-csharp/OpenTyrian.Core/AudioMixer.cs` - eight managed sample
  channels, replacement/consumption, Q20.12 logarithmic volume factors,
  saturating signed-16 output, native disabled/stopped gates, doubled music
  gain, and fractional 69.5 Hz LDS scheduling. Locking serializes audio buffers,
  playback changes, volume, and music commands; synthesis is a required service.
- [x] `tests/OpenTyrian.Core.Tests/AudioMixerTests.cs` - 256 modes at five
  rates and six buffer sizes (1,536 callbacks), exact native PCM/update/channel
  traces, replacement, cleanup, invalid inputs, and propagated synthesis failure.
- [x] `tests/native/build_audio_mixer_reference.py` - unchanged callback,
  volume/channel dispatch, native float/fixed-point factors, and a deterministic
  FM generator for full output/scheduler comparison.
- [x] `src-csharp/OpenTyrian.Core/MusicPlayer.cs` - managed archive/song
  ownership, offset validation, selection, stop/restart/fade, disabled gates,
  and synchronized LDS/mixer commands. Native song-zero initial selection and
  same-song resume are preserved. Invalid song/load failure throws with music
  stopped, rather than native warning followed by stale-track playback.
- [x] `tests/OpenTyrian.Core.Tests/MusicPlayerTests.cs` - four synthetic
  archives, 192 native command/state transitions, zero/large songs, malformed
  offsets, disabled gates, and failure-state ownership.
- [x] `tests/native/build_music_reference.py` - unchanged music-directory
  and control functions, recorded LDS load bytes, rewind/fade calls and states.
- [x] `src-csharp/OpenTyrian.Core/LdsPlayer.cs` - bounded binary LDS loading,
  typed instrument/channel/order ownership, rewind/OPL initialization, register
  caching/masking, instrument triggers, tuning/glide/portamento, vibrato/
  tremolo/arpeggio setup, managed teardown, OPL2 FM synthesis, and OPL3
  four-operator sample rendering for all four native connection topologies.
- [x] `src-csharp/OpenTyrian.Core/LdsPlayback.cs` - complete nine-channel
  packed-pattern sequencing, note/instrument transpose, delays, every native
  command, tempo/order jumps and looping, fade, key-off, arpeggio, portamento,
  vibrato, and carrier/modulator tremolo with fixed-width native arithmetic.
  Invalid order/pattern/instrument/arpeggio/register indices and negative
  frequency indices throw instead of native invalid memory accesses.
- [x] `src-csharp/OpenTyrian.Core/LdsTables.cs` - exact native frequency,
  operator, vibrato, and tremolo tables, verified through ordered register traces.
- [x] `tests/OpenTyrian.Core.Tests/LdsPlayerTests.cs` - 24 synthetic songs,
  9,216 sequencer frames and 1,536 direct notes, all native command families,
  transposes, effects, delayed notes, fades, stop/loops/rewind/cache writes,
  truncation/invalid data, real archive/controller/LDS/mixer integration,
  native OPL2 sample parity for FM/additive, tremolo, feedback, percussion,
  multiple channels, block boundaries, and reset behavior, plus native OPL3
  stereo sample hashes for all four four-operator topologies over short and
  block-boundary renders.
- [x] `tests/native/build_lds_reference.py` - entire unchanged LDS source,
  native instrument/channel models, recorded OPL reset/register order and
  playback state snapshots.
- [x] `src-csharp/OpenTyrian.Core/VideoLayout.cs` - all four native viewport
  modes, exact case-sensitive names, centered output rectangles, pixel-center
  coordinate mapping, and signed/truncated relative distances, including
  negative/offscreen and scaled-zero results. Host sets the last presented rect.
- [x] `src-csharp/OpenTyrian.Core/InputState.cs` - complete keyboard/mouse/
  text/focus event state, native per-key-event return ordering, Alt+Enter,
  relative-mode focus/reset behavior, pointer visibility/inactivity, button
  ordering, UTF-8 byte truncation, queue flushing, native empty clear-keyboard
  hook, keyboard/mouse/joystick waits and network service. Implements the real
  `IDelayPlatform` consumed by `DelayTimer`; actual SDL/joystick/network host
  callbacks are required and remain pending.
- [x] `tests/OpenTyrian.Core.Tests/InputTests.cs` - 512 native viewport/
  mapping cases, 1,792 event-state snapshots across 128 queues with ordered
  host/wait callbacks, relative/focus resets, key-release quirks, raw text-tail
  ownership, invalid scancodes/viewports, quit queue preservation, joystick
  press/release waits, PIT integration and propagated platform errors.
- [x] `tests/native/build_input_reference.py` - unchanged keyboard and
  viewport/mapping source with queued SDL input and recorded platform callbacks.
- [x] `src-csharp/OpenTyrian.Core/MouseCursor.cs` - all five pointer shapes/
  hotspots, bounded native 24x28 capture/restore including scanline crossing,
  real compressed sprite clipping/filtering, mouse/button gates, and native
  event-poll difference between normal and filtered cursor starts.
- [x] `tests/OpenTyrian.Core.Tests/MouseCursorTests.cs` - 2,560 exact native
  start/restore pixel hashes across five cursors, 64 edge/offscreen positions,
  normal/filter/inactive/disabled modes and two pitches; actual sprite sheet
  and `InputState` integration, queue polling gates, invalid shapes/surfaces.
- [x] `tests/native/build_mouse_reference.py` - unchanged full cursor source
  and four clipped sprite routines, synthetic compressed sprites and screen
  bytes; compares native capture/restore and real sprite rendering.
- [x] `src-csharp/OpenTyrian.Core/SdlNative.cs` - cdecl SDL2 resolver and
  checked audio conversion/device interop. Uses installed platform libraries
  or an explicit absolute `SDL2_LIBRARY_PATH`; no bundled native game logic.
- [x] `src-csharp/OpenTyrian.Core/SdlAudioSampleConverter.cs` - real native
  signed-8 mono 11025 Hz to signed-16 device-rate conversion, bounded CVT
  buffers and deterministic unmanaged cleanup; integrates with `SoundBank`.
  Shared CVT prefix offsets and maximum packed/MSVC storage sizes checked
  against native `sizeof`/`offsetof` before output comparison.
- [x] `src-csharp/OpenTyrian.Core/SdlAudioDevice.cs` - actual paused SDL2
  mono signed-16 device, native 44100 Hz/1024-frame request and frequency/
  buffer negotiation, rooted callback, real `AudioMixer` invocation, start/
  pause/close, channel cleanup, subsystem lifetime and construction rollback.
  `TryInitialize` preserves the native already-disabled short-circuit,
  initialization/open failure diagnostics and audio-disable policy, and starts
  successful devices; non-SDL mixer failures continue to propagate.
  Native callback faults are retained, output silenced, and rethrown by
  `ThrowIfFaulted`, start/pause/dispose. Host must check faults each frame.
  `TryInitializeOpl` creates a sample-rate-matched OPL2 chip and LDS player
  through the actual SDL device/mixer factory; native rhythm noise remains an
  injected platform random source. Full game-host lifetime wiring remains
  pending. `deinit_audio` is verified below.
- [x] `tests/OpenTyrian.Core.Tests/SdlAudioTests.cs` - 192 actual SDL
  conversion buffers (six rates, eight lengths, four signed patterns), all
  38 sound-bank conversions and PCM consumption, real dummy-driver OPL2/LDS
  initialization with sample playback through the callback, callback timing/
  start/pause/GC rooting/dispose, factory/callback failures and reopen.
- [x] `src-csharp/OpenTyrian.Core/TinyDirectory.cs` - managed sorted directory
  entries, indexed file/subdirectory navigation, file lookup, extension extraction, and
  native-compatible empty-directory/index failure handling.
- [x] `tests/OpenTyrian.Core.Tests/TinyDirectoryTests.cs` - temporary
  filesystem checks for directories-first sorting, file/directory metadata,
  hidden/trailing-dot extensions, nested and empty directories, and errors.
- [x] `tests/native/sdl_audio_reference.c` - native SDL CVT ABI validation
  and exact converted sample lengths/byte hashes using the same SDL2 runtime.
- [x] `src-csharp/OpenTyrian.Core/GameOptions.cs` - complete native game
  launch options/legacy tokens, help, diagnostic exits, argument permutation,
  network host/ports/name/player/delay state, Christmas precedence, and
  cheat/demo/joystick/audio flags. Integer-prefix behavior retained; undefined
  native integer/one-plus-delay overflow rejected with option diagnostics.
- [x] `tests/OpenTyrian.Core.Tests/GameOptionsTests.cs` - 51 native CLI
  scenarios compare complete state, reordered/uppercased arguments, stdout,
  stderr, exit codes, abbreviated/clustered options, legacy precedence,
  numeric prefixes/boundaries, plus caller ownership and overflow checks.
- [x] `tests/native/build_game_options_reference.py` - unchanged
  `JE_paramCheck` plus unchanged linked argument parser and recorded launch state.
- [x] `src-csharp/OpenTyrian.Core/SdlVideoDevice.cs` - actual SDL2 window,
  renderer, streaming RGB888 texture, palette mapping, managed 320x200
  primary/secondary/game surfaces, presentation/readback, display centering,
  fullscreen fallback/toggle, minimum-size enforcement, scaler changes,
  and deterministic native handle/subsystem cleanup. Uses existing managed
  nearest/Scale2x/Scale3x/HQ ports; all nine scaler choices have actual native
  SDL frame parity. Original-asset and physical-display gates remain pending.
- [x] `tests/OpenTyrian.Core.Tests/SdlVideoTests.cs` - 23 exact real SDL
  framebuffer hashes/window/viewport/fullscreen states against unchanged
  native video functions, all four layout modes and nearest factors 1..4,
  resize minima, out-of-range displays, clear, edge-scaler texture integration,
  unsupported scaler retention, owned surfaces, disposal and reopening.
- [x] `tests/native/build_sdl_video_reference.py` - unchanged full video
  source against real SDL2, deterministic nearest-scaler service, indexed
  source/palette fixtures and actual software-renderer framebuffer hashes.
  Nearest scaler fixture does not replace the separate Scale2x/Scale3x checks.
- [x] `src-csharp/OpenTyrian.Core/SdlInputPlatform.cs` - real SDL2 event
  queue union decoding/posting, owned UTF-8 buffers, ticks/delay, cursor and
  relative/system-scale services, real video resize/fullscreen integration
  and required joystick/network/quit callbacks. Frees ignored SDL-owned
  drop/extended-text pointers; owns event subsystem/marshal buffer lifetime.
- [x] `tests/OpenTyrian.Core.Tests/SdlInputTests.cs` - 640 real SDL event
  roundtrips compare native queue traces and every typed field; integration
  checks actual InputState key/text ordering, Alt+Enter fullscreen, resize,
  quit, SDL timer, required auxiliary callbacks and invalid/disposed inputs.
- [x] `tests/native/sdl_input_reference.c` - native 56-byte union ABI gate,
  real posted/polled focus/key/mouse/text/edit/user/quit event traces.
- [x] `src-csharp/OpenTyrian.Core/JoystickInput.cs` - typed assignments/
  device/state, signed analog thresholds/reduction/angle, axes/buttons/hats
  and two-bind maximums, poll/repeat/confirm/cancel, ordered menu key synthesis,
  native defaults, config load/save, exact code parsing/six-character format/
  comparison, and assignment capture with native axis/button/hat precedence,
  release tracking and input/display/PIT integration. Configuration assignment
  insertion, duplicate removal, two-slot rolling replacement, joystick
  selection, analog toggling and sensitivity/threshold adjustment are ported.
  Undefined integer/divide/code-number overflow throws; defined negative
  thresholds remain compatible.
- [x] `src-csharp/OpenTyrian.Core/SdlJoysticks.cs` - actual SDL joystick
  initialization/event-disable/enumeration/open/capabilities, config/defaults,
  poll/key-synthesis bridge to `SdlInputPlatform`, diagnostics, ignored gates,
  assignment persistence and native handle/subsystem teardown. Failed system
  initialization throws rather than silently disabling a requested subsystem;
  failed individual opens are warned and retain their neutral state slots.
- [x] `tests/OpenTyrian.Core.Tests/JoystickTests.cs` - 22 parsed/formatted
  codes, 484 equality pairs, 640 defaults, 4,608 native state snapshots over
  1,536 three-device frames, 1,536 analog/reduction traces, 32 exact config
  save/load outputs, eight detector cases (signed axes, thresholds, release,
  overriding hats/buttons, key/mouse cancellation), assignment routing,
  defaults/reset, joystick wrapping, analog gating and options-return cases,
  and invalid-input guards. Analog angles use a 1e-6 radian tolerance for
  platform math-library rounding; all integer/state/config/event ordering
  comparisons are exact.
- [x] `tests/OpenTyrian.Core.Tests/SdlJoystickTests.cs` - real synthetic
  SDL virtual device enumeration, axes/buttons/hats into managed state, actual
  SDL menu key-down/up queue, assignment save/reopen, ignored-device gates,
  disposal and post-close handle guards. Runs with SDL input integration checks.
- [x] `tests/native/build_joystick_reference.py` - unchanged joystick
  control/assignment/config/detection functions, linked native configuration
  serializer, scripted SDL device/tick/transitions and complete state traces.
- [x] `src-csharp/OpenTyrian.Core/SelectionMenus.cs` - gameplay/episode/
  difficulty selectors with native label bytes, navigation/wrap, hover/click
  bounds, disabled entries, mouse-over-key precedence, difficulty unlocks,
  sound/fade/keep-alive ordering and commit/cancel state. Shared managed
  presentation uses font shadows, full background copy, cursor save/restore
  (also on present failure), required palette/audio/episode/platform hooks,
  menu navigation geometry/rendering/help selection, planet rendering,
  pitch-aware native bitmap scaling, weapon-preview initialization, and
  ship-specification/upgrade-score presentations and navigation-map frame
  integration, next-level setup, item-upgrade submenu label/state building,
  keyboard configuration defaults, options return, and key rebinding, plus
  full-game, multiplayer, limited/options, SuperTyrian, upgrade, next-level and
  upgrade-completion and data-cube menu actions, and two-player device
  cycling in both directions. The central menu-selection dispatcher preserves
  its click/color setup, selected-row versus clicked-row arguments, submenu
  routing and post-dispatch player-item snapshot.
  Confirmed menu exits now also preserve conditional network halting and the
  native game-loaded/main-level state transition. The confirmation dialog
  reproduces native keyboard/mouse selection, animation brightness, frame
  ordering, cursor presentation, final sound and network quit packet/halt
  behavior.
- [x] `tests/OpenTyrian.Core.Tests/SelectionMenuTests.cs` - 120 native
  menu scenarios tracing complete draws/frame hashes, sounds, waits, fades,
  keep-alives and return/game state; native boundary and hidden difficulty
  checks, concrete managed font/cursor integration, validation and error
  cleanup; help selection, navigation planet rendering, weapon preview, and
  bitmap-scaling/presentation, score formatting, all upgrade submenu
  categories, confirmed/cancelled local/network quit transitions, and keyboard
  and mouse quit-dialog scenarios additionally match native traces. Keyboard
  configuration tests cover default reset, local/network return destinations,
  binding swaps, reserved keys and mouse cancellation. SDL input
  group also runs selectors with real events, modifiers and PIT fades. Main
  menu tests cover each navigation action, local/network exit destinations,
  two-player device cycling, upgrade snapshot isolation, cash handoff, Done
  selection, data-cube subpage/empty-state transitions, save/load paging,
  quick-save restoration, save-name popups, save-list disposal, and
  twelve exact native device-cycling traces. The original C menu dispatcher
  also matches managed dispatch callback order and state across all fourteen
  menu kinds plus an unknown-menu path. Upgrade-weapon power changes match
  native temporary-array and narrowed player-power state across 24 traces.
- [x] `tests/native/build_selection_menus_reference.py` - unchanged
  complete `menus.c` functions with asset-free traced presentation services,
  menu-help selection, navigation grid/dot/planet, bitmap-scale, item submenu,
  and complete quit-confirmation traces, and scripted keyboard/mouse/modifier
  input across 120 cases.
- [x] `tests/OpenTyrian.Core.Tests/UiControlTests.cs` - 256 native
  shadowed-gauge frame hashes/warnings, width/height unsigned underflow,
  clipping/pitch/color wrapping, partial-segment division order, zero divisor
  rejection and 16 keyboard/mouse/joystick polling/no-op cases.
- [x] `tests/native/build_ui_controls_reference.py` - unchanged native
  shadowed gauge/button/no-op functions and pixel/fill/shade primitives,
  asset-free surface/poll fixtures and complete warning/frame traces.
- [x] `src-csharp/OpenTyrian.Core/LegacySaveArchive.cs` - byte-exact
  2,502-byte legacy `tyrian.sav` cipher/four checksums and diagnostics,
  22 fixed-width 109-byte slots, little-endian signed fields, raw item/name/
  power bytes, Pascal strings, 100-byte availability tail and native
  editor-level save/load byte-order quirk. Decrypt returns only initialized
  payload, not native uninitialized checksum-tail stack bytes; invalid lengths,
  Pascal overflows and corruption throw. Also preserves native C-string XOR.
- [x] `src-csharp/OpenTyrian.Core/LegacySaveGame.cs` - typed legacy
  game state and slot save/load: native mode flags, player/last-item mapping,
  signed score to platform-width cash, per-player weapon powers, old sidekick
  compatibility, completed-episode transition, input/cube/difficulty/level
  state and required persistence/episode-loading callbacks.
- [x] `tests/OpenTyrian.Core.Tests/LegacySaveTests.cs` - 32 exact
  cipher/decoded buffers, 256 item mappings/power preservation, 16 complete
  serialized 22-slot archives, raw/decoded field checks, 12 XOR traces and
  native checksum collision plus five rejected corruption/diagnostic cases.
  All 22 slots across eight game modes compare complete bytes and loaded state
  (176 save/load cases). Real temporary-file persistence, callback failures,
  invalid slots/names, truncated buffers and malformed Pascal fields covered.
- [x] `tests/native/build_legacy_saves_reference.py` - unchanged
  cipher/XOR/item/slot-save/load functions and save/config record serialization
  regions, native model declarations, asset-free fixtures and corrupt-save
  exit/diagnostic oracle. Full configuration/new-format save functions remain
  unchecked; a verified extracted region is not whole-function completion.
- [x] `src-csharp/OpenTyrian.Core/GameRuntimeSettings.cs` - native
  processor-detail flags, game-speed-to-fast-play mapping and real managed PIT
  delay/frame-count controls; undefined asserted speed modes throw.
- [x] `tests/OpenTyrian.Core.Tests/RuntimeSettingsTests.cs` - 432 native
  processor/game-speed/prior-fast-play detail states and exact PIT targets,
  unknown detail/speed fallback preservation and invalid fast-play rejection.
- [x] `tests/native/build_runtime_settings_reference.py` - unchanged
  processor/game-speed functions and native PIT target functions with scripted
  ticks, no hardware/asset requirements.
- [x] `src-csharp/OpenTyrian.Core/GameConfiguration.cs` - native text
  configuration defaults, fullscreen/scaler/layout/shop state, eight keyboard
  bindings, required key-name services and actual SDL UTF-8 name adapter.
  File load/save preserves unrelated sections, emits missing-path diagnostics,
  uses durable flush and Unix user-only directory creation, and propagates
  permission/locked-file/write errors instead of returning silent failure.
- [x] `tests/OpenTyrian.Core.Tests/GameConfigurationTests.cs` - 32 native
  parsed states and exact serialized texts with real SDL names, all scaler/
  layout names, unknown settings and keys, optional fullscreen, case-insensitive
  SDL keys, unknown-binding removal and unrelated joystick sections. Managed
  default tests need no SDL library; real file persistence/missing defaults and
  locked-path/write error propagation verified.
- [x] `tests/native/build_game_config_reference.py` - unchanged native
  text load/save functions, linked configuration implementation and real SDL
  key-name functions; temporary stream fixtures avoid modifying native config.
- [x] `src-csharp/OpenTyrian.Core/GameSettingsStore.cs` - complete legacy
  configuration composition, exact 28-byte DOS codec, conditional seeded
  high-score initialization and availability defaults, volume clamping/callback,
  processor overrides, cached platform user directory, and durable saves.
  Overlong Unix user directories throw instead of truncating native UTF-8 paths;
  file errors propagate and invalid-size binary input is closed before defaults.
- [x] `tests/OpenTyrian.Core.Tests/SettingsStoreTests.cs` - eight complete
  native startup/save traces, exact DOS/encrypted archive output, missing and
  invalid-size settings, existing/default saves, detail/background overrides,
  bounded volumes, real persistence and locked-file failures.
- [x] `tests/native/build_settings_store_reference.py` - complete unchanged
  configuration load/save and processor functions with native RNG/default tables,
  isolated temporary streams and platform user-directory comparison.
- [x] `src-csharp/OpenTyrian.Core/SavedGameCatalog.cs` - versioned `.tyrsav`
  metadata and sorted/player-filtered catalogs, fourteen-byte display names,
  ten-byte level names, version compatibility and diagnosed invalid/truncated/
  missing files. Preserves native literal-backslash paths and 59-byte truncation;
  unsupported versions and unsafe/non-Latin-1 filenames are explicitly rejected.
- [x] `tests/OpenTyrian.Core.Tests/SaveMetadataTests.cs` - six native metadata
  cases and four real tinydir sorted/filter lists, retained compatibility-version
  rules, extension/directory exclusion, all 157 header truncation positions,
  corrupt/unsupported/missing/locked-file errors and native path limits.
- [x] `tests/native/build_save_metadata_reference.py` - unchanged metadata
  reader and list builder with actual tinydir enumeration and real file streams.
- [x] `src-csharp/OpenTyrian.Core/VersionedSaveGame.cs` - complete version-one
  save header and ID-based item payload, both players/current/previous items,
  native power/cash and mode/episode restoration, required initialization callback,
  real file persistence and shared metadata/path handling. Malformed/unsupported/
  missing-item/numeric-overflow input and native NUL-cipher corruption are explicit
  errors; invalid loads preserve prior state and invalid saves preserve prior files.
- [x] `tests/OpenTyrian.Core.Tests/VersionedSaveTests.cs` - 57 complete native
  byte/restored-state traces and seven native cipher truncations safely rejected,
  player/mode/episode/preserved-field boundaries, metadata/file integration,
  future-compatible version, truncation/invalid/cipher/missing-item failures and
  locked-file persistence errors.
- [x] `tests/native/build_versioned_saves_reference.py` - unchanged versioned
  save/load functions with numeric item tables and isolated temporary files.
  The native allocator fixture supplies a trailing NUL to bound otherwise
  undefined string reads past the loader's unterminated allocation.
- [x] `src-csharp/OpenTyrian.Core/AnimationPlayer.cs` - native animation
  palette/blank/frame ordering, final loop-delta exclusion, key interruption,
  required presentation/network callbacks and PIT wait integration. Invalid
  files/pages/frames throw instead of silently aborting; streams close on failures.
- [x] `tests/OpenTyrian.Core.Tests/AnimationPlayerTests.cs` - 72 complete
  native multi-page playback traces across start/speed/key interruption, frame
  hashes and ordered palette/presentation/events/network/waits. Managed timing,
  single-record boundaries, missing/bad/decode/presentation errors, ownership and
  real SDL palette/renderer/event interruption also verified.
- [x] `tests/native/build_animation_reference.py` - complete unchanged
  animation module and size-buffer decoder with real synthetic LPF files and
  traced presentation/input/network/timer boundaries.
- [x] `src-csharp/OpenTyrian.Core/JukeboxStarfield.cs` - all 15 native
  patterns, 1,000 stars, depth/pixel rendering, signed wrapping, seeded RNG,
  keyboard pattern/color/speed/pause controls and persistent initialization.
- [x] `tests/OpenTyrian.Core.Tests/JukeboxStarfieldTests.cs` - 21 native
  full starfield scenes, 840 frame hashes/state/RNG traces and 10,080 direct
  pattern results with exact floating-point bits; pause/modifier/bounds checks.
- [x] `tests/native/build_jukebox_stars_reference.py` - unchanged native
  starfield with synthetic indexed screen/input/timer boundaries.
- [x] `src-csharp/OpenTyrian.Core/Jukebox.cs` - complete jukebox control
  loop, music/sample mode, original title tables, automatic song fades/switches,
  text toggle and keyboard/mouse quit with 15-step palette fade. Concrete
  star/font/palette and locked music-controller/mixer/sample adapters included.
- [x] `tests/OpenTyrian.Core.Tests/JukeboxTests.cs` - 32 complete native
  control traces, exact music/sound title tables, concrete palette/star/font
  integration, music archive/controller/PCM/sample routing, disabled audio,
  volume restoration on failures, and real SDL presentation/events/timing.
- [x] `tests/native/build_jukebox_reference.py` - unchanged jukebox loop
  and audio title tables with traced audio/input/presentation/timer fixtures.
- [x] `src-csharp/OpenTyrian.Core/HqVideoScaler.cs` - HQ2x/HQ3x/HQ4x
  neighborhood/YUV detection, native six-bit RGB masking, ten exact unsigned
  interpolations, pitch/boundary handling and explicit input validation.
- [x] `src-csharp/OpenTyrian.Core/HqPatternKernels.cs` - readable generated
  managed pixel recipes for all 256 patterns at each factor; no native runtime
  dependency. Reproducible from the retained native compatibility tables.
- [x] `tools/generate_hq_kernels.py` - validated pattern/macro grammar,
  256-case coverage and balanced blocks; generator check matches formatted output.
- [x] `tests/OpenTyrian.Core.Tests/HqVideoScalerTests.cs` - exact native
  output bytes for 12,372 cases: all pattern seeds, RGB overflow/YUV thresholds,
  widths/heights/padding/borders and complete 320x200 frames at all three factors;
  output padding and malformed palette/factor/memory checks.
- [x] `tests/native/build_hq_scaler_reference.py` - complete unchanged
  HQ module with indexed/RGB/YUV fixtures and exact binary pixel output;
  inline declaration linkage normalized for C99/MSVC, arithmetic unchanged.
- [x] `src-csharp/OpenTyrian.Core/NetworkSession.cs` - native 16-packet
  acknowledged control queues, retry/keep-alive/timeout rules, delayed 28-byte
  states and XOR/retransmit recovery; complete version/player/delay/name/episode
  handshake with duplicate connection packets. Native promoted sync subtraction
  and reconstructed XOR headers retained. Invalid packets/I/O raise errors;
  network termination unwinds with typed exceptions; `Shutdown` composes the
  native graphical halt/sync/button/fade/transport-release/game-halt sequence.
- [x] `src-csharp/OpenTyrian.Core/SdlNetTransport.cs` - actual SDL2_net
  init/open/resolve/bind/send/receive, port/packet ABI marshalling, owned packet/
  socket cleanup and deterministic reopen. Shared resolver supports installed
  libraries or absolute `SDL2_NET_LIBRARY_PATH`, without bundled native game logic.
- [x] `src-csharp/OpenTyrian.Core/NetworkShutdown.cs` - native request flags
  and exact five-flag reset preserving the peer-menu flag; picture-2/error-font/
  ten-step palette presentation composed with verified managed rendering/PIT services.
- [x] `tests/OpenTyrian.Core.Tests/NetworkSessionTests.cs` - nine complete
  native control/state traces across delays 1..7, packet loss/XOR/retransmit,
  duplicate controls, retries, resets and 65,542 control sequence transitions;
  32 complete handshake/error/duplicate/Escape traces, 32 graphical halt traces
  across every error/no-sync/timeout/already-synced/acknowledgment variant and
  32 request-mask resets. Actual SDL picture/font/fades/keyboard acknowledgment;
  malformed/queue/transport/cancellation/timeout/quit/keep-alive and cleanup checks.
- [x] `tests/native/build_network_reference.py` - unchanged network
  packet/queue/state/connect/halt/request functions with deterministic tick/UDP/
  input/presentation fixtures; process exit replaced only at the host boundary.
- [x] `tests/OpenTyrian.Core.Tests/SdlNetTests.cs` - 128 actual loopback
  packet/echo byte/channel comparisons, two concurrent player/episode/name
  handshakes, real acknowledged controls and delayed/XOR state exchange,
  invalid/disposed/unbound guards and ownership/reopen.
- [x] `tests/native/sdl_net_reference.c` - actual SDL2_net loopback
  reference for packet bytes/channel/native port resolution and bidirectional echo.
- [x] `src-csharp/OpenTyrian.Core/LevelNetwork.cs` - integrated native
  delayed gameplay payload/request processing on `LevelEvents` and actual
  `NetworkSession`; XOR requests, pause/setup context, skip timing, NortShip
  items and exact overlapping native desync diagnostic offsets. Location
  mismatch throws before applying requests; setup failure clears peer-menu context.
- [x] `tests/OpenTyrian.Core.Tests/LevelNetworkTests.cs` - 5,120 complete
  native gameplay traces, all request pairs, reset/end/non-network gates,
  menu halt, coordinate diagnostic combinations and uint16 truncation.
  Actual two-peer SDL2_net gameplay frames apply delayed pause/NortShip identically;
  SDL font/shade pixels and callback forwarding checked by shutdown integration.
- [x] `tests/native/build_level_network_reference.py` - unchanged full
  gameplay network update with deterministic packets/update and traced menu/font
  callbacks; verified item indices are supplied from the existing database fixture.
- [x] `src-csharp/OpenTyrian.Core/GamePause.cs` - native pause text,
  half-volume/full-volume, modifier-key filtering, mouse/joystick input,
  PIT timing and acknowledged network pause/resume; input/audio cleanup on errors.
  `InputState` waits now support cancellation without changing normal polling order.
- [x] `tests/OpenTyrian.Core.Tests/GamePauseTests.cs` - twelve unchanged
  native pause traces covering normal/super/network/mouse/remote resume and all
  Ctrl/Alt modifiers; error/cancellation restoration. Actual SDL font/event input
  and paired SDL2_net acknowledged pause/resume paths.
- [x] `tests/native/build_pause_reference.py` - unchanged complete pause
  function with scripted input/packet services and native floating-point PIT targets;
  compares externally ordered screen/input/time/volume effects, protocol primitives
  independently covered by full network oracles.
- [x] `src-csharp/OpenTyrian.Core/PlayerLifecycle.cs` - native death
  explosion/RNG/sound cadence, arcade life decrement/respawn, Galaga/episode armor,
  shield/HUD and linked-player resets, network all-gone state and continuous-death
  cheat. Shares actual `PlayerCombat`, `LevelEvents`, `SceneEffects` and player state.
- [x] `tests/OpenTyrian.Core.Tests/PlayerLifecycleTests.cs` - 8,192
  full unchanged native state/explosion/sound/HUD/RNG comparisons, both players,
  two pitches and combinations of alive/exploding/lives/arcade/Galaga/episode/
  network/end-wait/constant-death conditions; actual HUD armor clamping preserved.
- [x] `tests/native/build_player_lifecycle_reference.py` - unchanged
  full death/respawn function and original native effects/HUD/RNG code; deterministic
  player/life fixtures and exhaustive binary-state/text/hash trace.
- [x] `src-csharp/OpenTyrian.Core/PlayerMovement.cs` - typed motion state,
  inertia/friction/history, linking/analog turret turning, level-end warp/trails,
  linked turret projectiles and all five sidekick styles/attachment controls.
  Integrated with actual players/combat/level/item/projectile/effect state;
  street-fighter code detection now has a concrete `PlayerWeapons` integration.
- [x] `tests/OpenTyrian.Core.Tests/PlayerMovementTests.cs` - 4,096
  complete native linking/state/float-bit/history/warp-trail/projectile/sidekick/
  attachment/sound/explosion/charge/ammunition/animation/HUD traces; real projectile/item integration and exact
  native modulo/type widths/rounding/thresholds across control/mode combinations.
- [x] `tests/native/build_player_movement_reference.py` - unchanged
  linking/movement/trail/sidekick/firing/drawing functions and real native typed
  items/projectiles; complete charge/ammo HUD pixels use unchanged native gauge
  drawing. External sprite/effect service boundaries are traced; special-command
  recognition additionally compares 42,432 full native progress/execution states.
- [x] `src-csharp/OpenTyrian.Core/PlayerWeapons.cs` - native front/rear
  single/two-player firing, linked-player suppression, Galaga power, mode tables,
  per-port cooldowns and owner/trigger/cooldown/literal-index superbombs.
  Player-two charge levels/graphics/HUD, difficulty-based wait, sidekick firing/
  ammo refill/charge reset/gauges, and sidekick animation/draw/charge timing are
  compared in the same 4,096 native traces.
  Weapon-mode latches, normal wraparound, all seven arcade special pairs,
  shared special/rear multiposition reset and rear-pattern indicator draw calls
  are verified in that oracle with native item-ID lookup.
  Ship rendering adds 2,048 native boundary/banking/shadow/invulnerability/
  DragonWing sidekick/NortSpark projectile and RNG traces, plus actual compressed
  sprite copy/blend/shadow pixels and padded-surface checks.
  All 26 keyboard combos, normal/SuperTyrian ship maps, both player owners,
  release/no-direction/diagonal/repeat/wrong-direction behavior and native
  executed-command overwrites are verified. Invalid native ship-table index
  14 and corrupt progress throw rather than accessing past the tables.
- [x] `src-csharp/OpenTyrian.Core/PlayerSpecialWeapons.cs` - special completion
  and per-frame updates share actual players, projectiles, enemy/hostile shots,
  combat/HUD, background filters and recognized commands. Shield/armor costs,
  weapon/repulsor/attractor/flare/Zinglon/astral/spray/invulnerability/repair/
  sidekick effects, waits and held buttons preserve native widths and ordering.
  Concrete readiness/icon/gauge adapters use managed sprite libraries.
- [x] `tests/OpenTyrian.Core.Tests/PlayerSpecialWeaponsTests.cs` - 20,480 complete
  native traces: 4,096 special completion, 4,096 special updates, and 4,096
  native-ordered full attack routines. Compares every projectile, all enemy/
  hostile-shot velocity changes, both player states, sounds, filters, HUD/main
  frame hashes including padding, RNG and actual sidekick initialization.
  Separate concrete compressed/table sprite checks cover readiness, option
  icons and rear-pattern buttons without original assets.
  Another 4,096 individual player scenarios and 4,096 consecutive paired-frame
  scenarios compare death/respawn, linking/commands, movement/history,
  ship/sidekick draws, all attacks, complete projectiles, HUD/frame hashes and
  RNG. They cover local/remote delayed network input and reset gates, demo text,
  shared primary-player special coordinates, Galaga mode changes, parallax
  offsets/positions, previous-map state and signed warp overflow.
- [x] `src-csharp/OpenTyrian.Core/PlayerFrame.cs` - enclosing player and paired
  frame integration with actual input, death/respawn, movement, command/link,
  rendering, weapons and delayed network queues. Updates managed background
  parallax state and preserves shared buttons, primary special coordinates,
  request/command resets and native order. Required presentation callbacks
  expose asset-dependent draws; playable level-loop integration remains pending.
- [x] `tests/native/build_player_special_reference.py` - unchanged native special/
  attack/sidekick initialization/HUD/gauge/brightening routines composed with
  actual native typed items and player projectiles. Extended with unchanged
  player/paired-frame, death and purple-ball routines. Frame input uses a bounded
  keyboard/demo fixture and network packet shim; actual native input and queues
  have separate differential coverage. Frame explosion requests are deferred
  for state comparison; explosion/death animation uses the lifecycle oracle.
  Sprite calls are traced; actual HUD/brightening pixels are compared.
- [x] `src-csharp/OpenTyrian.Core/PlayerInput.cs` - actual keyboard/mouse/joystick
  collection, analog linking, menu/pause requests, fixed native movement speed,
  vertical inversion, demo recording and binary key playback, acceleration/
  short narrowing/clamping, and prepared network input payload/reset semantics.
- [x] `tests/OpenTyrian.Core.Tests/PlayerInputTests.cs` - 16,384 complete native
  traces with real native/managed joystick polling, mouse distance scaling,
  all input device/mode/local-owner gates, exact float bits, short overflow,
  recording wait wrap and bytes, playback EOF, service order and all packet bytes.
  Malformed bindings, missing replay/prepared state, button length and local
  owner validation are checked. Another 16,384 native replay traces cover wait
  countdown, consecutive zero-wait records, all key/button bits, EOF/truncated
  records, stream positions, and real collector/level-end integration. Managed
  reads also handle fragmented streams; demo headers/loading now use `DemoSession`.
- [x] `tests/native/build_player_input_reference.py` - unchanged collector,
  joystick polling/axis/angle and mouse/viewport distance functions; deterministic
  SDL events/devices, scripted and actual replay, and big-endian network writes.
  Integrated with actual `PlayerShots` and typed items; invalid native table
  indices produce explicit errors instead of undefined accesses.
- [x] `src-csharp/OpenTyrian.Core/DemoSession.cs` - demo selection/wrap/fallback,
  original and compact header loading with native numeric item-ID resolution,
  episode initialization, input wait/key resets and recording headers/filenames.
  Owns its stream for playback/recording; callers supply it to `PlayerFrame`.
  Missing files report an open warning and disable playback. Invalid item IDs
  and truncated mandatory headers throw. Recording creates files atomically;
  exhausted slots fail explicitly rather than looping indefinitely.
- [x] `tests/OpenTyrian.Core.Tests/DemoSessionTests.cs` - 15 native load attempts
  across regular/wrapping/missing scenarios and 64 exact 30-byte recording
  headers, filenames, wait/key resets and occupied-slot preservation. Checks
  compact headers, partial/absent waits, high-bit/NUL name bytes and actual
  managed episode archive initialization; all mandatory header truncations,
  unresolved item IDs and full recording-slot exhaustion are guarded.
- [x] `tests/native/build_demo_reference.py` - unchanged `load_next_demo` and
  `record_level_demo`, native typed item parser/ID lookup, actual stdio/filesystem
  reads/writes, and a traced episode initializer. No original assets included.
- [x] `src-csharp/OpenTyrian.Core/EnemyCollisions.cs` - player/enemy collision
  damage caps and impulses, invulnerable enemy armor, linked enemy destruction
  and linked/unlinked armor pickups, composed with actual player combat/HUD
  and small/large/repeating explosion state. Pickup dispatch and enclosing
  player collision iteration now use `PlayerPickups`; no projectile-kill reward logic
  is incorrectly reused for collision destruction.
- [x] `tests/OpenTyrian.Core.Tests/EnemyCollisionTests.cs` - 8,192 native
  scenarios compare both player states, all 100 enemies, 200 explosions,
  20 repeating explosions, sounds, complete padded HUD hashes and RNG at
  pitches 320/327. Covers armor 255, damage caps, signed impulse arithmetic,
  link zero/255/exact/offset/group rules, clipping, large/ground effects,
  pickup rounding/caps and malformed indices/types/values.
- [x] `tests/native/build_enemy_collision_reference.py` - unchanged collision
  damage, linked destruction and armor pickup functions with actual native
  player damage, armor/shield drawing and explosion setup. Deterministic
  enemy definitions/state require no original assets.
- [x] `src-csharp/OpenTyrian.Core/PlayerPickups.cs` - network/local player
  names, arcade/sidekick/special/front/rear rewards, score items, bonus-level
  triggers and the complete ordered player collision dispatcher. Composes
  actual combat, armor HUD, item-ID resolution, weapon upgrades, shot creation,
  explosions, and shared enemy state. Text/options/song presentation uses
  explicit callbacks; the enclosing playable level loop remains pending.
- [x] `tests/OpenTyrian.Core.Tests/PlayerPickupTests.cs` - 8,192 native
  direct reward and complete collision-loop scenarios compare names/text,
  song/draw ordering, both players and last-item snapshots, enemy availability,
  actual effects, projectiles, sound queues, padded HUD pixels and RNG.
  Includes unsigned negative shared rewards, wrapped cash, Galaga recruitment,
  all arcade rows, sidekick tiers, unconsumed rear-weapon rewards, Latin-1 names,
  and explicit malformed text/table/index checks.
- [x] `tests/native/build_pickups_reference.py` - unchanged native player
  names, pickup routines and collision dispatch, composed with actual weapon
  upgrades, player damage, armor drawing, shots and explosion setup. Retains
  the native unsigned `COUNTOF` promotion rather than size_t division.
- [x] `src-csharp/OpenTyrian.Core/GameHud.cs` - complete in-game cash,
  special icon, lives, local/network names, superbombs and cheating display
  ordering plus cube shadows/hue. `GameHudPresentation` composes actual
  compressed sprites, option shapes and font/shadow rendering. Native life
  subtraction underflow and signed numeric display are retained.
- [x] `tests/OpenTyrian.Core.Tests/GameHudTests.cs` - 4,096 unchanged native
  HUD/cube call traces across mode combinations, all byte life counts,
  native-width cash, bombs, special icons and name precedence. Synthetic
  compressed sprites/font tables additionally verify concrete presentation
  pixels, full shadows, text width and NUL termination; invalid ownership and
  special indices fail explicitly.
- [x] `tests/native/build_game_hud_reference.py` - unchanged native
  `JE_getName`, `JE_inGameDisplays`, `JE_drawCube` with deterministic drawing
  and width traces, no original assets.
- [x] `src-csharp/OpenTyrian.Core/GameGamma.cs` - consumes F11 once,
  resets `NewKey`, cycles gamma and republishes a corrected copy of the
  current picture palette through actual managed palette RGB/YUV mapping.
  `PaletteSystem.GammaChannel`/`CorrectGamma` preserve roundf half-away-from-zero,
  float arithmetic, clamping and all 256 byte gamma values.
- [x] `tests/OpenTyrian.Core.Tests/GameSettingsTests.cs` - all 256 gamma
  settings across every channel byte, 1,024 palette/hotkey states and 512
  high-score sorting scenarios compared against unchanged C. Verifies ties,
  signed scores, selected three-slot groups, six full groups and retention
  of unrelated slot fields. Invalid palettes/factors/group bounds fail explicitly.
- [x] `tests/native/build_game_settings_reference.py` - unchanged native
  gamma/channel/hotkey and score-group/full-table sorting routines; exact
  palette hashes, key flags and score/name/difficulty/retained-field traces.
- [x] `src-csharp/OpenTyrian.Core/PlayerProgression.cs` - new-game player
  initialization, snapshot ordering, native-width shop costs and triangular
  upgrades, full item-inclusive scores and score-driven difficulty increases.
  Uses existing save-state/player/item ownership and preserves RNG, initial
  mode resets and player-two rear/sidekick defaults. Invalid weapon power,
  item indices and undefined native adjusted-score overflow are explicit errors.
- [x] `tests/OpenTyrian.Core.Tests/PlayerProgressionTests.cs` - 512 native
  new-game/cost/score scenarios and 147,456 difficulty evaluations across
  nine initial difficulties and score threshold boundaries. Verifies real
  known-item resolution, last-item timing, life aliases, retained cash,
  wrapped totals, ship armor, RNG and 72 signed/native-long upgrade cases.
- [x] `tests/native/build_player_progression_reference.py` - unchanged
  native initialization, shop cost/value, triangular upgrades, total scores
  and difficulty functions composed with real typed-item loading and RNG.
- [x] `src-csharp/OpenTyrian.Core/TextGlow.cs` - ordered four-direction
  glow shadows, complete rising/falling brightness loops, warning hue,
  input-controlled frame delays, network keep-alive and brightness reset.
  Concrete `TextGlowHost` composes actual font, input and PIT timer services.
- [x] `tests/OpenTyrian.Core.Tests/TextGlowTests.cs` - 2,048 consecutive
  native glow calls cover initial skipped animation, all three fonts,
  brightness gates, warning hue, button timing, empty text and complete
  ordered delay/draw/input/network/present/wait traces. Concrete synthetic
  glyph/input/timer integration verifies pixels and skip timing; cancellation
  is explicit.
- [x] `tests/native/build_text_glow_reference.py` - unchanged native
  `JE_outTextGlow` with deterministic presentation/input/network services.
- [x] `src-csharp/OpenTyrian.Core/GameTextEffects.cs` - native timed
  warning stripes, color/direction wrapping, presentation and warning-sound
  countdown through actual surfaces/PIT timers; text-window erase ordering,
  lifetime reset and real option-shape/font presentation.
- [x] `tests/OpenTyrian.Core.Tests/GameTextTests.cs` - 32,768 native
  warning updates cover every initial color and signed change, timer gates,
  sound cadence and full padded-surface hashes. Forty text-window updates
  compare erase/draw ordering, empty/NUL/Latin-1 text and lifetime state;
  concrete sprite/font window pixels are independently checked.
- [x] `tests/native/build_game_text_reference.py` - unchanged
  `JE_updateWarning` and `JE_drawTextWindow`, actual rectangular fills,
  PIT-derived timer targets, warning state and text presentation traces.
- [x] `src-csharp/OpenTyrian.Core/CharacterGlow.cs` - character-position
  and brightness wave animation, missing glyphs/spaces, native 16-bit
  position narrowing, black shadows, shared glow delay/warning hue state,
  skip input, escape, warning updates and network/presentation ordering.
  Concrete host uses actual font/sprite/input/PIT/warning services; text
  beyond the native 60-character capacity is an explicit error.
- [x] `tests/OpenTyrian.Core.Tests/CharacterGlowTests.cs` - 1,024 native
  traces across fast/animated, warning/last banks, initial/dynamic escape,
  input skipping, unsupported/high-byte glyphs, long spaces and 60 characters.
  Actual managed glyph/shadow pixels, input consumption, scheduled-delay
  retention after skip, warning stripes/timing and fast partial shading
  are verified together.
- [x] `tests/native/build_character_glow_reference.py` - unchanged
  `JE_outCharGlow` and original font character map with deterministic
  glyph/width/shadow/input/warning/timer/network presentation traces.
- [x] `src-csharp/OpenTyrian.Core/GameHelp.cs` - complete single/two-player
  in-game help layouts, help-box state, shaded panel, sprite labels, centered
  continue text and ordered input/mouse/presentation/joystick/network wait.
  Concrete host composes actual help/font/shape/input/cursor services on an
  explicit primary surface instead of temporarily changing a global screen
  pointer; text-window erase ticks become one after successful completion.
- [x] `tests/OpenTyrian.Core.Tests/GameHelpTests.cs` - 32 native help
  scenarios cover both layouts, keyboard/mouse completion and repeated
  wait frames with exact drawing/input/cursor/network order. Concrete
  font/help/shape pixels, help-box state and actual input/timing integration
  are checked; cancellation is explicit.
- [x] `tests/native/build_game_help_reference.py` - unchanged native
  `JE_inGameHelp`, deterministic presentation/input traces, help-box state,
  text-window lifetime and screen-pointer restoration.
- [x] `src-csharp/OpenTyrian.Core/LevelResults.cs` - complete end-level
  unlocks, difficulty adjustment, item/level snapshots, completed/exiting/
  bonus text, cash/kill percentages, wrapped editor-level rewards, cube
  animations, skip/continue waits, audio selection and fade/clear flow.
  Composes actual progression, glow, font/sprites, palette, input and timers.
  Native unlock offsets that overlap the four regenerated checksum bytes
  use explicit transient scratch rather than corrupting managed save state.
- [x] `tests/OpenTyrian.Core.Tests/LevelResultsTests.cs` - 512 unchanged
  native result traces compare all mode/alive/timer/episode gates, item
  snapshots/unlock bytes, integer kill percentage, cube cap/frame sequence,
  skip input and final waits. Concrete font/glow/palette/input/timing/audio
  integration and explicit invalid unlock bounds are checked.
- [x] `tests/native/build_level_results_reference.py` - unchanged
  `JE_endLevelAni` with actual native typed-item progression/difficulty
  routines; deterministic unlock, glow/cube/input/network/audio traces.
- [x] `src-csharp/OpenTyrian.Core/EpisodeTransition.cs` - next-episode
  availability/wrap selection, high-score/credits gates, random SuperCarrot/
  BananaBlast reward, item snapshots and new-level flags; concrete title/
  palette/song/input/fade presentation. Real `EpisodeState` archive loading
  is integrated; high-score entry and credits playback remain explicit
  callbacks until their own conversions are complete.
- [x] `tests/OpenTyrian.Core.Tests/EpisodeTransitionTests.cs` - 1,024
  unchanged native transitions compare availability masks, repeat/high-score/
  credits gates, random reward/item snapshots, exact RNG and ordered
  title/input/network/fade calls. Uses real synthetic episode archives;
  concrete font/title pixels, palette fading and input/audio callbacks
  are checked without original assets.
- [x] `tests/native/build_episode_transition_reference.py` - unchanged
  `JE_nextEpisode` and `JE_findNextEpisode` plus actual typed-item/RNG
  integration; deterministic transition presentation and item-state traces.
- [x] `src-csharp/OpenTyrian.Core/EpisodePictureDisplay.cs` - episode picture
  and PCX-palette branch, including native constant-play and host-action order.
- [x] `tests/OpenTyrian.Core.Tests/EpisodePictureDisplayTests.cs` - 256
  section parsing, palette mapping, picture loading and presentation traces.
- [x] `tests/native/build_episode_picture_reference.py` - standalone oracle
  generated from the unchanged native episode-picture routine.
- [x] `src-csharp/OpenTyrian.Core/EpisodeWarningDisplay.cs` - encrypted
  episode-warning lines, native frame-count parsing/narrowing, warning state
  setup and presenter handoff.
- [x] `tests/OpenTyrian.Core.Tests/EpisodeWarningDisplayTests.cs` - 256
  early-return, state, encrypted-line and presentation-order cases.
- [x] `tests/native/build_episode_warning_reference.py` - standalone oracle
  generated from the unchanged native episode-warning routine.
- [x] `src-csharp/OpenTyrian.Core/EpisodeLevelJump.cs` - conditional episode
  jump gates, C-style destination parsing and native byte narrowing.
- [x] `tests/OpenTyrian.Core.Tests/EpisodeLevelJumpTests.cs` - 2,048
  command, state, parser and jump-gate compatibility cases.
- [x] `tests/native/build_episode_level_jump_reference.py` - standalone
  oracle generated from the unchanged native episode-level jump routine.
- [x] `src-csharp/OpenTyrian.Core/EpisodeModeConfiguration.cs` - Galaga,
  extra-game and SuperTyrian flags and player loadout configuration.
- [x] `tests/OpenTyrian.Core.Tests/EpisodeModeConfigurationTests.cs` - 256
  seeded mode, player-item and state-transition compatibility cases.
- [x] `tests/native/build_episode_mode_reference.py` - standalone oracle
  generated from the unchanged native episode mode routine.
- [x] `src-csharp/OpenTyrian.Core/EpisodeItemAvailability.cs` - encrypted
  episode shop rows, typed ID lookup, optional random offers, and item-screen
  callback.
- [x] `tests/OpenTyrian.Core.Tests/EpisodeItemAvailabilityTests.cs` - 15
  shop-setting/input-length scenarios, unchanged-native state/stream traces,
  and a deterministic random-offer integration case.
- [x] `tests/native/build_episode_item_availability_reference.py` - standalone
  oracle generated from the unchanged native episode shop reader.
- [x] `src-csharp/OpenTyrian.Core/MapTransition.cs` - upper, lower, and
  horizontal picture reveals using the native per-frame indexed-pixel order.
- [x] `tests/OpenTyrian.Core.Tests/MapTransitionTests.cs` - 16 native frame
  traces across both common pitches, all transition branches, key skipping,
  constant-play short-circuiting, and network mode.
- [x] `tests/native/build_map_transition_reference.py` - standalone oracle
  generated from the unchanged native map-transition function.
- [x] `src-csharp/OpenTyrian.Core/EpisodePresentation.cs` - episode
  presentation command routing and native fade/clear/palette ordering.
- [x] `tests/OpenTyrian.Core.Tests/EpisodePresentationTests.cs` - 40
  command, network-state, constant-play, callback and completion-result traces.
- [x] `tests/native/build_episode_presentation_reference.py` - standalone
  oracle generated from the unchanged native episode presentation dispatcher.
- [x] `src-csharp/OpenTyrian.Core/EpisodeCompletion.cs` - encrypted completion
  hints, score/cash lines, SuperTyrian unlock state and presentation callbacks.
- [x] `tests/OpenTyrian.Core.Tests/EpisodeCompletionTests.cs` - 48 native
  scenarios covering hint offsets, scores, early returns, unlocks, graphics,
  network synchronization and presentation order.
- [x] `tests/native/build_episode_completion_reference.py` - standalone oracle
  generated from the unchanged native episode-completion routine.
- [x] `tests/native/core_reference.c` - asset-free reference harness linked
  with the unchanged native parser and RNG; compiled with MSVC x64.

## Verification scope and remaining integration

The converted functions below are integrated into the core library and
regression harness, not yet into playable game flows. Module file dispositions
identify verified counterparts, not completion of their remaining gameplay
callers; whole-game integration remains a separate release gate.
Full native game builds and runtime parity still require the SDL
development dependencies and separately supplied Tyrian assets. The supplied
Tyrian 2.1 assets have verified real-asset extractor parity, but end-to-end
gameplay and whole-game runtime parity remain outstanding. MSVC is
installed outside PATH and can compile asset-free reference programs via
`vcvars64.bat`. The unchanged full native SDL2/SDL2_net Windows game build and
asset-free `--help` path have been verified; gameplay still requires external
assets and the outstanding runtime gates.
The standalone managed extractor is integrated end-to-end; its native
source-region oracle needs no SDL runtime. Real-asset extraction was compared
against the unchanged native extractor for all eight categories. See the
workflow for intentional output-failure deviations.

The RNG's native float expressions can round to 1.0f even in `mt_rand_lt1`.
Preserve this rather than clamping or changing the sequence. The animation
decoder also preserves the native truncated-long-stop behavior: an incomplete
word after `0x80` is treated as stop. These are baseline quirks, not claims of
strict exclusive floats or comprehensive malformed-file validation.

New managed functions/helpers are tracked through their file entries and
these implementation inventories:

| New source | Implementations |
| --- | --- |
| `ArgumentParser.cs` | Constructor, `Next`, `Permute`, `ParseShort`, `ParseLong`, `FindCharacterOrEnd`; `OptionDefinition` and `ParsedOption` records |
| `AnimationDecoder.cs` | `Decode`; `FrameSize` contract |
| `BinaryFile.cs` | `Length`, `ReadBytes`, `ReadExactly`, `ReadBoolean`, `ReadUInt8`, `ReadInt8`, `ReadUInt16`, `ReadInt16`, `ReadUInt32`, `ReadInt32`, `WriteExactly`, `WriteBoolean`, `WriteBytes`, `WriteUInt8`, `WriteInt8`, `WriteUInt16`, `WriteInt16`, `WriteUInt32`, `WriteInt32`, `WritePcxHeader` |
| `ArgumentParserTests.cs` | `Run`, `ShortClustersAndPermutation`, `ArgumentsAndAbbreviations`, `EndOfOptions`, `Errors`, `IndependentParsers`, `CheckError`, `Equal` |
| `AnimationDecoderTests.cs` | `Run`, `Equal` |
| `BinaryFileTests.cs` | `Run`, `BytesEqual`, `Equal`, `FragmentedStream.Read` |
| `NativeCompatibilityTests.cs` | `Run`, `Execute`, `Normalize`, `Equal` |
| `core_reference.c` | `float_bits`, `main` |
| `Sprites.cs` | `SpriteTable.Get`, `Exists`, `Width`, `Height`, `Clear`, `LoadFile`, `Load`, `Blit`; `SpriteColors.Apply`; `SpriteSheet.Clear`, `LoadFile`, `Load`, `Blit`, `Blit2x2`, `ReadByte`; `MainShapeTables.Load`, `Clear`; sprite/effect types and table/sheet construction |
| `FontRenderer.cs` | Constructor, `SpriteIndex`, `TextWidth`, `Center`, `DrawHue`, `DrawBlend`, `DrawDark`, `DrawShadow`, `DrawFullShadow`, `Draw`, `CreateCharacterMap`; font/alignment enums |
| `SpriteTests.cs` | `Run`, `TestFailures`, `TestFileLoading`, `TestMainShapes`, `Check`, `Throws` |
| `FontTests.cs` | `Run`, `Check` |
| `DataFiles.cs` | Constructor, `Directory` getter, `Open`, `OpenWarn`, `OpenRequired`, `Exists`, `Resolve` |
| `ItemDataExtractor.cs` | `Run`, `PrintUsage`, `Extract`, `ReadLegacyData`, `WriteCategory`, `ReadShot`, `ReadWeapon`, `ReadSpecial`, `ReadGenerator`, `ReadShip`, `ReadSidekick`, `ReadShield`, `ReadEnemy`; `LegacyReader` constructor, `U8`, `S8`, `U16`, `S16`, `Values`, `Name`; `Record` constructor, `Text`, `Number`, `Numbers`, `Finish` |
| `ExtractorTests.cs` | `Run`, `BuildFixture`, `TestCommandLine`, `TestDataFiles`, `TestFailures`, `Execute`, `CategoryPath`, `ReadCategory`, `Check` |
| `build_extractor_reference.py` | `without_includes`, `build_source`, `main` |
| `MersenneTwister.cs` | `Seed`, `NextUInt32`, `NextFloatInclusive`, `NextFloatExclusive` |
| `SizeBuffer.cs` | Constructor, `Fill`, `CopyFrom`, `Seek`, `ReadByte`, `ReadWord`, `CanAccess` |
| `Configuration.cs` | `ConfigOption` constructor, `CString`, `SetValue`, `AddValue`, `RemoveValue`; `ConfigSection` constructor, `GetOption`, `SetOption`, `GetOrSetOption`, `AppendOption`, `TryGetString`, `GetOrSetString`, `SetBoolean`, `TryGetBoolean`, `GetOrSetBoolean`, `SetInt32`, `SetUInt32`, `TryGetInt32`, `TryGetUInt32`, `GetOrSetInt32`, `GetOrSetUInt32`, `TryGetInteger`; `Configuration.Clear`, `ThrowOutOfMemory`, `AddSection`, `FindSections`, `FindSection`, `FindOrAddSection`, `Parse`, `IsWhitespace`, `IsEnd`, `CharacterAt`, `ReadDirective`, `TryReadField`, `HexDigit`, `Write`, `WriteField` |
| `EncryptedText.cs` | `Decrypt`, `ReadPascalString`, `SkipPascalString` |
| `AnimationFile.cs` | Constructor, `FindPage`, `LoadPage`, `RenderFrame`, `ReadPageHeader`, `Dispose`; palette/page-header record types |
| `IndexedSurface.cs` | Both constructors, `Pixel`, `PixelCross`, `Rectangle`, `FillRectangle`, `FillRectangleSize`, `ShadeRectangle`, `BrightenRectangle`, `SegmentedGauge`, `ValidRectangle` |
| `PaletteSystem.cs` | Constructor, `Load`, `Expand`, `SetPalette`, `SetSolid`, `InitializeFade`, `InitializeSolidFade`, `StepFade`, `FadePalette`, `FadeSolid`, `FadeBlack`, `FadeWhite`, `RunFade`, `SetColor`, `RgbToYuv`, `ValidateRange` |
| `PictureLoader.cs` | `LoadPcx`, `LoadPicture`, `DecodeRle`, `ReadByte`, `ValidateScreen` |
| `VideoScaler.cs` | `SetByName`, `Scale32`, `Scale16`, generic `Scale`; scaler-definition/algorithm types |
| `PlayerState.cs` | `PlayerItems.Copy`, `PlayerState` constructor, `Cash` and `Lives` accessors; `PlayerRules.CalculatePurpleBalls`, `PowerUpWeapon`, `GotPurpleBall`, `AllDead`, `AllAlive`; weapon/sidekick/item model types |
| Core tests `Program.cs` | Entrypoint, dedicated SDL audio/runtime-platform/SDL2_net modes, `Run`, `TestDefaultSeed`, `TestPublishedVectors`, `TestSeedTruncationAndRepeatability`, `TestStateWraparound`, `TestFloatBounds`, `TestSizeBufferReadAndWrite`, `TestSizeBufferBoundsAndRecovery`, `TestSizeBufferCopyFailures`, `TestSizeBufferSeekModes`, `AssertSequence`, `AssertEqual`, `AssertThrows` |
| `ConfigurationTests.cs` | `Run`, `Equal`; captured diagnostic/exception test for managed OOM handling |
| `AssetReaderTests.cs` | `Run`, `PutWord`, `Check`, `ExpectInvalid` |
| `IndexedSurfaceTests.cs` | `Run`, `Equal` |
| `PaletteTests.cs`, `PictureLoaderTests.cs`, `VideoScalerTests.cs`, `PlayerRulesTests.cs` | Each defines `Run` and `Check` |
| Game/extractor `Program.cs` | Top-level host entrypoints; game remains a scaffold, extractor calls `ItemDataExtractor.Run` |
| `LevelArchive.cs` | `LevelArchive.Analyze`; `EpisodeState` constructor, `Initialize`, `Scan`, `FindNext` |
| `BackgroundRenderer.cs` | `BlitRow`, `DrawFirst`, `DrawSecond`, `DrawThird`, `MoveLayer`, `DrawLayer`, `CheckSmoothies`, `Darken`, `FilterScreen`, `Blur`, `Lava`, `Water`, `Wave`, `RequireScreen`; `Starfield.Initialize`, `UpdateAndDraw`, `DrawPixel`; layer/star state types |
| `HelpTextData.cs` | `Load`, `Create`, table initialization and `MenuHelp` accessor |
| `DelayTimer.cs` | Constructor, `SetSpeed`, `SetDelay`, `SetDelay2`, `Target`, `GetDelayTicks`, `GetDelayTicks2`, `Remaining`, `Wait`, `ServiceWait`, `WaitOrInput`, `WaitWithService`, `ChangeVolume`; `IDelayPlatform` contract |
| `ItemDefinitions.cs` | `Records`, `TryFind`, `LoadBundle`, `LoadAll`, `ParseBundle`, `ReserveZeroSlots`, `IsSpace`, `Trim`, `ValidId`, `ComparePaths`, `Fold`, `AsciiEquals`, `Replace`, `Normalize`, `TypeFromName`, `ValidateField`, `Tokens`, `Error`, `Resolve`, `Schema`; field/record/type models |
| Added legacy `FontRenderer.cs` routines | `DrawString`, `OutText`, `OutTextModify`, `OutTextAdjust`, `OutTextAndDarken`, `ShadeText`, `DrawLegacy`; `TextShade` enum |
| `LevelTests.cs`, `HelpTextTests.cs` | Each defines `Run` and `Check` |
| `BackgroundTests.cs` | `Run`, `TestLayers`, `TestFade`, `Execute`, `Check` |
| `DelayTests.cs` | `Run`, `Check`; fake platform `Delay`, `ServiceEvents`, `PollJoysticks` |
| `ItemDefinitionTests.cs` | `Run`, `CompareNative`, `Normalize`, `TestModLoading`, `Check` |
| `build_background_reference.py`, `build_items_reference.py` | Each defines `build_source` and `main`; generated native oracle `main` functions and platform shims |
| `GameItems.cs` | `GameItem`, `NamedItem`, `ShopItem`, eight concrete models and their fixed-size array initialization; `KnownItem` enum |
| `ItemDatabase.cs` | Constructor, `Load`, `FromDefinitions`, `Items`, both `TryFind` and `Index` overloads, `KnownIndex`, `AddRandomShopItem`, `Contains`, `Create`, `RequireIndex`, `ResolveReferences`/local `Resolve`, `Number`, `Fill`, `FillReferences`, `ReferenceTokens`, `Apply`/local `Reference` and `Unsupported` |
| `ItemDatabaseTests.cs` | `Run`, `TestFileLoading`, `Parse`, `Fixture`/local `AppendArray`, `Dump`, `Format`, `CompareNative`, `Check` |
| Added `build_items_reference.py` helpers | `build_typed_source`; generated `load_typed_items`, `dump_typed_items` |
| `HelpTextRenderer.cs` | Constructor, `DrawBox`, `DrawHelp`, `Layout`; `HelpTextLine` record |
| `HelpTextRendererTests.cs` | `Run`, `Check` |
| `build_help_reference.py` | `build_source`, `main`; generated `SDL_strlcpy`, `JE_textShade`, `main` oracle shims |
| `PlayerShots.cs` | `PlayerShots` constructor, `Create`, `CreateTyped`, `SetDirection`, `Simulate`, `MoveAndDraw`, `MoveCircle`, `AdvanceAnimation`, `RoundedShort`; `SpritePlayerShotEffects.Draw`, `DrawSpecial`, `Explosion`, `SuperPixels`; projectile/enemy/result models and `IPlayerShotEffects` contract |
| `PlayerShotTests.cs` | `Run`, `Fixture`, `Setup`, `Execute`, `Dump`, `Check`; `Effects.Draw`, `DrawSpecial`, `Explosion`, `SuperPixels` |
| `build_shots_reference.py` | `build_source`, `main`; generated `dump`, `main`, sprite/effect recording shims |
| `SceneEffects.cs` | Constructor, `SetupExplosion`, `SetupLarge`, both `UpdateExplosions` overloads, `SpawnSuperPixels`, `DrawSuperPixels`, `CreateProjectileEffects`, `Blend`; `Explosion`, `RepeatingExplosion`, `SuperPixel` models |
| `SceneEffectsTests.cs` | `Run`, `Execute`, `Dump`, `Check` |
| `build_effects_reference.py` | `build_source`, `main`; generated sprite shims, `dump_state`, `main` |
| `PlayerCombat.cs` | Constructor, `Damage`, `WipeBars`, `DrawShield`, `DrawArmor`, `PortConfigurations`, `DrawBar`, `RoundedGauge`; level-end effect wait state |
| `PlayerCombatTests.cs` | `Run`, `Execute`, `Check` |
| `build_combat_reference.py` | `build_source`, `main`; generated `SDL_FillRect`, `main` |
| `EnemyState.cs` | `EnemyState` and `BossBar` runtime model properties and fixed-size arrays |
| `EnemyMovement.cs` | `UpdateAcceleration`, both `AnimateAndDraw` overloads, `BlitEnemy`, `UpdatePosition`, `Oscillate`, `AimVelocity`, `RoundVelocity` |
| `EnemyAttacks.cs` | `AimShot`, `CreateEnemyShot`, `ApplyWeaponSpecial`, `FireShots`, `ProcessAttacks`, `LaunchFromParent`, `DrawAndUpdate`, `RandomSoundChannel`, both `UpdateEnemyShots` overloads; `EnemyShot` runtime model |
| `EnemyAttackTests.cs` | `Run`, `Fixture`, `Execute`, `ExecuteSpecials`, `Create`, `UpdateShots`, `Dump`, `Check` |
| `build_enemy_attacks_reference.py` | `build_source`, `main`; generated `JE_playerDamage`, `dump_attacks`, oracle `main` |
| `LevelEvents.cs` | Constructor, `Current` accessor, `Jump`, `SearchEnemy`, `CreateEnemy`, `CreateEnemyAtY`, `ResolveEnemyLink`, `ApplyRandomSpecialDrop`, `SetEnemyDeath`, `GlobalMove`, `GlobalAcceleration`, `GlobalAnimation`, `GlobalDamage`, `ReverseAcceleration`, `OverrideFire`, `HandleDeath`, `HandleEnemyEvent`; `LevelEvent` typed record properties; level-entry flag state and shared quit-request backing |
| `LevelEventActions.cs` | `HandleEnvironmentEvent`, `PositiveByte`, `SetBackgroundMoves`, `HandleLevelEvent`, `HandleMiscEvent`, `AssignRandomLink`, `Dispatch`, `UpdateEndState`, `ResumeBackgrounds`, `PlaySounds`; `ILevelEventServices` shape/text/song/volume contract and event-flow state properties |
| `LevelEventTests.cs` | `Run`, `Fixture`, `Execute`, `Create`, `Check` |
| `LevelEventDispatchTests.cs` | `Run`, `Execute`, `Flag`, `Dump`, `TestInvalidActions`, `ThrowsInvalid`; services `LoadEnemySheet`, `DrawTextWindow`, `PlaySong`, `SetVolume` |
| `build_enemy_events_reference.py` | `build_source`, `main`; generated `dump_event`, native helper/oracle `main` |
| `build_level_events_reference.py` | `build_source`, `main`; generated `dump_dispatch`, shape/text/music/volume recording shims, native oracle `main` |
| `LevelMap.cs` | Constructor, `ReadHeader`, `LoadShapes`, `LoadMapLayer`, `Load`, `LoadSelected`, `Apply`; `Apply` also transfers the parsed enemy roster to level background state |
| `LevelMapTests.cs` | `Run`, `BuildLevel`, `BuildShapes`, `TestIntegration` (map/event/enemy-roster application), `Dump`, `Hash`, `CompareNative`, `TestFailures`, `Throws`, `Check` |
| `DestructUnitNames.cs` | `DestructUnit` enum and `DestructUnitNames.GetUnitByName`; exact native unit-name matching |
| `DestructUnitNameTests.cs` | `Run`, `Check`; all native unit-name mappings, case/whitespace mismatches, and null handling |
| `build_level_map_reference.py` | `build_source`, `main`; generated binary-read/file-open/endian shims, `hash`, native oracle `main` |
| `EnemyHud.cs` | Both `DrawBossBars` overloads and `BeveledBar`; shared `BossBar.Armor` state |
| `EnemyHudTests.cs` | `Run`, `Execute`, `TestRendering`, `Check` |
| `build_boss_bar_reference.py` | `build_source`, `main`; generated `fill_rectangle_xy`, native oracle `main` |
| `LevelStateTests.cs` | `Run`, `Flag`, `TestAudioFailures`, `CompareNative` |
| `build_level_state_reference.py` | `build_source`, `main`; generated `multiSamplePlay`, native oracle `main` |
| `SoundBank.cs` | Both `Load` overloads, `ReadBank`, `PlaySample`, `Play`, `Clear`; `IAudioSampleConverter.Convert` contract and managed sample ownership |
| `SoundBankTests.cs` | `Run`, `BuildBank`, `TestSoundDispatch`, `TestFailures`, `Hash`, `CompareNative`, `Throws`, `Check`; recording and failure converters `Convert` |
| `build_sound_bank_reference.py` | `build_source`, `main`; generated audio conversion/read/playback shims, `hash_bytes`, native oracle `main` |
| `AudioMixer.cs` | Constructor, synchronized mode/state accessors, `RemainingSamples`, `SetVolume`, `Play`, `ClearChannels`, `ChangeMusic`, `Mix`, `Clamp`; `IMusicSynthesizer.Update`/`Render` contract |
| `AudioMixerTests.cs` | `Run`, `Execute`, `TestFailuresAndOwnership`, `Check`, `Throws`; synthetic/failing `Update` and `Render` |
| `build_audio_mixer_reference.py` | `build_source`, `main`; generated lock/update/render shims and native oracle `main` |
| `MusicPlayer.cs` | `MusicArchive` constructor, `Load`, `Song`; `MusicController` constructor, `SongPlaying` accessor, `PlaySong`, `Restart`, `Stop`, `Fade`; `IMusicPlayer.Load`/`Rewind`/`Fade` contract |
| `MusicPlayerTests.cs` | `Run`/local `State`, `Fixture`, `TestFailures`, `Hash`, `CompareNative`, `Throws`, `Check`; fake player `Load`, `Rewind`, `Fade`, `Update`, `Render` |
| `build_music_reference.py` | `build_source`, `main`; generated archive/read/lock/LDS shims, `state`, native oracle `main` |
| `LdsPlayer.cs` | Constructor, `NewChannels`, `Load`, `Require`, `Free`, `Rewind`, `Fade`, `SetRegister`, `SetRegisterMasked`, `Render`, `Register`, `Masked`, `PlaySound`, `OplSample.ClipInt16`, `OplWaveforms.GetTable`, `GetStart`, `GetMask`, `GetInitialPosition`, `CreateTable`, `OplChipState` constructor, `Initialize`, `Reset`, `Write`, `Render`, `RenderPercussion`, `TremoloFor`, `AdvanceEnvelopeAfterDrumAdvance`, `AddPercussionOutput`, `BuildModulationTables`, `RenderTwoOperatorChannel`, `RenderFourOperatorChannel`, `AdvanceAndRenderOperator`, `CreateTremoloTable`, `ConfigureFourOperatorChannels`, `ReadPort`, `WriteIndex`, `WriteRegister`, `ConfigureOperatorMisc`, `ConfigureOperatorLevel`, `ConfigureOperatorAttackDecay`, `ConfigureOperatorSustainRelease`, `ConfigureChannelFrequencies`, `ConfigureChannelKey`, `ConfigurePercussion`, `ConfigurePercussionOperator`, `ConfigureChannelFeedback`, `ConfigureOperatorWaveform`, `IsFourOperatorEnabled`, `EnableOperator`, `DisableOperator`, `WaveSelectionIndex`, `ConfigureMappedFrequency`, `ConfigureFrequency`, `OplFrequencyTables` constructor, `FrequencyMultiplier`, `KeyScaleMultiplier`, `KeyScaleLevel`, `CreateKeyScaleLevels`, `OplOperatorTimingState.Initialize`, `Advance`, `AdvanceTime`, `AdvanceDrums`, `Render`, `ConfigureWaveform`, `ConfigureVibrato`, `ConfigureFeedback`, `Enable`, `Disable`, `ConfigureFrequency`, `OplEnvelopeState.Initialize`, `ConfigureAttack`, `ConfigureSustainLevel`, `ConfigureDecayRate`, `ConfigureReleaseRate`, `ConfigureSustainKeep`, `CalculateEnvelopeStepMask`, `AdvanceOff`, `AdvanceSustain`, `AdvanceRelease`, `AdvanceDecay`, `AdvanceAttack`, `OplPortState.Read`, `WriteIndex`, `WriteRegister`; `OplFrequencyRegisters` model; typed instrument/channel/position models and `IOplSynthesizer` contract |
| `LdsPlayback.cs` | `Update`, `Advance`, `ResetPackedChannels`, `Command`, `OffsetTune`, `TuneChannel`, `ApplyEffects`, `Tremolo` |
| `LdsTables.cs` | Exact frequency/operator/vibrato/tremolo table initialization |
| `LdsPlayerTests.cs` | `Run`, `TestClipInt16`, `TestOplInitialization`, `TestOplSampleRendering`, `RenderOplSignature`, `RenderOplRhythmSignature`, `RenderOpl3Signature`, `ConfigureVoice`, `TestOperatorTiming`, `TestDrumTiming`, `TestOperatorOutput`, `TestWaveformConfiguration`, `TestOperatorActivation`, `TestFrequencyConfiguration`, `TestOplRegisterWrites`, `TestOplPorts`, `TestOff`, `TestAttackConfiguration`, `TestSustainLevelConfiguration`, `TestDecayReleaseConfiguration`, `TestSustainKeepConfiguration`, `TestVibratoConfiguration`, `TestFeedbackConfiguration`, `TestSustain`, `TestRelease`, `TestDecay`, `TestAttack`, `Fixture`, `FixtureForAudio`, `Execute`/local `State`, `TestFailuresAndIntegration`, `CompareNative`, `Check`, `Throws`; synthetic OPL `Reset`, `Write`, `Render` |
| `build_lds_reference.py` | `build_source`, `main`; generated OPL/read shims, `dump_state`, native oracle `main` |
| `VideoLayout.cs` | `Output` validation, `SetModeByName`, `Calculate`, `ScreenToWindow`, `WindowToScreen`, `WindowDistanceToScreen`; `ScalingMode`/`OutputRectangle` models |
| `InputState.cs` | Constructor/state accessors, `Initialize`, `FlushEvents`, `ClearKeyboard`, `SetRelative`, `MousePosition`, `RelativePosition`, `WaitInput`, `WaitNoInput`, `Wait`, `ServiceEvents`, `ValidateScancode`, `Delay`, `PollJoysticks`; typed event model and required `IInputPlatform` contract |
| `InputTests.cs` | `Run`, `Execute`/local `State`, `Bit`, `Hash`, `CompareNative`, `Check`, `Throws`; scripted platform event/timing/joystick/network/cursor/fullscreen/focus/quit services |
| `build_input_reference.py` | `build_source`, `main`; generated SDL window/event/input/wait services, `append`, `hash`, `state`, native oracle `main` |
| `MouseCursor.cs` | Constructor/state, `Start`, `StartFiltered`, `StartCore`, `Replace`, `Capture`, `CopyBlock`, `Validate`; exact pointer metadata initialization |
| `MouseCursorTests.cs` | `Run`, `Fixture`, `Hash`, `CompareNative`, `Check`, `Throws`; queue platform with explicit unexpected-service failures |
| `build_mouse_reference.py` | `function`, `build_source`, `main`; generated `hash`, synthetic sprites/surface and native oracle `main` |
| `SdlNative.cs` | Static resolver registration, `Resolve`, `Error`; cdecl `GetError`, `BuildAudioConverter`, `ConvertAudio`, `InitSubSystem`, `QuitSubSystem`, `Quit`, `OpenAudioDevice`, `PauseAudioDevice`, `CloseAudioDevice`; ABI `AudioSpec`/`AudioCallback` |
| `SdlNative.cs` video/input members | `Rectangle` constructor, window/display/fullscreen/renderer/texture/presentation/readback cdecl services, `PollEvent`, `PushEvent`, `GetTicks`, `Delay`, `ShowCursor`, `SetRelativeMouseMode`, `SetHint`, `Free` |
| `SdlAudioSampleConverter.cs` | Constructor, `Convert`; bounded native CVT buffer ownership and cleanup |
| `SdlAudioDevice.cs` | Constructor, `TryInitialize`, `TryInitializeOpl`, `CompletedCallbacks`, `Start`, `Pause`, `ThrowIfFaulted`, `Mix`, `Dispose`, `Release` |
| `SdlAudioTests.cs` | `Run`, `TestDisabledInitialization`, `TestDevice`, `TestRuntimeAudioSession`, `MusicArchive`, `WaitFor`, `Bank`, `Throws`; synthetic music `Update`/`Render` failure gates and real SDL OPL/LDS/runtime asset startup |
| `TinyDirectory.cs` | `TryReadFileAt`, `TryOpenSorted`, `OpenSubdirectory`, `TryOpenFile`, `GetExtension`; managed filesystem entry model |
| `TinyDirectoryTests.cs` | `Run`, `TestFileOpen`, `TestSubdirectoryOpen`, `Check` |
| `sdl_audio_reference.c` | `hash`, `main`; real SDL conversion fixtures and ABI gates |
| `GameOptions.cs` | `Parse`/local `Invalid`, `TryIntegerPrefix`, `Help`; launch/network state and `GameOptionResult` |
| `GameOptionsTests.cs` | `Run`, `State`, `CompareNative`, `Normalize`, `Bit`, `Check` |
| `build_game_options_reference.py` | `build_source`, `main`; generated `SDL_strlcpy`, state-output oracle `main` |
| `SdlVideoDevice.cs` | Constructor, `WindowSize`, `DisplayIndex`, `ReinitializeFullscreen`, `ToggleFullscreen`, `SetWindowSize`, `WindowResized`, `SetScalerByName`, `SelectScaler`, `CreateTexture`, `ResizeToScaler`, `CenterInDisplay`, `Clear`, both `Present` overloads, `ReadPixels`, `ClearRenderer`, `EnsureOpen`, `Dispose`, `Release` |
| `SdlVideoTests.cs` | `Run`/local `State`, `Hash`, `CompareNative`, `Check`, `Throws` |
| `build_sdl_video_reference.py` | `build_source`, `main`; generated nearest service, `hash`, `state`, native SDL oracle `main` |
| `SdlInputPlatform.cs` | Constructor, SDL/event state accessors, `PollEvent`, `ReadText`, `PostEvent`, `Delay`, `SetCursorVisible`, `SetRelativeMouseMode`, `EnableSystemMouseScaling`, `ToggleFullscreen`, `WindowResized`, `Quit`, `PushJoysticksAsKeyboard`, `PollJoysticks`, `CheckNetwork`, `EnsureOpen`, `Dispose`; required `IInputAuxiliaryServices` |
| `SdlInputTests.cs` | `Run`, `Describe`, `CompareNative`, `Check`, `Throws`; recording auxiliary input services |
| `sdl_input_reference.c` | `print`, `main`; real SDL ABI/event posting/polling fixtures |
| `JoystickInput.cs` | `JoystickAssignment.Matches`, `ToCode`, `Parse`, `Describe`; `JoystickState.AxisThreshold`, `AxisReduce`, `AnalogAngle`, `CheckAssigned`, `HatValue`, `ResetAssignments`, `LoadAssignments`, `SaveAssignments`; `JoystickInput.Poll`, `PollAll`, `PushKey`, `PushAsKeyboard`, `DetectAssignment`; `JoystickConfigurationSelection.Assign`, `AdjustSetting`, `Handle`; required `IJoystickDevice`, `IJoystickAssignmentHost` |
| `SdlJoysticks.cs` | Constructor, `States`, `JoystickDown`, `IsNetworkGame`, `PushJoysticksAsKeyboard`, `PollJoysticks`, `CheckNetwork`, `EnsureOpen`, `Dispose`, `Release`; device handle/name/count/read guards and `Close` |
| `JoystickTests.cs` | `Run`, `Execute`/local `Update`, `AssignmentRouting`, `Bit`, `Bits`, `CompareNative`, `Check`, `Throws`; scripted device getters, detector platform services and assignment host |
| `SdlJoystickTests.cs` | `Run`/local `Export`, `Check`, `Throws`; native virtual-device/init/close/set/hint delegate contracts |
| `build_joystick_reference.py` | `function`, `build_source`, `main`; scripted SDL getters/update/post/detector callbacks, `bits`, native oracle `main` |
| `SdlNative.cs` joystick members | Cdecl `NumJoysticks`, `JoystickEventState`, `JoystickOpen`, `JoystickClose`, `JoystickName`, capability counts, axis/button/hat reads and `JoystickUpdate` |
| `SelectionMenus.cs` | `SelectionMenuState` gameplay/episode/difficulty properties; `SelectionMenus.SelectGameplay`, `SelectEpisode`, `SelectDifficulty`, shared `Select`/`Kind`; `MainMenuActionState`; `MainMenuActionHandlers.SelectFullGame`, `SelectTwoPlayer`, `ChangeTwoPlayerInputDevice`, `ChangeUpgradeWeaponPower`, `SelectOnePlayer`, `SelectLimitedOptions`, `SelectSuperTyrian`, `SelectOptions`, `SelectNextLevel`, `SelectUpgrade`, `SelectUpgradeSubmenu`, `SelectDataCube`, `SelectLoadSave`, `CopyItems`; `MainMenuFunction.Dispatch`; `WeaponViewFrame.Update`; required `ISelectionMenuHost`, `IMainMenuFunctionHost`, `ISaveMenuHost`, `ISaveMenuList`, `IWeaponViewFrameHost`; `SelectionMenuPresentation` background copy, font width/shadow, normal cursor/fades, present/restore, sound, joystick, keep-alive, modifiers and episode services |
| `SelectionMenuTests.cs` | `Run`, `Execute`, `MainMenuActionParity`, `MainMenuFunctionParity`, `TwoPlayerInputDeviceParity`, `UpgradeWeaponPowerParity`, `KeyboardConfigurationParity`, `QuitConfirmationParity`, `MenuQuitParity`, `UpgradeSubmenuParity`, `WeaponViewFrameParity`, `Script`/`Key`/`Mouse`/`Down`, `Presentation`, `RunSdl`/`Present`/`BeginDelay`, `Bit`, `Check`; menu-function and save-menu test hosts/lists, frame/script platform, traced host, SDL auxiliary and modifier delegate |
| `build_selection_menus_reference.py` | `build_source`, `main`; native oracle fixtures, frame script, input services and traced rendering/audio/palette/network/episode callbacks |
| `UiControlTests.cs` | `Run`, `Bit`; scripted platform event/poll services, native trace comparison and gauge/input guards |
| `build_ui_controls_reference.py` | `build_source`, `main`; native input/poll/fill fixtures and oracle `main` |
| `InputState.cs` UI members | `AnyButton`; caller-owned `InputDetected` state; shared no-op `ClearKeyboard` also replaces `JE_wipeKey` |
| `IndexedSurface.cs` UI members | `ShadowedGauge`; native unsigned dimension/position/color wrapping and pre-multiplication partial division |
| `SdlNative.cs`/`SdlInputPlatform.cs` menu members | Cdecl `GetModState`; disposed-guarded live `Modifiers` property |
| `LegacySaveArchive.cs` | `LegacySaveCipher.Encrypt`, `Decrypt`, `Checksums`, `XorCString`; `LegacySaveSlot.Read`, `Write`, `ReadPascal`, `WritePascal` and all serialized properties; `LegacySaveArchive.Read`, `Write`, fixed-size slot/tail inventory |
| `LegacySaveGame.cs` | `LegacyGameState` typed modes, players, episode/level/cube/difficulty/input and level-transition state including `OldDifficulty`, `EndLevel`, `ReallyEndLevel`, `PlayerEndLevel`, `DoNotSaveBackup`; `LegacySaveGame.Save`, `Load`, `Slot`, `IsCompleted` |
| `PlayerState.cs` legacy members | `PlayerItems.ToLegacyItems`, `LoadLegacyItems`; exact twelve-byte item/episode layout without overwriting weapon power |
| `LegacySaveTests.cs` | `Run`, `GameOperations`, `FilePersistence`, `Failures`, `RunNative`, `Bit`, `Throws`, `Check`; persistence and episode fixture callbacks |
| `build_legacy_saves_reference.py` | `build_source`, `main`; native `serialize`, `deserialize`, `hex`, fixture persistence/episode callbacks and oracle `main` |
| `GameRuntimeSettings.cs` | `InitializeProcessor`, `SetGameSpeed`; processor/detail/speed/pentium/frame-count properties |
| `RuntimeSettingsTests.cs` | `Run`, `Bit`; required delay-platform fixture |
| `build_runtime_settings_reference.py` | `build_source`, `main`; native tick fixture and oracle `main` |
| `GameConfiguration.cs` | `IKeyboardNames`; `SdlKeyboardNames.FromName`, `Name`; `GameConfiguration.Defaults`, `Load`, `LoadFile`, `Save`, `SaveFile`, text/scaler/layout/fullscreen/shop/key properties |
| `GameConfigurationTests.cs` | `Run`, `Fixture`, `Files`, `Throws`, `Check`; fake keyboard name/code mapping |
| `build_game_config_reference.py` | `build_source`, `main`; file/directory/name/scaler/layout fixtures and native oracle `main` |
| `GameSettingsStore.cs` | `DosConfiguration.Defaults`, `Read`, `Write`, fixed fields/arrays; `GameSettingsStore.GetUserDirectory`, `ResolveUserDirectory`, `Load`, `OpenOptional`, `InitializeSaves`, `Save`, `SaveFile`, text/runtime/archive ownership |
| `SettingsStoreTests.cs` | `Run`, `RunNative`, `Bit`, `Throws`, `Check`; fake keyboard and file/volume fixtures |
| `build_settings_store_reference.py` | `build_source`, `main`; native isolated stream/read/write/close/directory/volume fixtures and oracle `main` |
| `SavedGameCatalog.cs` | `SavedGameMetadata`; `ReadMetadata`, `ReadMetadataFile`, `BuildList`, `NativePath`, `ValidateFilename` |
| `SaveMetadataTests.cs` | `Run`, `Fixture`, `Trace`, `RunNative`, `Compare`, `Throws`, `Check`; local `Write` file fixture |
| `build_save_metadata_reference.py` | `build_source`, `main`; native real-file read helpers, `hex`, `print_metadata` and oracle `main` |
| `VersionedSaveGame.cs` | `SaveFile`, `LoadFile`, `Write`, `Load`, `Atoi`; local item `Append`, `Id`, `Apply`, `Index` and bounded cipher layout |
| `VersionedSaveTests.cs` | `Run`, `FailureChecks`, `Persistence`, `Database`, `Fixture`, `Dump`, `RunNative`, `Bit`, `Throws`, `Check`; local bounded cipher fixture `Encoded` |
| `build_versioned_saves_reference.py` | `build_source`, `main`; native numeric item lookup, temporary file/read/write/close helpers, terminated allocator, `fill`, `dump` and oracle `main` |
| `AnimationPlayer.cs` | `PlayFile`, `Play`; required screen/palette/input/PIT/presentation/network integration |
| `AnimationPlayerTests.cs` | `Run`, `RunSdl`, `Failures`, `Fixture`, `Hash`, `RunNative`, `Throws`, `Check`; fixture `Word`, input platform and SDL auxiliary services |
| `build_animation_reference.py` | `build_source`, `main`; native real-file I/O, palette/screen/event/timer/network fixtures and oracle `main` |
| `JukeboxStarfield.cs` | `JukeboxStar` state; `Initialize`, `Reset`, `ChangeSetup`, `NewStar`, `AdvancePhase2`, `Update`, `HandleKey`; persistent pattern/depth/color/speed/phase state |
| `JukeboxStarfieldTests.cs` | `Run`, `Dump`, `Bits`, `Bit`, `Hash`, `RunNative`, `Throws`, `Check`; recording platform event/wait services |
| `build_jukebox_stars_reference.py` | `build_source`, `main`; native screen/star/phase/RNG hashes, key/wait fixtures and oracle `main` |
| `Jukebox.cs` | `IJukeboxAudio`, `JukeboxAudio` status, `Read`, `SetVolume`, `Play`, `Stop`, `Restart`, `Sample`; `IJukeboxPresentation`, `JukeboxPresentation.Initialize`, `Stars`, `DrawLine`, `FadeStep`, `FadeBlack`, `Present`; both `Jukebox.Run` overloads; read-only `JukeboxTitles.Music`/`Sounds` |
| `JukeboxTests.cs` | `Run`, `Trace`, `Integration`, `CreateAudio`, `AudioIntegration`, `RunSdl`, `RunNative`, `Throws`, `Check`; mixer player, SDL auxiliary, traced audio/presentation/platform services |
| `build_jukebox_reference.py` | `build_source`, `main`; original title tables, native audio/input/presentation/timer fixtures and oracle `main` |
| `HqVideoScaler.cs` | `Scale`, `Different`, `Interpolate`, `Weighted`; all neighborhoods, threshold masks, unsigned interpolation and validation |
| `HqPatternKernels.cs` | `Pattern2`, `Pattern3`, `Pattern4`; all 256 switch patterns and conditional pixel recipes per factor |
| `generate_hq_kernels.py` | `generate`, `main`; validates native pattern/macro syntax, generates managed kernels and checks reproducibility |
| `HqVideoScalerTests.cs` | `Run`, local `Execute`, `Failures`, `CompareNative`, `Throws`, `Check`; exhaustive fixtures and complete byte comparisons |
| `build_hq_scaler_reference.py` | `build_source`, `main`; synthetic texture/surface, exact fixture reader and native binary-output `main` |
| `NetworkSession.cs` | `NetworkPacketType`, `NetworkPacket` constructor/state/`Copy`, `INetworkTransport`, `NetworkTerminationException`; `NetworkSession` constructor/state/request accessors, `Elapsed`, `Distance`, `Write`, `ShiftUp`, `ShiftDown`, `Prepare`, `SendUnacknowledged`, `Send`, `Acknowledge`, `Check`, `Validate`, `Xor`, `Update`, `PrepareState`, `SendState`, `UpdateState`, `ResetState`, `Connect`, `SendConnect`, `ApplyConnect`, `Shutdown`, `Terminate` |
| `NetworkShutdown.cs` | `NetworkRequests` flag accessors/`ClearSpecialRequests`; `INetworkShutdownPresentation`; `NetworkShutdownPresentation` constructor, `FadeBlack`, `ShowMessage`, `Present`, `FadeIn` |
| `SdlNetTransport.cs` | Constructor, `LocalPort`, `Bind`, `Send`, `Receive`, `EnsureOpen`, `Dispose`, `Release`; ABI `Address`/`Packet`, cdecl `Init`, `Quit`, `Resolve`, `Open`, `Close`, `Bind`, `GetAddress`, `Allocate`, `Free`, `Send`, `Receive` |
| `SdlNative.cs` network additions | `RegisterLibraries`; shared SDL2/SDL2_net resolver and platform/environment filenames |
| `NetworkSessionTests.cs` | `Run`, `Trace`, `Failures`, `Shutdown`, `RunShutdownSdl`, `Bit`, `Result`, `Dump`, `RunNative`, `Throws`, `Check`; deterministic clock/input platform, recorded transport/send/receive/injection/history/handshake services; `ShutdownPresentation.FadeBlack`/`ShowMessage`/`Present`/`FadeIn`, shutdown input `PollEvent`/`Delay`/`PollJoysticks`/`Quit`/auxiliary services |
| `build_network_reference.py` | `build_source`, `main`; deterministic native tick/UDP/allocation/receive/send/handshake/input fixtures, `clone`, `queue`, `dump`, `inject`, halt picture/font/fade/button/SDLNet-quit/game-halt fixtures, native oracle `main` |
| `SdlNetTests.cs` | `Run`, `Receive`, `Poll`, `CompareNative`, `Throws`, `Check`; clock/input platform for actual loopback queues |
| `sdl_net_reference.c` | `fail`, `main`; real SDL2_net resolve/bind/native packet/echo/lifecycle reference |
| `LevelNetwork.cs` | `ILevelNetworkServices`; `LevelNetworkServices.Pause`/`ShowInGameSetup`/`DrawDesynchronization`; `InGameChat.Handle`; `LevelEvents.UpdateNetworkState`/local `Write`, `Read` |
| `LevelNetworkTests.cs` | `Run`, `RunSdl`, `Failures`, `CopyRequests`, `Bit`; `InGameChat.Handle` no-op regression; wire `Transport.Send`/`Receive`, `Services.Pause`/`ShowInGameSetup`/`DrawDesynchronization`, deterministic/real-time input platform |
| `build_level_network_reference.py` | `build_source`, `main`; native wire read/write/send/update, pause/setup/text/reset fixture functions and exhaustive oracle `main` |
| `GamePause.cs` | `GamePausePresentation.Draw`, constructors; `GamePause.Run` |
| `GamePauseTests.cs` | `Run`, `RunSdl`, `RunSdlNetwork`, `Normalize`; scripted/real-time `Platform` queue/focus/timing/input services, `Transport.Send`/`Queue`/`Receive`, actual SDL auxiliary services |
| `build_pause_reference.py` | `build_source`, `main`; scripted native pause input/presentation/volume/queue/PIT services and oracle `main` |
| `PlayerLifecycle.cs` | `PlayerCombat.Linked`/`ConstantDie`/`LevelEndEffectsWait`, `UpdateDeath` |
| `PlayerLifecycleTests.cs` | `Run`, `Bit`; integrated player/level/combat/effect/HUD fixtures and exact native trace comparison |
| `build_player_lifecycle_reference.py` | `build_source`, `main`; native purple-ball fixture and exhaustive lifecycle/HUD/effects/RNG oracle `main` |
| `PlayerMovement.cs` | `PlayerMovementState` motion fields; `PlayerMovement` constructor/state/`ConstantLastX`, `DrawShip` sprite/callback overloads and `Ship`, `UpdateLinking` callback/command overloads, `UpdateLinkingCore`, `DrawLevelEnd`, `UpdateMovement`, `UpdateSidekickPositions`, `Round`, `Validate` |
| `PlayerMovementTests.cs` | `Run`, `TestShipPixels`, `AppendShipTraces`, `AppendSpecialTraces`, `TestInvalidInput`, `Throws`, `Bit`; typed-item/projectile/motion/weapon-power/mode/cooldown/superbomb/charge/ammo/animation/arcade-special/latch/ship/command fixtures, native HUD hash/trace/RNG comparison and shot-render service guards |
| `build_player_movement_reference.py` | `build_source`, `main`; native trail/SDL rectangle/pattern-sprite fixtures, original combo tables/recognition and exhaustive typed-item/projectile/motion/main-weapon/superbomb/charge/sidekick/HUD/weapon-mode/ship/command oracle `main` |
| `PlayerWeapons.cs` | Constructor; charge/mode state, `SpecialCodeProgress`, `ExecutedSpecial`, native `KeyboardCombos`/`ShipCombos`/`SuperShipCombos` tables; `DrawOptions` icon/callback overloads, `DrawOptionLevel`, `UpdateAttacks`, `RecognizeSpecial`, `ChangeMode`/`Special`, `DrawModeButtons` callback/icon overloads, `FireMain`, `FireSuperbomb`, `UpdateCharge`/`Bar`, `FireSidekicks`/`Fire`/`ResetCharge`/`Gauge`, `DrawSidekicks`, `Validate` |
| `PlayerSpecialWeapons.cs` | Constructor and native wait/duration/flare/filter/weapon/cost/arcade state; `Complete`/`Create`/`Zinglon`, `Update` callback/sprite overloads |
| `PlayerFrame.cs` | `PlayerFramePresentation` required surfaces/drawing services; `PlayerFrame` constructor, buttons/episode/render/demo/network/device/graphic/old-position/parallax state; `Update`/`Replay`, `UpdatePlayers`; camera offsets share explicit initialization/restore state |
| `PlayerSpecialWeaponsTests.cs` | `Run`, `TestPresentation`, `TestFrameFailures`, `Throws`, `FillFramePacket`, `Hash`, `Bit`; `FrameTransport.Send`/`Receive`, `FramePlatform` input guards; complete/special/attack/individual/paired frame fixtures and full state/projectile/pixel/RNG native comparison; guarded `ShotEffects` |
| `build_player_special_reference.py` | `build_source`, `main`; generated native full special/ordered attack/sidekick initialization/player/paired-frame/death/purple-ball oracle `main`, full-frame `hash`, and bounded input/demo-text/network-packet/deferred explosion shims |
| `PlayerInput.cs` | Constructor; exposed shared `Requests`; native input/device/demo/constant/inversion state; `Collect`, `ReplayDemo` and `Down` |
| `PlayerInputTests.cs` | `Run`, `AppendReplayTraces`, `AppendReplayCollection`, `TestFailures`, `Throws`, `Bit`; `FragmentedStream.Read`; `Device` virtual axis/button/hat state; `Platform` events/service-order guards; guarded `ShotEffects` |
| `build_player_input_reference.py` | `build_source`, `main`; generated native collector/device/event/demo/network fixtures, scripted `replay_demo_keys`, unchanged `replay_native_keys`, `fread_u8`, `fwrite_u8`, `SDLNet_Write16` and oracle `main` |
| `DemoSession.cs` | Constructor; demo number/episode/level/song/bonus/main-level/owned stream state; `LoadNext`, `RecordLevel`, `Dispose`, `ReadByte`, `ReadItem` |
| `DemoSessionTests.cs` | `Run`, `Prepare`, `Header`, `Execute`/`Value`, `TestFailures`, `Throws`, `Bit`; guarded `Platform` input and `ShotEffects` rendering services |
| `build_demo_reference.py` | `build_source`, `main`; generated native player/header/filesystem/stdio/episode fixtures, `hex`, oracle `main` and unchanged demo/item routines |
| `EnemyCollisions.cs` | `EnemySystem.DestroyOnCollision`, `DamageOnCollision`, `CollectArmorPickup`, `ValidateCollisionIndex` |
| `EnemyCollisionTests.cs` | `Run`, `TestFailures`, `Throws`, `Bit`; complete native collision/enemy/player/effect/HUD/RNG state comparison |
| `build_enemy_collision_reference.py` | `build_source`, `main`; generated enemy collision/definition/player fixtures, oracle `main`, unchanged collision/combat/HUD/effect routines |
| `PlayerPickups.cs` | `GetName`, `DefaultName`, `CollectBonusLevel`, `CollectScore`, `CollectArcade`, `Collide`, `UpgradeText`, `PickupText`, `Text`, `Secondary`, `CString`, `Index`, `RawName`, `Validate` |
| `PlayerPickupTests.cs` | `Run`, local `Dump`, `TestFailures`, `Throws`, `Bit`, `ShotEffects.Draw`, `DrawSpecial`, `Explosion`, `SuperPixels`; complete pickup/collision state traces |
| `build_pickups_reference.py` | `build_source`, `main`; generated `dump_items`, oracle `main`, unchanged pickup/collision/combat/effect functions |
| `GameHud.cs` | `GameHud.Draw`, `DrawCube`; `IGameHudPresentation` and `GameHudPresentation.Text`, `TextWidth`, `Icon`, `Cube` |
| `GameHudTests.cs` | `Run`, `TestPresentation`, `Throws`; `TracePresentation.Text`, `TextWidth`, `Icon`, `Cube`; `UnusedEffects.Draw`, `DrawSpecial`, `Explosion`, `SuperPixels` |
| `build_game_hud_reference.py` | `build_source`, `main`; generated drawing/width trace helpers, oracle `main` and unchanged HUD/name/cube routines |
| `GameGamma.cs` | `Check`; managed input and palette publication |
| `GameSettingsTests.cs` | `Run`, `Hash`, `Bit`, `Throws`; `NoInput` platform guards; gamma and high-score state traces |
| `build_game_settings_reference.py` | `build_source`, `main`; generated `set_palette`, oracle `main`, unchanged gamma and score sorting routines |
| `PlayerProgression.cs` | `WeaponUpgradeCost`, `GetCost`, `GetValue`, `TotalScore`, `Initialize`, `AdjustDifficulty`, `Known`, `BaseCost`, `NativeLong`, `NativeUnsignedLong` |
| `PlayerProgressionTests.cs` | `Run`, local `Dump`, `TestFailures`, `Bit`, `Throws`; complete player/progression traces |
| `build_player_progression_reference.py` | `build_source`, local `extract`, `main`; unchanged native progression functions, generated oracle `main` |
| `TextGlow.cs` | `TextGlow.Draw`, local `Frame`; `ITextGlowHost`/`TextGlowHost.Draw`, `SetDelay`, `AnyButton`, `KeepAlive`, `Present`, `Wait` |
| `TextGlowTests.cs` | `Run`, `TestHost`; `TraceHost.Draw`, `SetDelay`, `AnyButton`, `KeepAlive`, `Present`, `Wait`; `Platform` input/timer guards |
| `build_text_glow_reference.py` | `build_source`, `main`; native glow, generated draw/input/network/timer traces and oracle `main` |
| `GameTextEffects.cs` | `WarningBars.Update`, `GameTextWindow.Show`, concrete text-window presentation constructor |
| `GameTextTests.cs` | `Run`, `TestWindowPresentation`; `Clock.Delay`, `ServiceEvents`, `PollJoysticks`; warning and text-window traces |
| `build_game_text_reference.py` | `build_source`, `main`; native warning/window routines, timer/fill/text trace services and oracle `main` |
| `CharacterGlow.cs` | `CharacterGlow.Draw`; `ICharacterGlowHost`/`CharacterGlowHost.Width`, `Shade`, `Glyph`, `Shadow`, `SetDelay2`, `SetDelay`, `Remaining`, `AnyButton`, `KeepAlive`, `UpdateWarning`, `Delay`, `Present` |
| `CharacterGlowTests.cs` | `Run`, `TestHost`, `Bit`; `TraceHost` presentation/input/timer services; `UnusedGlowHost` full-text guards |
| `build_character_glow_reference.py` | `build_source`, `main`; original character map and character glow, generated glyph/input/timer/warning traces and oracle `main` |
| `GameHelp.cs` | `GameHelp.Run`; `IGameHelpHost`/`GameHelpHost.ClearKeyboard`, `WipeKey`, `Shade`, `Fill`, `Option`, `Box`, `TextWidth`, `Text`, `ServiceEvents`, `MouseStart`, `Present`, `MouseReplace`, `Delay`, `PushJoysticks`, `KeepAlive` |
| `GameHelpTests.cs` | `Run`, `TestHost`, `Bit`; `TraceHost` full help presentation/input/mouse/network services |
| `build_game_help_reference.py` | `build_source`, `main`; native help routine, generated presentation/input/cursor/network trace helpers and oracle `main` |
| `LevelResults.cs` | `LevelResults.Run`, local `Show`/`CubeFrame`, `Unlock`, `Text`, `Cash`, `CString`, `Join`; `ILevelResultsHost`/`LevelResultsHost` audio/glow/cube/input/palette/presentation services |
| `LevelResultsTests.cs` | `Run`, `TestHost`; `TraceHost` result presentation/input services; `TraceGlow`/`UnusedShots` guards |
| `build_level_results_reference.py` | `build_source`, `main`; native end-level/progression functions, generated result presentation/audio/input helpers and oracle `main` |
| `EpisodeTransition.cs` | `EpisodeTransition.Run`, `Known`; `IEpisodeTransitionHost`/`EpisodeTransitionHost` score/credits/title/palette/audio/input services |
| `EpisodeTransitionTests.cs` | `Run`, local `Dump`, `TestHost`, `Bit`; `TraceHost` transition services |
| `build_episode_transition_reference.py` | `build_source`, `main`; native next-episode selection/transition, generated title/input/credits/score callbacks and oracle `main` |
| `EpisodePictureDisplay.cs` | `EpisodePictureDisplay.Show`, `ParseAtoi`; picture/PCX palette selection, clear/present and fade host actions |
| `EpisodePictureDisplayTests.cs` | `Run`, `AssertSequence`, `ParseExpected`; 256 extracted-native section/palette/action traces |
| `build_episode_picture_reference.py` | `build_source`, `main`; extracts the unchanged native episode-picture routine and traces screen/palette actions |
| `EpisodeWarningDisplay.cs` | `EpisodeWarningDisplay.Show`, `ParseAtoi`; warning state, decrypted lines, native word frame-count parsing and presenter handoff |
| `EpisodeWarningDisplayTests.cs` | `Run`, `AssertState`, `MakeEpisode`, `WriteEncryptedString`, `HexLines`, `ParseExpected`; trace presenter/glow/input fixtures and 256 native warning traces |
| `build_episode_warning_reference.py` | `build_source`, `main`; extracts the unchanged native episode-warning routine and traces state, input position and decrypted lines |
| `EpisodeLevelJump.cs` | `EpisodeLevelJump.Handle`, `ParseAtoi`; six conditional jump codes, section offset, signed decimal-prefix parsing and byte destination |
| `EpisodeLevelJumpTests.cs` | `Run`, `AssertState`, `ParseExpected`, `AppendState`; 2,048 native jump-gate/state traces |
| `build_episode_level_jump_reference.py` | `build_source`, `main`; extracts the unchanged native conditional jump routine and traces byte-level state |
| `EpisodeModeConfiguration.cs` | `EpisodeModeConfiguration.Configure`; Galaga loadout copy, extra-game flag and SuperTyrian loadout reset |
| `EpisodeModeConfigurationTests.cs` | `Run`, `SeedPlayer`, `AssertState`, `SnapshotPlayer`, `Dump`, `DumpPlayer`; 256 seeded native mode/loadout traces |
| `build_episode_mode_reference.py` | `build_source`, `main`; extracts the unchanged native episode mode routine and traces flags/player fields |
| `EpisodeItemAvailability.cs` | `EpisodeItemAvailability.Read`, `EpisodeItemAvailabilityState`; encrypted 9-row shop parsing, typed lookups, optional random item offers and item-screen handoff |
| `EpisodeItemAvailabilityTests.cs` | `Run`, `MakeEpisode`, `WriteEncryptedString`, `Dump`, `AssertState`, `RowType`; 15 shop-setting traces and native comparisons |
| `build_episode_item_availability_reference.py` | `build_source`, `main`; extracts the unchanged native episode availability reader and generates encrypted episode data/oracle output |
| `MapTransition.cs` | `MapTransition.Play`, `MapTransition.ParseAtoi`; upper/lower/horizontal indexed-surface reveals, event and frame timing handoff |
| `MapTransitionTests.cs` | `Run`, `FillPattern`, `Hash`, `TraceHost.LoadPicture`/`ServiceEvents`/`SetDelay`/`Present`/`WaitDelay`; per-frame native pixel hashes across 16 scenarios |
| `build_map_transition_reference.py` | `build_source`, `main`; extracts the unchanged native picture-reveal function and generates per-frame pixel hashes |
| `EpisodePresentation.cs` | `EpisodePresentation.Handle`, `IEpisodePresentationHost`; command dispatch for pictures, map transitions, fades, palette changes, warnings and completion |
| `EpisodePresentationTests.cs` | `Run`, `Bool`, `Section`, `TraceHost` fade/clear/present/palette services; 40 extracted-native dispatcher and host-order cases |
| `build_episode_presentation_reference.py` | `build_source`, `main`; extracts the unchanged native episode presentation dispatcher with traced dependent routines |
| `EpisodeCompletion.cs` | `EpisodeCompletion.Show`; encrypted hint-section selection, score/cash warning lines, unlock presentation, ship graphics, network sync and SuperTyrian return state |
| `EpisodeCompletionTests.cs` | `Run`, `MakeData`, `MakeEpisode`, `WriteEncryptedString`, `DumpLines`, `TestMalformedInput`, trace host; 48 unchanged-native completion and unlock scenarios |
| `build_episode_completion_reference.py` | `build_source`, `main`; extracts the unchanged native completion routine with deterministic encrypted sections and presentation traces |
| `Credits.cs` | `Credits.Run`; `ICreditsHost`/`CreditsHost` picture/text/file/font/palette/ship/audio/input/timer and cleanup services |
| `CreditsTests.cs` | `Run`, `TextData`, `TestHost`, `Throws`; `TraceHost` credits services |
| `build_credits_reference.py` | `build_source`, `main`; native credits playback and generated synthetic service oracle |
| `HighScores.cs` | `HighScores.Run`, local `Rank`/`Title`, `Number`, `CString`, `Join`; `HighScoreInput`, `IHighScoreHost`/`HighScoreHost` cursor/palette/input/text/shape/glow/audio/timer services; `HighScoreHost` saved-name popup rendering/input/callback adapter |
| `HighScoreTests.cs` | `Run`, `TestEditing`, `TestHost`, `TestSaveNamePopup`, local `LoadCursor`; `TraceHost` and `TraceSaveNameHost` scripted input |
| `SavedGameNamePopup.cs` | `ISavedGameNamePopupHost`; `SavedGameNamePopup.Run` with input, drawing, and save/load callbacks |
| `build_high_scores_reference.py` | `build_source`, `main`; native sorting/qualification/name entry and generated service oracle |
| `PlayerShipInfo.cs` | `Initialize`, `ExtraGraphic`, `Draw`; shared frame graphics, selected sprite sheets and generator power |
| `ShipInfoTests.cs` | `Run`, local `SheetId`, `Sheet`, `TestDraw`, `Throws` |
| `build_ship_info_reference.py` | `build_source`, `main`; native ship state/graphics initialization oracle |
| `EditedShips.cs` | `Decrypt`, `Load(string)`, `Load(Stream)`; owned custom records/sprite sheet and transactional publication |
| `EditedShipTests.cs` | `Run`, `Encrypt`, `Compare`, `Throws` |
| `build_edited_ships_reference.py` | `build_source`, `main`; native archive/decryption and file/checksum exit oracle |
| `GameKeyboard.cs` | `GameKeyboard.Run`, local `Held`/`Known`/`Edit`; `IGameKeyboardHost`/`GameKeyboardHost` gamma/ship/HUD/help/text/timer/audio integration |
| `GameKeyboardTests.cs` | `Run`, `TestHost`, `Bit`; `TraceHost` ordered keyboard services; `UnusedEffects` explicit unexpected-shot guards |
| `build_game_keyboard_reference.py` | `SCANCODES`, `build_source`, `main`; native keyboard dispatcher and generated mode/chord/state oracle |
| `InGameSetup.cs` | `InGameSetup.Run`, `Draw`, `Wait`, `ReadMouse`, `ReadKeyboard`, `Adjust`, `ChangeVolume`, `Apply`, `ValidateSelected`; `SetupInput`, `InGameSetupAction`, `IInGameSetupHost`/`InGameSetupHost` and `Copy` surface/input/audio/settings services |
| `InGameSetupTests.cs` | `Run`, `TestMouseBoundaries`, `TestHost`, `Fill`, `Bit`; `Silence` fixture; `TraceHost` services, `ResetWait`, `RunScan` |
| `build_ingame_setup_reference.py` | `build_source`, `main`; native draw/read/apply/wait/full-menu and volume oracle |
| `GameSetupSession.cs` | `GameSetupWaiting.Draw`; `GameSetupSession.Run` and local `Send`; bound input/settings/level/network flags and explicit primary presentation; `QuitRequested` aliases shared level state |
| `GameSetupSessionTests.cs` | `Run`, `RunSdlNetwork`, `CreateLevel`, `TestFailure`, `TestWaiting`, `Bit`, `Normalize`; scripted `Transport`, `Platform`, `Host` boundary services |
| `build_game_setup_reference.py` | `build_source`, `main`; unchanged native wrapper/ordered send/wait/flag oracle |
| `HighScoreScreen.cs` | `HighScoreScreen.Run`; `IHighScoreScreenHost`/`HighScoreScreenHost.EnsureCursor`, `LoadBackground`, `RestoreBackground`, `Header`, `Shaded`, `Arrow`, `Help`, `FadeIn`, `PresentCursor`, `PushJoysticks`, `Sample`, `FadeBlack` |
| `HighScoreScreenTests.cs` | `Run`, `RunSdl`, `TestHost`, `Fill`, `CreateShapes`, `SpriteBytes`; `Auxiliary`, trace `Host` and scripted `Platform` input services |
| `build_high_score_screen_reference.py` | `build_source`, `main`; unchanged native score-page drawing/input oracle |
| `NewGame.cs` | `NewGame.Run`, `RunSuperArcade`, `RunSuperTyrian`, `Known`, `Index`, `CString`; `INewGameHost`/`NewGameHost` selection, rendering, palette, input, audio and episode services; `Labels` |
| `NewGameTests.cs` | `Run`/local `Create`, `Dump`; `Reset`, `TestHost`, `Bit`; trace `TraceHost` and actual selection `MenuHost` services |
| `build_new_game_reference.py` | `build_source`, `main`; unchanged native normal/super-arcade/SuperTyrian flow oracle |
| `TitleScreen.cs` | `TitleScreen.Run`, `Draw`, `Wait`, `Read`, `Dispatch`, `Secrets`, `ValidateSelected`; `TitleScreenInput`, `TitleScreenResult`; `ITitleScreenHost`/`TitleScreenHost` rendering/input/actions and `Copy` |
| `TitleScreenTests.cs` | `Run`, `Create`, `SetInput`, `TestInputEdges`, `TestHost`, `RunSdl`, `Bit`; `Auxiliary`, trace `Host` and scripted `Platform` services |
| `build_title_screen_reference.py` | `build_source`, `main`; unchanged native title drawing/wait/input/dispatch/secrets/full-loop oracle |
| `GameIntro.cs` | `IIntroLogoHost`/`IntroLogoHost.Clear`, `FadeWhite`, `LoadPicture`, `Present`, `FadeIn`, `WaitInput`, `FadeBlack`; `IntroLogos.Run`/local `Show`; `ScreenBleed.Run` |
| `GameIntroTests.cs` | `Run`, `TestHost`, `TestFailures`, `Hash`; `TraceHost`, scripted `Platform` input/timing services |
| `build_game_intro_reference.py` | `build_source`, `main`; unchanged native startup/bleed and full-buffer byte oracle |
| `LoadScreen.cs` | `LoadScreen.Draw`, `WaitInput`, `ReadInput`, `HandleAction`, `Run`, `Label`; `ILoadScreenHost`/`LoadScreenHost.GameLoaded`, `EnsureCursor`, `BuildSaves`, `LoadBackground`, `RestoreBackground`, `Header`, `Shaded`, `Arrow`, `Help`, `FadeIn`, `PresentCursor`, `PushJoysticks`, `Sample`, `LoadSave`, `FadeBlack`; `LoadScreenInput`, `LoadScreenResult` |
| `LoadScreenTests.cs` | `Run`, `Compare`, `Create`, `Text`, `SetInput`, `TestInputEdges`, `TestHost`, `RunSdl`, `Bit`; `Auxiliary`, trace `Host`, scripted `Platform`; local cursor, unexpected-action and persisted-save callbacks |
| `build_load_screen_reference.py` | `build_source`, `main`; native saved-game count/drawing/input/action/wait/full-loop oracle |
| `HelpSystem.cs` | `HelpSystem.Run`, `ShowPages`, `DrawPage`, `Format`; `IHelpSystemHost`/`HelpSystemHost.EnsureCursor`, `PlaySong`, `LoadBackground`, `RestoreBackground`, `ClearFooter`, `TextWidth`, `Header`, `Footer`, `TextStyle`, `Box`, `Cursor`, `FadeIn`, `PresentCursor`, `PushJoysticks`, `Sample`, `FadeBlack` |
| `HelpSystemTests.cs` | `Run`, `Text`, `Create`, `TestHost`, `RunSdl`, `Bit`; `Auxiliary`, trace `Host`, scripted `Platform`/`Key`/`Drain`/`Mouse`; local cursor and unexpected title-child callbacks |
| `build_help_system_reference.py` | `build_source`, `main`; native help topic-menu/page rendering and navigation oracle |
| `SetupMenu.cs` | `SetupMenu.PickerCount`, `PickerItem`, `Draw`, `WaitInput`, `CollectMenuInput`, `CollectPickerInput`, `ApplyPicker`, `Activate`, `ApplyMenu`, `ChangeVolume`, `Run`; `SetupMenuState`, `SetupMenuInput`, `SetupMenuId`, `SetupItemId`, `SetupAction`; `ISetupMenuHost`/`SetupMenuHost` video/scaling/shop/audio properties and rendering/input/video/configuration/child service methods |
| `SetupMenuTests.cs` | `Run`, `Compare`, `Create`, `State`, `Item`, `Dump`, `DumpInput`, `Bit`, `SetInput`, `TestFailures`, `RunSdl`, `TestRendering`; `Player`, `Auxiliary`, trace `Host`, scripted `Platform`; local cursor/background/save/jukebox/unexpected-child callbacks |
| `build_setup_menu_reference.py` | `build_source`, `main`; native setup/picker/render/input/action/volume/full-loop oracle |
| `ChristmasPrompt.cs` | `ChristmasPrompt.IsChristmasTime`, `Run`; managed `Flake` ownership; `IChristmasPromptHost`/`ChristmasPromptHost.EnsureCursor`, `Fill`, `Flake`, `Label`, `FadeIn`, `PresentCursor`, `PushJoysticks`, `FadeBlack` |
| `ChristmasPromptTests.cs` | `Run`, `TestHost`, `RunSdl`, `Bit`; `RandomService.Next`, `Auxiliary`, trace `Host`, scripted `Platform` |
| `build_christmas_prompt_reference.py` | `build_source`, `main`; native date and snow-prompt oracle with explicit random/input/clock services |
| `NetworkStartScreen.cs` | `NetworkStartScreen.Run` with optional runtime-owned connection action; `INetworkStartChannel`/`NetworkStartChannel.Packet`, `Synchronized`, `SendDetails`, `SendQuit`, `Update`, `Check`, `Quit`; `INetworkStartHost`/`NetworkStartHost.LoadPicture`, `Capture`, `Restore`, `Copy`, `Message`, `Present`, `FadeIn`, `FadeBlack`, `Connect`, `SelectEpisode`, `SelectDifficulty`, `InitializeEpisode` |
| `NetworkStartScreenTests.cs` | `Run`, `Items`, `Game`, `TestHost`, `RunSdlNetwork`, `Bit`; trace `Host`, scripted `Channel`, synthetic/real-time `Platform`; concrete two-peer connection/selection/init/termination callbacks through the injected session connection |
| `build_network_start_reference.py` | `build_source`, `main`; native start-screen service/selection/packet/state oracle |
| `WarningTextDisplay.cs` | `WarningTextDisplay.Run`, `Synchronize`; `IWarningTextPresenter`; `IWarningTextHost`/`WarningTextHost.EscapePressed`, `HaltGame`, `WarningEnabled`, `Draw`, `Center`, `Halt`, `UpdateWarning`, `SetDelay`, `KeepAlive`, `Wait`, `AnyButton` |
| `WarningTextTests.cs` | `Run`, `TestHost`, `TestValidation`, `RunSdl`, `Bit`; native-trace `TraceHost`, exception marker `Halted`, shared-state `UnusedGlowHost`, SDL `Auxiliary` |
| `build_warning_text_reference.py` | `build_source`, `main`; native warning display and disabled synchronization oracle |
| `GameShutdown.cs` | `GameTerminationException.ExitCode`; `GameShutdown.Halt`, `ReleaseResources`; `IGameShutdownHost`/`SdlGameShutdownHost.Output`, `DeinitializeAudio`, `DeinitializeVideo`, `DeinitializeJoysticks`, `ClearMainShapes`, `ClearShopShapes`, `ClearExplosionShapes`, `ClearDestructShapes`, `ClearSounds`, `SaveConfiguration`, `QuitSdl`, `Exit` |
| `GameShutdownTests.cs` | `Run`, `TestFailures`, `RunSdl`, `RunSdlChild`, `Throws`; native trace `TraceHost`, synthetic `Converter`/`SilentMusic` and SDL subsystem `Initialized` query |
| `build_game_shutdown_reference.py` | `build_source`, `main`; native ordered shutdown/resource/save/farewell/exit oracle |
| `check_conversion_inventory.py` | `definitions`, `main`; native function/added-path inventory and summary consistency checks |
| `TextTokenReader.cs` | `Remaining`, `CString`, `TryReadInt32`, `TryReadToken`; native byte token/capacity and platform long/int conversion |
| `TextTokenTests.cs` | `Run`, `Hex`, `Bit`; repeated reads, native byte/overflow/capacity and UTF-8 reference limits |
| `build_text_tokens_reference.py` | `build_source`, `main`; native token/byte/overflow/error oracle |
| `LevelInput.cs` | `LevelInput.Handle`, `NormalBonusLevel`, `BonusLevel`, `StoppedDemo`; `ILevelInputHost`/`LevelInputHost.SkipPresentation`, `DrawGameOver`, `PlayGameOver`, `SetVolume`, `PushJoysticks`, `Keyboard`, `Pause`, `Setup`; shared `NetworkRequests.PausePressed`/`InGameMenuPressed` distinguish local input from protocol requests |
| `LevelInputTests.cs` | `Run`, `RunSdl`, `Bit`; native `Host`/`Platform`, real SDL `Auxiliary`, `SilentMusic` and local presentation callback |
| `build_level_input_reference.py` | `build_source`, `main`; native level game-over/demo/key/modal/request oracle |
| `LevelPreparation.cs` | `LevelPreparation.Prepare`; `LevelPreparationState`; `ILevelPreparationHost` for relative-mouse release, keyboard/sprite cleanup, speed reset, demo teardown, level transitions and save recovery |
| `LevelPreparationTests.cs` | `Run`, `DumpState`, `Bit`, `Bool`; 49,152 native preparation traces across demo, game-over, bonus, Galaga, save-recovery and speed modes |
| `build_level_preparation_reference.py` | `build_source`, `main`; unchanged native next-level cleanup and routing oracle |
| `LevelMainLoop.cs` | `LevelMainLoop.Run`; `LevelMainLoopState`; `ILevelMainLoopHost` for native-order per-level setup and frame dispatch |
| `LevelMainLoopTests.cs` | deterministic multi-level, title-return, and demo/network/save gate traces |
| `LevelFrame.cs` | `LevelFrame.Run`; frame state, due event loop, music/player conditions, and native gameplay/render/input/network stage ordering |
| `LevelFrameTests.cs` | native-order frame trace, state transitions, network end-level and input early-return scenarios |
| `EpisodeMapLoader.cs` | `EpisodeMapLoader.Run`, command handlers for galaxy/cube/level records, fixed-width atoi fields, restart/title/demo routing; `IEpisodeMapLoaderHost` for existing ported handlers |
| `EpisodeMapLoaderTests.cs` | encrypted episode records covering directives, jumps/reopen, item-screen game-loaded restart, title return, presentation cancellation and demo loading |
| `GameStartup.cs` | `GameStartupArguments.Parse` applies parsed options only on successful parse; `GameStartup.Run`; initialization/output/audio/network gates, Christmas fallback, title/destruct/main loop, and shutdown; `GameStartupState.ApplyOptions` preserves parsed data-directory, network peer/name/port/player/delay, audio, Christmas, and demo options for the eventual concrete host; `IGameStartupHost` |
| `GameStartupTests.cs` | startup ordering, SDL/network failure exits, Christmas/assets/audio/demo and repeated title/destruct routing; parsed launch-option preservation in startup state |
| `GameRuntimeAssets.cs` | `GameRuntimeAssets.Load` composes episode availability, item database, palette, main shapes, help text and optional edited ships; seasonal mode requires both native shape and voice assets and falls back with a warning; `Dispose` releases owned shape tables |
| `GameRuntimeAssetsTests.cs` | synthetic native asset fixture, complete startup asset load, optional edited-ship absence, and Christmas asset enable/fallback |
| `GameRuntimeAudio.cs` | `GameRuntimeAudio.Load` composes `music.mus`, effects and selected standard/seasonal voice banks; `Dispose` releases converted samples |
| `GameRuntimeAudioTests.cs` | synthetic music/effects/voice archives, seasonal bank selection, conversion coverage and sample cleanup |
| `GameRuntimeResources.cs` | `GameRuntimeResources.Load` composes runtime assets and optional audio data; `LoadForStartup` consumes typed options and binds the real SDL audio session; both resolve seasonal fallback and clean up failures |
| `GameRuntimeAudioSession.cs` | `TryInitialize` binds native SDL OPL/LDS device, loaded music and converted sound bank to one mixer; song/sample/volume operations and deterministic cleanup |
| `GameRuntimePlatform.cs` | `Start` composes persisted configuration, SDL video/input/joysticks, game assets/audio, runtime settings, and an owned SDL2_net transport/session for network launches; `InitializeEpisode` owns episode archive initialization and updates game episode state; `LoadPicture` composes indexed Tyrian picture decoding with runtime palettes; `CreateDelayTimer` binds PIT-style timing to shared SDL input; `CreateMouseCursor` lazily loads and owns the native cursor sprite sheet and composes it with shared SDL input/primary surface; `CreateIntroLogoHost` and `CreateIntroLogos` bind startup logo pictures, palette fades, SDL presentation, timing and title logo movement; `CreateCreditsHost` and `CreateCredits` bind credits picture/text archives, player ship tables, palette fades, SDL input/presentation, timer, audio, and caller-provided network keep-alive; `CreateEpisodeTransitionHost` composes the active episode palette, SDL presentation/input/timing, audio, high-score and credits actions; `CreateEpisodeTransition` binds the managed episode transition to loaded episodes/items and explicitly supplied episode names, palette, credits text, and keep-alive callback while applying network/constant-play options, with an overload using loaded episode-name data; `CreateEpisodeCompletionHost` binds SDL picture/palette/fade, drawing, input waits and presentation with explicit completion-display/progression/network-sync callbacks; `CreateEpisodeCompletion` composes that host with completion state, score labels and super-arcade tables; `CreateEpisodeCompletionState` bridges launch modes, episode completion flags, computed managed item-value score (or explicit score), and both players' cash from runtime/game state; `RunEpisodeCompletion` executes the managed completion flow and publishes section/unlock/main-level results back to the game and startup state even if the completion host fails, refreshes jump-back state after episode progression, and provides an overload that builds its state from the active game; `CreateTitleScreenHost` composes the loaded planet/font/cursor/palette assets, primary/secondary surfaces, SDL presentation, timing, audio and title actions; composed `CreateTitleScreen` connects title dispatch directly to managed normal new-game, saved-game, super-arcade, SuperTyrian, high-score, help, and setup flows while retaining explicit callbacks for other title actions; `CreateSelectionMenuPresentation` composes shared video, fonts, cursor, palette fades, audio and episode initialization, with a runtime-owned episode initializer overload; `CreateNewGameHost` wires the managed selection menus, ship sprites, assets, audio, input and episode callback, with a runtime-owned initializer overload; `CreateLoadScreenHost` binds the native saved-game UI to runtime picture/font/cursor/palette/input/audio services and the user save catalog/versioned loader; `CreateHighScoreScreenHost` and `CreateHelpSystemHost` compose those existing screens with runtime fonts, assets, palettes, cursor, input, audio and SDL presentation; `CreateSetupMenuHost` composes SDL video, configuration persistence, fonts, cursor, palette, input, audio mixer, and caller-owned jukebox/Destruct actions, including an inert disabled mixer when audio is not initialized; `CreateNetworkStartHost` binds peer-screen presentation, shared episode/difficulty selectors, connection negotiation, and episode initialization to the session, with an overload using the runtime-owned episode initializer; the composed `RunNetworkStartScreen` overload creates this host from runtime menus; `ConnectNetwork` runs negotiated session setup with configured player identity and episode availability; the host-based `RunNetworkStartScreen` connects the configured SDL2_net session at the native screen point and runs peer episode/difficulty setup; `UpdateNetworkGameState` routes per-frame network payloads/requests through that same negotiated session; screen factories bind title, new-game, gameplay/episode/difficulty selection, saved-game, high-score, setup and help flows to the owned runtime assets/input/settings; `CreateLevelMainLoopState` composes typed launch flags and current game mode/level into the managed multi-level loop state; `CreateLevelGameplay` composes shared per-game combat, enemy, projectile, movement, weapon, event, background, and input owners; `LoadLevelMap` applies the selected archive map and its events/enemy roster to that graph; `RunLevelMainLoop` executes that loop and publishes mutated mode, demo, level-transition and level values back to the owning game/startup state even if a host operation fails; initializes player defaults, loads and saves legacy games, and `CreatePlayerInput` binds configured keyboard/joystick and saved device settings plus session-owned requests, local player number and typed launch options; exposes a session-owned gameplay request queue and cleans partially initialized services in reverse order; `SaveConfiguration`, `Dispose` |
| `LevelGameplayRuntime.cs` | `LevelGameplayRuntime` groups per-game scene effects, enemy system, player combat, backgrounds, events, projectiles, movement, weapons, specials, input, ship info, player-frame state, pickups, warnings, and armor warning into one runtime-owned graph with shared player, enemy, sound, request, and launch-mode state; `LoadMap` loads the selected archive level and applies its tile maps, events, and enemy roster to that graph; map/ship, per-level flags, and combat initializers reset the same owners using native ordering |
| `GameRuntimePlatformTests.cs` | SDL dummy-driver initialization, incomplete-asset failure cleanup, runtime indexed-picture/palette loading, lazy cursor-asset rendering/restoration, startup intro-picture presentation, credits archive loading/host composition, episode-transition host and factory composition including explicit active-palette/keep-alive bindings, episode-completion host/factory/state/run composition including SDL picture/palette/fade, callback and game/startup-state write-back, title/new-game/selection host SDL/assets/callback composition, saved-game discovery and versioned-load state propagation, composed title dispatch bindings, high-score/help/setup host composition including no-audio settings, live audio callback, configured SDL2_net session ownership and network-start host composition, title/new-game/level-loop and gameplay-input composition, shared level gameplay graph and player-frame/ship initialization, selected map layers/events/enemy roster, native-order map/ship, level-flag and local/network combat initialization, network player identity, demo mode, loop state write-back, legacy game-save round trip, and settings persistence |
| `LevelHud.cs` | `LevelHud.Draw`; `LevelHudState`; `ILevelHudHost` for HUD surface, message erase, rectangle and shield drawing |
| `LevelHudTests.cs` | `Run`, `AppendState`, `Bit`, `Bool`, `TestInvalidWeaponBars`; 4,096 complete state, host-call and old-weapon-bar traces |
| `build_level_hud_reference.py` | `build_source`, `main`; unchanged native shield regeneration, Galaga reward, weapon-bar and power HUD oracle |
| `LevelBackgroundScene.cs` | `LevelBackgroundScene.Draw`, `DrawBackground2`; `LevelBackgroundSceneState`; `ILevelBackgroundSceneHost` for ordered layer, filter, enemy and random operations |
| `LevelBackgroundSceneTests.cs` | `Run`, `AppendState`, `Bit`, `Bool`; trace `TraceHost` for 4,096 native background/filter/enemy composition scenarios |
| `build_level_background_scene_reference.py` | `build_source`, `main`; unchanged native level-background composition and state oracle |
| `ArmorWarning.cs` | `ArmorWarning.Draw`, `ShipDelay`; `IArmorWarningHost`/`ArmorWarningHost.CreateShip`, `ConfigureShip`, `Fill`, `Label`; shared `WarningBars` color/sound state |
| `ArmorWarningTests.cs` | `Run`, `TestHost`; trace `Host.CreateShip`, `ConfigureShip`, `Fill`, `Label`, `Drop`, `Speed`, `Armor` |
| `build_armor_warning_reference.py` | `build_source`, `main`; native low-armor rescue/color/sound/owner/RNG oracle |
| `LevelPresentation.cs` | `Show`, `SpecialCode`, `PlayerEndLevel`; shared keyboard `SkipPresentation` and setup `QuitRequested` |
| `LevelPresentationTests.cs` | `Run`, `RunSdl`, `Hash`, `TestFailures`; concrete `Fixture` keyboard/settings/setup hosts and timer; trace `Platform` and `SilentMusic` |
| `build_level_presentation_reference.py` | `build_source`, `main`; native pixel hash, inversion, searchlight, aliasing, padded stride and PIT timing oracle |
| `LevelTimerDisplay.cs` | `Draw`, `Flash`, `FlashChange`, `DebugTime`; `ILevelTimerDisplayHost`/`LevelTimerDisplayHost.Ticks`, `DebugText`, `Notice`, `TimerLabel`, `TimerValue` |
| `LevelTimerTests.cs` | `Run`, `RunSdl`, `TestHost`, `TestFailure`, `TestDebug`; native trace host and shared `LevelPresentationTests.Fixture` |
| `build_level_timer_reference.py` | `build_source`, `main`; native debug/FPS, flash, shared timer/sound state and alternating frame-count oracle |
| `LevelPlayerInitializer.cs` | `Initialize`; `ILevelPlayerInitializationHost`; concrete `LevelPlayerInitializationHost.LoadPicture`, `DrawOptions`, `DrawLevelName`, `Present`, `CorrectGamma`, `FadePalette`, `LoadExplosionShapes` |
| `LevelPlayerInitializerTests.cs` | `Run`, `TestFailure`, `TestConcreteHost`; native trace `TraceHost` |
| `build_level_player_init_reference.py` | `build_source`, `main`; native player histories, difficulty clamp, display/palette/load ordering |
| `LevelMapShipInitializer.cs` | `Initialize`; resets loaded map row/pixel offsets, current and third-layer player-frame camera accumulators, layer pacing, music fade, player shots, starfield speed and ship/player startup state |
| `LevelMapShipInitializerTests.cs` | `Run`, `Seed`; compares 1,024 extracted-native startup traces including preserved old/secondary camera accumulators and retains loaded map layers/header indices |
| `build_level_map_ship_init_reference.py` | `build_source`, `main`; extracts the unchanged native map/ship initializer and captures row pointers, layer offsets, delays, music, power, starfield and player state |
| `LevelFlagsInitializer.cs` | `Initialize`; resets shared gameplay/input/map/background/effect/warning/event/boss-bar flags at level entry |
| `LevelFlagsInitializerTests.cs` | `Run`, `Seed`; 16,384 extracted-native state traces for entry flags and retained boss-bar fields |
| `build_level_flags_reference.py` | `build_source`, `main`; extracts the native level-flag initializer and captures all reset and preserved state |
| `LevelStatusDisplayInitializer.cs` | Host contract/adapter; `Initialize` resets shields, armor/HUD, superbombs, cube/flash/notice state, music and port buttons |
| `LevelStatusDisplayInitializerTests.cs` | `Run`; 256 extracted-native traces for narrowed shield timing, both players' shields/armor, reset state and host-call order |
| `build_level_status_display_reference.py` | `build_source`, `main`; extracts the native status initializer and captures state and explicit host calls |
| `LevelCombatStateInitializer.cs` | `Initialize`; resets gameplay-owned combat, projectile, enemy, effect, weapon, level and background state |
| `LevelCombatStateInitializerTests.cs` | `Run`, `Fixture`, `AssertState`, `AppendState`; 256 seeded reset cases and extracted-native trace comparison |
| `build_level_combat_state_reference.py` | `build_source`, `main`; extracts the unchanged native combat-state initializer and traces all reset groups |
| `GameKeyboard.cs` additions | `RecordDebugFrame`; shares debug histories with keyboard toggle/reset and updates last tick only after overlay drawing succeeds |
| `GameRuntimeSettings.cs` additions | `AdvancePentiumFrame`; alternates the shared `FrameCountMax` only in Pentium mode |
| `GameRuntimeSettings.cs` additions | `SetFrameCountMax`; sets the native episode-warning frame-count remainder |
| `GameRuntimeSettings.cs` additions | `RestoreLevelTransitionSpeed`; restores smooth scrolling and forwards the native speed value to the timer host |
| `SdlNative.cs` configuration members | Cdecl UTF-8 `GetScancodeFromName`, `GetScancodeName` |
| `EnemySystem.cs` | Constructor, `ProcessShot`, `ShotHits`, `MarkLinked`, `AnimateDamaged`, `DestroyLinked`, `FindSpriteSheet`, `CreateLegacy`, `Create`, `Initialize`, `ScaleScore`, `ScaleArmor`, `InitializeScoreAndArmor`, `RandomStart` |
| `EnemySystemTests.cs` | `Run`, `Fixture`, `Execute`, `DumpEffects`, `Normalize`, `Dump`, `IndexOfReference`, `Check`; `TraceEffects.Draw`, `DrawSpecial`, `Explosion`, `SuperPixels` |
| `build_enemy_reference.py` | `build_source`, `main`; generated `sprite_identity`, `dump_enemy`, `dump_effects`, `JE_eventJump`, sprite shims, oracle `main` |

- [x] `src-csharp/OpenTyrian.Core/Credits.cs` - complete credits playback:
  encrypted text loading, scrolling/shadows, timed picture fades, seeded ship
  selection/banking, song transitions, input/network waits and cleanup.
  Uses actual managed sprite/font/palette/input/PIT/file services.
- [x] `tests/OpenTyrian.Core.Tests/CreditsTests.cs` - 24,483 native-matched
  frames across six exit timings and four picture-table sizes, including
  end-of-scroll frame holding; concrete pixels, timers, palette/input/audio,
  malformed text and cancellation cleanup.
- [x] `tests/native/build_credits_reference.py` - unchanged native
  `JE_playCredits` with synthetic text/pictures and ordered service traces.

- [x] `src-csharp/OpenTyrian.Core/HighScores.cs` - score qualification and
  entry for both players, native signed-score narrowing, sorting/shifting,
  ASCII glyph-filtered names, keyboard/mouse cancellation and full blinking/
  cursor/glow/results flow with actual managed services; also provides the
  saved-name popup rendering/input adapter.
- [x] `tests/OpenTyrian.Core.Tests/HighScoreTests.cs` - 512 native-matched
  qualification/edit/presentation scenarios, threshold ties and signed
  narrowing boundaries; concrete sprite/font/palette/input/cursor/timer
  routing, 28-character cap, trailing spaces and cancel semantics, plus
  scripted save-name popup input and save/load/cancel dispatch.
- [x] `src-csharp/OpenTyrian.Core/SavedGameNamePopup.cs` - native save/load
  name-entry loop, bounded character editing, prompt drawing and operation
  dispatch, verified with scripted host scenarios.
- [x] `tests/native/build_high_scores_reference.py` - unchanged native
  high-score checking/sorting and real total-score calculations with
  scripted input and ordered presentation traces.

- [x] `src-csharp/OpenTyrian.Core/PlayerShipInfo.cs` - generator power,
  primary/secondary armor and hit areas, all fifteen custom graphic selectors,
  native sheet-pointer preservation and concrete player sprite routing.
  Shares the graphic array used by `PlayerFrame`.
- [x] `tests/OpenTyrian.Core.Tests/ShipInfoTests.cs` - 1,024 native-matched
  ship initialization cases and 10,240 custom lookups, all normal/custom
  combinations, selector/sheet behavior, concrete pixels and invalid records.
- [x] `tests/native/build_ship_info_reference.py` - unchanged native
  `JE_getShipInfo`/`JE_SGr` and correctly sized synthetic edited-ship records.
- [x] `src-csharp/OpenTyrian.Core/EditedShips.cs` - optional user archive,
  native ushort shape-size wrapping and four-checksum decryption. Reuses
  legacy checksum arithmetic and integrates `SpriteSheet`; failures throw
  without publishing partial state. Only 150 defined decrypted bytes are
  retained, not the native uninitialized four-byte scratch tail.
- [x] `tests/OpenTyrian.Core.Tests/EditedShipTests.cs` - 256 decryption
  vectors, all checksum failures, 24 native archive cases including wrapped/
  zero sizes, optional absence, malformed/reload ownership and sprite pixels.
- [x] `tests/native/build_edited_ships_reference.py` - unchanged native
  archive/decrypt functions, real stdio reads and defined-payload tracing.

- [x] `src-csharp/OpenTyrian.Core/GameKeyboard.cs` - complete ordered
  keyboard dispatcher, custom ships, help/network requests, NortShip chord,
  cheating/skip/debug/pause/menu controls, smoothie/filter/speed toggles and
  random music. Concrete adapter composes gamma, ship/combat HUD, help/text
  and PIT settings with shared gameplay/network state.
- [x] `tests/OpenTyrian.Core.Tests/GameKeyboardTests.cs` - 4,096 native
  mode/chord cases, key consumption, two-player custom selection precedence,
  armor preservation, speed byte wrapping and full state/RNG traces; concrete
  gamma/ship/combat/help/text/speed/audio service integration.
- [x] `tests/native/build_game_keyboard_reference.py` - unchanged native
  dispatcher with synthetic keyboard/edited data, real RNG and ordered host
  calls; item indices are supplied from the actual managed test database.

- [x] `src-csharp/OpenTyrian.Core/InGameSetup.cs` - complete in-game
  settings menu, saved-surface ownership, labels/gauges/help, mouse hover/
  sliders, keyboard navigation, ordered volume/mute/detail/speed changes and
  local/network quit flags. Concrete adapter binds fonts, indexed surfaces,
  cursor/input, audio mixer, DOS configuration and runtime/PIT settings.
- [x] `tests/OpenTyrian.Core.Tests/InGameSetupTests.cs` - 2,048 native
  draw/action/wait cases, 16 complete menu loops, exact mouse/slider
  boundaries, negative-coordinate ushort wait comparisons, quit/mute/W
  commands and actual rendering/audio/input/configuration/timer integration.
- [x] `tests/native/build_ingame_setup_reference.py` - seven unchanged
  native setup functions and real volume adjustment with scripted input,
  synthetic text and ordered draw/audio/setting traces.

- [x] `src-csharp/OpenTyrian.Core/GameSetupSession.cs` - local/network
  settings entry and exit, acknowledged menu rendezvous, waiting/quit packet
  handling, local quit/Escape flags and finally-based mouse/request cleanup.
  Peer-wait presentation takes the primary surface explicitly without
  mutating a global surface pointer.
- [x] `tests/OpenTyrian.Core.Tests/GameSetupSessionTests.cs` - 64 native
  wrapper flag combinations with the real managed settings menu/network
  queues, failure/cancellation cleanup, exact peer-wait pixels, and actual
  SDL2_net two-peer resume and quit synchronization.
- [x] `tests/native/build_game_setup_reference.py` - unchanged native
  setup wrapper with scripted settings/packet boundary services; ordered
  messages, waiting draws and exact flag state. Only repeated presentations
  during protocol polling are coalesced; packet primitives are verified
  separately and through actual SDL2_net peers.

- [x] `src-csharp/OpenTyrian.Core/HighScoreScreen.cs` - complete three-episode
  high-score viewer: saved picture/header/black strip, disabled-episode
  headings, unsorted signed score/name/difficulty rows, arrow visibility,
  keyboard wrap and mouse edge/precedence behavior, help/cursor and fades.
- [x] `tests/OpenTyrian.Core.Tests/HighScoreScreenTests.cs` - 256 native
  full-page scripts covering every byte difficulty rank, signed score
  boundaries, paging hit boxes, unavailable episodes, simultaneous mouse/key
  input, all exit keys and exact draw/audio/wait ordering; archive immutability,
  concrete pixels/services/cancellation and full actual SDL2 screen loop.
- [x] `tests/native/build_high_score_screen_reference.py` - unchanged
  native score-page loop with synthetic saved scores, localized labels,
  scripted mouse/keyboard/joystick input and complete presentation trace.

- [x] `src-csharp/OpenTyrian.Core/NewGame.cs` - normal/two-player/rich,
  seven super-arcade modes and SuperTyrian initialization; native item-ID
  resolution, selection short circuit/cancellation side effects, signed
  difficulty changes and intro text/ship/audio/palette/input services.
  Concrete adapter synchronizes the existing selection menus with shared
  game state, preserving prior state when selections cancel.
- [x] `tests/OpenTyrian.Core.Tests/NewGameTests.cs` - 2,048 native normal
  starts, 1,792 super-arcade starts and 256 SuperTyrian starts; byte/signed
  wrapping, cancelled selections, rich/cash precedence, all mode loadouts,
  exact intro service ordering and concrete selection/render/audio/palette/
  wait/cancellation integration.
- [x] `tests/native/build_new_game_reference.py` - three unchanged
  native new-game functions with the real typed item parser, known indices,
  super-arcade tables and scripted selection/presentation boundary services.

- [x] `src-csharp/OpenTyrian.Core/TitleScreen.cs` - title picture/version/
  animated logo, split palette fades, saved menu and selected labels, exact
  mouse/key/text waits with idle demo, all seven action branches, nine
  streaming secret-code counters and mode flags. Concrete adapter binds
  indexed surfaces, fonts, planet sprites, filtered cursor, palette and PIT
  services plus explicit child-flow delegates.
- [x] `tests/OpenTyrian.Core.Tests/TitleScreenTests.cs` - fourteen native
  draw cases, 4,096 input snapshots, 28 dispatch states, eight wait/timeout/
  unsigned-wrap/negative-coordinate scenarios, 136 secret cases and 144
  full loops; exact hit boundaries, cancellation, concrete rendering/
  service integration and actual SDL2 title animation -> high scores ->
  title restart -> gameplay/episode/difficulty selection -> new-game state.
- [x] `tests/native/build_title_screen_reference.py` - six unchanged
  native title functions with exact synthetic menu/secret tables,
  scripted input/ticks and ordered rendering/action traces.

- [x] `src-csharp/OpenTyrian.Core/GameIntro.cs` - original startup picture/
  white/black fade/input sequence and title logo flag; concrete palette/PIT
  host. `ScreenBleed` retains the exact native in-place neighbor processing,
  two scratch buffers, asymmetric copy length, frame/input exit and warning
  reset while validating geometry and non-overlapping managed memory.
- [x] `tests/OpenTyrian.Core.Tests/GameIntroTests.cs` - native startup
  ordering, 1,264 screen-bleed frames at two pitches/six exit timings,
  per-frame primary/scratch fingerprints and full final buffer byte
  comparisons for every timing; concrete fades/input and invalid geometry/
  cancellation checks. Actual SDL intro/bleed is integrated by title tests.
- [x] `tests/native/build_game_intro_reference.py` - unchanged native
  startup and bleed functions with synthetic surfaces, per-frame pixel/
  scratch hashes, complete final buffer bytes and scripted exits.

- [x] `src-csharp/OpenTyrian.Core/LoadScreen.cs` - saved-game viewer
  drawing/paging, exact row/control input precedence and boundaries, PIT/
  palette/cursor/audio presentation and save-operation host integration.
  Native signed-count clamp is replaced by nonnegative typed-list count;
  managed list ownership replaces the native allocation/free.
- [x] `tests/OpenTyrian.Core.Tests/LoadScreenTests.cs` - 960 native draw
  states, 8,192 input states, 7,680 action states, eight mouse/key/negative-
  coordinate waits and 160 full loops, plus row edges/cancellation/
  concrete padded-surface rendering. Actual SDL title -> load -> next-page
  -> persisted versioned-save loading verifies catalog and game/episode
  restoration, perform-save clearing and palette exit.
- [x] `tests/native/build_load_screen_reference.py` - six unchanged
  load-screen functions, signed/empty/partial/multiple-page saved lists,
  short-circuit action precedence and existing game-loaded preservation.

- [x] `src-csharp/OpenTyrian.Core/HelpSystem.cs` - five-entry topic menu,
  eight original help-page layouts, per-topic/global footers, page boundary
  return to menu, cursor direction and exact mouse/key/hover/click precedence;
  concrete font/help/palette/SDL services and text-style restoration.
- [x] `tests/OpenTyrian.Core.Tests/HelpSystemTests.cs` - 144 native page
  scripts and 304 full-menu scripts (including 160 row-boundary/independent
  click/hover/simultaneous key snapshots), all topic starts/eight layouts,
  concrete padded-surface heading/footer/wrapping/cursor/fade checks and
  cancellation/topic validation. Actual SDL title -> help -> all eight
  pages -> topic menu -> title exit verifies music and style restoration.
- [x] `tests/native/build_help_system_reference.py` - unchanged native
  help menu and page functions, full rendering/service/input traces, topic/
  page boundary behavior and exact text-style values at fades/presentation.

- [x] `src-csharp/OpenTyrian.Core/SetupMenu.cs` - title settings and
  graphics/audio/gameplay submenus, native display/scaler/scaling/shop
  pickers, volume/toggle behavior and ordering, parent/back navigation,
  jukebox child integration and rendering/input/PIT services. Concrete SDL
  host synchronizes active video configuration and persisted shop settings.
- [x] `tests/OpenTyrian.Core.Tests/SetupMenuTests.cs` - 16 picker catalogs,
  256 draw states, 8,192 menu and 8,192 picker inputs, 1,024 picker actions,
  156 activations, 2,816 menu actions, eight waits and 64 full native loops.
  Includes clamped volume extremes, native picker click-coordinate quirk,
  scaler failure/restore and cancellation. Actual SDL title -> setup ->
  display/scaler/scaling -> audio toggles/volume -> real jukebox -> shop
  settings/save -> title return verifies rendering and configuration reload.
- [x] `tests/native/build_setup_menu_reference.py` - all sixteen
  unchanged setup/picker functions and native volume helper, synthetic
  display catalog, service/audio/video/settings traces and full loops.

- [x] `src-csharp/OpenTyrian.Core/ChristmasPrompt.cs` - local-time
  December detection, native 80-flake fixed-point snow updates/layer order,
  choice hover/click/key/cancel semantics and concrete blended sprite/full-
  shadow font/palette/PIT/input host. Platform C-runtime random service is
  explicit, not silently substituted with the gameplay Mersenne Twister.
- [x] `tests/OpenTyrian.Core.Tests/ChristmasPromptTests.cs` - 12 native
  month cases/local timezone boundary and 300 complete prompt scenarios,
  including 12 long 350-frame waits exercising snowflake respawn; explicit
  reproducible RNG stream/state, every flake/frame/service trace, choice
  edges/precedence, cancellation and padded-surface rendering verified.
  Actual SDL snow/choice/fade/presentation path is exercised.
- [x] `tests/native/build_christmas_prompt_reference.py` - unchanged
  native date/prompt bodies with synthetic local months and an explicitly
  injected shared reproducible random service (not a platform-rand claim).

- [x] `src-csharp/OpenTyrian.Core/NetworkStartScreen.cs` - network-start
  waiting presentation, connection/selection/details/quit flow, fixed-width
  difficulty/episode state and cash/Silver Ship setup; concrete palette/
  font/surface/selection host and existing network-session packet adapter.
- [x] `tests/OpenTyrian.Core.Tests/NetworkStartScreenTests.cs` - 112 native
  controller/peer flows with selection cancellation, signed difficulty and
  wire-value truncation, exact polling/fades/state preservation; concrete
  padded surfaces/palette/host and input validation/cancellation verified.
  Actual SDL2_net two-peer handshake/details/episode state synchronization
  and both episode/difficulty cancellation-to-quit propagation covered.
- [x] `tests/native/build_network_start_reference.py` - unchanged native
  start-screen body with packet/selection/presentation mocks, delayed
  details/acks and real 16-bit network-byte encoding.

- [x] `src-csharp/OpenTyrian.Core/WarningTextDisplay.cs` - line/continue-prompt
  composition, red/non-red layout, animation skip and warning wait/cleanup;
  concrete shared character glow, fonts, input, PIT and network keep-alive.
  The disabled native text synchronization remains a verified no-op.
- [x] `tests/OpenTyrian.Core.Tests/WarningTextTests.cs` - 2,816 native traces
  over zero through ten lines, animation/escape/warning/termination states;
  concrete padded pixels, shared state, timing and validation/cancellation;
  actual SDL mouse/Return/Escape prompt input and presentation.
- [x] `tests/native/build_warning_text_reference.py` - original warning
  display and disabled synchronization bodies with scripted glow/input/
  halt/clock services and complete wait/presentation-state traces.

- [x] `src-csharp/OpenTyrian.Core/GameShutdown.cs` - ordered platform/resource
  shutdown, save/skip and farewell behavior, exit-code normalization and typed
  nonreturning termination; concrete owned SDL device/shape/sound/settings host.
- [x] `tests/OpenTyrian.Core.Tests/GameShutdownTests.cs` - 512 native ordered
  resource/save/farewell/exit traces and ten explicit cleanup-failure cases;
  isolated real SDL device/subsystem teardown, configuration reload, skip-save
  and save-failure checks; real warning-text halt-to-shutdown composition.
- [x] `tests/native/build_game_shutdown_reference.py` - original shutdown
  body with resource/sample/configuration/SDL/process-exit mocks, all byte exit
  codes and both farewell flags.

- [x] `tests/native/check_conversion_inventory.py` - lexical native function
  audit covering comments/literals, pointer-return signatures and conditional
  definitions; checks per-file function coverage, completion totals, and every
  added implementation/build/test path and total on both CI platforms.

- [x] `src-csharp/OpenTyrian.Core/TextTokenReader.cs` - byte-oriented integer/
  reference token consumption, native platform-long saturation/int narrowing,
  C whitespace/NUL and reference capacity behavior; typed item arrays and
  references reuse the reader without mutable pointer/string-buffer emulation.
- [x] `tests/OpenTyrian.Core.Tests/TextTokenTests.cs` - 192 repeated-read native
  cases covering signs, numeric prefixes, platform overflow, whitespace,
  NUL/non-ASCII bytes and reference capacities/errors; UTF-8 byte limits.
- [x] `tests/native/build_text_tokens_reference.py` - original integer/
  reference parsing bodies, hex byte traces and trapped native capacity errors.

- [x] `src-csharp/OpenTyrian.Core/LevelInput.cs` - game-over/demo/keyboard/
  pause/setup dispatcher and concrete existing keyboard/pause/settings host;
  local pressed latches are distinct from queued network requests.
- [x] `tests/OpenTyrian.Core.Tests/LevelInputTests.cs` - 32,768 native
  mode/input/game-over/skip/request traces; actual SDL game-over/demo,
  keyboard/help, pause/resume, setup/return and network-request dispatch.
- [x] `tests/native/build_level_input_reference.py` - original level input
  body with state/input/presentation/modal mocks and complete state traces.
- [x] `src-csharp/OpenTyrian.Core/LevelPreparation.cs` - next-level cleanup,
  Galaga mode normalization, demo teardown, difficulty reset, bonus/game-over
  routing, level transition and failed-save recovery.
- [x] `tests/OpenTyrian.Core.Tests/LevelPreparationTests.cs` - 49,152 native
  traces cover all preparation flags and six speed settings.
- [x] `tests/native/build_level_preparation_reference.py` - unchanged native
  preparation body with ordered service and state traces.
- [x] `src-csharp/OpenTyrian.Core/LevelMainLoop.cs` - level/main-game
  orchestration, preserving reset, map load, startup, network/demo/save and
  frame-loop order.
- [x] `tests/OpenTyrian.Core.Tests/LevelMainLoopTests.cs` - verifies multi-level
  transitions, demo/network/save gates and title-screen return.
- [x] `src-csharp/OpenTyrian.Core/LevelFrame.cs` - native level-frame event,
  gameplay, background, projectile, player, HUD, sound, input, network and
  end-state orchestration with stateful early-return behavior.
- [x] `tests/OpenTyrian.Core.Tests/LevelFrameTests.cs` - deterministic native
  order, background wrapping, due-event, network end-level and input-skip traces.
- [x] `src-csharp/OpenTyrian.Core/EpisodeMapLoader.cs` - encrypted episode
  command loop, section navigation, mode/jump/item/presentation delegation,
  galaxy/cube/level record decoding and demo/title/map-load routing.
- [x] `tests/OpenTyrian.Core.Tests/EpisodeMapLoaderTests.cs` - encrypted
  synthetic episode fixtures verify command order, fixed-offset fields,
  jump/reopen, game-loaded retry, title return, presentation exit and demo path.
- [x] `src-csharp/OpenTyrian.Core/GameStartup.cs` - native process startup,
  initialization/fallback/network gates, repeated title/game modes and halt
  routing with testable platform boundaries; launch parsing applies and
  retains typed startup options for the concrete runtime host.
- [x] `src-csharp/OpenTyrian.Core/GameRuntimeAssets.cs` - concrete ownership and
  loading for episode availability, typed extracted items, all 23 palettes,
  main shape tables, encrypted help text, and optional edited ships; matches
  the native requirement for both seasonal shapes and voice data, including
  warning and base-shape fallback; synthetic startup-asset fixtures pass.
- [x] `tests/OpenTyrian.Core.Tests/GameRuntimeAssetsTests.cs` - composes a
  synthetic episode archive, palette, shape archive, encrypted help archive,
  all required extracted item categories, and optional edited-ship absence.
- [x] `src-csharp/OpenTyrian.Core/GameRuntimeAudio.cs` - owns the music archive
  and converted sound bank loaded from the native asset filenames; selects the
  standard or seasonal voice archive and clears sound samples on failure or
  disposal.
- [x] `tests/OpenTyrian.Core.Tests/GameRuntimeAudioTests.cs` - synthetic music,
  effects and voice archives verify composed loading, all 38 conversions,
  seasonal voice selection and sample cleanup.
- [x] `src-csharp/OpenTyrian.Core/GameRuntimeAudioSession.cs` - shared live SDL
  audio session composes the OPL/LDS device mixer with native music and sound
  assets, converts samples at the negotiated device rate, exposes song/sample/
  volume operations, honors disabled-audio startup and cleans every resource
  on load failure or shutdown.
- [x] `tests/OpenTyrian.Core.Tests/SdlAudioTests.cs` - SDL dummy-driver runtime
  test verifies disabled startup, failed asset-load cleanup, shared music/
  sample mixer operation, callback progress, pause and disposal.
- [x] `src-csharp/OpenTyrian.Core/GameRuntimeResources.cs` - one owned
  composition for loaded game assets and audio; consumes typed startup data
  directory/Christmas options, synchronizes seasonal fallback before selecting
  voice data, honors audio-disabled startup without opening audio assets and
  disposes assets if enabled audio loading fails. `LoadForStartup` binds the
  real OPL/SDL device session after loading required game assets and propagates
  resolved Christmas/audio-device fallback state to startup.
- [x] `tests/OpenTyrian.Core.Tests/GameRuntimeAssetsTests.cs` - complete runtime
  composition selects seasonal voices only when both seasonal asset files are
  present, and falls back to standard voices otherwise.
- [x] `src-csharp/OpenTyrian.Core/GameRuntimePlatform.cs` - platform owner
  initializes configuration-dependent SDL video, input and joysticks before
  loading runtime assets/audio and legacy settings; its gameplay-input factory
  applies persisted device choices, configured key bindings, and record/constant/
  network startup options. It also initializes native player defaults and
  persisted controls and routes legacy save/load operations through the owned
  save archive and durable settings store. Episode selection now initializes
  the owned episode archive directly, with convenience selection/new-game/
  network-host factories using that runtime-owned initializer. Supports
  configuration persistence and reverse-order cleanup after both successful
  and failed startup.
- [x] `src-csharp/OpenTyrian.Core/LevelGameplayRuntime.cs` - groups the
  per-game combat, enemy, projectile, event, background, movement, weapon,
  special-weapon, input, ship-info, player-frame, pickup and warning owners
  while preserving shared players, enemy slots, sound/request queues and
  launch-mode flags. Its map loader applies a selected `LevelMap` to the same
  background/event/enemy state; map/ship and per-level flag initialization
  reset that shared graph.
  The runtime factory carries current difficulty, multiplayer, network-player
  and demo settings. Combat-state initialization reuses the parity-verified
  native-order reset.
- [x] `tests/OpenTyrian.Core.Tests/GameRuntimePlatformTests.cs` - SDL
  dummy video/audio-driver success, incomplete-asset cleanup, live runtime
  sound-bank/audio-device/callback composition, persisted input-device and key
  binding composition into `PlayerInput`, runtime-owned episode initialization
  through local and network menu factories, native legacy game-save round trip,
  shared level gameplay graph and selected episode-map composition, and
  DOS/save/text persistence.
- [x] `src-csharp/OpenTyrian.Core/GameIdentity.cs` - the native product name
  and default `OPENTYRIAN_VERSION` identity constants.
- [x] `tests/OpenTyrian.Core.Tests/GameStartupTests.cs` - startup order,
  SDL/network failures, asset/audio/demo options, parsed launch-option state,
  and repeated mode loop checks.
- [x] `src-csharp/OpenTyrian.Core/LevelHud.cs` - shield regeneration, Galaga
  rewards, weapon-bar and power display, and end-of-frame state updates.
- [x] `tests/OpenTyrian.Core.Tests/LevelHudTests.cs` - 4,096 complete native
  state/host-call comparisons and short weapon-bar validation.
- [x] `tests/native/build_level_hud_reference.py` - unchanged native level HUD
  body with ordered host-call, player, shield, reward, power and bar traces.
- [x] `src-csharp/OpenTyrian.Core/LevelBackgroundScene.cs` - background
  composition order, filters, enemy strata/spawning, darkening and map/screen
  state through explicit host operations.
- [x] `tests/OpenTyrian.Core.Tests/LevelBackgroundSceneTests.cs` - 4,096
  native state and ordered host-call comparisons.
- [x] `tests/native/build_level_background_scene_reference.py` - unchanged
  native background-composition body with mocked rendering and enemy passes.

- [x] `src-csharp/OpenTyrian.Core/ArmorWarning.cs` - low-armor rescue spawning,
  native byte color/sound cadence and multiplayer owner rules; shared warning
  state and concrete enemy/item/font/surface host.
- [x] `tests/OpenTyrian.Core.Tests/ArmorWarningTests.cs` - 16,384 native state/
  RNG/spawn/render traces; real enemy allocation/drop/speed/armor, padded
  warning pixels, full-pool and missing-drop validation.
- [x] `tests/native/build_armor_warning_reference.py` - unchanged armor warning
  body with mocked creation/rendering and actual native Mersenne Twister.

- [x] `src-csharp/OpenTyrian.Core/LevelPresentation.cs` - native viewport copy,
  inversion, searchlight, smooth-scroll timing and shared keyboard/setup flags.
- [x] `tests/OpenTyrian.Core.Tests/LevelPresentationTests.cs` - 2,048 native
  viewport pixel/timing/state traces including padded/asymmetric pitches and
  aliased buffers; SDL presentation, skipped frames and failure propagation.
- [x] `tests/native/build_level_presentation_reference.py` - unchanged native
  viewport presenter with buffer hashes, aliasing and PIT timing traces.

- [x] `src-csharp/OpenTyrian.Core/LevelTimerDisplay.cs` - debug text/FPS,
  pickup bonus flash, alternating Pentium frame pacing and countdown expiration.
- [x] `tests/OpenTyrian.Core.Tests/LevelTimerTests.cs` - 16,384 native
  debug/notice/pacing/timer traces; real event jumps and padded font/SDL pixels,
  shared pickup/keyboard/settings state, invariant formatting and error handling.
- [x] `tests/native/build_level_timer_reference.py` - original native debug
  and countdown function with controlled ticks and exact overlay/state traces.
- [x] `src-csharp/OpenTyrian.Core/LevelPlayerInitializer.cs` - reset both
  player histories/positions, adjust demo difficulty, and sequence native
  level picture/options/name/presentation/gamma/fade/explosion-shape setup.
- [x] `tests/OpenTyrian.Core.Tests/LevelPlayerInitializerTests.cs` - 512
  native player/history/startup traces; concrete gamma/palette fade/font,
  conditional explosion-sheet load and sequential failure behavior.
- [x] `tests/native/build_level_player_init_reference.py` - unchanged native
  level player/display initializer with state and host-call traces.
- [x] `src-csharp/OpenTyrian.Core/LevelMapShipInitializer.cs` - initialize
  three native map row pointers/offsets, reset the current and third-layer
  player-frame camera accumulators, layer positions and delays, music fade,
  shot power, starfield speed and both players through shared ship setup.
- [x] `tests/OpenTyrian.Core.Tests/LevelMapShipInitializerTests.cs` - 1,024
  extracted-native traces, including pointer-relative map strides, preserved
  old/secondary camera accumulators and native reset of the third-layer offset,
  loaded map layers/header indices,
  background pacing and player startup state.
- [x] `tests/native/build_level_map_ship_init_reference.py` - extract the
  original native initializer body and compile controlled map/pointer/player
  fixtures for direct differential verification.
- [x] `src-csharp/OpenTyrian.Core/LevelFlagsInitializer.cs` - reset level-entry
  control, gameplay, background, movement, timer, warning, event, filter,
  boss-bar, pickup and player-frame state through the shared subsystem owners.
- [x] `tests/OpenTyrian.Core.Tests/LevelFlagsInitializerTests.cs` - 16,384
  extracted-native flag traces, including signed/unsigned boundaries and
  boss-bar armor/color preservation.
- [x] `tests/native/build_level_flags_reference.py` - compile the unchanged
  native level-entry flag function with explicit state fixtures and traces.
- [x] `src-csharp/OpenTyrian.Core/LevelStatusDisplayInitializer.cs` - share
  level-entry shield, HUD, pickup, cube and flash resets across their existing
  state owners; preserve native host-action order and byte truncation.
- [x] `tests/OpenTyrian.Core.Tests/LevelStatusDisplayInitializerTests.cs` - 256
  extracted-native startup cases compare shield timing, both players' shields,
  armor clamp, superbombs, cube/flash/notice resets and song/button order.
- [x] `tests/native/build_level_status_display_reference.py` - extract the
  unchanged C routine and trace startup state plus text/music/button actions.
- [x] `src-csharp/OpenTyrian.Core/LevelCombatStateInitializer.cs` - reset
  combat, projectile, enemy, effect, weapon, level and background state through
  existing subsystem owners while retaining native network/Galaga behavior.
- [x] `tests/OpenTyrian.Core.Tests/LevelCombatStateInitializerTests.cs` - 256
  extracted-native cases verify owned state clears, retained enemy availability,
  per-mode network reset, draw action, Galaga difficulty and map-wrap offsets.
- [x] `tests/native/build_level_combat_state_reference.py` - extract the
  unchanged C initializer and compile a standalone trace oracle.

## Baseline file inventory

### Repository root

- [x] `.editorconfig` - language-agnostic whitespace and encoding policy
  remains valid for both native reference and managed source files.
- [x] `.gitignore` — added nested C# build-output exclusions.
- [x] `COPYING` - repository license text is retained unchanged.
- [x] `Doxyfile` - retained for documentation of the native compatibility
  reference while native code remains in the repository.
- [x] `Makefile` - retained to build and verify the native compatibility
  reference until the final removal gates pass.
- [x] `NEWS` - historical release notes are retained unchanged.
- [x] `README` - retained as legacy/native reference documentation; the
  managed project instructions are in `README.md`.
- [x] `lower-script.sh` - retained as a legacy data-tree preparation utility;
  it is not part of the managed runtime or release executable.

### Documentation and tools

- [x] `doc/files.txt` - external runtime-asset descriptions are retained as
  reference documentation; the assets themselves remain external.
- [x] `doc/tools/weapon.py` - retained as the Python compatibility reference
  for direct Tyrian HDT binary/XML editing; managed replacement is
  `src-csharp/OpenTyrian.Core/HdtWeaponArchive.cs` and
  `src-csharp/OpenTyrian.HdtWeaponTool/`.
- [x] `doc/tyrian.hdt.txt` - retained as the binary-format reference used by
  the extractor and compatibility work.

### Linux integration and artwork

- [x] `linux/icons/tyrian-128.png` - existing game artwork retained for
  native-reference packaging; managed packaging remains a release task.
- [x] `linux/icons/tyrian-22.png` - existing game artwork retained for
  native-reference packaging; managed packaging remains a release task.
- [x] `linux/icons/tyrian-24.png` - existing game artwork retained for
  native-reference packaging; managed packaging remains a release task.
- [x] `linux/icons/tyrian-32.png` - existing game artwork retained for
  native-reference packaging; managed packaging remains a release task.
- [x] `linux/icons/tyrian-48.png` - existing game artwork retained for
  native-reference packaging; managed packaging remains a release task.
- [x] `linux/man/opentyrian.6` - retained for the native reference; update
  the installed manual when managed release packaging is integrated.
- [x] `linux/opentyrian.desktop` - retained for the native reference; update
  its executable target when managed release packaging is integrated.

### Native source

- [x] `src/animlib.c` - `AnimationFile`, `AnimationDecoder`, `AnimationPlayer`; complete synthetic native playback plus real SDL presentation/input.
- [x] `src/animlib.h` - managed animation APIs replace native declarations; required host services retain gameplay/platform separation.
- [x] `src/arg_parse.c` — all five parser functions are mapped to
  `ArgumentParser`; unit scenarios and the native game-options oracle verify
  permutations, clustered/long options, argument binding and diagnostics.
- [x] `src/arg_parse.h` — `Options`/`Option` and status constants are replaced
  by managed option records/constants; native game-option compatibility and
  parser tests pass.
- [x] `src/backgrnd.c` - all fifteen background, filter, tile-row and
  starfield functions map to `BackgroundRenderer`/`Starfield`; native pixel,
  pitch, layer, filter and seeded-starfield regression coverage is registered.
- [x] `src/backgrnd.h` - background layer offsets, motion/delay, smoothie
  filters, starfield speed, and rendering operations map to typed
  `BackgroundRenderer`/`BackgroundLayer`/`JukeboxStarfield` state.
- [x] `src/config.c` - all eight functions are integrated through
  `GameSettingsStore`, `LegacySaveGame`, `LegacySaveCipher`,
  `SavedGameCatalog`, and `GameRuntimeSettings`; runtime owns configuration,
  legacy saves, input settings and processor/game-speed state.
- [x] `src/config.h` - save/configuration records and declarations map to
  `DosConfiguration`, `LegacySaveArchive`, `LegacySaveSlot`, `GameConfiguration`,
  `SavedGameCatalog`, and runtime/game state; difficulty, keyboard-setting and
  player-shot ordinals are typed and compatibility-tested.
- [x] `src/config_file.c` — all configuration functions replaced by the
  managed configuration model/parser/writer; regression fixtures pass.
- [x] `src/config_file.h` — native configuration declarations and inline
  helpers replaced by managed types and methods; configuration tests pass.
- [x] `src/data_extractor.c` - managed CLI delegates to the fully integrated
  legacy extraction pipeline; synthetic native output/CLI parity verified.
- [x] `src/destruct.c` - `JE_destructGame` and all Destruct game/session
  operations map to `DestructUnits.RunGame`; native configuration, map setup,
  gameplay ticks, state, RNG, rendering, and cleanup parity are covered.
- [x] `src/destruct.h` - the native Destruct entrypoint maps to the managed
  `DestructUnits.RunGame` API and its explicit host boundary.
- [x] `src/editship.c` - replaced by `EditedShips`; complete optional archive and decrypt behavior verified against compiled native source.
- [x] `src/editship.h` - managed owned sprite/record state replaces globals and the opaque byte-array typedef; declarations integrated and verified with archive/ship tests.
- [x] `src/episodes.c` - item/mod parsing, validation, indexing, extraction
  and episode selection map to `ItemDefinitions`, `ItemDatabase`,
  `ItemDataExtractor`, and `EpisodeState`; native parser/data/CLI comparisons
  and managed loading tests pass.
- [x] `src/episodes.h` - native current/legacy item layouts, item IDs,
  episode flags, and APIs map to typed item records/database and episode
  state; legacy serialization and reference-resolution tests pass.
- [x] `src/file.c` — data-directory lookup, open/error paths, file existence,
  EOF position, exact reads/writes and PCX header output are mapped to
  `DataFiles`/`BinaryFile`; filesystem, failure and binary-layout tests pass.
- [x] `src/file.h` — endian-aware typed read/write helpers map to `BinaryFile`
  fixed-width APIs; all declared helpers and native-width layout behavior are
  covered by binary-format regression tests.
- [x] `src/font.c` - all font drawing functions map to `FontRenderer` and are
  integrated with the managed sprite/surface pipeline.
- [x] `src/font.h` - font and alignment enums/declarations map to `Font` and
  `FontAlignment`; native font operation behavior is covered by pixel tests.
- [x] `src/fonthand.c` - font ASCII mapping and all text/glow/warning
  operations map to managed font, text-glow, character-glow, and warning
  services; native pixels, state and timing are covered.
- [x] `src/fonthand.h` - shade constants, glyph map and warning state map to
  typed font APIs and their owning warning/glow state objects.
- [x] `src/game_menu.c` - item/shop/drawing/navigation/weapon-view operations
  map to managed selection menus, player progression, item screens, and
  projectile previews; native menu traces and concrete surface rendering pass.
- [x] `src/game_menu.h` - menu state, data-cube, screen, and drawing
  declarations map to typed managed menu/state/presentation interfaces.
- [x] `src/helptext.c` - encrypted help loading, help-box rendering, and
  menu-help data map to `HelpTextData`/`HelpSystem`; archive, text, drawing,
  navigation, and SDL integration tests pass.
- [x] `src/helptext.h` - help text groups, capacities, menu maps, and display
  state map to typed managed text arrays and help-system state.
- [x] `src/joystick.c` - complete managed control/config/detector and SDL device counterparts, synthetic native parity and real virtual-SDL integration; physical-controller release checks remain pending.
- [x] `src/joystick.h` - typed assignment/device/state declarations and input services; verified control/config/SDL integration.
- [x] `src/jukebox.c` - `Jukebox`, concrete presentation/audio adapters; native control/title traces and real SDL integration.
- [x] `src/jukebox.h` - managed `Jukebox.Run` entrypoint and required platform services.
- [x] `src/keyboard.c` - complete event, key, mouse, wait, text and relative
  input behavior maps to `InputState`/`SdlInputPlatform`; native traces and
  actual SDL integration pass.
- [x] `src/keyboard.h` - input flags, coordinates, keys, text and focus state
  map to typed `InputState` and platform services.
- [x] `src/lds_play.c` - all LDS load, sequence, register, effect, sound and
  playback functions map to `LdsPlayer`; native command/register/PCM parity
  and SDL audio integration pass.
- [x] `src/lds_play.h` - player, instrument, channel and pattern state map to
  owned managed LDS objects and fixed-width fields.
- [x] `src/loudness.c` - SDL audio initialization, sample-rate-matched OPL2/LDS
  synthesis, PCM callback mixing, music controls and teardown are represented
  by the managed audio/mixer/player services and tested; final game-host
  ownership remains a separate integration gate.
- [x] `src/loudness.h` - audio, music, sample-rate and control declarations map
  to managed SDL audio, mixer, music archive/controller and LDS player APIs.
- [x] `src/lvllib.c` - `JE_analyzeLevel` maps to `LevelArchive.Analyze`;
  archive count, offsets and final length are managed and bounded.
- [x] `src/lvllib.h` - level archive position/count/name state maps to
  `LevelArchive` and `EpisodeState`; native declarations are replaced.
- [x] `src/lvlmast.c` - `shapeFile` maps to the verified
  `LevelMaster.ShapeFiles` table.
- [x] `src/lvlmast.h` - all capacity macros and `shapeFile` declaration map
  to typed `LevelMaster` constants/table; native capacities and all 34 shape
  bytes are regression-tested.
- [ ] `src/mainint.c`
- [x] `src/mainint.h` - shared input, save, section, pause, and presentation
  declarations map to typed input/game/startup/screen owners and verified
  managed APIs; no C header declarations remain required by the C# projects.
- [x] `src/menus.c` - verified managed gameplay/episode/difficulty selectors and presentation adapter; native synthetic parity and real SDL integration, whole-game callers still pending.
- [x] `src/menus.h` - managed byte-label lists, selection state and public selectors; all native module functions verified.
- [x] `src/mouse.c` — cursor capture, filtered/unfiltered drawing and restore
  are mapped to `MouseCursor`; native cursor traces verify clipping, scanline
  bounds, sprite output and restoration.
- [x] `src/mouse.h` — cursor/button state and enum declarations are represented
  by `MouseCursor` and `InputState`; SDL-backed input/presentation integration
  and the native cursor oracle pass.
- [x] `src/mtrand.c` — all four RNG functions are mapped to
  `MersenneTwister`; default/published vectors, seed truncation, repeatability,
  state wraparound and float-bit behavior are verified.
- [x] `src/mtrand.h` — `MT_RAND_MAX` and the four native function declarations
  are represented by managed 32-bit RNG APIs/constants; regression vectors
  verify the public behavior.
- [x] `src/musmast.c` - shared song-buy selection, fade flag, song titles and
  constants map to `MusicMaster`; episode/menu and level/event/frame access
  share the same managed state.
- [x] `src/musmast.h` - music constants and extern declarations map to the
  `MusicMaster` managed API.
- [x] `src/network.c` - protocol, queued control/state synchronization, peer negotiation, graphical shutdown, and special-request reset map to the owned `NetworkSession`, SDL2_net transport, presentation adapter, and runtime startup/gameplay synchronization paths; native packet traces and real two-peer SDL2_net tests pass.
- [x] `src/network.h` - native network globals and declarations are replaced by runtime-owned session, transport, packet, and request state; network start and level-state synchronization consume the same session.
- [x] `src/nortsong.c` - PIT delays, speed and volume controls map to
  `DelayTimer`; sound bank ownership/conversion/playback maps to `SoundBank`,
  `IAudioSampleConverter`, and `AudioMixer`. Dummy SDL audio integration and
  timer/audio compatibility tests pass.
- [x] `src/nortsong.h` - timing, sample, volume, and sound declarations map
  to managed timer/audio/configuration state; native global arrays are replaced
  by owned managed buffers.
- [x] `src/nortvars.c` - `PlayerCombat.DrawBar`, `IndexedSurface.ShadowedGauge`, `InputState.AnyButton`/`ClearKeyboard`; native pixel/input parity including warnings.
- [x] `src/nortvars.h` - typed surface/input counterparts; `inputDetected` replaced by per-instance `InputState.InputDetected`, retained across button poll/no-op; remaining game-menu callers pending.
- [x] `src/opentyr.c` - all utility-token and setup-menu functions map to
  `TextTokenReader`/`SetupMenu`; setup is composed from the runtime SDL,
  configuration, audio and title-menu hosts, and the managed startup loop is
  covered by native-trace tests.
- [x] `src/opentyr.h` - product identity maps to `GameIdentity`; token-reader
  and setup entrypoints map to typed managed APIs; fixed-width Pascal aliases
  use explicit C# widths and common count/min/max/math macros map to idiomatic
  managed operations where used. No C header declarations are required by
  the managed projects.
- [x] `src/opentyrian_version.h` - native product version macro and default
  value map to `GameIdentity.ProgramName`/`Version`; startup output verifies
  the exact banner identity.
- [x] `src/opl.c` - all OPL2/OPL3 operator, envelope, register and sample
  generation functions map to `OplChipState`/`OplOperatorTimingState`; native
  OPL2 PCM and OPL3 stereo topology comparisons pass, and audio startup uses
  the managed chip.
- [x] `src/opl.h` - fixed-width/pointer-width types and register/sample API
  map to managed OPL state, `IOplSynthesizer`, and the `int16` render API.
- [x] `src/palette.c` - palette loading, RGB/YUV conversion, fades and
  stepwise updates map to `PaletteSystem`, with exact pixel/color and callback
  order checks.
- [x] `src/palette.h` - palette storage and fade declarations map to managed
  `PaletteColor`, `PaletteSystem`, and video palette buffers.
- [x] `src/params.c` - complete `GameOptions` counterpart, native state/CLI parity and executable help/error integration; original retained as baseline.
- [x] `src/params.h` - declarations and mode globals replaced by typed `GameOptions` state/parser; verified with native CLI traces.
- [x] `src/pcxload.c` - `JE_loadPCX` maps to `PictureLoader.LoadPcx`;
  palette-marker, RLE, pitched-screen and malformed-run behavior is tested.
- [x] `src/pcxload.h` - the standalone PCX loading declaration is replaced by
  the typed managed stream/surface/palette API.
- [x] `src/pcxmast.c` - picture and face palette tables map to the managed
  constants in `PictureLoader`.
- [x] `src/pcxmast.h` - picture count/palette declarations map to
  `PictureLoader.PictureCount` and `FacePaletteIndices`; per-call offsets are
  bounded managed state rather than a shared native array.
- [x] `src/picload.c` - `JE_loadPic` maps to `PictureLoader.LoadPicture`;
  archive RLE, palette selection, storage and screen-pitch behavior are tested.
- [x] `src/picload.h` - the picture-loader declaration is replaced by the
  typed managed stream/surface/palette API.
- [x] `src/player.c` - player initialization, life/weapon progression and
  pickup behavior map to `PlayerRules`/`PlayerState`; managed references are
  wired through lifecycle, combat, pickups, level and save flows.
- [x] `src/player.h` - player/items/weapon/sidekick data and alive/life rules
  map to typed managed state; legacy item payload, native-width cash behavior,
  lifecycle and multiplayer integration are tested.
- [x] `src/shots.c` - shot creation, movement/drawing, direction control and
  shop-preview simulation map to `PlayerShots`; used in player weapons,
  pickups, specials, enemy collision, and weapon-preview flows with native tests.
- [x] `src/shots.h` - player-shot data, capacity, availability and public
  operations map to owned typed `PlayerShotState`/`PlayerShots` objects.
- [x] `src/sizebuf.c` — all seven buffer operations are mapped to
  `SizeBuffer`; regression tests cover bounded reads/writes, error propagation,
  seek modes, cursor updates and little-endian word decoding.
- [x] `src/sizebuf.h` — the native `sizebuf_t` state and function declarations
  are represented by the managed owned-memory buffer API; all operations pass
  the dedicated size-buffer regression cases.
- [x] `src/sndmast.c` - sound titles and window voice table map to
  `SoundMaster`; jukebox and level-event playback use the shared tables.
- [x] `src/sndmast.h` - sound/voice counts and IDs map to `SoundMaster` and
  `SoundId`; duplicate select/explosion ID 8 and all ranges are preserved.
- [x] `src/sprite.c` - sprite tables, compressed sheets, main-shape loading,
  ownership and every native blit variant map to `SpriteTable`,
  `SpriteSheet`, and `MainShapeTables`; drawing is used across managed menus,
  gameplay, text, effects, and presentations.
- [x] `src/sprite.h` - sprite dimensions/data, eight table indices, five
  shared sheets, accessors and load/draw declarations map to managed typed
  owners and methods; all inline accessors have dedicated parity coverage.
- [x] `src/starlib.c` - `JukeboxStarfield` and typed stars; complete native state/pixel/RNG parity; empty color stub omitted.
- [x] `src/starlib.h` - `JukeboxStar`/`JukeboxStarfield` replace declarations and global state.
- [x] `src/tinydir.h` - replaced by the managed `TinyDirectory` entry/list
  model. All callers and inline helpers use managed directory enumeration,
  indexed metadata, extension extraction, sorting, and file/subdirectory
  lookup; native-specific dirent buffer sizing is delegated to the CLR's
  directory enumerator because that helper is not built in the supported
  native configuration (`TINYDIR_USE_READDIR`).
- [ ] `src/tyrian2.c`
- [ ] `src/tyrian2.h`
- [ ] `src/varz.c` - native arcade/sidekick, player special-code, planet,
  enemy-launch, SuperTyrian drop and SuperArcade progression/loadout tables
  now share the managed `LegacyGameTables` API and are tested; mutable
  gameplay globals remain distributed across subsystem owners and still need
  end-to-end integration.
- [ ] `src/varz.h`
- [x] `src/vga256d.c` - drawing operations map to `IndexedSurface` primitives
  and are covered by pixel/pitch clipping tests.
- [x] `src/vga256d.h` - SDL surface drawing declarations, inline rectangle
  fill and segmented gauge map to `IndexedSurface` APIs.
- [x] `src/vga_palette.c` - 256-entry VGA palette table mapped to
  `JukeboxPalette.Colors`; all RGB bytes are hash-verified.
- [x] `src/vga_palette.h` - palette declaration maps to the managed palette
  API and typed `PaletteColor` values.
- [x] `src/video.c` - `SdlVideoDevice`/`VideoLayout`; real native SDL window/renderer/texture/fullscreen and all-scaler parity.
- [x] `src/video.h` - managed video surfaces, viewport, scaling modes and SDL device declarations.
- [x] `src/video_scale.c` - `VideoScaler` and HQ kernels; full native scaler table and real SDL output verified.
- [x] `src/video_scale.h` - `ScalerDefinition`, `VideoScaler`, `HqVideoScaler` and typed algorithms.
- [x] `src/video_scale_hqNx.c` - `HqVideoScaler`/`HqPatternKernels`; exact native pixels for all three HQ kernels/interpolations.
- [x] `src/xmas.c` — `ChristmasPrompt.IsChristmasTime` and the complete
  snow/choice loop in `ChristmasPrompt.Run` are differentially verified;
  startup owns the Christmas flag in `GameStartupState`.
- [x] `src/xmas.h` — `xmas` state and time/prompt declarations map to
  `GameStartupState.Christmas` and `ChristmasPrompt`.

### Visual Studio project and resources

- [x] `visualc/data_extractor.vcxproj` - retained to build the native data
  extractor reference until managed/native release gates pass.
- [x] `visualc/opentyrian.props.template` - retained for native MSVC
  compatibility builds until the native baseline can be removed.
- [x] `visualc/opentyrian.sln` - retained for native MSVC compatibility
  builds until the native baseline can be removed.
- [x] `visualc/opentyrian.vcxproj` - retained for native MSVC compatibility
  builds until the native baseline can be removed.
- [x] `visualc/resources.rc` - retained as a resource of the native Windows
  compatibility executable.
- [x] `visualc/saves/.gitkeep` - retained to preserve the native save-data
  directory structure; runtime save data remains untracked.
- [x] `visualc/sdl_paths.props.template` - retained for native MSVC
  compatibility builds until the native baseline can be removed.
- [x] `visualc/tyrian.ico` - retained as a resource of the native Windows
  compatibility executable.

## Function inventory

### `src/animlib.c`

- [x] `JE_loadPage` - `AnimationFile.LoadPage`; record totals and caching.
- [x] `JE_findPage` - `AnimationFile.FindPage`; boundary fixtures.
- [x] `JE_renderFrame` - `AnimationFile.RenderFrame`; page-record decoding.
- [x] `JE_playAnim` - `AnimationPlayer.Play`/`PlayFile`; complete native multi-page frame/host traces and SDL integration; malformed playback throws.
- [x] `JE_loadAnim` - `AnimationFile` constructor; LPF header and BGR palette.
- [x] `JE_closeAnim` - `AnimationFile.Dispose`; stream ownership fixtures.
- [x] `JE_playRunSkipDump` - `AnimationDecoder.Decode`; operation and
  malformed-input compatibility fixtures.

### `src/arg_parse.c`

- [x] `parse_args` - `ArgumentParser.Next`; native differential fixtures.
- [x] `permute` - `ArgumentParser.Permute`; native argument-order comparisons.
- [x] `parse_short_opt` - `ArgumentParser.ParseShort`; native clustered and
  argument-bearing option comparisons.
- [x] `parse_long_opt` - `ArgumentParser.ParseLong`; native exact,
  abbreviation, ambiguity, and diagnostic comparisons.
- [x] `ot_strchrnul` - `ArgumentParser.FindCharacterOrEnd`; native attached
  and unattached long-option comparisons.

### `src/backgrnd.c`

- [x] `JE_darkenBackground` - `BackgroundRenderer.Darken`; native pixels.
- [x] `blit_background_row` - `BackgroundRenderer.BlitRow`; native pixels.
- [x] `blit_background_row_blend` - `BlitRow(blend: true)`; native pixels.
- [x] `draw_background_1` - `BackgroundRenderer.DrawFirst`.
- [x] `draw_background_2` - `BackgroundRenderer.DrawSecond`.
- [x] `draw_background_2_blend` - `DrawSecond(blend: true)`.
- [x] `draw_background_3` - `BackgroundRenderer.DrawThird`.
- [x] `JE_filterScreen` - `BackgroundRenderer.FilterScreen`; native pixels.
- [x] `JE_checkSmoothies` - `BackgroundRenderer.CheckSmoothies`.
- [x] `lava_filter` - `BackgroundRenderer.Lava`; native pixels.
- [x] `water_filter` - `BackgroundRenderer.Water`; native pixels.
- [x] `iced_blur_filter` - `BackgroundRenderer.Blur(iced: true)`; native pixels.
- [x] `blur_filter` - `BackgroundRenderer.Blur`; native pixels.
- [x] `initialize_starfield` - `Starfield.Initialize`; native seeded output.
- [x] `update_and_draw_starfield` - `Starfield.UpdateAndDraw`; native pixels.

### `src/config.c`

- [x] `xor` - `LegacySaveCipher.XorCString`; native input NUL/key cycling and in-place byte behavior, empty key rejected as undefined native input.
- [x] `load_opentyrian_config` - `GameConfiguration.Load`/`LoadFile`; native defaults/state, actual SDL key-name lookup and diagnosed missing paths.
- [x] `save_opentyrian_config` - `GameConfiguration.Save`/`SaveFile`; exact native serialized fields/sections and durable real-file writes; I/O failures propagate.
- [x] `playeritems_to_pitems` - `PlayerItems.ToLegacyItems`; exact twelve-byte mapping.
- [x] `pitems_to_playeritems` - `PlayerItems.LoadLegacyItems`; item mapping/returned episode, weapon powers preserved.
- [x] `saveGameOperation` - `VersionedSaveGame.Write`/`SaveFile`; exact native headers/ID payloads and file persistence; NUL-truncated native output rejected before overwriting.
- [x] `loadGameOperation` - `VersionedSaveGame.Load`/`LoadFile`; native current/previous player state, modes and episodes; bounded parsing and required initialization callback.
- [x] `JE_saveGame` - `LegacySaveGame.Save`; all 22 slots/eight modes, exact archive persistence, required configuration-save callback.
- [x] `read_savegame_metadata` - `SavedGameCatalog.ReadMetadata`/`ReadMetadataFile`; exact native fields/version compatibility; unsupported or malformed files diagnosed rather than accepted with empty metadata.
- [x] `build_save_game_list` - `SavedGameCatalog.BuildList`; real tinydir sorting/filter parity; typed list/count replaces allocation/sentinel ownership.
- [x] `JE_loadGame` - `LegacySaveGame.Load`; native modes/player/cash/powers/sidekick/episode state and required initialization callback.
- [x] `JE_initProcessorType` - `GameRuntimeSettings.InitializeProcessor`; native detail flags and preserved unknown-speed behavior.
- [x] `JE_setNewGameSpeed` - `GameRuntimeSettings.SetGameSpeed`; native PIT speed/frame targets; asserted invalid mode throws.
- [x] `JE_encryptSaveTemp` - `LegacySaveCipher.Encrypt`; exact native bytes and four wrapping checksums.
- [x] `JE_decryptSaveTemp` - `LegacySaveCipher.Decrypt`; exact payload/diagnostics/collision behavior; corruption throws instead of process exit.
- [x] `get_user_directory` - `GameSettingsStore.GetUserDirectory`; cached Windows/Unix environment precedence, explicit overlong-path rejection.
- [x] `JE_loadConfiguration` - `GameSettingsStore.Load`; native 28-byte config/defaults, volume call, archive/high-score RNG ordering and processor overrides.
- [x] `JE_saveConfiguration` - `GameSettingsStore.Save`; native archive/DOS/text order and bytes, managed durable flush/error propagation.

### `src/config_file.c`

- [x] `config_oom` - `Configuration.ThrowOutOfMemory`; emits the native
  diagnostic and raises managed `OutOfMemoryException` instead of terminating
  the process, verified with captured error output.
- [x] `string_init_len` - Managed strings replace the native inline/heap
  `ConfigString` storage; quoted, unquoted and empty-string fixtures pass.
- [x] `string_deinit` - Garbage-collected strings replace explicit frees;
  `Configuration.Clear` releases the owning object graph.
- [x] `string_equal_len` - Ordinal managed string equality in
  `ConfigSection.GetOption`; exact-key and case-sensitive lookup tests pass.
- [x] `config_init` - `Configuration` constructor.
- [x] `config_deinit` - `Configuration.Clear`; managed reset/collection.
- [x] `init_section` - `ConfigSection` constructor initializes type, optional
  name and an empty managed option collection.
- [x] `deinit_section` - Managed ownership replaces recursive native frees;
  `Configuration.Clear` drops the section collection.
- [x] `config_add_section_len` - `Configuration.AddSection`.
- [x] `config_find_sections` - `Configuration.FindSections`; managed enumeration.
- [x] `config_find_section` - `Configuration.FindSection`.
- [x] `config_find_or_add_section` - `Configuration.FindOrAddSection`.
- [x] `init_option_value` - `ConfigOption.SetValue` initializes scalar/list
  state and empty values; config model tests cover both empty and populated
  states.
- [x] `deinit_option_value` - Managed value lists replace scalar/list
  deallocation; `SetValue` and `RemoveValue` lifecycle tests pass.
- [x] `init_option` - `ConfigOption` constructor initializes its key and value;
  model tests exercise scalar and list options.
- [x] `deinit_option` - Garbage collection replaces explicit key/value frees;
  collection clearing and option replacement are covered by model tests.
- [x] `append_option` - `ConfigSection.AppendOption`.
- [x] `get_option_len` - `ConfigSection.GetOption`; field-length strings.
- [x] `config_set_option_len` - `ConfigSection.SetOption`.
- [x] `config_get_option` - `ConfigSection.GetOption`.
- [x] `config_get_or_set_option_len` - `ConfigSection.GetOrSetOption`.
- [x] `config_set_string_option_len` - `ConfigSection.SetOption`.
- [x] `config_get_string_option` - `ConfigSection.TryGetString`.
- [x] `config_get_or_set_string_option` - `ConfigSection.GetOrSetString`.
- [x] `config_set_bool_option` - `ConfigSection.SetBoolean`.
- [x] `config_get_bool_option` - `ConfigSection.TryGetBoolean`.
- [x] `config_get_or_set_bool_option` - `ConfigSection.GetOrSetBoolean`.
- [x] `config_set_int_option` - `ConfigSection.SetInt32`.
- [x] `config_get_int_option` - `ConfigSection.TryGetInt32`.
- [x] `config_get_or_set_int_option` - `ConfigSection.GetOrSetInt32`.
- [x] `config_set_uint_option` - `ConfigSection.SetUInt32`.
- [x] `config_get_uint_option` - `ConfigSection.TryGetUInt32`.
- [x] `config_get_or_set_uint_option` - `ConfigSection.GetOrSetUInt32`.
- [x] `config_set_value_len` - `ConfigOption.SetValue`.
- [x] `config_add_value_len` - `ConfigOption.AddValue`.
- [x] `config_remove_value` - `ConfigOption.RemoveValue`.
- [x] `config_get_value` - `ConfigOption.Value`.
- [x] `is_whitespace` - `Configuration.IsWhitespace`.
- [x] `is_end` - `Configuration.IsEnd`.
- [x] `is_whitespace_or_end` - combined whitespace/end predicates.
- [x] `match_directive` - `Configuration.ReadDirective`.
- [x] `match_nonquote_field` - `Configuration.TryReadField` unquoted branch.
- [x] `parse_quote_field` - `Configuration.TryReadField` quoted branch.
- [x] `parse_field` - `Configuration.TryReadField`.
- [x] `config_parse` - `Configuration.Parse`; native differential fixtures.
- [x] `write_field` - `Configuration.WriteField`; native byte escapes.
- [x] `config_write` - `Configuration.Write`; native differential fixtures.

### `src/data_extractor.c`

- [x] `print_usage` - `ItemDataExtractor.PrintUsage`; native CLI text parity.
- [x] `main` - `ItemDataExtractor.Run` and extractor `Program.cs`.

### `src/destruct.c`

- [x] `get_unit_by_name` - `DestructUnitNames.GetUnitByName`; exact case-sensitive native names and `UNIT_NONE` fallback.
- [x] `DE_isValidUnit` - `DestructUnits.IsValid`; only positive health is valid.
- [x] `DE_ResetUnits` - `DestructUnits.ResetUnits`; clears health for each configured installation of both players and leaves excess capacity untouched.
- [x] `DE_ResetWeapons` - `DestructUnits.ResetWeapons`; enables configured shot/explosion slots while leaving excess capacity untouched.
- [x] `DE_RaiseAngle` - `DestructUnitState.RaiseAngle`; native 0.01-radian step and upper bound.
- [x] `DE_LowerAngle` - `DestructUnitState.LowerAngle`; native 0.01-radian step and zero lower bound.
- [x] `DE_RaisePower` - `DestructUnitState.RaisePower`; native 0.05 step and five-unit upper bound.
- [x] `DE_LowerPower` - `DestructUnitState.LowerPower`; native 0.05 step and one-unit lower bound.
- [x] `DE_CycleWeaponUp` - `DestructUnits.CycleWeaponUp`; native per-unit weapon availability, skipping and wraparound.
- [x] `DE_CycleWeaponDown` - `DestructUnits.CycleWeaponDown`; native per-unit weapon availability, skipping and wraparound.
- [x] `DE_RunTickCheckEndgame` - `DestructUnits.CheckEndgame`; left-first elimination priority, mode score award, 32-bit score wrap and sound queue.
- [x] `DE_ResetActions` - `DestructUnits.ResetActions`; clears all eight action flags for both players.
- [x] `DE_ResetPlayers` - `DestructUnits.ResetPlayers`; resets CPU/selection/timing/score/AI state and copies each player's 8-by-4 default key map, retaining unrelated state.
- [x] `DE_ResetAI` - `DestructUnits.ResetAi`; initializes live CPU units' aim, power and map-specific default weapons, retaining dead and human units.
- [x] `DE_ResetLevel` - `DestructUnits.ResetLevel`; resets resources, invokes terrain generation, then initializes CPU units in native order.
- [x] `DE_RunTickPlaySounds` - `DestructUnits.PlayQueuedSounds`; eight-channel order, player/effect volume split, and per-entry clear after successful playback.
- [x] `JE_pixCool` - `DestructUnits.DrawCoolPixel`; center/edge colors, byte wrapping and clipped screen coordinates.
- [x] `DE_RunTickCycleDeadUnits` - `DestructUnits.CycleDeadUnits`; skips destroyed/unarmed installations and wraps selection for living players.
- [x] `DE_RunTickAnimate` - `DestructUnits.AnimateUnits`; living-unit four-frame cycle with native static/animated type rules.
- [x] `DE_DestroyUnit` - `DestructUnits.DestroyUnit`; explosion-first callback, heli/satellite behavior, rounded position, and native unsigned score/count arithmetic.
- [x] `DE_RunTickDrawWalls` - `DestructUnits.DrawWalls`; configured wall iteration, existing-wall filtering, native sprite index, and draw order.
- [x] `DE_RunTickDrawCrosshairs` - `DestructUnits.DrawCrosshairs`; both aim formulas, heli movement offset, HUD clipping, and native pixel colors.
- [x] `DE_RunTickDrawHUD` - `DestructUnits.DrawHud`; exact panel geometry/colors, weapon sprite/name, damage/score text, and native signed score display.
- [x] `DE_GravityDrawUnit` - `DestructUnits.DrawUnit`; per-player/unit sprite bases, heli direction banks, angle frame selection, and rounded Y position.
- [x] `DE_GravityLowerUnit` - `DestructUnits.LowerUnit`; terrain-gated regular/heli lowering, native movement, and ground clamp.
- [x] `DE_GravityFlyUnit` - `DestructUnits.FlyUnit`; ground/ceiling limits, type-specific gravity, rounded stability checks, and landing state.
- [x] `JE_stabilityCheck` - `DestructUnits.IsStableAt`; 12 bottom-edge terrain samples, dirt threshold, and checked surface bounds.
- [x] `aliasDirtPixel` - `DestructUnits.AliasDirtPixel`; four-neighbor dirt shading and screen-edge handling.
- [x] `JE_aliasDirt` - `DestructUnits.AliasDirt`; in-place top-to-bottom aliasing over the native pitch.
- [x] `JE_tempScreenChecking` - `DestructUnits.CheckTemporaryScreen`; effect palette decay, optional sequential dirt aliasing, and pitch-aware copy.
- [x] `DE_RunTickGravity` - `DestructUnits.RunGravity`; configured two-player iteration, dead/satellite filtering, grounded/flying movement, then native unit drawing.
- [x] `load_destruct_config` - `DestructUnits.LoadConfig`; native gameplay options, key-name validation/default serialization, custom roster filtering/padding, and byte narrowing.
- [x] `JE_destructGame` - `DestructUnits.RunGame`; configuration, bounded resource allocation, per-player installation capacity, sprite lifetime, initial display, reusable map setup, and guaranteed sprite release.
- [x] `JE_destructMain` - `DestructUnits.RunMain`; intro/setup, configured CPU state, mode selection, map reload loop, tick continue/exit transitions, and fade order.
- [x] `JE_introScreen` - `DestructUnits.ShowIntro`; native screen snapshot, centered prompts, palette transition, event wait, and background restoration.
- [x] `DrawModeSelectMenu` - `DestructUnits.DrawModeSelectMenu`; five base modes and optional custom row at native text positions and selection shade.
- [x] `JE_modeSelect` - `DestructUnits.RunModeSelect`; initial draw/fade, up/down wrapping, custom mode, Escape cancel, Enter select, black fade and screen restore.
- [x] `JE_generateTerrain` - `DestructUnits.GenerateTerrain`; native map modifiers/song selection, ordered terrain/unit/wall/ring composition, aliasing, presentation, and temporary-screen copy.
- [x] `DE_generateBaseTerrain` - `DestructUnits.GenerateBaseTerrain`; native RNG sequence, fuzzy/tall modifiers, rounded wave composition, and height clamps.
- [x] `DE_drawBaseTerrain` - `DestructUnits.DrawBaseTerrain`; the native 1..318 vertical brown terrain columns and validation of expected playfield geometry.
- [x] `DE_generateUnits` - `DestructUnits.GenerateUnits`; native player-specific X ranges, random terrain clearing and unit choice, satellite handling, defaults, health, and active-unit counts.
- [x] `DE_generateWalls` - `DestructUnits.GenerateWalls`; separate C/game RNG streams, obstruction retries, wall stack ordering/height and disabled-map cleanup.
- [x] `DE_generateRings` - `DestructUnits.GenerateRings`; native ring/point random-call order, trigonometric spread, rounding, and map clipping.
- [x] `JE_placementPosition` - `DestructUnits.PlaceTerrain`; native maximum-height selection and in-place leveling with validated segment bounds.
- [x] `JE_makeExplosion` - `DestructUnits.MakeExplosion`; shot-specific explosion dimensions/types, sound thresholds, pool selection, and random fallback.
- [x] `JE_eSound` - `DestructUnits.QueueExplosionSound`; persistent five-channel rotation and queue replacement.
- [x] `JE_superPixel` - `DestructUnits.DrawSuperPixel`; native 5x5 star pattern, edge clipping, intensity addition, and byte saturation.
- [x] `JE_helpScreen` - `DestructHelpScreen.Run`; all 25 native help lines, text layout, fade/present order, event wait, and screen restoration.
- [x] `JE_pauseScreen` - `DestructPauseScreen.Run`; half-volume pause, centered message, input wait, saved-screen restoration, and original-volume recovery.
- [x] `DE_RunTick` - `DestructUnits.RunTick`; native per-frame update/draw order, first-frame fade, input and sound sequencing, end-delay reload, CPU toggles, pause/help, and quit/reload outcomes.
- [x] `DE_RunTickExplosions` - `DestructUnits.RunTickExplosions`; explosion flare RNG order, horizontal wrapping, dirt/star rendering, collision callbacks, and width/lifetime progression.
- [x] `DE_TestExplosionCollision` - `DestructUnits.TestExplosionCollision`; live-unit filter, strict collision bounds, one health decrement per hit, and destruction callback.
- [x] `DE_RunTickShots` - `DestructUnits.RunTickShots`; shot movement/gravity, map and floor bounce, lifetime bounds, unit/wall/dirt collision, demolition, trails, and explosion requests.
- [x] `DE_DrawTrails` - `DestructUnits.DrawTrails`; native reverse draw/update order, conditional decay, hidden trail filtering, and rounded head position.
- [x] `DE_RunTickAI` - `DestructUnits.RunTickAi`; native random-call order, CPU angle/power memory, helicopter preference/movement, target hovering, fire suppression, and action latches.
- [x] `DE_RunTickGetInput` - `DestructUnits.RunTickGetInput`; per-player key maps, unknown-key slot termination, event service order, held movement keys, and one-shot key consumption.
- [x] `DE_ProcessInput` - `DestructUnits.ProcessInput`; angle/power/weapon controls, helicopter stability-gated movement, jumper launch, selection order, shot cooldown/effect dispatch, and projectile creation.
- [x] `DE_MakeShot` - `DestructUnits.MakeShot`; available-slot selection, grounded heli suppression, native sound choice, heli/jumper/default trajectories, and trail reset.
- [x] `DE_RunMagnet` - `DestructUnits.RunMagnet`; directed projectile acceleration, opposing airborne-helicopter displacement, installation bounds, and animation frame.

### `src/editship.c`

- [x] `JE_decryptShips` - `EditedShips.Decrypt`; reversed chained XOR and all four wrapping checksums verified. Invalid checksums throw instead of native exit 255; uninitialized trailing scratch bytes are not exposed.
- [x] `JE_loadExtraShapes` - `EditedShips.Load`; optional absence, ushort sprite-size truncation, real archive bytes, native zero-read rejection and sprite decoding verified. Failed loads retain prior managed state instead of leaving an unavailable native process.

### `src/episodes.c`

- [x] `item_data_error` - `ItemDefinitions.Error`; path/line/message and
  trailing extractor-guidance diagnostics match native stderr in parser and
  typed-item reference tests; managed callers throw instead of terminating
  the host process.
- [x] `value_in_array_je_byte` - `ItemDatabase.Contains`.
- [x] `add_random_shop_item` - `ItemDatabase.AddRandomShopItem`; native seeded selections.
- [x] `item_idx_from_jebyte` - `ItemDatabase.Index(type, int)`, byte promotion.
- [x] `item_idx_from_int` - `ItemDatabase.Index(type, int)`.
- [x] `item_idx` - `ItemDatabase.Index(type, string)`.
- [x] `get_item_index_from_int` - `ItemDatabase.TryFind(type, int, out index)`.
- [x] `get_item_index` - `ItemDatabase.TryFind` and loading `RequireIndex`.
- [x] `apply_item_field` - `ItemDatabase.Apply`; every native typed field compared.
- [x] `prepare_extracted_data_directories` - `ItemDataExtractor.Extract`.
- [x] `extract_item_data` - `ItemDataExtractor.Extract`.
- [x] `close_extracted_file` - `WriteCategory` disposal/finalization.
- [x] `open_extracted_category` - `WriteCategory` preservation/temporary open.
- [x] `item_data_realloc` - Managed `List<T>`/`Dictionary<TKey,TValue>`
  growth replaces native `realloc`; allocation failures propagate to the host
  rather than terminating the process. Native parser and typed-item parity
  tests cover collection growth.
- [x] `is_item_space` - `ItemDefinitions.IsSpace`.
- [x] `trim_item_text` - `ItemDefinitions.Trim`.
- [x] `valid_item_id_span` - `ItemDefinitions.ValidId`.
- [x] `valid_item_id` - `ItemDefinitions.ValidId`.
- [x] `item_type_from_name` - `ItemDefinitions.TypeFromName`.
- [x] `hash_item_id` - Ordinal string hashing in `ItemDefinitions`' dictionaries
  replaces the private FNV-1a hash; IDs remain case-sensitive.
- [x] `find_item_slot` - `ItemDefinitions.TryFind` uses the per-type ordinal
  dictionary; native parser and typed-item parity tests cover lookups/aliases.
- [x] `rebuild_item_slots` - CLR dictionary rehashing replaces explicit native
  slot-table rebuilds; native parser and typed-item parity tests pass.
- [x] `find_loading_item_index` - `ItemDefinitions.TryFind`, including aliases.
- [x] `replace_item_record` - `ItemDefinitions.Replace`.
- [x] `validate_item_field` - `ItemDefinitions.ValidateField`; native errors.
- [x] `read_item_bundle_text` - `ItemDefinitions.LoadBundle`.
- [x] `load_item_bundle` - `ItemDefinitions.ParseBundle`; native records.
- [x] `compare_item_paths` - `ItemDefinitions.ComparePaths`.
- [x] `load_additional_item_bundles` - mod portion of `ItemDefinitions.LoadAll`.
- [x] `load_items` - `ItemDatabase.Load`; base/mod ordering, typed install,
  forward/late references, required special indices, and native field parity.
- [x] `read_legacy_item_data` - `ItemDataExtractor.ReadLegacyData` and eight
  category readers; complete byte output compared with compiled C.
- [x] `JE_initEpisode` - `EpisodeState.Initialize`.
- [x] `JE_scanForEpisodes` - `EpisodeState.Scan`.
- [x] `JE_findNextEpisode` - `EpisodeState.FindNext`.

### `src/file.c`

- [x] `data_dir` - `DataFiles.Directory`.
- [x] `dir_fopen` - `DataFiles.Open`.
- [x] `dir_fopen_warn` - `DataFiles.OpenWarn`.
- [x] `dir_fopen_die` - `DataFiles.OpenRequired`.
- [x] `dir_file_exists` - `DataFiles.Exists`.
- [x] `ftell_eof` - `BinaryFile.Length`; position preservation verified.
- [x] `fread_die` - `BinaryFile.ReadExactly`; fragmented-read and EOF
  regression tests; errors propagate to the future host.
- [x] `fwrite_die` - `BinaryFile.WriteExactly`; output and write-failure
  regression tests.
- [x] `write_pcx_header` - `BinaryFile.WritePcxHeader`; exact 128-byte header
  layout verified for the native 320x200 screen.

### `src/font.c`

- [x] `draw_font_hv_shadow` - `FontRenderer.DrawShadow`.
- [x] `draw_font_hv_full_shadow` - `FontRenderer.DrawFullShadow`.
- [x] `draw_font_hv` - `FontRenderer.DrawHue`.
- [x] `draw_font_hv_blend` - `FontRenderer.DrawBlend`.
- [x] `draw_font_dark` - `FontRenderer.DrawDark`.

### `src/fonthand.c`

- [x] `JE_dString` - `FontRenderer.DrawString`.
- [x] `JE_fontCenter` - `FontRenderer.Center`.
- [x] `JE_textWidth` - `FontRenderer.TextWidth`.
- [x] `JE_textShade` - `FontRenderer.ShadeText`.
- [x] `JE_outText` - `FontRenderer.OutText`.
- [x] `JE_outTextModify` - `FontRenderer.OutTextModify`.
- [x] `JE_outTextAdjust` - `FontRenderer.OutTextAdjust`; tiny-font existence gate retained.
- [x] `JE_outTextAndDarken` - `FontRenderer.OutTextAndDarken`.
- [x] `JE_updateWarning` - `WarningBars.Update`; timer-2 gate, byte/signed color wrapping, exact stripe pixels, presentation/delay ordering and sound countdown verified in 32,768 native frames and actual character-glow integration.
- [x] `JE_outTextGlow` - `TextGlow.Draw`/`TextGlowHost`; native shadow/brightness frame ordering, warning hue, input skip behavior, delay/network/presentation calls and reset verified in 2,048 native calls plus actual font/input/PIT integration.

### `src/game_menu.c`

- [x] `playeritem_map` - `PlayerItems.GetMenuItemId`/`SetMenuItemId`; tests
  verify all seven menu slots map to the correct ID fields without changing
  weapon power, and invalid indices fail explicitly.
- [x] `JE_cashLeft` - `PlayerProgression.CashLeft`; all seven upgrade menu
  item types, Windows/Unix unsigned-long cash boundaries, weapon powers and
  ushort cumulative surcharge wrapping verified against the native C oracle.
- [x] `draw_item_screen_load_save` - `ItemScreenLoadSaveDrawing.Draw`; save/
  load pages, create-save and return rows, player-filtered list retrieval,
  selection shades, bounded episode labels, empty slots, and repeated header
  drawing verified against native traces.
- [x] `draw_item_screen_keyboard_config` - `ItemScreenKeyboardConfigurationDrawing.Draw`;
  all ten label rows, selected/unselected action and scancode shades, menu gate,
  and resulting choice count verified against native traces.
- [x] `draw_item_screen_joystick_config` - `ItemScreenJoystickConfigurationDrawing.Draw`;
  all 16 rows, no-device disabling, analog values/shading, two-slot assignment
  names, joystick selection, menu gating and choice count compared with native.
- [x] `draw_item_screen_upgrade_menu` - `ItemScreenUpgradeMenuDrawing.Draw`;
  affordable-selection correction and wrapping, selected-item updates and Done
  rollback, weapon power affordability, sidekick charge/style/animation resets,
  custom-ship labels, owned markers, item icons, cost shades, and localized
  title/Done rendering compared with native traces across all seven item types.
- [x] `load_cubes` - `DataCubeTextLoader.LoadCubes`; per-cube archive reads,
  one-based cube indices, ordered slot creation, and decoded/wrapped record
  contents match the native loader on a multi-record encrypted fixture.
- [x] `load_cube` - `DataCubeTextLoader.LoadCube`; encrypted Pascal records,
  marker/face parsing, metadata, paragraph spacing, tiny-font word wrapping,
  fixed 90-line storage and termination match the unchanged native C routine.
- [x] `JE_drawItem` - `ItemIconDrawing.Draw`; native item-type catalog mapping,
  normal and custom ship sheet/frame selection, omitted zero/unsupported icons,
  and 2x2 draw coordinates verified using synthetic item definitions.
- [x] `draw_ship_illustration` - `ShipIllustrationDrawing.Draw`; ship fallback,
  generator, optional front/rear weapons, sidekick icons, shield hue/brightness,
  component placement, and native draw ordering verified on a synthetic catalog.
- [x] `draw_item_screen_status` - `ItemScreenStatusDrawing.Draw`; cash/armor/
  shield status gating, two-player and SuperArcade/SuperTyrian labels, ship
  illustration routing, music/effects volume bars, and help-box state changes
  compared with native traces across supported menu states.
- [x] `draw_item_screen_data_cubes` - `ItemScreenDataCubeDrawing.Draw`; zero/
  multiple-cube layouts, highlighted help boxes, selection and submenu re-entry,
  face positioning, and face-palette color updates compared with native traces.
- [x] `draw_item_screen_footer` - `ItemScreenFooterDrawing.Draw`; two-player
  input-device labels and fallback assignments, frame-state resets, score/help
  ordering, palette restoration, and selected data-cube title placement matched
  against native traces.
- [x] `prepare_item_screen_frame` - `ItemScreenFramePreparation.Prepare`; full-game
  mode precedence, menu-frame buffer restore and data-cube exception,
  upgrade-power initialization, state resets, and next-level navigation setup
  verified against native traces.
- [x] `draw_item_screen` - `ItemScreenDrawing.Draw`; header/choice gates,
  data-cube icon coordinates and draw order, and delegated item-panel order
  compared with native traces across supported menu IDs.
- [x] `move_item_screen_selection` - `ItemScreenSelectionMovement.Move`; cursor
  wrapping, cursor sound suppression on data-cube text, selected weapon power/
  mode updates, and non-analog joystick row skips verified against native traces.
- [x] `handle_item_screen_horizontal_key` - `ItemScreenInputHandlers.HandleHorizontal`;
  option volume and resume behavior, joystick/device routing, and upgrade-power
  boundary dispatch verified against the native C trace.
- [x] `activate_item_screen_selection` - `ItemScreenInputHandlers.ActivateSelection`;
  keyboard-use state, upgrade "Done" power snapshot, and menu dispatch ordering
  verified against the native C trace.
- [x] `cancel_item_screen_selection` - `ItemScreenInputHandlers.CancelSelection`;
  quick-save restoration, upgrade rollback/cash recalculation, escape-menu
  routing, and palette retention for the data-cube page verified against the
  native C trace; root-quit handoff covered by a direct managed regression.
- [x] `show_item_screen_help` - `ItemScreenHelp.Show`; network no-op, help/
  music/picture order, next-level palette override, background/palette restore,
  presentation, and return flags compared against the native trace.
- [x] `handle_item_screen_key` - `ItemScreenKeyHandler.Handle`; shot-pattern
  debug adjustments with 16-bit wrapping, rear weapon-mode cycling, activation/
  cancel/help/movement/horizontal dispatch, and data-cube Home/End scrolling
  verified against the native trace.
- [x] `update_item_screen_frame` - `ItemScreenFrameUpdate.Update`; animation
  color reversal, data-cube scroll clamping/text/progress and mouse cursors,
  next-level redraw, selected-cube flash, weapon-simulation and regular-frame
  event/palette/presentation ordering compared with native traces.
- [x] `initialize_item_screen` - `ItemScreenInitializer.Initialize`; cached and
  first-time sprite loading, cube/song/picture/palette/presentation ordering,
  menu-state reset, owned-item insertion/replacement, frame-buffer copy, and
  inventory sorting verified against native traces.
- [x] `open_quick_save_menu` - `ItemScreenInputHandlers.OpenQuickSave`; data-cube
  parent redirection and complete quick-save/save-page/palette state verified
  against the native C trace.
- [x] `wait_for_item_screen_input` - `ItemScreenInputWait.WaitForInput`; event and
  mouse polling, Alt-S/Alt-L quick-save exclusion and sequential precedence,
  data-cube cursor/page/keyboard/joystick scrolling, directional precedence,
  input suppression, and both scroll clamps verified against native traces.
- [x] `adjust_item_screen_volume` - `ItemScreenVolumeAdjustment.Adjust`; slider
  hit ranges, volume quantization/clamps, disable-flag clearing, resume-song
  ordering, final volume application and cursor sound verified natively.
- [x] `get_item_screen_mouse_selection` - `ItemScreenInputHandlers.GetMouseSelection`;
  all supported menus and negative/boundary/large mouse-Y inputs, including
  full-game, two-player, and next-level special row rules, match native traces.
- [x] `select_item_screen_menu_option` - `ItemScreenMenuSelection.SelectOption`;
  invalid-row no-op, upgrade Done cash return, repeated-choice dispatch,
  affordability rejection, weapon-mode switch, and selected power sync compared
  against native traces.
- [x] `adjust_item_screen_weapon_power` - `ItemScreenInputHandlers.AdjustWeaponPowerOnMouse`;
  all pointer hitbox edges, unavailable-power feedback, weapon-power mutation,
  sounds, and mouse-release waits verified against the native trace.
- [x] `handle_item_screen_mouse_input` - `ItemScreenMouseInput.Handle`; cursor
  button/position refresh, empty-cube/menu-page transitions, option hit testing,
  volume/selection/wait/weapon-adjust ordering verified against native traces.
- [x] `handle_item_screen_input` - `ItemScreenInputHandlers.DispatchInput`;
  positive mouse buttons take precedence over keyboard input, with no action
  for absent input, verified against native traces.
- [x] `JE_itemScreen` - `ItemScreenLoop.Run`; initialization, preparation and
  drawing, constant-play versus animated input loops, event clearing, keyboard
  state reset, game/jump exit conditions, network wait/synchronization packets,
  and loaded-game fade ordering verified with deterministic host traces.
- [x] `draw_ship_illustration`
- [x] `load_cubes`
- [x] `load_cube`
- [x] `JE_drawItem`
- [x] `JE_drawMenuHeader`
- [x] `get_menu_choice_y` - `MainMenuChoiceLayout.GetChoiceY`; full byte-value
  tests for choices, menu special cases, and the live next-level selection
  matched the extracted native function.
- [x] `draw_menu_choice` - `MainMenuChoiceLayout.DrawChoices`; selected
  tilde-prefixed labels, fixed X/small-font rendering, and row placement
  verified against the extracted native routine.
- [x] `JE_drawMenuChoices` - `MainMenuChoiceLayout.DrawChoices`; choice
  iteration/order and presentation callbacks match the native C oracle.
- [x] `JE_drawMenuHeader` - `MainMenuChoiceLayout.DrawHeader`; data-cube,
  save/load, and default header lookup, centering, font, and draw calls match
  native branches across every menu ID and representative selections.
- [x] `JE_updateNavScreen` - `NavigationMapScreen.Update`; grid/dot/planet
  ordering, sprite interface, selection-centered scroll target, smooth
  viewport convergence and animation/dot timers match native frame traces.
- [x] `JE_drawLines` - `NavigationMapGrid.DrawBackground`; crosshatch,
  strict boundaries, and dark/light color/offset callbacks match native.
- [x] `JE_drawNavLines` - `NavigationMapGrid.DrawViewport`; signed viewport
  half-offsets and all dark/light rectangle and crosshatch callbacks match
  native at signed-coordinate boundaries.
- [x] `JE_drawDots` - `NavigationMapDots.Draw`; viewport translation,
  strict clipping edges, selected-path progression highlighting and sprite
  call order match the native trace.
- [x] `JE_drawPlanet` - `NavigationMapPlanets.Draw`; base-frame centering,
  animated frame selection, signed viewport offsets and strict clipping match
  the native sprite-call trace.
- [x] `JE_drawMainMenuHelpText` - `MainMenuHelpLayout.Draw`; joystick,
  keyboard, power, completion/cube, and indexed-help branches match native
  text selection and draw coordinates.
- [x] `JE_scaleBitmap` - `SelectionBitmapScaler.Scale`; destination pitch,
  source-pitch horizontal sampling, native row-stride behavior and float
  accumulation match native hashes across padded and tightly packed surfaces.
- [x] `JE_initWeaponView` - `WeaponViewInitializer.Initialize`; screen clear,
  player/sidekick preview coordinates, projectile arrays, power-bar state and
  starfield initialization match the native setup trace.
- [x] `JE_scaleInPicture` - `ShipSpecificationsPresentation.ScaleInPicture`;
  expanding rectangle bounds, integer-rounded height, per-frame scaling,
  button interruption, presentation and delay order match the native trace.
- [x] `JE_doShipSpecs` - `ShipSpecificationsPresentation.Run`; input gating,
  ship-spec rendering, temporary picture reload, spring sound, zoom animation
  and final input wait match the native call/frame trace.
- [x] `JE_drawScore` - `MainMenuScore.Draw`; upgrade-submenu-only display,
  signed 32-bit cash formatting, text coordinates and shade parameters match
  native for negative and boundary values.
- [x] `setup_next_level_menu` - `NextLevelMenuSetup.Initialize`; route dots,
  palette, origin viewport, choice bounds, selected choice, planet labels and
  completion label match one- and five-destination native traces.
- [x] `start_current_level` - `NextLevelMenuState.StartCurrentLevel`;
  section selection uses the final route destination and enables section jump,
  matching the native trace.
- [x] `return_to_options_menu` - `NextLevelMenuState.ReturnToOptions`;
  normal and network mode choose their respective options menus, matching the
  native trace.
- [x] `JE_computeDots` - `NavigationMapDots.Compute`; all origins and
  deterministic five-destination routes matched the native fourth-root
  distance, dot cap, and fixed-width interpolated coordinates.
- [x] `JE_partWay` - `MainMenuChoiceLayout.PartWay`; 8,192 deterministic
  signed-coordinate and byte-parameter samples match native division and
  16-bit result narrowing.
- [x] `JE_quitRequest` - `QuitConfirmationScreen.Run`; yes/no keyboard,
  escape, irrelevant-key retry, mouse hit-box boundaries, highlight sounds,
  frame brightness/reset, cursor presentation and network quit packet/halt
  traces match native.
- [x] `JE_genItemMenu` - `UpgradeSubmenuBuilder.Build`; all seven shop
  categories match native labels, choice counts, duplicate/current item
  selection, byte-name encoding and untouched menu-buffer entries.
- [x] `request_menu_quit` - `MenuQuitRequest.Run`; declined requests preserve
  state, confirmed requests optionally halt network play before setting the
  loaded flag and clearing the main level.
- [x] `rebind_keyboard_key` - `KeyboardConfigurationSelection.Rebind`;
  prompt placement, pulsing selection bounds, input capture, duplicate-binding
  swaps, reserved Escape/F11/P handling, click feedback and mouse cancellation.
- [x] `handle_keyboard_config_selection` -
  `KeyboardConfigurationSelection.Handle`; default bindings, local/network
  options-menu destination and control-row rebinding dispatch.
- [x] `assign_joystick_control` - `JoystickConfigurationSelection.Assign`;
  detected duplicate removal, first free-slot assignment, oldest-assignment
  rollover, native row highlight and post-change joystick polling.
- [x] `adjust_joystick_config_setting` -
  `JoystickConfigurationSelection.AdjustSetting`; joystick index wrapping,
  analog toggle and analog-only sensitivity/threshold cycling.
- [x] `handle_joystick_config_selection` - `JoystickConfigurationSelection.Handle`;
  no-device gating, reset, local/network options return and assignment dispatch.
- [x] `handle_full_game_selection` - `MainMenuActionHandlers.SelectFullGame`;
  data-cube, ship-specification, upgrades, options, next-level and quit routes.
- [x] `handle_two_player_menu_selection` - `MainMenuActionHandlers.SelectTwoPlayer`;
  start/options/quit routes and distinct input-device cycling with no-device
  peer adjustment.
- [x] `change_two_player_input_device` -
  `MainMenuActionHandlers.ChangeTwoPlayerInputDevice`; forward/backward device
  wrap, zero-joystick peer adjustment, row validation and cursor-sound ordering,
  matched in twelve native traces.
- [x] `change_upgrade_weapon_power` -
  `MainMenuActionHandlers.ChangeUpgradeWeaponPower`; affordability feedback,
  selected temporary power adjustment and native byte narrowing match 24
  unchanged-C traces.
- [x] `handle_one_player_menu_selection` - `MainMenuActionHandlers.SelectOnePlayer`;
  start, local/network options return and quit routes.
- [x] `handle_limited_options_selection` - `MainMenuActionHandlers.SelectLimitedOptions`;
  joystick, keyboard and one-player destinations.
- [x] `handle_super_tyrian_menu_selection` - `MainMenuActionHandlers.SelectSuperTyrian`;
  start, ship specifications, options and network-aware quit callback routes.
- [x] `handle_upgrade_menu_selection` - `MainMenuActionHandlers.SelectUpgrade`;
  Done return and upgrade snapshot/submenu/weapon-view/cash transition order.
- [x] `handle_options_menu_selection` - `MainMenuActionHandlers.SelectOptions`;
  save/load flags/page reset and game/keyboard/joystick destinations.
- [x] `handle_next_level_menu_selection` - `MainMenuActionHandlers.SelectNextLevel`;
  map-section jump or Done return/palette transition.
- [x] `handle_upgrade_submenu_selection` -
  `MainMenuActionHandlers.SelectUpgradeSubmenu`; unaffordable-selection Done
  correction or item-sound/cash commit/menu return.
- [x] `handle_data_cube_menu_selection` - `MainMenuActionHandlers.SelectDataCube`;
  Done/empty return and selected cube subpage/scroll initialization.
- [x] `handle_load_save_menu_selection` - `MainMenuActionHandlers.SelectLoadSave`;
  Done and quick-save restoration, save-name entry, player-aware save-list
  indexing, save/load dispatch and deterministic list disposal.
- [x] `JE_menuFunction` - `MainMenuFunction.Dispatch`; central click/color
  setup, menu-specific argument routing, data-cube submenu transition, and
  unconditional post-dispatch item snapshot match all native menu cases.
- [x] `JE_drawShipSpecs` - `ShipSpecificationsDrawing.Draw`; matching screen
  initialization, border/line order, ship-name/help/footer text layout, fallback
  ship selection, sprite placement, and four-neighbor low-nibble greenification
  verified with a synthetic catalog and compressed sprite fixture.
- [x] `JE_weaponSimUpdate` - `WeaponSimulationDrawing.Update`; frame handoff,
  affordability/disabled controls, per-level power-bar pixels, equipped weapon
  shot label, fallback hidden controls and final ship draw coordinates verified
  with synthetic item catalogs and ordered presentation traces.
- [x] `JE_weaponViewFrame` - `WeaponViewFrame.Update`; weapon and sidekick firing,
  repeat timing, sidekick positioning/animation/charge, shot simulation,
  starfield/interface order, and power-bar pixels verified with parsed item fixtures,
  a controlled player-shot effects host, and a screen/HUD state fixture.

### `src/helptext.c`

- [x] `decrypt_string` - `EncryptedText.Decrypt`; reverse XOR fixtures.
- [x] `read_encrypted_pascal_string` - `EncryptedText.ReadPascalString`.
- [x] `skip_pascal_string` - `EncryptedText.SkipPascalString`.
- [x] `JE_helpBox` - `HelpTextRenderer.Layout`/`DrawBox`; native wrapping.
- [x] `JE_HBox` - `HelpTextRenderer.DrawHelp`; one-based archive lookup.
- [x] `JE_loadHelpText` - `HelpTextData.Load`.

### `src/joystick.c`

- [x] `joystick_axis_threshold` - `JoystickState.AxisThreshold`; signed/dead-zone/native-defined arithmetic.
- [x] `joystick_axis_reduce` - `JoystickState.AxisReduce`; native sensitivity division.
- [x] `joystick_analog_angle` - `JoystickState.AnalogAngle`; native quadrant/angle accumulation.
- [x] `check_assigned` - `JoystickState.CheckAssigned`; axis/button/hat two-bind maximum.
- [x] `poll_joystick` - `JoystickInput.Poll`; exact native directions/actions/repeat flags.
- [x] `poll_joysticks` - `JoystickInput.PollAll`/`SdlJoysticks.PollJoysticks`; aggregate down state and device updates.
- [x] `push_key` - `JoystickInput.PushKey`; ordered key-down/up to actual SDL queue.
- [x] `push_joysticks_as_keyboard` - `JoystickInput.PushAsKeyboard`; native confirm/cancel/cardinal order.
- [x] `init_joysticks` - `SdlJoysticks` constructor; real SDL enumeration/open/config/defaults and ignored gate.
- [x] `deinit_joysticks` - `SdlJoysticks.Dispose`/`Release`; config persistence, real close/subsystem cleanup.
- [x] `reset_joystick_assignments` - `JoystickState.ResetAssignments`; capability-dependent native defaults.
- [x] `load_joystick_assignments` - `JoystickState.LoadAssignments`; exact native section/two-bind/default semantics.
- [x] `save_joystick_assignments` - `JoystickState.SaveAssignments`; exact native serialized assignment/config output.
- [x] `joystick_assignments_to_string` - `JoystickAssignment.Describe`; native comma-separated nonempty codes.
- [x] `code_to_assignment` - `JoystickAssignment.Parse`; native whitespace/case/partial scan semantics.
- [x] `assignment_to_code` - `JoystickAssignment.ToCode`; native six-character truncation and signs.
- [x] `detect_joystick_assignment` - `JoystickInput.DetectAssignment`; native threshold/release/override/cancel behavior and input/display/timer flow.
- [x] `joystick_assignment_cmp` - `JoystickAssignment.Matches`; native relevant-field equality.

### `src/jukebox.c`

- [x] `jukebox` - `Jukebox.Run`; complete native controls/fade/action traces, original titles, actual managed audio/presentation and SDL flows.

### `src/keyboard.c`

- [x] `flush_events_buffer` - `InputState.FlushEvents`; native queue-only consumption.
- [x] `wait_input` - `InputState.WaitInput`; native delay/joystick keyboard/network ordering.
- [x] `wait_noinput` - `InputState.WaitNoInput`; native release/poll/network ordering.
- [x] `init_keyboard` - `InputState.Initialize`; native flags/cursor/system-scale services.
- [x] `mouseSetRelative` - `InputState.SetRelative`; native requested/focused mode and accumulator reset.
- [x] `JE_mousePosition` - `InputState.MousePosition`; native service, ushort wrapping and last-button gate.
- [x] `mouseGetRelativePosition` - `InputState.RelativePosition`; native accumulation/scaled-zero rounding/reset.
- [x] `service_SDL_events` - `InputState.ServiceEvents`; complete native event state and ordered host callbacks.
- [x] `JE_clearKeyboard` - `InputState.ClearKeyboard`; native intentionally empty hook preserved and tested.

### `src/lds_play.c`

- [x] `lds_load` - `LdsPlayer.Load`; bounded native binary/instrument/order/pattern behavior.
- [x] `lds_free` - `LdsPlayer.Free`; managed ownership release also stops invalid post-free playback.
- [x] `lds_rewind` - `LdsPlayer.Rewind`; exact native reset/register initialization.
- [x] `lds_fade` - `LdsPlayer.Fade`; native fade state and register traces.
- [x] `lds_setregs` - `LdsPlayer.SetRegister`; native write suppression/cache.
- [x] `lds_setregs_adv` - `LdsPlayer.SetRegisterMasked`; native masking.
- [x] `lds_update` - `LdsPlayer.Advance`/`Update`; complete native command/effect/register parity.
- [x] `lds_playsound` - `LdsPlayer.PlaySound`; 1,536 native instrument triggers and integrated sequencing.

### `src/loudness.c`

- [x] `init_audio` - `SdlAudioDevice.TryInitializeOpl`; native disabled-audio
  short circuit, SDL init/open failure policy, negotiated sample-rate OPL2
  initialization, LDS player/mixer creation, and actual dummy SDL callback
  playback are verified. The platform supplies rhythm-noise randomness.
- [x] `audioCallback` - `AudioMixer.Mix`; exact native PCM/channel/fractional update parity, required OPL service.
- [x] `deinit_audio` - `SdlGameShutdownHost.DeinitializeAudio` disposes the SDL
  audio device, clears sample channels and frees the owned `LdsPlayer`; dummy
  SDL shutdown verifies subsystem release and music-state cleanup, including
  cleanup when a later configuration save fails.
- [x] `load_music` - `MusicArchive.Load`; native archive bytes/offset parity.
- [x] `load_song` - `MusicArchive.Song`/`MusicController.PlaySong`; required LDS load boundary.
- [x] `play_song` - `MusicController.PlaySong`; native selection/resume/disabled gates.
- [x] `restart_song` - `MusicController.Restart`; native rewind and stopped state.
- [x] `stop_song` - `MusicController.Stop`; synchronized native stop/disabled behavior.
- [x] `fade_song` - `MusicController.Fade`; native speed-one fade command.
- [x] `set_volume` - `AudioMixer.SetVolume`; native byte volumes and disabled gate.
- [x] `multiSamplePlay` - `AudioMixer.Play`; native replacement, channel/volume/gating.

### `src/lvllib.c`

- [x] `JE_analyzeLevel` - `LevelArchive.Analyze`.

### `src/lvlmast.c`

- No function definitions; capacities and shape-file table are represented
  by `LevelMaster` and verified by `LevelMasterTests`.

### `src/mainint.c`

- [x] `JE_drawTextWindow` - `GameTextWindow.Show`; optional previous-text erasure, reset to 100 ticks, tiny-font hue/brightness and actual option-shape/font presentation.
- [x] `JE_outCharGlow` - `CharacterGlow.Draw`/`CharacterGlowHost`; character map/width/16-bit positions, brightness wave, shadow, skip/escape/warning/network/timing order in 1,024 native scenarios; real glyph/input/PIT/warning pixels verified. Oversized native buffers fail explicitly.
- [x] `JE_drawPortConfigButtons` - `PlayerWeapons.DrawModeButtons`; single-player sprite locations/lit/unlit frames and two-player suppression verified against unchanged native draw calls.
- [x] `JE_helpSystem` - `HelpSystem.Run`/`HelpSystemHost`; 304 native complete menu scripts including all topic starts, keyboard wrap and 160 exact row/click/hover/mouse-over-key edges; music/background/fades/cursor and actual SDL title/help/title return verified.
- [x] `saveGameNamePopup` - `SavedGameNamePopup.Run`; mouse/key/text input,
  filtering, uppercase and 28-character limit, backspace/accept/cancel, trailing
  space trimming, and save/load dispatch verified with scripted host tests.
- [x] `helpSystemPage` - `HelpSystem.ShowPages`/`DrawPage`; 144 native page scripts cover all eight layouts, per-topic/global footers, page boundary/menu return, left/right cursor and key/mouse precedence, fade/text-style restore; padded managed drawing, SDL full eight-page navigation and cancellation verified.
- [x] `weapon_upgrade_cost` - `PlayerProgression.WeaponUpgradeCost`; triangular costs, power 0..11 and platform-width signed long overflow verified with 72 native cases.
- [x] `JE_getCost` - `PlayerProgression.GetCost`; native item-type tables, special ship >90 cost, player-one upgrade ownership and ushort cost narrowing.
- [x] `saved_game_count` - nonnegative `IReadOnlyList<SavedGameMetadata>.Count`; signed negative and empty native counts verified against empty managed lists.
- [x] `draw_load_screen` - `LoadScreen.Draw`/`LoadScreenHost`; 960 native draw states, 21-byte formatted-label truncation, 11 saves plus exit per page, exact selected hues/brightness/arrows/help/fades; concrete padded surface rendering verified.
- [x] `wait_for_load_screen_input` - `LoadScreen.WaitInput`; native event clear/cursor/presentation/joystick/16 ms polling and unsigned-old-coordinate movement behavior verified over eight waits, actual SDL input and cancellation.
- [x] `read_load_screen_input` - `LoadScreen.ReadInput`; 8,192 native inputs and every row edge, sparse-page navigation, hover/click-coordinate distinctions, visible paging bounds and simultaneous mouse/key precedence verified.
- [x] `handle_load_screen_action` - `LoadScreen.HandleAction`; 7,680 native states cover left/right/action/done precedence, invalid-row sample, exit/load and perform-save clearing; actual catalog/versioned save/episode loading verified.
- [x] `JE_loadScreen` - `LoadScreen.Run`; 160 native full loops, exact return/fade and existing game-loaded state behavior, managed list ownership, concrete SDL title -> paged-load -> persisted-game state integration verified.
- [x] `JE_totalScore` - `PlayerProgression.TotalScore`; actual seven item values, collecting player cash, global weapon-power ownership and unsigned native-width total wrapping.
- [x] `JE_getValue` - `PlayerProgression.GetValue`; full cumulative upgrade value and two-player front/rear ownership, exact signed 32-bit result.
- [x] `JE_nextEpisode` - `EpisodeTransition.Run`; real availability/wrap and archive initialization, repeated-game flags, high-score/credits gates, RNG-based SuperCarrot/BananaBlast state, item snapshots and full title/song/input/fade ordering verified in 1,024 native scenarios. Credits/high-score implementations are available through its explicit callbacks; playable-host wiring remains pending.
- [x] `JE_initPlayerData` - `PlayerProgression.Initialize`; known item IDs, primary last-item snapshot before power resets, copied secondary items/rear/sidekick defaults, mode flags, armor/life ownership, level name and exact RNG.
- [x] `JE_sortHighScores` - `LegacySaveArchive.SortHighScores`; six three-slot groups, score/name/difficulty swaps only, all other save fields retained.
- [x] `JE_highScoreScreen` - `HighScoreScreen.Run`/`HighScoreScreenHost`; 256 native scripts verify all three episode pages, signed scores, byte difficulty clamping, exact mouse bounds/precedence and keyboard wrap/exit keys. Concrete saved surfaces/fonts/arrows/help/palettes/cursor services and full actual SDL2 input/video loop verified; final title menu caller remains pending.
- [x] `JE_gammaCorrect_func` - `PaletteSystem.GammaChannel`; single-precision multiply, half-away-from-zero rounding and 255 cap.
- [x] `JE_gammaCorrect` - `PaletteSystem.CorrectGamma`; all 256 RGB colors and all byte gamma settings compared to unchanged native output.
- [x] `JE_gammaCheck` - `GameGamma.Check`; F11 consumption, NewKey reset, modulo-four gamma, base-picture palette copy/correction and complete palette publication.
- [x] `JE_doInGameSetup` - `GameSetupSession.Run` and `GameSetupWaiting.Draw`; 64 native local/network/request/quit/prior-end-level states, exact waiting pixels, cancellation/error cleanup and actual two-peer SDL2_net resume/quit synchronization. Primary screen is passed explicitly instead of global surface-pointer switching; final playable level host remains pending.
- [x] `draw_ingame_setup_menu` - `InGameSetup.Draw`; exact saved-surface restart/restore, labels, disabled-volume gauges, detail/speed text and selected help in 2,048 native traces; concrete font/gauge/shade pixels verified.
- [x] `wait_for_ingame_setup_input` - `InGameSetup.Wait`; cursor/presentation, delay16, joystick/event/keepalive ordering and native ushort old-coordinate comparison including negative positions.
- [x] `read_ingame_setup_mouse` - `InGameSetup.ReadMouse`; selection/hover, exact half-open name/value/row bounds, click coordinates and slider 0..255 endpoints.
- [x] `read_ingame_setup_keyboard` - `InGameSetup.ReadKeyboard`; mouse precedence/right close, wrap navigation, activate/decrease/increase/escape and W processor6 behavior.
- [x] `adjust_ingame_setup_setting` - `InGameSetup.Adjust`; volume saturation/sound ordering and wrapped detail/speed selection; actual runtime/config/PIT integration.
- [x] `apply_ingame_setup_action` - `InGameSetup.Apply`; slider publication, activation precedence, music/effects toggles with native sample ordering, return/quit/close and constant/network halt flags.
- [x] `JE_inGameSetup` - `InGameSetup.Run`; 16 complete native loops verify cursor load and draw/wait/read/apply composition, return/quit/mute/W/detail/speed behavior; concrete managed surface/input/audio/config/timer adapters integrated.
- [x] `JE_inGameHelp` - `GameHelp.Run`/`GameHelpHost`; complete single/two-player layouts, help-box states, centered continue text and ordered event/mouse/joystick/network wait in 32 native cases; actual help/font/shape/input integration verified. Explicit primary-surface ownership replaces transient global screen switching.
- [x] `JE_highScoreCheck` - `HighScores.Run`; 512 native scenarios verify score qualification, highest-cash-first ordering, signed32 score narrowing, sorting/shift/cancel semantics, name editing/filtering/cap/trailing spaces, cursor blink and full result glow/fades/input/music flow. Concrete managed rendering/input/palette/cursor/timer services verified; playable-host wiring remains pending.
- [x] `adjust_difficulty` - `PlayerProgression.AdjustDifficulty`; exact single/two-player scores, float multipliers, roundf conversion, all threshold tiers and nondecreasing difficulty in 147,456 native cases. Invalid initial difficulty and native out-of-range float-to-ulong conversions throw explicitly.
- [x] `load_next_demo` - `DemoSession.LoadNext`; exact original/compact header fields, numeric item-ID lookup, episode initialization, demo number wrapping/missing-next fallback, optional wait bytes, stream positions and missing-file playback/main-level state compared with unchanged native loading.
- [x] `replay_demo_keys` - `PlayerInput.ReplayDemo`; 8,192 native replay frames plus 8,192 real collector/playback frames compare keys, wait countdown, zero-wait loops, movement/buttons, partial/EOF state, stream positions and level-end ordering. Fragmented managed stream reads preserve native full-read semantics; player two remains unchanged.
- [x] `JE_SFCodes` - `PlayerWeapons.RecognizeSpecial`, concrete linking integration; 42,432 native command-state traces across all 26 combos, normal/SuperTyrian maps, both players, release/diagonal/repeat/cancellation behavior.
- [x] `JE_sort` - `LegacySaveArchive.SortHighScoreGroup`; native ordered pair comparisons, descending signed scores, exact tie behavior and score/name/difficulty-only swaps.
- [x] `JE_playCredits` - `Credits.Run`; 24,483 frames match native scrolling, text/shadow layout, picture fades/wraps, seeded ship motion/banking, audio transitions, held final frame, input/network ordering and cleanup. Concrete managed sprite/font/palette/file/input/timer routing also verified; top-level game wiring remains pending.
- [x] `JE_endLevelAni` - `LevelResults.Run`; all unlock/snapshot/difficulty/alive/timer/score/editor/cube/skip/wait/audio/fade behavior in 512 native cases plus actual glow/input/palette/timing integration. Checksum-overlap unlocks use transient scratch; invalid native-buffer indices fail explicitly.
- [x] `JE_drawCube` - `GameHud.DrawCube`/`GameHudPresentation.Cube`; exact ordered dark shadows and signed brightness hue conversion with real sprite adapter pixels.
- [x] `JE_handleChat` - `InGameChat.Handle`; preserves the original explicit
  no-op stub, invoked and verified with the level-network compatibility tests.
- [x] `JE_inGameDisplays` - `GameHud.Draw`; cash/special/lives/name/bombs/cheat ordering, all 256 life values including unsigned underflow and signed display, local/network name width and mode gates verified in 4,096 native traces and actual sprite/font adapter tests.
- [x] `JE_mainKeyboardInput` - `GameKeyboard.Run`; 4,096 native mode/chord cases verify ordered custom-ship selection/armor preservation, help/NortShip requests, cheat/skip/debug/pause/menu behavior, smoothing/filter/speed toggles, key consumption and RNG/music. Actual gamma, ship/combat HUD, help/text, network request state and PIT settings are integrated; enclosing playable level loop remains pending.
- [x] `JE_pauseGame` - `GamePause.Run`/`GamePausePresentation`; twelve native control/presentation/input/timing/volume traces, actual SDL font/input and real two-peer SDL2_net pause/resume; explicit failures and cancellation restore audio/mouse.
- [x] `update_player_death` - `PlayerCombat.UpdateDeath`; 8,192 exact native player/level/lives/HUD/effect/sound/RNG traces; managed life references and primary HUD ownership replace pointer/global screen side effects.
- [x] `collect_player_movement_input` - `PlayerInput.Collect`; 16,384 native input/analog/digital/mouse/keyboard/demo-record/network-owner traces with exact narrowed movement, float bits, request flags, recording bytes, service/poll ordering and full prepared packet bytes. Actual `ReplayDemo` is integrated through the replay service and checked against unchanged native collection; `DemoSession` supplies header/loading state and streams.
- [x] `update_player_linking` - `PlayerMovement.UpdateLinking`; exact native link/unlink/angle/float-bit traces with callback and concrete verified special-command recognition overloads.
- [x] `draw_player_level_end` - `PlayerMovement.DrawLevelEnd`; exact native death/end/warp and all three ship/trail layouts.
- [x] `update_player_movement` - `PlayerMovement.UpdateMovement`; native friction/clamp/history/linked turret/marker/rear projectile state verified with real item/projectile code.
- [x] `change_player_weapon_mode` - `PlayerWeapons.ChangeMode`; native button/request/readiness latches, rear/special multiposition resets, normal mode wrap, all seven ID-resolved arcade special pairs and shared player-one special ownership verified in 4,096 native traces.
- [x] `fire_main_weapons` - `PlayerWeapons.FireMain`; all 11 power levels/two modes, single/two-player ports, linked suppression, Galaga and cooldowns in 4,096 exact native real-projectile traces.
- [x] `update_super_charge_weapon` - `PlayerWeapons.UpdateCharge`; native charge graphics/levels/waits/difficulty/HUD/firing and real projectiles in 4,096 exact native traces.
- [x] `fire_superbomb` - `PlayerWeapons.FireSuperbomb`; native owner/count/button/cooldown and literal port 16/shot 535 retained; actual bomb projectile output and shared state compared in native motion/weapon oracle.
- [x] `update_sidekick_positions` - `PlayerMovement.UpdateSidekickPositions`; all native styles, rotation bits, history/fixed/orbit/return/attachment motion, launch/link sounds and explosion calls.
- [x] `fire_sidekick_weapons` - `PlayerWeapons.FireSidekicks`; limited/unlimited ammo, refill, cooldowns, button ownership, charge reset, raw shot offsets and full gauge pixels verified against native traces.
- [x] `update_player_attacks` - `PlayerWeapons.UpdateAttacks`; native gate and delta/mode/special/main/charge/bomb/sidekick-position/fire ordering, actual shared gameplay state and HUD compared in 4,096 complete native attack traces.
- [x] `draw_player_sidekicks` - `PlayerWeapons.DrawSidekicks`; sprite size/offsets, animation enable/frame wrap, charge ticks including unsigned underflow, and single/two-player/end-level gates verified against unchanged native draw traces.
- [x] `draw_player_ship` - `PlayerMovement.DrawShip`; 2,048 native boundary/banking/shadow/invulnerability/DragonWing/NortSpark traces, real raw-index 671 projectiles, exact RNG state and actual compressed-sheet copy/blend/shadow pixels verified. Native supported-toolchain Y-before-X random argument evaluation is explicit.
- [x] `JE_playerMovement` - `PlayerFrame.Update`; 4,096 native enclosing-player scenarios compose verified death/respawn, actual managed input/replay, linking/commands, movement, ship/sidekick rendering and ordered attacks. Compares old positions, histories, lives/armor/shields, follow deltas, all projectiles, HUD/frame hashes and RNG; native delayed local/remote packet application/reset and shared primary special coordinates also match. Network packet availability and ownership errors are explicit.
- [x] `JE_mainGamePlayerFunctions` - `PlayerFrame.UpdatePlayers`; 4,096 native scenarios each run two consecutive complete single/paired updates, including Galaga respawn mode changes, command/request resets, shared button state, signed warp overflow, float parallax rounding, all three background positions/tile offsets and previous-map state. Full level/game loop remains pending.
- [x] `JE_getName` - `PlayerPickups.GetName`; local/opponent/fallback name precedence and NUL termination.
- [x] `collect_arcade_pickup` - `PlayerPickups.CollectArcade`; full purple-ball/Galaga, arcade tables, sidekick tiers, special/front/rear rewards, cash, item-ID mapping, text/options and cooldown behavior match native state traces.
- [x] `collect_armor_pickup` - `EnemySystem.CollectArmorPickup`; actual linked/unlinked player armor, division rounding, 28-point cap, enemy availability, armor HUD and sound compared in native collision scenarios. Invalid pickup values are explicit errors.
- [x] `collect_bonus_level_pickup` - `PlayerPickups.CollectBonusLevel`; active-bonus gate, song, next level, enemy availability and display timer.
- [x] `collect_score_item` - `PlayerPickups.CollectScore`; cubes, upgrades, shot 104 with native slot clearing, superbombs, Hotdog snapshot and signed/unsigned cash arithmetic, sound and actual explosions. Invalid native out-of-bounds slot clearing throws explicitly.
- [x] `destroy_linked_enemies` - `EnemySystem.DestroyOnCollision`; preserves collision-specific zero/255/exact/offset/group link rules, forced target availability, no kill rewards/events, and actual small/large/ground/repeating explosion state and sound channels.
- [x] `damage_enemy_on_collision` - `EnemySystem.DamageOnCollision`; capped player damage through actual combat/HUD, signed speed-times-armor impulses, armor-255 sentinel, enemy armor decrement/hit sound and collision-specific linked destruction compared across 8,192 native scenarios.
- [x] `JE_playerCollide` - `PlayerPickups.Collide`; first-100-enemy ordered iteration, exact proximity/availability/ground/invulnerability gates and arcade/armor/bonus/score/damage precedence with all helpers integrated and native traces compared.

### `src/menus.c`

- [x] `gameplaySelect` - `SelectionMenus.SelectGameplay`; native disabled network, mode flags, navigation/mouse/sound/fade flow.
- [x] `episodeSelect` - `SelectionMenus.SelectEpisode`; availability gates, episode initialization/initial state and keep-alive.
- [x] `difficultySelect` - `SelectionMenus.SelectDifficulty`; Shift-G/Shift-]/held or sequential LORD unlocks, noncontiguous levels and native input priority.

### `src/mouse.c`

- [x] `JE_drawShapeTypeOne` - `MouseCursor.CopyBlock(false)`; native bounded/scanline-crossing restoration.
- [x] `JE_grabShapeTypeOne` - `MouseCursor.Capture`/`CopyBlock(true)`; native 24x28 capture.
- [x] `JE_mouseStart` - `MouseCursor.Start`; native event/button/hotspot/clip behavior.
- [x] `JE_mouseStartFilter` - `MouseCursor.StartFiltered`; native no-poll/filter behavior.
- [x] `JE_mouseReplace` - `MouseCursor.Replace`; native mouse-gated pixel restoration.

### `src/mtrand.c`

- [x] `mt_srand` — `MersenneTwister.Seed`; regression tests verify sequence
  compatibility and truncation to the low 32 bits.
- [x] `mt_rand` — `MersenneTwister.NextUInt32`; regression tests verify
  default, published, reseeded, and wraparound vectors.
- [x] `mt_rand_1` — `MersenneTwister.NextFloatInclusive`; regression tests
  verify the inclusive range and native float bit patterns.
- [x] `mt_rand_lt1` — `MersenneTwister.NextFloatExclusive`; regression tests
  verify native float bit patterns, including native rounding semantics.

### `src/musmast.c`

- No function definitions; title metadata/constants map to `MusicMaster`.
  Mutable selection/fade fields remain split across episode and level state
  until complete game-host ownership wiring is implemented.

### `src/sndmast.c`

- No function definitions; sound-title, voice-selection, and sound-ID tables
  are represented by `SoundMaster` and `SoundId`.

### `src/network.c`

- [x] `packet_copy` - `NetworkPacket.Copy`; independent owned payload and preserved channel.
- [x] `packets_shift_up` - `NetworkSession.ShiftUp`; native queue order with managed ownership.
- [x] `packets_shift_down` - `NetworkSession.ShiftDown`; native state history and tail disposal.
- [x] `network_prepare` - `NetworkSession.Prepare`; exact big-endian type/sequence.
- [x] `network_send_no_ack` - `NetworkSession.SendUnacknowledged`; exact native bytes; I/O errors throw.
- [x] `network_send` - `NetworkSession.Send`; pending copies, queue bounds, sequence/tick ordering and native traces.
- [x] `network_acknowledge` - `NetworkSession.Acknowledge`; exact native acknowledgment bytes/order.
- [x] `network_is_alive` - `NetworkSession.IsAlive`; native dual tick/time-out predicate.
- [x] `network_check` - `NetworkSession.Check`; native receipt/ack/queue/retry/state/XOR/resend/keep-alive; quit/timeout unwind explicitly.
- [x] `network_update` - `NetworkSession.Update`; native control shift/sync behavior.
- [x] `network_is_sync` - `NetworkSession.IsSynchronized`; preserves native promoted subtraction even across sequence wrap.
- [x] `network_state_prepare` - `NetworkSession.PrepareState`; exact 28-byte state/clear and overwrite warning.
- [x] `network_state_send` - `NetworkSession.SendState`; native state/parity payload, spacing and history.
- [x] `network_state_update` - `NetworkSession.UpdateState`; native delayed state/XOR recovery/resend and ticks; cancellable waits.
- [x] `network_state_is_reset` - `NetworkSession.StateIsReset`; native initial delayed-frame predicate.
- [x] `network_state_reset` - `NetworkSession.ResetState`; native state/parity/history and ticks reset.
- [x] `network_connect` - `SdlNetTransport.Bind`/`NetworkSession.Connect`; complete native negotiation/duplicate/error/input traces and real two-peer connection.
- [x] `network_tyrian_halt` - `NetworkSession.Shutdown`/`NetworkShutdownPresentation`; exact native error normalization, picture/text/fade/input/sync/timeout/release/exit-code order; actual SDL frame/font/palette/keyboard and transport release; cancellation/rendering failures release ownership without claiming normal game halt.
- [x] `network_init` - `NetworkSession`/`SdlNetTransport` constructors; delay validation, actual SDL2_net socket/packet initialization; exceptions and deterministic failure cleanup replace native negative result codes/leaks.
- [x] `JE_clearSpecialRequests` - `NetworkRequests.ClearSpecialRequests`; all 32 native local-flag combinations reset, peer-menu flag retained.

### `src/nortsong.c`

- [x] `setDelay` - `DelayTimer.SetDelay`.
- [x] `setDelay2` - `DelayTimer.SetDelay2`.
- [x] `getDelayTicks` - `DelayTimer.GetDelayTicks`.
- [x] `getDelayTicks2` - `DelayTimer.GetDelayTicks2`.
- [x] `wait_delay` - `DelayTimer.Wait`.
- [x] `service_wait_delay` - `DelayTimer.ServiceWait`.
- [x] `wait_delayorinput` - `DelayTimer.WaitOrInput`.
- [x] `loadSndFile` - `SoundBank.Load`; native bank/tail/size parity at explicit conversion boundary; SDL adapter pending.
- [x] `JE_playSampleNum` - `SoundBank.PlaySample`; native one-based routing/channel/volume.
- [x] `setDelaySpeed` - `DelayTimer.SetSpeed`.
- [x] `JE_changeVolume` - `DelayTimer.ChangeVolume`.

### `src/nortvars.c`

- [x] `JE_anyButton` - `InputState.AnyButton`; joystick poll before cleared event service and native held-mouse/new-key/joystick result.
- [x] `JE_dBar3` - `PlayerCombat.DrawBar`; native HUD pixels.
- [x] `JE_barDrawShadow` - `IndexedSurface.ShadowedGauge`; exact native bevel/shadow/partial division, wrapping and clipped warnings.
- [x] `JE_wipeKey` - shared `InputState.ClearKeyboard`; intentionally retains the native no-op and input flags.

### `src/opentyr.c`

- [x] `str_pop_int` - `TextTokenReader.TryReadInt32`; repeated native whitespace/sign/prefix/no-number/NUL/platform-long saturation/int narrowing traces, integrated into typed item-array loading and existing native item database parity.
- [x] `str_pop_str` - `TextTokenReader.TryReadToken`; native space/tab-only delimiters, NUL and byte-capacity/error/remainder traces; typed item-reference loading reuses the reader and UTF-8 references obey native byte rather than character limits.
- [x] `getShopContentsPickerItemsCount` - `SetupMenu.PickerCount`; three native entries verified in 16 catalogs.
- [x] `getShopContentsPickerItem` - `SetupMenu.PickerItem`; Mission/Mixed/Random and invalid-setting fallback verified.
- [x] `getDisplayPickerItemsCount` - `SetupMenu.PickerCount`/`SetupMenuHost.DisplayCount`; window plus SDL display enumeration, native catalogs and real SDL fullscreen/window switching verified.
- [x] `getDisplayPickerItem` - `SetupMenu.PickerItem`; window and one-based Display labels verified.
- [x] `getScalerPickerItemsCount` - `SetupMenu.PickerCount`; all nine existing scaler definitions verified.
- [x] `getScalerPickerItem` - `SetupMenu.PickerItem`; exact native scaler names/order and real SDL scaler selection verified.
- [x] `getScalingModePickerItemsCount` - `SetupMenu.PickerCount`; all four original modes verified.
- [x] `getScalingModePickerItem` - `SetupMenu.PickerItem`; exact native mode labels/order and SDL layout selection verified.
- [x] `draw_setup_menu` - `SetupMenu.Draw`/`SetupMenuHost`; 256 native states cover headers/values/descriptions/gauges/highlights/disabled rows/picker fit and borders; concrete SDL rendering verified.
- [x] `wait_for_setup_input` - `SetupMenu.WaitInput`; eight native waits cover key/mouse/movement/negative coordinates/fullscreen-change wakeup; actual SDL and cancellation verified.
- [x] `collect_setup_menu_input` - `SetupMenu.CollectMenuInput`; 8,192 native snapshots cover menu/value/volume bounds, separate hover/click coordinates and mouse/key precedence.
- [x] `collect_setup_picker_input` - `SetupMenu.CollectPickerInput`; 8,192 native snapshots retain the observable native click-Y/X-limit quirk, hover/keyboard/mouse precedence and picker bounds.
- [x] `apply_setup_picker_action` - `SetupMenu.ApplyPicker`; 1,024 native actions cover wrapping/hover/back/activation/display/scaler/mode/shop/save, failed scaler restore and explicit double-failure error; real SDL and persistence verified.
- [x] `apply_setup_menu_activation` - `SetupMenu.Activate`; 156 native activations cover parent navigation/picker starts/shop clamping/music/sample toggle ordering/restart/child calls; actual jukebox and SDL integration verified.
- [x] `apply_setup_menu_action` - `SetupMenu.ApplyMenu`; 2,816 native actions cover hover/wrap/back/activation/clamped direct/relative volumes, boundary audio ordering and exit fades.
- [x] `setupMenu` - `SetupMenu.Run`; 64 native complete loops and actual SDL title/settings/graphics/audio/jukebox/gameplay/configuration-reload/title-return path verified.
- [x] `main` - `GameStartup.Run`; startup/output/resource initialization,
  Christmas/audio/network gates, title/destruct/game loop and halt/error paths
  verified with deterministic host traces.

### `src/opl.c`

- [x] `operator_advance` - `OplOperatorTimingState.Advance`; fixed-point waveform/vibrato/generator advancement with unsigned wraparound and negative division tests.
- [x] `operator_advance_drums` - `OplOperatorTimingState.AdvanceDrums`; hihat/snare/cymbal phase selection and a single injected RNG-bit read are tested.
- [x] `operator_output` - `OplOperatorTimingState.Render`; native off-state behavior, unsigned modulated waveform indexing/masking, floating-point sample scaling and feedback output are tested.
- [x] `operator_off` - `OplEnvelopeState.AdvanceOff`; preserves the native empty handler, with a regression verifying that no envelope state changes.
- [x] `operator_sustain` - `OplEnvelopeState.AdvanceSustain`; consumes complete 16.16 samples, retains fractional generator position and uses the native pointer-width signed envelope counter.
- [x] `operator_release` - `OplEnvelopeState.AdvanceRelease`; release attenuation, native envelope-step masking, fractional generator position, silent cutoff and release-only off transition are covered.
- [x] `operator_decay` - `OplEnvelopeState.AdvanceDecay`; sustain threshold transitions with/without sustain hold, decay attenuation, native envelope-step masking and fractional generator position are covered.
- [x] `operator_attack` - `OplEnvelopeState.AdvanceAttack`; polynomial attack, envelope-step masks, cyclic step skipping, full-level clamp and decay transition are covered.
- [x] `change_attackrate` - `OplEnvelopeState.ConfigureAttack`; polynomial coefficients, envelope-step/skip masks, disabled rate and OPL2/OPL3 immediate attack thresholds are tested.
- [x] `change_decayrate` - `OplEnvelopeState.ConfigureDecayRate`; all 16 register rates exercise attenuation factors and native envelope-step masks.
- [x] `change_releaserate` - `OplEnvelopeState.ConfigureReleaseRate`; all 16 register rates exercise attenuation factors and native envelope-step masks.
- [x] `change_sustainlevel` - `OplEnvelopeState.ConfigureSustainLevel`; all 16 register levels and high-nibble extraction are tested.
- [x] `change_waveform` - `OplOperatorTimingState.ConfigureWaveform`/`OplWaveforms`; all eight native waveform start/mask selections and generated table samples are tested.
- [x] `change_keepsustain` - `OplEnvelopeState.ConfigureSustainKeep`; both direction changes between sustain modes and unrelated-mode preservation are tested.
- [x] `change_vibrato` - `OplOperatorTimingState.ConfigureVibrato`; vibrato/tremolo flag decoding is exhaustively tested for all 256 register bytes.
- [x] `change_feedback` - `OplOperatorTimingState.ConfigureFeedback`; feedback amount is exhaustively checked for all 256 channel register bytes.
- [x] `change_frequency` - `OplOperatorTimingState.ConfigureFrequency` and `OplFrequencyTables`; frequency/octave/note-select/KSR/key-level calculations and dependent envelope-rate recalculation are tested.
- [x] `enable_operator` - `OplOperatorTimingState.Enable`; waveform start, attack transition, and already-active handling are tested.
- [x] `disable_operator` - `OplOperatorTimingState.Disable`; shared activity bits and final active-to-release transition are tested.
- [x] `adlib_init` - `OplChipState.Initialize`; OPL2/OPL3 reset state, operator/envelope initialization, clock and modulation increments, vibrato/tremolo tables and buffers are regression-tested against the native initialization logic.
- [x] `adlib_write` - `OplChipState.WriteRegister`; control/status reset, operator parameter mapping, frequency and envelope updates, key transitions, percussion triggers, OPL2 waveform gating, OPL3 second-bank routing, four-operator channel pairing and panning are regression-tested.
- [x] `adlib_reg_read` - `OplPortState.Read`; OPL2 status mask and both
  register-detection port values, plus OPL3 status/read behavior.
- [x] `adlib_write_index` - `OplPortState.WriteIndex`; primary/secondary
  register bank selection with OPL3 mode and register-5 detection.
- [x] `clipit16` - `OplSample.ClipInt16`; signed-16 saturation boundaries and integer extremes.
- [x] `adlib_getsample` - `OplChipState.Render`; OPL2 sample rendering matches
  native PCM vectors for FM/additive output, tremolo, feedback, multiple
  channels, 512-sample block boundaries, and deterministic percussion. An
  independent native OPL3 build verifies every four-operator connection
  topology as stereo PCM for short and block-boundary renders.

### `src/palette.c`

- [x] `JE_loadPals` - `PaletteSystem.Load`.
- [x] `set_palette` - `PaletteSystem.SetPalette`; injected RGB format mapping.
- [x] `set_colors` - `PaletteSystem.SetSolid`.
- [x] `init_step_fade_palette` - `PaletteSystem.InitializeFade`.
- [x] `init_step_fade_solid` - `PaletteSystem.InitializeSolidFade`.
- [x] `step_fade_palette` - `PaletteSystem.StepFade`.
- [x] `fade_palette` - `PaletteSystem.FadePalette`; ordered platform callbacks.
- [x] `fade_solid` - `PaletteSystem.FadeSolid`.
- [x] `fade_black` - `PaletteSystem.FadeBlack`.
- [x] `fade_white` - `PaletteSystem.FadeWhite`.
- [x] `rgb_to_yuv` - `PaletteSystem.RgbToYuv`; signed-shift tests.

### `src/params.c`

- [x] `JE_paramCheck` - `GameOptions.Parse`; complete native option/help/error/legacy/state behavior, integrated game entrypoint.

### `src/pcxload.c`

- [x] `JE_loadPCX` - `PictureLoader.LoadPcx`; pitch and palette tests.

### `src/pcxmast.c`

- No function definitions; tracked by the file inventory.

### `src/picload.c`

- [x] `JE_loadPic` - `PictureLoader.LoadPicture`; archive and palette tests.

### `src/player.c`

- [x] `calc_purple_balls_needed` - `PlayerRules.CalculatePurpleBalls`.
- [x] `power_up_weapon` - `PlayerRules.PowerUpWeapon`.
- [x] `handle_got_purple_ball` - `PlayerRules.GotPurpleBall`.

### `src/shots.c`

- [x] `simulate_player_shots` - `PlayerShots.Simulate`; native preview traces.
- [x] `player_shot_set_direction` - `PlayerShots.SetDirection`; native directions.
- [x] `player_shot_move_and_draw` - `PlayerShots.MoveAndDraw`; native state/effects.
- [x] `player_shot_create` - `PlayerShots.Create`; native allocation/state traces.

### `src/sizebuf.c`

- [x] `SZ_Init` — `SizeBuffer` constructor; regression tests verify bounded
  initialization and operations.
- [x] `SZ_Error` — `SizeBuffer.Error`; regression tests verify sticky errors
  and recovery through seek.
- [x] `SZ_Memset` — `SizeBuffer.Fill`; regression tests verify byte truncation
  and writing.
- [x] `SZ_Memcpy2` — `SizeBuffer.CopyFrom`; regression tests verify cursor
  updates and independent source/destination failures.
- [x] `SZ_Seek` — `SizeBuffer.Seek`; regression tests cover begin/current/end
  offsets, invalid positions, and invalid origins.
- [x] `MSG_ReadByte` — `SizeBuffer.ReadByte`; regression tests verify values,
  cursor movement, and overrun behavior.
- [x] `MSG_ReadWord` — `SizeBuffer.ReadWord`; regression tests verify
  little-endian decoding and overrun behavior.

### `src/sndmast.c`

- No function definitions; tracked by the file inventory.

### `src/sprite.c`

- [x] `load_sprites_file` - `SpriteTable.LoadFile`.
- [x] `load_sprites` - `SpriteTable.Load`.
- [x] `free_sprites` - `SpriteTable.Clear`.
- [x] `blit_sprite` - `SpriteTable.Blit(Copy)`.
- [x] `blit_sprite_blend` - `SpriteTable.Blit(Blend)`.
- [x] `blit_sprite_hv_unsafe` - `SpriteTable.Blit(HueUnsafe)`.
- [x] `blit_sprite_hv` - `SpriteTable.Blit(Hue)`.
- [x] `blit_sprite_hv_blend` - `SpriteTable.Blit(HueBlend)`.
- [x] `blit_sprite_dark` - `SpriteTable.Blit(Black/Darken)`.
- [x] `JE_loadCompShapes` - `SpriteSheet.LoadFile`.
- [x] `JE_loadCompShapesB` - `SpriteSheet.Load`.
- [x] `free_sprite2s` - `SpriteSheet.Clear`.
- [x] `blit_sprite2` - `SpriteSheet.Blit(Copy)`.
- [x] `blit_sprite2_clip` - `SpriteSheet.Blit(Copy, clip: true)`.
- [x] `blit_sprite2_blend` - `SpriteSheet.Blit(Blend)`.
- [x] `blit_sprite2_darken` - `SpriteSheet.Blit(Darken)`.
- [x] `blit_sprite2_filter` - `SpriteSheet.Blit(Filter)`.
- [x] `blit_sprite2_filter_clip` - `SpriteSheet.Blit(Filter, clip: true)`.
- [x] `blit_sprite2x2` - `SpriteSheet.Blit2x2(Copy)`.
- [x] `blit_sprite2x2_clip` - `SpriteSheet.Blit2x2(Copy, clip: true)`.
- [x] `blit_sprite2x2_blend` - `SpriteSheet.Blit2x2(Blend)`.
- [x] `blit_sprite2x2_darken` - `SpriteSheet.Blit2x2(Darken)`.
- [x] `blit_sprite2x2_filter` - `SpriteSheet.Blit2x2(Filter)`.
- [x] `blit_sprite2x2_filter_clip` - `SpriteSheet.Blit2x2(Filter, clip: true)`.
- [x] `JE_loadMainShapeTables` - `MainShapeTables.Load`.
- [x] `free_main_shape_tables` - `MainShapeTables.Clear`.

### `src/starlib.c`

- [x] `JE_starlib_main` - `JukeboxStarfield.Update`/`HandleKey`; native star/pixel/state/RNG traces.
- [x] `JE_wackyCol` - unchanged native empty function intentionally omitted; its initialization call has no observable behavior.
- [x] `JE_starlib_init` - `JukeboxStarfield.Initialize`; native initialization/RNG/idempotence.
- [x] `JE_resetValues` - `JukeboxStarfield.Reset`; native phase/speed/color defaults.
- [x] `JE_changeSetup` - `JukeboxStarfield.ChangeSetup`; native random setup/change-time rules.
- [x] `JE_newStar` - `JukeboxStarfield.NewStar`/`AdvancePhase2`; all 15 pattern outputs, float bits and signed/word wrapping.

### `src/tyrian2.c`

- [x] `JE_starShowVGA` - `LevelPresentation.Show`; 2,048 native viewport pixel hashes/timing/shared flags including inversion, searchlight/player coordinates, native asymmetric spotlight stride and aliased buffers; actual SDL viewport presentation/skip, invalid dimensions and failed presentation verified.
- [x] `blit_enemy` - `EnemySystem.BlitEnemy`; native draw traces and compressed-sprite pixels.
- [x] `update_enemy_acceleration` - `EnemySystem.UpdateAcceleration`; native states/RNG.
- [x] `animate_and_draw_enemy` - `EnemySystem.AnimateAndDraw`; native animation/draw traces.
- [x] `update_enemy_oscillation` - `EnemySystem.Oscillate`; native signed-byte state traces.
- [x] `update_enemy_position` - `EnemySystem.UpdatePosition`; native movement/bounce/visibility.
- [x] `calculate_enemy_aim_velocity` - `EnemySystem.AimVelocity`; 1,000 native aim cases.
- [x] `aim_enemy_shot` - `EnemySystem.AimShot`; native target selection/velocity/RNG.
- [x] `create_enemy_shot` - `EnemySystem.CreateEnemyShot`; native shot slots/patterns/sounds.
- [x] `apply_enemy_weapon_special` - `EnemySystem.ApplyWeaponSpecial`; native force/smoke/filter cases.
- [x] `fire_enemy_shots` - `EnemySystem.FireShots`; native timing/difficulty/Galaga traces.
- [x] `process_enemy_attacks` - `EnemySystem.ProcessAttacks`; native ice/damage/onscreen states.
- [x] `launch_enemy_from_parent` - `EnemySystem.LaunchFromParent`; native child state/sounds/RNG.
- [x] `JE_drawEnemy` - `EnemySystem.DrawAndUpdate`; full native enemy update/draw traces.
- [x] `prepare_next_level` - `LevelPreparation.Prepare`; 49,152 unchanged-native
  traces verify mouse/keyboard/sprite cleanup, Galaga/two-player state,
  speed restoration, demo close/stop/wait behavior, difficulty reset,
  bonus/game-over routing, save slot, failed-save recovery and return value.
- [x] `draw_level_hud` - `LevelHud.Draw`; 4,096 unchanged-native state and
  ordered host-call traces cover end-level no-op, Galaga shields/rewards and
  mode normalization, shield regeneration countdowns, weapon-bar redraws,
  power arithmetic/display and frame-final map/enemy state.
- [x] `handle_level_input` - `LevelInput.Handle`/`LevelInputHost`; 32,768 native mode/game-over/demo/keyboard/skip/focus/modal/request traces; actual SDL game-over, demo interruption, help skip, pause/resume, setup/return and network request dispatch. Keyboard/player-input producers now write local pressed latches, not delayed network requests; existing native keyboard/player-input parity retained.
- [x] `draw_level_backgrounds` - `LevelBackgroundScene.Draw`; 4,096
  unchanged-native state and ordered host-call traces cover background
  priority/blending, delay/wrap arithmetic, starfield and filters, enemy
  strata/spawning, wild darkening, sound and screen/map state.
- [x] `update_level_explosions` - `SceneEffects.UpdateExplosions`; native state/draw traces.
- [x] `draw_armor_warning` - `ArmorWarning.Draw`/`ArmorWarningHost`; 16,384 native state/RNG/spawn/color/render traces, including byte wrap and network-owner/Galaga quirks; concrete real enemy pool/rescue/drop/fixed-width velocity/armor and padded font/rectangle pixels verified.
- [x] `play_level_sounds` - `LevelEvents.PlaySounds`; native channel ordering/volume/gating.
- [x] `player_shot_hits_enemy` - `EnemySystem.ShotHits`; 5,000 native hit cases.
- [x] `mark_player_shot_linked_enemies` - `EnemySystem.MarkLinked`; full native marker sweeps.
- [x] `animate_player_shot_damaged_enemies` - `EnemySystem.AnimateDamaged`; native states/effects.
- [x] `destroy_player_shot_linked_enemies` - `EnemySystem.DestroyLinked`; native death/pickup/reward traces.
- [x] `process_player_shot` - `EnemySystem.ProcessShot`; integrated native projectile/enemy/effect traces.
- [x] `draw_enemy_shots` - `EnemySystem.UpdateEnemyShots`; native motion/collision/draw traces and real combat integration.
- [x] `draw_level_debug_and_timer` - `LevelTimerDisplay.Draw`, concrete font host and shared keyboard debug history/pickup display-time/runtime pacing/event countdown; 16,384 native multi-frame traces including infinite FPS, high tick signedness, byte flash reversals, sound cadence and timer expiration; real event jump/padded font/SDL presentation/PIT pacing and failure propagation verified. Legacy scratch `tempW/tempX/tempY` mouse copies have no retained consumer in this frame path.
- [x] `update_level_network_state` - `LevelEvents.UpdateNetworkState`; 5,120 complete native traces plus actual two-peer SDL2_net delayed gameplay/request integration; native overlapping diagnostic offsets preserved, location assertion becomes an explicit error.
- [x] `update_level_end_state` - `LevelEvents.UpdateEndState`; all 1,024 native flag combinations.
- [x] `record_level_demo` - `DemoSession.RecordLevel`; 64 native recording scenarios compare every byte of the 30-byte header, NUL padding/high-bit/truncated names, episode/item indices/powers/song and sequential first-free filenames. Existing recordings remain intact; concurrent creation uses atomic CreateNew and all 256 occupied slots raise an explicit error rather than native wraparound looping.
- [x] `run_level_frame` - `LevelFrame.Run`; native event, render, player,
  collision, sound, input, network and end-state ordering plus both early exits
  verified with deterministic host traces.
- [x] `initialize_level_players_and_display` - `LevelPlayerInitializer.Initialize`; 512 native startup modes compare both alive flags, old/current difficulty with episode-4 clamp, fixed player origins, all 40 history entries/player and last-shot positions plus picture/options/title/present/gamma/fade/conditional shape-load order. Concrete indexed-font drawing, gamma-corrected SDL palette fade, conditional existing explosion-sheet behavior and sequential failure side effects verified.
- [x] `initialize_level_map_and_ship_state` - `LevelMapShipInitializer.Initialize`; 1,024 unchanged-native traces compare all three row pointers using native layer strides, map X/header preservation, horizontal/vertical offsets, current versus retained prior/secondary camera offsets, layer delays, music fade, back positions, shot power, starfield speed, and both player velocity/invulnerability resets; preloaded tile maps remain installed and shared `PlayerShipInfo` performs ship-specific initialization.
- [x] `initialize_level_flags` - `LevelFlagsInitializer.Initialize`; 16,384 extracted-native cases verify every entry reset, three per-frame button flags, shared quit-request routing, map/effect pacing, event/filter/warning/boss-bar state and preservation of boss armor/color.
- [x] `initialize_level_status_display` - `LevelStatusDisplayInitializer.Initialize`; 256 extracted-native cases verify byte-narrowed shield recharge timing, both shield maxima, armor gauge/clamping, superbomb and cube/flash/notice resets, and text/music/port-button call order.
- [x] `initialize_level_combat_state` - `LevelCombatStateInitializer.Initialize`; 256 unchanged-native cases verify combat/player/enemy/projectile/effect/weapon/level resets, network-only enemy availability, Galaga difficulty, draw order and all three background wrap offsets.
- [x] `JE_main` - `LevelMainLoop.Run`; initial and subsequent level setup,
  network/demo gates, backup/recording, title return and frame-loop order
  verified with deterministic host traces.
- [x] `read_level_header_and_events` - `LevelMap.ReadHeader`; signed record layout/native parity.
- [x] `load_level_shapes` - `LevelMap.LoadShapes`; swapped IDs, ownership, null/fill semantics.
- [x] `load_level_map_layer` - `LevelMap.LoadMapLayer`; all three layer dimensions/order.
- [x] `play_map_transition` - `MapTransition.Play`; 16 extracted-native frame traces cover upper/lower/horizontal and fallback reveals, both 320/327 pitches, key skipping, constant-play behavior, load/event order, and final surface/buffer contents.
- [x] `handle_episode_presentation` - `EpisodePresentation.Handle`; 40 extracted-native command/network/constant-play traces verify callback routing, offline fade gates, clear/present/palette ordering, completion return propagation and unknown-command behavior.
- [x] `show_episode_completion` - `EpisodeCompletion.Show`; 48 unchanged-native
  scenarios verify encrypted hint offsets, score/cash text, line counts,
  completion early returns, SuperTyrian mode mapping, unlock text and ship
  graphics, fade/input/network ordering, final state and stream position.
- [x] `show_episode_picture` - `EpisodePictureDisplay.Show`; 256 unchanged-native cases verify the constant-play gate, native `atoi` parsing, PCX-palette branch, picture selection and clear/present/fade ordering.
- [x] `show_episode_warning` - `EpisodeWarningDisplay.Show`; 256 unchanged-native cases verify both early returns, warning-state setup, C-style `atoi` and 16-bit narrowing, encrypted Pascal lines, and display/key-clear order.
- [x] `handle_episode_level_jump` - `EpisodeLevelJump.Handle`; 2,048 unchanged-native cases verify all six gates, `H`-specific `atoi` offset, byte narrowing and failed-gate state preservation.
- [x] `configure_episode_mode` - `EpisodeModeConfiguration.Configure`; 256 unchanged-native cases verify mode flags, player item copying, loadout rewrites, sidekick resets and cash state.
- [x] `read_episode_item_availability` - `EpisodeItemAvailability.Read`; 15 unchanged-native scenarios verify encrypted Pascal rows, all shop-setting branches, typed item indices, row capacity, cleared availability state, stream position and item-screen call ordering.
- [x] `load_selected_level_map` - `LevelMap.LoadSelected`; episode directory and shape-file lookup.
- [x] `JE_loadMap` - `EpisodeMapLoader.Run`; encrypted command dispatch,
  section seek/jump and loaded-game retry, level metadata, and demo/title/
  presentation/map routing verified with deterministic encoded fixtures.
- [x] `networkStartScreen` - `NetworkStartScreen.Run`/`NetworkStartHost`/`NetworkStartChannel`; 112 native controller/peer start/cancel/poll/state/fixed-width-wire cases, managed concrete surfaces/fades/selection and actual SDL2_net two-peer handshake/details/cash/ship/difficulty/episode synchronization; both selector cancellation paths propagate quit without starting the game.
- [x] `draw_title_screen` - `TitleScreen.Draw`; fourteen native draw traces cover all selections, 29-frame moving logo, two palette partitions, version/RELOADED labels, measured outlines and saved surfaces; actual SDL2 animation and concrete pixels verified.
- [x] `wait_for_title_screen_input` - `TitleScreen.Wait`; strict >30000 idle/demo threshold, uint tick wrap, ushort prior mouse coordinates including negatives, text/mouse/key/motion exit and filtered cursor/presentation/joystick timing match native traces; cancellation verified.
- [x] `read_title_screen_input` - `TitleScreen.Read`; 4,096 native snapshots and explicit half-open bounds verify hover selection, separate click coordinates, mouse precedence and wrapped keys/sample order.
- [x] `dispatch_title_screen_action` - `TitleScreen.Dispatch`; 28 native result/restart states cover all seven actions, child cancellation/success, demo and quit; actual high-score and new-game child flows integrated through SDL2.
- [x] `handle_title_screen_secret_codes` - `TitleScreen.Secrets`; 136 native cases cover streaming counters, prefix/reset/non-overlap behavior, nine-byte maximum, canceled arcade restart, destruct and SuperTyrian ordered sound/effect/black/action; caller child services bound explicitly.
- [x] `titleScreen` - `TitleScreen.Run`; 144 complete native loops verify Setup label override, cursor ownership, restarts, action/secret precedence, demo timeout and exits; actual SDL2 path traverses animated title/high-score/title restart/selection/new-game. Final game startup, load/setup/instructions/whoa child implementations and playable outer host remain pending.
- [x] `newGame` - `NewGame.Run`; 2,048 native selection/mode/prior-load/rich/episode/difficulty states preserve partial cancellation effects, cash/ship/device precedence and signed increment; actual selection-menu game-state binding verified.
- [x] `newSuperArcadeGame` - `NewGame.RunSuperArcade`; 1,792 native starts across all seven modes verify item mapping, ship previews, intro order, cancellation side effects, prior loaded state, signed difficulty and NortShip Z paired companions; concrete font/ship/palette/input services verified.
- [x] `newSuperTyrianGame` - `NewGame.RunSuperTyrian`; 256 native starts verify ScrollLock difficulty, exact intro text/title, audio, waits, episode init, cheat/mode flags and loadout; concrete services/cancellation verified. Final title/game-loop caller remains pending.
- [x] `intro_logos` - `IntroLogos.Run`/`IntroLogoHost`; native white fade/pictures 10 and 12/timed-or-input waits/black fades and title animation flag verified; concrete palette/input/PIT and actual SDL startup presentation integrated.
- [x] `JE_readTextSync` - `WarningTextDisplay.Synchronize`; intentionally has no effects, matching the native function's entirely disabled `#if 0` body, called before/after all 2,816 differential scenarios.
- [x] `JE_displayText` - `WarningTextDisplay.Run`/`WarningTextHost`; 2,816 native line/prompt/warning/wait/animation/escape/halt traces, concrete padded pixels and shared character-glow state/timing, actual SDL mouse/Return/Escape prompt flows; validates native line limits and preserves halt/skip ordering.
- [x] `scale_enemy_score` - `EnemySystem.ScaleScore`; native difficulty cases.
- [x] `scale_enemy_armor` - `EnemySystem.ScaleArmor`; native difficulty cases.
- [x] `initialize_enemy_score_and_armor` - `EnemySystem.InitializeScoreAndArmor`.
- [x] `find_enemy_sprite_sheet` - `EnemySystem.FindSpriteSheet`.
- [x] `JE_newEnemyOld` - `EnemySystem.CreateLegacy`; native ID/arcade case.
- [x] `JE_newEnemy` - `EnemySystem.Create`; native allocation/exhaustion.
- [x] `JE_makeEnemy` - `EnemySystem.Initialize`; complete native slot state/RNG.
- [x] `JE_createNewEventEnemy` - `LevelEvents.CreateEnemy`; native spawns/RNG.
- [x] `JE_eventJump` - `LevelEvents.Jump`; native jump/return cursor ordering.
- [x] `JE_searchFor` - `LevelEvents.SearchEnemy`; last active match/Galaga effects.
- [x] `resolve_event_enemy_link` - `LevelEvents.ResolveEnemyLink`.
- [x] `create_event_enemy_at_y` - `LevelEvents.CreateEnemyAtY`.
- [x] `apply_event_random_special_drop` - `LevelEvents.ApplyRandomSpecialDrop`.
- [x] `set_event_enemy_death` - `LevelEvents.SetEnemyDeath`.
- [x] `handle_environment_event` - `LevelEvents.HandleEnvironmentEvent`.
- [x] `handle_enemy_global_move_event` - `LevelEvents.GlobalMove`.
- [x] `handle_enemy_global_accel_event` - `LevelEvents.GlobalAcceleration`.
- [x] `handle_enemy_global_animation_event` - `LevelEvents.GlobalAnimation`.
- [x] `handle_enemy_global_damage_event` - `LevelEvents.GlobalDamage`.
- [x] `handle_enemy_acceleration_reverse_event` - `LevelEvents.ReverseAcceleration`.
- [x] `handle_enemy_fire_override_event` - `LevelEvents.OverrideFire`.
- [x] `handle_enemy_death_event` - `LevelEvents.HandleDeath`.
- [x] `handle_enemy_event` - `LevelEvents.HandleEnemyEvent`.
- [x] `handle_level_event` - `LevelEvents.HandleLevelEvent`.
- [x] `handle_misc_event` - `LevelEvents.HandleMiscEvent`.
- [x] `JE_eventSystem` - `LevelEvents.Dispatch`; all native event groups and host calls.
- [x] `JE_whoa` - `ScreenBleed.Run`; 1,264 native-matched frame/scratch fingerprints over two pitches/six exit timings, full final bytes for all three buffers per timing, exact in-place neighbor/copy/swap semantics, 300-frame/input exit and warning-lines reset; actual SDL presentation/input, alias validation and cancellation verified.
- [x] `JE_barX` - `EnemySystem.BeveledBar`; ordered three-tone rectangle parity.
- [x] `draw_boss_bar` - `EnemySystem.DrawBossBars`; linked minimum armor/state/HUD parity.

### `src/varz.c`

- [x] `JE_getShipInfo` - `PlayerShipInfo.Initialize`; 1,024 native scenarios verify generator power, primary/custom graphics, secondary default armor, initial armor/hit boxes and sprite-sheet selection; shared frame graphics and concrete rendering integrated.
- [x] `JE_SGr` - `PlayerShipInfo.ExtraGraphic`; all fifteen selectors and 10,240 native lookups verify graphic indices and pointer preservation/switching. Invalid custom records raise explicit errors instead of native out-of-bounds access.
- [x] `JE_drawOptions` - `PlayerWeapons.DrawOptions`; native selected player, ammo/refill/style/animation/charge initialization, icons/gauges and full HUD pixels verified within special sidekick spawning and attack traces; concrete icon adapter checked.
- [x] `JE_drawOptionLevel` - `PlayerWeapons.DrawOptionLevel`; exact three highlighted/unhighlighted two-player gauge rectangles and single-player gate verified in native full HUD comparisons.
- [x] `JE_tyrianHalt` - `GameShutdown.Halt`/`SdlGameShutdownHost`; 512 native cleanup/save/farewell/exit traces, ten explicit failure paths; isolated real SDL audio/video/joystick/event teardown and subsystem-zero query, shapes/sounds cleared, persisted configuration reload, code-9 skip-save and save-failure cleanup. Managed nonreturning calls throw `GameTerminationException` for the final executable to map to process exit; warning halt composition verified. Final main-host ownership remains pending.
- [x] `JE_specialComplete` - `PlayerSpecialWeapons.Complete`; all native effect cases and native no-op type 15, per-owner behavior, byte cost parameter, sound/cooldown and raw 707 projectiles compared in 4,096 native traces.
- [x] `JE_doSpecialShot` - `PlayerSpecialWeapons.Update`; shield/all/half/armor costs, affordability, execution clear/HUD order, held fire, duration/filter/RNG/spray/cooldown transitions, full native projectiles and frame pixels compared in standalone and integrated attack traces.
- [x] `JE_setupExplosion` - `SceneEffects.SetupExplosion`.
- [x] `JE_setupExplosionLarge` - `SceneEffects.SetupLarge`.
- [x] `JE_wipeShieldArmorBars` - `PlayerCombat.WipeBars`.
- [x] `JE_playerDamage` - `PlayerCombat.Damage`; native damage/HUD states.
- [x] `JE_portConfigs` - `PlayerCombat.PortConfigurations`.
- [x] `JE_drawShield` - `PlayerCombat.DrawShield`; native HUD pixels.
- [x] `JE_drawArmor` - `PlayerCombat.DrawArmor`; native HUD pixels/clamping.
- [x] `JE_doSP` - `SceneEffects.SpawnSuperPixels`; native state/RNG.
- [x] `JE_drawSP` - `SceneEffects.DrawSuperPixels`; native pixels.

### `src/vga256d.c`

- [x] `JE_pix` - `IndexedSurface.Pixel`.
- [x] `JE_pix3` - `IndexedSurface.PixelCross`.
- [x] `JE_rectangle` - `IndexedSurface.Rectangle`.
- [x] `fill_rectangle_xy` - `IndexedSurface.FillRectangle`.
- [x] `JE_barShade` - `IndexedSurface.ShadeRectangle`.
- [x] `JE_barBright` - `IndexedSurface.BrightenRectangle`.
- [x] `draw_segmented_gauge` - `IndexedSurface.SegmentedGauge`.

### `src/vga_palette.c`

- No function definitions; tracked by the file inventory.

### `src/video.c`

- [x] `init_video` - `SdlVideoDevice` constructor; real SDL surfaces/window/renderer/fullscreen/textures and all nine scalers.
- [x] `deinit_video` - `SdlVideoDevice.Dispose`/`Release`; deterministic native cleanup and managed surface ownership.
- [x] `init_renderer` - `SdlVideoDevice` constructor; actual native renderer creation and checked errors.
- [x] `deinit_renderer` - `SdlVideoDevice.Release`; real SDL renderer destruction.
- [x] `init_texture` - `SdlVideoDevice.CreateTexture`; RGB888 streaming textures, managed palette map and actual frame parity.
- [x] `deinit_texture` - `SdlVideoDevice.CreateTexture`/`Release`; deterministic native texture replacement/destruction.
- [x] `window_get_display_index` - `SdlVideoDevice.DisplayIndex`; real SDL display lookup.
- [x] `window_center_in_display` - `SdlVideoDevice.CenterInDisplay`; native display-bound centering.
- [x] `reinit_fullscreen` - `SdlVideoDevice.ReinitializeFullscreen`; real native display clamp/window/fullscreen transitions.
- [x] `video_on_win_resize` - `SdlVideoDevice.WindowResized`; exact native scaler-size minimums.
- [x] `toggle_fullscreen` - `SdlVideoDevice.ToggleFullscreen`; real windowed/current-display toggling.
- [x] `init_scaler` - `SdlVideoDevice.SetScalerByName`/`CreateTexture`; exact scaler sizes, resize and full native SDL output parity.
- [x] `set_scaling_mode_by_name` - `VideoLayout.SetModeByName`; exact native names/failure retention.
- [x] `JE_clr256` - `SdlVideoDevice.Clear`; entire indexed pixel memory cleared, real black-frame parity.
- [x] `JE_showVGA` - `SdlVideoDevice.Present()`; primary-surface real texture/renderer presentation.
- [x] `calc_dst_render_rect` - `VideoLayout.Calculate`; exact native center/integer/aspect outputs.
- [x] `scale_and_flip` - `SdlVideoDevice.Present(screen)`; managed scaler/palette, real native texture upload, viewport/clear/copy/present and last-output rect.
- [x] `mapScreenPointToWindow` - `VideoLayout.ScreenToWindow`; native pixel-center mapping.
- [x] `mapWindowPointToScreen` - `VideoLayout.WindowToScreen`; native signed/truncated inverse mapping.
- [x] `scaleWindowDistanceToScreen` - `VideoLayout.WindowDistanceToScreen`; native relative-distance rounding.

### `src/video_scale.c`

- [x] `set_scaler_by_name` - `VideoScaler.SetByName`; exact names/order.
- [x] `nn_32` - `VideoScaler.Scale32` nearest algorithm.
- [x] `nn_16` - `VideoScaler.Scale16` nearest algorithm.
- [x] `scale2x_32` - `VideoScaler.Scale32` Scale2x algorithm.
- [x] `scale2x_16` - `VideoScaler.Scale16` Scale2x algorithm.
- [x] `scale3x_32` - `VideoScaler.Scale32` Scale3x algorithm.
- [x] `scale3x_16` - `VideoScaler.Scale16` Scale3x algorithm.

### `src/video_scale_hqNx.c`

- [x] `interp1` - `HqVideoScaler.Interpolate(1)`; exact unsigned raw additions and shift.
- [x] `interp2` - `HqVideoScaler.Interpolate(2)`; native 2:1:1 interpolation.
- [x] `interp3` - `HqVideoScaler.Interpolate(3)`/`Weighted`; native 7:1 masked interpolation.
- [x] `interp4` - `HqVideoScaler.Interpolate(4)`/`Weighted`; native 2:7:7 masked interpolation.
- [x] `interp5` - `HqVideoScaler.Interpolate(5)`; exact unsigned raw half-sum.
- [x] `interp6` - `HqVideoScaler.Interpolate(6)`/`Weighted`; native 5:2:1 masked interpolation.
- [x] `interp7` - `HqVideoScaler.Interpolate(7)`/`Weighted`; native 6:1:1 masked interpolation.
- [x] `interp8` - `HqVideoScaler.Interpolate(8)`/`Weighted`; native 5:3 masked interpolation.
- [x] `interp9` - `HqVideoScaler.Interpolate(9)`/`Weighted`; native 2:3:3 masked interpolation.
- [x] `interp10` - `HqVideoScaler.Interpolate(10)`/`Weighted`; native 14:1:1 masked interpolation.
- [x] `diff` - `HqVideoScaler.Different`; native masked Y/U/V thresholds.
- [x] `hq2x_32` - `HqVideoScaler.Scale(factor: 2)`/`Pattern2`; native exact pixels and real SDL output.
- [x] `hq3x_32` - `HqVideoScaler.Scale(factor: 3)`/`Pattern3`; native exact pixels and real SDL output.
- [x] `hq4x_32` - `HqVideoScaler.Scale(factor: 4)`/`Pattern4`; native exact pixels and real SDL output.

### `src/xmas.c`

- [x] `xmas_time` - `ChristmasPrompt.IsChristmasTime`; all twelve native local-month cases and local timezone conversion boundary verified.
- [x] `xmas_prompt` - `ChristmasPrompt.Run`/`ChristmasPromptHost`; 300 native complete scenarios with fixed-point/layer snow, respawn, choice/click/mouse-key precedence, fades and RNG call ordering; padded blended/full-shadow drawing, cancellation and actual SDL choices verified. Platform-rand service and startup Christmas asset/global ownership remain final-host integration requirements.

### Inline function definitions in headers

#### `src/tinydir.h`

- [x] `tinydir_open` - `Directory.EnumerateFiles` in the managed item-bundle
  loader and save catalog supplies the regular-file entries those callers
  consume; missing-directory behavior and native parsed results are tested.
- [x] `tinydir_open_sorted` - managed file enumeration plus explicit ordering
  in `ItemDefinitions.LoadAll` and `SavedGameCatalog.BuildList`; native mod
  ordering and saved-list traces match.
- [x] `tinydir_close` - managed enumeration scopes dispose their iterators on
  completion through `foreach`/LINQ enumerator disposal; both callers are
  exercised by their directory-loading and catalog tests.
- [x] `tinydir_next` - `foreach` iteration over `Directory.EnumerateFiles`
  replaces native directory-entry advancement; native bundle/catalog outputs
  match.
- [x] `tinydir_readfile` - `FileInfo` values from managed enumeration replace
  native entry metadata for the callers' file filtering and naming needs.
- [x] `tinydir_readfile_n` - `TinyDirectory.TryReadFileAt` copies indexed
  entries and recomputes extensions; `DirectoryInfo.EnumerateFiles` supplies
  save-catalog regular-file entries, and native traces confirm same-suffix
  directories are excluded.
- [x] `tinydir_open_subdir_n` - `TinyDirectory.OpenSubdirectory`; directories-first ordered listing, index/type errors, and native empty-directory failure behavior.
- [x] `tinydir_file_open` - `TinyDirectory.TryOpenFile`; exact path lookup, root/directory/file status, and filename extension semantics.
- [x] `_tinydir_get_ext` - `FileInfo.Extension` and the managed suffix check
  replace extension extraction; native save-list tests cover a leading-dot
  save name, mixed-case suffix, and nonmatching extension.
- [x] `_tinydir_file_cmp` - ordinal name ordering in `SavedGameCatalog.BuildList`
  matches the native file comparator after its directory-first entries are
  filtered; native list traces verify mixed-case order and player filters.
- [x] `_tinydir_dirent_buf_size` - the readdir_r-only buffer-size helper is
  excluded by the native build's `TINYDIR_USE_READDIR` configuration; managed
  directory enumeration owns its platform buffer and exposes no equivalent
  buffer-size operation.

#### `src/config_file.h`

- [x] `config_string_to_cstr` - Managed `ConfigOption.Value`/`Values` expose
  immutable managed strings; `CString` applies the native NUL-terminated view.
  Configuration regression fixtures cover embedded NUL values and round-trip
  output.
- [x] `config_add_section` - `Configuration.AddSection`.
- [x] `config_set_option` - `ConfigSection.SetOption`.
- [x] `config_get_or_set_option` - `ConfigSection.GetOrSetOption`.
- [x] `config_set_string_option` - `ConfigSection.SetOption`.
- [x] `config_set_value` - `ConfigOption.SetValue`.
- [x] `config_add_value` - `ConfigOption.AddValue`.
- [x] `config_is_value_list` - `ConfigOption.IsList`.
- [x] `config_get_value_count` - `ConfigOption.Count`.

#### `src/file.h`

- [x] `fread_bool_die` - `BinaryFile.ReadBoolean`.
- [x] `fread_u8` - `BinaryFile.ReadBytes`.
- [x] `fread_u8_die` - `BinaryFile.ReadUInt8`.
- [x] `fread_s8_die` - `BinaryFile.ReadInt8`.
- [x] `fread_u16_die` - `BinaryFile.ReadUInt16`.
- [x] `fread_s16_die` - `BinaryFile.ReadInt16`.
- [x] `fread_u32_die` - `BinaryFile.ReadUInt32`.
- [x] `fread_s32_die` - `BinaryFile.ReadInt32`.
- [x] `fwrite_bool_die` - `BinaryFile.WriteBoolean`.
- [x] `fwrite_u8` - `BinaryFile.WriteBytes`.
- [x] `fwrite_u8_die` - `BinaryFile.WriteUInt8`.
- [x] `fwrite_s8_die` - `BinaryFile.WriteInt8`.
- [x] `fwrite_u16_die` - `BinaryFile.WriteUInt16`.
- [x] `fwrite_s16_die` - `BinaryFile.WriteInt16`.
- [x] `fwrite_u32_die` - `BinaryFile.WriteUInt32`.
- [x] `fwrite_s32_die` - `BinaryFile.WriteInt32`.

#### `src/player.h`

- [x] `all_players_dead` - `PlayerRules.AllDead`.
- [x] `all_players_alive` - `PlayerRules.AllAlive`.

#### `src/sprite.h`

- [x] `sprite` - `SpriteTable.Get`.
- [x] `sprite_exists` - `SpriteTable.Exists`.
- [x] `get_sprite_width` - `SpriteTable.Width`.
- [x] `get_sprite_height` - `SpriteTable.Height`.

#### `src/vga256d.h`

- [x] `fill_rectangle_wh` - `IndexedSurface.FillRectangleSize`.
