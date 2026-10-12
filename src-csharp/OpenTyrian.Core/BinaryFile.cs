/*
 * OpenTyrian: A modern cross-platform port of Tyrian
 * Copyright (C) 2007-2009 The OpenTyrian Development Team
 * Distributed under the GNU General Public License, version 2 or later.
 * See COPYING for details.
 */

using System.Buffers.Binary;
using System.Runtime.InteropServices;

namespace OpenTyrian.Core;

public static class BinaryFile
{
    public static long Length(Stream stream) => stream.Length;

    public static int ReadBytes(Stream stream, Span<byte> destination)
    {
        return stream.ReadAtLeast(destination, destination.Length, throwOnEndOfStream: false);
    }

    public static void ReadExactly(Stream stream, Span<byte> destination)
    {
        try
        {
            stream.ReadExactly(destination);
        }
        catch (EndOfStreamException exception)
        {
            throw new EndOfStreamException("error: An unexpected problem occurred while reading from a file.", exception);
        }
    }

    public static bool ReadBoolean(Stream stream)
    {
        Span<byte> value = stackalloc byte[1];
        ReadExactly(stream, value);
        return value[0] != 0;
    }

    public static void ReadUInt8(Stream stream, Span<byte> destination) => ReadExactly(stream, destination);

    public static void ReadInt8(Stream stream, Span<sbyte> destination)
    {
        ReadExactly(stream, MemoryMarshal.AsBytes(destination));
    }

    public static void ReadUInt16(Stream stream, Span<ushort> destination)
    {
        ReadExactly(stream, MemoryMarshal.AsBytes(destination));
        if (!BitConverter.IsLittleEndian)
        {
            for (var i = 0; i < destination.Length; i++)
            {
                destination[i] = BinaryPrimitives.ReverseEndianness(destination[i]);
            }
        }
    }

    public static void ReadInt16(Stream stream, Span<short> destination)
    {
        ReadUInt16(stream, MemoryMarshal.Cast<short, ushort>(destination));
    }

    public static void ReadUInt32(Stream stream, Span<uint> destination)
    {
        ReadExactly(stream, MemoryMarshal.AsBytes(destination));
        if (!BitConverter.IsLittleEndian)
        {
            for (var i = 0; i < destination.Length; i++)
            {
                destination[i] = BinaryPrimitives.ReverseEndianness(destination[i]);
            }
        }
    }

    public static void ReadInt32(Stream stream, Span<int> destination)
    {
        ReadUInt32(stream, MemoryMarshal.Cast<int, uint>(destination));
    }

    public static void WriteExactly(Stream stream, ReadOnlySpan<byte> source) => stream.Write(source);

    public static void WriteBoolean(Stream stream, bool value)
    {
        Span<byte> bytes = stackalloc byte[1];
        bytes[0] = value ? (byte)1 : (byte)0;
        WriteExactly(stream, bytes);
    }

    public static int WriteBytes(Stream stream, ReadOnlySpan<byte> source)
    {
        WriteExactly(stream, source);
        return source.Length;
    }

    public static void WriteUInt8(Stream stream, ReadOnlySpan<byte> source) => WriteExactly(stream, source);

    public static void WriteInt8(Stream stream, ReadOnlySpan<sbyte> source)
    {
        WriteExactly(stream, MemoryMarshal.AsBytes(source));
    }

    public static void WriteUInt16(Stream stream, ushort value)
    {
        Span<byte> bytes = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, value);
        WriteExactly(stream, bytes);
    }

    public static void WriteInt16(Stream stream, short value) => WriteUInt16(stream, unchecked((ushort)value));

    public static void WriteUInt32(Stream stream, uint value)
    {
        Span<byte> bytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        WriteExactly(stream, bytes);
    }

    public static void WriteInt32(Stream stream, int value) => WriteUInt32(stream, unchecked((uint)value));

    public static void WritePcxHeader(Stream stream, uint width, uint height)
    {
        Span<byte> header = stackalloc byte[128];
        header.Clear();
        header[0] = 10;
        header[1] = 5;
        header[2] = 1;
        header[3] = 8;
        BinaryPrimitives.WriteUInt16LittleEndian(header[8..], unchecked((ushort)(width - 1)));
        BinaryPrimitives.WriteUInt16LittleEndian(header[10..], unchecked((ushort)(height - 1)));
        BinaryPrimitives.WriteUInt16LittleEndian(header[12..], 320);
        BinaryPrimitives.WriteUInt16LittleEndian(header[14..], 200);
        header[65] = 1;
        BinaryPrimitives.WriteUInt16LittleEndian(header[66..], unchecked((ushort)width));
        BinaryPrimitives.WriteUInt16LittleEndian(header[68..], 1);
        WriteExactly(stream, header);
    }
}
