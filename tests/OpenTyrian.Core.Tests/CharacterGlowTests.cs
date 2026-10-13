using System.Diagnostics;
using System.Text;
using OpenTyrian.Core;

namespace OpenTyrian.Core.Tests;

internal static class CharacterGlowTests
{
    public static void Run(string? executable = null)
    {
        var trace = new StringBuilder();
        string[] texts = ["", "A B", "~Hi!", "Byte\x80\xff", "@A&", "Name\0tail", "A                                     B",
            "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz12345678"];
        for (var mode = 0; mode < 1024; mode++)
        {
            var host = new TraceHost(trace, mode);
            var glow = new TextGlow(new UnusedGlowHost()) { WarningRed = (mode & 1) != 0, FrameCount = (mode & 4) != 0 ? 0u : 4u };
            var characters = new CharacterGlow(glow, host) { UseLastBank = (mode & 2) != 0 };
            trace.AppendLine($"chars:{mode}");
            characters.Draw(unchecked((ushort)(mode * 79)), unchecked((ushort)(mode * 51)), Encoding.Latin1.GetBytes(texts[mode % 8]));
            trace.AppendLine($"state:{glow.FrameCount},{host.Buttons},{Bit(host.EscapePressed)}");
        }
        try
        {
            new CharacterGlow(new TextGlow(new UnusedGlowHost()), new TraceHost(new StringBuilder(), 0)).Draw(0, 0, new byte[61].Select(_ => (byte)'A').ToArray());
            throw new InvalidOperationException("Oversized character glow was accepted.");
        }
        catch (InvalidDataException) { }
        TestHost();
        if (executable is null) return;
        using var process = Process.Start(new ProcessStartInfo(executable)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.Latin1
        }) ?? throw new InvalidOperationException("Cannot start character glow reference.");
        var output = process.StandardOutput.ReadToEndAsync(); var errors = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(30000)) { process.Kill(entireProcessTree: true); throw new TimeoutException("Character glow reference timed out."); }
        var expected = output.GetAwaiter().GetResult().Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var actual = trace.ToString().Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        if (process.ExitCode != 0 || !expected.SequenceEqual(actual))
        {
            var index = Enumerable.Range(0, Math.Min(expected.Length, actual.Length)).FirstOrDefault(i => expected[i] != actual[i], -1);
            throw new InvalidOperationException($"Character glow differs at {index}: {expected.ElementAtOrDefault(index)} / " +
                $"{actual.ElementAtOrDefault(index)}; {errors.GetAwaiter().GetResult()}");
        }
    }

    private static void TestHost()
    {
        var shapes = new MainShapeTables();
        using var data = new MemoryStream(); BinaryFile.WriteUInt16(data, 151);
        for (var i = 0; i < 151; i++)
        {
            BinaryFile.WriteBoolean(data, true); BinaryFile.WriteUInt16(data, 1); BinaryFile.WriteUInt16(data, 1);
            BinaryFile.WriteUInt16(data, 1); data.WriteByte(0xAF);
        }
        foreach (var table in shapes.Tables) { data.Position = 0; table.Load(data); }
        var surface = new IndexedSurface(320, 200, 327, TextWriter.Null);
        var expected = new IndexedSurface(320, 200, 327, TextWriter.Null);
        var platform = new TextGlowTests.Platform(); var input = new InputState(platform, new VideoLayout());
        var timer = new DelayTimer(input); var font = new FontRenderer(shapes);
        var keeps = 0; var presents = 0;
        var warning = new WarningBars(surface, timer, () => presents++, _ => throw new InvalidOperationException("Unexpected warning sound."))
        { Color = 228, ColorChange = 1, SoundDelay = 255 };
        var host = new CharacterGlowHost(surface, font, shapes.Tables[2], input, timer, warning, () => keeps++, () => presents++)
        { WarningEnabled = true };
        var glow = new TextGlow(new UnusedGlowHost()) { FrameCount = 4 };
        var characters = new CharacterGlow(glow, host);
        platform.Events.Enqueue(new(InputEventKind.KeyDown, 40));
        characters.Draw(50, 75, "A"u8.ToArray());
        shapes.Tables[2].Blit(expected, 50, 75, 0, SpriteEffect.Hue, 14, 0);
        shapes.Tables[2].Blit(expected, 51, 76, 0, SpriteEffect.Black);
        expected.FillRectangle(0, 0, 319, 5, 234);
        expected.FillRectangle(0, 194, 319, 199, 234);
        if (!surface.Pixels.Span.SequenceEqual(expected.Pixels.Span) || glow.FrameCount != 0 || keeps != 29 || presents != 35 ||
            warning.Color != 234 || warning.SoundDelay != 249 ||
            platform.Polls != 29 || platform.Ticks != 32 * 16)
            throw new InvalidOperationException($"Concrete character glow integration differs: frames={glow.FrameCount}, " +
                $"keeps={keeps}, presents={presents}, polls={platform.Polls}, ticks={platform.Ticks}, " +
                $"pixels={surface.Pixels.Span.SequenceEqual(expected.Pixels.Span)}.");
        characters.Draw(60, 80, "B\0tail"u8.ToArray());
        font.ShadeText(expected, 60, 80, "B"u8, 14, 0, TextShade.Partial);
        if (!surface.Pixels.Span.SequenceEqual(expected.Pixels.Span)) throw new InvalidOperationException("Skipped character glow differs.");
    }

    private static int Bit(bool value) => value ? 1 : 0;
    private sealed class TraceHost(StringBuilder trace, int mode) : ICharacterGlowHost
    {
        private int _delays, _frames;
        public int Buttons { get; private set; }
        public bool EscapePressed { get; private set; } = (mode & 64) != 0;
        public bool WarningEnabled => (mode & 16) != 0;
        public int Width(int sprite) { var width = sprite % 5; trace.AppendLine($"width:{sprite},{width}"); return width; }
        public void Shade(ushort x, ushort y, ReadOnlyMemory<byte> text, byte hue)
            => trace.AppendLine($"shade:{x},{y},{Encoding.Latin1.GetString(text.Span)},{hue}");
        public void Glyph(ushort x, ushort y, int sprite, byte hue, sbyte brightness) => trace.AppendLine($"glyph:{x},{y},{sprite},{hue},{brightness}");
        public void Shadow(int x, int y, int sprite) => trace.AppendLine($"shadow:{x},{y},{sprite}");
        public void SetDelay2(int frames) => trace.AppendLine($"delay2:{frames}");
        public void SetDelay(int frames) { _frames = frames; _delays = 0; trace.AppendLine($"delay:{frames}"); }
        public uint Remaining() { var result = _frames > 0 && _delays < 2 ? 1u : 0u; trace.AppendLine($"remaining:{result}"); return result; }
        public bool AnyButton()
        {
            Buttons++; var result = Buttons == mode % 25;
            if ((mode & 8) != 0 && Buttons == mode % 7) EscapePressed = true;
            trace.AppendLine($"button:{Bit(result)}"); return result;
        }
        public void KeepAlive() => trace.AppendLine("keep");
        public void UpdateWarning() => trace.AppendLine("warning");
        public void Delay(uint milliseconds) { _delays++; trace.AppendLine($"sleep:{milliseconds}"); }
        public void Present() => trace.AppendLine("present");
    }
    private sealed class UnusedGlowHost : ITextGlowHost
    {
        public void Draw(int x, int y, ReadOnlyMemory<byte> text, byte hue, int brightness, Font font) => throw new InvalidOperationException("Unexpected full-text glow.");
        public void SetDelay(int frames) => throw new InvalidOperationException("Unexpected full-text glow delay.");
        public bool AnyButton() => throw new InvalidOperationException("Unexpected full-text glow input.");
        public void KeepAlive() => throw new InvalidOperationException("Unexpected full-text glow keep-alive.");
        public void Present() => throw new InvalidOperationException("Unexpected full-text glow presentation.");
        public void Wait() => throw new InvalidOperationException("Unexpected full-text glow wait.");
    }
}
