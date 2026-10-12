using System.Diagnostics;
using System.Runtime.InteropServices;
using OpenTyrian.Core;

namespace OpenTyrian.Core.Tests;

internal static class HqVideoScalerTests
{
    public static void Run(string? executable = null)
    {
        using var fixtures = new MemoryStream();
        using var expected = new MemoryStream();
        using var writer = new BinaryWriter(fixtures, System.Text.Encoding.UTF8, leaveOpen: true);
        writer.Write(0);
        var count = 0;
        var rgb = new uint[256];
        var yuv = new uint[256];
        var source = new IndexedSurface(3, 3, 3, TextWriter.Null);
        for (var i = 0; i < 9; i++) source.Pixels.Span[i] = (byte)(i + 1);
        for (var pattern = 0; pattern < 256; pattern++)
            for (var variation = 0; variation < 16; variation++)
            {
                for (var i = 0; i < 256; i++)
                {
                    rgb[i] = unchecked((uint)i * 0x29486719 + (uint)variation * 0x45671239);
                    yuv[i] = 0x808080;
                }
                var bit = 0;
                for (var i = 1; i <= 9; i++)
                {
                    if (i == 5) continue;
                    if ((pattern & (1 << bit)) != 0)
                        yuv[i] = variation < 8 ? (((i + variation) % 3) switch { 0 => 0x008080u, 1 => 0xff8080u, _ => 0x408080u })
                            : unchecked((uint)(i * (variation + 1) * 1234567)) & 0xffffff;
                    else
                        yuv[i] = variation < 8 ? 0x808080 : (uint)(0x808080 + (i % 2 == 0 ? 0x300706 : -0x300706));
                    bit++;
                }
                for (var factor = 2; factor <= 4; factor++) Execute(source, factor, source.Width * factor + 7);
            }
        foreach (var (width, height) in new[] { (1, 1), (1, 5), (2, 2), (3, 1), (8, 11), (17, 5), (320, 200) })
            foreach (var padding in new[] { 0, 5 })
            {
                source = new(width, height, width + padding, TextWriter.Null);
                for (var i = 0; i < source.Pixels.Length; i++) source.Pixels.Span[i] = unchecked((byte)(i * 71 + i / source.Pitch));
                var colors = Enumerable.Range(0, 256).Select(i => new PaletteColor((byte)i, (byte)(255 - i), (byte)(i ^ 85))).ToArray();
                var palette = new PaletteSystem(c => (uint)(c.Red << 16 | c.Green << 8 | c.Blue));
                palette.SetPalette(colors, 0, 255);
                rgb = palette.Rgb.ToArray(); yuv = palette.Yuv.ToArray();
                for (var factor = 2; factor <= 4; factor++)
                    foreach (var outputPadding in new[] { 0, 7 }) Execute(source, factor, width * factor + outputPadding);
            }
        fixtures.Position = 0; writer.Write(count); writer.Flush();
        if (executable != null) CompareNative(executable, fixtures.ToArray(), expected.ToArray());
        Failures();
        return;

        void Execute(IndexedSurface image, int factor, int pitch)
        {
            count++;
            writer.Write(image.Width); writer.Write(image.Height); writer.Write(factor); writer.Write(image.Pitch); writer.Write(pitch);
            writer.Write(MemoryMarshal.AsBytes(rgb.AsSpan())); writer.Write(MemoryMarshal.AsBytes(yuv.AsSpan())); writer.Write(image.Pixels.Span);
            var output = new uint[pitch * image.Height * factor];
            Array.Fill(output, 0x12345678u);
            HqVideoScaler.Scale(image, output, pitch, rgb, yuv, factor);
            for (var y = 0; y < image.Height * factor; y++)
                Check(output.AsSpan(y * pitch + image.Width * factor, pitch - image.Width * factor).ToArray().All(p => p == 0x12345678),
                    "HQ scaler wrote destination padding.");
            expected.Write(MemoryMarshal.AsBytes(output.AsSpan()));
        }
    }

    private static void Failures()
    {
        var source = new IndexedSurface(1, 1, 1, TextWriter.Null);
        var palette = new uint[256]; Array.Fill(palette, 0xffffffffu);
        for (var factor = 2; factor <= 4; factor++)
        {
            var output = new uint[factor * factor];
            HqVideoScaler.Scale(source, output, factor, palette, palette, factor);
            // All-equal patterns use the native raw masked additions, including unsigned overflow.
            Check(output.All(value => value != 0), "HQ single-pixel replication was blank.");
        }
        Throws<ArgumentOutOfRangeException>(() => HqVideoScaler.Scale(source, new uint[16], 4, palette, palette, 1));
        Throws<ArgumentOutOfRangeException>(() => HqVideoScaler.Scale(source, new uint[16], 4, palette, palette, 5));
        Throws<ArgumentException>(() => HqVideoScaler.Scale(source, new uint[16], 4, palette[..255], palette, 4));
        Throws<ArgumentException>(() => HqVideoScaler.Scale(source, new uint[16], 4, palette, palette[..255], 4));
        Throws<ArgumentException>(() => HqVideoScaler.Scale(source, new uint[15], 4, palette, palette, 4));
        Throws<ArgumentException>(() => HqVideoScaler.Scale(source, new uint[16], 3, palette, palette, 4));
    }

    private static void CompareNative(string executable, byte[] fixture, byte[] expected)
    {
        var path = Path.Combine(Path.GetTempPath(), $"opentyrian-hq-{Guid.NewGuid():N}.bin");
        try
        {
            File.WriteAllBytes(path, fixture);
            var info = new ProcessStartInfo(executable) { RedirectStandardOutput = true, RedirectStandardError = true };
            info.ArgumentList.Add(path);
            using var process = Process.Start(info) ?? throw new InvalidOperationException("Could not start native HQ scaler.");
            using var actual = new MemoryStream();
            var stdout = process.StandardOutput.BaseStream.CopyToAsync(actual);
            var stderr = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(60000)) { process.Kill(entireProcessTree: true); throw new TimeoutException("Native HQ scaler timed out."); }
            stdout.GetAwaiter().GetResult();
            Check(process.ExitCode == 0, $"Native HQ scaler failed: {stderr.GetAwaiter().GetResult()}");
            var bytes = actual.ToArray();
            if (!bytes.AsSpan().SequenceEqual(expected))
            {
                var first = Enumerable.Range(0, Math.Min(bytes.Length, expected.Length)).FirstOrDefault(i => bytes[i] != expected[i], -1);
                throw new InvalidOperationException($"Native HQ pixels differ at byte {first}; lengths {bytes.Length}/{expected.Length}.");
            }
        }
        finally { File.Delete(path); }
    }

    private static void Throws<T>(Action action) where T : Exception
    {
        try { action(); } catch (T) { return; }
        throw new InvalidOperationException($"Expected {typeof(T).Name}.");
    }
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
