/*
 * OpenTyrian: A modern cross-platform port of Tyrian
 * Copyright (C) 2007-2010 The OpenTyrian Development Team
 * Distributed under the GNU General Public License, version 2 or later.
 * See COPYING for details.
 */

namespace OpenTyrian.Core;

public enum ScaleAlgorithm
{
    Nearest,
    Scale2x,
    Scale3x
}

public readonly record struct ScalerDefinition(string Name, int Factor, bool Supports16Bit);

public sealed class VideoScaler
{
    private static readonly ScalerDefinition[] Definitions =
    [
        new("None", 1, true), new("2x", 2, true), new("Scale2x", 2, true),
        new("hq2x", 2, false), new("3x", 3, true), new("Scale3x", 3, true),
        new("hq3x", 3, false), new("4x", 4, true), new("hq4x", 4, false)
    ];

    public static IReadOnlyList<ScalerDefinition> Scalers => Array.AsReadOnly(Definitions);
    public int SelectedIndex { get; private set; }

    public bool SetByName(string name)
    {
        for (var index = 0; index < Definitions.Length; index++)
        {
            if (Definitions[index].Name == name)
            {
                SelectedIndex = index;
                return true;
            }
        }

        return false;
    }

    public static void Scale32(IndexedSurface source, Span<uint> output, int outputPitch, ReadOnlySpan<uint> palette, ScaleAlgorithm algorithm, int factor)
    {
        Scale(source, output, outputPitch, palette, algorithm, factor);
    }

    public static void Scale16(IndexedSurface source, Span<ushort> output, int outputPitch, ReadOnlySpan<uint> palette, ScaleAlgorithm algorithm, int factor)
    {
        if (palette.Length != 256)
        {
            throw new ArgumentException("Scaling requires a 256-entry RGB palette.", nameof(palette));
        }

        Span<ushort> mapped = stackalloc ushort[256];
        for (var index = 0; index < mapped.Length; index++)
        {
            mapped[index] = unchecked((ushort)palette[index]);
        }

        Scale(source, output, outputPitch, mapped, algorithm, factor);
    }

    private static void Scale<T>(IndexedSurface source, Span<T> output, int outputPitch, ReadOnlySpan<T> palette, ScaleAlgorithm algorithm, int factor)
        where T : unmanaged, IEquatable<T>
    {
        if (factor <= 0 || (algorithm == ScaleAlgorithm.Scale2x && factor != 2)
            || (algorithm == ScaleAlgorithm.Scale3x && factor != 3)
            || !Enum.IsDefined(algorithm))
        {
            throw new ArgumentOutOfRangeException(nameof(factor), "Scale2x and Scale3x require their corresponding integer factors.");
        }

        var width = checked(source.Width * factor);
        var height = checked(source.Height * factor);
        if (palette.Length != 256 || outputPitch < width || (long)outputPitch * height > output.Length)
        {
            throw new ArgumentException("Scaling requires a full palette and sufficient pitched output memory.");
        }

        var pixels = source.Pixels.Span;
        Span<T> block = stackalloc T[9];
        for (var y = 0; y < source.Height; y++)
        {
            var previous = Math.Max(0, y - 1) * source.Pitch;
            var current = y * source.Pitch;
            var next = Math.Min(source.Height - 1, y + 1) * source.Pitch;
            for (var x = 0; x < source.Width; x++)
            {
                var center = palette[pixels[current + x]];
                var position = y * factor * outputPitch + x * factor;
                if (algorithm == ScaleAlgorithm.Nearest)
                {
                    for (var row = 0; row < factor; row++)
                    {
                        output.Slice(position + row * outputPitch, factor).Fill(center);
                    }

                    continue;
                }

                var left = Math.Max(0, x - 1);
                var right = Math.Min(source.Width - 1, x + 1);
                var b = palette[pixels[previous + x]];
                var d = palette[pixels[current + left]];
                var f = palette[pixels[current + right]];
                var h = palette[pixels[next + x]];
                block[..(factor * factor)].Fill(center);
                if (!b.Equals(h) && !d.Equals(f))
                {
                    if (factor == 2)
                    {
                        block[0] = d.Equals(b) ? d : center;
                        block[1] = b.Equals(f) ? f : center;
                        block[2] = d.Equals(h) ? d : center;
                        block[3] = h.Equals(f) ? f : center;
                    }
                    else
                    {
                        var a = palette[pixels[previous + left]];
                        var c = palette[pixels[previous + right]];
                        var g = palette[pixels[next + left]];
                        var i = palette[pixels[next + right]];
                        block[0] = d.Equals(b) ? d : center;
                        block[1] = (d.Equals(b) && !center.Equals(c)) || (b.Equals(f) && !center.Equals(a)) ? b : center;
                        block[2] = b.Equals(f) ? f : center;
                        block[3] = (d.Equals(b) && !center.Equals(g)) || (d.Equals(h) && !center.Equals(a)) ? d : center;
                        block[5] = (b.Equals(f) && !center.Equals(i)) || (h.Equals(f) && !center.Equals(c)) ? f : center;
                        block[6] = d.Equals(h) ? d : center;
                        block[7] = (d.Equals(h) && !center.Equals(i)) || (h.Equals(f) && !center.Equals(g)) ? h : center;
                        block[8] = h.Equals(f) ? f : center;
                    }
                }

                for (var row = 0; row < factor; row++)
                {
                    block.Slice(row * factor, factor).CopyTo(output.Slice(position + row * outputPitch, factor));
                }
            }
        }
    }
}
