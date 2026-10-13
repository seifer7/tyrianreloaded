using System.Diagnostics;
using System.Text;
using OpenTyrian.Core;

namespace OpenTyrian.Core.Tests;

internal static class GameTextTests
{
    public static void Run(string? executable = null)
    {
        var trace = new StringBuilder();
        for (var mode = 0; mode < 1024; mode++)
        {
            var surface = new IndexedSurface(320, 200, 327, TextWriter.Null); surface.Pixels.Span.Fill(0x45);
            var clock = new Clock(); var timer = new DelayTimer(clock);
            var warning = new WarningBars(surface, timer, () => trace.AppendLine("present"), sample => trace.AppendLine($"sound:{sample}"))
            {
                Color = (byte)(mode % 256),
                ColorChange = unchecked((sbyte)(mode / 4)),
                SoundDelay = (byte)(mode % 16)
            };
            trace.AppendLine($"warning:{mode}");
            for (var frame = 0; frame < 32; frame++)
            {
                warning.Update();
                uint hash = 2166136261;
                foreach (var pixel in surface.Pixels.Span) hash = unchecked((hash ^ pixel) * 16777619);
                trace.AppendLine($"state:{warning.Color},{warning.ColorChange},{warning.SoundDelay},{timer.GetDelayTicks2()},{hash}");
                clock.Delay(frame % 3 == 0 ? 100u : 7u);
            }
        }
        string[] texts = ["", "First", "A~B", "Byte\x80\xff", "Name\0tail"];
        var window = new GameTextWindow(() => trace.AppendLine("erase:16,189,36"), text =>
        {
            var bytes = text.Span; var end = bytes.IndexOf((byte)0);
            trace.AppendLine($"text:20,190,{Encoding.Latin1.GetString(end < 0 ? bytes : bytes[..end])},0,4");
        });
        for (var mode = 0; mode < 20; mode++)
        {
            window.EraseTicks = (mode & 1) != 0 ? ushort.MaxValue : (ushort)0;
            trace.AppendLine($"window:{mode}"); window.Show(Encoding.Latin1.GetBytes(texts[mode % 5]));
            trace.AppendLine($"eraseTicks:{window.EraseTicks}"); window.Show(Encoding.Latin1.GetBytes(texts[(mode + 1) % 5]));
            trace.AppendLine($"eraseTicks:{window.EraseTicks}");
        }
        TestWindowPresentation();
        if (executable is null) return;
        using var process = Process.Start(new ProcessStartInfo(executable)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.Latin1
        }) ?? throw new InvalidOperationException("Cannot start game text reference.");
        var output = process.StandardOutput.ReadToEndAsync(); var errors = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(60000)) { process.Kill(entireProcessTree: true); throw new TimeoutException("Game text reference timed out."); }
        var expected = output.GetAwaiter().GetResult().Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var actual = trace.ToString().Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        if (process.ExitCode != 0 || !expected.SequenceEqual(actual))
        {
            var index = Enumerable.Range(0, Math.Min(expected.Length, actual.Length)).FirstOrDefault(i => expected[i] != actual[i], -1);
            throw new InvalidOperationException($"Game text differs at {index}: {expected.ElementAtOrDefault(index)} / " +
                $"{actual.ElementAtOrDefault(index)}; {errors.GetAwaiter().GetResult()}");
        }
    }

    private static void TestWindowPresentation()
    {
        var shapes = new MainShapeTables();
        using var data = new MemoryStream();
        BinaryFile.WriteUInt16(data, 151);
        for (var i = 0; i < 151; i++)
        {
            BinaryFile.WriteBoolean(data, true); BinaryFile.WriteUInt16(data, 2); BinaryFile.WriteUInt16(data, 1);
            BinaryFile.WriteUInt16(data, 2); data.WriteByte(0xAF); data.WriteByte(0xAE);
        }
        foreach (var table in shapes.Tables) { data.Position = 0; table.Load(data); }
        var surface = new IndexedSurface(320, 200, 327, TextWriter.Null);
        var expected = new IndexedSurface(320, 200, 327, TextWriter.Null);
        var font = new FontRenderer(shapes);
        var window = new GameTextWindow(surface, font, shapes.Tables[5]);
        foreach (var text in new[] { "First", "", "B~C", "Name\0tail" })
        {
            if (window.EraseTicks > 0) shapes.Tables[5].Blit(expected, 16, 189, 36);
            font.OutText(expected, 20, 190, Encoding.Latin1.GetBytes(text), 0, 4);
            window.Show(Encoding.Latin1.GetBytes(text));
            if (!surface.Pixels.Span.SequenceEqual(expected.Pixels.Span) || window.EraseTicks != 100)
                throw new InvalidOperationException("Concrete game text-window sprite/font pixels differ.");
        }
    }

    private sealed class Clock : IDelayPlatform
    {
        public uint Ticks { get; private set; }
        public void Delay(uint milliseconds) => Ticks += milliseconds;
        public void ServiceEvents(bool clearNew) => throw new InvalidOperationException("Warning must not poll input.");
        public void PollJoysticks() => throw new InvalidOperationException("Warning must not poll input.");
        public bool NewKey { get; set; }
        public bool MouseDown => false;
        public bool JoystickDown => false;
    }
}
