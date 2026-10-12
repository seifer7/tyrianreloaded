using OpenTyrian.Core;
using OpenTyrian.Core.Tests;

var failures = new List<string>();
Run("default seed", TestDefaultSeed);
Run("published seed vectors", TestPublishedVectors);
Run("seed truncation and repeatability", TestSeedTruncationAndRepeatability);
Run("state wraparound", TestStateWraparound);
Run("float bounds", TestFloatBounds);
Run("size buffer read and write", TestSizeBufferReadAndWrite);
Run("size buffer bounds and recovery", TestSizeBufferBoundsAndRecovery);
Run("size buffer copy failures", TestSizeBufferCopyFailures);
Run("size buffer seek modes", TestSizeBufferSeekModes);
Run("argument parser compatibility", ArgumentParserTests.Run);
Run("game launch options, legacy switches, and network argument validation", () => GameOptionsTests.Run());
Run("joystick assignment codes, analog controls, repeat timing, and configuration", () => JoystickTests.Run());
Run("gameplay, episode, and difficulty menu interaction and presentation", () => SelectionMenuTests.Run());
Run("shadowed HUD gauges and any-button input polling", () => UiControlTests.Run());
Run("legacy save encryption, item layouts, and binary slot archives", () => LegacySaveTests.Run());
Run("processor detail settings and native game-speed timing", () => RuntimeSettingsTests.Run());
Run("game text configuration defaults, keyboard bindings, and persistence", () => GameConfigurationTests.Run());
Run("complete legacy configuration startup and save persistence", () => SettingsStoreTests.Run());
Run("versioned save metadata, native paths, sorting, and player filters", () => SaveMetadataTests.Run());
Run("versioned save item payloads, state restoration, and persistence", () => VersionedSaveTests.Run());
Run("animation run/skip/copy decoding", AnimationDecoderTests.Run);
Run("animation playback frames, palette, input interruption, and PIT timing", () => AnimationPlayerTests.Run());
Run("jukebox star patterns, depth rendering, keyboard controls, and seeded RNG", () => JukeboxStarfieldTests.Run());
Run("jukebox music/sample controls, fades, presentation, and quit flows", () => JukeboxTests.Run());
Run("HQ2x/HQ3x/HQ4x patterns, YUV thresholds, interpolation and pitch", () => HqVideoScalerTests.Run());
Run("network control queues, retry timing, delayed states and XOR recovery", () => NetworkSessionTests.Run());
Run("gameplay network payloads, delayed requests, NortShip and desync diagnostics", () => LevelNetworkTests.Run());
Run("pause controls, modifier filtering, volumes and network acknowledgment", () => GamePauseTests.Run());
Run("player death explosions, arcade respawn, lives and continuous death", () => PlayerLifecycleTests.Run());
Run("player linking, inertia, warp trails, turret shots and sidekick positions", () => PlayerMovementTests.Run());
Run("special weapons, shield/armor costs, repulsion, flares and HUD", () => PlayerSpecialWeaponsTests.Run());
Run("player keyboard, mouse, joystick, demo and network input collection", () => PlayerInputTests.Run());
Run("demo selection, original/compact headers, item IDs and recording bytes", () => DemoSessionTests.Run());
Run("enemy collision damage, linked destruction and shared armor pickups", () => EnemyCollisionTests.Run());
Run("binary file format compatibility", BinaryFileTests.Run);
Run("configuration model and text format", ConfigurationTests.Run);
Run("encrypted text and animation page loading", AssetReaderTests.Run);
Run("indexed surface primitives", IndexedSurfaceTests.Run);
Run("palette expansion and fades", PaletteTests.Run);
Run("PCX and Tyrian picture loading", PictureLoaderTests.Run);
Run("nearest and edge-aware video scalers", VideoScalerTests.Run);
Run("player upgrades and lives", PlayerRulesTests.Run);
Run("sprite tables, effects, and compressed sheets", SpriteTests.Run);
Run("font metrics, alignment, and shadows", FontTests.Run);
Run("item extraction, CLI, and data-directory lookup", () => ExtractorTests.Run());
Run("level archive and episode selection", LevelTests.Run);
Run("binary level headers, events, shapes, and tile maps", () => LevelMapTests.Run());
Run("encrypted help archive groups", HelpTextTests.Run);
Run("help wrapping and state reset", () => HelpTextRendererTests.Run());
Run("PIT delays, input waits, and volume bounds", DelayTests.Run);
Run("item definitions, validation, and mod ordering", () => ItemDefinitionTests.Run());
Run("typed items, reference resolution, and random shops", () => ItemDatabaseTests.Run());
Run("player projectile creation, movement, and previews", () => PlayerShotTests.Run());
Run("explosions and superpixel effects", () => SceneEffectsTests.Run());
Run("player damage, shield, and armor HUD", () => PlayerCombatTests.Run());
Run("enemy creation, scaling, and projectile targeting", () => EnemySystemTests.Run());
Run("enemy attacks, launches, and hostile projectiles", () => EnemyAttackTests.Run());
Run("linked boss health bars and timed HUD placement", () => EnemyHudTests.Run());
Run("level enemy events, jumps, and scripted controls", () => LevelEventTests.Run());
Run("complete level event dispatch and host actions", () => LevelEventDispatchTests.Run());
Run("level completion and queued sound dispatch", () => LevelStateTests.Run());
Run("sound banks, voice trimming, conversion, and playback routing", () => SoundBankTests.Run());
Run("eight-channel audio mixing and music update scheduling", () => AudioMixerTests.Run());
Run("music archive loading and synchronized song controls", () => MusicPlayerTests.Run());
Run("LDS music sequencing, instrument effects, and OPL register routing", () => LdsPlayerTests.Run());
Run("keyboard/mouse events, waits, text, and video coordinate mapping", () => InputTests.Run());
Run("mouse cursor capture, clipping, filtering, and pixel restoration", () => MouseCursorTests.Run());
Run("background tiles, filters, scrolling, and starfield", () => BackgroundTests.Run());
for (var index = 0; index < args.Length; index += 2)
{
    if (args[index] == "--native-sdl-net" && index + 1 < args.Length)
    {
        Run("real SDL2_net UDP bytes, channels, lifecycle and managed state integration", () => SdlNetTests.Run(args[index + 1]));
        continue;
    }
    if (args[index] == "--native-network" && index + 1 < args.Length)
    {
        Run("native network packet/state/XOR and sequence-wrap compatibility", () => NetworkSessionTests.Run(args[index + 1]));
        continue;
    }
    if (args[index] == "--native-level-network" && index + 1 < args.Length)
    {
        Run("native gameplay network state and request compatibility", () => LevelNetworkTests.Run(args[index + 1]));
        continue;
    }
    if (args[index] == "--native-pause" && index + 1 < args.Length)
    {
        Run("native game pause loop compatibility", () => GamePauseTests.Run(args[index + 1]));
        continue;
    }
    if (args[index] == "--native-player-lifecycle" && index + 1 < args.Length)
    {
        Run("native player death and respawn compatibility", () => PlayerLifecycleTests.Run(args[index + 1]));
        continue;
    }
    if (args[index] == "--native-player-movement" && index + 1 < args.Length)
    {
        Run("native player movement, linking, trails and sidekick compatibility", () => PlayerMovementTests.Run(args[index + 1]));
        continue;
    }
    if (args[index] == "--native-player-special" && index + 1 < args.Length)
    {
        Run("native special weapon effects, costs, flares and HUD compatibility", () => PlayerSpecialWeaponsTests.Run(args[index + 1]));
        continue;
    }
    if (args[index] == "--native-player-input" && index + 1 < args.Length)
    {
        Run("native player input collection, demo bytes and network payload compatibility", () => PlayerInputTests.Run(args[index + 1]));
        continue;
    }
    if (args[index] == "--native-demo" && index + 1 < args.Length)
    {
        Run("native demo selection, header loading and recording compatibility", () => DemoSessionTests.Run(args[index + 1]));
        continue;
    }
    if (args[index] == "--native-enemy-collision" && index + 1 < args.Length)
    {
        Run("native player/enemy collision damage, linked destruction and armor compatibility", () => EnemyCollisionTests.Run(args[index + 1]));
        continue;
    }
    if (args[index] == "--native-hq" && index + 1 < args.Length)
    {
        Run("native exact HQ2x/HQ3x/HQ4x output compatibility", () => HqVideoScalerTests.Run(args[index + 1]));
        continue;
    }
    if (args[index] == "--native-jukebox" && index + 1 < args.Length)
    {
        Run("native complete jukebox music, input and presentation flow compatibility", () => JukeboxTests.Run(args[index + 1]));
        continue;
    }
    if (args[index] == "--native-jukebox-stars" && index + 1 < args.Length)
    {
        Run("native jukebox star patterns, pixels, state and RNG compatibility", () => JukeboxStarfieldTests.Run(args[index + 1]));
        continue;
    }
    if (args[index] == "--native-animation" && index + 1 < args.Length)
    {
        Run("native animation playback frames and ordered host compatibility", () => AnimationPlayerTests.Run(args[index + 1]));
        continue;
    }
    if (index + 1 == args.Length || (args[index] != "--native-reference" && args[index] != "--native-extractor" && args[index] != "--native-background" && args[index] != "--native-items" && args[index] != "--native-help" && args[index] != "--native-shots" && args[index] != "--native-effects" && args[index] != "--native-combat" && args[index] != "--native-enemies" && args[index] != "--native-attacks" && args[index] != "--native-events" && args[index] != "--native-dispatch" && args[index] != "--native-level-map" && args[index] != "--native-boss-bars" && args[index] != "--native-level-state" && args[index] != "--native-sounds" && args[index] != "--native-mixer" && args[index] != "--native-music" && args[index] != "--native-lds" && args[index] != "--native-input" && args[index] != "--native-mouse" && args[index] != "--native-sdl-audio" && args[index] != "--native-game-options" && args[index] != "--native-sdl-video" && args[index] != "--native-sdl-input" && args[index] != "--native-joystick" && args[index] != "--native-selection-menus" && args[index] != "--native-ui-controls" && args[index] != "--native-legacy-saves" && args[index] != "--native-runtime-settings" && args[index] != "--native-game-config" && args[index] != "--native-settings-store" && args[index] != "--native-save-metadata" && args[index] != "--native-versioned-saves"))
    {
        Console.Error.WriteLine("Usage: OpenTyrian.Core.Tests [--native-reference PATH] [--native-extractor PATH] [--native-background PATH] [--native-items PATH] [--native-help PATH] [--native-shots PATH] [--native-effects PATH] [--native-combat PATH] [--native-enemies PATH] [--native-attacks PATH] [--native-events PATH] [--native-dispatch PATH] [--native-level-map PATH] [--native-boss-bars PATH] [--native-level-state PATH] [--native-sounds PATH] [--native-mixer PATH] [--native-music PATH]         [--native-lds PATH]         [--native-input PATH]         [--native-mouse PATH]         [--native-sdl-audio PATH]         [--native-game-options PATH]         [--native-sdl-video PATH]         [--native-sdl-input PATH] [--native-joystick PATH] [--native-selection-menus PATH] [--native-ui-controls PATH] [--native-legacy-saves PATH] [--native-runtime-settings PATH] [--native-game-config PATH] [--native-settings-store PATH] [--native-save-metadata PATH] [--native-versioned-saves PATH]");
        Console.Error.WriteLine("Additional references: [--native-animation PATH] [--native-jukebox-stars PATH] [--native-jukebox PATH] [--native-hq PATH] [--native-network PATH] [--native-sdl-net PATH] [--native-level-network PATH] [--native-pause PATH] [--native-player-lifecycle PATH] [--native-player-movement PATH] [--native-player-special PATH] [--native-player-input PATH] [--native-demo PATH] [--native-enemy-collision PATH]");
        return 1;
    }

    var executable = args[index + 1];
    if (args[index] == "--native-reference")
    {
        Run("native C differential compatibility", () => NativeCompatibilityTests.Run(executable));
    }
    else if (args[index] == "--native-extractor")
    {
        Run("native extractor byte/CLI compatibility", () => ExtractorTests.Run(executable));
    }
    else if (args[index] == "--native-background")
    {
        Run("native background pixel compatibility", () => BackgroundTests.Run(executable));
    }
    else if (args[index] == "--native-help")
    {
        Run("native help wrapping compatibility", () => HelpTextRendererTests.Run(executable));
    }
    else if (args[index] == "--native-shots")
    {
        Run("native player projectile compatibility", () => PlayerShotTests.Run(executable));
    }
    else if (args[index] == "--native-effects")
    {
        Run("native explosions and superpixel compatibility", () => SceneEffectsTests.Run(executable));
    }
    else if (args[index] == "--native-combat")
    {
        Run("native player combat and HUD compatibility", () => PlayerCombatTests.Run(executable));
    }
    else if (args[index] == "--native-enemies")
    {
        Run("native enemy initialization compatibility", () => EnemySystemTests.Run(executable));
    }
    else if (args[index] == "--native-attacks")
    {
        Run("native enemy attacks and hostile projectile compatibility", () => EnemyAttackTests.Run(executable));
    }
    else if (args[index] == "--native-events")
    {
        Run("native level enemy event compatibility", () => LevelEventTests.Run(executable));
    }
    else if (args[index] == "--native-dispatch")
    {
        Run("native complete level event dispatch compatibility", () => LevelEventDispatchTests.Run(executable));
    }
    else if (args[index] == "--native-level-map")
    {
        Run("native binary level map loading compatibility", () => LevelMapTests.Run(executable));
    }
    else if (args[index] == "--native-boss-bars")
    {
        Run("native boss health bar compatibility", () => EnemyHudTests.Run(executable));
    }
    else if (args[index] == "--native-level-state")
    {
        Run("native level completion and sound compatibility", () => LevelStateTests.Run(executable));
    }
    else if (args[index] == "--native-sounds")
    {
        Run("native sound-bank reading and playback compatibility", () => SoundBankTests.Run(executable));
    }
    else if (args[index] == "--native-mixer")
    {
        Run("native audio mixing and scheduling compatibility", () => AudioMixerTests.Run(executable));
    }
    else if (args[index] == "--native-music")
    {
        Run("native music directory and control compatibility", () => MusicPlayerTests.Run(executable));
    }
    else if (args[index] == "--native-lds")
    {
        Run("native LDS sequencing and OPL register compatibility", () => LdsPlayerTests.Run(executable));
    }
    else if (args[index] == "--native-input")
    {
        Run("native input events, waits, text, and viewport compatibility", () => InputTests.Run(executable));
    }
    else if (args[index] == "--native-mouse")
    {
        Run("native mouse cursor blitting and restoration compatibility", () => MouseCursorTests.Run(executable));
    }
    else if (args[index] == "--native-sdl-audio")
    {
        Run("real SDL sample conversion and sound bank/mixer integration", () => SdlAudioTests.Run(executable));
    }
    else if (args[index] == "--native-game-options")
    {
        Run("native game launch options and diagnostic compatibility", () => GameOptionsTests.Run(executable));
    }
    else if (args[index] == "--native-sdl-video")
    {
        Run("real SDL window, texture, indexed frame, and fullscreen compatibility", () => SdlVideoTests.Run(executable));
    }
    else if (args[index] == "--native-sdl-input")
    {
        Run("real SDL event queue ABI and managed input/video integration", () => SdlInputTests.Run(executable));
    }
    else if (args[index] == "--native-joystick")
    {
        Run("native joystick controls, repeat, assignment, and configuration compatibility", () => JoystickTests.Run(executable));
    }
    else if (args[index] == "--native-selection-menus")
    {
        Run("native selection menu interaction, state, and presentation compatibility", () => SelectionMenuTests.Run(executable));
    }
    else if (args[index] == "--native-ui-controls")
    {
        Run("native shadowed HUD gauge and any-button input compatibility", () => UiControlTests.Run(executable));
    }
    else if (args[index] == "--native-legacy-saves")
    {
        Run("native legacy save cipher, archive, and item layout compatibility", () => LegacySaveTests.Run(executable));
    }
    else if (args[index] == "--native-runtime-settings")
    {
        Run("native processor settings and game-speed/PIT compatibility", () => RuntimeSettingsTests.Run(executable));
    }
    else if (args[index] == "--native-game-config")
    {
        Run("native game configuration text/state and SDL keyboard-name compatibility", () => GameConfigurationTests.Run(executable));
    }
    else if (args[index] == "--native-settings-store")
    {
        Run("native complete legacy configuration startup and save compatibility", () => SettingsStoreTests.Run(executable));
    }
    else if (args[index] == "--native-save-metadata")
    {
        Run("native save metadata, tinydir order, and player filter compatibility", () => SaveMetadataTests.Run(executable));
    }
    else if (args[index] == "--native-versioned-saves")
    {
        Run("native versioned save bytes and full restored-state compatibility", () => VersionedSaveTests.Run(executable));
    }
    else
    {
        Run("native item parser compatibility", () => ItemDefinitionTests.Run(executable));
        Run("native typed item and shop compatibility", () => ItemDatabaseTests.Run(executable));
    }
}

if (failures.Count == 0)
{
    Console.WriteLine("All core compatibility tests passed.");
    return 0;
}

foreach (var failure in failures)
{
    Console.Error.WriteLine(failure);
}

return 1;

void Run(string name, Action test)
{
    try
    {
        test();
        Console.WriteLine($"PASS {name}");
    }
    catch (Exception exception)
    {
        failures.Add($"FAIL {name}: {exception.Message}");
    }
}

static void TestDefaultSeed()
{
    AssertSequence(
        [3499211612u, 581869302u, 3890346734u, 3586334585u, 545404204u],
        MersenneTwister.NextUInt32);
}

static void TestPublishedVectors()
{
    MersenneTwister.Seed(0);
    AssertSequence(
        [2357136044u, 2546248239u, 3071714933u, 3626093760u, 2588848963u],
        MersenneTwister.NextUInt32);

    MersenneTwister.Seed(1);
    AssertSequence(
        [1791095845u, 4282876139u, 3093770124u, 4005303368u, 491263u],
        MersenneTwister.NextUInt32);

    MersenneTwister.Seed(5489);
    AssertSequence(
        [3499211612u, 581869302u, 3890346734u, 3586334585u, 545404204u],
        MersenneTwister.NextUInt32);
}

static void TestSeedTruncationAndRepeatability()
{
    MersenneTwister.Seed(0);
    var expected = MersenneTwister.NextUInt32();

    MersenneTwister.Seed(0x1_0000_0000);
    AssertEqual(expected, MersenneTwister.NextUInt32());

    MersenneTwister.Seed(uint.MaxValue);
    var first = MersenneTwister.NextUInt32();
    MersenneTwister.Seed(uint.MaxValue);
    AssertEqual(first, MersenneTwister.NextUInt32());
}

static void TestStateWraparound()
{
    MersenneTwister.Seed(5489);
    for (var i = 0; i < 999; i++)
    {
        _ = MersenneTwister.NextUInt32();
    }

    AssertSequence(
        [1341017984u, 2500741117u, 4263797064u, 2322457777u, 1155622524u],
        MersenneTwister.NextUInt32);
}

static void TestFloatBounds()
{
    MersenneTwister.Seed(5489);
    for (var i = 0; i < 10_000; i++)
    {
        var inclusive = MersenneTwister.NextFloatInclusive();
        var exclusive = MersenneTwister.NextFloatExclusive();

        if (inclusive is < 0.0f or > 1.0f)
        {
            throw new InvalidOperationException($"Inclusive float out of range: {inclusive}.");
        }

        // Like the C float expression, rounding can produce 1.0f.
        if (exclusive is < 0.0f or > 1.0f)
        {
            throw new InvalidOperationException($"Exclusive float out of range: {exclusive}.");
        }
    }
}

static void TestSizeBufferReadAndWrite()
{
    var outputBytes = new byte[8];
    var output = new SizeBuffer(outputBytes, 6);
    output.Fill(0x123, 2);
    AssertEqual(2u, output.Position);
    AssertEqual(0x23u, outputBytes[0]);
    AssertEqual(0x23u, outputBytes[1]);
    output.Seek(0, SeekOrigin.Begin);
    AssertEqual(0x23u, output.ReadByte());

    var inputBytes = new byte[] { 0x12, 0x34, 0x56, 0x78 };
    var input = new SizeBuffer(inputBytes, 4);
    AssertEqual(0x12u, input.ReadByte());
    AssertEqual(0x5634u, input.ReadWord());
    AssertEqual(3u, input.Position);

    output.Seek(0, SeekOrigin.Begin);
    input.Seek(1, SeekOrigin.Begin);
    output.CopyFrom(input, 1);
    AssertEqual(0x34u, outputBytes[0]);
    AssertEqual(2u, input.Position);
    AssertEqual(1u, output.Position);
    AssertEqual(false, output.Error);
    AssertEqual(false, input.Error);
}

static void TestSizeBufferBoundsAndRecovery()
{
    var bytes = new byte[] { 0xA5, 0x5A };
    var buffer = new SizeBuffer(bytes, 2);
    buffer.Seek(1, SeekOrigin.Begin);
    AssertEqual(0u, buffer.ReadWord());
    AssertEqual(true, buffer.Error);
    AssertEqual(1u, buffer.Position);
    AssertEqual(0u, buffer.ReadByte());
    AssertEqual(1u, buffer.Position);

    buffer.Seek(1, SeekOrigin.Begin);
    AssertEqual(false, buffer.Error);
    AssertEqual(0x5Au, buffer.ReadByte());
    AssertEqual(false, buffer.Error);
    AssertEqual(0u, buffer.ReadByte());
    AssertEqual(true, buffer.Error);
    AssertEqual(2u, buffer.Position);
}

static void TestSizeBufferCopyFailures()
{
    var destinationBytes = new byte[1];
    var sourceBytes = new byte[] { 0x11, 0x22 };
    var destination = new SizeBuffer(destinationBytes, 1);
    var source = new SizeBuffer(sourceBytes, 2);
    destination.CopyFrom(source, 2);
    AssertEqual(true, destination.Error);
    AssertEqual(false, source.Error);
    AssertEqual(0u, source.Position);
    AssertEqual(0u, destination.Position);

    destination.Seek(0, SeekOrigin.Begin);
    source.Seek(2, SeekOrigin.Begin);
    destination.CopyFrom(source, 1);
    AssertEqual(true, source.Error);
    AssertEqual(false, destination.Error);
    AssertEqual(0u, destination.Position);
}

static void TestSizeBufferSeekModes()
{
    var buffer = new SizeBuffer(new byte[8], 8);
    buffer.Seek(5, SeekOrigin.Begin);
    buffer.Seek(-2, SeekOrigin.Current);
    AssertEqual(3u, buffer.Position);

    buffer.Seek(2, SeekOrigin.End);
    AssertEqual(6u, buffer.Position);

    buffer.Seek(-1, SeekOrigin.Begin);
    AssertEqual(true, buffer.Error);
    buffer.Seek(0, SeekOrigin.Begin);
    AssertEqual(false, buffer.Error);
    AssertEqual(0u, buffer.Position);

    var invalidBuffer = new SizeBuffer(new byte[8], 8);
    AssertThrows<ArgumentOutOfRangeException>(() => invalidBuffer.Seek(0, (SeekOrigin)99));
}

static void AssertSequence(uint[] expected, Func<uint> next)
{
    foreach (var value in expected)
    {
        AssertEqual(value, next());
    }
}

static void AssertEqual<T>(T expected, T actual)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"Expected {expected}, got {actual}.");
    }
}

static void AssertThrows<TException>(Action action) where TException : Exception
{
    try
    {
        action();
    }
    catch (TException)
    {
        return;
    }

    throw new InvalidOperationException($"Expected {typeof(TException).Name}.");
}
