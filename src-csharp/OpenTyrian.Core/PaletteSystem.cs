/*
 * OpenTyrian: A modern cross-platform port of Tyrian
 * Copyright (C) 2007-2009 The OpenTyrian Development Team
 * Distributed under the GNU General Public License, version 2 or later.
 * See COPYING for details.
 */

namespace OpenTyrian.Core;

public sealed class PaletteSystem
{
    public const int PaletteCount = 23;
    public const int ColorCount = 256;
    private readonly PaletteColor[] _active = new PaletteColor[ColorCount];
    private readonly uint[] _rgb = new uint[ColorCount];
    private readonly uint[] _yuv = new uint[ColorCount];
    private readonly Func<PaletteColor, uint> _mapRgb;
    public ReadOnlyMemory<PaletteColor> Active => _active;
    public ReadOnlyMemory<uint> Rgb => _rgb;
    public ReadOnlyMemory<uint> Yuv => _yuv;

    public PaletteSystem(Func<PaletteColor, uint> mapRgb)
    {
        ArgumentNullException.ThrowIfNull(mapRgb);
        _mapRgb = mapRgb;
    }

    public static PaletteColor[][] Load(Stream stream)
    {
        if (BinaryFile.Length(stream) / (ColorCount * 3) != PaletteCount)
        {
            throw new InvalidDataException("palette.dat must contain 23 palettes.");
        }

        var palettes = new PaletteColor[PaletteCount][];
        Span<byte> color = stackalloc byte[3];
        for (var palette = 0; palette < PaletteCount; palette++)
        {
            palettes[palette] = new PaletteColor[ColorCount];
            for (var index = 0; index < ColorCount; index++)
            {
                BinaryFile.ReadExactly(stream, color);
                palettes[palette][index] = new PaletteColor(Expand(color[0]), Expand(color[1]), Expand(color[2]));
            }
        }

        return palettes;
    }

    private static byte Expand(byte color) => unchecked((byte)((color << 2) | (color >> 4)));

    public static byte GammaChannel(byte color, float factor)
    {
        if (!float.IsFinite(factor) || factor < 0)
            throw new ArgumentOutOfRangeException(nameof(factor));
        return (byte)MathF.Min(255, MathF.Round(color * factor, MidpointRounding.AwayFromZero));
    }

    public static void CorrectGamma(Span<PaletteColor> colors, byte gamma)
    {
        if (colors.Length != ColorCount) throw new ArgumentException("Gamma correction requires 256 colors.", nameof(colors));
        var factor = 1f + gamma / 10f;
        for (var i = 0; i < ColorCount; i++)
            colors[i] = new PaletteColor(GammaChannel(colors[i].Red, factor),
                GammaChannel(colors[i].Green, factor), GammaChannel(colors[i].Blue, factor));
    }

    public void SetPalette(ReadOnlySpan<PaletteColor> colors, int first, int last)
    {
        ValidateRange(first, last);
        if (colors.Length <= last)
        {
            throw new ArgumentException("Palette is missing the requested colors.", nameof(colors));
        }

        for (var index = first; index <= last; index++)
        {
            SetColor(index, colors[index]);
        }
    }

    public void SetSolid(PaletteColor color, int first, int last)
    {
        ValidateRange(first, last);
        for (var index = first; index <= last; index++)
        {
            SetColor(index, color);
        }
    }

    public int[,] InitializeFade(ReadOnlySpan<PaletteColor> colors, int first, int last)
    {
        ValidateRange(first, last);
        if (colors.Length <= last)
        {
            throw new ArgumentException("Palette is missing the requested colors.", nameof(colors));
        }

        var differences = new int[ColorCount, 3];
        for (var index = first; index <= last; index++)
        {
            differences[index, 0] = colors[index].Red - _active[index].Red;
            differences[index, 1] = colors[index].Green - _active[index].Green;
            differences[index, 2] = colors[index].Blue - _active[index].Blue;
        }

        return differences;
    }

    public int[,] InitializeSolidFade(PaletteColor color, int first, int last)
    {
        var colors = new PaletteColor[ColorCount];
        Array.Fill(colors, color);
        return InitializeFade(colors, first, last);
    }

    public void StepFade(int[,] differences, int steps, int first, int last)
    {
        ValidateRange(first, last);
        if (steps <= 0 || differences.GetLength(0) != ColorCount || differences.GetLength(1) != 3)
        {
            throw new ArgumentException("Fade requires positive steps and a 256 x 3 difference array.");
        }

        for (var index = first; index <= last; index++)
        {
            var red = differences[index, 0] / steps;
            var green = differences[index, 1] / steps;
            var blue = differences[index, 2] / steps;
            differences[index, 0] -= red;
            differences[index, 1] -= green;
            differences[index, 2] -= blue;
            SetColor(index, new PaletteColor(
                unchecked((byte)(_active[index].Red + red)),
                unchecked((byte)(_active[index].Green + green)),
                unchecked((byte)(_active[index].Blue + blue))));
        }
    }

    public void FadePalette(ReadOnlySpan<PaletteColor> colors, int steps, int first, int last, Action beginDelay, Action present, Action wait)
    {
        RunFade(InitializeFade(colors, first, last), steps, first, last, beginDelay, present, wait);
    }

    public void FadeSolid(PaletteColor color, int steps, int first, int last, Action beginDelay, Action present, Action wait)
    {
        RunFade(InitializeSolidFade(color, first, last), steps, first, last, beginDelay, present, wait);
    }

    public void FadeBlack(int steps, Action beginDelay, Action present, Action wait)
    {
        FadeSolid(new PaletteColor(0, 0, 0), steps, 0, 255, beginDelay, present, wait);
    }

    public void FadeWhite(int steps, Action beginDelay, Action present, Action wait)
    {
        FadeSolid(new PaletteColor(255, 255, 255), steps, 0, 255, beginDelay, present, wait);
    }

    private void RunFade(int[,] differences, int steps, int first, int last, Action beginDelay, Action present, Action wait)
    {
        ArgumentNullException.ThrowIfNull(beginDelay);
        ArgumentNullException.ThrowIfNull(present);
        ArgumentNullException.ThrowIfNull(wait);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(steps);
        for (; steps > 0; steps--)
        {
            beginDelay();
            StepFade(differences, steps, first, last);
            present();
            wait();
        }
    }

    private void SetColor(int index, PaletteColor color)
    {
        _active[index] = color;
        _rgb[index] = _mapRgb(color);
        _yuv[index] = RgbToYuv(color.Red, color.Green, color.Blue);
    }

    public static uint RgbToYuv(int red, int green, int blue)
    {
        var y = (red + green + blue) >> 2;
        var u = 128 + ((red - blue) >> 2);
        var v = 128 + ((-red + 2 * green - blue) >> 3);
        return unchecked((uint)((y << 16) + (u << 8) + v));
    }

    private static void ValidateRange(int first, int last)
    {
        if (first < 0 || last >= ColorCount || last < first)
        {
            throw new ArgumentOutOfRangeException(nameof(first), "Palette ranges must be within 0..255.");
        }
    }
}
