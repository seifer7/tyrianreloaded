/*
 * OpenTyrian: A modern cross-platform port of Tyrian
 * Copyright (C) 2007-2013 The OpenTyrian Development Team
 * Distributed under the GNU General Public License, version 2 or later.
 * See COPYING for details.
 */

namespace OpenTyrian.Core;

public interface ICharacterGlowHost
{
    bool EscapePressed { get; }
    bool WarningEnabled { get; }
    int Width(int sprite);
    void Shade(ushort x, ushort y, ReadOnlyMemory<byte> text, byte hue);
    void Glyph(ushort x, ushort y, int sprite, byte hue, sbyte brightness);
    void Shadow(int x, int y, int sprite);
    void SetDelay2(int frames);
    void SetDelay(int frames);
    uint Remaining();
    bool AnyButton();
    void KeepAlive();
    void UpdateWarning();
    void Delay(uint milliseconds);
    void Present();
}

public sealed class CharacterGlow(TextGlow glow, ICharacterGlowHost host)
{
    public bool UseLastBank { get; set; }

    public void Draw(ushort x, ushort y, ReadOnlyMemory<byte> text, CancellationToken cancellation = default)
    {
        var end = text.Span.IndexOf((byte)0);
        if (end >= 0) text = text[..end];
        if (text.Length > 60) throw new InvalidDataException("Character glow text exceeds the native 60-character buffers.");
        host.SetDelay2(1);
        var hue = glow.WarningRed ? (byte)7 : UseLastBank ? (byte)15 : (byte)14;
        if (text.IsEmpty) return;
        if (glow.FrameCount == 0)
        {
            host.Shade(x, y, text, hue); host.Present(); return;
        }
        var colors = new sbyte[60]; Array.Fill(colors, (sbyte)-8);
        var changes = new sbyte[60]; Array.Fill(changes, (sbyte)1);
        var positions = new ushort[60];
        var cursor = unchecked((short)x);
        for (var i = 0; i < text.Length; i++)
        {
            positions[i] = unchecked((ushort)cursor);
            var character = text.Span[i]; var sprite = FontRenderer.SpriteIndex(character);
            if (character == (byte)' ') cursor = unchecked((short)(cursor + 6));
            else if (sprite != -1) cursor = unchecked((short)(cursor + host.Width(sprite) + 1));
        }
        for (var location = 0; location < text.Length + 28; location++)
        {
            cancellation.ThrowIfCancellationRequested();
            if (host.EscapePressed) continue;
            host.SetDelay(unchecked((int)glow.FrameCount));
            host.KeepAlive();
            var sprite = -1;
            for (var i = location - 28; i <= location; i++)
            {
                if (i < 0 || i >= text.Length) continue;
                sprite = FontRenderer.SpriteIndex(text.Span[i]);
                if (sprite == -1) continue;
                host.Glyph(positions[i], y, sprite, hue, colors[i]);
                colors[i] = unchecked((sbyte)(colors[i] + changes[i]));
                if (colors[i] > 9) changes[i] = -1;
            }
            if (sprite != -1 && location < text.Length)
                host.Shadow(positions[location] + 1, y + 1, sprite);
            if (host.AnyButton()) glow.FrameCount = 0;
            do
            {
                cancellation.ThrowIfCancellationRequested();
                if (host.WarningEnabled) host.UpdateWarning();
                host.Delay(16);
            } while (host.Remaining() != 0 && !host.EscapePressed);
            host.Present();
        }
    }
}

public sealed class CharacterGlowHost(IndexedSurface surface, FontRenderer font, SpriteTable tinyFont,
    InputState input, DelayTimer timer, WarningBars warning, Action keepAlive, Action present) : ICharacterGlowHost
{
    public bool EscapePressed => input.EscapePressed;
    public bool WarningEnabled { get; set; }
    public int Width(int sprite) => tinyFont.Get(sprite).Width;
    public void Shade(ushort x, ushort y, ReadOnlyMemory<byte> text, byte hue)
        => font.ShadeText(surface, x, y, text.Span, hue, 0, TextShade.Partial);
    public void Glyph(ushort x, ushort y, int sprite, byte hue, sbyte brightness)
        => tinyFont.Blit(surface, x, y, sprite, SpriteEffect.Hue, hue, brightness);
    public void Shadow(int x, int y, int sprite) => tinyFont.Blit(surface, x, y, sprite, SpriteEffect.Black);
    public void SetDelay2(int frames) => timer.SetDelay2(frames);
    public void SetDelay(int frames) => timer.SetDelay(frames);
    public uint Remaining() => timer.GetDelayTicks();
    public bool AnyButton() => input.AnyButton();
    public void KeepAlive() => keepAlive();
    public void UpdateWarning() => warning.Update();
    public void Delay(uint milliseconds) => input.Delay(milliseconds);
    public void Present() => present();
}
