/*
 * OpenTyrian: A modern cross-platform port of Tyrian
 * Copyright (C) 2007-2009 The OpenTyrian Development Team
 * Distributed under the GNU General Public License, version 2 or later.
 * See COPYING for details.
 */

namespace OpenTyrian.Core;

public sealed class EditedShips
{
    public const int PayloadSize = 150;
    public const int FileRecordSize = PayloadSize + 4;
    private static readonly byte[] Key = [58, 23, 16, 192, 254, 82, 113, 147, 62, 99];
    public bool Available { get; private set; }
    public ushort ShapeSize { get; private set; }
    public ReadOnlyMemory<byte> Records { get; private set; } = new byte[PayloadSize];
    public SpriteSheet Shapes { get; private set; } = new();

    public static byte[] Decrypt(ReadOnlySpan<byte> encrypted)
    {
        if (encrypted.Length != FileRecordSize) throw new InvalidDataException("Edited ships require 154 encrypted bytes.");
        var payload = new byte[PayloadSize];
        for (var i = PayloadSize - 1; i >= 0; i--)
        {
            payload[i] = (byte)(encrypted[i] ^ Key[(i + 1) % Key.Length]);
            if (i > 0) payload[i] ^= encrypted[i - 1];
        }
        if (!LegacySaveCipher.Checksums(payload).AsSpan().SequenceEqual(encrypted[PayloadSize..]))
            throw new InvalidDataException("Edited-ship checksum verification failed.");
        return payload;
    }

    public bool Load(string userDirectory)
    {
        FileStream stream;
        try { stream = File.OpenRead(Path.Combine(userDirectory, "newsh$.shp")); }
        catch (FileNotFoundException) { return false; }
        catch (DirectoryNotFoundException) { return false; }
        using (stream) Load(stream);
        return true;
    }

    public void Load(Stream stream)
    {
        if (!stream.CanSeek || stream.Length < FileRecordSize)
            throw new InvalidDataException("Edited-ship archive is truncated or not seekable.");
        var size = unchecked((ushort)(stream.Length - FileRecordSize));
        if (size == 0) throw new InvalidDataException("Edited-ship archive has an empty native sprite read.");
        var shapes = new SpriteSheet();
        shapes.Load(stream, size);
        var encrypted = new byte[FileRecordSize];
        BinaryFile.ReadExactly(stream, encrypted);
        var records = Decrypt(encrypted);
        ShapeSize = size; Shapes = shapes; Records = records; Available = true;
    }
}
