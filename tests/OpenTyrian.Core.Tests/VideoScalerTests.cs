using OpenTyrian.Core;

namespace OpenTyrian.Core.Tests;

internal static class VideoScalerTests
{
    public static void Run()
    {
        using var warnings = new StringWriter();
        var source = new IndexedSurface(3, 3, 5, warnings);
        byte[] pixels = [0, 1, 0, 1, 2, 0, 0, 0, 0];
        for (var row = 0; row < 3; row++)
        {
            pixels.AsSpan(row * 3, 3).CopyTo(source.Pixels.Span.Slice(row * 5, 3));
        }

        var palette = Enumerable.Range(0, 256).Select(index => 0x10000u + (uint)index).ToArray();
        foreach (var factor in new[] { 1, 2, 3, 4 })
        {
            var pitch = factor * 3 + 2;
            var nearest = Enumerable.Repeat(uint.MaxValue, pitch * factor * 3).ToArray();
            VideoScaler.Scale32(source, nearest, pitch, palette, ScaleAlgorithm.Nearest, factor);
            for (var y = 0; y < 3 * factor; y++)
            {
                for (var x = 0; x < 3 * factor; x++)
                {
                    Check(nearest[y * pitch + x] == palette[pixels[y / factor * 3 + x / factor]], "Nearest-neighbor replication differs.");
                }

                Check(nearest[y * pitch + factor * 3] == uint.MaxValue, "Output padding was overwritten.");
            }
        }

        foreach (var (algorithm, factor, expected) in new[]
        {
            (ScaleAlgorithm.Scale2x, 2, new uint[] { 1, 2, 2, 0 }),
            (ScaleAlgorithm.Scale3x, 3, new uint[] { 1, 1, 2, 1, 2, 0, 2, 0, 0 })
        })
        {
            var pitch = 3 * factor;
            var output32 = new uint[pitch * pitch];
            var output16 = new ushort[pitch * pitch];
            VideoScaler.Scale32(source, output32, pitch, palette, algorithm, factor);
            VideoScaler.Scale16(source, output16, pitch, palette, algorithm, factor);
            for (var y = 0; y < factor; y++)
            {
                for (var x = 0; x < factor; x++)
                {
                    var position = (factor + y) * pitch + factor + x;
                    Check(output32[position] == expected[y * factor + x] + 0x10000, "Edge-aware center block differs.");
                    Check(output16[position] == expected[y * factor + x], "16-bit palette truncation differs.");
                }
            }

            // The native 16-bit algorithm compares truncated colors, not indices.
            Array.Fill(palette, 0x1234u);
            VideoScaler.Scale16(source, output16, pitch, palette, algorithm, factor);
            Check(output16.All(value => value == 0x1234), "Identical mapped colors should not create edges.");
            palette = Enumerable.Range(0, 256).Select(index => 0x10000u + (uint)index).ToArray();
        }

        var scaler = new VideoScaler();
        Check(VideoScaler.Scalers.Count == 9, "Native scaler inventory differs.");
        Check(scaler.SetByName("Scale3x") && scaler.SelectedIndex == 5, "Scaler selection differs.");
        Check(!scaler.SetByName("scale3x") && scaler.SelectedIndex == 5, "Unknown names must not change selection.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
