/*
 * OpenTyrian: A modern cross-platform port of Tyrian
 * Copyright (C) 2007-2009 The OpenTyrian Development Team
 * Distributed under the GNU General Public License, version 2 or later.
 * See COPYING for details.
 */

using System.Runtime.InteropServices;

namespace OpenTyrian.Core;

public sealed class SdlAudioSampleConverter : IAudioSampleConverter
{
    private const int BufferOffset = 16;
    private static readonly int LengthOffset = BufferOffset + IntPtr.Size;
    public int SampleRate { get; }

    public SdlAudioSampleConverter(int sampleRate)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sampleRate);
        SampleRate = sampleRate;
    }

    public short[] Convert(ReadOnlySpan<sbyte> samples)
    {
        if (samples.Length > ushort.MaxValue) throw new ArgumentOutOfRangeException(nameof(samples), "Native sound samples are limited to 64 KiB.");
        // CVT's first fields share offsets on SDL's packed GCC and MSVC ABIs.
        // Reserve the larger MSVC size; SDL owns all trailing filter metadata.
        var converter = Marshal.AllocHGlobal(IntPtr.Size == 8 ? 136 : 88);
        nint buffer = 0;
        try
        {
            Marshal.Copy(new byte[IntPtr.Size == 8 ? 136 : 88], 0, converter, IntPtr.Size == 8 ? 136 : 88);
            var format = BitConverter.IsLittleEndian ? (ushort)0x8010 : (ushort)0x9010;
            if (SdlNative.BuildAudioConverter(converter, 0x8008, 1, 11025, format, 1, SampleRate) < 0)
                throw SdlNative.Error("Failed to build SDL audio converter");
            var multiplier = Marshal.ReadInt32(converter, LengthOffset + 8);
            if (multiplier <= 0) throw new InvalidDataException("SDL returned an invalid audio conversion buffer multiplier.");
            var capacity = checked(samples.Length * multiplier);
            buffer = Marshal.AllocHGlobal(Math.Max(capacity, 1));
            var bytes = MemoryMarshal.AsBytes(samples).ToArray();
            Marshal.Copy(bytes, 0, buffer, bytes.Length);
            Marshal.WriteIntPtr(converter, BufferOffset, buffer);
            Marshal.WriteInt32(converter, LengthOffset, bytes.Length);
            if (SdlNative.ConvertAudio(converter) != 0) throw SdlNative.Error("Failed to convert SDL audio samples");
            var converted = Marshal.ReadInt32(converter, LengthOffset + 4);
            if (converted < 0 || converted > capacity || (converted & 1) != 0)
                throw new InvalidDataException("SDL returned an invalid signed-16 audio buffer length.");
            var output = new short[converted / sizeof(short)];
            Marshal.Copy(buffer, output, 0, output.Length);
            return output;
        }
        finally
        {
            if (buffer != 0) Marshal.FreeHGlobal(buffer);
            Marshal.FreeHGlobal(converter);
        }
    }
}
