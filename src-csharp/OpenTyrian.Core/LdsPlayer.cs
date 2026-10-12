/*
 * OpenTyrian: A modern cross-platform port of Tyrian
 * Copyright (C) 2007-2009 The OpenTyrian Development Team
 * Distributed under the GNU General Public License, version 2 or later.
 * See COPYING for details.
 */

using System.Buffers.Binary;

namespace OpenTyrian.Core;

public interface IOplSynthesizer
{
    void Reset();
    void Write(byte register, byte value);
    void Render(Span<short> samples);
}

public sealed partial class LdsPlayer(IOplSynthesizer opl) : IMusicPlayer
{
    private sealed class Instrument(byte[] data)
    {
        public byte ModMisc => data[0];
        public byte ModVolume => data[1];
        public byte ModAttackDecay => data[2];
        public byte ModSustainRelease => data[3];
        public byte ModWave => data[4];
        public byte CarrierMisc => data[5];
        public byte CarrierVolume => data[6];
        public byte CarrierAttackDecay => data[7];
        public byte CarrierSustainRelease => data[8];
        public byte CarrierWave => data[9];
        public byte Feedback => data[10];
        public byte KeyOff => data[11];
        public byte Portamento => data[12];
        public byte Glide => data[13];
        public byte FineTune => data[14];
        public byte Vibrato => data[15];
        public byte VibratoDelay => data[16];
        public byte ModTremolo => data[17];
        public byte CarrierTremolo => data[18];
        public byte TremoloWait => data[19];
        public byte Arpeggio => data[20];
        public ReadOnlySpan<byte> ArpeggioTable => data.AsSpan(21, 12);
    }

    private sealed class Channel
    {
        public ushort TargetTune, LastTune, PackedPosition;
        public byte FineTune, GlideTo, PortamentoSpeed, NextVolume, ModVolume, CarrierVolume,
            VibratoWait, VibratoSpeed, VibratoRate, TremoloStay, ModTremoloWait, ModTremoloSpeed,
            ModTremoloRate, ModTremoloCount, CarrierTremoloWait, CarrierTremoloSpeed, CarrierTremoloRate,
            CarrierTremoloCount, ArpeggioSize, ArpeggioSpeed, KeyCount, VibratoCount, ArpeggioPosition,
            ArpeggioCount, PackedWait, Delay, DelayedSound;
        public ushort DelayedTune;
        public byte[] ArpeggioTable { get; } = new byte[12];
    }

    private readonly record struct Position(ushort Pattern, byte Transpose);
    private Instrument[] _instruments = [];
    private Position[] _positions = [];
    private ushort[] _patterns = [];
    private Channel[] _channels = NewChannels();
    private readonly byte[] _registers = new byte[255];
    private byte[] _delays = new byte[9];
    private byte _tempo, _patternLength, _rhythmRegister, _tempoNow, _row, _fade, _allVolume, _hardFade;
    private ushort _position, _jumpPosition, _mainVolume;
    private byte _jumping;
    public bool Playing { get; private set; }
    public bool SongLooped { get; private set; }
    public byte Row => _row;
    public ushort PositionIndex => _position;
    public byte Tempo => _tempo;
    public byte TempoNow => _tempoNow;
    public byte AllVolume => _allVolume;
    public byte FadeSpeed => _fade;

    private static Channel[] NewChannels() => Enumerable.Range(0, 9).Select(_ => new Channel()).ToArray();

    public void Load(ReadOnlySpan<byte> song)
    {
        if (song.Length < 17) throw new EndOfStreamException("LDS song header is truncated.");
        if (song[0] > 2) throw new InvalidDataException("error: failed to load music");
        var tempo = song[3];
        var length = song[4];
        var delays = song.Slice(5, 9).ToArray();
        var rhythm = song[14];
        var patchCount = BinaryPrimitives.ReadUInt16LittleEndian(song[15..]);
        var cursor = 17;
        var patches = new Instrument[patchCount];
        for (var i = 0; i < patches.Length; i++)
        {
            Require(song, cursor, 46);
            patches[i] = new Instrument(song.Slice(cursor, 46).ToArray());
            cursor += 46;
        }
        Require(song, cursor, 2);
        var count = BinaryPrimitives.ReadUInt16LittleEndian(song[cursor..]);
        cursor += 2;
        var positions = new Position[count * 9];
        for (var i = 0; i < positions.Length; i++)
        {
            Require(song, cursor, 3);
            positions[i] = new Position((ushort)(BinaryPrimitives.ReadUInt16LittleEndian(song[cursor..]) / 2), song[cursor + 2]);
            cursor += 3;
        }
        Require(song, cursor, 2);
        cursor += 2;
        var patterns = new ushort[(song.Length - cursor) / 2];
        for (var i = 0; i < patterns.Length; i++) patterns[i] = BinaryPrimitives.ReadUInt16LittleEndian(song[(cursor + i * 2)..]);
        _instruments = patches; _positions = positions; _patterns = patterns;
        _tempo = tempo; _patternLength = length; _delays = delays; _rhythmRegister = rhythm;
        Rewind();
    }

    private static void Require(ReadOnlySpan<byte> song, int cursor, int count)
    {
        if (cursor > song.Length - count) throw new EndOfStreamException("LDS song data is truncated.");
    }

    public void Free()
    {
        _instruments = []; _positions = []; _patterns = [];
        Playing = false;
    }

    public void Rewind()
    {
        _tempoNow = 3;
        Playing = true; SongLooped = false;
        _jumping = _fade = _allVolume = _hardFade = _row = 0;
        _position = _jumpPosition = _mainVolume = 0;
        _channels = NewChannels();
        Array.Clear(_registers);
        opl.Reset();
        opl.Write(1, 0x20); opl.Write(8, 0); opl.Write(0xBD, _rhythmRegister);
        for (var i = 0; i < 9; i++)
        {
            var register = Operators[i];
            opl.Write((byte)(0x20 + register), 0); opl.Write((byte)(0x23 + register), 0);
            opl.Write((byte)(0x40 + register), 0x3F); opl.Write((byte)(0x43 + register), 0x3F);
            opl.Write((byte)(0x60 + register), 0xFF); opl.Write((byte)(0x63 + register), 0xFF);
            opl.Write((byte)(0x80 + register), 0xFF); opl.Write((byte)(0x83 + register), 0xFF);
            opl.Write((byte)(0xE0 + register), 0); opl.Write((byte)(0xE3 + register), 0);
            opl.Write((byte)(0xA0 + i), 0); opl.Write((byte)(0xB0 + i), 0); opl.Write((byte)(0xC0 + i), 0);
        }
    }

    public void Fade(byte speed) => _fade = speed;

    public void SetRegister(byte register, byte value)
    {
        if (register >= _registers.Length) throw new InvalidDataException("LDS register exceeds the native cache.");
        if (_registers[register] == value) return;
        opl.Write(register, value);
        _registers[register] = value;
    }

    public void SetRegisterMasked(byte register, byte mask, byte value)
    {
        if (register >= _registers.Length) throw new InvalidDataException("LDS register exceeds the native cache.");
        SetRegister(register, (byte)((_registers[register] & mask) | value));
    }

    public void Render(Span<short> samples) => opl.Render(samples);

    private void Register(int register, int value) => SetRegister(checked((byte)register), unchecked((byte)value));
    private void Masked(int register, int mask, int value) => SetRegisterMasked(checked((byte)register), (byte)mask, unchecked((byte)value));

    public void PlaySound(int instrument, int channel, int tune)
    {
        if ((uint)channel >= _channels.Length || (uint)instrument >= _instruments.Length)
            throw new InvalidDataException("LDS note references a missing channel or instrument.");
        var c = _channels[channel];
        var patch = _instruments[instrument];
        var register = Operators[channel];
        unchecked
        {
            tune += ((patch.FineTune + c.FineTune + 0x80) & 0xFF) - 0x80;
            if (patch.Arpeggio == 0)
            {
                var adjustment = patch.ArpeggioTable[0] << 4;
                tune += adjustment > 0x800 ? -(adjustment ^ 0xFF0) - 16 : adjustment;
            }
            if (c.GlideTo != 0)
            {
                c.TargetTune = (ushort)tune; c.PortamentoSpeed = c.GlideTo;
                c.GlideTo = c.FineTune = 0;
                return;
            }
            Register(0x20 + register, patch.ModMisc);
            c.ModVolume = c.NextVolume == 0 || (patch.Feedback & 1) == 0 ? patch.ModVolume
                : (byte)((patch.ModVolume & 0xC0) | (((patch.ModVolume & 0x3F) * c.NextVolume) >> 6));
            Register(0x40 + register, ((patch.Feedback & 1) != 0 && _allVolume != 0
                ? (c.ModVolume & 0xC0) | (((c.ModVolume & 0x3F) * _allVolume) >> 8) : c.ModVolume) ^ 0x3F);
            Register(0x60 + register, patch.ModAttackDecay); Register(0x80 + register, patch.ModSustainRelease);
            Register(0xE0 + register, patch.ModWave);
            Register(0x23 + register, patch.CarrierMisc);
            c.CarrierVolume = c.NextVolume == 0 ? patch.CarrierVolume
                : (byte)((patch.CarrierVolume & 0xC0) | (((patch.CarrierVolume & 0x3F) * c.NextVolume) >> 6));
            Register(0x43 + register, (_allVolume != 0
                ? (c.CarrierVolume & 0xC0) | (((c.CarrierVolume & 0x3F) * _allVolume) >> 8) : c.CarrierVolume) ^ 0x3F);
            Register(0x63 + register, patch.CarrierAttackDecay); Register(0x83 + register, patch.CarrierSustainRelease);
            Register(0xE3 + register, patch.CarrierWave); Register(0xC0 + channel, patch.Feedback);
            Masked(0xB0 + channel, 0xDF, 0);
            if (tune < 0) throw new InvalidDataException("LDS note has a negative native frequency-table index.");
            var frequency = Frequencies[tune % 192];
            var octave = (byte)(tune / 192 - 1);
            if (patch.Glide == 0)
            {
                if (patch.Portamento == 0 || c.LastTune == 0)
                {
                    Register(0xA0 + channel, frequency & 0xFF);
                    Register(0xB0 + channel, (octave << 2) + 0x20 + (frequency >> 8));
                    c.LastTune = c.TargetTune = (ushort)tune;
                }
                else
                {
                    c.TargetTune = (ushort)tune; c.PortamentoSpeed = patch.Portamento;
                    Masked(0xB0 + channel, 0xDF, 0x20);
                }
            }
            else
            {
                Register(0xA0 + channel, frequency & 0xFF);
                Register(0xB0 + channel, (octave << 2) + 0x20 + (frequency >> 8));
                c.LastTune = (ushort)tune;
                c.TargetTune = (ushort)(tune + ((patch.Glide + 0x80) & 0xFF) - 0x80);
                c.PortamentoSpeed = patch.Portamento;
            }
            if (patch.Vibrato == 0) c.VibratoWait = c.VibratoSpeed = c.VibratoRate = 0;
            else
            {
                c.VibratoWait = patch.VibratoDelay;
                c.VibratoSpeed = (byte)((patch.Vibrato >> 4) + 2); c.VibratoRate = (byte)((patch.Vibrato & 15) + 1);
            }
            if ((c.TremoloStay & 0xF0) == 0)
            {
                c.ModTremoloWait = (byte)((patch.TremoloWait & 0xF0) >> 3);
                c.ModTremoloSpeed = (byte)(patch.ModTremolo >> 4); c.ModTremoloRate = (byte)(patch.ModTremolo & 15);
                c.ModTremoloCount = 0;
            }
            if ((c.TremoloStay & 15) == 0)
            {
                c.CarrierTremoloWait = (byte)((patch.TremoloWait & 15) << 1);
                c.CarrierTremoloSpeed = (byte)(patch.CarrierTremolo >> 4); c.CarrierTremoloRate = (byte)(patch.CarrierTremolo & 15);
                c.CarrierTremoloCount = 0;
            }
            c.ArpeggioSize = (byte)(patch.Arpeggio & 15); c.ArpeggioSpeed = (byte)(patch.Arpeggio >> 4);
            patch.ArpeggioTable.CopyTo(c.ArpeggioTable);
            c.KeyCount = patch.KeyOff;
            c.NextVolume = c.GlideTo = c.FineTune = c.VibratoCount = c.ArpeggioPosition = c.ArpeggioCount = 0;
        }
    }
}
