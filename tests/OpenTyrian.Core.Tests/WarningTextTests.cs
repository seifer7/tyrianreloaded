using System.Diagnostics;
using System.Text;
using OpenTyrian.Core;

namespace OpenTyrian.Core.Tests;

internal static class WarningTextTests
{
    public static void Run(string? executable = null)
    {
        var trace = new StringBuilder();
        var text = new HelpTextData(); text.MiscText[4] = "Continue?"u8.ToArray();
        for (var mode = 0; mode < 2816; mode++)
        {
            var glow = new TextGlow(new UnusedGlowHost())
            {
                FrameCount = (mode & 4) != 0 ? 4u : 0u,
                WarningRed = (mode & 1) != 0,
                Font = Font.Normal
            };
            var host = new TraceHost(trace, glow, mode);
            var display = new WarningTextDisplay(glow, text, host);
            ReadOnlyMemory<byte>[] lines = Enumerable.Range(0, mode >> 8)
                .Select(i => (ReadOnlyMemory<byte>)Encoding.Latin1.GetBytes(i % 4 == 0 ? "" : $"Warning {i}")).ToArray();
            trace.AppendLine($"case:{mode}");
            var halted = false;
            try { WarningTextDisplay.Synchronize(); display.Run(lines); WarningTextDisplay.Synchronize(); }
            catch (Halted) { halted = true; }
            trace.AppendLine($"state:{glow.FrameCount},{(int)glow.Font},{Bit(host.WarningEnabled)},{Bit(host.EscapePressed)}," +
                $"{host.Draws},{host.Waits},{host.Buttons},{Bit(halted)}");
        }
        TestHost();
        TestValidation();
        if (executable is null) return;
        using var process = Process.Start(new ProcessStartInfo(executable)
        { RedirectStandardOutput = true, RedirectStandardError = true, StandardOutputEncoding = Encoding.Latin1 })
            ?? throw new InvalidOperationException("Cannot start warning text reference.");
        var output = process.StandardOutput.ReadToEndAsync(); var errors = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(30000)) { process.Kill(entireProcessTree: true); throw new TimeoutException("Warning text reference timed out."); }
        var expected = output.GetAwaiter().GetResult().Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var actual = trace.ToString().Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        if (process.ExitCode != 0 || !expected.SequenceEqual(actual))
        {
            var index = Enumerable.Range(0, Math.Min(expected.Length, actual.Length)).FirstOrDefault(i => expected[i] != actual[i], -1);
            throw new InvalidOperationException($"Warning text differs at {index}: {expected.ElementAtOrDefault(index)} / " +
                $"{actual.ElementAtOrDefault(index)}; {errors.GetAwaiter().GetResult()}");
        }
    }

    private static void TestHost()
    {
        for (var mode = 0; mode < 4; mode++)
        {
            var surface = new IndexedSurface(320, 200, 327, TextWriter.Null);
            var expected = new IndexedSurface(320, 200, 327, TextWriter.Null);
            var shapes = HighScoreScreenTests.CreateShapes(151); var font = new FontRenderer(shapes);
            var platform = new TextGlowTests.Platform(); var input = new InputState(platform, new());
            var timer = new DelayTimer(input); var presents = 0; var keeps = 0;
            var warning = new WarningBars(surface, timer, () => presents++, _ => throw new InvalidOperationException("Unexpected warning sound."))
            { Color = 228, ColorChange = 1, SoundDelay = 255 };
            var characterHost = new CharacterGlowHost(surface, font, shapes.Tables[2], input, timer, warning, () => keeps++, () => presents++)
            { WarningEnabled = true };
            var glow = new TextGlow(new UnusedGlowHost()) { WarningRed = (mode & 1) != 0, FrameCount = (mode & 2) != 0 ? 4u : 0u };
            var characters = new CharacterGlow(glow, characterHost);
            var host = new WarningTextHost(characters, characterHost, input, timer, font, warning,
                () => false, _ => throw new InvalidOperationException("Unexpected game halt."), () => keeps++);
            var text = new HelpTextData(); text.MiscText[4] = "A"u8.ToArray();
            var display = new WarningTextDisplay(glow, text, host);
            if (mode < 2)
            {
                platform.Events.Enqueue(new(InputEventKind.MouseButtonDown, Button: 1));
                display.Run(["A\0ignored"u8.ToArray(), "B"u8.ToArray()]);
                font.ShadeText(expected, 10, glow.WarningRed ? 2 : 55, "A"u8, glow.WarningRed ? (byte)7 : (byte)14, 0, TextShade.Partial);
                font.ShadeText(expected, 10, glow.WarningRed ? 12 : 65, "B"u8, glow.WarningRed ? (byte)7 : (byte)14, 0, TextShade.Partial);
                font.ShadeText(expected, font.Center("A"u8, Font.Small), glow.WarningRed ? 118 : 184,
                    "A"u8, glow.WarningRed ? (byte)7 : (byte)14, 0, TextShade.Partial);
                if (!surface.Pixels.Span.SequenceEqual(expected.Pixels.Span) || presents != 3 || keeps != 1 ||
                    platform.Polls != 1 || platform.Ticks != 14 || warning.Color != 228)
                    throw new InvalidOperationException("Concrete skipped warning text pixels, input, timing or keep-alive differ.");
            }
            else
            {
                platform.Events.Enqueue(new(InputEventKind.KeyDown, 40));
                display.Run([]);
                if (platform.Polls != 30 || platform.Ticks != 558 || keeps != 30 || presents < 29 ||
                    glow.FrameCount != 0 || input.NewKey)
                    throw new InvalidOperationException($"Concrete animated prompt skip differs: polls={platform.Polls}, " +
                        $"ticks={platform.Ticks}, keeps={keeps}, presents={presents}.");
            }
            if (glow.Font != Font.Small || host.WarningEnabled || characterHost.WarningEnabled)
                throw new InvalidOperationException("Warning display did not share/reset the glow font and warning state.");
        }
    }

    private static void TestValidation()
    {
        var trace = new StringBuilder(); var glow = new TextGlow(new UnusedGlowHost());
        var host = new TraceHost(trace, glow, 0);
        var display = new WarningTextDisplay(glow, new(), host);
        foreach (var lines in new ReadOnlyMemory<byte>[][]
            { new ReadOnlyMemory<byte>[11], [new byte[61].Select(_ => (byte)'A').ToArray()] })
        {
            try { display.Run(lines); throw new InvalidOperationException("Invalid warning text accepted."); }
            catch (InvalidDataException) { }
        }
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        try { display.Run([], cancellation.Token); throw new InvalidOperationException("Warning cancellation ignored."); }
        catch (OperationCanceledException) { }
        if (trace.Length != 0) throw new InvalidOperationException("Invalid warning display had presentation side effects.");
    }

    internal static void RunSdl(SdlVideoDevice video)
    {
        for (var mode = 0; mode < 3; mode++)
        {
            using var platform = new SdlInputPlatform(video, new Auxiliary(), () => throw new InvalidOperationException("Unexpected warning quit."));
            var input = new InputState(platform, video.Layout); input.FlushEvents();
            var shapes = HighScoreScreenTests.CreateShapes(151); var font = new FontRenderer(shapes);
            var timer = new DelayTimer(input); var presents = 0; var keeps = 0;
            var warning = new WarningBars(video.Primary, timer, () => { presents++; video.Present(); },
                _ => throw new InvalidOperationException("Unexpected warning sound."))
            { Color = 228, ColorChange = 1, SoundDelay = 255 };
            var characterHost = new CharacterGlowHost(video.Primary, font, shapes.Tables[2], input, timer, warning,
                () => keeps++, () => { presents++; video.Present(); })
            { WarningEnabled = true };
            var glow = new TextGlow(new UnusedGlowHost()) { FrameCount = mode == 0 ? 0u : 4u, WarningRed = mode == 2 };
            var characters = new CharacterGlow(glow, characterHost);
            var host = new WarningTextHost(characters, characterHost, input, timer, font, warning,
                () => false, _ => throw new InvalidOperationException("Unexpected warning halt."), () => keeps++);
            var text = new HelpTextData(); text.MiscText[4] = "A"u8.ToArray();
            video.Primary.Pixels.Span.Clear();
            video.Palette.SetPalette(Enumerable.Range(0, 256).Select(i => new PaletteColor((byte)i, (byte)i, (byte)i)).ToArray(), 0, 255);
            platform.PostEvent(mode == 0 ? new(InputEventKind.MouseButtonDown, Button: 1) :
                new(InputEventKind.KeyDown, mode == 1 ? 40 : 41));
            using var cancellation = new CancellationTokenSource(5000);
            new WarningTextDisplay(glow, text, host).Run(mode != 0 ? [] :
                ["A"u8.ToArray(), "B"u8.ToArray()], cancellation.Token);
            if (characterHost.WarningEnabled || glow.Font != Font.Small || glow.FrameCount != 0 ||
                input.EscapePressed || keeps != (mode == 0 ? 1 : 30) || presents < 2)
                throw new InvalidOperationException($"Actual SDL warning text input/glow/wait state differs in case {mode}.");
            input.FlushEvents();
        }
    }

    private sealed class Auxiliary : IInputAuxiliaryServices
    {
        public bool JoystickDown => false;
        public bool IsNetworkGame => false;
        public void PushJoysticksAsKeyboard() { }
        public void PollJoysticks() { }
        public void CheckNetwork() { }
    }
    private static int Bit(bool value) => value ? 1 : 0;
    private sealed class Halted : Exception;
    private sealed class UnusedGlowHost : ITextGlowHost
    {
        public void Draw(int x, int y, ReadOnlyMemory<byte> text, byte hue, int brightness, Font font) => throw new NotSupportedException();
        public void SetDelay(int frames) => throw new NotSupportedException();
        public bool AnyButton() => throw new NotSupportedException();
        public void KeepAlive() => throw new NotSupportedException();
        public void Present() => throw new NotSupportedException();
        public void Wait() => throw new NotSupportedException();
    }
    private sealed class TraceHost(StringBuilder trace, TextGlow glow, int mode) : IWarningTextHost
    {
        public bool EscapePressed { get; private set; } = (mode & 16) != 0;
        public bool HaltGame => (mode & 8) != 0;
        public bool WarningEnabled { get; set; } = (mode & 2) != 0;
        public int Draws { get; private set; }
        public int Waits { get; private set; }
        public int Buttons { get; private set; }
        public void Draw(ushort x, ushort y, ReadOnlyMemory<byte> text, CancellationToken cancellation)
        {
            var end = text.Span.IndexOf((byte)0); if (end >= 0) text = text[..end];
            trace.AppendLine($"draw:{x},{y},{Encoding.Latin1.GetString(text.Span)}"); Draws++;
            if ((mode & 32) != 0 && Draws == (mode >> 8) % 11 + 1) glow.FrameCount = 0;
            if ((mode & 64) != 0 && Draws == 1) EscapePressed = true;
        }
        public int Center(ReadOnlyMemory<byte> text)
        {
            trace.AppendLine($"center:{Encoding.Latin1.GetString(text.Span)},2");
            return 160 - text.Length;
        }
        public void Halt(int code) { trace.AppendLine($"halt:{code}"); throw new Halted(); }
        public void UpdateWarning() => trace.AppendLine("warning");
        public void SetDelay(int frames) => trace.AppendLine($"delay:{frames}");
        public void KeepAlive() => trace.AppendLine("keepalive");
        public void Wait() { trace.AppendLine("wait"); Waits++; if ((mode & 128) != 0 && Waits == 2) EscapePressed = true; }
        public bool AnyButton() { Buttons++; var value = Buttons == mode % 3 + 1; trace.AppendLine($"button:{Bit(value)}"); return value; }
    }
}
