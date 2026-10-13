/*
 * OpenTyrian: A modern cross-platform port of Tyrian
 * Copyright (C) 2007-2013 The OpenTyrian Development Team
 * Distributed under the GNU General Public License, version 2 or later.
 * See COPYING for details.
 */

namespace OpenTyrian.Core;

public sealed class GameGamma(InputState input, PaletteSystem palette)
{
    public byte Correction { get; set; }
    public PaletteColor[] Colors { get; } = new PaletteColor[PaletteSystem.ColorCount];

    public bool Check(ReadOnlySpan<PaletteColor> picturePalette)
    {
        const int f11 = 68;
        if (input.KeysActive[f11] == 0) return false;
        if (picturePalette.Length != PaletteSystem.ColorCount)
            throw new ArgumentException("Game gamma requires the current picture palette.", nameof(picturePalette));
        input.KeysActive[f11] = 0;
        input.NewKey = false;
        Correction = (byte)((Correction + 1) % 4);
        picturePalette.CopyTo(Colors);
        PaletteSystem.CorrectGamma(Colors, Correction);
        palette.SetPalette(Colors, 0, 255);
        return true;
    }
}
