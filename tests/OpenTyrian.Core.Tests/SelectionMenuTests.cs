using System.Diagnostics;
using System.Text;
using System.Runtime.InteropServices;
using OpenTyrian.Core;

namespace OpenTyrian.Core.Tests;

internal static class SelectionMenuTests
{
    private readonly record struct Frame(int Key = 0, ushort Mod = 0, int X = -999, int Y = 0,
        int ClickX = 0, int ClickY = 0, byte Button = 0, int Held = 0);

    public static void Run(string? executable = null)
    {
        var actual = Execute();
        Check(actual.Contains("result:1,1,1,7,9,8,", StringComparison.Ordinal), "Lord difficulty was not unlocked.");
        Check(actual.Contains("black:15", StringComparison.Ordinal), "Menu cancellation did not fade out.");
        if (executable != null)
        {
            using var process = Process.Start(new ProcessStartInfo(executable)
            { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false })
                ?? throw new InvalidOperationException("Cannot start native menu oracle.");
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(30000))
            {
                process.Kill(entireProcessTree: true);
                throw new TimeoutException("Native menu oracle timed out.");
            }
            var expected = stdout.GetAwaiter().GetResult().Replace("\r\n", "\n", StringComparison.Ordinal);
            Check(process.ExitCode == 0, $"Native menu oracle failed: {stderr.GetAwaiter().GetResult()}");
            var expectedLines = expected.Split('\n');
            var actualLines = actual.Split('\n');
            for (var i = 0; i < Math.Max(expectedLines.Length, actualLines.Length); i++)
                if (i >= expectedLines.Length || i >= actualLines.Length || expectedLines[i] != actualLines[i])
                    throw new InvalidOperationException($"Menu trace mismatch at {i + 1}: native '{expectedLines.ElementAtOrDefault(i)}', managed '{actualLines.ElementAtOrDefault(i)}'.");
        }
        Presentation();
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void SetModifiers(int modifiers);

    internal static void RunSdl(SdlVideoDevice video)
    {
        SdlInputPlatform? platform = null;
        var keys = new Queue<int>();
        var auxiliary = new MenuAuxiliary(() =>
        {
            Check(keys.Count > 0, "SDL menu input script exhausted.");
            platform!.PostEvent(new(InputEventKind.KeyDown, keys.Dequeue()));
        });
        using (platform = new SdlInputPlatform(video, auxiliary, () => throw new InvalidOperationException("Unexpected menu quit.")))
        {
            var path = Environment.GetEnvironmentVariable("SDL2_LIBRARY_PATH") ??
                (OperatingSystem.IsWindows() ? "SDL2.dll" : OperatingSystem.IsMacOS() ? "libSDL2.dylib" : "libSDL2-2.0.so.0");
            var library = NativeLibrary.Load(path);
            var set = Marshal.GetDelegateForFunctionPointer<SetModifiers>(NativeLibrary.GetExport(library, "SDL_SetModState"));
            var previous = platform.Modifiers;
            try
            {
                set(1);
                Check(platform.Modifiers == 1, "Menus cannot read live SDL modifier state.");
                set(0);
                var input = new InputState(platform, video.Layout);
                input.FlushEvents();
                var timer = new DelayTimer(input);
                var cursor = new MouseCursor(input, video.Primary, new SpriteSheet());
                var colors = Enumerable.Range(0, 256).Select(i => new PaletteColor((byte)i, (byte)(255 - i), (byte)(i / 2))).ToArray();
                var sounds = new List<byte>();
                var presents = 0;
                void Present() { video.Present(); presents++; }
                void BeginDelay() => timer.SetDelay(1);
                var host = new SelectionMenuPresentation(video.Primary, video.Secondary, new FontRenderer(new MainShapeTables()), cursor,
                    () => { }, () => video.Secondary.Pixels.Span.Fill(0xA8),
                    () => video.Palette.FadePalette(colors, 10, 0, 255, BeginDelay, Present, timer.Wait),
                    Present, sounds.Add,
                    steps => video.Palette.FadeBlack(steps, BeginDelay, Present, timer.Wait),
                    platform.PushJoysticksAsKeyboard, platform.CheckNetwork, () => platform.Modifiers, episode => episode);
                var state = new SelectionMenuState();
                var menus = new SelectionMenus(input, host, state);
                var names = new ReadOnlyMemory<byte>[7];
                keys.Enqueue(81); keys.Enqueue(40);
                Check(menus.SelectGameplay(names) && state.OnePlayerAction && !state.TwoPlayerMode,
                    "Real SDL input did not select arcade gameplay.");
                keys.Enqueue(81); keys.Enqueue(81); keys.Enqueue(40);
                Check(menus.SelectEpisode(names, [true, false, true, false]) && state.Episode == 3 && state.InitialEpisode == 3,
                    "Real SDL input did not commit the selected episode.");
                keys.Enqueue(41);
                state.Difficulty = 6;
                Check(!menus.SelectDifficulty(names) && state.Difficulty == 6, "Real SDL difficulty cancellation changed state.");
                Check(sounds.SequenceEqual(new byte[] { 28, 8, 28, 28, 8, 16 }) && auxiliary.KeepAlives == 4,
                    "SDL menu sound or keep-alive ordering changed.");
                Check(presents >= 70 && video.Palette.Active.Span.ToArray().All(c => c == new PaletteColor(0, 0, 0)),
                    "Menu fades did not reach real SDL presentation or finish black.");
            }
            finally { set(previous); NativeLibrary.Free(library); }
        }
    }

    private sealed class MenuAuxiliary(Action push) : IInputAuxiliaryServices
    {
        internal int KeepAlives;
        public bool JoystickDown => false;
        public bool IsNetworkGame => true;
        public void PushJoysticksAsKeyboard() => push();
        public void PollJoysticks() => throw new InvalidOperationException();
        public void CheckNetwork() => KeepAlives++;
    }

    private static string Execute()
    {
        var trace = new StringBuilder();
        for (var kind = 0; kind < 3; kind++)
            for (var mode = 0; mode < 40; mode++)
            {
                trace.AppendLine($"case:{kind},{mode}");
                var platform = new Platform(trace, Script(kind, mode));
                var input = new InputState(platform, new VideoLayout { Output = new(0, 0, 320, 200) });
                platform.Input = input;
                var host = new Host(trace, platform, input, mode % 2 != 0);
                var state = new SelectionMenuState { OnePlayerAction = true, TwoPlayerMode = true, Episode = 7, InitialEpisode = 9, Difficulty = 10 };
                var menus = new SelectionMenus(input, host, state);
                var prefix = kind == 0 ? 'G' : kind == 1 ? 'E' : 'D';
                var names = Enumerable.Range(0, 7).Select(i => (ReadOnlyMemory<byte>)Encoding.ASCII.GetBytes($"{prefix}{i}")).ToArray();
                var result = kind switch
                {
                    0 => menus.SelectGameplay(names),
                    1 => menus.SelectEpisode(names, Enumerable.Range(0, 4).Select(i => mode % 4 == 0 || i % 2 == 0).ToArray()),
                    _ => menus.SelectDifficulty(names)
                };
                trace.AppendLine($"result:{Bit(result)},{Bit(state.OnePlayerAction)},{Bit(state.TwoPlayerMode)},{state.Episode},{state.InitialEpisode},{state.Difficulty},{platform.FrameIndex}");
            }
        return trace.ToString().Replace("\r\n", "\n", StringComparison.Ordinal);
    }

    private static List<Frame> Script(int kind, int mode)
    {
        var frames = new List<Frame>();
        void Key(int key, ushort mod = 0, int held = 0) => frames.Add(new(Key: key, Mod: mod, Held: held));
        void Mouse(int mx, int my, int cx, int cy, byte button, int key = 0)
            => frames.Add(new(key, X: mx, Y: my, ClickX: cx, ClickY: cy, Button: button));
        void Down(int count) { for (var i = 0; i < count; i++) Key(81); }
        var x = kind == 1 ? 20 : 140;
        var y = kind == 1 ? 50 : 54;
        var dy = kind == 1 ? 30 : 24;
        switch (mode)
        {
            case 0: Key(40); break;
            case 1: Key(41); break;
            case 2: Key(82); Key(40); break;
            case 3: Key(81); Key(40); break;
            case 4: Key(88); break;
            case 5: Key(44); break;
            case 6: Mouse(0, 0, 0, 0, 3); break;
            case 7: Mouse(x, y, x, y, 1); break;
            case 8: Mouse(x, y + 2 * dy, x, y + 2 * dy, 1); break;
            case 9: Mouse(x + 40, y, x + 40, y, 1); break;
            case 10: Mouse(x, y + 13, x, y + 13, 1); break;
            case 11: Mouse(x, y + dy, 0, 0, 1); break;
            case 12: Mouse(x, y + 2 * dy, x, y + 2 * dy, 3, 40); break;
            case 13: Mouse(x, y + dy, x, y + dy, 1, 82); break;
            case 14: Key(0); Key(0); Key(40); break;
            case 15: Key(10, 1); Down(2); Key(40); break;
            case 16: Key(48, 1); Key(10, 1); Key(48, 1); Down(3); Key(40); break;
            case 17 or 18 or 19:
                Key(10, 1); Key(48, 1);
                if (mode == 18) Key(0, held: 15);
                else { Key(15); Key(18); if (mode == 19) Key(5); Key(21); Key(7); }
                Down(4); Key(40); break;
            case 20: Key(10); Key(48); Key(82); Key(40); break;
            case 21: Mouse(x, y + dy, x, y + dy, 0); Key(40); break;
            case 22: Mouse(-1, -1, -1, -1, 1); break;
            case 23: Mouse(x + 39, y + 12, x + 39, y + 12, 1); break;
            default:
                for (var i = 0; i < mode - 21; i++) Key(mode % 2 != 0 ? 82 : 81);
                Key(40); break;
        }
        Key(41);
        return frames;
    }

    private sealed class Platform(StringBuilder trace, List<Frame> frames) : IInputPlatform
    {
        private readonly Queue<InputEvent> _events = new();
        internal InputState? Input;
        internal int FrameIndex;
        internal ushort Modifiers;
        public bool PollEvent(out InputEvent input) => _events.TryDequeue(out input);
        public uint Ticks => 0;
        public bool JoystickDown => false;
        public bool IsNetworkGame => false;
        public void Delay(uint milliseconds)
        {
            Check(FrameIndex < frames.Count, "Managed menu script exhausted.");
            trace.AppendLine($"delay:{milliseconds}");
            var frame = frames[FrameIndex++];
            Modifiers = frame.Mod;
            var active = Input!.KeysActive;
            Array.Clear(active);
            int[] lord = [15, 18, 21, 7];
            for (var i = 0; i < 4; i++) if ((frame.Held & (1 << i)) != 0) active[lord[i]] = 1;
            if (frame.X != -999) _events.Enqueue(new(InputEventKind.MouseMotion, X: frame.X, Y: frame.Y));
            if (frame.Button != 0) _events.Enqueue(new(InputEventKind.MouseButtonDown, Button: frame.Button, X: frame.ClickX, Y: frame.ClickY));
            if (frame.Key != 0) _events.Enqueue(new(InputEventKind.KeyDown, frame.Key, frame.Mod));
        }
        public void SetCursorVisible(bool visible) { }
        public void PushJoysticksAsKeyboard() => trace.AppendLine("joystick");
        public void CheckNetwork() => trace.AppendLine("keep");
        public void SetRelativeMouseMode(bool enabled) => throw new InvalidOperationException();
        public void EnableSystemMouseScaling() => throw new InvalidOperationException();
        public void ToggleFullscreen() => throw new InvalidOperationException();
        public void WindowResized() => throw new InvalidOperationException();
        public void Quit() => throw new InvalidOperationException();
        public void PollJoysticks() => throw new InvalidOperationException();
    }

    private sealed class Host(StringBuilder trace, Platform platform, InputState input, bool loaded) : ISelectionMenuHost
    {
        private readonly byte[] _screen = new byte[64000], _background = new byte[64000];
        public void EnsureCursorSprites() { if (!loaded) trace.AppendLine("cursor-load"); }
        public void LoadBackground() { trace.AppendLine("picture"); Array.Fill(_background, (byte)0xA8); }
        public void RestoreBackground() => _background.CopyTo(_screen, 0);
        public int TextWidth(ReadOnlyMemory<byte> text) => 40;
        public void DrawText(bool background, int x, int y, ReadOnlyMemory<byte> text, Font font, FontAlignment alignment, sbyte brightness)
        {
            trace.AppendLine($"draw:{Bit(background)},{x},{y},{Encoding.ASCII.GetString(text.Span)},{(int)font},{(int)alignment},{brightness}");
            var left = x - (alignment == FontAlignment.Centered ? 20 : 0);
            if (left >= 0 && left < 320 && y >= 0 && y < 200)
                (background ? _background : _screen)[y * 320 + left] = unchecked((byte)(15 * 16 + brightness + 8));
        }
        public void FadeIn() => trace.AppendLine("fade-in");
        public void PresentWithCursor()
        {
            input.ServiceEvents(false);
            trace.AppendLine("cursor-start");
            uint hash = 2166136261;
            foreach (var pixel in _screen) hash = unchecked((hash ^ pixel) * 16777619);
            trace.AppendLine($"show:{hash}");
            trace.AppendLine("cursor-restore");
        }
        public void PlaySound(byte sample) => trace.AppendLine($"sound:{sample}");
        public void FadeBlack(int steps) => trace.AppendLine($"black:{steps}");
        public void PushJoysticksAsKeyboard() => platform.PushJoysticksAsKeyboard();
        public void KeepAlive() => platform.CheckNetwork();
        public ushort Modifiers => platform.Modifiers;
        public int InitializeEpisode(int episode) { trace.AppendLine($"episode:{episode}"); return episode; }
    }

    private static void Presentation()
    {
        using var warnings = new StringWriter();
        var screen = new IndexedSurface(320, 200, 320, warnings);
        var background = new IndexedSurface(320, 200, 320, warnings);
        var shapes = new MainShapeTables();
        using var encoded = new MemoryStream();
        BinaryFile.WriteUInt16(encoded, 1);
        BinaryFile.WriteBoolean(encoded, true);
        BinaryFile.WriteUInt16(encoded, 1);
        BinaryFile.WriteUInt16(encoded, 1);
        BinaryFile.WriteUInt16(encoded, 1);
        BinaryFile.WriteExactly(encoded, new byte[] { 0xA8 });
        foreach (var table in shapes.Tables.Take(2)) { encoded.Position = 0; table.Load(encoded); }
        var platform = new Platform(new(), []);
        var input = new InputState(platform, new VideoLayout());
        var cursor = new MouseCursor(input, screen, new SpriteSheet()) { Cursor = 4 };
        var presentCount = 0;
        var failPresent = false;
        var host = new SelectionMenuPresentation(screen, background, new FontRenderer(shapes), cursor,
            () => { }, () => background.Pixels.Span.Fill(0xAE), () => { },
            () => { presentCount++; screen.Pixel(0, 0, 33); if (failPresent) throw new IOException("fixture"); },
            _ => { }, _ => { }, () => { }, () => { }, () => 0, episode => episode);
        host.LoadBackground();
        host.DrawText(true, 160, 20, "A"u8.ToArray(), Font.Large, FontAlignment.Centered, -3);
        host.RestoreBackground();
        Check(screen.Pixels.Span[20 * 320 + 159] == 0xF5 && screen.Pixels.Span[22 * 320 + 161] == 0xA7,
            "Menu presentation did not use managed font shadow/alignment.");
        host.FadeIn();
        Check(cursor.Cursor == 0, "Menu did not select the normal pointer.");
        host.PresentWithCursor();
        Check(screen.Pixels.Span[0] == 0xAE && presentCount == 1, "Menu cursor restoration did not preserve its background.");
        failPresent = true;
        try { host.PresentWithCursor(); throw new InvalidOperationException("Missing presentation failure."); }
        catch (IOException) { Check(screen.Pixels.Span[0] == 0xAE, "Failed presentation left cursor pixels on screen."); }
        Check(warnings.ToString().Length == 0, "Menu presentation emitted clipping warnings.");
        var menus = new SelectionMenus(input, host, new());
        try { menus.SelectGameplay([]); throw new InvalidOperationException("Missing label validation."); }
        catch (ArgumentException) { }
        try { menus.SelectEpisode(new ReadOnlyMemory<byte>[5], []); throw new InvalidOperationException("Missing availability validation."); }
        catch (ArgumentException) { }
    }

    private static int Bit(bool value) => value ? 1 : 0;
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
