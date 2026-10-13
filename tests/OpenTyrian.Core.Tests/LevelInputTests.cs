using System.Diagnostics;
using System.Text;
using OpenTyrian.Core;

namespace OpenTyrian.Core.Tests;

internal static class LevelInputTests
{
    public static void Run(string? executable = null)
    {
        var definitions = new ItemDefinitions();
        definitions.ParseBundle(Encoding.Latin1.GetBytes(LevelEventTests.Fixture()), "level-input.tyrr");
        var items = ItemDatabase.FromDefinitions(definitions);
        var trace = new StringBuilder();
        var players = new[] { new PlayerState(0), new PlayerState(1) };
        var surface = new IndexedSurface(320, 200, 327, TextWriter.Null);
        var effects = new SceneEffects(new byte[8]); var combat = new PlayerCombat(players, effects, surface);
        var level = new LevelEvents(items, new(items, TextWriter.Null), players, new(), effects);
        var shots = new PlayerShots(items.Shots, items.Weapons, players,
            new SpritePlayerShotEffects(surface, new(), new(), new(), (_, _, _) => { }, (_, _, _, _, _) => { }));
        var movement = new PlayerMovement(players, combat, level, shots, items);
        for (var mode = 0; mode < 32768; mode++)
        {
            var platform = new Platform(mode); var input = new InputState(platform, new());
            platform.Input = input;
            platform.Events.Enqueue(new((mode & 512) == 0 ? InputEventKind.FocusGained : InputEventKind.FocusLost));
            input.ServiceEvents(false); platform.Active = true;
            input.NewKey = mode % 19 == 0; input.NewMouse = mode % 23 == 0;
            input.MousePressed[0] = mode % 29 == 0; input.MousePressed[1] = mode % 31 == 0; input.MousePressed[2] = mode % 37 == 0;
            var requests = new NetworkRequests
            {
                PausePressed = mode % 43 == 0,
                InGameMenuPressed = mode % 47 == 0,
                Pause = mode % 53 == 0,
                InGameMenu = mode % 59 == 0,
                YourInGameMenu = mode % 61 == 0
            };
            players[0].ExplodingTicks = mode % 5 == 0 ? 17u : 0u; players[1].ExplodingTicks = mode % 7 == 0 ? 19u : 0u;
            combat.ConstantDie = (mode & 2) != 0; combat.Galaga = (mode & 8) != 0;
            combat.MusicVolume = unchecked((ushort)(mode * 71));
            level.AllPlayersGone = (mode & 4) != 0; level.FirstGameOver = (mode & 128) != 0;
            level.MusicFade = mode % 11 == 0; level.ReallyEndLevel = mode % 13 == 0;
            level.EffectsVolume = unchecked((ushort)(mode * 97));
            var playerInput = new PlayerInput(input, new([], () => 0, () => { }, _ => { }), [], [], movement,
                combat, level, requests)
            { ConstantPlay = (mode & 1) != 0, PlayDemo = (mode & 16) != 0, NetworkGame = (mode & 256) != 0 };
            var host = new Host(trace, requests, mode) { SkipPresentation = mode % 41 == 0 };
            var dispatcher = new LevelInput(players, combat, level, playerInput, input, requests, host)
            { NormalBonusLevel = (mode & 32) != 0, BonusLevel = (mode & 64) != 0, StoppedDemo = mode % 17 == 0 };
            trace.AppendLine($"case:{mode}");
            var result = dispatcher.Handle();
            trace.AppendLine($"state:{Bit(result)},{players[0].ExplodingTicks},{players[1].ExplodingTicks}," +
                $"{Bit(level.MusicFade)},{Bit(level.ReallyEndLevel)},{Bit(level.FirstGameOver)},{Bit(dispatcher.StoppedDemo)}," +
                $"{Bit(input.NewKey)},{Bit(input.NewMouse)},{Bit(host.SkipPresentation)},{Bit(requests.PausePressed)}," +
                $"{Bit(requests.InGameMenuPressed)},{Bit(requests.Pause)},{Bit(requests.InGameMenu)},{platform.Services}");
            trace.AppendLine($"your:{Bit(requests.YourInGameMenu)}");
        }
        if (executable is null) return;
        using var process = Process.Start(new ProcessStartInfo(executable) { RedirectStandardOutput = true, RedirectStandardError = true })
            ?? throw new InvalidOperationException("Cannot start native level input reference.");
        var output = process.StandardOutput.ReadToEndAsync(); var errors = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(30000)) { process.Kill(entireProcessTree: true); throw new TimeoutException("Level input reference timed out."); }
        var expected = output.GetAwaiter().GetResult().Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var actual = trace.ToString().Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        if (process.ExitCode != 0 || !expected.SequenceEqual(actual))
        {
            var index = Enumerable.Range(0, Math.Min(expected.Length, actual.Length)).FirstOrDefault(i => expected[i] != actual[i], -1);
            throw new InvalidOperationException($"Level input differs at {index}: {expected.ElementAtOrDefault(index)} / " +
                $"{actual.ElementAtOrDefault(index)}; {errors.GetAwaiter().GetResult()}");
        }
    }

    internal static void RunSdl(SdlVideoDevice video)
    {
        var definitions = new ItemDefinitions();
        definitions.ParseBundle(Encoding.Latin1.GetBytes(ItemDatabaseTests.Fixture(0)), "level-input-sdl.tyrr");
        var items = ItemDatabase.FromDefinitions(definitions);
        for (var mode = 0; mode < 6; mode++)
        {
            SdlInputPlatform? platform = null;
            var pushes = 0; var presents = 0;
            using var ownedPlatform = platform = new SdlInputPlatform(video, new Auxiliary(() =>
            {
                pushes++;
                if (mode is 2 or 3)
                {
                    platform!.PostEvent(new(InputEventKind.FocusLost));
                    platform.PostEvent(new(InputEventKind.KeyDown, mode == 2 ? 40 : 41));
                }
            }), () => throw new InvalidOperationException("Unexpected level input quit."));
            var input = new InputState(platform, video.Layout); input.FlushEvents();
            platform.PostEvent(new(InputEventKind.FocusGained)); input.ServiceEvents(false);
            var game = new LegacyGameState(); var effects = new SceneEffects(new byte[8]);
            var combat = new PlayerCombat(game.Players, effects, video.Primary) { MusicVolume = 191 };
            var backgrounds = new BackgroundRenderer();
            var level = new LevelEvents(items, new(items, TextWriter.Null), game.Players, backgrounds, effects)
            { AllPlayersGone = mode == 0, FirstGameOver = true, EffectsVolume = 127 };
            var requests = new NetworkRequests();
            var font = new FontRenderer(HighScoreScreenTests.CreateShapes(151));
            var shots = new PlayerShots(items.Shots, items.Weapons, game.Players,
                new SpritePlayerShotEffects(video.Primary, new(), new(), new(), (_, _, _) => { }, (_, _, _, _, _) => { }));
            var movement = new PlayerMovement(game.Players, combat, level, shots, items);
            var playerInput = new PlayerInput(input, new([], () => input.Ticks, () => { }, platform.PostEvent),
                [], [], movement, combat, level, requests)
            { PlayDemo = mode == 1, NetworkGame = mode == 4 };
            var settings = new GameSettingsStore(new(new SdlKeyboardNames()), TextWriter.Null);
            settings.Runtime.ProcessorType = 3; settings.Runtime.GameSpeed = 4;
            settings.Dos.MusicVolume = 191; settings.Dos.EffectsVolume = 127;
            var timer = new DelayTimer(input); var mixer = new AudioMixer(44100, new SilentMusic());
            var text = new HelpTextData(); text.MiscText[21] = "Game over"u8.ToArray();
            var songs = new List<uint>(); var volumes = new List<(ushort Music, ushort Effects)>();
            void Present() { presents++; video.Present(); }
            var window = new GameTextWindow(() => { }, _ => { });
            var help = new GameHelp(new GameHelpTests.TraceHost(new(), 0), window);
            var graphics = new ushort[2];
            var keyboardHost = new GameKeyboardHost(new(input, video.Palette), new PaletteColor[256], combat, items,
                new(game.Players, items, new(), new(), new byte[150], graphics), help, "Continue"u8.ToArray(),
                window, "Cheat"u8.ToArray(), settings.Runtime, timer, input, () => { }, songs.Add);
            var keyboard = new GameKeyboard(game, items, input, combat, level, backgrounds, requests, settings.Runtime,
                shots, graphics, new(), keyboardHost)
            { NetworkGame = mode == 4 };
            var pause = new GamePause(input, timer, new GamePausePresentation(video.Primary, font, "Paused"u8.ToArray(), Present).Draw,
                platform.PushJoysticksAsKeyboard, (music, samples) => volumes.Add((music, samples)));
            var cursorSheet = new SpriteSheet(); var bytes = HighScoreScreenTests.SpriteBytes();
            using (var stream = new MemoryStream(bytes)) cursorSheet.Load(stream, bytes.Length);
            var cursor = new MouseCursor(input, video.Primary, cursorSheet) { HasMouse = false };
            var setupHost = new InGameSetupHost(video.Primary, new(320, 200, video.Primary.Pitch, TextWriter.Null), font,
                input, cursor, cursorSheet, mixer, settings.Runtime, settings.Dos, timer, () => { }, Present,
                platform.PushJoysticksAsKeyboard, () => { }, _ => { }, () => throw new InvalidOperationException("Unexpected setup halt."));
            var setup = new GameSetupSession(input, new(settings.Runtime, settings.Dos, text, setupHost), level, requests, () => { }, Present);
            var host = new LevelInputHost(keyboard, pause, setup, settings, video.Primary, font, text, songs.Add,
                (music, samples) => volumes.Add((music, samples)), platform.PushJoysticksAsKeyboard);
            var dispatcher = new LevelInput(game.Players, combat, level, playerInput, input, requests, host);
            video.Primary.Pixels.Span.Clear();
            platform.PostEvent(mode is 0 or 1 ? new(InputEventKind.MouseButtonDown, Button: 1) :
                new(InputEventKind.KeyDown, mode == 2 ? 19 : mode is 3 or 4 ? 41 : 58));
            using var cancellation = new CancellationTokenSource(5000);
            var skip = dispatcher.Handle(cancellation.Token);
            if (skip != (mode == 5) || requests.PausePressed || requests.InGameMenuPressed || requests.YourInGameMenu ||
                requests.InGameMenu != (mode == 4) || requests.Pause || dispatcher.StoppedDemo != (mode == 1) ||
                level.ReallyEndLevel != (mode is 0 or 1))
                throw new InvalidOperationException($"Actual SDL level input flags differ in case {mode}.");
            if (mode == 0 && (!songs.SequenceEqual([10u]) || !volumes.SequenceEqual<(ushort, ushort)>([(191, 127)]) || level.FirstGameOver))
                throw new InvalidOperationException("Actual SDL game-over music/volume ordering differs.");
            if (mode == 2 && (!volumes.SequenceEqual<(ushort, ushort)>([(95, 127), (191, 127)]) || pushes != 1 || presents != 1 || !input.RelativeEnabled))
                throw new InvalidOperationException("Actual SDL pause did not compose volume/input/relative-mode services.");
            if (mode == 3 && (!host.SkipPresentation || presents == 0 || !input.RelativeEnabled || input.KeysActive[41] != 0))
                throw new InvalidOperationException("Actual SDL setup did not compose settings/menu/presentation state.");
            if (mode == 5 && (input.NewKey || !host.SkipPresentation))
                throw new InvalidOperationException("Keyboard help did not consume the key and skip the level frame.");
            input.FlushEvents();
        }
    }

    private sealed class SilentMusic : IMusicSynthesizer
    {
        public void Update() { }
        public void Render(Span<short> samples) => samples.Clear();
    }
    private sealed class Auxiliary(Action push) : IInputAuxiliaryServices
    {
        public bool JoystickDown => false;
        public bool IsNetworkGame => false;
        public void PushJoysticksAsKeyboard() => push();
        public void PollJoysticks() { }
        public void CheckNetwork() { }
    }
    private static int Bit(bool value) => value ? 1 : 0;
    private sealed class Host(StringBuilder trace, NetworkRequests requests, int mode) : ILevelInputHost
    {
        public bool SkipPresentation { get; set; }
        public void DrawGameOver() => trace.AppendLine("draw:120,60,Game over,0");
        public void PlayGameOver() => trace.AppendLine("song:10");
        public void SetVolume(ushort music, ushort effects) => trace.AppendLine($"volume:{music},{effects}");
        public void PushJoysticks() => trace.AppendLine("joysticks");
        public void Keyboard(CancellationToken cancellation)
        {
            trace.AppendLine("keyboard"); SkipPresentation = (mode & 4096) != 0;
            requests.PausePressed |= (mode & 8192) != 0; requests.InGameMenuPressed |= (mode & 16384) != 0;
        }
        public void Pause(CancellationToken cancellation) => trace.AppendLine("pause");
        public void Setup(CancellationToken cancellation) { trace.AppendLine("setup"); requests.YourInGameMenu = false; }
    }
    private sealed class Platform(int mode) : IInputPlatform
    {
        public Queue<InputEvent> Events { get; } = new();
        public InputState? Input { get; set; }
        public bool Active { get; set; }
        public int Services { get; private set; }
        public bool PollEvent(out InputEvent input)
        {
            if (Events.TryDequeue(out input)) return true;
            if (Active && Services++ == 0)
            {
                Input!.NewKey |= (mode & 1024) != 0;
                Input.NewMouse |= (mode & 2048) != 0;
            }
            return false;
        }
        public uint Ticks => 0;
        public bool JoystickDown => false;
        public bool IsNetworkGame => false;
        public void Delay(uint milliseconds) => throw new NotSupportedException();
        public void PollJoysticks() => throw new NotSupportedException();
        public void PushJoysticksAsKeyboard() => throw new NotSupportedException();
        public void CheckNetwork() => throw new NotSupportedException();
        public void SetCursorVisible(bool visible) => throw new NotSupportedException();
        public void SetRelativeMouseMode(bool enabled) { }
        public void EnableSystemMouseScaling() => throw new NotSupportedException();
        public void ToggleFullscreen() => throw new NotSupportedException();
        public void WindowResized() => throw new NotSupportedException();
        public void Quit() => throw new NotSupportedException();
    }
}
