using System.Diagnostics;
using System.Text;
using OpenTyrian.Core;

namespace OpenTyrian.Core.Tests;

internal static class GameIntroTests
{
    public static void Run(string? executable = null)
    {
        var trace = new StringBuilder(); var moving = false;
        var primary = new IndexedSurface(320, 200, 320, TextWriter.Null); primary.Pixels.Span.Fill(123);
        new IntroLogos(() => moving = true, new TraceHost(trace, primary)).Run();
        trace.AppendLine($"introstate:{(moving ? 1 : 0)},{Hash(primary.Pixels.Span)}");
        int[] exits = [1, 2, 3, 27, 299, 300];
        foreach (var pitch in new[] { 320, 327 })
            for (var n = 0; n < exits.Length; n++)
            {
                var screen = new IndexedSurface(320, 200, pitch, TextWriter.Null);
                var game = new IndexedSurface(320, 200, pitch, TextWriter.Null);
                var background = new IndexedSurface(320, 200, pitch, TextWriter.Null);
                for (var i = 0; i < screen.Pixels.Length; i++) screen.Pixels.Span[i] = unchecked((byte)(i * 17 + n * 31));
                game.Pixels.Span.Fill(0xaa); background.Pixels.Span.Fill(0xbb);
                var platform = new Platform(); var input = new InputState(platform, new()); var timer = new DelayTimer(input);
                var frame = 0; byte warning = 9;
                trace.AppendLine($"bleed:{pitch},{exits[n]}");
                new ScreenBleed(screen, game, background, input, timer, () =>
                {
                    frame++;
                    trace.AppendLine($"frame:{frame},{Hash(screen.Pixels.Span)},{Hash(game.Pixels.Span)},{Hash(background.Pixels.Span)}");
                    if (frame == exits[n]) platform.Events.Enqueue(new(InputEventKind.KeyDown, 40));
                }, value => warning = value).Run();
                trace.AppendLine($"bleedstate:{warning},{Hash(screen.Pixels.Span)},{Hash(game.Pixels.Span)},{Hash(background.Pixels.Span)}");
                foreach (var surface in new[] { screen, game, background }) trace.AppendLine($"bytes:{Convert.ToHexString(surface.Pixels.Span)}");
                if (frame != exits[n] || warning != 4) throw new InvalidOperationException("Screen bleed frame limit or warning state differs.");
            }
        TestHost();
        TestFailures();
        if (executable is null) return;
        using var process = Process.Start(new ProcessStartInfo(executable) { RedirectStandardOutput = true, RedirectStandardError = true })
            ?? throw new InvalidOperationException("Cannot start game intro oracle.");
        var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(30000)) { process.Kill(entireProcessTree: true); throw new TimeoutException("Game intro oracle timed out."); }
        var expected = output.GetAwaiter().GetResult().Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var actual = trace.ToString().Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        if (process.ExitCode != 0 || !expected.SequenceEqual(actual))
        {
            var index = Enumerable.Range(0, Math.Min(expected.Length, actual.Length)).FirstOrDefault(i => expected[i] != actual[i], -1);
            throw new InvalidOperationException($"Game intro differs at {index}: {expected.ElementAtOrDefault(index)} / " +
                $"{actual.ElementAtOrDefault(index)}; {error.GetAwaiter().GetResult()}");
        }
    }

    private static void TestHost()
    {
        var primary = new IndexedSurface(320, 200, 327, TextWriter.Null); primary.Pixels.Span.Fill(123);
        var palette = new PaletteSystem(c => (uint)(c.Red << 16 | c.Green << 8 | c.Blue));
        var colors = Enumerable.Range(0, 256).Select(i => new PaletteColor((byte)i, (byte)i, (byte)i)).ToArray();
        var platform = new Platform { PushInput = true }; var input = new InputState(platform, new()); var timer = new DelayTimer(input);
        var presents = 0; var pictures = new List<int>();
        var host = new IntroLogoHost(primary, palette, colors, timer, pictures.Add, () => presents++);
        host.Clear(); host.FadeWhite();
        if (primary.Pixels.Span.ContainsAnyExcept((byte)0) || palette.Active.Span.ToArray().Any(c => c.Red != 255 || c.Green != 255 || c.Blue != 255) || presents != 25)
            throw new InvalidOperationException("Logo intro clear/white fade differs.");
        host.LoadPicture(10); host.Present(); host.FadeIn(25); host.WaitInput();
        if (!palette.Active.Span.SequenceEqual(colors) || input.NewKey || platform.Ticks >= 2000 || presents != 51)
            throw new InvalidOperationException("Logo intro palette/wait-input services differ.");
        host.FadeBlack();
        if (palette.Active.Span.ToArray().Any(c => c != default) || presents != 61 || !pictures.SequenceEqual([10]))
            throw new InvalidOperationException("Logo intro exit fade/picture routing differs.");
    }

    private static void TestFailures()
    {
        var primary = new IndexedSurface(320, 200, 320, TextWriter.Null);
        var background = new IndexedSurface(320, 200, 320, TextWriter.Null);
        var input = new InputState(new Platform(), new()); var timer = new DelayTimer(input);
        try
        {
            new ScreenBleed(primary, primary, background, input, timer, () => { }, _ => { }).Run();
            throw new InvalidOperationException("Screen bleed accepted overlapping scratch memory.");
        }
        catch (InvalidDataException) { }
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        var called = false;
        try
        {
            new ScreenBleed(primary, new(320, 200, 320, TextWriter.Null), background, input, timer,
                () => called = true, _ => called = true).Run(cancellation.Token);
            throw new InvalidOperationException("Screen bleed ignored cancellation.");
        }
        catch (OperationCanceledException) { if (called) throw new InvalidOperationException("Canceled bleed published frames/warnings."); }
    }

    private static uint Hash(ReadOnlySpan<byte> bytes)
    {
        var hash = 2166136261u;
        foreach (var value in bytes) hash = unchecked((hash ^ value) * 16777619u);
        return hash;
    }

    private sealed class TraceHost(StringBuilder trace, IndexedSurface primary) : IIntroLogoHost
    {
        public void Clear() { primary.Pixels.Span.Clear(); trace.AppendLine("clear"); }
        public void FadeWhite() => trace.AppendLine("white:25");
        public void LoadPicture(int number) => trace.AppendLine($"picture:{number}");
        public void Present() => trace.AppendLine("present");
        public void FadeIn(int steps) => trace.AppendLine($"fadein:{steps}");
        public void WaitInput() { trace.AppendLine("delay:200"); trace.AppendLine("waitinput"); }
        public void FadeBlack() => trace.AppendLine("black:10");
    }

    private sealed class Platform : IInputPlatform
    {
        public Queue<InputEvent> Events { get; } = new();
        public bool PushInput;
        public uint Ticks { get; private set; }
        public bool PollEvent(out InputEvent input) => Events.TryDequeue(out input);
        public void Delay(uint milliseconds) { Ticks += milliseconds; if (PushInput) Events.Enqueue(new(InputEventKind.KeyDown, 40)); }
        public bool JoystickDown => false;
        public bool IsNetworkGame => false;
        public void SetRelativeMouseMode(bool enabled) { }
        public void SetCursorVisible(bool visible) { }
        public void EnableSystemMouseScaling() { }
        public void ToggleFullscreen() { }
        public void WindowResized() { }
        public void PushJoysticksAsKeyboard() { }
        public void PollJoysticks() { }
        public void CheckNetwork() { }
        public void Quit() => throw new InvalidOperationException("Unexpected intro quit.");
    }
}
