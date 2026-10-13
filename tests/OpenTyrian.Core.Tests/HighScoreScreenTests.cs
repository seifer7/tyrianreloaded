using System.Buffers.Binary;
using System.Diagnostics;
using System.Text;
using OpenTyrian.Core;

namespace OpenTyrian.Core.Tests;

internal static class HighScoreScreenTests
{
    public static void Run(string? executable = null)
    {
        var text = new HelpTextData();
        Fill(text.MiscText, "Text"); Fill(text.EpisodeNames, "Episode"); Fill(text.HighScoreDifficultyNames, "Rank");
        int[] scores = [0, -1, int.MinValue, int.MaxValue, 123456789, -987654321];
        for (var mode = 0; mode < 256; mode++)
        {
            var trace = new StringBuilder();
            var archive = new LegacySaveArchive();
            for (var i = 0; i < archive.Slots.Length; i++)
            {
                archive.Slots[i].HighScore1 = scores[(mode + i) % 6];
                archive.Slots[i].HighScoreDifficulty = unchecked((byte)(mode + i));
                archive.Slots[i].HighScoreName = Encoding.ASCII.GetBytes($"Name{i}");
            }
            var original = archive.Slots.Select(slot => (slot.HighScore1, slot.HighScoreDifficulty, Convert.ToHexString(slot.HighScoreName))).ToArray();
            var platform = new Platform(trace, mode);
            new HighScoreScreen(archive, text, Enumerable.Range(0, 5).Select(i => (mode & 1 << i) != 0).ToArray(),
                new(platform, new()), new Host(trace, mode, platform)).Run();
            trace.AppendLine($"steps:{platform.Steps}");
            if (!original.SequenceEqual(archive.Slots.Select(slot => (slot.HighScore1, slot.HighScoreDifficulty, Convert.ToHexString(slot.HighScoreName)))))
                throw new InvalidOperationException("Score viewing changed or sorted the archive.");
            if (executable is null) continue;
            using var process = Process.Start(new ProcessStartInfo(executable, mode.ToString(System.Globalization.CultureInfo.InvariantCulture))
            { RedirectStandardOutput = true, RedirectStandardError = true }) ?? throw new InvalidOperationException("Cannot start score-page oracle.");
            var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(30000)) { process.Kill(entireProcessTree: true); throw new TimeoutException("Score-page oracle timed out."); }
            var expected = output.GetAwaiter().GetResult().Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
            var actual = trace.ToString().Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
            if (process.ExitCode != 0 || !expected.SequenceEqual(actual))
            {
                var index = Enumerable.Range(0, Math.Min(expected.Length, actual.Length)).FirstOrDefault(i => expected[i] != actual[i], -1);
                throw new InvalidOperationException($"Score page {mode} differs at {index}: {expected.ElementAtOrDefault(index)} / " +
                    $"{actual.ElementAtOrDefault(index)}; {error.GetAwaiter().GetResult()}");
            }
        }
        TestHost();
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        var traceCanceled = new StringBuilder(); var canceledPlatform = new Platform(traceCanceled, 0);
        try
        {
            new HighScoreScreen(new(), text, [true, false, true], new(canceledPlatform, new()),
                new Host(traceCanceled, 0, canceledPlatform)).Run(cancellation.Token);
            throw new InvalidOperationException("Score paging ignored cancellation.");
        }
        catch (OperationCanceledException) { }
    }

    internal static void RunSdl(SdlVideoDevice video)
    {
        var text = new HelpTextData(); Fill(text.MiscText, "Text"); Fill(text.EpisodeNames, "Episode"); Fill(text.HighScoreDifficultyNames, "Rank");
        SdlInputPlatform? platform = null;
        var pushes = 0;
        var auxiliary = new Auxiliary(() =>
        {
            int[] scans = [80, 79, 40];
            platform!.PostEvent(new(InputEventKind.KeyDown, scans[pushes++]));
        });
        using var ownedPlatform = platform = new SdlInputPlatform(video, auxiliary,
            () => throw new InvalidOperationException("Unexpected SDL score-page quit."));
        var input = new InputState(platform, video.Layout); input.FlushEvents();
        var sprites = new SpriteSheet(); var bytes = SpriteBytes();
        var background = new IndexedSurface(320, 200, video.Primary.Pitch, TextWriter.Null);
        var font = new FontRenderer(CreateShapes()); var cursor = new MouseCursor(input, video.Primary, sprites) { HasMouse = false };
        var colors = Enumerable.Range(0, 256).Select(i => new PaletteColor((byte)i, (byte)i, (byte)i)).ToArray();
        var samples = new List<byte>(); var headers = new List<byte>(); var pictures = 0; var loads = 0;
        var x = font.Center(text.EpisodeNames[1], Font.Normal);
        var host = new HighScoreScreenHost(video.Primary, background, font, new(font), sprites, cursor, video.Palette,
            colors, new(input), () => { using var stream = new MemoryStream(bytes); sprites.Load(stream, bytes.Length); loads++; },
            () => { background.Pixels.Span.Fill(0x75); pictures++; },
            () => { headers.Add(video.Primary.Pixels.Span[30 * video.Primary.Pitch + x]); video.Present(); },
            platform.PushJoysticksAsKeyboard, samples.Add);
        using var cancellation = new CancellationTokenSource(5000);
        new HighScoreScreen(new(), text, [true, false, false], input, host).Run(cancellation.Token);
        if (pushes != 3 || pictures != 1 || loads != 1 || !samples.SequenceEqual(new byte[] { 28, 28, 16 }) ||
            !headers.Contains(0xf7) || headers.Count != 28 || video.Palette.Active.Span.ToArray().Any(color => color != default) ||
            background.Pixels.Span[(192 * background.Pitch)..].ContainsAnyExcept((byte)0))
            throw new InvalidOperationException($"Actual SDL score paging differs: pushes={pushes}, pictures={pictures}, loads={loads}, " +
                $"samples={string.Join(',', samples)}, headers={string.Join(',', headers)}, " +
                $"paletteBlack={video.Palette.Active.Span.ToArray().All(color => color == default)}, " +
                $"stripBlack={!background.Pixels.Span[(192 * background.Pitch)..].ContainsAnyExcept((byte)0)}.");
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

    private static void TestHost()
    {
        var shapes = CreateShapes();
        var sprites = new SpriteSheet(); var bytes = SpriteBytes();
        var primary = new IndexedSurface(320, 200, 327, TextWriter.Null);
        var background = new IndexedSurface(320, 200, 327, TextWriter.Null);
        var expected = new IndexedSurface(320, 200, 327, TextWriter.Null);
        var font = new FontRenderer(shapes); var help = new HelpTextRenderer(font);
        var input = new InputState(new TextGlowTests.Platform(), new()); var timer = new DelayTimer(input);
        var cursor = new MouseCursor(input, primary, sprites) { HasMouse = false, Cursor = 3 };
        var palette = new PaletteSystem(c => (uint)(c.Red << 16 | c.Green << 8 | c.Blue));
        var colors = Enumerable.Range(0, 256).Select(i => new PaletteColor((byte)i, (byte)i, (byte)i)).ToArray();
        var loads = 0; var pictures = 0; var presents = 0; var pushes = 0; var samples = new List<byte>();
        var host = new HighScoreScreenHost(primary, background, font, help, sprites, cursor, palette, colors, timer,
            () => { using var stream = new MemoryStream(bytes); sprites.Load(stream, bytes.Length); loads++; },
            () => { background.Pixels.Span.Fill(0x75); pictures++; }, () => presents++, () => pushes++, samples.Add);
        host.EnsureCursor(); host.EnsureCursor(); host.LoadBackground();
        expected.Pixels.Span.Fill(0x75); expected.FillRectangleSize(0, 192, 320, 8, 0);
        host.Header(true, 3, "TITLE"u8.ToArray(), Font.Large, -3);
        font.DrawShadow(expected, 160, 3, "TITLE"u8, Font.Large, FontAlignment.Centered, 15, -3, false, 2);
        host.RestoreBackground();
        if (!primary.Pixels.Span.SequenceEqual(expected.Pixels.Span)) throw new InvalidOperationException("Score background/strip/header differs.");
        var savedPixels = background.Pixels.ToArray();
        host.Header(false, 30, "DISABLED"u8.ToArray(), Font.Normal, -7);
        font.DrawShadow(expected, 160, 30, "DISABLED"u8, Font.Normal, FontAlignment.Centered, 15, -7, false, 2);
        host.Shaded(20, 75, "~#1:~  -2147483648"u8.ToArray(), 0);
        font.ShadeText(expected, 20, 75, "~#1:~  -2147483648"u8, 15, 0, TextShade.Full);
        host.Arrow(213, 281); sprites.Blit2x2(expected, 213, 179, 281);
        host.Help("Done now"u8.ToArray()); var expectedHelp = new HelpTextRenderer(font) { Color = 15 };
        expectedHelp.DrawBox(expected, 103, 182, "Done now"u8, 25);
        if (!primary.Pixels.Span.SequenceEqual(expected.Pixels.Span) || !background.Pixels.Span.SequenceEqual(savedPixels) ||
            loads != 1 || pictures != 1)
            throw new InvalidOperationException("Score font/arrow/help rendering differs.");
        host.FadeIn(); host.PresentCursor(); host.PushJoysticks(); host.Sample(28);
        if (!palette.Active.Span.SequenceEqual(colors) || cursor.Cursor != 0 || pushes != 1 ||
            !samples.SequenceEqual(new byte[] { 28 }) || presents != 11)
            throw new InvalidOperationException("Score host palette/input/audio routing differs.");
        host.FadeBlack();
        if (palette.Active.Span.ToArray().Any(color => color.Red != 0 || color.Green != 0 || color.Blue != 0) || presents != 26)
            throw new InvalidOperationException("Score exit did not fade black.");
    }

    private static void Fill(byte[][] entries, string prefix)
    {
        for (var i = 0; i < entries.Length; i++) entries[i] = Encoding.ASCII.GetBytes($"{prefix}{i}");
    }

    internal static MainShapeTables CreateShapes(ushort count = 127)
    {
        var shapes = new MainShapeTables(); using var data = new MemoryStream(); BinaryFile.WriteUInt16(data, count);
        for (var i = 0; i < count; i++)
        {
            BinaryFile.WriteBoolean(data, true); BinaryFile.WriteUInt16(data, 1); BinaryFile.WriteUInt16(data, 1);
            BinaryFile.WriteUInt16(data, 2); data.WriteByte(0xae); data.WriteByte(254);
        }
        foreach (var table in shapes.Tables) { data.Position = 0; table.Load(data); }
        return shapes;
    }

    internal static byte[] SpriteBytes()
    {
        var bytes = new byte[663];
        for (var i = 0; i < 330; i++) BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(i * 2), 660);
        bytes[660] = 0x10; bytes[661] = 0xae; bytes[662] = 0x0f;
        return bytes;
    }

    private sealed class Host(StringBuilder trace, int mode, Platform platform) : IHighScoreScreenHost
    {
        public void EnsureCursor() { if ((mode & 1) == 0) trace.AppendLine("cursor:1"); }
        public void LoadBackground() { trace.AppendLine("picture:2"); trace.AppendLine("fill:0,192,320,8,0"); }
        public void RestoreBackground() { }
        public void Header(bool background, int y, ReadOnlyMemory<byte> text, Font font, sbyte brightness) =>
            trace.AppendLine($"header:{(background ? 1 : 0)},{y},{Encoding.ASCII.GetString(text.Span)},{(int)font},{brightness}");
        public void Shaded(int x, int y, ReadOnlyMemory<byte> text, int brightness) =>
            trace.AppendLine($"shaded:{x},{y},{Encoding.ASCII.GetString(text.Span)},{brightness}");
        public void Arrow(int x, int sprite) => trace.AppendLine($"arrow:{x},{sprite}");
        public void Help(ReadOnlyMemory<byte> text) => trace.AppendLine($"help:{Encoding.ASCII.GetString(text.Span)},15");
        public void FadeIn() => trace.AppendLine("fadein:10,0");
        public void PresentCursor() { trace.AppendLine("mouse"); trace.AppendLine("present"); trace.AppendLine("replace"); }
        public void PushJoysticks() => platform.PushJoysticksAsKeyboard();
        public void Sample(byte number) => trace.AppendLine($"sample:{number}");
        public void FadeBlack() => trace.AppendLine("fade:15");
    }

    private sealed class Platform(StringBuilder trace, int mode) : IInputPlatform
    {
        private readonly Queue<InputEvent> _events = new();
        private int _waits;
        public int Steps { get; private set; }
        public bool PollEvent(out InputEvent input) => _events.TryDequeue(out input);
        public uint Ticks { get; private set; }
        public void Delay(uint milliseconds) { trace.AppendLine($"delay:{milliseconds}"); Ticks += milliseconds; }
        public bool JoystickDown => false;
        public bool IsNetworkGame => false;
        public void PushJoysticksAsKeyboard()
        {
            trace.AppendLine("joystick");
            if (++_waits < mode % 3 + 1) return;
            var kind = Steps == 7 ? 16 + mode / 16 % 4 : (mode + Steps * 3) % 16;
            Steps++; _waits = 0;
            if (kind is < 3 or >= 16)
            {
                int[] exits = [44, 40, 88, 41];
                _events.Enqueue(new(InputEventKind.KeyDown, kind >= 16 ? exits[kind - 16] : kind == 0 ? 80 : kind == 1 ? 79 : 0));
                return;
            }
            int[] xs = [0, 0, 0, 82, 83, 106, 107, 212, 213, 236, 237, 213, 83, 213, 83, 0];
            var y = kind == 11 ? 178 : kind == 12 ? 500 : 179;
            var button = (byte)(kind == 13 ? 2 : kind == 14 ? 3 : 1);
            _events.Enqueue(new(InputEventKind.MouseMotion, X: xs[kind], Y: y));
            _events.Enqueue(new(InputEventKind.MouseButtonDown, Button: button, X: xs[kind], Y: y));
            if (kind >= 14) _events.Enqueue(new(InputEventKind.KeyDown, kind == 14 ? 79 : 80));
        }
        public void SetRelativeMouseMode(bool enabled) { }
        public void SetCursorVisible(bool visible) { }
        public void EnableSystemMouseScaling() { }
        public void ToggleFullscreen() { }
        public void WindowResized() { }
        public void PollJoysticks() { }
        public void CheckNetwork() { }
        public void Quit() => throw new InvalidOperationException("Unexpected score paging quit.");
    }
}
