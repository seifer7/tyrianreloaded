using System.Diagnostics;
using System.Text;
using OpenTyrian.Core;

namespace OpenTyrian.Core.Tests;

internal static class HelpSystemTests
{
    public static void Run(string? executable = null)
    {
        var trace = new StringBuilder();
        for (var mode = 0; mode < 144; mode++)
        {
            var (system, platform, _) = Create(mode, trace); platform.Pager = true;
            var topic = (byte)(mode / 12 % 6 + 1); var restart = (mode & 1) != 0;
            trace.AppendLine($"pages:{mode}");
            var result = system.ShowPages(ref topic, ref restart);
            trace.AppendLine($"pagestate:{Bit(result)},{topic},{Bit(restart)},{platform.Steps}");
        }
        for (var mode = 0; mode < 304; mode++)
        {
            var (system, platform, host) = Create(mode, trace);
            trace.AppendLine($"run:{mode}"); system.Run((byte)(mode >= 144 ? 1 : mode / 12 % 6 + 1));
            trace.AppendLine($"runstate:{platform.Steps},{host.Brightness},{host.Height}");
        }
        if (executable != null)
        {
            using var process = Process.Start(new ProcessStartInfo(executable) { RedirectStandardOutput = true, RedirectStandardError = true })
                ?? throw new InvalidOperationException("Cannot start help-system oracle.");
            var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(30000)) { process.Kill(entireProcessTree: true); throw new TimeoutException("Help-system oracle timed out."); }
            var expected = output.GetAwaiter().GetResult().Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
            var actual = trace.ToString().Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
            if (process.ExitCode != 0 || !expected.SequenceEqual(actual))
            {
                var index = Enumerable.Range(0, Math.Min(expected.Length, actual.Length)).FirstOrDefault(i => expected[i] != actual[i], -1);
                throw new InvalidOperationException($"Help-system trace differs at {index}: {expected.ElementAtOrDefault(index)} / " +
                    $"{actual.ElementAtOrDefault(index)}; {error.GetAwaiter().GetResult()}");
            }
        }
        TestHost();
        var (canceled, _, _) = Create(0, new());
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        try { canceled.Run(cancellation: cancellation.Token); throw new InvalidOperationException("Help menu ignored cancellation."); }
        catch (OperationCanceledException) { }
        byte canceledTopic = 2; var canceledRestart = true;
        try
        {
            canceled.ShowPages(ref canceledTopic, ref canceledRestart, cancellation.Token);
            throw new InvalidOperationException("Help pages ignored cancellation.");
        }
        catch (OperationCanceledException) { }
        foreach (var topic in new byte[] { 0, 7, 255 })
        {
            try { canceled.Run(topic); throw new InvalidOperationException("Help menu accepted an invalid topic."); }
            catch (ArgumentOutOfRangeException) { }
        }
    }

    private static HelpTextData Text()
    {
        var text = new HelpTextData();
        for (var i = 0; i < text.TopicNames.Length; i++) text.TopicNames[i] = Encoding.ASCII.GetBytes($"Topic{i}");
        for (var i = 0; i < text.MiscText.Length; i++) text.MiscText[i] = Encoding.ASCII.GetBytes($"Text{i}");
        for (var i = 0; i < text.Help.Length; i++) text.Help[i] = Encoding.ASCII.GetBytes($"Help{i} words");
        return text;
    }

    private static (HelpSystem System, Platform Platform, Host Host) Create(int mode, StringBuilder trace)
    {
        var platform = new Platform(mode, trace); var input = new InputState(platform, new()); platform.Input = input;
        platform.Events.Enqueue(new(InputEventKind.MouseMotion, X: 10, Y: 10)); input.ServiceEvents(true);
        var host = new Host(mode, trace, platform);
        return (new(Text(), input, host), platform, host);
    }

    private static void TestHost()
    {
        var font = new FontRenderer(HighScoreScreenTests.CreateShapes()); var text = Text();
        var help = new HelpTextRenderer(font);
        var primary = new IndexedSurface(320, 200, 327, TextWriter.Null);
        var background = new IndexedSurface(320, 200, 327, TextWriter.Null);
        var expected = new IndexedSurface(320, 200, 327, TextWriter.Null);
        var input = new InputState(new TextGlowTests.Platform(), new()); var timer = new DelayTimer(input);
        var sprites = new SpriteSheet(); var bytes = HighScoreScreenTests.SpriteBytes();
        var cursor = new MouseCursor(input, primary, sprites) { HasMouse = false };
        var palette = new PaletteSystem(c => (uint)(c.Red << 16 | c.Green << 8 | c.Blue));
        var colors = Enumerable.Range(0, 256).Select(i => new PaletteColor((byte)i, (byte)i, (byte)i)).ToArray();
        var presents = 0; var pictures = 0; var loads = 0; var pushes = 0; var songs = new List<uint>(); var samples = new List<byte>();
        var host = new HelpSystemHost(primary, background, font, help, text, sprites, cursor, palette, colors, timer,
            () => { using var stream = new MemoryStream(bytes); sprites.Load(stream, bytes.Length); loads++; },
            () => { background.Pixels.Span.Fill(0x75); pictures++; }, songs.Add, () => presents++, () => pushes++, samples.Add);
        host.EnsureCursor(); host.EnsureCursor(); host.PlaySong(); host.LoadBackground(); host.RestoreBackground(); host.ClearFooter();
        expected.Pixels.Span.Fill(0x75); expected.FillRectangleSize(0, 192, 320, 8, 0);
        host.Header(1, "Topic1"u8.ToArray(), Font.Normal, -3);
        font.DrawShadow(expected, 160, 1, "Topic1"u8, Font.Normal, FontAlignment.Centered, 15, -3, false, 2);
        host.Footer(10, "Topic 1"u8.ToArray(), FontAlignment.Left);
        font.DrawHue(expected, 10, 192, "Topic 1"u8, Font.Small, FontAlignment.Left, 13, 5);
        host.Footer(310, "Page 1 of 8"u8.ToArray(), FontAlignment.Right);
        font.DrawHue(expected, 310, 192, "Page 1 of 8"u8, Font.Small, FontAlignment.Right, 13, 5);
        host.TextStyle(3, 8); host.Box(20, 2);
        new HelpTextRenderer(font) { Brightness = 3, VerticalHeight = 8 }.DrawHelp(expected, 10, 20, text, 2, 60);
        host.TextStyle(1, 7);
        if (!primary.Pixels.Span.SequenceEqual(expected.Pixels.Span) || loads != 1 || pictures != 1 ||
            host.TextWidth("Topic1"u8.ToArray()) != font.TextWidth("Topic1"u8, Font.Normal) || !songs.SequenceEqual([19u]))
            throw new InvalidOperationException("Help-system concrete heading/footer/text/background rendering differs.");
        host.Cursor(4); host.FadeIn(); host.PresentCursor(); host.PushJoysticks(); host.Sample(28);
        if (!palette.Active.Span.SequenceEqual(colors) || cursor.Cursor != 4 || presents != 11 || pushes != 1 ||
            !samples.SequenceEqual(new byte[] { 28 }) || help.Brightness != 1 || help.VerticalHeight != 7)
            throw new InvalidOperationException("Help-system concrete palette, cursor, audio or text-style restore differs.");
        host.FadeBlack();
        if (presents != 26 || palette.Active.Span.ToArray().Any(color => color != default))
            throw new InvalidOperationException("Help-system did not fade black on exit.");
    }

    internal static void RunSdl(SdlVideoDevice video)
    {
        var pushes = 0; var pictures = 0; var instructions = 0; var songs = new List<uint>();
        var samples = new List<byte>(); int[] scans = [81, 81, 81, 40, 40, 79, 79, 79, 79, 79, 79, 79, 79, 40, 41];
        SdlInputPlatform? platform = null;
        using var ownedPlatform = platform = new SdlInputPlatform(video, new Auxiliary(() =>
        {
            if (pushes >= scans.Length) throw new InvalidOperationException("SDL title/help path requested unexpected input.");
            platform!.PostEvent(new(InputEventKind.KeyDown, scans[pushes++]));
        }), () => throw new InvalidOperationException("Unexpected SDL help-system quit."));
        var input = new InputState(platform, video.Layout); input.FlushEvents();
        var text = Text(); for (var i = 0; i < text.MenuText.Length; i++) text.MenuText[i] = Encoding.ASCII.GetBytes($"Menu{i}");
        var shapes = HighScoreScreenTests.CreateShapes(151); var font = new FontRenderer(shapes);
        var help = new HelpTextRenderer(font); var timer = new DelayTimer(input);
        var sprites = new SpriteSheet(); var bytes = HighScoreScreenTests.SpriteBytes();
        var background = new IndexedSurface(320, 200, video.Primary.Pitch, TextWriter.Null);
        var cursor = new MouseCursor(input, video.Primary, sprites) { HasMouse = false };
        var colors = Enumerable.Range(0, 256).Select(i => new PaletteColor((byte)i, (byte)i, (byte)i)).ToArray();
        void Cursor() { if (sprites.Size != 0) return; using var stream = new MemoryStream(bytes); sprites.Load(stream, bytes.Length); }
        var helpHost = new HelpSystemHost(video.Primary, background, font, help, text, sprites, cursor, video.Palette, colors, timer,
            Cursor, () => { background.Pixels.Span.Fill(0x75); pictures++; }, songs.Add, video.Present, platform.PushJoysticksAsKeyboard, samples.Add);
        using var cancellation = new CancellationTokenSource(10000);
        var system = new HelpSystem(text, input, helpHost);
        bool Unexpected() => throw new InvalidOperationException("Unexpected SDL title child action.");
        void UnexpectedVoid() => throw new InvalidOperationException("Unexpected SDL title child action.");
        var title = new TitleScreen(text, input, new TitleScreenHost(video.Primary, background, font, shapes.Tables[7],
            sprites, cursor, video.Palette, colors, timer, "Version"u8.ToArray(), Cursor,
            () => { video.Primary.Pixels.Span.Fill(0x75); pictures++; }, songs.Add, video.Present, platform.PushJoysticksAsKeyboard,
            samples.Add, Unexpected, Unexpected, UnexpectedVoid, () => { instructions++; system.Run(cancellation: cancellation.Token); },
            UnexpectedVoid, UnexpectedVoid, UnexpectedVoid, _ => Unexpected()));
        if (title.Run(cancellation.Token) || pushes != 15 || instructions != 1 || pictures != 3 ||
            !songs.SequenceEqual([29u, 19u, 29u]) || samples.Count(number => number == 28) != 11 ||
            samples.Count(number => number == 8) != 3 || samples.Count(number => number == 16) != 1 ||
            help.Brightness != 1 || help.VerticalHeight != 7 || video.Palette.Active.Span.ToArray().Any(color => color != default))
            throw new InvalidOperationException("Actual SDL title/help/all-eight-pages/return-to-title path differs.");
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

    private sealed class Host(int mode, StringBuilder trace, Platform platform) : IHelpSystemHost
    {
        private byte _cursor = (byte)(mode % 5);
        public byte Brightness { get; private set; } = 1;
        public byte Height { get; private set; } = 7;
        public void EnsureCursor() { if ((mode & 1) == 0) trace.AppendLine("cursor:1"); }
        public void PlaySong() => trace.AppendLine("song:19");
        public void LoadBackground() => trace.AppendLine("picture:2");
        public void RestoreBackground() { }
        public void ClearFooter() => trace.AppendLine("fill:0,192,320,8,0");
        public int TextWidth(ReadOnlyMemory<byte> text) { trace.AppendLine($"width:{Encoding.Latin1.GetString(text.Span)}"); return text.Length * 2; }
        public void Header(int y, ReadOnlyMemory<byte> text, Font font, sbyte brightness)
        {
            if (y == 1) platform.Pager = true; else if (y == 30) platform.Pager = false;
            trace.AppendLine($"header:{y},{Encoding.Latin1.GetString(text.Span)},{(int)font},{brightness}");
        }
        public void Footer(int x, ReadOnlyMemory<byte> text, FontAlignment alignment) =>
            trace.AppendLine($"footer:{x},{Encoding.Latin1.GetString(text.Span)},{(int)alignment}");
        public void TextStyle(byte brightness, byte height) { Brightness = brightness; Height = height; }
        public void Box(int y, uint number) => trace.AppendLine($"box:10,{y},{number},60,{Brightness},{Height}");
        public void Cursor(byte shape) => _cursor = shape;
        public void FadeIn() => trace.AppendLine($"fadein:10,0,255,{_cursor},{Brightness},{Height}");
        public void PresentCursor() { trace.AppendLine($"mouse:{_cursor},{Brightness},{Height}"); trace.AppendLine("present"); trace.AppendLine("replace"); }
        public void PushJoysticks() => platform.PushJoysticksAsKeyboard();
        public void Sample(byte number) => trace.AppendLine($"sample:{number}");
        public void FadeBlack() => trace.AppendLine("fade:15");
    }

    private sealed class Platform(int mode, StringBuilder trace) : IInputPlatform
    {
        public Queue<InputEvent> Events { get; } = new();
        public InputState? Input { get; set; }
        public bool Pager { get; set; }
        public int Steps { get; private set; }
        private int _waits;
        public uint Ticks { get; private set; }
        public bool JoystickDown => false;
        public bool IsNetworkGame => false;
        public bool PollEvent(out InputEvent input) => Events.TryDequeue(out input);
        public void Delay(uint milliseconds) { trace.AppendLine($"delay:{milliseconds}"); Ticks += milliseconds; }
        public void PushJoysticksAsKeyboard()
        {
            trace.AppendLine("joystick"); if (++_waits < mode % 3 + 1) return; _waits = 0;
            var kind = mode % 12;
            if (mode >= 144)
            {
                Key(41);
                if (Steps == 0)
                {
                    Drain();
                    var edge = mode - 144; int[] xs = [153, 154, 165, 166]; int[] dy = [-1, 0, 12, 13];
                    Mouse(1, xs[edge / 8 % 4], 60 + edge / 32 % 5 * 20 + dy[edge / 2 % 4], (edge & 1) != 0);
                    Drain();
                }
            }
            else if (Steps >= 24) Key(41);
            else if (Pager)
            {
                if (kind < 5) { int[] scans = [80, 79, 44, 40, 88]; Key(scans[kind]); }
                else if (kind < 9) Mouse(kind == 8 ? 3 : 1, kind == 5 ? 159 : kind == 6 ? 160 : 300);
                else if (kind == 9) { Key(41); Drain(); Mouse(2); Drain(); }
                else if (kind == 10) Key(0);
                else Mouse(2);
            }
            else
            {
                if (kind < 2) Key(Steps == 0 ? 40 : Steps == 1 ? 80 : 41);
                else if (kind < 7) { int[] scans = [82, 81, 44, 40, 88]; Key(scans[kind - 2]); }
                else if (kind == 7) Mouse(1, 160, 60 + Steps % 5 * 20, true);
                else if (kind == 8) Mouse(3);
                else if (kind == 9) { Key(41); Drain(); Mouse(1, 160, 60); Drain(); }
                else if (kind == 10) Key(0);
                else Events.Enqueue(new(InputEventKind.MouseMotion, X: (Steps & 1) != 0 ? -2 : 160, Y: 60 + Steps % 5 * 20));
            }
            Steps++;
        }
        private void Key(int scan) => Events.Enqueue(new(InputEventKind.KeyDown, scan));
        private void Drain() => (Input ?? throw new InvalidOperationException("Missing scripted help input.")).ServiceEvents(false);
        private void Mouse(int button, int? x = null, int y = 10, bool clickedAtHover = false)
        {
            if (x != null) Events.Enqueue(new(InputEventKind.MouseMotion, X: x.Value, Y: y));
            Events.Enqueue(new(InputEventKind.MouseButtonDown, Button: (byte)button,
                X: clickedAtHover ? x!.Value : 0, Y: clickedAtHover ? y : 0));
        }
        public void SetRelativeMouseMode(bool enabled) { }
        public void SetCursorVisible(bool visible) { }
        public void EnableSystemMouseScaling() { }
        public void ToggleFullscreen() { }
        public void WindowResized() { }
        public void PollJoysticks() { }
        public void CheckNetwork() { }
        public void Quit() => throw new InvalidOperationException("Unexpected help-system quit.");
    }
}
