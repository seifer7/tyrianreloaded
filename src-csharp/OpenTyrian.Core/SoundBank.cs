/*
 * OpenTyrian: A modern cross-platform port of Tyrian
 * Copyright (C) 2007-2009 The OpenTyrian Development Team
 * Distributed under the GNU General Public License, version 2 or later.
 * See COPYING for details.
 */

using System.Runtime.InteropServices;

namespace OpenTyrian.Core;

public interface IAudioSampleConverter
{
    // Signed 8-bit mono at 11025 Hz to signed 16-bit mono at the device rate.
    short[] Convert(ReadOnlySpan<sbyte> samples);
}

public sealed class SoundBank
{
    private short[][] _samples = Enumerable.Range(0, 38).Select(_ => Array.Empty<short>()).ToArray();
    public IReadOnlyList<short[]> Samples => _samples;

    public void Clear()
    {
        for (var i = 0; i < _samples.Length; i++) _samples[i] = [];
    }

    public void Load(DataFiles files, bool christmas, IAudioSampleConverter converter)
    {
        using var effects = files.OpenRequired(files.Directory, "tyrian.snd");
        using var voices = files.OpenRequired(files.Directory, christmas ? "voicesc.snd" : "voices.snd");
        Load(effects, voices, converter);
    }

    public void Load(Stream effects, Stream voices, IAudioSampleConverter converter)
    {
        var raw = ReadBank(effects, 29, false).Concat(ReadBank(voices, 9, true)).ToArray();
        var converted = new short[raw.Length][];
        for (var i = 0; i < raw.Length; i++) converted[i] = converter.Convert(MemoryMarshal.Cast<byte, sbyte>(raw[i]));
        _samples = converted;
    }

    private static byte[][] ReadBank(Stream stream, ushort expected, bool trimVoice)
    {
        Span<ushort> count = stackalloc ushort[1];
        BinaryFile.ReadUInt16(stream, count);
        if (count[0] != expected) throw new InvalidDataException($"Sound bank contains {count[0]} records; expected {expected}.");
        var positions = new uint[expected + 1];
        BinaryFile.ReadUInt32(stream, positions.AsSpan(0, expected));
        if (stream.Length > uint.MaxValue) throw new InvalidDataException("Sound bank exceeds the native 32-bit directory size.");
        positions[expected] = (uint)stream.Length;
        var samples = new byte[expected][];
        for (var i = 0; i < samples.Length; i++)
        {
            if (positions[i] > positions[i + 1] || positions[i + 1] > stream.Length)
                throw new InvalidDataException("Sound bank offsets are unordered or outside the file.");
            var size = positions[i + 1] - positions[i];
            if (trimVoice) size = size >= 100 ? size - 100 : 0;
            if (size > ushort.MaxValue) throw new InvalidDataException("Sound sample exceeds the native 64 KiB limit.");
            samples[i] = new byte[size];
            stream.Position = positions[i];
            BinaryFile.ReadExactly(stream, samples[i]);
        }
        return samples;
    }

    public void PlaySample(byte sample, Action<ReadOnlyMemory<short>, byte, byte> play)
        => Play(sample, 0, 4, play);

    public void Play(byte sample, byte channel, byte volume, Action<ReadOnlyMemory<short>, byte, byte> play)
    {
        if (sample is < 1 or > 38) throw new InvalidDataException($"Sound sample {sample} is outside the native sound bank.");
        if (channel >= 8) throw new ArgumentOutOfRangeException(nameof(channel));
        if (volume >= 8) throw new ArgumentOutOfRangeException(nameof(volume));
        play(_samples[sample - 1], channel, volume);
    }
}
