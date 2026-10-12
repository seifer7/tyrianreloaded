/*
 * OpenTyrian: A modern cross-platform port of Tyrian
 * Copyright (C) 2007-2009 The OpenTyrian Development Team
 * Distributed under the GNU General Public License, version 2 or later.
 * See COPYING for details.
 */

namespace OpenTyrian.Core;

public interface IMusicSynthesizer
{
    void Update();
    void Render(Span<short> samples);
}

public sealed class AudioMixer
{
    private readonly object _gate = new();
    private readonly IMusicSynthesizer _music;
    private readonly int[] _volumeFactors = new int[256];
    private readonly ReadOnlyMemory<short>[] _channels = new ReadOnlyMemory<short>[8];
    private readonly int[] _positions = new int[8];
    private readonly byte[] _channelVolumes = new byte[8];
    private readonly int _samplesPerUpdate;
    private readonly int _samplesPerUpdateFraction;
    private int _untilUpdate;
    private int _untilUpdateFraction;
    private byte _musicVolume = 255;
    private byte _sampleVolume = 255;
    private bool _audioDisabled;
    private bool _musicDisabled;
    private bool _samplesDisabled;
    private bool _musicStopped = true;

    public AudioMixer(int sampleRate, IMusicSynthesizer music)
    {
        if (sampleRate < 139 || sampleRate > int.MaxValue / 2)
            throw new ArgumentOutOfRangeException(nameof(sampleRate));
        _music = music;
        _samplesPerUpdate = 2 * (sampleRate / 139);
        _samplesPerUpdateFraction = 2 * (sampleRate % 139);
        for (var i = 1; i < _volumeFactors.Length; i++)
            _volumeFactors[i] = (int)(MathF.Pow(10, (255 - i) * (-30.0f / (20.0f * 255))) * 4096);
    }

    public bool AudioDisabled { get { lock (_gate) return _audioDisabled; } set { lock (_gate) _audioDisabled = value; } }
    public bool MusicDisabled { get { lock (_gate) return _musicDisabled; } set { lock (_gate) _musicDisabled = value; } }
    public bool SamplesDisabled { get { lock (_gate) return _samplesDisabled; } set { lock (_gate) _samplesDisabled = value; } }
    public bool MusicStopped { get { lock (_gate) return _musicStopped; } set { lock (_gate) _musicStopped = value; } }
    public int SamplesUntilUpdate { get { lock (_gate) return _untilUpdate; } }
    public int SamplesUntilUpdateFraction { get { lock (_gate) return _untilUpdateFraction; } }

    public int RemainingSamples(byte channel)
    {
        if (channel >= 8) throw new ArgumentOutOfRangeException(nameof(channel));
        lock (_gate) return _channels[channel].Length - _positions[channel];
    }

    public void SetVolume(byte music, byte samples)
    {
        lock (_gate)
        {
            if (_audioDisabled) return;
            _musicVolume = music;
            _sampleVolume = samples;
        }
    }

    public void Play(ReadOnlyMemory<short> samples, byte channel, byte volume)
    {
        if (channel >= 8) throw new ArgumentOutOfRangeException(nameof(channel));
        if (volume >= 8) throw new ArgumentOutOfRangeException(nameof(volume));
        lock (_gate)
        {
            if (_audioDisabled || _samplesDisabled) return;
            _channels[channel] = samples;
            _positions[channel] = 0;
            _channelVolumes[channel] = volume;
        }
    }

    public void ClearChannels()
    {
        lock (_gate)
        {
            Array.Clear(_channels);
            Array.Clear(_positions);
        }
    }

    internal void ChangeMusic(Action action)
    {
        lock (_gate) action();
    }

    public void Mix(Span<short> samples)
    {
        lock (_gate)
        {
            if (!_musicDisabled && !_musicStopped)
            {
                var remaining = samples;
                while (!remaining.IsEmpty)
                {
                    if (_untilUpdate == 0)
                    {
                        _music.Update();
                        _untilUpdate += _samplesPerUpdate;
                        _untilUpdateFraction += _samplesPerUpdateFraction;
                        if (_untilUpdateFraction >= 139)
                        {
                            _untilUpdate++;
                            _untilUpdateFraction -= 139;
                        }
                    }
                    var count = Math.Min(_untilUpdate, remaining.Length);
                    _music.Render(remaining[..count]);
                    remaining = remaining[count..];
                    _untilUpdate -= count;
                }
            }
            else samples.Clear();

            var musicFactor = _volumeFactors[_musicVolume] * 2;
            if (_samplesDisabled && !_musicDisabled)
            {
                for (var i = 0; i < samples.Length; i++)
                    samples[i] = Clamp(samples[i] * musicFactor);
            }
            else if (!_samplesDisabled)
            {
                Span<int> factors = stackalloc int[8];
                for (var i = 0; i < factors.Length; i++) factors[i] = _volumeFactors[_sampleVolume] * (i + 1) / 8;
                for (var i = 0; i < samples.Length; i++)
                {
                    var mixed = samples[i] * musicFactor;
                    for (var channel = 0; channel < _channels.Length; channel++)
                    {
                        if (_positions[channel] == _channels[channel].Length) continue;
                        mixed += _channels[channel].Span[_positions[channel]++] * factors[_channelVolumes[channel]];
                    }
                    samples[i] = Clamp(mixed);
                }
            }
        }
    }

    private static short Clamp(int sample) => (short)Math.Clamp(sample >> 12, short.MinValue, short.MaxValue);
}
