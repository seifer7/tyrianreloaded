/*
 * OpenTyrian: A modern cross-platform port of Tyrian
 * Copyright (C) 2007-2009 The OpenTyrian Development Team
 * Distributed under the GNU General Public License, version 2 or later.
 * See COPYING for details.
 */

using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;

namespace OpenTyrian.Core;

public sealed class SdlAudioDevice : IDisposable
{
    private sealed class DeviceUnavailableException(bool subsystemInitialization, string error)
        : IOException(error)
    {
        public bool SubsystemInitialization { get; } = subsystemInitialization;
    }

    private const uint AudioSubsystem = 0x10;
    private readonly object _gate = new();
    private readonly SdlNative.AudioCallback _callback;
    private uint _device;
    private bool _initialized, _disposed;
    private short[] _buffer = [];
    private ExceptionDispatchInfo? _failure;
    private long _completedCallbacks;
    public AudioMixer Mixer { get; }
    public int SampleRate { get; }
    public ushort BufferSamples { get; }
    public long CompletedCallbacks => Interlocked.Read(ref _completedCallbacks);

    public static bool TryInitialize(ref bool audioDisabled, Func<int, AudioMixer> createMixer,
        TextWriter diagnostics, out SdlAudioDevice? device)
    {
        ArgumentNullException.ThrowIfNull(createMixer);
        ArgumentNullException.ThrowIfNull(diagnostics);
        device = null;
        if (audioDisabled) return false;

        try
        {
            device = new SdlAudioDevice(createMixer);
            device.Start();
            return true;
        }
        catch (DeviceUnavailableException error)
        {
            audioDisabled = true;
            diagnostics.WriteLine(error.SubsystemInitialization
                ? $"error: failed to initialize SDL audio: {error.Message}"
                : $"error: SDL failed to open audio device: {error.Message}");
            return false;
        }
        catch
        {
            device?.Dispose();
            device = null;
            throw;
        }
    }

    public static bool TryInitializeOpl(ref bool audioDisabled, TextWriter diagnostics,
        out SdlAudioDevice? device, out LdsPlayer? player, Func<uint>? nextRandom = null)
    {
        LdsPlayer? createdPlayer = null;
        var initialized = TryInitialize(ref audioDisabled, sampleRate =>
        {
            createdPlayer = new LdsPlayer(OplChipState.Initialize((uint)sampleRate), nextRandom);
            return new AudioMixer(sampleRate, createdPlayer);
        }, diagnostics, out device);
        player = initialized
            ? createdPlayer ?? throw new InvalidOperationException("OPL audio initialization did not create an LDS player.")
            : null;
        return initialized;
    }

    public SdlAudioDevice(Func<int, AudioMixer> createMixer)
    {
        ArgumentNullException.ThrowIfNull(createMixer);
        _callback = Mix;
        if (SdlNative.InitSubSystem(AudioSubsystem) != 0)
            throw new DeviceUnavailableException(true, SdlNative.ErrorText);
        _initialized = true;
        try
        {
            var desired = new SdlNative.AudioSpec
            {
                Frequency = 44100,
                Format = BitConverter.IsLittleEndian ? (ushort)0x8010 : (ushort)0x9010,
                Channels = 1,
                Samples = 1024,
                Callback = Marshal.GetFunctionPointerForDelegate(_callback)
            };
            _device = SdlNative.OpenAudioDevice(0, 0, ref desired, out var obtained, 1 | 8);
            if (_device == 0) throw new DeviceUnavailableException(false, SdlNative.ErrorText);
            if (obtained.Frequency < 139 || obtained.Format != desired.Format || obtained.Channels != 1 ||
                obtained.Samples == 0 || obtained.Size != obtained.Samples * sizeof(short))
                throw new InvalidDataException("SDL audio device did not retain signed-16 mono output.");
            SampleRate = obtained.Frequency;
            BufferSamples = obtained.Samples;
            _buffer = new short[BufferSamples];
            Mixer = createMixer(SampleRate) ?? throw new InvalidOperationException("Audio mixer factory returned null.");
        }
        catch
        {
            Release();
            throw;
        }
    }

    public void Start()
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            ThrowIfFaulted();
            SdlNative.PauseAudioDevice(_device, 0);
        }
    }

    public void Pause()
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            SdlNative.PauseAudioDevice(_device, 1);
            ThrowIfFaulted();
        }
    }

    public void ThrowIfFaulted() => Volatile.Read(ref _failure)?.Throw();

    private void Mix(nint userData, nint stream, int length)
    {
        try
        {
            if (length != _buffer.Length * sizeof(short))
                throw new InvalidDataException("SDL callback length differs from the obtained device specification.");
            if (Volatile.Read(ref _failure) == null) Mixer.Mix(_buffer);
            else Array.Clear(_buffer);
            Marshal.Copy(_buffer, 0, stream, _buffer.Length);
            Interlocked.Increment(ref _completedCallbacks);
        }
        catch (Exception error)
        {
            // Managed exceptions must not unwind SDL's native callback thread.
            // The host checks the retained fault each frame and after stopping.
            Interlocked.CompareExchange(ref _failure, ExceptionDispatchInfo.Capture(error), null);
            for (var i = 0; i < length; i++) Marshal.WriteByte(stream, i, 0);
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            Release();
            Mixer.ClearChannels();
            ThrowIfFaulted();
        }
        GC.SuppressFinalize(this);
    }

    private void Release()
    {
        if (_device != 0)
        {
            SdlNative.PauseAudioDevice(_device, 1);
            SdlNative.CloseAudioDevice(_device);
            _device = 0;
        }
        if (_initialized)
        {
            SdlNative.QuitSubSystem(AudioSubsystem);
            _initialized = false;
        }
        GC.KeepAlive(_callback);
    }
}
