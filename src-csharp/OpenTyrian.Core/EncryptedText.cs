/*
 * OpenTyrian: A modern cross-platform port of Tyrian
 * Copyright (C) 2007-2009 The OpenTyrian Development Team
 * Distributed under the GNU General Public License, version 2 or later.
 * See COPYING for details.
 */

namespace OpenTyrian.Core;

public static class EncryptedText
{
    private static readonly byte[] Key = [204, 129, 63, 255, 71, 19, 25, 62, 1, 99];

    public static void Decrypt(Span<byte> text)
    {
        for (var index = text.Length - 1; index >= 0; index--)
        {
            text[index] ^= Key[index % Key.Length];
            if (index != 0)
            {
                text[index] ^= text[index - 1];
            }
        }
    }

    public static void ReadPascalString(Stream stream, Span<byte> destination)
    {
        Span<byte> length = stackalloc byte[1];
        BinaryFile.ReadExactly(stream, length);
        Span<byte> text = stackalloc byte[length[0]];
        BinaryFile.ReadExactly(stream, text);
        if (destination.IsEmpty)
        {
            return;
        }

        Decrypt(text);
        var count = Math.Min(text.Length, destination.Length - 1);
        text[..count].CopyTo(destination);
        destination[count] = 0;
    }

    public static void SkipPascalString(Stream stream)
    {
        Span<byte> length = stackalloc byte[1];
        BinaryFile.ReadExactly(stream, length);
        Span<byte> buffer = stackalloc byte[length[0]];
        BinaryFile.ReadExactly(stream, buffer);
    }
}
