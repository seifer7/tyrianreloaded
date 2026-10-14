/*
 * OpenTyrian: A modern cross-platform port of Tyrian
 * Copyright (C) 2007-2013 The OpenTyrian Development Team
 * Distributed under the GNU General Public License, version 2 or later.
 * See COPYING for details.
 */

namespace OpenTyrian.Core;

public sealed class GameRuntimeAudioSession : IDisposable
{
    private readonly SdlAudioDevice _device;
    private readonly LdsPlayer _player;
    private readonly GameRuntimeAudio _resources;
    private bool _disposed;

    public MusicController Music { get; }
    public SoundBank Sounds => _resources.Sounds;
    public AudioMixer Mixer => _device.Mixer;
    public int SampleRate => _device.SampleRate;
    public long CompletedCallbacks => _device.CompletedCallbacks;

    private GameRuntimeAudioSession(SdlAudioDevice device, LdsPlayer player, GameRuntimeAudio resources)
    {
        _device = device;
        _player = player;
        _resources = resources;
        Music = new(resources.Music, device.Mixer, player);
    }

    public static bool TryInitialize(DataFiles files, bool christmas, ref bool audioDisabled,
        TextWriter diagnostics, out GameRuntimeAudioSession? session, Func<uint>? nextRandom = null)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(diagnostics);
        session = null;
        if (!SdlAudioDevice.TryInitializeOpl(ref audioDisabled, diagnostics, out var device, out var player, nextRandom))
            return false;
        if (device is null || player is null)
            throw new InvalidOperationException("SDL OPL initialization succeeded without a device and player.");

        GameRuntimeAudio? resources = null;
        try
        {
            resources = GameRuntimeAudio.Load(files, christmas, new SdlAudioSampleConverter(device.SampleRate));
            session = new(device, player, resources);
            return true;
        }
        catch
        {
            try { resources?.Dispose(); }
            finally
            {
                try { device.Dispose(); }
                finally { player.Free(); }
            }
            throw;
        }
    }

    public void Start() { EnsureOpen(); _device.Start(); }
    public void Pause() { EnsureOpen(); _device.Pause(); }
    public void ThrowIfFaulted() { EnsureOpen(); _device.ThrowIfFaulted(); }
    public void PlaySong(uint song) { EnsureOpen(); Music.PlaySong(song); }
    public void RestartSong() { EnsureOpen(); Music.Restart(); }
    public void FadeSong() { EnsureOpen(); Music.Fade(); }
    public void StopSong() { EnsureOpen(); Music.Stop(); }

    public void SetVolume(ushort music, ushort samples)
    {
        EnsureOpen();
        Mixer.SetVolume((byte)Math.Min(music, (ushort)255), (byte)Math.Min(samples, (ushort)255));
    }

    public void PlaySample(byte sample, byte channel = 0, byte volume = 4)
    {
        EnsureOpen();
        Sounds.Play(sample, channel, volume, Mixer.Play);
    }

    private void EnsureOpen() => ObjectDisposedException.ThrowIf(_disposed, this);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try { _device.Dispose(); }
        finally
        {
            try { _resources.Dispose(); }
            finally { _player.Free(); }
        }
        GC.SuppressFinalize(this);
    }
}
