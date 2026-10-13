/*
 * OpenTyrian: A modern cross-platform port of Tyrian
 * Copyright (C) 2007-2013 The OpenTyrian Development Team
 * Distributed under the GNU General Public License, version 2 or later.
 * See COPYING for details.
 */

namespace OpenTyrian.Core;

public interface IEpisodePictureHost
{
    void SelectPcxPalette(int zeroBasedIndex);
    void Clear();
    void LoadPcx();
    void LoadPicture(int number, bool preservePalette);
    void Present();
    void FadePalette(int steps, int firstColor, int lastColor);
}

public sealed class EpisodePictureDisplay(IEpisodePictureHost host)
{
    public int TemporaryPicture { get; set; }

    public void Show(ReadOnlySpan<byte> section, bool constantPlay)
    {
        if (constantPlay)
            return;

        TemporaryPicture = ParseAtoi(section.Length > 3 ? section[3..] : []);
        if (TemporaryPicture > 900)
        {
            var paletteIndex = TemporaryPicture - 901;
            if ((uint)paletteIndex >= PictureLoader.PictureCount)
                throw new InvalidDataException("Episode picture references a missing PCX palette.");
            host.SelectPcxPalette(paletteIndex);
            host.Clear();
            host.Present();
            host.FadePalette(1, 0, 255);
        }
        else
        {
            if (TemporaryPicture == 0)
                host.LoadPcx();
            else
                host.LoadPicture(TemporaryPicture, false);

            host.Present();
            host.FadePalette(10, 0, 255);
        }
    }

    private static int ParseAtoi(ReadOnlySpan<byte> text)
    {
        var index = 0;
        while (index < text.Length && text[index] is 9 or 10 or 11 or 12 or 13 or 32)
            index++;

        var negative = false;
        if (index < text.Length && text[index] is (byte)'+' or (byte)'-')
        {
            negative = text[index] == '-';
            index++;
        }

        long value = 0;
        var digits = 0;
        while (index < text.Length && text[index] is >= (byte)'0' and <= (byte)'9')
        {
            value = checked(value * 10 + text[index] - (byte)'0');
            digits++;
            index++;
        }

        if (digits == 0)
            return 0;

        if (negative)
            value = -value;
        return checked((int)value);
    }
}
