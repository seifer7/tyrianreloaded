/*
 * OpenTyrian: A modern cross-platform port of Tyrian
 * Copyright (C) 2007-2009 The OpenTyrian Development Team
 * Distributed under the GNU General Public License, version 2 or later.
 * See COPYING for details.
 */

namespace OpenTyrian.Core;

public enum Font { Large, Normal, Small }
public enum FontAlignment { Left, Centered, Right }
public enum TextShade : byte { Partial, Full, Darken, Blend, None = 255 }

public sealed class FontRenderer(MainShapeTables shapes)
{
    private static readonly int[] CharacterSprites = CreateCharacterMap();

    public static int SpriteIndex(byte character) => CharacterSprites[character];

    public int TextWidth(ReadOnlySpan<byte> text, Font font)
    {
        var width = 0;
        foreach (var character in text)
        {
            if (character == 0)
            {
                break;
            }

            if (character == (byte)' ')
            {
                width += 6;
            }
            else if (SpriteIndex(character) is var index && index != -1)
            {
                // Native measurement includes one pixel even for an empty glyph.
                width += shapes.Tables[(int)font].Get(index).Width + 1;
            }
        }

        return width;
    }

    public int Center(ReadOnlySpan<byte> text, Font font) => 160 - TextWidth(text, font) / 2;

    public void DrawHue(IndexedSurface surface, int x, int y, ReadOnlySpan<byte> text, Font font,
        FontAlignment alignment, byte hue, sbyte value)
        => Draw(surface, x, y, text, font, alignment, SpriteEffect.Hue, hue, value);

    public void DrawBlend(IndexedSurface surface, int x, int y, ReadOnlySpan<byte> text, Font font,
        FontAlignment alignment, byte hue, sbyte value)
        => Draw(surface, x, y, text, font, alignment, SpriteEffect.HueBlend, hue, value);

    public void DrawDark(IndexedSurface surface, int x, int y, ReadOnlySpan<byte> text, Font font,
        FontAlignment alignment, bool black)
        => Draw(surface, x, y, text, font, alignment, black ? SpriteEffect.Black : SpriteEffect.Darken, 0, 0);

    public void DrawShadow(IndexedSurface surface, int x, int y, ReadOnlySpan<byte> text, Font font,
        FontAlignment alignment, byte hue, sbyte value, bool black, int distance)
    {
        DrawDark(surface, x + distance, y + distance, text, font, alignment, black);
        DrawHue(surface, x, y, text, font, alignment, hue, value);
    }

    public void DrawFullShadow(IndexedSurface surface, int x, int y, ReadOnlySpan<byte> text, Font font,
        FontAlignment alignment, byte hue, sbyte value, bool black, int distance)
    {
        DrawDark(surface, x, y - distance, text, font, alignment, black);
        DrawDark(surface, x + distance, y, text, font, alignment, black);
        DrawDark(surface, x, y + distance, text, font, alignment, black);
        DrawDark(surface, x - distance, y, text, font, alignment, black);
        DrawHue(surface, x, y, text, font, alignment, hue, value);
    }

    public void DrawString(IndexedSurface surface, int x, int y, ReadOnlySpan<byte> text, Font font)
        => DrawLegacy(surface, x, y, text, font, 15, -3, SpriteEffect.HueUnsafe, 2, 2, false);

    public void OutText(IndexedSurface surface, int x, int y, ReadOnlySpan<byte> text, byte hue, int brightness)
        => DrawLegacy(surface, x, y, text, Font.Small, hue, brightness,
            brightness < 0 ? SpriteEffect.Black : SpriteEffect.HueUnsafe, 4, 0, true);

    public void OutTextModify(IndexedSurface surface, int x, int y, ReadOnlySpan<byte> text, byte hue, uint brightness, Font font)
        => DrawLegacy(surface, x, y, text, font, hue, unchecked((int)brightness), SpriteEffect.HueBlend, 0, 0, false);

    public void OutTextAdjust(IndexedSurface surface, int x, int y, ReadOnlySpan<byte> text, byte hue, int brightness, Font font, bool shadow)
        => DrawLegacy(surface, x, y, text, font, hue, brightness, SpriteEffect.Hue, 4, shadow ? 2 : 0, true);

    public void OutTextAndDarken(IndexedSurface surface, int x, int y, ReadOnlySpan<byte> text, byte hue, uint brightness, Font font)
        => DrawLegacy(surface, x, y, text, font, hue, unchecked((int)brightness), SpriteEffect.HueUnsafe, 4, 1, true);

    public void ShadeText(IndexedSurface surface, int x, int y, ReadOnlySpan<byte> text, byte hue, int brightness, TextShade shade)
    {
        switch (shade)
        {
            case TextShade.Partial:
                OutText(surface, x + 1, y + 1, text, 0, -1);
                OutText(surface, x, y, text, hue, brightness);
                break;
            case TextShade.Full:
                OutText(surface, x - 1, y, text, 0, -1);
                OutText(surface, x + 1, y, text, 0, -1);
                OutText(surface, x, y - 1, text, 0, -1);
                OutText(surface, x, y + 1, text, 0, -1);
                OutText(surface, x, y, text, hue, brightness);
                break;
            case TextShade.Darken:
                OutTextAndDarken(surface, x + 1, y + 1, text, hue, unchecked((uint)brightness), Font.Small);
                break;
            case TextShade.Blend:
                OutTextModify(surface, x, y, text, hue, unchecked((uint)brightness), Font.Small);
                break;
            default:
                break;
        }
    }

    private void DrawLegacy(IndexedSurface surface, int x, int y, ReadOnlySpan<byte> text, Font font,
        byte hue, int brightness, SpriteEffect effect, int highlightAmount, int shadowDistance, bool checkTinyFont)
    {
        var highlight = 0;
        var table = shapes.Tables[(int)font];
        foreach (var character in text)
        {
            if (character == 0)
            {
                return;
            }

            if (character == (byte)' ')
            {
                x += 6;
            }
            else if (character == (byte)'~')
            {
                highlight = highlight == 0 ? highlightAmount : 0;
            }
            else if (SpriteIndex(character) is var index && index != -1 &&
                (!checkTinyFont || shapes.Tables[(int)Font.Small].Exists(index)))
            {
                if (shadowDistance != 0)
                {
                    table.Blit(surface, x + shadowDistance, y + shadowDistance, index, SpriteEffect.Darken);
                }

                table.Blit(surface, x, y, index, effect, hue, unchecked((sbyte)(brightness + highlight)));
                x += table.Get(index).Width + 1;
            }
        }
    }

    private void Draw(IndexedSurface surface, int x, int y, ReadOnlySpan<byte> text, Font font,
        FontAlignment alignment, SpriteEffect effect, byte hue, sbyte value)
    {
        x -= alignment switch
        {
            FontAlignment.Left => 0,
            FontAlignment.Centered => TextWidth(text, font) / 2,
            FontAlignment.Right => TextWidth(text, font),
            _ => throw new ArgumentOutOfRangeException(nameof(alignment))
        };
        var table = shapes.Tables[(int)font];
        var highlight = false;
        foreach (var character in text)
        {
            if (character == 0)
            {
                break;
            }

            if (character == (byte)' ')
            {
                x += 6;
            }
            else if (character == (byte)'~')
            {
                if (effect == SpriteEffect.Hue)
                {
                    highlight = !highlight;
                    value = unchecked((sbyte)(value + (highlight ? 4 : -4)));
                }
            }
            else if (SpriteIndex(character) is var index && index != -1 && table.Exists(index))
            {
                table.Blit(surface, x, y, index, effect, hue, value);
                x += table.Get(index).Width + 1;
            }
        }
    }

    private static int[] CreateCharacterMap()
    {
        var map = Enumerable.Repeat(-1, 256).ToArray();
        int[] ascii =
        [
            -1, 26, 33, 60, 61, 62, -1, 32, 64, 65, 63, 84, 29, 83, 28, 80,
            79, 70, 71, 72, 73, 74, 75, 76, 77, 78, 31, 30, -1, 85, -1, 27,
            -1, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14,
            15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 68, 82, 69, -1, -1,
            -1, 34, 35, 36, 37, 38, 39, 40, 41, 42, 43, 44, 45, 46, 47, 48,
            49, 50, 51, 52, 53, 54, 55, 56, 57, 58, 59, 66, 81, 67, -1, -1
        ];
        ascii.CopyTo(map, 32);
        for (var index = 128; index <= 168; index++)
        {
            map[index] = index - 128 + 86;
        }

        return map;
    }
}
