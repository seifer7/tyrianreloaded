using OpenTyrian.Core;

namespace OpenTyrian.Core.Tests;

internal static class IndexedSurfaceTests
{
    public static void Run()
    {
        using var warnings = new StringWriter();
        var surface = new IndexedSurface(8, 5, 10, warnings);
        surface.PixelCross(3, 2, 0xAC);
        Equal(5, surface.Pixels.Span.ToArray().Count(value => value == 0xAC));
        surface.Pixel(-1, 0, 3);
        surface.Pixel(10, 0, 3);
        surface.Pixel(0, -1, 3);
        surface.Pixel(0, 5, 3);
        surface.Rectangle(1, 1, 5, 3, 0x112);
        Equal((byte)0x12, surface.Pixels.Span[11]);
        Equal((byte)0xAC, surface.Pixels.Span[23]);
        surface.FillRectangle(-2, -2, 1, 1, 0xBF);
        Equal((byte)0xBF, surface.Pixels.Span[0]);
        Equal((byte)0xBF, surface.Pixels.Span[11]);
        Equal((byte)0, surface.Pixels.Span[8]);
        surface.ShadeRectangle(0, 0, 1, 1);
        Equal((byte)0xB7, surface.Pixels.Span[0]);
        surface.BrightenRectangle(0, 0, 1, 1);
        Equal((byte)0xB9, surface.Pixels.Span[0]);
        surface.FillRectangle(0, 0, 1, 1, 0xFE);
        surface.BrightenRectangle(0, 0, 1, 1);
        Equal((byte)0xFF, surface.Pixels.Span[0]);
        surface.Rectangle(0, 0, 10, 4, 0);
        surface.ShadeRectangle(0, 0, 10, 4);
        surface.BrightenRectangle(0, 0, 10, 4);
        Equal("!!! WARNING: Rectangle clipped: 0 0 10 4 0\n!!! WARNING: Darker Rectangle clipped: 0 0 10 4\n!!! WARNING: Brighter Rectangle clipped: 0 0 10 4\n", warnings.ToString());
        surface.Pixels.Span.Clear();
        surface.SegmentedGauge(0, 1, 0xA0, 2, 2, 4, 10);
        byte[] row = [0xAC, 0xAC, 0, 0xAC, 0xAC, 0, 0xA6, 0xA6, 0, 0];
        if (!surface.Pixels.Span.Slice(10, 10).SequenceEqual(row) || !surface.Pixels.Span.Slice(20, 10).SequenceEqual(row))
        {
            throw new InvalidOperationException("Segmented gauge differs.");
        }
    }

    private static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException($"Expected {expected}, got {actual}.");
        }
    }
}
