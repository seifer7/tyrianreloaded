/*
 * OpenTyrian: A modern cross-platform port of Tyrian
 * Copyright (C) 2007-2009 The OpenTyrian Development Team
 * Distributed under the GNU General Public License, version 2 or later.
 * See COPYING for details.
 */

namespace OpenTyrian.Core;

public static class PictureLoader
{
    public const int PictureCount = 13;
    private static readonly byte[] PicturePalettes = [0, 7, 5, 8, 10, 5, 18, 19, 19, 20, 21, 22, 5];
    private static readonly byte[] FacePalettes = [1, 2, 3, 4, 6, 9, 11, 12, 16, 13, 14, 15];
    public static ReadOnlySpan<byte> FacePaletteIndices => FacePalettes;

    public static void LoadPcx(Stream stream, IndexedSurface surface, Span<PaletteColor> colors)
    {
        ValidateScreen(surface);
        if (!stream.CanSeek || stream.Length < 769 || colors.Length != 256)
        {
            throw new InvalidDataException("PCX file requires a seekable stream and a 256-color destination.");
        }

        stream.Seek(-769, SeekOrigin.End);
        if (ReadByte(stream) == 12)
        {
            Span<byte> rgb = stackalloc byte[3];
            for (var index = 0; index < 256; index++)
            {
                BinaryFile.ReadExactly(stream, rgb);
                colors[index] = new PaletteColor(rgb[0], rgb[1], rgb[2]);
            }
        }

        stream.Position = 128;
        DecodeRle(stream, surface);
    }

    public static void LoadPicture(Stream stream, byte number, IndexedSurface surface, Span<PaletteColor> colors,
        PaletteColor[][] palettes, bool storePalette, PaletteSystem activePalette)
    {
        ValidateScreen(surface);
        if (number is 0 or > PictureCount)
        {
            throw new ArgumentOutOfRangeException(nameof(number));
        }

        if (colors.Length != 256 || palettes.Length < PaletteSystem.PaletteCount)
        {
            throw new ArgumentException("Picture loading requires 23 complete palettes and 256 working colors.");
        }

        var positions = new int[PictureCount + 1];
        stream.Position = 2;
        BinaryFile.ReadInt32(stream, positions.AsSpan(0, PictureCount));
        positions[PictureCount] = checked((int)stream.Length);
        var index = number - 1;
        var start = positions[index];
        var length = (long)positions[index + 1] - start;
        if (start < 0 || length < 0 || start + length > stream.Length)
        {
            throw new InvalidDataException("Picture offsets are outside tyrian.pic.");
        }

        var data = new byte[checked((int)length)];
        stream.Position = start;
        BinaryFile.ReadExactly(stream, data);
        using var compressed = new MemoryStream(data, writable: false);
        DecodeRle(compressed, surface);
        var palette = palettes[PicturePalettes[index]];
        if (palette.Length != 256)
        {
            throw new InvalidDataException("Picture palette must contain 256 colors.");
        }

        palette.CopyTo(colors);
        if (storePalette)
        {
            activePalette.SetPalette(colors, 0, 255);
        }
    }

    private static void DecodeRle(Stream stream, IndexedSurface surface)
    {
        var decoded = 0;
        var position = 0;
        while (decoded < AnimationDecoder.FrameSize)
        {
            var opcode = ReadByte(stream);
            var count = 1;
            var value = opcode;
            if ((opcode & 0xC0) == 0xC0)
            {
                count = opcode & 0x3F;
                value = ReadByte(stream);
            }

            if (decoded + count > AnimationDecoder.FrameSize || position + count > surface.Pixels.Length)
            {
                throw new InvalidDataException("Picture run exceeds the screen buffer.");
            }

            surface.Pixels.Span.Slice(position, count).Fill(value);
            position += count;
            decoded += count;
            if (decoded != 0 && decoded % 320 == 0)
            {
                position += surface.Pitch - 320;
            }
        }
    }

    private static byte ReadByte(Stream stream)
    {
        Span<byte> value = stackalloc byte[1];
        BinaryFile.ReadExactly(stream, value);
        return value[0];
    }

    private static void ValidateScreen(IndexedSurface surface)
    {
        if (surface.Width != 320 || surface.Height != 200)
        {
            throw new ArgumentException("Tyrian pictures require a 320 x 200 indexed surface.", nameof(surface));
        }
    }
}
