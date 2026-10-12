/*
 * OpenTyrian: A modern cross-platform port of Tyrian
 * Copyright (C) 2007-2009 The OpenTyrian Development Team
 * Distributed under the GNU General Public License, version 2 or later.
 * See COPYING for details.
 */

using System.Runtime.InteropServices;

namespace OpenTyrian.Core;

public sealed class SdlJoysticks : IInputAuxiliaryServices, IDisposable
{
    private readonly Configuration _config;
    private readonly Func<bool> _networkGame;
    private readonly Action _checkNetwork;
    private readonly List<Device> _devices = [];
    private readonly List<JoystickState> _states = [];
    private bool _initialized, _disposed;
    public IReadOnlyList<JoystickState> States => _states.AsReadOnly();
    public JoystickInput Input { get; }
    public bool JoystickDown => Input.Down;
    public bool IsNetworkGame => _networkGame();

    public SdlJoysticks(Configuration config, TextWriter output, TextWriter warnings, Action<InputEvent> post,
        Func<bool> networkGame, Action checkNetwork, bool ignore = false)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(warnings);
        ArgumentNullException.ThrowIfNull(post);
        ArgumentNullException.ThrowIfNull(networkGame);
        ArgumentNullException.ThrowIfNull(checkNetwork);
        _config = config; _networkGame = networkGame; _checkNetwork = checkNetwork;
        Input = new JoystickInput(_states, SdlNative.GetTicks, SdlNative.JoystickUpdate, post);
        if (ignore) return;
        if (SdlNative.InitSubSystem(0x200) != 0) throw SdlNative.Error("Failed to initialize SDL joystick system");
        _initialized = true;
        try
        {
            SdlNative.JoystickEventState(0);
            var count = SdlNative.NumJoysticks();
            if (count < 0) throw SdlNative.Error("Failed to enumerate SDL joysticks");
            for (var i = 0; i < count; i++)
            {
                var handle = SdlNative.JoystickOpen(i);
                if (handle == 0)
                {
                    warnings.WriteLine(SdlNative.Error($"Failed to open SDL joystick {i}").Message);
                    _states.Add(new(null));
                    continue;
                }
                var device = new Device(handle);
                _devices.Add(device);
                var state = new JoystickState(device);
                _states.Add(state);
                output.WriteLine($"joystick detected: {device.Name} ({device.AxisCount} axes, {device.ButtonCount} buttons, {device.HatCount} hats)");
                if (!state.LoadAssignments(config)) state.ResetAssignments();
            }
            if (count == 0) output.WriteLine("no joysticks detected");
        }
        catch { Release(); throw; }
    }

    public void PushJoysticksAsKeyboard() { EnsureOpen(); Input.PushAsKeyboard(); }
    public void PollJoysticks() { EnsureOpen(); Input.PollAll(); }
    public void CheckNetwork() { EnsureOpen(); _checkNetwork(); }

    private sealed class Device(nint handle) : IJoystickDevice
    {
        private nint _handle = handle;
        private nint Handle => _handle != 0 ? _handle : throw new ObjectDisposedException("SDL joystick");
        public string Name => Marshal.PtrToStringUTF8(SdlNative.JoystickName(Handle))
            ?? throw SdlNative.Error("Failed to read SDL joystick name");
        public int AxisCount => SdlNative.JoystickNumAxes(Handle);
        public int ButtonCount => SdlNative.JoystickNumButtons(Handle);
        public int HatCount => SdlNative.JoystickNumHats(Handle);
        public short Axis(int index) => SdlNative.JoystickGetAxis(Handle, index);
        public byte Button(int index) => SdlNative.JoystickGetButton(Handle, index);
        public byte Hat(int index) => SdlNative.JoystickGetHat(Handle, index);
        internal void Close()
        {
            if (_handle == 0) return;
            SdlNative.JoystickClose(_handle); _handle = 0;
        }
    }

    private void EnsureOpen() => ObjectDisposedException.ThrowIf(_disposed, this);
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try { foreach (var state in _states) if (state.Device != null) state.SaveAssignments(_config); }
        finally { Release(); }
        GC.SuppressFinalize(this);
    }

    private void Release()
    {
        foreach (var device in _devices) device.Close();
        _devices.Clear();
        if (_initialized) { SdlNative.QuitSubSystem(0x200); _initialized = false; }
    }
}
