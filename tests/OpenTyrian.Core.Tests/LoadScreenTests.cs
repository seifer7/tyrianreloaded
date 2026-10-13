using System.Diagnostics;
using System.Text;
using OpenTyrian.Core;

namespace OpenTyrian.Core.Tests;

internal static class LoadScreenTests
{
    private static readonly int[] Counts = [0, 1, 10, 11, 12, 21, 22, 23, 33, -9];

    public static void Run(string? executable = null)
    {
        var trace = new StringBuilder();
        for (var mode = 0; mode < 960; mode++)
        {
            var (screen, _, _, _, saves) = Create(mode, Counts[mode % 10], trace);
            trace.AppendLine($"draw:{mode}"); screen.Draw(saves, mode / 10 % 4, mode / 40 % 12, mode >= 480);
        }
        int[] scans = [80, 79, 82, 81, 44, 40, 88, 41, 0];
        int[] xs = [9, 10, 82, 83, 106, 107, 212, 213, 236, 237, 309, 310];
        int[] ys = [29, 30, 37, 38, 160, 167, 173, 178, 179, 180, 500, -1];
        for (var mode = 0; mode < 8192; mode++)
        {
            var count = Counts[mode % 10];
            var (screen, input, platform, _, _) = Create(mode, count, trace);
            var x = xs[mode / 192 % 12]; var y = ys[mode / 16 % 12];
            SetInput(input, platform, (mode & 1) != 0, (mode & 2) != 0, scans[mode / 8 % 9], mode / 72 % 3 + 1,
                x, y, (mode & 16) != 0 ? x : 0, (mode & 32) != 0 ? y : 0);
            var selected = mode / 40 % 12;
            trace.AppendLine($"read:{mode}");
            var result = screen.ReadInput(Math.Max(0, count), mode / 10 % 4, ref selected, (mode & 4) != 0);
            trace.AppendLine($"input:{selected},{Bit(result.LeftAction)},{Bit(result.RightAction)},{Bit(result.Action)},{Bit(result.Done)}");
        }
        for (var mode = 0; mode < 7680; mode++)
        {
            var (screen, _, _, host, saves) = Create(mode, Counts[mode / 16 % 10], trace);
            var page = mode / 160 % 4;
            trace.AppendLine($"action:{mode}");
            var result = screen.HandleAction(saves, ref page, mode / 640 % 12,
                new((mode & 1) != 0, (mode & 2) != 0, (mode & 4) != 0, (mode & 8) != 0));
            trace.AppendLine($"actionstate:{page},{(int)result},{Bit(host.PerformSave)},{Bit(host.GameLoaded)}");
        }
        for (var mode = 0; mode < 8; mode++)
        {
            var (screen, input, platform, _, _) = Create(mode, 0, trace);
            if (mode == 3) SetInput(input, platform, false, false, 0, 0, -1, 10, 0, 0);
            trace.AppendLine($"wait:{mode}");
            var moved = screen.WaitInput();
            trace.AppendLine($"waitstate:{Bit(moved)},{input.MouseX},{input.MouseY},{platform.Waits}");
        }
        for (var mode = 0; mode < 160; mode++)
        {
            var (screen, _, platform, host, _) = Create(mode, Counts[mode % 10], trace);
            platform.Running = true;
            trace.AppendLine($"run:{mode}"); var result = screen.Run();
            trace.AppendLine($"runstate:{Bit(result)},{Bit(host.PerformSave)},{Bit(host.GameLoaded)},{platform.Steps}");
        }
        if (executable != null) Compare(executable, trace);
        TestHost();
        TestInputEdges();
    }

    private static void Compare(string executable, StringBuilder trace)
    {
        using var process = Process.Start(new ProcessStartInfo(executable) { RedirectStandardOutput = true, RedirectStandardError = true })
            ?? throw new InvalidOperationException("Cannot start load-screen oracle.");
        var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(30000)) { process.Kill(entireProcessTree: true); throw new TimeoutException("Load-screen oracle timed out."); }
        var expected = output.GetAwaiter().GetResult().Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var actual = trace.ToString().Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        if (process.ExitCode == 0 && expected.SequenceEqual(actual)) return;
        var index = Enumerable.Range(0, Math.Min(expected.Length, actual.Length)).FirstOrDefault(i => expected[i] != actual[i], -1);
        throw new InvalidOperationException($"Load-screen trace differs at {index}: {expected.ElementAtOrDefault(index)} / " +
            $"{actual.ElementAtOrDefault(index)}; {error.GetAwaiter().GetResult()}");
    }

    private static (LoadScreen Screen, InputState Input, Platform Platform, Host Host, IReadOnlyList<SavedGameMetadata> Saves)
        Create(int mode, int count, StringBuilder trace)
    {
        var text = Text();
        var saves = Enumerable.Range(0, Math.Max(0, count)).Select(i =>
            new SavedGameMetadata($"Name{i}", Encoding.ASCII.GetBytes($"Level{i:00000}"), 0, false, (byte)(i * 19), 1, 1)).ToArray();
        var platform = new Platform(mode, trace);
        var input = new InputState(platform, new());
        SetInput(input, platform, false, false, 0, 0, 10, 10, 0, 0);
        var host = new Host(mode, count, saves, trace, platform);
        return (new(text, input, host), input, platform, host, saves);
    }

    private static HelpTextData Text()
    {
        var text = new HelpTextData();
        for (var i = 0; i < text.MiscText.Length; i++) text.MiscText[i] = Encoding.ASCII.GetBytes($"Text{i}");
        text.MiscTextB[1] = "Episode"u8.ToArray(); text.MiscTextB[2] = "Last-level-12345678"u8.ToArray();
        return text;
    }

    private static void SetInput(InputState input, Platform platform, bool key, bool mouse, int scan, int button,
        int x, int y, int clickX, int clickY)
    {
        input.ServiceEvents(true);
        platform.Events.Enqueue(new(InputEventKind.MouseMotion, X: x < 0 ? x - 1 : x, Y: y < 0 ? y - 1 : y));
        input.ServiceEvents(false);
        if (key) { platform.Events.Enqueue(new(InputEventKind.KeyDown, scan)); input.ServiceEvents(false); }
        if (mouse)
        {
            platform.Events.Enqueue(new(InputEventKind.MouseButtonDown, Button: (byte)button,
                X: clickX < 0 ? clickX - 1 : clickX, Y: clickY < 0 ? clickY - 1 : clickY));
            input.ServiceEvents(false);
        }
    }

    private static void TestInputEdges()
    {
        var (screen, input, platform, _, _) = Create(0, 1, new());
        for (var row = 0; row < 12; row++)
            foreach (var x in new[] { 9, 10, 309, 310 })
                foreach (var offset in new[] { -1, 0, 7, 8 })
                {
                    var y = 30 + 13 * row + offset;
                    SetInput(input, platform, true, true, 41, 1, x, y, x, y);
                    var selected = row;
                    var action = screen.ReadInput(1, 0, ref selected, false);
                    var hit = x is >= 10 and < 310 && offset is >= 0 and < 8;
                    if (action.Action != hit || action.Done) throw new InvalidOperationException("Load-screen row bounds or mouse precedence differs.");
                }
        SetInput(input, platform, true, true, 41, 1, 15, 30, 0, 0);
        var selection = 0;
        if (screen.ReadInput(1, 0, ref selection, false) != default)
            throw new InvalidOperationException("Load-screen used hover coordinates as a click or failed mouse precedence.");
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        try { screen.Run(cancellation.Token); throw new InvalidOperationException("Load-screen ignored cancellation."); }
        catch (OperationCanceledException) { }
    }

    private static void TestHost()
    {
        var primary = new IndexedSurface(320, 200, 327, TextWriter.Null);
        var background = new IndexedSurface(320, 200, 327, TextWriter.Null);
        var expected = new IndexedSurface(320, 200, 327, TextWriter.Null);
        var font = new FontRenderer(HighScoreScreenTests.CreateShapes()); var help = new HelpTextRenderer(font);
        var sprites = new SpriteSheet(); var spriteBytes = HighScoreScreenTests.SpriteBytes();
        var input = new InputState(new TextGlowTests.Platform(), new()); var timer = new DelayTimer(input);
        var cursor = new MouseCursor(input, primary, sprites) { HasMouse = false, Cursor = 3 };
        var palette = new PaletteSystem(c => (uint)(c.Red << 16 | c.Green << 8 | c.Blue));
        var colors = Enumerable.Range(0, 256).Select(i => new PaletteColor((byte)i, (byte)i, (byte)i)).ToArray();
        var loads = 0; var pictures = 0; var presents = 0; var pushes = 0; var performSave = true;
        var samples = new List<byte>(); var names = new List<string>(); var saved = new SavedGameMetadata("Fixture", "LEVEL"u8.ToArray(), 0, false, 2, 1, 1);
        var host = new LoadScreenHost(primary, background, font, help, sprites, cursor, palette, colors, timer,
            () => { using var stream = new MemoryStream(spriteBytes); sprites.Load(stream, spriteBytes.Length); loads++; },
            () => { background.Pixels.Span.Fill(0x75); pictures++; }, () => presents++, () => pushes++, samples.Add,
            () => new[] { saved }, () => performSave = false, names.Add, () => true);
        host.EnsureCursor(); host.EnsureCursor(); host.LoadBackground(); host.RestoreBackground(); host.Header();
        expected.Pixels.Span.Fill(0x75); expected.FillRectangleSize(0, 192, 320, 8, 0);
        font.DrawShadow(expected, 160, 5, "Saved Games"u8, Font.Large, FontAlignment.Centered, 15, -3, false, 2);
        host.Shaded(10, 30, "SAVE"u8.ToArray(), 13, 6); font.ShadeText(expected, 10, 30, "SAVE"u8, 13, 6, TextShade.Full);
        host.Arrow(213, 281); sprites.Blit2x2(expected, 213, 179, 281);
        host.Help("Pick"u8.ToArray()); new HelpTextRenderer(font) { Color = 15 }.DrawBox(expected, 103, 182, "Pick"u8, 25);
        if (!primary.Pixels.Span.SequenceEqual(expected.Pixels.Span) || loads != 1 || pictures != 1)
            throw new InvalidOperationException("Load-screen font, background, arrow or help rendering differs.");
        host.FadeIn(); host.PresentCursor(); host.PushJoysticks(); host.Sample(23); host.LoadSave("Fixture");
        if (!palette.Active.Span.SequenceEqual(colors) || cursor.Cursor != 0 || pushes != 1 || presents != 11 || performSave ||
            !samples.SequenceEqual(new byte[] { 23 }) || !names.SequenceEqual(["Fixture"]) || !host.GameLoaded ||
            host.BuildSaves().Single() != saved) throw new InvalidOperationException("Load-screen host palette, input, audio or save routing differs.");
        host.FadeBlack();
        if (presents != 26 || palette.Active.Span.ToArray().Any(color => color != default))
            throw new InvalidOperationException("Load-screen host failed to fade black.");
    }

    internal static void RunSdl(SdlVideoDevice video)
    {
        var root = Path.Combine(Path.GetTempPath(), $"opentyrian-load-screen-{Guid.NewGuid():N}");
        var listing = Path.Combine(root, "saves"); Directory.CreateDirectory(listing);
        var files = new HashSet<string>(StringComparer.Ordinal);
        var items = VersionedSaveTests.Database(); var source = VersionedSaveTests.Fixture(0);
        var game = VersionedSaveTests.Fixture(32); var performSave = true;
        using var diagnostics = new StringWriter();
        var catalog = new SavedGameCatalog(root, diagnostics);
        try
        {
            for (var i = 0; i < 12; i++)
            {
                var name = $"Fixture{i:00}";
                var native = catalog.NativePath($"{name}.tyrsav"); files.Add(native);
                VersionedSaveGame.SaveFile(root, name, source, items);
                var enumerated = Path.Combine(listing, $"{name}.tyrsav"); files.Add(enumerated);
                if (native != enumerated) File.Copy(native, enumerated);
            }
            var pushes = 0; var pictures = 0; var inits = 0; var listBuilds = 0;
            int[] scans = [81, 40, 79, 40];
            SdlInputPlatform? platform = null;
            using var ownedPlatform = platform = new SdlInputPlatform(video, new Auxiliary(() =>
            {
                if (pushes >= scans.Length) throw new InvalidOperationException("SDL title/load path requested unexpected input.");
                platform!.PostEvent(new(InputEventKind.KeyDown, scans[pushes++]));
            }), () => throw new InvalidOperationException("Unexpected SDL load-screen quit."));
            var input = new InputState(platform, video.Layout); input.FlushEvents();
            var timer = new DelayTimer(input); var shapes = HighScoreScreenTests.CreateShapes(151); var font = new FontRenderer(shapes);
            var sprites = new SpriteSheet(); var bytes = HighScoreScreenTests.SpriteBytes();
            var background = new IndexedSurface(320, 200, video.Primary.Pitch, TextWriter.Null);
            var cursor = new MouseCursor(input, video.Primary, sprites) { HasMouse = false };
            var colors = Enumerable.Range(0, 256).Select(i => new PaletteColor((byte)i, (byte)i, (byte)i)).ToArray();
            var text = Text(); for (var i = 0; i < text.MenuText.Length; i++) text.MenuText[i] = Encoding.ASCII.GetBytes($"Menu{i}");
            var loadedNames = new List<string>(); var samples = new List<byte>();
            var host = new LoadScreenHost(video.Primary, background, font, new(font), sprites, cursor, video.Palette, colors, timer,
                () => { using var stream = new MemoryStream(bytes); sprites.Load(stream, bytes.Length); },
                () => { background.Pixels.Span.Fill(0x75); pictures++; }, video.Present, platform.PushJoysticksAsKeyboard,
                samples.Add, () => { listBuilds++; return catalog.BuildList(0, root); }, () => performSave = false,
                name =>
                {
                    loadedNames.Add(name);
                    VersionedSaveGame.LoadFile(root, name, game, items, episode => { inits++; return episode; }, diagnostics);
                }, () => game.GameLoaded);
            using var cancellation = new CancellationTokenSource(10000);
            var screen = new LoadScreen(text, input, host);
            void Cursor() { host.EnsureCursor(); }
            bool Unexpected() => throw new InvalidOperationException("Unexpected title child action.");
            void UnexpectedVoid() => throw new InvalidOperationException("Unexpected title child action.");
            var title = new TitleScreen(text, input, new TitleScreenHost(video.Primary, background, font, shapes.Tables[7],
                sprites, cursor, video.Palette, colors, timer, "Version"u8.ToArray(), Cursor,
                () => { video.Primary.Pixels.Span.Fill(0x75); pictures++; }, _ => { }, video.Present,
                platform.PushJoysticksAsKeyboard, samples.Add, Unexpected, () => screen.Run(cancellation.Token),
                UnexpectedVoid, UnexpectedVoid, UnexpectedVoid, UnexpectedVoid, UnexpectedVoid, _ => Unexpected()));
            if (!title.Run(cancellation.Token) || pushes != 4 || pictures != 2 || listBuilds != 1 || inits != 1 ||
                !loadedNames.SequenceEqual(["Fixture11"]) || performSave || !game.GameLoaded || !game.GameJustLoaded ||
                game.Episode != 2 || game.MainLevel != 2 || game.SaveLevel != 2 || game.Difficulty != -128 ||
                game.Players[0].Cash != 17 || !game.LevelName.AsSpan().SequenceEqual("Completed"u8) ||
                !samples.SequenceEqual(new byte[] { 28, 8, 28, 8 }) || diagnostics.ToString().Length != 0 ||
                video.Palette.Active.Span.ToArray().Any(color => color != default))
                throw new InvalidOperationException("Actual SDL title/load paging did not restore the persisted save state.");
            input.FlushEvents();
        }
        finally
        {
            foreach (var path in files) if (File.Exists(path)) File.Delete(path);
            Directory.Delete(listing); Directory.Delete(root);
        }
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

    private sealed class Host(int mode, int count, IReadOnlyList<SavedGameMetadata> saves, StringBuilder trace, Platform platform) : ILoadScreenHost
    {
        public bool GameLoaded { get; private set; } = (mode & 2) != 0;
        public bool PerformSave { get; private set; } = true;
        public void EnsureCursor() { if ((mode & 1) == 0) trace.AppendLine("cursor:1"); }
        public IReadOnlyList<SavedGameMetadata> BuildSaves() { trace.AppendLine($"list:0,{count}"); return saves; }
        public void LoadBackground() { trace.AppendLine("picture:2"); trace.AppendLine("fill:0,192,320,8,0"); }
        public void RestoreBackground() { }
        public void Header() => trace.AppendLine("header:160,5,Saved Games,15,-3,2");
        public void Shaded(int x, int y, ReadOnlyMemory<byte> text, byte hue, int brightness) =>
            trace.AppendLine($"shaded:{x},{y},{Encoding.Latin1.GetString(text.Span)},{hue},{brightness}");
        public void Arrow(int x, int sprite) => trace.AppendLine($"arrow:{x},179,{sprite}");
        public void Help(ReadOnlyMemory<byte> text) => trace.AppendLine($"help:103,182,{Encoding.Latin1.GetString(text.Span)},25,15");
        public void FadeIn() => trace.AppendLine("fadein:10,0,255,0");
        public void PresentCursor() { trace.AppendLine("mouse"); trace.AppendLine("present"); trace.AppendLine("replace"); }
        public void PushJoysticks() => platform.PushJoysticksAsKeyboard();
        public void Sample(byte number) => trace.AppendLine($"sample:{number}");
        public void LoadSave(string name) { PerformSave = false; trace.AppendLine($"load:{name},0"); if (mode % 3 != 0) GameLoaded = true; }
        public void FadeBlack() => trace.AppendLine("fade:15");
    }

    private sealed class Platform(int mode, StringBuilder trace) : IInputPlatform
    {
        public Queue<InputEvent> Events { get; } = new();
        public bool Running { get; set; }
        public int Steps { get; private set; }
        public int Waits { get; private set; }
        private int _mouseY = 10;
        public uint Ticks { get; private set; }
        public bool JoystickDown => false;
        public bool IsNetworkGame => false;
        public bool PollEvent(out InputEvent input) => Events.TryDequeue(out input);
        public void Delay(uint milliseconds) { trace.AppendLine($"delay:{milliseconds}"); Ticks += milliseconds; }
        public void PushJoysticksAsKeyboard()
        {
            trace.AppendLine("joystick");
            if (!Running)
            {
                if (++Waits < mode % 4 + 1) return;
                if (mode == 0) Events.Enqueue(new(InputEventKind.KeyDown, 40));
                else if (mode == 1) Events.Enqueue(new(InputEventKind.MouseButtonDown, Button: 1));
                else if (mode == 2) Events.Enqueue(new(InputEventKind.MouseMotion, X: 11, Y: 10));
                else if (mode >= 4) Events.Enqueue(new(InputEventKind.MouseMotion, X: 10, Y: ++_mouseY));
                return;
            }
            var kind = mode % 8; var scan = 41;
            if (Steps == 0)
            {
                int[] scans = [40, 82, 81, 79, 80, 0, 0, 81]; scan = scans[kind];
                if (kind is 5 or 6)
                {
                    Events.Enqueue(new(InputEventKind.MouseMotion, X: 15, Y: 160));
                    Events.Enqueue(new(InputEventKind.MouseButtonDown, Button: (byte)(kind == 5 ? 1 : 3), X: 15, Y: 160));
                }
            }
            else if (Steps == 1 && kind is not 5 and not 6) scan = kind == 7 ? 82 : 40;
            else if (Steps == 2 && kind == 7) scan = 40;
            if (Steps != 0 || kind is not 5 and not 6) Events.Enqueue(new(InputEventKind.KeyDown, scan));
            Steps++;
        }
        public void SetRelativeMouseMode(bool enabled) { }
        public void SetCursorVisible(bool visible) { }
        public void EnableSystemMouseScaling() { }
        public void ToggleFullscreen() { }
        public void WindowResized() { }
        public void PollJoysticks() { }
        public void CheckNetwork() { }
        public void Quit() => throw new InvalidOperationException("Unexpected load-screen quit.");
    }
}
