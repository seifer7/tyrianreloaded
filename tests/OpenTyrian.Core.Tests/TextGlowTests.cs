using System.Diagnostics;
using System.Text;
using OpenTyrian.Core;

namespace OpenTyrian.Core.Tests;

internal static class TextGlowTests
{
    public static void Run(string? executable = null)
    {
        var trace = new StringBuilder();
        for (var mode = 0; mode < 1024; mode++)
        {
            var host = new TraceHost(trace, mode);
            var glow = new TextGlow(host)
            {
                FrameCount = (mode & 1) != 0 ? 4u : 0u,
                Font = (Font)(mode % 3),
                Brightness = (byte)(mode % 17),
                WarningRed = (mode & 2) != 0
            };
            trace.AppendLine($"glow:{mode}");
            glow.Draw(50, 75, (mode & 4) != 0 ? "~Hello"u8.ToArray() : []);
            trace.AppendLine($"state:{glow.FrameCount},{glow.Brightness},{host.Buttons}");
            glow.Draw(70, 95, "Second"u8.ToArray());
            trace.AppendLine($"state:{glow.FrameCount},{glow.Brightness},{host.Buttons}");
        }
        var canceled = new TextGlow(new TraceHost(new StringBuilder(), 1)) { FrameCount = 4 };
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        try { canceled.Draw(0, 0, "cancel"u8.ToArray(), cancellation.Token); throw new InvalidOperationException("Glow did not cancel."); }
        catch (OperationCanceledException) { }
        TestHost();
        if (executable is null) return;
        using var process = Process.Start(new ProcessStartInfo(executable) { RedirectStandardOutput = true, RedirectStandardError = true })
            ?? throw new InvalidOperationException("Cannot start text glow reference.");
        var output = process.StandardOutput.ReadToEndAsync(); var errors = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(30000)) { process.Kill(entireProcessTree: true); throw new TimeoutException("Glow reference timed out."); }
        var expected = output.GetAwaiter().GetResult().Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var actual = trace.ToString().Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        if (process.ExitCode != 0 || !expected.SequenceEqual(actual))
        {
            var index = Enumerable.Range(0, Math.Min(expected.Length, actual.Length)).FirstOrDefault(i => expected[i] != actual[i], -1);
            throw new InvalidOperationException($"Text glow differs at {index}: {expected.ElementAtOrDefault(index)} / " +
                $"{actual.ElementAtOrDefault(index)}; {errors.GetAwaiter().GetResult()}");
        }
    }

    private static void TestHost()
    {
        var shapes = new MainShapeTables();
        using var data = new MemoryStream();
        BinaryFile.WriteUInt16(data, 151);
        for (var i = 0; i < 151; i++)
        {
            BinaryFile.WriteBoolean(data, true); BinaryFile.WriteUInt16(data, 1); BinaryFile.WriteUInt16(data, 1);
            BinaryFile.WriteUInt16(data, 1); data.WriteByte(0xAF);
        }
        foreach (var table in shapes.Tables) { data.Position = 0; table.Load(data); }
        var surface = new IndexedSurface(320, 200, 327, TextWriter.Null);
        var expected = new IndexedSurface(320, 200, 327, TextWriter.Null);
        var font = new FontRenderer(shapes);
        var platform = new Platform(); var input = new InputState(platform, new VideoLayout()); var timer = new DelayTimer(input);
        var keeps = 0; var presents = 0;
        var host = new TextGlowHost(surface, font, input, timer, () => keeps++, () => presents++);
        var glow = new TextGlow(host) { Font = Font.Small, FrameCount = 4 };
        platform.Events.Enqueue(new(InputEventKind.KeyDown, 40));
        glow.Draw(50, 75, "A~B"u8.ToArray());
        font.OutTextAdjust(expected, 49, 75, "A~B"u8, 0, -12, Font.Small, false);
        font.OutTextAdjust(expected, 50, 74, "A~B"u8, 0, -12, Font.Small, false);
        font.OutTextAdjust(expected, 51, 75, "A~B"u8, 0, -12, Font.Small, false);
        font.OutTextAdjust(expected, 50, 76, "A~B"u8, 0, -12, Font.Small, false);
        for (var z = 1; z <= 12; z++) font.OutTextAdjust(expected, 50, 75, "A~B"u8, 15, z - 10, Font.Small, false);
        font.OutTextAdjust(expected, 50, 75, "A~B"u8, 15, -4, Font.Small, false);
        if (!surface.Pixels.Span.SequenceEqual(expected.Pixels.Span) || glow.FrameCount != 0 ||
            keeps != 13 || presents != 13 || platform.Polls != 13 || platform.Ticks == 0)
            throw new InvalidOperationException("Concrete glow font/input/timing host integration differs.");
    }

    private sealed class TraceHost(StringBuilder trace, int mode) : ITextGlowHost
    {
        public int Buttons { get; private set; }
        public void Draw(int x, int y, ReadOnlyMemory<byte> text, byte hue, int brightness, Font font)
            => trace.AppendLine($"draw:{x},{y},{Encoding.Latin1.GetString(text.Span)},{hue},{brightness},{(int)font}");
        public void SetDelay(int frames) => trace.AppendLine($"delay:{frames}");
        public bool AnyButton() { Buttons++; var result = Buttons == mode % 25; trace.AppendLine($"button:{(result ? 1 : 0)}"); return result; }
        public void KeepAlive() => trace.AppendLine("keep");
        public void Present() => trace.AppendLine("present");
        public void Wait() => trace.AppendLine("wait");
    }
    internal sealed class Platform : IInputPlatform
    {
        public Queue<InputEvent> Events { get; } = new();
        public uint Ticks { get; private set; }
        public int Polls { get; private set; }
        public bool PollEvent(out InputEvent input) => Events.TryDequeue(out input);
        public void Delay(uint milliseconds) => Ticks += milliseconds;
        public void PollJoysticks() => Polls++;
        public bool JoystickDown => false;
        public bool IsNetworkGame => false;
        public void SetCursorVisible(bool visible) => throw new InvalidOperationException("Unexpected glow cursor change.");
        public void SetRelativeMouseMode(bool enabled) => throw new InvalidOperationException("Unexpected glow mouse change.");
        public void EnableSystemMouseScaling() => throw new InvalidOperationException("Unexpected glow mouse change.");
        public void ToggleFullscreen() => throw new InvalidOperationException("Unexpected glow fullscreen change.");
        public void WindowResized() => throw new InvalidOperationException("Unexpected glow resize.");
        public void Quit() => throw new InvalidOperationException("Unexpected glow quit.");
        public void PushJoysticksAsKeyboard() => throw new InvalidOperationException("Unexpected glow joystick push.");
        public void CheckNetwork() => throw new InvalidOperationException("Unexpected glow event-network check.");
    }
}
