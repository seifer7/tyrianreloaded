/*
 * OpenTyrian: A modern cross-platform port of Tyrian
 * Copyright (C) 2007-2009 The OpenTyrian Development Team
 * Distributed under the GNU General Public License, version 2 or later.
 * See COPYING for details.
 */

namespace OpenTyrian.Core;

public sealed partial class LdsPlayer
{
    public void Update() => Advance();

    public bool Advance()
    {
        if (!Playing) return false;
        unchecked
        {
            if (_fade != 0)
            {
                if (_fade <= 128)
                {
                    if (_allVolume > _fade || _allVolume == 0) _allVolume -= _fade;
                    else
                    {
                        _allVolume = 1; _fade = 0;
                        if (_hardFade != 0)
                        {
                            Playing = false; _hardFade = 0;
                            foreach (var channel in _channels) channel.KeyCount = 1;
                        }
                    }
                }
                else if ((byte)(_allVolume + 256 - _fade) <= _mainVolume) _allVolume += (byte)(256 - _fade);
                else { _allVolume = (byte)_mainVolume; _fade = 0; }
            }
            for (var channel = 0; channel < 9; channel++)
            {
                var c = _channels[channel];
                if (c.Delay != 0 && --c.Delay == 0) PlaySound(c.DelayedSound, channel, c.DelayedTune);
            }
            if (_tempoNow == 0 && _positions.Length != 0)
            {
                var breaking = false;
                for (var channel = 0; channel < 9; channel++)
                {
                    var c = _channels[channel];
                    if (c.PackedWait != 0) { c.PackedWait--; continue; }
                    var index = _position * 9 + channel;
                    if (index >= _positions.Length) throw new InvalidDataException("LDS position is outside its order list.");
                    var position = _positions[index];
                    var pattern = position.Pattern + c.PackedPosition;
                    if (pattern >= _patterns.Length) throw new InvalidDataException("LDS packed pattern is outside its data.");
                    var word = _patterns[pattern];
                    var high = (byte)(word >> 8); var low = (byte)word;
                    if (word != 0)
                    {
                        if (high == 0x80) c.PackedWait = low;
                        else if (high >= 0x80) Command(channel, high, low, ref breaking);
                        else
                        {
                            var transpose = (sbyte)((position.Transpose & 127) | ((position.Transpose & 64) != 0 ? 128 : 0));
                            var sound = (byte)(((position.Transpose & 128) != 0 ? low + transpose : low) & 63);
                            var tune = (ushort)(((position.Transpose & 128) != 0 ? high : high + transpose) << 4);
                            if (_delays[channel] == 0) PlaySound(sound, channel, tune);
                            else
                            {
                                c.Delay = _delays[channel]; c.DelayedSound = sound; c.DelayedTune = tune;
                            }
                        }
                    }
                    c.PackedPosition++;
                }
                _tempoNow = _tempo;
                _row++;
                if (breaking)
                {
                    _row = 0; ResetPackedChannels(); _position = _jumpPosition;
                }
                else if (_row >= _patternLength)
                {
                    _row = 0; ResetPackedChannels(); _position = (ushort)((_position + 1) & 255);
                }
            }
            else _tempoNow--;
            for (var channel = 0; channel < 9; channel++) ApplyEffects(channel);
            return Playing && !SongLooped;
        }
    }

    private void ResetPackedChannels()
    {
        foreach (var channel in _channels) { channel.PackedPosition = 0; channel.PackedWait = 0; }
    }

    private void Command(int channel, byte high, byte low, ref bool breaking)
    {
        var c = _channels[channel];
        unchecked
        {
            switch (high)
            {
                case 0xFF:
                    c.CarrierVolume = (byte)((((c.CarrierVolume & 63) * low) >> 6) & 63);
                    if ((_registers[0xC0 + channel] & 1) != 0) c.ModVolume = (byte)((((c.ModVolume & 63) * low) >> 6) & 63);
                    break;
                case 0xFE: _tempo = (byte)(low & 63); break;
                case 0xFD: c.NextVolume = low; break;
                case 0xFC: Playing = false; break;
                case 0xFB: c.KeyCount = 1; break;
                case 0xFA: breaking = true; _jumpPosition = (ushort)((_position + 1) & 255); break;
                case 0xF9:
                    breaking = true; _jumpPosition = low; _jumping = 1;
                    if (_jumpPosition < _position) SongLooped = true;
                    break;
                case 0xF8: c.LastTune = 0; break;
                case 0xF7: c.VibratoWait = 0; c.VibratoSpeed = (byte)((low >> 4) + 2); c.VibratoRate = (byte)((low & 15) + 1); break;
                case 0xF6: c.GlideTo = low; break;
                case 0xF5: c.FineTune = low; break;
                case 0xF4: if (_hardFade == 0) { _allVolume = (byte)(_mainVolume = low); _fade = 0; } break;
                case 0xF3: if (_hardFade == 0) _fade = low; break;
                case 0xF2: c.TremoloStay = low; break;
                case 0xF1 or 0xF0: break;
                default: if (high < 0xA0) c.GlideTo = (byte)(high & 31); break;
            }
        }
    }

    private static ushort OffsetTune(ushort tune, ushort arpeggio)
        => unchecked((ushort)(arpeggio >= 0x800 ? tune - (arpeggio ^ 0xFF0) - 16 : tune + arpeggio));

    private void TuneChannel(int channel, ushort tune)
    {
        var frequency = Frequencies[tune % 192];
        var octave = unchecked((ushort)(tune / 192 - 1));
        Register(0xA0 + channel, frequency & 255);
        Masked(0xB0 + channel, 0x20, ((octave << 2) + (frequency >> 8)) & 0xDF);
    }

    private void ApplyEffects(int channel)
    {
        var c = _channels[channel];
        var register = Operators[channel];
        unchecked
        {
            if (c.KeyCount > 0)
            {
                if (c.KeyCount == 1) Masked(0xB0 + channel, 0xDF, 0);
                c.KeyCount--;
            }
            ushort arpeggio = 0;
            if (c.ArpeggioSize != 0)
            {
                if (c.ArpeggioPosition >= c.ArpeggioTable.Length) throw new InvalidDataException("LDS arpeggio exceeds its twelve-note table.");
                arpeggio = (ushort)(c.ArpeggioTable[c.ArpeggioPosition] << 4);
                if (arpeggio == 0x800)
                {
                    if (c.ArpeggioPosition > 0) c.ArpeggioTable[0] = c.ArpeggioTable[c.ArpeggioPosition - 1];
                    c.ArpeggioSize = 1; c.ArpeggioPosition = 0;
                    arpeggio = (ushort)(c.ArpeggioTable[0] << 4);
                }
                if (c.ArpeggioCount == c.ArpeggioSpeed)
                {
                    c.ArpeggioPosition++;
                    if (c.ArpeggioPosition >= c.ArpeggioSize) c.ArpeggioPosition = 0;
                    c.ArpeggioCount = 0;
                }
                else c.ArpeggioCount++;
            }
            if (c.LastTune != 0 && c.LastTune != c.TargetTune)
            {
                if (c.LastTune > c.TargetTune) c.LastTune = c.LastTune - c.TargetTune < c.PortamentoSpeed
                    ? c.TargetTune : (ushort)(c.LastTune - c.PortamentoSpeed);
                else c.LastTune = c.TargetTune - c.LastTune < c.PortamentoSpeed
                    ? c.TargetTune : (ushort)(c.LastTune + c.PortamentoSpeed);
                TuneChannel(channel, OffsetTune(c.LastTune, arpeggio));
            }
            else if (c.VibratoWait == 0)
            {
                if (c.VibratoRate != 0)
                {
                    var vibrato = VibratoTable[c.VibratoCount & 63] * c.VibratoRate;
                    var tune = (ushort)((c.VibratoCount & 64) == 0 ? c.LastTune + (vibrato >> 8) : c.LastTune - (vibrato >> 8));
                    TuneChannel(channel, OffsetTune(tune, arpeggio));
                    c.VibratoCount += c.VibratoSpeed;
                }
                else if (c.ArpeggioSize != 0) TuneChannel(channel, OffsetTune(c.LastTune, arpeggio));
            }
            else
            {
                c.VibratoWait--;
                if (c.ArpeggioSize != 0) TuneChannel(channel, OffsetTune(c.LastTune, arpeggio));
            }
            Tremolo(channel, register, c.ModVolume, ref c.ModTremoloWait, c.ModTremoloRate, c.ModTremoloSpeed,
                ref c.ModTremoloCount, false);
            Tremolo(channel, register, c.CarrierVolume, ref c.CarrierTremoloWait, c.CarrierTremoloRate, c.CarrierTremoloSpeed,
                ref c.CarrierTremoloCount, true);
        }
    }

    private void Tremolo(int channel, int register, byte volume, ref byte wait, byte rate, byte speed, ref byte count, bool carrier)
    {
        var address = (carrier ? 0x43 : 0x40) + register;
        var faded = _allVolume != 0 && (carrier || (_registers[0xC0 + channel] & 1) != 0);
        if (wait == 0)
        {
            if (rate != 0)
            {
                var tremolo = TremoloTable[count & 127] * rate;
                var level = (tremolo >> 8) <= (volume & 63) ? (volume & 63) - (tremolo >> 8) : 0;
                Masked(address, 0xC0, (faded ? (level * _allVolume) >> 8 : level) ^ 63);
                count = unchecked((byte)(count + speed));
            }
            else Masked(address, 0xC0, ((faded ? ((volume & 63) * _allVolume) >> 8 : volume) ^ 63) & 63);
        }
        else
        {
            wait--;
            if (faded) Masked(address, 0xC0, ((((volume & 63) * _allVolume) >> 8) ^ 63) & 63);
        }
    }
}
