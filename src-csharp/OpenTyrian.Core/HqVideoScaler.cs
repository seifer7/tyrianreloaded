/*
 * OpenTyrian HQ scalers, Copyright (C) 2003 MaxSt;
 * Copyright (C) 2007-2010 The OpenTyrian Development Team.
 * Distributed under the GNU General Public License, version 2 or later.
 * See COPYING for details.
 */

namespace OpenTyrian.Core;

public static partial class HqVideoScaler
{
    public static void Scale(IndexedSurface source, Span<uint> output, int outputPitch,
        ReadOnlySpan<uint> rgbPalette, ReadOnlySpan<uint> yuvPalette, int factor)
    {
        if (factor is < 2 or > 4) throw new ArgumentOutOfRangeException(nameof(factor));
        var width = checked(source.Width * factor);
        var height = checked(source.Height * factor);
        if (rgbPalette.Length != 256 || yuvPalette.Length != 256 || outputPitch < width ||
            (long)outputPitch * height > output.Length)
            throw new ArgumentException("HQ scaling requires full RGB/YUV palettes and sufficient pitched output memory.");
        var pixels = source.Pixels.Span;
        Span<uint> colors = stackalloc uint[10];
        Span<uint> yuv = stackalloc uint[10];
        Span<byte> indices = stackalloc byte[10];
        for (var y = 0; y < source.Height; y++)
        {
            var current = y * source.Pitch;
            // Native HQ vertical neighbors use visible width, not surface pitch.
            var previous = y > 0 ? current - source.Width : current;
            var next = y < source.Height - 1 ? current + source.Width : current;
            for (var x = 0; x < source.Width; x++)
            {
                var left = Math.Max(0, x - 1);
                var right = Math.Min(source.Width - 1, x + 1);
                indices[1] = pixels[previous + left]; indices[2] = pixels[previous + x]; indices[3] = pixels[previous + right];
                indices[4] = pixels[current + left]; indices[5] = pixels[current + x]; indices[6] = pixels[current + right];
                indices[7] = pixels[next + left]; indices[8] = pixels[next + x]; indices[9] = pixels[next + right];
                var pattern = 0;
                var flag = 1;
                for (var i = 1; i <= 9; i++)
                {
                    colors[i] = rgbPalette[indices[i]] & 0xfcfcfcfc;
                    yuv[i] = yuvPalette[indices[i]];
                    if (i == 5) continue;
                    if (indices[i] != indices[5] && Different(yuvPalette[indices[5]], yuv[i])) pattern |= flag;
                    flag <<= 1;
                }
                var destination = output[(y * factor * outputPitch + x * factor)..];
                switch (factor)
                {
                    case 2: Pattern2(pattern, destination, outputPitch, colors, yuv); break;
                    case 3: Pattern3(pattern, destination, outputPitch, colors, yuv); break;
                    case 4: Pattern4(pattern, destination, outputPitch, colors, yuv); break;
                }
            }
        }
    }

    internal static bool Different(uint first, uint second)
        => Math.Abs((int)(first & 0xff0000) - (int)(second & 0xff0000)) > 0x300000 ||
           Math.Abs((int)(first & 0xff00) - (int)(second & 0xff00)) > 0x700 ||
           Math.Abs((int)(first & 0xff) - (int)(second & 0xff)) > 6;

    internal static uint Interpolate(int kind, uint first, uint second, uint third = 0)
    {
        unchecked
        {
            return kind switch
            {
                1 => (first * 3 + second) >> 2,
                2 => (first * 2 + second + third) >> 2,
                3 => Weighted(first, second, third, 7, 1, 0, 3),
                4 => Weighted(first, second, third, 2, 7, 7, 4),
                5 => (first + second) >> 1,
                6 => Weighted(first, second, third, 5, 2, 1, 3),
                7 => Weighted(first, second, third, 6, 1, 1, 3),
                8 => Weighted(first, second, third, 5, 3, 0, 3),
                9 => Weighted(first, second, third, 2, 3, 3, 3),
                10 => Weighted(first, second, third, 14, 1, 1, 4),
                _ => throw new ArgumentOutOfRangeException(nameof(kind))
            };
        }
    }

    private static uint Weighted(uint first, uint second, uint third, uint a, uint b, uint c, int shift)
    {
        unchecked
        {
            var greenMask = shift == 3 ? 0x0007f800u : 0x000ff000u;
            var otherMask = shift == 3 ? 0x07f807f8u : 0x0ff00ff0u;
            return (((first & 0xff00) * a + (second & 0xff00) * b + (third & 0xff00) * c) & greenMask) +
                (((first & 0xff00ff) * a + (second & 0xff00ff) * b + (third & 0xff00ff) * c) & otherMask) >> shift;
        }
    }
}
