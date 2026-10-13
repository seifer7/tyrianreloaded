/*
 * OpenTyrian: A modern cross-platform port of Tyrian
 * Copyright (C) 2007-2009 The OpenTyrian Development Team
 * Distributed under the GNU General Public License, version 2 or later.
 * See COPYING for details.
 */

namespace OpenTyrian.Core;

public sealed class WarningBars(IndexedSurface surface, DelayTimer timer, Action present, Action<byte> playSample)
{
    public byte Color { get; set; }
    public sbyte ColorChange { get; set; }
    public byte SoundDelay { get; set; }

    public void Update()
    {
        if (timer.GetDelayTicks2() != 0) return;
        Color = unchecked((byte)(Color + ColorChange));
        if (Color > 234 || Color < 228) ColorChange = unchecked((sbyte)-ColorChange);
        surface.FillRectangle(0, 0, 319, 5, Color);
        surface.FillRectangle(0, 194, 319, 199, Color);
        present();
        timer.SetDelay2(6);
        if (SoundDelay > 0) SoundDelay--;
        else { SoundDelay = 14; playSample(17); }
    }
}

public sealed class GameTextWindow(Action erase, Action<ReadOnlyMemory<byte>> draw)
{
    public ushort EraseTicks { get; set; }

    public GameTextWindow(IndexedSurface surface, FontRenderer font, SpriteTable options)
        : this(() => options.Blit(surface, 16, 189, 36), text => font.OutText(surface, 20, 190, text.Span, 0, 4)) { }

    public void Show(ReadOnlyMemory<byte> text)
    {
        if (EraseTicks > 0) erase();
        EraseTicks = 100;
        draw(text);
    }
}
