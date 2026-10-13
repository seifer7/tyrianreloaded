using System.Diagnostics;
using System.Text;
using OpenTyrian.Core;

namespace OpenTyrian.Core.Tests;

internal static class TitleScreenTests
{
    private static readonly string[] Codes = ["ALPHA", "ALP", "BETA", "AAB", "NINEBYTE9", "SHIP", "ZED", "DESTRUCT", "ENGAGE"];
    private static readonly string[] Entered = ["alpha", "aalpha", "al", "pha", "betaSHIP", "ninebyte9", "deSTRUCT", "engage",
        "aab", "zzed", "ship", "bogus", "alp", "ALPHAALP", "NINEBYTE9X", "BETA", "DESTRUCTengage"];

    public static void Run(string? executable = null)
    {
        var trace = new StringBuilder();
        for (var mode = 0; mode < 14; mode++)
        {
            var (screen, _, _, _) = Create(mode, trace);
            screen.MoveLogoUp = (mode & 1) != 0; screen.Selected = mode % 7;
            var widths = new int[7]; trace.AppendLine($"draw:{mode}"); screen.Draw(true, widths); screen.Draw(false, widths);
            trace.AppendLine($"drawstate:{Bit(screen.MoveLogoUp)},{string.Join(',', widths)}");
        }
        for (var mode = 0; mode < 4096; mode++)
        {
            var (screen, input, platform, _) = Create(mode, trace); screen.Selected = mode % 7;
            var widths = Enumerable.Range(0, 7).Select(i => 10 + i * 3).ToArray();
            int[] scans = [82, 81, 44, 40, 88, 41, 0];
            SetInput(input, platform, (mode & 1) != 0, (mode & 2) != 0, scans[mode / 4 % 7], mode % 3 + 1,
                mode % 181 + 70, mode / 8 % 121 + 90, mode * 7 % 181 + 70, mode * 13 % 121 + 90);
            trace.AppendLine($"read:{mode}"); var result = screen.Read(widths, (mode & 4) != 0);
            trace.AppendLine($"input:{screen.Selected},{Bit(result.Action)},{Bit(result.Done)}");
        }
        for (var mode = 0; mode < 28; mode++)
        {
            var (screen, _, _, _) = Create(mode, trace); screen.Selected = mode % 7;
            var restart = (mode & 1) != 0; trace.AppendLine($"dispatch:{mode}"); var result = screen.Dispatch(ref restart);
            trace.AppendLine($"result:{(int)result},{Bit(restart)},{Bit(screen.PlayDemo)}");
        }
        for (var mode = 0; mode < 8; mode++)
        {
            var (screen, input, platform, _) = Create(mode, trace);
            platform.Ticks = (mode & 1) != 0 ? uint.MaxValue - 16 : 0;
            SetInput(input, platform, false, false, 0, 0, mode >= 5 ? -1 : 10, mode >= 6 ? -1 : 10, 0, 0);
            trace.AppendLine($"waitcase:{mode}"); var result = screen.Wait(out var moved);
            trace.AppendLine($"waitstate:{Bit(result)},{Bit(moved)},{Bit(screen.PlayDemo)},{platform.Ticks}");
        }
        for (var mode = 0; mode < 8; mode++)
            for (var i = 0; i < Entered.Length; i++)
            {
                var (screen, input, platform, _) = Create(mode, trace);
                var restart = (mode & 1) != 0; var progress = new int[9];
                if ((mode & 2) != 0) for (var j = 0; j < 9; j++) progress[j] = j % 10;
                trace.AppendLine($"secret:{mode},{i}");
                platform.Events.Enqueue(new(InputEventKind.TextInput, Text: Encoding.ASCII.GetBytes(Entered[i]))); input.ServiceEvents(true);
                var result = screen.Secrets(progress, ref restart);
                trace.AppendLine($"secretstate:{(int)result},{Bit(restart)},{Bit(screen.LoadDestruct)},{string.Join(',', progress)}");
                input.ServiceEvents(true); result = screen.Secrets(progress, ref restart); trace.AppendLine($"notext:{(int)result}");
            }
        for (var mode = 0; mode < 144; mode++)
        {
            var (screen, _, platform, text) = Create(mode, trace);
            platform.Running = true; screen.MoveLogoUp = (mode & 16) != 0;
            trace.AppendLine($"run:{mode}"); var result = screen.Run();
            trace.AppendLine($"runstate:{Bit(result)},{Bit(screen.PlayDemo)},{Bit(screen.LoadDestruct)},{Bit(screen.MoveLogoUp)}," +
                $"{platform.Steps},{Encoding.ASCII.GetString(text.MenuText[4])}");
        }
        TestInputEdges();
        TestHost();
        if (executable is null) return;
        using var process = Process.Start(new ProcessStartInfo(executable) { RedirectStandardOutput = true, RedirectStandardError = true })
            ?? throw new InvalidOperationException("Cannot start title-screen oracle.");
        var output = process.StandardOutput.ReadToEndAsync(); var errors = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(30000)) { process.Kill(entireProcessTree: true); throw new TimeoutException("Title-screen oracle timed out."); }
        var expected = output.GetAwaiter().GetResult().Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var actual = trace.ToString().Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        if (process.ExitCode != 0 || !expected.SequenceEqual(actual))
        {
            var index = Enumerable.Range(0, Math.Min(expected.Length, actual.Length)).FirstOrDefault(i => expected[i] != actual[i], -1);
            throw new InvalidOperationException($"Title screen differs at {index}: {expected.ElementAtOrDefault(index)} / " +
                $"{actual.ElementAtOrDefault(index)}; {errors.GetAwaiter().GetResult()}");
        }
    }

    private static (TitleScreen Screen, InputState Input, Platform Platform, HelpTextData Text) Create(int mode, StringBuilder trace)
    {
        var text = new HelpTextData();
        for (var i = 0; i < 7; i++) text.MenuText[i] = Encoding.ASCII.GetBytes($"Menu{i}");
        for (var i = 0; i < 9; i++) Encoding.ASCII.GetBytes(Codes[i]).CopyTo(text.SpecialNames[i], 0);
        var platform = new Platform(mode, trace); var input = new InputState(platform, new VideoLayout());
        return (new(text, input, new Host(mode, trace, platform)), input, platform, text);
    }

    private static void SetInput(InputState input, Platform platform, bool key, bool mouse, int scan, int button,
        int x, int y, int clickX, int clickY)
    {
        input.ServiceEvents(true);
        platform.Events.Enqueue(new(InputEventKind.MouseMotion, X: x < 0 ? x - 1 : x, Y: y < 0 ? y - 1 : y)); input.ServiceEvents(false);
        if (key) { platform.Events.Enqueue(new(InputEventKind.KeyDown, scan)); input.ServiceEvents(false); }
        if (mouse)
        {
            platform.Events.Enqueue(new(InputEventKind.MouseButtonDown, Button: (byte)button, X: clickX, Y: clickY)); input.ServiceEvents(false);
        }
    }

    private static void TestInputEdges()
    {
        var (screen, input, platform, _) = Create(0, new()); int[] widths = [10, 13, 16, 19, 22, 25, 28];
        for (var row = 0; row < 7; row++)
        {
            var left = 160 - widths[row] / 2;
            foreach (var x in new[] { left - 1, left, left + widths[row] - 1, left + widths[row] })
                foreach (var delta in new[] { -1, 0, 12, 13 })
                {
                    var y = 104 + row * 13 + delta;
                    SetInput(input, platform, false, true, 0, 1, x, y, x, y);
                    screen.Selected = row;
                    var result = screen.Read(widths, false);
                    var hit = Enumerable.Range(0, 7).Any(i => x >= 160 - widths[i] / 2 &&
                        x < 160 - widths[i] / 2 + widths[i] && y >= 104 + i * 13 && y < 117 + i * 13);
                    if (result.Action != hit || result.Done) throw new InvalidOperationException("Title hover/click bounds differ.");
                }
        }
        SetInput(input, platform, true, true, 41, 1, 160, 104, 0, 0); screen.Selected = 0;
        if (screen.Read(widths, false) != default) throw new InvalidOperationException("Title used key or hover coordinates as a click.");
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        try { screen.Wait(out _, cancellation.Token); throw new InvalidOperationException("Title wait ignored cancellation."); }
        catch (OperationCanceledException) { }
    }

    private static void TestHost()
    {
        var shapes = new MainShapeTables(); using var data = new MemoryStream(); BinaryFile.WriteUInt16(data, 151);
        for (var i = 0; i < 151; i++)
        {
            BinaryFile.WriteBoolean(data, true); BinaryFile.WriteUInt16(data, 1); BinaryFile.WriteUInt16(data, 1);
            BinaryFile.WriteUInt16(data, 2); data.WriteByte(0xae); data.WriteByte(254);
        }
        foreach (var table in shapes.Tables) { data.Position = 0; table.Load(data); }
        var primary = new IndexedSurface(320, 200, 327, TextWriter.Null);
        var background = new IndexedSurface(320, 200, 327, TextWriter.Null);
        var expected = new IndexedSurface(320, 200, 327, TextWriter.Null);
        var font = new FontRenderer(shapes); var input = new InputState(new TextGlowTests.Platform(), new()); var timer = new DelayTimer(input);
        var sprites = new SpriteSheet(); var cursor = new MouseCursor(input, primary, sprites) { HasMouse = false, Cursor = 3 };
        var palette = new PaletteSystem(c => (uint)(c.Red << 16 | c.Green << 8 | c.Blue));
        var colors = Enumerable.Range(0, 256).Select(i => new PaletteColor((byte)i, (byte)i, (byte)i)).ToArray();
        var calls = new List<string>(); var samples = new List<byte>(); var presents = 0;
        var host = new TitleScreenHost(primary, background, font, shapes.Tables[7], sprites, cursor, palette, colors, timer, "Version"u8.ToArray(),
            () => calls.Add("cursor"), () => calls.Add("picture"), song => calls.Add($"song{song}"), () => presents++,
            () => calls.Add("push"), samples.Add, () => { calls.Add("new"); return true; }, () => { calls.Add("load"); return false; },
            () => calls.Add("scores"), () => calls.Add("help"), () => calls.Add("setup"), () => calls.Add("whoa"),
            () => calls.Add("super"), i => { calls.Add($"arcade{i}"); return true; });
        primary.Pixels.Span.Fill(0x75); expected.Pixels.Span.Fill(0x75);
        host.Version(); font.DrawShadow(expected, 2, 192, "Version"u8, Font.Small, FontAlignment.Left, 15, 0, false, 1);
        host.Logo(4); shapes.Tables[7].Blit(expected, 11, 4, 146);
        host.Reloaded(); font.DrawString(expected, 150, 80, "RELOADED"u8, Font.Large);
        host.Label(160, 104, "NEW"u8.ToArray(), true, -1); font.DrawHue(expected, 160, 104, "NEW"u8, Font.Normal, FontAlignment.Centered, 15, -1);
        host.Capture(); primary.Pixels.Span.Clear(); host.Restore();
        if (!primary.Pixels.Span.SequenceEqual(expected.Pixels.Span) || host.Width != 320 || host.TextWidth("NEW"u8.ToArray()) != font.TextWidth("NEW"u8, Font.Normal))
            throw new InvalidOperationException("Title concrete version/logo/label/saved rendering differs.");
        host.ResetCursor(); host.FadeIn(10, 0, 239); host.FadeIn(20, 240, 255);
        if (!palette.Active.Span.SequenceEqual(colors) || cursor.Cursor != 0 || presents != 30)
            throw new InvalidOperationException("Title concrete palette split differs.");
        host.SetDelay(2); host.ServiceWait(); host.Present(); host.PresentCursor(); host.FadeBlack(15);
        host.EnsureCursor(); host.PlayTitleSong(); host.LoadPicture(); host.PushJoysticks(); host.Sample(8);
        if (!host.NewGame() || host.LoadGame()) throw new InvalidOperationException("Title new/load return binding differs.");
        host.HighScores(); host.Instructions(); host.Setup(); host.Whoa(); host.SuperTyrian();
        if (!host.SuperArcade(6)) throw new InvalidOperationException("Title arcade return binding differs.");
        host.BlackColors();
        if (!calls.SequenceEqual(["cursor", "song29", "picture", "push", "new", "load", "scores", "help", "setup", "whoa", "super", "arcade6"]) ||
            !samples.SequenceEqual(new byte[] { 8 }) || presents != 47 || palette.Active.Span.ToArray().Any(color => color != default))
            throw new InvalidOperationException("Title concrete action/audio/input binding differs.");
    }

    internal static void RunSdl(SdlVideoDevice video)
    {
        var definitions = new ItemDefinitions(); definitions.ParseBundle(Encoding.Latin1.GetBytes(ItemDatabaseTests.Fixture(0)), "title-sdl.tyrr");
        var items = ItemDatabase.FromDefinitions(definitions); var game = new LegacyGameState { Difficulty = 2 };
        var text = new HelpTextData();
        for (var i = 0; i < 7; i++) text.MenuText[i] = Encoding.ASCII.GetBytes($"Menu{i}");
        foreach (var group in new[] { text.MiscText, text.GameplayNames, text.EpisodeNames, text.DifficultyNames, text.HighScoreDifficultyNames })
            for (var i = 0; i < group.Length; i++) group[i] = Encoding.ASCII.GetBytes($"Label{i}");
        for (var i = 0; i < 9; i++) Encoding.ASCII.GetBytes(Codes[i]).CopyTo(text.SpecialNames[i], 0);
        var shapes = new MainShapeTables(); using var data = new MemoryStream(); BinaryFile.WriteUInt16(data, 151);
        for (var i = 0; i < 151; i++)
        {
            BinaryFile.WriteBoolean(data, true); BinaryFile.WriteUInt16(data, 1); BinaryFile.WriteUInt16(data, 1);
            BinaryFile.WriteUInt16(data, 2); data.WriteByte(0xae); data.WriteByte(254);
        }
        foreach (var table in shapes.Tables) { data.Position = 0; table.Load(data); }
        var sprites = new SpriteSheet(); var bytes = new byte[663];
        for (var i = 0; i < 330; i++) System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(i * 2), 660);
        bytes[660] = 0x10; bytes[661] = 0xae; bytes[662] = 0x0f;
        var pushes = 0; var pictures = 0; var scores = 0; var loads = 0; var inits = 0; var songs = new List<uint>();
        int[] scans = [81, 81, 40, 41, 82, 82, 40, 40, 40, 40];
        SdlInputPlatform? platform = null;
        using var ownedPlatform = platform = new SdlInputPlatform(video,
            new Auxiliary(() =>
            {
                if (pushes >= scans.Length) throw new InvalidOperationException("SDL title/new-game path requested unexpected input.");
                platform!.PostEvent(new(InputEventKind.KeyDown, scans[pushes++]));
            }), () => throw new InvalidOperationException("Unexpected SDL title quit."));
        var input = new InputState(platform, video.Layout); input.FlushEvents();
        var timer = new DelayTimer(input); var font = new FontRenderer(shapes);
        var background = new IndexedSurface(320, 200, video.Primary.Pitch, TextWriter.Null);
        var cursor = new MouseCursor(input, video.Primary, sprites) { HasMouse = false };
        var colors = Enumerable.Range(0, 256).Select(i => new PaletteColor((byte)i, (byte)i, (byte)i)).ToArray();
        void Cursor() { if (sprites.Size != 0) return; using var stream = new MemoryStream(bytes); sprites.Load(stream, bytes.Length); loads++; }
        void Background() { background.Pixels.Span.Fill(0x75); pictures++; }
        void Fade(int steps) => video.Palette.FadeBlack(steps, () => timer.SetDelay(1), video.Present, timer.Wait);
        var selection = new SelectionMenuState();
        var selectionHost = new SelectionMenuPresentation(video.Primary, background, font, cursor, Cursor, Background,
            () => video.Palette.FadePalette(colors, 10, 0, 255, () => timer.SetDelay(1), video.Present, timer.Wait), video.Present,
            _ => { }, Fade, platform.PushJoysticksAsKeyboard, () => { }, () => input.LastModifiers, episode => { inits++; return episode; });
        var newHost = new NewGameHost(game, new(input, selectionHost, selection), selection, text, [true, true, true, true],
            video.Primary, font, sprites, video.Palette, colors, input, timer, Background, video.Present, songs.Add, _ => { }, episode => episode);
        var newGame = new NewGame(game, items, text, input, newHost);
        using var cancellation = new CancellationTokenSource(10000);
        var scoreHost = new HighScoreScreenHost(video.Primary, background, font, new(font), sprites, cursor, video.Palette, colors,
            timer, Cursor, Background, video.Present, platform.PushJoysticksAsKeyboard, _ => { });
        var scoreViewer = new HighScoreScreen(new(), text, [true, true, true], input, scoreHost);
        var titleHost = new TitleScreenHost(video.Primary, background, font, shapes.Tables[7], sprites, cursor, video.Palette,
            colors, timer, "Version"u8.ToArray(), Cursor, () => { video.Primary.Pixels.Span.Fill(0x75); pictures++; }, songs.Add,
            video.Present, platform.PushJoysticksAsKeyboard, _ => { }, newGame.Run,
            () => throw new InvalidOperationException("Unexpected load action."),
            () => { scores++; scoreViewer.Run(cancellation.Token); },
            () => throw new InvalidOperationException("Unexpected help action."),
            () => throw new InvalidOperationException("Unexpected setup action."),
            () => throw new InvalidOperationException("Unexpected whoa action."),
            () => newGame.RunSuperTyrian(cancellation.Token), mode => newGame.RunSuperArcade(mode, cancellation.Token));
        var title = new TitleScreen(text, input, titleHost);
        var introPictures = new List<int>();
        new IntroLogos(() => title.MoveLogoUp = true,
            new IntroLogoHost(video.Primary, video.Palette, colors, timer,
                number => { introPictures.Add(number); platform.PostEvent(new(InputEventKind.KeyDown, 40)); },
                video.Present)).Run(cancellation.Token);
        byte warningLines = 9; var bleedFrames = 0;
        new ScreenBleed(video.Primary, new(320, 200, video.Primary.Pitch, TextWriter.Null), background, input, timer,
            () => { video.Present(); if (++bleedFrames == 2) platform.PostEvent(new(InputEventKind.KeyDown, 40)); },
            value => warningLines = value).Run(cancellation.Token);
        if (!introPictures.SequenceEqual([10, 12]) || warningLines != 4 || bleedFrames != 2)
            throw new InvalidOperationException("Actual SDL logo/bleed input and presentation integration differs.");
        if (!title.Run(cancellation.Token) || pushes != 10 || scores != 1 || loads != 1 || inits != 1 || pictures != 6 ||
            !songs.SequenceEqual([29u, 29u]) || !game.GameLoaded || game.Episode != 1 || game.InitialEpisode != 1 ||
            game.Difficulty != 2 || game.InitialDifficulty != 2 || game.Players[0].Cash != 10000 ||
            title.MoveLogoUp || title.PlayDemo || title.LoadDestruct || Encoding.ASCII.GetString(text.MenuText[4]) != "Setup")
            throw new InvalidOperationException("Actual SDL title animation/high-score/new-game path did not initialize native-compatible state.");
        input.FlushEvents();
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
    private sealed class Host(int mode, StringBuilder trace, Platform platform) : ITitleScreenHost
    {
        public int Width => 320;
        public void EnsureCursor() { if ((mode & 1) == 0) trace.AppendLine("cursor:1"); }
        public void PlayTitleSong() => trace.AppendLine("song:29");
        public void LoadPicture() => trace.AppendLine("picture:4");
        public void Version() => trace.AppendLine("version:2,192,Version,2,0,1");
        public void Capture() { }
        public void Restore() { }
        public void Logo(int y) => trace.AppendLine($"logo:{y},146");
        public void FadeIn(int steps, int first, int last) => trace.AppendLine($"fadein:{steps},{first},{last}");
        public void SetDelay(int ticks) => trace.AppendLine($"setdelay:{ticks}");
        public void Present() => trace.AppendLine("present");
        public void ServiceWait() => trace.AppendLine("wait");
        public void Reloaded() => trace.AppendLine("reloaded:RELOADED");
        public int TextWidth(ReadOnlyMemory<byte> text) { trace.AppendLine($"width:{Encoding.ASCII.GetString(text.Span)}"); return text.Length * 2; }
        public void Label(int x, int y, ReadOnlyMemory<byte> text, bool centered, sbyte brightness) =>
            trace.AppendLine($"label:{x},{y},{Encoding.ASCII.GetString(text.Span)},{Bit(centered)},{brightness}");
        public void ResetCursor() { }
        public void PresentCursor() { trace.AppendLine("mouse:240"); trace.AppendLine("present"); trace.AppendLine("replace"); }
        public void PushJoysticks() => platform.PushJoysticksAsKeyboard();
        public void Sample(byte number) => trace.AppendLine($"sample:{number}");
        public void FadeBlack(int steps) => trace.AppendLine($"fade:{steps}");
        public bool NewGame() { trace.AppendLine("new"); return (mode & 1) != 0; }
        public bool LoadGame() { trace.AppendLine("load"); return (mode & 2) != 0; }
        public void HighScores() => trace.AppendLine("scores");
        public void Instructions() => trace.AppendLine("help:1");
        public void Setup() => trace.AppendLine("setup");
        public void Whoa() => trace.AppendLine("whoa");
        public void BlackColors() => trace.AppendLine("black:0,255");
        public void SuperTyrian() => trace.AppendLine("super");
        public bool SuperArcade(int ship) { trace.AppendLine($"arcade:{ship}"); return (mode & 4) != 0; }
    }

    private sealed class Platform(int mode, StringBuilder trace) : IInputPlatform
    {
        public Queue<InputEvent> Events { get; } = new();
        public bool Running { get; set; }
        public int Steps { get; private set; }
        private int _waits;
        private int _mouseX = 10;
        public uint Ticks { get; set; }
        public bool JoystickDown => false;
        public bool IsNetworkGame => false;
        public bool PollEvent(out InputEvent input) => Events.TryDequeue(out input);
        public void Delay(uint milliseconds) { trace.AppendLine($"delay:{milliseconds}"); Ticks = unchecked(Ticks + milliseconds); }
        public void PushJoysticksAsKeyboard()
        {
            trace.AppendLine("joystick");
            if (!Running)
            {
                if (mode == 0 || ++_waits < mode % 4 + 1) return;
                if (mode == 1) Events.Enqueue(new(InputEventKind.KeyDown, 40));
                else if (mode == 2) Events.Enqueue(new(InputEventKind.TextInput, Text: "a"u8.ToArray()));
                else if (mode == 3) Events.Enqueue(new(InputEventKind.MouseButtonDown, Button: 1));
                else
                {
                    if (mode >= 5 && _waits == mode % 4 + 1) _mouseX = -1;
                    var x = ++_mouseX;
                    Events.Enqueue(new(InputEventKind.MouseMotion, X: x < 0 ? x - 1 : x, Y: mode >= 6 ? -2 : 10));
                }
                return;
            }
            var scenario = mode % 12;
            if (scenario == 11) return;
            if (scenario < 7) Events.Enqueue(new(InputEventKind.KeyDown, Steps < scenario ? 81 : Steps == scenario ? 40 : 41));
            else if (scenario == 7) Events.Enqueue(new(InputEventKind.MouseButtonDown, Button: 3));
            else if (scenario is 8 or 9)
                Events.Enqueue(new(InputEventKind.TextInput, Text: Encoding.ASCII.GetBytes(scenario == 8 ? "destruct" : "engage")));
            else if (Steps == 0) Events.Enqueue(new(InputEventKind.TextInput, Text: "ship"u8.ToArray()));
            else Events.Enqueue(new(InputEventKind.KeyDown, 41));
            Steps++;
        }
        public void SetRelativeMouseMode(bool enabled) { }
        public void SetCursorVisible(bool visible) { }
        public void EnableSystemMouseScaling() { }
        public void ToggleFullscreen() { }
        public void WindowResized() { }
        public void PollJoysticks() { }
        public void CheckNetwork() { }
        public void Quit() => throw new InvalidOperationException("Unexpected title quit.");
    }
}
