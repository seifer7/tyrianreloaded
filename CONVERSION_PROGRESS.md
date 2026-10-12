# C# conversion progress

This inventory is the baseline of **126 files tracked before this setup**:
50 `.c` files, 50 `.h` files, and 26 other tracked files. It records **707
function definitions in C files** plus **32 inline function definitions in
headers**, for **739 baseline functions**. Function names are listed once per
source file; the two function-like initializer macros in `src/episodes.c`
(`ITEM_INT` and `ITEM_REF`) are not functions and are intentionally omitted.

**Baseline conversion status: 2/126 files dispositioned; 336/739 functions
converted and verified.** The C# foundation also adds one solution, shared
build properties, four projects, forty-seven application source files, thirty-eight
managed test sources, a native core harness, and twenty oracle generators
(113 added implementation/build/test files). The game entrypoint remains a
scaffold; the data extractor is functional with synthetic native parity.

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
- [x] `src-csharp/OpenTyrian.Game/Program.cs` — explicit not-yet-ported status
  entrypoint; verified to build.
- [x] `src-csharp/OpenTyrian.DataExtractor/OpenTyrian.DataExtractor.csproj` —
  data-extractor host project referencing the managed core; verified to build.
- [x] `src-csharp/OpenTyrian.DataExtractor/Program.cs` — explicit
  extraction CLI entrypoint; verified by build and core CLI parity checks.
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
- [x] `src-csharp/OpenTyrian.Core/Configuration.cs` - ordered scalar/list
  configuration model, quoted text codec, typed settings, and case-sensitive
  section/option lookup. Native serialization and numeric-reading fixtures
  match compiled C. The ten unmanaged string/allocation lifecycle helpers are
  left pending their explicit managed-ownership disposition.
- [x] `tests/OpenTyrian.Core.Tests/ConfigurationTests.cs` - configuration
  roundtrips, model operations, typed settings, and invalid directives.
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
  truncation. HQ scaler names remain inventoried but HQ kernels and SDL
  texture-locking integration are pending, not replaced by nearest scaling.
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
  Failure handling removes incomplete temporary output and refuses concurrent
  overwrites; real-asset verification remains pending.
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
  explosion/superpixel handlers and enemy collision callers are integrated;
  whole-game callers remain pending.
  Native invalid item-count equality, array offsets, and undefined floating
  conversions throw rather than accessing invalid memory.
- [x] `tests/OpenTyrian.Core.Tests/PlayerShotTests.cs` - six native state/
  effect traces, all 81 slots, homing target disappearance, mouse control,
  capacity/power rejection, zero graphics, preview behavior, reserved slot,
  short wrapping, special collision radii, and 68 directional updates per
  trace. SpriteTests additionally exercise the rendering/callback adapter.
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
  tremolo/arpeggio setup, and managed teardown. Required OPL adapter remains pending.
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
  truncation/invalid data, and real archive/controller/LDS/mixer integration
  through an explicitly synthetic OPL renderer (not FM waveform parity).
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
- [x] `tests/native/core_reference.c` - asset-free reference harness linked
  with the unchanged native parser and RNG; compiled with MSVC x64.

## Verification scope and remaining integration

The converted functions below are integrated into the core library and
regression harness, not yet into playable game flows. Keep native file
checkboxes unchecked until their gameplay callers are ported and exercised.
Full native game builds and runtime parity still require the SDL
development dependencies and separately supplied Tyrian assets. MSVC is
installed outside PATH and can compile asset-free reference programs via
`vcvars64.bat`; no native baseline game build has yet been verified.
The standalone managed extractor is integrated end-to-end and its native
source-region oracle needs no SDL runtime, but original-asset verification has
not been performed. See the workflow for intentional output-failure deviations.

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
| `Configuration.cs` | `ConfigOption` constructor, `CString`, `SetValue`, `AddValue`, `RemoveValue`; `ConfigSection` constructor, `GetOption`, `SetOption`, `GetOrSetOption`, `AppendOption`, `TryGetString`, `GetOrSetString`, `SetBoolean`, `TryGetBoolean`, `GetOrSetBoolean`, `SetInt32`, `SetUInt32`, `TryGetInt32`, `TryGetUInt32`, `GetOrSetInt32`, `GetOrSetUInt32`, `TryGetInteger`; `Configuration.Clear`, `AddSection`, `FindSections`, `FindSection`, `FindOrAddSection`, `Parse`, `IsWhitespace`, `IsEnd`, `CharacterAt`, `ReadDirective`, `TryReadField`, `HexDigit`, `Write`, `WriteField` |
| `EncryptedText.cs` | `Decrypt`, `ReadPascalString`, `SkipPascalString` |
| `AnimationFile.cs` | Constructor, `FindPage`, `LoadPage`, `RenderFrame`, `ReadPageHeader`, `Dispose`; palette/page-header record types |
| `IndexedSurface.cs` | Both constructors, `Pixel`, `PixelCross`, `Rectangle`, `FillRectangle`, `FillRectangleSize`, `ShadeRectangle`, `BrightenRectangle`, `SegmentedGauge`, `ValidRectangle` |
| `PaletteSystem.cs` | Constructor, `Load`, `Expand`, `SetPalette`, `SetSolid`, `InitializeFade`, `InitializeSolidFade`, `StepFade`, `FadePalette`, `FadeSolid`, `FadeBlack`, `FadeWhite`, `RunFade`, `SetColor`, `RgbToYuv`, `ValidateRange` |
| `PictureLoader.cs` | `LoadPcx`, `LoadPicture`, `DecodeRle`, `ReadByte`, `ValidateScreen` |
| `VideoScaler.cs` | `SetByName`, `Scale32`, `Scale16`, generic `Scale`; scaler-definition/algorithm types |
| `PlayerState.cs` | `PlayerItems.Copy`, `PlayerState` constructor, `Cash` and `Lives` accessors; `PlayerRules.CalculatePurpleBalls`, `PowerUpWeapon`, `GotPurpleBall`, `AllDead`, `AllAlive`; weapon/sidekick/item model types |
| Core tests `Program.cs` | Entrypoint, `Run`, `TestDefaultSeed`, `TestPublishedVectors`, `TestSeedTruncationAndRepeatability`, `TestStateWraparound`, `TestFloatBounds`, `TestSizeBufferReadAndWrite`, `TestSizeBufferBoundsAndRecovery`, `TestSizeBufferCopyFailures`, `TestSizeBufferSeekModes`, `AssertSequence`, `AssertEqual`, `AssertThrows` |
| `ConfigurationTests.cs` | `Run`, `Equal` |
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
| `PlayerShots.cs` | `PlayerShots` constructor, `Create`, `SetDirection`, `Simulate`, `MoveAndDraw`, `MoveCircle`, `AdvanceAnimation`, `RoundedShort`; `SpritePlayerShotEffects.Draw`, `DrawSpecial`, `Explosion`, `SuperPixels`; projectile/enemy/result models and `IPlayerShotEffects` contract |
| `PlayerShotTests.cs` | `Run`, `Fixture`, `Setup`, `Execute`, `Dump`, `Check`; `Effects.Draw`, `DrawSpecial`, `Explosion`, `SuperPixels` |
| `build_shots_reference.py` | `build_source`, `main`; generated `dump`, `main`, sprite/effect recording shims |
| `SceneEffects.cs` | Constructor, `SetupExplosion`, `SetupLarge`, both `UpdateExplosions` overloads, `SpawnSuperPixels`, `DrawSuperPixels`, `CreateProjectileEffects`, `Blend`; `Explosion`, `RepeatingExplosion`, `SuperPixel` models |
| `SceneEffectsTests.cs` | `Run`, `Execute`, `Dump`, `Check` |
| `build_effects_reference.py` | `build_source`, `main`; generated sprite shims, `dump_state`, `main` |
| `PlayerCombat.cs` | Constructor, `Damage`, `WipeBars`, `DrawShield`, `DrawArmor`, `PortConfigurations`, `DrawBar`, `RoundedGauge` |
| `PlayerCombatTests.cs` | `Run`, `Execute`, `Check` |
| `build_combat_reference.py` | `build_source`, `main`; generated `SDL_FillRect`, `main` |
| `EnemyState.cs` | `EnemyState` and `BossBar` runtime model properties and fixed-size arrays |
| `EnemyMovement.cs` | `UpdateAcceleration`, both `AnimateAndDraw` overloads, `BlitEnemy`, `UpdatePosition`, `Oscillate`, `AimVelocity`, `RoundVelocity` |
| `EnemyAttacks.cs` | `AimShot`, `CreateEnemyShot`, `ApplyWeaponSpecial`, `FireShots`, `ProcessAttacks`, `LaunchFromParent`, `DrawAndUpdate`, `RandomSoundChannel`, both `UpdateEnemyShots` overloads; `EnemyShot` runtime model |
| `EnemyAttackTests.cs` | `Run`, `Fixture`, `Execute`, `ExecuteSpecials`, `Create`, `UpdateShots`, `Dump`, `Check` |
| `build_enemy_attacks_reference.py` | `build_source`, `main`; generated `JE_playerDamage`, `dump_attacks`, oracle `main` |
| `LevelEvents.cs` | Constructor, `Current` accessor, `Jump`, `SearchEnemy`, `CreateEnemy`, `CreateEnemyAtY`, `ResolveEnemyLink`, `ApplyRandomSpecialDrop`, `SetEnemyDeath`, `GlobalMove`, `GlobalAcceleration`, `GlobalAnimation`, `GlobalDamage`, `ReverseAcceleration`, `OverrideFire`, `HandleDeath`, `HandleEnemyEvent`; `LevelEvent` typed record properties |
| `LevelEventActions.cs` | `HandleEnvironmentEvent`, `PositiveByte`, `SetBackgroundMoves`, `HandleLevelEvent`, `HandleMiscEvent`, `AssignRandomLink`, `Dispatch`, `UpdateEndState`, `ResumeBackgrounds`, `PlaySounds`; `ILevelEventServices` shape/text/song/volume contract and event-flow state properties |
| `LevelEventTests.cs` | `Run`, `Fixture`, `Execute`, `Create`, `Check` |
| `LevelEventDispatchTests.cs` | `Run`, `Execute`, `Flag`, `Dump`, `TestInvalidActions`, `ThrowsInvalid`; services `LoadEnemySheet`, `DrawTextWindow`, `PlaySong`, `SetVolume` |
| `build_enemy_events_reference.py` | `build_source`, `main`; generated `dump_event`, native helper/oracle `main` |
| `build_level_events_reference.py` | `build_source`, `main`; generated `dump_dispatch`, shape/text/music/volume recording shims, native oracle `main` |
| `LevelMap.cs` | Constructor, `ReadHeader`, `LoadShapes`, `LoadMapLayer`, `Load`, `LoadSelected`, `Apply`; `LevelMapHeader` record and `LevelTileSet` arrays/ownership |
| `LevelMapTests.cs` | `Run`, `BuildLevel`, `BuildShapes`, `TestIntegration`, `Dump`, `Hash`, `CompareNative`, `TestFailures`, `Throws`, `Check` |
| `build_level_map_reference.py` | `build_source`, `main`; generated binary-read/file-open/endian shims, `hash`, native oracle `main` |
| `EnemyHud.cs` | Both `DrawBossBars` overloads and `BeveledBar`; shared `BossBar.Armor` state |
| `EnemyHudTests.cs` | `Run`, `Execute`, `TestRendering`, `Check` |
| `build_boss_bar_reference.py` | `build_source`, `main`; generated `fill_rectangle_xy`, native oracle `main` |
| `LevelStateTests.cs` | `Run`, `Flag`, `TestAudioFailures`, `CompareNative` |
| `build_level_state_reference.py` | `build_source`, `main`; generated `multiSamplePlay`, native oracle `main` |
| `SoundBank.cs` | Both `Load` overloads, `ReadBank`, `PlaySample`, `Play`; `IAudioSampleConverter.Convert` contract and managed sample ownership |
| `SoundBankTests.cs` | `Run`, `BuildBank`, `TestSoundDispatch`, `TestFailures`, `Hash`, `CompareNative`, `Throws`, `Check`; recording and failure converters `Convert` |
| `build_sound_bank_reference.py` | `build_source`, `main`; generated audio conversion/read/playback shims, `hash_bytes`, native oracle `main` |
| `AudioMixer.cs` | Constructor, synchronized mode/state accessors, `RemainingSamples`, `SetVolume`, `Play`, `ClearChannels`, `ChangeMusic`, `Mix`, `Clamp`; `IMusicSynthesizer.Update`/`Render` contract |
| `AudioMixerTests.cs` | `Run`, `Execute`, `TestFailuresAndOwnership`, `Check`, `Throws`; synthetic/failing `Update` and `Render` |
| `build_audio_mixer_reference.py` | `build_source`, `main`; generated lock/update/render shims and native oracle `main` |
| `MusicPlayer.cs` | `MusicArchive` constructor, `Load`, `Song`; `MusicController` constructor, `SongPlaying` accessor, `PlaySong`, `Restart`, `Stop`, `Fade`; `IMusicPlayer.Load`/`Rewind`/`Fade` contract |
| `MusicPlayerTests.cs` | `Run`/local `State`, `Fixture`, `TestFailures`, `Hash`, `CompareNative`, `Throws`, `Check`; fake player `Load`, `Rewind`, `Fade`, `Update`, `Render` |
| `build_music_reference.py` | `build_source`, `main`; generated archive/read/lock/LDS shims, `state`, native oracle `main` |
| `LdsPlayer.cs` | Constructor, `NewChannels`, `Load`, `Require`, `Free`, `Rewind`, `Fade`, `SetRegister`, `SetRegisterMasked`, `Render`, `Register`, `Masked`, `PlaySound`; typed instrument/channel/position models and `IOplSynthesizer` contract |
| `LdsPlayback.cs` | `Update`, `Advance`, `ResetPackedChannels`, `Command`, `OffsetTune`, `TuneChannel`, `ApplyEffects`, `Tremolo` |
| `LdsTables.cs` | Exact frequency/operator/vibrato/tremolo table initialization |
| `LdsPlayerTests.cs` | `Run`, `Fixture`, `Execute`/local `State`, `TestFailuresAndIntegration`, `CompareNative`, `Check`, `Throws`; synthetic OPL `Reset`, `Write`, `Render` |
| `build_lds_reference.py` | `build_source`, `main`; generated OPL/read shims, `dump_state`, native oracle `main` |
| `VideoLayout.cs` | `Output` validation, `SetModeByName`, `Calculate`, `ScreenToWindow`, `WindowToScreen`, `WindowDistanceToScreen`; `ScalingMode`/`OutputRectangle` models |
| `InputState.cs` | Constructor/state accessors, `Initialize`, `FlushEvents`, `ClearKeyboard`, `SetRelative`, `MousePosition`, `RelativePosition`, `WaitInput`, `WaitNoInput`, `Wait`, `ServiceEvents`, `ValidateScancode`, `Delay`, `PollJoysticks`; typed event model and required `IInputPlatform` contract |
| `InputTests.cs` | `Run`, `Execute`/local `State`, `Bit`, `Hash`, `CompareNative`, `Check`, `Throws`; scripted platform event/timing/joystick/network/cursor/fullscreen/focus/quit services |
| `build_input_reference.py` | `build_source`, `main`; generated SDL window/event/input/wait services, `append`, `hash`, `state`, native oracle `main` |
| `MouseCursor.cs` | Constructor/state, `Start`, `StartFiltered`, `StartCore`, `Replace`, `Capture`, `CopyBlock`, `Validate`; exact pointer metadata initialization |
| `MouseCursorTests.cs` | `Run`, `Fixture`, `Hash`, `CompareNative`, `Check`, `Throws`; queue platform with explicit unexpected-service failures |
| `build_mouse_reference.py` | `function`, `build_source`, `main`; generated `hash`, synthetic sprites/surface and native oracle `main` |
| `EnemySystem.cs` | Constructor, `ProcessShot`, `ShotHits`, `MarkLinked`, `AnimateDamaged`, `DestroyLinked`, `FindSpriteSheet`, `CreateLegacy`, `Create`, `Initialize`, `ScaleScore`, `ScaleArmor`, `InitializeScoreAndArmor`, `RandomStart` |
| `EnemySystemTests.cs` | `Run`, `Fixture`, `Execute`, `DumpEffects`, `Normalize`, `Dump`, `IndexOfReference`, `Check`; `TraceEffects.Draw`, `DrawSpecial`, `Explosion`, `SuperPixels` |
| `build_enemy_reference.py` | `build_source`, `main`; generated `sprite_identity`, `dump_enemy`, `dump_effects`, `JE_eventJump`, sprite shims, oracle `main` |

## Baseline file inventory

### Repository root

- [ ] `.editorconfig`
- [x] `.gitignore` — added nested C# build-output exclusions.
- [ ] `COPYING`
- [ ] `Doxyfile`
- [ ] `Makefile`
- [ ] `NEWS`
- [ ] `README`
- [ ] `lower-script.sh`

### Documentation and tools

- [ ] `doc/files.txt`
- [ ] `doc/tools/weapon.py`
- [ ] `doc/tyrian.hdt.txt`

### Linux integration and artwork

- [ ] `linux/icons/tyrian-128.png`
- [ ] `linux/icons/tyrian-22.png`
- [ ] `linux/icons/tyrian-24.png`
- [ ] `linux/icons/tyrian-32.png`
- [ ] `linux/icons/tyrian-48.png`
- [ ] `linux/man/opentyrian.6`
- [ ] `linux/opentyrian.desktop`

### Native source

- [ ] `src/animlib.c`
- [ ] `src/animlib.h`
- [ ] `src/arg_parse.c`
- [ ] `src/arg_parse.h`
- [ ] `src/backgrnd.c`
- [ ] `src/backgrnd.h`
- [ ] `src/config.c`
- [ ] `src/config.h`
- [ ] `src/config_file.c`
- [ ] `src/config_file.h`
- [x] `src/data_extractor.c` - managed CLI delegates to the fully integrated
  legacy extraction pipeline; synthetic native output/CLI parity verified.
- [ ] `src/destruct.c`
- [ ] `src/destruct.h`
- [ ] `src/editship.c`
- [ ] `src/editship.h`
- [ ] `src/episodes.c`
- [ ] `src/episodes.h`
- [ ] `src/file.c`
- [ ] `src/file.h`
- [ ] `src/font.c`
- [ ] `src/font.h`
- [ ] `src/fonthand.c`
- [ ] `src/fonthand.h`
- [ ] `src/game_menu.c`
- [ ] `src/game_menu.h`
- [ ] `src/helptext.c`
- [ ] `src/helptext.h`
- [ ] `src/joystick.c`
- [ ] `src/joystick.h`
- [ ] `src/jukebox.c`
- [ ] `src/jukebox.h`
- [ ] `src/keyboard.c`
- [ ] `src/keyboard.h`
- [ ] `src/lds_play.c`
- [ ] `src/lds_play.h`
- [ ] `src/loudness.c`
- [ ] `src/loudness.h`
- [ ] `src/lvllib.c`
- [ ] `src/lvllib.h`
- [ ] `src/lvlmast.c`
- [ ] `src/lvlmast.h`
- [ ] `src/mainint.c`
- [ ] `src/mainint.h`
- [ ] `src/menus.c`
- [ ] `src/menus.h`
- [ ] `src/mouse.c`
- [ ] `src/mouse.h`
- [ ] `src/mtrand.c`
- [ ] `src/mtrand.h`
- [ ] `src/musmast.c`
- [ ] `src/musmast.h`
- [ ] `src/network.c`
- [ ] `src/network.h`
- [ ] `src/nortsong.c`
- [ ] `src/nortsong.h`
- [ ] `src/nortvars.c`
- [ ] `src/nortvars.h`
- [ ] `src/opentyr.c`
- [ ] `src/opentyr.h`
- [ ] `src/opentyrian_version.h`
- [ ] `src/opl.c`
- [ ] `src/opl.h`
- [ ] `src/palette.c`
- [ ] `src/palette.h`
- [ ] `src/params.c`
- [ ] `src/params.h`
- [ ] `src/pcxload.c`
- [ ] `src/pcxload.h`
- [ ] `src/pcxmast.c`
- [ ] `src/pcxmast.h`
- [ ] `src/picload.c`
- [ ] `src/picload.h`
- [ ] `src/player.c`
- [ ] `src/player.h`
- [ ] `src/shots.c`
- [ ] `src/shots.h`
- [ ] `src/sizebuf.c`
- [ ] `src/sizebuf.h`
- [ ] `src/sndmast.c`
- [ ] `src/sndmast.h`
- [ ] `src/sprite.c`
- [ ] `src/sprite.h`
- [ ] `src/starlib.c`
- [ ] `src/starlib.h`
- [ ] `src/tinydir.h`
- [ ] `src/tyrian2.c`
- [ ] `src/tyrian2.h`
- [ ] `src/varz.c`
- [ ] `src/varz.h`
- [ ] `src/vga256d.c`
- [ ] `src/vga256d.h`
- [ ] `src/vga_palette.c`
- [ ] `src/vga_palette.h`
- [ ] `src/video.c`
- [ ] `src/video.h`
- [ ] `src/video_scale.c`
- [ ] `src/video_scale.h`
- [ ] `src/video_scale_hqNx.c`
- [ ] `src/xmas.c`
- [ ] `src/xmas.h`

### Visual Studio project and resources

- [ ] `visualc/data_extractor.vcxproj`
- [ ] `visualc/opentyrian.props.template`
- [ ] `visualc/opentyrian.sln`
- [ ] `visualc/opentyrian.vcxproj`
- [ ] `visualc/resources.rc`
- [ ] `visualc/saves/.gitkeep`
- [ ] `visualc/sdl_paths.props.template`
- [ ] `visualc/tyrian.ico`

## Function inventory

### `src/animlib.c`

- [x] `JE_loadPage` - `AnimationFile.LoadPage`; record totals and caching.
- [x] `JE_findPage` - `AnimationFile.FindPage`; boundary fixtures.
- [x] `JE_renderFrame` - `AnimationFile.RenderFrame`; page-record decoding.
- [ ] `JE_playAnim`
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

- [ ] `xor`
- [ ] `load_opentyrian_config`
- [ ] `save_opentyrian_config`
- [ ] `playeritems_to_pitems`
- [ ] `pitems_to_playeritems`
- [ ] `saveGameOperation`
- [ ] `loadGameOperation`
- [ ] `JE_saveGame`
- [ ] `read_savegame_metadata`
- [ ] `build_save_game_list`
- [ ] `JE_loadGame`
- [ ] `JE_initProcessorType`
- [ ] `JE_setNewGameSpeed`
- [ ] `JE_encryptSaveTemp`
- [ ] `JE_decryptSaveTemp`
- [ ] `get_user_directory`
- [ ] `JE_loadConfiguration`
- [ ] `JE_saveConfiguration`

### `src/config_file.c`

- [ ] `config_oom`
- [ ] `string_init_len`
- [ ] `string_deinit`
- [ ] `string_equal_len`
- [x] `config_init` - `Configuration` constructor.
- [x] `config_deinit` - `Configuration.Clear`; managed reset/collection.
- [ ] `init_section`
- [ ] `deinit_section`
- [x] `config_add_section_len` - `Configuration.AddSection`.
- [x] `config_find_sections` - `Configuration.FindSections`; managed enumeration.
- [x] `config_find_section` - `Configuration.FindSection`.
- [x] `config_find_or_add_section` - `Configuration.FindOrAddSection`.
- [ ] `init_option_value`
- [ ] `deinit_option_value`
- [ ] `init_option`
- [ ] `deinit_option`
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

- [ ] `get_unit_by_name`
- [ ] `load_destruct_config`
- [ ] `JE_destructGame`
- [ ] `JE_destructMain`
- [ ] `JE_introScreen`
- [ ] `DrawModeSelectMenu`
- [ ] `JE_modeSelect`
- [ ] `JE_generateTerrain`
- [ ] `DE_generateBaseTerrain`
- [ ] `DE_drawBaseTerrain`
- [ ] `DE_generateUnits`
- [ ] `DE_generateWalls`
- [ ] `DE_generateRings`
- [ ] `aliasDirtPixel`
- [ ] `JE_aliasDirt`
- [ ] `JE_placementPosition`
- [ ] `JE_stabilityCheck`
- [ ] `JE_tempScreenChecking`
- [ ] `JE_makeExplosion`
- [ ] `JE_eSound`
- [ ] `JE_superPixel`
- [ ] `JE_helpScreen`
- [ ] `JE_pauseScreen`
- [ ] `DE_ResetUnits`
- [ ] `DE_ResetPlayers`
- [ ] `DE_ResetWeapons`
- [ ] `DE_ResetLevel`
- [ ] `DE_ResetAI`
- [ ] `DE_ResetActions`
- [ ] `DE_RunTick`
- [ ] `DE_RunTickCycleDeadUnits`
- [ ] `DE_RunTickGravity`
- [ ] `DE_GravityDrawUnit`
- [ ] `DE_GravityLowerUnit`
- [ ] `DE_GravityFlyUnit`
- [ ] `DE_RunTickAnimate`
- [ ] `DE_RunTickDrawWalls`
- [ ] `DE_RunTickExplosions`
- [ ] `DE_TestExplosionCollision`
- [ ] `DE_DestroyUnit`
- [ ] `DE_RunTickShots`
- [ ] `DE_DrawTrails`
- [ ] `DE_RunTickAI`
- [ ] `DE_RunTickDrawCrosshairs`
- [ ] `DE_RunTickDrawHUD`
- [ ] `DE_RunTickGetInput`
- [ ] `DE_ProcessInput`
- [ ] `DE_CycleWeaponUp`
- [ ] `DE_CycleWeaponDown`
- [ ] `DE_MakeShot`
- [ ] `DE_RunMagnet`
- [ ] `DE_RaiseAngle`
- [ ] `DE_LowerAngle`
- [ ] `DE_RaisePower`
- [ ] `DE_LowerPower`
- [ ] `DE_isValidUnit`
- [ ] `DE_RunTickCheckEndgame`
- [ ] `DE_RunTickPlaySounds`
- [ ] `JE_pixCool`

### `src/editship.c`

- [ ] `JE_decryptShips`
- [ ] `JE_loadExtraShapes`

### `src/episodes.c`

- [ ] `item_data_error`
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
- [ ] `item_data_realloc`
- [x] `is_item_space` - `ItemDefinitions.IsSpace`.
- [x] `trim_item_text` - `ItemDefinitions.Trim`.
- [x] `valid_item_id_span` - `ItemDefinitions.ValidId`.
- [x] `valid_item_id` - `ItemDefinitions.ValidId`.
- [x] `item_type_from_name` - `ItemDefinitions.TypeFromName`.
- [ ] `hash_item_id`
- [ ] `find_item_slot`
- [ ] `rebuild_item_slots`
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
- [ ] `JE_updateWarning`
- [ ] `JE_outTextGlow`

### `src/game_menu.c`

- [ ] `playeritem_map`
- [ ] `JE_cashLeft`
- [ ] `draw_item_screen_load_save`
- [ ] `draw_item_screen_keyboard_config`
- [ ] `draw_item_screen_joystick_config`
- [ ] `draw_item_screen_upgrade_menu`
- [ ] `draw_item_screen_status`
- [ ] `draw_item_screen_data_cubes`
- [ ] `draw_item_screen_footer`
- [ ] `prepare_item_screen_frame`
- [ ] `draw_item_screen`
- [ ] `move_item_screen_selection`
- [ ] `adjust_joystick_config_setting`
- [ ] `change_two_player_input_device`
- [ ] `change_upgrade_weapon_power`
- [ ] `handle_item_screen_horizontal_key`
- [ ] `activate_item_screen_selection`
- [ ] `cancel_item_screen_selection`
- [ ] `show_item_screen_help`
- [ ] `handle_item_screen_key`
- [ ] `update_item_screen_frame`
- [ ] `initialize_item_screen`
- [ ] `open_quick_save_menu`
- [ ] `wait_for_item_screen_input`
- [ ] `adjust_item_screen_volume`
- [ ] `get_item_screen_mouse_selection`
- [ ] `select_item_screen_menu_option`
- [ ] `adjust_item_screen_weapon_power`
- [ ] `handle_item_screen_mouse_input`
- [ ] `handle_item_screen_input`
- [ ] `JE_itemScreen`
- [ ] `draw_ship_illustration`
- [ ] `load_cubes`
- [ ] `load_cube`
- [ ] `JE_drawItem`
- [ ] `JE_drawMenuHeader`
- [ ] `get_menu_choice_y`
- [ ] `draw_menu_choice`
- [ ] `JE_drawMenuChoices`
- [ ] `JE_updateNavScreen`
- [ ] `JE_drawLines`
- [ ] `JE_drawNavLines`
- [ ] `JE_drawDots`
- [ ] `JE_drawPlanet`
- [ ] `JE_scaleBitmap`
- [ ] `JE_initWeaponView`
- [ ] `JE_computeDots`
- [ ] `JE_partWay`
- [ ] `JE_doShipSpecs`
- [ ] `JE_drawMainMenuHelpText`
- [ ] `JE_quitRequest`
- [ ] `JE_genItemMenu`
- [ ] `JE_scaleInPicture`
- [ ] `JE_drawScore`
- [ ] `setup_next_level_menu`
- [ ] `start_current_level`
- [ ] `request_menu_quit`
- [ ] `return_to_options_menu`
- [ ] `rebind_keyboard_key`
- [ ] `handle_keyboard_config_selection`
- [ ] `assign_joystick_control`
- [ ] `handle_joystick_config_selection`
- [ ] `handle_full_game_selection`
- [ ] `handle_two_player_menu_selection`
- [ ] `handle_one_player_menu_selection`
- [ ] `handle_limited_options_selection`
- [ ] `handle_super_tyrian_menu_selection`
- [ ] `handle_upgrade_menu_selection`
- [ ] `handle_options_menu_selection`
- [ ] `handle_next_level_menu_selection`
- [ ] `handle_upgrade_submenu_selection`
- [ ] `handle_load_save_menu_selection`
- [ ] `handle_data_cube_menu_selection`
- [ ] `JE_menuFunction`
- [ ] `JE_drawShipSpecs`
- [ ] `JE_weaponSimUpdate`
- [ ] `JE_weaponViewFrame`

### `src/helptext.c`

- [x] `decrypt_string` - `EncryptedText.Decrypt`; reverse XOR fixtures.
- [x] `read_encrypted_pascal_string` - `EncryptedText.ReadPascalString`.
- [x] `skip_pascal_string` - `EncryptedText.SkipPascalString`.
- [x] `JE_helpBox` - `HelpTextRenderer.Layout`/`DrawBox`; native wrapping.
- [x] `JE_HBox` - `HelpTextRenderer.DrawHelp`; one-based archive lookup.
- [x] `JE_loadHelpText` - `HelpTextData.Load`.

### `src/joystick.c`

- [ ] `joystick_axis_threshold`
- [ ] `joystick_axis_reduce`
- [ ] `joystick_analog_angle`
- [ ] `check_assigned`
- [ ] `poll_joystick`
- [ ] `poll_joysticks`
- [ ] `push_key`
- [ ] `push_joysticks_as_keyboard`
- [ ] `init_joysticks`
- [ ] `deinit_joysticks`
- [ ] `reset_joystick_assignments`
- [ ] `load_joystick_assignments`
- [ ] `save_joystick_assignments`
- [ ] `joystick_assignments_to_string`
- [ ] `code_to_assignment`
- [ ] `assignment_to_code`
- [ ] `detect_joystick_assignment`
- [ ] `joystick_assignment_cmp`

### `src/jukebox.c`

- [ ] `jukebox`

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

- [ ] `init_audio`
- [x] `audioCallback` - `AudioMixer.Mix`; exact native PCM/channel/fractional update parity, required OPL service.
- [ ] `deinit_audio`
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

- No function definitions; tracked by the file inventory.

### `src/mainint.c`

- [ ] `JE_drawTextWindow`
- [ ] `JE_outCharGlow`
- [ ] `JE_drawPortConfigButtons`
- [ ] `JE_helpSystem`
- [ ] `saveGameNamePopup`
- [ ] `helpSystemPage`
- [ ] `weapon_upgrade_cost`
- [ ] `JE_getCost`
- [ ] `saved_game_count`
- [ ] `draw_load_screen`
- [ ] `wait_for_load_screen_input`
- [ ] `read_load_screen_input`
- [ ] `handle_load_screen_action`
- [ ] `JE_loadScreen`
- [ ] `JE_totalScore`
- [ ] `JE_getValue`
- [ ] `JE_nextEpisode`
- [ ] `JE_initPlayerData`
- [ ] `JE_sortHighScores`
- [ ] `JE_highScoreScreen`
- [ ] `JE_gammaCorrect_func`
- [ ] `JE_gammaCorrect`
- [ ] `JE_gammaCheck`
- [ ] `JE_doInGameSetup`
- [ ] `draw_ingame_setup_menu`
- [ ] `wait_for_ingame_setup_input`
- [ ] `read_ingame_setup_mouse`
- [ ] `read_ingame_setup_keyboard`
- [ ] `adjust_ingame_setup_setting`
- [ ] `apply_ingame_setup_action`
- [ ] `JE_inGameSetup`
- [ ] `JE_inGameHelp`
- [ ] `JE_highScoreCheck`
- [ ] `adjust_difficulty`
- [ ] `load_next_demo`
- [ ] `replay_demo_keys`
- [ ] `JE_SFCodes`
- [ ] `JE_sort`
- [ ] `JE_playCredits`
- [ ] `JE_endLevelAni`
- [ ] `JE_drawCube`
- [ ] `JE_handleChat`
- [ ] `JE_inGameDisplays`
- [ ] `JE_mainKeyboardInput`
- [ ] `JE_pauseGame`
- [ ] `update_player_death`
- [ ] `collect_player_movement_input`
- [ ] `update_player_linking`
- [ ] `draw_player_level_end`
- [ ] `update_player_movement`
- [ ] `change_player_weapon_mode`
- [ ] `fire_main_weapons`
- [ ] `update_super_charge_weapon`
- [ ] `fire_superbomb`
- [ ] `update_sidekick_positions`
- [ ] `fire_sidekick_weapons`
- [ ] `update_player_attacks`
- [ ] `draw_player_sidekicks`
- [ ] `draw_player_ship`
- [ ] `JE_playerMovement`
- [ ] `JE_mainGamePlayerFunctions`
- [ ] `JE_getName`
- [ ] `collect_arcade_pickup`
- [ ] `collect_armor_pickup`
- [ ] `collect_bonus_level_pickup`
- [ ] `collect_score_item`
- [ ] `destroy_linked_enemies`
- [ ] `damage_enemy_on_collision`
- [ ] `JE_playerCollide`

### `src/menus.c`

- [ ] `gameplaySelect`
- [ ] `episodeSelect`
- [ ] `difficultySelect`

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

- No function definitions; tracked by the file inventory.

### `src/network.c`

- [ ] `packet_copy`
- [ ] `packets_shift_up`
- [ ] `packets_shift_down`
- [ ] `network_prepare`
- [ ] `network_send_no_ack`
- [ ] `network_send`
- [ ] `network_acknowledge`
- [ ] `network_is_alive`
- [ ] `network_check`
- [ ] `network_update`
- [ ] `network_is_sync`
- [ ] `network_state_prepare`
- [ ] `network_state_send`
- [ ] `network_state_update`
- [ ] `network_state_is_reset`
- [ ] `network_state_reset`
- [ ] `network_connect`
- [ ] `network_tyrian_halt`
- [ ] `network_init`
- [ ] `JE_clearSpecialRequests`

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

- [ ] `JE_anyButton`
- [x] `JE_dBar3` - `PlayerCombat.DrawBar`; native HUD pixels.
- [ ] `JE_barDrawShadow`
- [ ] `JE_wipeKey`

### `src/opentyr.c`

- [ ] `str_pop_int`
- [ ] `str_pop_str`
- [ ] `getShopContentsPickerItemsCount`
- [ ] `getShopContentsPickerItem`
- [ ] `getDisplayPickerItemsCount`
- [ ] `getDisplayPickerItem`
- [ ] `getScalerPickerItemsCount`
- [ ] `getScalerPickerItem`
- [ ] `getScalingModePickerItemsCount`
- [ ] `getScalingModePickerItem`
- [ ] `draw_setup_menu`
- [ ] `wait_for_setup_input`
- [ ] `collect_setup_menu_input`
- [ ] `collect_setup_picker_input`
- [ ] `apply_setup_picker_action`
- [ ] `apply_setup_menu_activation`
- [ ] `apply_setup_menu_action`
- [ ] `setupMenu`
- [ ] `main`

### `src/opl.c`

- [ ] `operator_advance`
- [ ] `operator_advance_drums`
- [ ] `operator_output`
- [ ] `operator_off`
- [ ] `operator_sustain`
- [ ] `operator_release`
- [ ] `operator_decay`
- [ ] `operator_attack`
- [ ] `change_attackrate`

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

- [ ] `JE_paramCheck`

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

- [ ] `JE_starlib_main`
- [ ] `JE_wackyCol`
- [ ] `JE_starlib_init`
- [ ] `JE_resetValues`
- [ ] `JE_changeSetup`
- [ ] `JE_newStar`

### `src/tyrian2.c`

- [ ] `JE_starShowVGA`
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
- [ ] `prepare_next_level`
- [ ] `draw_level_hud`
- [ ] `handle_level_input`
- [ ] `draw_level_backgrounds`
- [x] `update_level_explosions` - `SceneEffects.UpdateExplosions`; native state/draw traces.
- [ ] `draw_armor_warning`
- [x] `play_level_sounds` - `LevelEvents.PlaySounds`; native channel ordering/volume/gating.
- [x] `player_shot_hits_enemy` - `EnemySystem.ShotHits`; 5,000 native hit cases.
- [x] `mark_player_shot_linked_enemies` - `EnemySystem.MarkLinked`; full native marker sweeps.
- [x] `animate_player_shot_damaged_enemies` - `EnemySystem.AnimateDamaged`; native states/effects.
- [x] `destroy_player_shot_linked_enemies` - `EnemySystem.DestroyLinked`; native death/pickup/reward traces.
- [x] `process_player_shot` - `EnemySystem.ProcessShot`; integrated native projectile/enemy/effect traces.
- [x] `draw_enemy_shots` - `EnemySystem.UpdateEnemyShots`; native motion/collision/draw traces and real combat integration.
- [ ] `draw_level_debug_and_timer`
- [ ] `update_level_network_state`
- [x] `update_level_end_state` - `LevelEvents.UpdateEndState`; all 1,024 native flag combinations.
- [ ] `record_level_demo`
- [ ] `run_level_frame`
- [ ] `initialize_level_players_and_display`
- [ ] `initialize_level_map_and_ship_state`
- [ ] `initialize_level_flags`
- [ ] `initialize_level_status_display`
- [ ] `initialize_level_combat_state`
- [ ] `JE_main`
- [x] `read_level_header_and_events` - `LevelMap.ReadHeader`; signed record layout/native parity.
- [x] `load_level_shapes` - `LevelMap.LoadShapes`; swapped IDs, ownership, null/fill semantics.
- [x] `load_level_map_layer` - `LevelMap.LoadMapLayer`; all three layer dimensions/order.
- [ ] `play_map_transition`
- [ ] `show_episode_completion`
- [ ] `show_episode_picture`
- [ ] `show_episode_warning`
- [ ] `read_episode_item_availability`
- [x] `load_selected_level_map` - `LevelMap.LoadSelected`; episode directory and shape-file lookup.
- [ ] `configure_episode_mode`
- [ ] `handle_episode_level_jump`
- [ ] `handle_episode_presentation`
- [ ] `JE_loadMap`
- [ ] `networkStartScreen`
- [ ] `draw_title_screen`
- [ ] `wait_for_title_screen_input`
- [ ] `read_title_screen_input`
- [ ] `dispatch_title_screen_action`
- [ ] `handle_title_screen_secret_codes`
- [ ] `titleScreen`
- [ ] `newGame`
- [ ] `newSuperArcadeGame`
- [ ] `newSuperTyrianGame`
- [ ] `intro_logos`
- [ ] `JE_readTextSync`
- [ ] `JE_displayText`
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
- [ ] `JE_whoa`
- [x] `JE_barX` - `EnemySystem.BeveledBar`; ordered three-tone rectangle parity.
- [x] `draw_boss_bar` - `EnemySystem.DrawBossBars`; linked minimum armor/state/HUD parity.

### `src/varz.c`

- [ ] `JE_getShipInfo`
- [ ] `JE_SGr`
- [ ] `JE_drawOptions`
- [ ] `JE_drawOptionLevel`
- [ ] `JE_tyrianHalt`
- [ ] `JE_specialComplete`
- [ ] `JE_doSpecialShot`
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

- [ ] `init_video`
- [ ] `deinit_video`
- [ ] `init_renderer`
- [ ] `deinit_renderer`
- [ ] `init_texture`
- [ ] `deinit_texture`
- [ ] `window_get_display_index`
- [ ] `window_center_in_display`
- [ ] `reinit_fullscreen`
- [ ] `video_on_win_resize`
- [ ] `toggle_fullscreen`
- [ ] `init_scaler`
- [x] `set_scaling_mode_by_name` - `VideoLayout.SetModeByName`; exact native names/failure retention.
- [ ] `JE_clr256`
- [ ] `JE_showVGA`
- [x] `calc_dst_render_rect` - `VideoLayout.Calculate`; exact native center/integer/aspect outputs.
- [ ] `scale_and_flip`
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

- [ ] `interp1`
- [ ] `interp2`
- [ ] `interp3`
- [ ] `interp4`
- [ ] `interp5`
- [ ] `interp6`
- [ ] `interp7`
- [ ] `interp8`
- [ ] `interp9`
- [ ] `interp10`
- [ ] `diff`
- [ ] `hq2x_32`
- [ ] `hq3x_32`
- [ ] `hq4x_32`

### `src/xmas.c`

- [ ] `xmas_time`
- [ ] `xmas_prompt`

### Inline function definitions in headers

#### `src/config_file.h`

- [ ] `config_string_to_cstr`
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
