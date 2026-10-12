/*
 * OpenTyrian: A modern cross-platform port of Tyrian
 * Copyright (C) 2007-2009 The OpenTyrian Development Team
 * Distributed under the GNU General Public License, version 2 or later.
 * See COPYING for details.
 */

using System.Buffers.Binary;

namespace OpenTyrian.Core;

public enum SpriteEffect { Copy, Blend, HueUnsafe, Hue, HueBlend, Darken, Black, Filter }

public readonly record struct Sprite(ushort Width, ushort Height, byte[]? Data);

public sealed class SpriteTable
{
    public const int Capacity = 151;
    private readonly Sprite[] _sprites = new Sprite[Capacity];
    public int Count { get; private set; }

    public Sprite Get(int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, Capacity);
        return _sprites[index];
    }

    public bool Exists(int index) => Get(index).Data is not null;
    public ushort Width(int index) => Exists(index) ? Get(index).Width : (ushort)0;
    public ushort Height(int index) => Exists(index) ? Get(index).Height : (ushort)0;

    public void Clear()
    {
        Array.Clear(_sprites);
        Count = 0;
    }

    public void LoadFile(string path)
    {
        Clear();
        using var stream = File.OpenRead(path);
        Load(stream);
    }

    public void Load(Stream stream)
    {
        Clear();
        Span<ushort> values = stackalloc ushort[3];
        BinaryFile.ReadUInt16(stream, values[..1]);
        if (values[0] > Capacity)
        {
            throw new InvalidDataException("Sprite table exceeds 151 slots.");
        }

        Count = values[0];
        for (var index = 0; index < Count; index++)
        {
            if (!BinaryFile.ReadBoolean(stream))
            {
                continue;
            }

            BinaryFile.ReadUInt16(stream, values);
            var data = new byte[values[2]];
            BinaryFile.ReadExactly(stream, data);
            _sprites[index] = new Sprite(values[0], values[1], data);
        }
    }

    public void Blit(IndexedSurface surface, int x, int y, int index, SpriteEffect effect = SpriteEffect.Copy, byte hue = 0, sbyte value = 0)
    {
        var sprite = Get(index);
        if (index >= Count || sprite.Data is null)
        {
            throw new ArgumentException("Cannot draw an empty sprite.", nameof(index));
        }

        if (sprite.Width == 0)
        {
            throw new InvalidDataException("Sprite width must be positive.");
        }

        long position = (long)y * surface.Pitch + x;
        var offset = 0;
        var data = sprite.Data;
        for (var input = 0; input < data.Length; input++)
        {
            switch (data[input])
            {
                case 255:
                    if (++input == data.Length)
                    {
                        throw new InvalidDataException("Sprite transparent run is truncated.");
                    }

                    position += data[input];
                    offset += data[input];
                    break;
                case 254:
                    position += sprite.Width - offset;
                    offset = sprite.Width;
                    break;
                case 253:
                    position++;
                    offset++;
                    break;
                default:
                    if (position >= surface.Pixels.Length)
                    {
                        return;
                    }

                    if (position >= 0)
                    {
                        ref var pixel = ref surface.Pixels.Span[(int)position];
                        pixel = SpriteColors.Apply(data[input], pixel, effect, hue, value);
                    }

                    position++;
                    offset++;
                    break;
            }

            if (offset >= sprite.Width)
            {
                position += surface.Pitch - offset;
                offset = 0;
            }
        }
    }
}

internal static class SpriteColors
{
    public static byte Apply(byte source, byte destination, SpriteEffect effect, byte hue, sbyte value)
    {
        var shiftedHue = unchecked((byte)(hue << 4));
        var brightness = unchecked((byte)((source & 15) + value));
        if (brightness > 15)
        {
            brightness = brightness >= 31 ? (byte)0 : (byte)15;
        }

        return effect switch
        {
            SpriteEffect.Copy => source,
            SpriteEffect.Blend => (byte)((source & 0xF0) | (((destination & 15) + (source & 15)) / 2)),
            SpriteEffect.HueUnsafe => unchecked((byte)(shiftedHue | ((source & 15) + value))),
            SpriteEffect.Hue => (byte)(shiftedHue | brightness),
            SpriteEffect.HueBlend => (byte)(shiftedHue | (((destination & 15) + brightness) / 2)),
            SpriteEffect.Darken => (byte)((destination & 0xF0) | ((destination & 15) / 2)),
            SpriteEffect.Black => 0,
            SpriteEffect.Filter => (byte)(hue | (source & 15)),
            _ => throw new ArgumentOutOfRangeException(nameof(effect))
        };
    }
}

public sealed class SpriteSheet
{
    private byte[] _data = [];
    public int Size => _data.Length;

    public void Clear() => _data = [];

    public void LoadFile(string directory, char suffix)
    {
        Clear();
        var name = $"newsh{char.ToLowerInvariant(suffix)}.shp";
        using var stream = File.OpenRead(Path.Combine(directory, name));
        Load(stream, checked((int)stream.Length));
    }

    public void Load(Stream stream, int size)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(size);
        var data = new byte[size];
        BinaryFile.ReadExactly(stream, data);
        _data = data;
    }

    public void Blit(IndexedSurface surface, int x, int y, int index, SpriteEffect effect = SpriteEffect.Copy,
        byte filter = 0, bool clip = false, int? primaryPitch = null)
    {
        if (index <= 0 || (long)index * 2 > _data.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        var input = (int)BinaryPrimitives.ReadUInt16LittleEndian(_data.AsSpan((index - 1) * 2, 2));
        long position = (long)y * surface.Pitch + x;
        while (true)
        {
            var command = ReadByte(ref input);
            if (command == 0x0F)
            {
                return;
            }

            if (clip && y >= surface.Height)
            {
                return;
            }

            var skip = command & 15;
            var count = command >> 4;
            x += skip;
            position += skip;
            if (count == 0)
            {
                y++;
                x -= 12;
                position += (primaryPitch ?? surface.Pitch) - 12;
            }
            else
            {
                while (count-- > 0)
                {
                    var source = ReadByte(ref input);
                    if (!clip && position >= surface.Pixels.Length)
                    {
                        return;
                    }

                    var destination = clip ? (long)y * surface.Pitch + x : position;
                    var visible = clip ? y >= 0 && x >= 0 && x < surface.Pitch : position >= 0;
                    if (visible)
                    {
                        ref var pixel = ref surface.Pixels.Span[(int)destination];
                        pixel = SpriteColors.Apply(source, pixel, effect, filter, 0);
                    }

                    position++;
                    x++;
                }
            }
        }
    }

    public void Blit2x2(IndexedSurface surface, int x, int y, int index, SpriteEffect effect = SpriteEffect.Copy,
        byte filter = 0, bool clip = false, int? primaryPitch = null)
    {
        Blit(surface, x, y, index, effect, filter, clip, primaryPitch);
        Blit(surface, x + 12, y, index + 1, effect, filter, clip, primaryPitch);
        Blit(surface, x, y + 14, index + 19, effect, filter, clip, primaryPitch);
        Blit(surface, x + 12, y + 14, index + 20, effect, filter, clip, primaryPitch);
    }

    private byte ReadByte(ref int position)
    {
        if ((uint)position >= _data.Length)
        {
            throw new InvalidDataException("Compressed sprite data is truncated.");
        }

        return _data[position++];
    }
}

public sealed class MainShapeTables
{
    public SpriteTable[] Tables { get; } = Enumerable.Range(0, 8).Select(_ => new SpriteTable()).ToArray();
    public SpriteSheet[] Sheets { get; } = Enumerable.Range(0, 5).Select(_ => new SpriteSheet()).ToArray();

    public void Load(Stream stream)
    {
        Span<ushort> count = stackalloc ushort[1];
        BinaryFile.ReadUInt16(stream, count);
        if (count[0] != 12)
        {
            throw new InvalidDataException("Main shape archive requires 12 shape tables.");
        }

        var positions = new int[13];
        BinaryFile.ReadInt32(stream, positions.AsSpan(0, 12));
        positions[12] = checked((int)stream.Length);
        for (var index = 0; index < 12; index++)
        {
            if (positions[index] < 0 || positions[index + 1] < positions[index] || positions[index + 1] > stream.Length)
            {
                throw new InvalidDataException("Main shape archive offsets are invalid.");
            }
        }

        for (var index = 0; index < 7; index++)
        {
            stream.Position = positions[index];
            Tables[index].Load(stream);
        }

        for (var index = 0; index < 5; index++)
        {
            Sheets[index].Load(stream, positions[index + 8] - positions[index + 7]);
        }
    }

    public void Clear()
    {
        foreach (var table in Tables)
        {
            table.Clear();
        }

        foreach (var sheet in Sheets)
        {
            sheet.Clear();
        }
    }
}
