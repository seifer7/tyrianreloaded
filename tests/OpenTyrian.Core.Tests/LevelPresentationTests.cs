using System.Diagnostics;
using System.Text;
using OpenTyrian.Core;

namespace OpenTyrian.Core.Tests;

internal static class LevelPresentationTests
{
    public static void Run(string? executable = null)
    {
        var trace = new StringBuilder();
        var gameData = new byte[80000]; var primaryData = new byte[80000];
        for (var mode = 0; mode < 2048; mode++)
        {
            var gamePitch = mode % 3 == 0 ? 320 : mode % 3 == 1 ? 327 : 341;
            var primaryPitch = mode % 2 == 0 ? 320 : 333;
            for (var i = 0; i < 80000; i++)
            { gameData[i] = unchecked((byte)(i * 71 + i / 317 + mode * 31)); primaryData[i] = unchecked((byte)(i * 17 + mode * 23)); }
            var game = new IndexedSurface(320, 200, gamePitch, gameData, TextWriter.Null);
            var primary = new IndexedSurface(320, 200, primaryPitch, (mode & 1024) != 0 ? gameData : primaryData, TextWriter.Null);
            var fixture = new Fixture(primary, trace, (byte)(mode % 6), (byte)(mode % 7));
            fixture.Game.Players[0].X = mode % 401 - 50; fixture.Game.Players[0].Y = mode % 251 - 30;
            fixture.Keyboard.SkipPresentation = (mode & 2) != 0; fixture.Setup.QuitRequested = (mode & 4) != 0;
            var presentation = new LevelPresentation(game, primary, fixture.Game.Players[0], fixture.Settings,
                fixture.Keyboard, fixture.Setup, fixture.Timer, () => trace.AppendLine("present"))
            { PlayerEndLevel = (mode & 1) != 0, SpecialCode = (byte)(mode / 16 % 4) };
            trace.AppendLine($"case:{mode}"); presentation.Show();
            trace.AppendLine($"state:{(fixture.Setup.QuitRequested ? 1 : 0)},{(fixture.Keyboard.SkipPresentation ? 1 : 0)}," +
                $"{Hash(primary.Pixels.Span)},{Hash(game.Pixels.Span)},{fixture.Platform.Ticks},{fixture.Timer.GetDelayTicks()}");
        }
        TestFailures();
        if (executable is null) return;
        using var process = Process.Start(new ProcessStartInfo(executable) { RedirectStandardOutput = true, RedirectStandardError = true })
            ?? throw new InvalidOperationException("Cannot start native level presentation reference.");
        var output = process.StandardOutput.ReadToEndAsync(); var errors = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(30000)) { process.Kill(entireProcessTree: true); throw new TimeoutException("Level presentation reference timed out."); }
        var expected = output.GetAwaiter().GetResult().Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var actual = trace.ToString().Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        if (process.ExitCode != 0 || !expected.SequenceEqual(actual))
        {
            var index = Enumerable.Range(0, Math.Min(expected.Length, actual.Length)).FirstOrDefault(i => expected[i] != actual[i], -1);
            throw new InvalidOperationException($"Level presentation differs at {index}: {expected.ElementAtOrDefault(index)} / " +
                $"{actual.ElementAtOrDefault(index)}; {errors.GetAwaiter().GetResult()}");
        }
    }

    internal static void RunSdl(SdlVideoDevice video)
    {
        var source = new IndexedSurface(320, 200, 327, TextWriter.Null);
        for (var i = 0; i < source.Pixels.Length; i++) source.Pixels.Span[i] = (byte)(i % 255);
        var fixture = new Fixture(video.Primary, new(), 2, 3);
        var presents = 0;
        var presentation = new LevelPresentation(source, video.Primary, fixture.Game.Players[0], fixture.Settings,
            fixture.Keyboard, fixture.Setup, fixture.Timer, () => { presents++; video.Present(); });
        presentation.Show();
        for (var row = 0; row < 184; row++)
            if (!source.Pixels.Span.Slice(row * source.Pitch + 24, 264).SequenceEqual(video.Primary.Pixels.Span.Slice(row * video.Primary.Pitch, 264)))
                throw new InvalidOperationException("SDL level viewport copy differs.");
        fixture.Keyboard.SkipPresentation = true; fixture.Setup.QuitRequested = true; presentation.Show();
        if (presents != 1 || fixture.Keyboard.SkipPresentation || fixture.Setup.QuitRequested)
            throw new InvalidOperationException("SDL level presentation did not suppress help/clear shared flags.");
    }

    private static uint Hash(ReadOnlySpan<byte> bytes)
    {
        var hash = 2166136261u;
        foreach (var value in bytes) hash = unchecked((hash ^ value) * 16777619u);
        return hash;
    }

    private static void TestFailures()
    {
        var surface = new IndexedSurface(320, 200, 320, TextWriter.Null);
        var fixture = new Fixture(surface, new(), 0, 2);
        fixture.Keyboard.SkipPresentation = false; fixture.Setup.QuitRequested = true;
        var presentation = new LevelPresentation(new(287, 183, 287, TextWriter.Null), surface,
            fixture.Game.Players[0], fixture.Settings, fixture.Keyboard, fixture.Setup, fixture.Timer, () => throw new InvalidOperationException("Unexpected present."));
        try { presentation.Show(); throw new InvalidOperationException("Short viewport accepted."); }
        catch (InvalidOperationException error) when (error.Message.StartsWith("Level presentation requires", StringComparison.Ordinal)) { }
        if (!fixture.Setup.QuitRequested || fixture.Platform.Ticks != 0) throw new InvalidOperationException("Invalid viewport cleared state or waited.");
        presentation.PlayerEndLevel = true; presentation.Show();
        if (fixture.Setup.QuitRequested) throw new InvalidOperationException("Skipped invalid viewport did not clear quit.");
        var failed = new LevelPresentation(surface, surface, fixture.Game.Players[0], fixture.Settings,
            fixture.Keyboard, fixture.Setup, fixture.Timer, () => throw new IOException("present failed"));
        fixture.Setup.QuitRequested = true;
        try { failed.Show(); throw new InvalidOperationException("Presentation failure hidden."); }
        catch (IOException error) when (error.Message == "present failed") { }
        if (!fixture.Setup.QuitRequested) throw new InvalidOperationException("Failed present fabricated successful frame cleanup.");
    }

    internal sealed class Fixture
    {
        public LegacyGameState Game { get; } = new();
        public GameRuntimeSettings Settings { get; }
        public GameKeyboard Keyboard { get; }
        public GameSetupSession Setup { get; }
        public DelayTimer Timer { get; }
        public Platform Platform { get; }
        public ItemDatabase Items { get; }
        public LevelEvents Level { get; }
        public PlayerCombat Combat { get; }
        public SceneEffects Effects { get; }
        public BackgroundRenderer Backgrounds { get; }
        public PlayerPickups Pickups { get; }

        public Fixture(IndexedSurface surface, StringBuilder trace, byte fastPlay, byte processor, int itemVariant = 0)
        {
            var definitions = new ItemDefinitions();
            definitions.ParseBundle(Encoding.Latin1.GetBytes(ItemDatabaseTests.Fixture(itemVariant)), "level-presentation.tyrr");
            var items = ItemDatabase.FromDefinitions(definitions);
            Items = items;
            Platform = new(trace); var input = new InputState(Platform, new()); Timer = new(input);
            Settings = new() { FastPlay = fastPlay, ProcessorType = processor }; Settings.SetGameSpeed(Timer);
            Timer.SetSpeed(65535); Timer.SetDelay(1);
            var effects = new SceneEffects(new byte[8]); var combat = new PlayerCombat(Game.Players, effects, surface);
            var backgrounds = new BackgroundRenderer(); var requests = new NetworkRequests();
            var level = new LevelEvents(items, new(items, TextWriter.Null), Game.Players, backgrounds, effects);
            Level = level; Combat = combat; Effects = effects; Backgrounds = backgrounds;
            var shapes = new MainShapeTables(); var font = new FontRenderer(shapes);
            var shots = new PlayerShots(items.Shots, items.Weapons, Game.Players,
                new SpritePlayerShotEffects(surface, new(), new(), new(), (_, _, _) => { }, (_, _, _, _, _) => { }));
            Pickups = new(Game.Players, new(items, TextWriter.Null), combat, shots, items, level, effects, [], []);
            var graphics = new ushort[2];
            var window = new GameTextWindow(() => { }, _ => { });
            var keyboardHost = new GameKeyboardHost(new(input, new PaletteSystem(_ => 0)), new PaletteColor[256], combat, items,
                new(Game.Players, items, new(), new(), new byte[150], graphics),
                new(new GameHelpTests.TraceHost(new(), 0), window), "Continue"u8.ToArray(), window, "Cheat"u8.ToArray(),
                Settings, Timer, input, () => { }, _ => { });
            Keyboard = new(Game, items, input, combat, level, backgrounds, requests, Settings, shots, graphics, new(), keyboardHost);
            var mixer = new AudioMixer(44100, new SilentMusic());
            var cursor = new MouseCursor(input, surface, new SpriteSheet()) { HasMouse = false };
            var dos = new DosConfiguration();
            var setupHost = new InGameSetupHost(surface, new(surface.Width, surface.Height, surface.Pitch, TextWriter.Null),
                font, input, cursor, new(), mixer, Settings, dos, Timer, () => { }, () => { }, () => { }, () => { }, _ => { }, () => { });
            Setup = new(input, new(Settings, dos, new(), setupHost), level, requests, () => { }, () => { });
        }
    }

    internal sealed class Platform(StringBuilder trace) : IInputPlatform
    {
        public uint Ticks { get; private set; }
        public void Delay(uint milliseconds) { trace.AppendLine($"wait:{milliseconds}"); Ticks += milliseconds; }
        public bool PollEvent(out InputEvent value) { value = default; return false; }
        public void PollJoysticks() { }
        public void PushJoysticksAsKeyboard() { }
        public bool JoystickDown => false;
        public bool IsNetworkGame => false;
        public void SetCursorVisible(bool visible) { }
        public void SetRelativeMouseMode(bool enabled) { }
        public void EnableSystemMouseScaling() { }
        public void ToggleFullscreen() { }
        public void WindowResized() { }
        public void CheckNetwork() { }
        public void Quit() => throw new InvalidOperationException("Unexpected quit.");
    }

    private sealed class SilentMusic : IMusicSynthesizer
    {
        public void Render(Span<short> samples) => samples.Clear();
        public void Update() { }
    }
}
