using System.Diagnostics;
using System.Text;
using OpenTyrian.Core;

namespace OpenTyrian.Core.Tests;

internal static class ChristmasPromptTests
{
    public static void Run(string? executable = null)
    {
        var trace = new StringBuilder();
        for (var month = 1; month <= 12; month++)
        {
            var date = new DateTimeOffset(new DateTime(2026, month, 15, 12, 0, 0, DateTimeKind.Local));
            var result = ChristmasPrompt.IsChristmasTime(date);
            if (result != (month == 12)) throw new InvalidOperationException("Local December detection differs.");
            trace.AppendLine($"month:{month - 1},{Bit(result)}");
        }
        var utc = new DateTimeOffset(2026, 12, 1, 0, 0, 0, TimeSpan.Zero);
        if (ChristmasPrompt.IsChristmasTime(utc) != (utc.ToLocalTime().Month == 12))
            throw new InvalidOperationException("Christmas detection ignored the local timezone boundary.");
        for (var mode = 0; mode < 300; mode++)
        {
            var random = new RandomService((uint)(mode + 1));
            var platform = new Platform(mode, trace);
            var input = new InputState(platform, new()); platform.Input = input;
            platform.Events.Enqueue(new(InputEventKind.MouseMotion, X: 10, Y: 10)); input.ServiceEvents(true);
            var host = new Host(mode, trace, platform);
            trace.AppendLine($"case:{mode}");
            var result = new ChristmasPrompt(input, host, random.Next).Run();
            trace.AppendLine($"result:{Bit(result)},{platform.Frames},{platform.Steps},{random.State}");
        }
        if (executable != null)
        {
            using var process = Process.Start(new ProcessStartInfo(executable) { RedirectStandardOutput = true, RedirectStandardError = true })
                ?? throw new InvalidOperationException("Cannot start Christmas prompt oracle.");
            var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(30000)) { process.Kill(entireProcessTree: true); throw new TimeoutException("Christmas prompt oracle timed out."); }
            var expected = output.GetAwaiter().GetResult().Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
            var actual = trace.ToString().Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
            if (process.ExitCode != 0 || !expected.SequenceEqual(actual))
            {
                var index = Enumerable.Range(0, Math.Min(expected.Length, actual.Length)).FirstOrDefault(i => expected[i] != actual[i], -1);
                throw new InvalidOperationException($"Christmas prompt differs at {index}: {expected.ElementAtOrDefault(index)} / " +
                    $"{actual.ElementAtOrDefault(index)}; {error.GetAwaiter().GetResult()}");
            }
        }
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        var canceledPlatform = new Platform(0, new()); var canceledHost = new Host(0, new(), canceledPlatform);
        try
        {
            new ChristmasPrompt(new(canceledPlatform, new()), canceledHost, () => throw new InvalidOperationException("Canceled snow consumed RNG."))
                .Run(cancellation.Token);
            throw new InvalidOperationException("Christmas prompt ignored cancellation.");
        }
        catch (OperationCanceledException) { }
        TestHost();
    }

    private static void TestHost()
    {
        var primary = new IndexedSurface(320, 200, 327, TextWriter.Null);
        var expected = new IndexedSurface(320, 200, 327, TextWriter.Null);
        var font = new FontRenderer(HighScoreScreenTests.CreateShapes());
        var flakes = new SpriteSheet(); var cursorSheet = new SpriteSheet(); var bytes = HighScoreScreenTests.SpriteBytes();
        using (var stream = new MemoryStream(bytes)) flakes.Load(stream, bytes.Length);
        var input = new InputState(new TextGlowTests.Platform(), new()); var timer = new DelayTimer(input);
        var cursor = new MouseCursor(input, primary, cursorSheet) { HasMouse = false, Cursor = 4 };
        var palette = new PaletteSystem(c => (uint)(c.Red << 16 | c.Green << 8 | c.Blue));
        var colors = Enumerable.Range(0, 256).Select(i => new PaletteColor((byte)i, (byte)i, (byte)i)).ToArray();
        var loads = 0; var presents = 0; var pushes = 0;
        var host = new ChristmasPromptHost(primary, font, flakes, cursorSheet, cursor, palette, colors, timer,
            () => { using var stream = new MemoryStream(bytes); cursorSheet.Load(stream, bytes.Length); loads++; },
            () => presents++, () => pushes++);
        host.EnsureCursor(); host.EnsureCursor(); host.Fill(); expected.FillRectangleSize(0, 0, 320, 200, 0x8f);
        host.Flake(25, -1, 225); flakes.Blit(expected, 25, -1, 225, SpriteEffect.Blend);
        host.Flake(319, 199, 226); flakes.Blit(expected, 319, 199, 226, SpriteEffect.Blend);
        host.Label(160, 85, "Christmas"u8.ToArray(), 4, -2);
        font.DrawFullShadow(expected, 160, 85, "Christmas"u8, Font.Normal, FontAlignment.Centered, 4, -2, true, 1);
        if (!primary.Pixels.Span.SequenceEqual(expected.Pixels.Span) || loads != 1)
            throw new InvalidOperationException("Christmas full shadow/blended flakes/padded rendering differs.");
        host.FadeIn(); host.PresentCursor(); host.PushJoysticks();
        if (!palette.Active.Span.SequenceEqual(colors) || cursor.Cursor != 0 || presents != 11 || pushes != 1)
            throw new InvalidOperationException("Christmas palette/cursor/presentation/input routing differs.");
        host.FadeBlack();
        if (presents != 21 || palette.Active.Span.ToArray().Any(color => color != default))
            throw new InvalidOperationException("Christmas prompt failed to fade black.");
    }

    internal static void RunSdl(SdlVideoDevice video)
    {
        SdlInputPlatform? platform = null;
        var pushes = 0;
        using var ownedPlatform = platform = new SdlInputPlatform(video, new Auxiliary(() =>
        {
            if (pushes++ == 0) platform!.PostEvent(new(InputEventKind.KeyDown, 79));
            else platform!.PostEvent(new(InputEventKind.KeyDown, 40));
        }), () => throw new InvalidOperationException("Unexpected SDL Christmas quit."));
        var input = new InputState(platform, video.Layout); input.FlushEvents();
        var font = new FontRenderer(HighScoreScreenTests.CreateShapes()); var flakes = new SpriteSheet();
        var cursorSheet = new SpriteSheet(); var bytes = HighScoreScreenTests.SpriteBytes();
        using (var stream = new MemoryStream(bytes)) flakes.Load(stream, bytes.Length);
        var cursor = new MouseCursor(input, video.Primary, cursorSheet) { HasMouse = false };
        var colors = Enumerable.Range(0, 256).Select(i => new PaletteColor((byte)i, (byte)i, (byte)i)).ToArray();
        var presents = 0; var timer = new DelayTimer(input); var random = new RandomService(1);
        var host = new ChristmasPromptHost(video.Primary, font, flakes, cursorSheet, cursor, video.Palette, colors, timer,
            () => { using var stream = new MemoryStream(bytes); cursorSheet.Load(stream, bytes.Length); },
            () => { presents++; video.Present(); }, platform.PushJoysticksAsKeyboard);
        using var cancellation = new CancellationTokenSource(5000);
        if (new ChristmasPrompt(input, host, random.Next).Run(cancellation.Token) || pushes != 2 || presents != 22 ||
            cursorSheet.Size == 0 || video.Palette.Active.Span.ToArray().Any(color => color != default))
            throw new InvalidOperationException("Actual SDL snow prompt selection/input/presentation differs.");
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

    private sealed class RandomService(uint state)
    {
        public uint State { get; private set; } = state;
        public int Next() { State = unchecked(State * 214013 + 2531011); return (int)(State >> 16 & 32767); }
    }
    private static int Bit(bool value) => value ? 1 : 0;

    private sealed class Host(int mode, StringBuilder trace, Platform platform) : IChristmasPromptHost
    {
        public void EnsureCursor() { if ((mode & 1) == 0) trace.AppendLine("cursor:1"); }
        public void Fill() { trace.AppendLine("fill:0,0,320,200,143"); platform.Frames++; }
        public void Flake(int x, int y, int sprite) => trace.AppendLine($"flake:{x},{y},{sprite}");
        public void Label(int x, int y, ReadOnlyMemory<byte> text, byte hue, sbyte brightness) =>
            trace.AppendLine($"label:{x},{y},{Encoding.Latin1.GetString(text.Span)},{hue},{brightness},1,1");
        public void FadeIn() => trace.AppendLine("fadein:10,0,255,0");
        public void PresentCursor() { trace.AppendLine("mouse"); trace.AppendLine("present"); trace.AppendLine("replace"); }
        public void PushJoysticks() => platform.PushJoysticksAsKeyboard();
        public void FadeBlack() => trace.AppendLine("fade:10");
    }

    private sealed class Platform(int mode, StringBuilder trace) : IInputPlatform
    {
        public Queue<InputEvent> Events { get; } = new();
        public InputState? Input { get; set; }
        public int Steps { get; private set; }
        public int Frames { get; set; }
        public uint Ticks { get; private set; }
        public bool JoystickDown => false;
        public bool IsNetworkGame => false;
        public bool PollEvent(out InputEvent input) => Events.TryDequeue(out input);
        public void Delay(uint milliseconds) { trace.AppendLine($"delay:{milliseconds}"); Ticks += milliseconds; }
        public void PushJoysticksAsKeyboard()
        {
            trace.AppendLine("joystick");
            if (Frames < (mode >= 288 ? 350 : mode % 4 + 1)) return;
            var kind = mode % 12;
            if (Steps >= 2) Events.Enqueue(new(InputEventKind.KeyDown, (mode & 16) != 0 ? 40 : 41));
            else if (kind < 2) Events.Enqueue(new(InputEventKind.KeyDown, kind == 0 ? 80 : 79));
            else if (kind < 6) { int[] scans = [44, 40, 88, 41]; Events.Enqueue(new(InputEventKind.KeyDown, scans[kind - 2])); }
            else if (kind == 6) Events.Enqueue(new(InputEventKind.MouseButtonDown, Button: 3));
            else if (kind < 11)
            {
                int[] xs = [119, 120, 159, 160, 199, 200]; int[] ys = [119, 120, 132, 133];
                var x = xs[mode / 12 % 6]; var y = ys[mode / 72 % 4];
                Events.Enqueue(new(InputEventKind.KeyDown, 41)); Input!.ServiceEvents(false);
                Events.Enqueue(new(InputEventKind.MouseMotion, X: x, Y: y));
                Events.Enqueue(new(InputEventKind.MouseButtonDown, Button: (byte)(kind == 10 ? 2 : 1),
                    X: kind == 7 ? x : (mode & 32) != 0 ? 500 : 0, Y: kind == 7 ? y : 0));
                Input.ServiceEvents(false);
            }
            else Events.Enqueue(new(InputEventKind.MouseMotion, X: (Steps & 1) != 0 ? 160 : -2, Y: 120));
            Steps++;
        }
        public void SetRelativeMouseMode(bool enabled) { }
        public void SetCursorVisible(bool visible) { }
        public void EnableSystemMouseScaling() { }
        public void ToggleFullscreen() { }
        public void WindowResized() { }
        public void PollJoysticks() { }
        public void CheckNetwork() { }
        public void Quit() => throw new InvalidOperationException("Unexpected Christmas prompt quit.");
    }
}
