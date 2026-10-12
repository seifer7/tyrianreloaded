/*
 * OpenTyrian: A modern cross-platform port of Tyrian
 * Copyright (C) 2007-2009 The OpenTyrian Development Team
 * Distributed under the GNU General Public License, version 2 or later.
 * See COPYING for details.
 */

using System.Buffers.Binary;

namespace OpenTyrian.Core;

public sealed class SizeBuffer
{
    private readonly Memory<byte> _data;

    public uint Length { get; }

    public uint Position { get; private set; }

    public bool Error { get; private set; }

    public SizeBuffer(Memory<byte> data, uint length)
    {
        if (length > data.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(length), "Length exceeds the supplied memory.");
        }

        _data = data[..(int)length];
        Length = length;
    }

    public void Fill(int value, ulong count)
    {
        if (!CanAccess(count))
        {
            Error = true;
            return;
        }

        _data.Span.Slice((int)Position, (int)count).Fill(unchecked((byte)value));
        Position += (uint)count;
    }

    public void CopyFrom(SizeBuffer source, ulong count)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (!CanAccess(count))
        {
            Error = true;
            return;
        }

        if (!source.CanAccess(count))
        {
            source.Error = true;
            return;
        }

        source._data.Span.Slice((int)source.Position, (int)count)
            .CopyTo(_data.Span.Slice((int)Position, (int)count));
        Position += (uint)count;
        source.Position += (uint)count;
    }

    public void Seek(long offset, SeekOrigin origin)
    {
        Position = origin switch
        {
            SeekOrigin.Begin => unchecked((uint)offset),
            SeekOrigin.Current => unchecked(Position + (uint)offset),
            SeekOrigin.End => unchecked(Length - (uint)offset),
            _ => throw new ArgumentOutOfRangeException(nameof(origin))
        };

        Error = Position > Length;
    }

    public uint ReadByte()
    {
        if (!CanAccess(1))
        {
            Error = true;
            return 0;
        }

        return _data.Span[(int)Position++];
    }

    public uint ReadWord()
    {
        if (!CanAccess(sizeof(ushort)))
        {
            Error = true;
            return 0;
        }

        var value = BinaryPrimitives.ReadUInt16LittleEndian(_data.Span.Slice((int)Position, sizeof(ushort)));
        Position += sizeof(ushort);
        return value;
    }

    private bool CanAccess(ulong count)
    {
        return !Error && Position <= Length && count <= Length - Position;
    }
}
