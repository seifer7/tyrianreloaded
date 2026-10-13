/*
 * OpenTyrian: A modern cross-platform port of Tyrian
 * Copyright (C) 2007-2009 The OpenTyrian Development Team
 * Distributed under the GNU General Public License, version 2 or later.
 * See COPYING for details.
 */

namespace OpenTyrian.Core;

public static class LegacySaveCipher
{
    public const int PayloadSize = 2498;
    public const int FileSize = PayloadSize + 4;
    private static readonly byte[] Key = [15, 50, 89, 240, 147, 34, 86, 9, 32, 208];

    public static byte[] Encrypt(ReadOnlySpan<byte> payload)
    {
        if (payload.Length != PayloadSize)
            throw new ArgumentException("Legacy save payload must contain exactly 2498 bytes.", nameof(payload));
        var result = new byte[FileSize];
        payload.CopyTo(result);
        Checksums(payload).CopyTo(result, PayloadSize);
        for (var i = 0; i < PayloadSize; i++)
        {
            result[i] ^= Key[(i + 1) % Key.Length];
            if (i > 0) result[i] ^= result[i - 1];
        }
        return result;
    }

    public static byte[] Decrypt(ReadOnlySpan<byte> file, TextWriter diagnostics)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
        if (file.Length != FileSize)
            throw new InvalidDataException("Legacy save file must contain exactly 2502 bytes.");
        var payload = new byte[PayloadSize];
        for (var i = PayloadSize - 1; i >= 0; i--)
        {
            payload[i] = (byte)(file[i] ^ Key[(i + 1) % Key.Length]);
            if (i > 0) payload[i] ^= file[i - 1];
        }
        var checksums = Checksums(payload);
        string[] names = ["additive", "subtractive", "multiplicative", "XOR'd"];
        var correct = true;
        for (var i = 0; i < 4; i++)
        {
            if (file[PayloadSize + i] == checksums[i]) continue;
            correct = false;
            diagnostics.Write($"Failed {names[i]} checksum: {file[PayloadSize + i]} vs {checksums[i]}\n");
        }
        if (!correct) throw new InvalidDataException("Error reading save file!");
        return payload;
    }

    internal static byte[] Checksums(ReadOnlySpan<byte> payload)
    {
        byte add = 0, subtract = 0, multiply = 1, xor = 0;
        foreach (var value in payload)
        {
            add = unchecked((byte)(add + value));
            subtract = unchecked((byte)(subtract - value));
            multiply = unchecked((byte)(multiply * value + 1));
            xor ^= value;
        }
        return [add, subtract, multiply, xor];
    }

    public static void XorCString(Span<byte> value, ReadOnlySpan<byte> key)
    {
        var end = key.IndexOf((byte)0);
        if (end >= 0) key = key[..end];
        if (key.IsEmpty) throw new ArgumentException("XOR key must not be empty.", nameof(key));
        for (var i = 0; i < value.Length && value[i] != 0; i++) value[i] ^= key[i % key.Length];
    }
}

public sealed class LegacySaveSlot
{
    public ushort Encode { get; set; }
    public ushort Level { get; set; }
    public byte[] Items { get; } = new byte[12];
    public int Score { get; set; }
    public int Score2 { get; set; }
    public byte[] LevelName { get; set; } = [];
    public byte[] Name { get; } = new byte[14];
    public byte Cubes { get; set; }
    public byte[] Power { get; } = new byte[2];
    public byte Episode { get; set; }
    public byte[] LastItems { get; } = new byte[12];
    public byte Difficulty { get; set; }
    public byte SecretHint { get; set; }
    public byte Input1 { get; set; }
    public byte Input2 { get; set; }
    public bool GameHasRepeated { get; set; }
    public byte InitialDifficulty { get; set; }
    public int HighScore1 { get; set; }
    public int HighScore2 { get; set; }
    public byte[] HighScoreName { get; set; } = [];
    public byte HighScoreDifficulty { get; set; }

    internal static LegacySaveSlot Read(Stream stream)
    {
        using var reader = new BinaryReader(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        var slot = new LegacySaveSlot
        {
            Encode = reader.ReadUInt16(),
            Level = reader.ReadUInt16()
        };
        BinaryFile.ReadExactly(stream, slot.Items);
        slot.Score = reader.ReadInt32();
        slot.Score2 = reader.ReadInt32();
        slot.LevelName = ReadPascal(stream, 10);
        BinaryFile.ReadExactly(stream, slot.Name);
        slot.Cubes = reader.ReadByte();
        BinaryFile.ReadExactly(stream, slot.Power);
        slot.Episode = reader.ReadByte();
        BinaryFile.ReadExactly(stream, slot.LastItems);
        slot.Difficulty = reader.ReadByte();
        slot.SecretHint = reader.ReadByte();
        slot.Input1 = reader.ReadByte();
        slot.Input2 = reader.ReadByte();
        slot.GameHasRepeated = BinaryFile.ReadBoolean(stream);
        slot.InitialDifficulty = reader.ReadByte();
        slot.HighScore1 = reader.ReadInt32();
        slot.HighScore2 = reader.ReadInt32();
        slot.HighScoreName = ReadPascal(stream, 30);
        slot.HighScoreDifficulty = reader.ReadByte();
        return slot;
    }

    internal void Write(Stream stream)
    {
        BinaryFile.WriteUInt16(stream, Encode);
        BinaryFile.WriteUInt16(stream, Level);
        BinaryFile.WriteExactly(stream, Items);
        BinaryFile.WriteInt32(stream, Score);
        BinaryFile.WriteInt32(stream, Score2);
        WritePascal(stream, LevelName, 10);
        BinaryFile.WriteExactly(stream, Name);
        stream.WriteByte(Cubes);
        BinaryFile.WriteExactly(stream, Power);
        stream.WriteByte(Episode);
        BinaryFile.WriteExactly(stream, LastItems);
        stream.WriteByte(Difficulty);
        stream.WriteByte(SecretHint);
        stream.WriteByte(Input1);
        stream.WriteByte(Input2);
        BinaryFile.WriteBoolean(stream, GameHasRepeated);
        stream.WriteByte(InitialDifficulty);
        BinaryFile.WriteInt32(stream, HighScore1);
        BinaryFile.WriteInt32(stream, HighScore2);
        WritePascal(stream, HighScoreName, 30);
        stream.WriteByte(HighScoreDifficulty);
    }

    private static byte[] ReadPascal(Stream stream, int size)
    {
        var field = new byte[size];
        BinaryFile.ReadExactly(stream, field);
        if (field[0] >= size) throw new InvalidDataException("Legacy save Pascal string exceeds its fixed-width field.");
        return field.AsSpan(1, field[0]).ToArray();
    }

    private static void WritePascal(Stream stream, ReadOnlySpan<byte> value, int size)
    {
        var end = value.IndexOf((byte)0);
        if (end >= 0) value = value[..end];
        if (value.Length >= size) throw new InvalidDataException("Legacy save string exceeds its fixed-width field.");
        var field = new byte[size];
        field[0] = (byte)value.Length;
        value.CopyTo(field.AsSpan(1));
        BinaryFile.WriteExactly(stream, field);
    }
}

public sealed class LegacySaveArchive
{
    public const int SlotCount = 22, SlotSize = 109, SlotsSize = SlotCount * SlotSize;
    public LegacySaveSlot[] Slots { get; } = Enumerable.Range(0, SlotCount).Select(_ => new LegacySaveSlot()).ToArray();

    public void SortHighScores()
    {
        for (var offset = 0; offset < 18; offset += 3) SortHighScoreGroup(offset);
    }

    public void SortHighScoreGroup(int offset)
    {
        if (offset < 0 || offset > Slots.Length - 3) throw new ArgumentOutOfRangeException(nameof(offset));
        for (var a = 0; a < 2; a++)
        {
            for (var b = a + 1; b < 3; b++)
            {
                var left = Slots[offset + a]; var right = Slots[offset + b];
                if (left.HighScore1 >= right.HighScore1) continue;
                (left.HighScore1, right.HighScore1) = (right.HighScore1, left.HighScore1);
                (left.HighScoreName, right.HighScoreName) = (right.HighScoreName, left.HighScoreName);
                (left.HighScoreDifficulty, right.HighScoreDifficulty) = (right.HighScoreDifficulty, left.HighScoreDifficulty);
            }
        }
    }
    public byte[] ItemAvailability { get; } = new byte[100];
    public ushort EditorLevel { get; set; } = 800;

    public static LegacySaveArchive Read(Stream stream, TextWriter diagnostics)
    {
        var file = new byte[LegacySaveCipher.FileSize];
        BinaryFile.ReadExactly(stream, file);
        var payload = LegacySaveCipher.Decrypt(file, diagnostics);
        using var decoded = new MemoryStream(payload, writable: false);
        var archive = new LegacySaveArchive();
        for (var i = 0; i < SlotCount; i++) archive.Slots[i] = LegacySaveSlot.Read(decoded);
        BinaryFile.ReadExactly(decoded, archive.ItemAvailability);
        archive.EditorLevel = (ushort)((payload[^1] << 8) | payload[^2]);
        return archive;
    }

    public void Write(Stream stream)
    {
        using var payload = new MemoryStream(LegacySaveCipher.PayloadSize);
        foreach (var slot in Slots) slot.Write(payload);
        payload.Write(ItemAvailability);
        var bytes = payload.ToArray();
        // Native save writes high then low, but its loader reads low then high.
        bytes[^2] = (byte)(EditorLevel >> 8);
        bytes[^1] = unchecked((byte)EditorLevel);
        BinaryFile.WriteExactly(stream, LegacySaveCipher.Encrypt(bytes));
    }
}
