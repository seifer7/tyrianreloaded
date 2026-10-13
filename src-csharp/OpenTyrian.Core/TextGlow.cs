/*
 * OpenTyrian: A modern cross-platform port of Tyrian
 * Copyright (C) 2007-2009 The OpenTyrian Development Team
 * Distributed under the GNU General Public License, version 2 or later.
 * See COPYING for details.
 */

namespace OpenTyrian.Core;

public interface ITextGlowHost
{
    void Draw(int x, int y, ReadOnlyMemory<byte> text, byte hue, int brightness, Font font);
    void SetDelay(int frames);
    bool AnyButton();
    void KeepAlive();
    void Present();
    void Wait();
}

public sealed class TextGlow(ITextGlowHost host)
{
    public uint FrameCount { get; set; }
    public Font Font { get; set; } = Font.Large;
    public byte Brightness { get; set; } = 6;
    public bool WarningRed { get; set; }

    public void Draw(int x, int y, ReadOnlyMemory<byte> text, CancellationToken cancellation = default)
    {
        var hue = WarningRed ? (byte)7 : (byte)15;
        host.Draw(x - 1, y, text, 0, -12, Font);
        host.Draw(x, y - 1, text, 0, -12, Font);
        host.Draw(x + 1, y, text, 0, -12, Font);
        host.Draw(x, y + 1, text, 0, -12, Font);
        if (FrameCount > 0)
            for (var z = 1; z <= 12; z++) Frame(z - 10);
        for (var z = FrameCount == 0 ? 6 : 12; z >= Brightness; z--) Frame(z - 10);
        Brightness = 6;

        void Frame(int value)
        {
            cancellation.ThrowIfCancellationRequested();
            host.SetDelay(unchecked((int)FrameCount));
            host.Draw(x, y, text, hue, value, Font);
            if (host.AnyButton()) FrameCount = 0;
            host.KeepAlive();
            host.Present();
            host.Wait();
        }
    }
}

public sealed class TextGlowHost(IndexedSurface surface, FontRenderer font, InputState input,
    DelayTimer timer, Action keepAlive, Action present) : ITextGlowHost
{
    public void Draw(int x, int y, ReadOnlyMemory<byte> text, byte hue, int brightness, Font selectedFont)
        => font.OutTextAdjust(surface, x, y, text.Span, hue, brightness, selectedFont, false);
    public void SetDelay(int frames) => timer.SetDelay(frames);
    public bool AnyButton() => input.AnyButton();
    public void KeepAlive() => keepAlive();
    public void Present() => present();
    public void Wait() => timer.Wait();
}
