/*
 * OpenTyrian: A modern cross-platform port of Tyrian
 * Copyright (C) 2007-2009 The OpenTyrian Development Team
 * Distributed under the GNU General Public License, version 2 or later.
 * See COPYING for details.
 */

using System.Runtime.InteropServices;

namespace OpenTyrian.Core;

public interface IInputAuxiliaryServices
{
    void PushJoysticksAsKeyboard();
    void PollJoysticks();
    bool JoystickDown { get; }
    bool IsNetworkGame { get; }
    void CheckNetwork();
}

public sealed class SdlInputPlatform : IInputPlatform, IDisposable
{
    private const uint EventsSubsystem = 0x4000;
    private readonly SdlVideoDevice _video;
    private readonly IInputAuxiliaryServices _auxiliary;
    private readonly Action _quit;
    private nint _event;
    private bool _disposed;
    public bool JoystickDown => _auxiliary.JoystickDown;
    public bool IsNetworkGame => _auxiliary.IsNetworkGame;
    public uint Ticks { get { EnsureOpen(); return SdlNative.GetTicks(); } }
    public ushort Modifiers { get { EnsureOpen(); return unchecked((ushort)SdlNative.GetModState()); } }

    public SdlInputPlatform(SdlVideoDevice video, IInputAuxiliaryServices auxiliary, Action quit)
    {
        ArgumentNullException.ThrowIfNull(video);
        ArgumentNullException.ThrowIfNull(auxiliary);
        ArgumentNullException.ThrowIfNull(quit);
        _video = video; _auxiliary = auxiliary; _quit = quit;
        if (SdlNative.InitSubSystem(EventsSubsystem) != 0) throw SdlNative.Error("Failed to initialize SDL events");
        try { _event = Marshal.AllocHGlobal(56); }
        catch { SdlNative.QuitSubSystem(EventsSubsystem); throw; }
    }

    public bool PollEvent(out InputEvent input)
    {
        EnsureOpen();
        input = default;
        if (SdlNative.PollEvent(_event) == 0) return false;
        var type = unchecked((uint)Marshal.ReadInt32(_event));
        input = type switch
        {
            0x100 => new(InputEventKind.Quit),
            0x200 => new(Marshal.ReadByte(_event, 12) switch
            {
                5 => InputEventKind.WindowResized,
                12 => InputEventKind.FocusGained,
                13 => InputEventKind.FocusLost,
                _ => InputEventKind.Unknown
            }),
            0x300 or 0x301 => new(type == 0x300 ? InputEventKind.KeyDown : InputEventKind.KeyUp,
                Marshal.ReadInt32(_event, 16), unchecked((ushort)Marshal.ReadInt16(_event, 24))),
            0x302 => new(InputEventKind.TextEditing),
            0x303 => ReadText(),
            0x400 => new(InputEventKind.MouseMotion, X: Marshal.ReadInt32(_event, 20), Y: Marshal.ReadInt32(_event, 24),
                RelativeX: Marshal.ReadInt32(_event, 28), RelativeY: Marshal.ReadInt32(_event, 32)),
            0x401 or 0x402 => new(type == 0x401 ? InputEventKind.MouseButtonDown : InputEventKind.MouseButtonUp,
                Button: Marshal.ReadByte(_event, 16), X: Marshal.ReadInt32(_event, 20), Y: Marshal.ReadInt32(_event, 24)),
            _ => new(InputEventKind.Unknown)
        };
        if (type is 0x1000 or 0x1001) SdlNative.Free(Marshal.ReadIntPtr(_event, 8));
        if (type == 0x305) SdlNative.Free(Marshal.ReadIntPtr(_event, IntPtr.Size == 8 ? 16 : 12));
        return true;
    }

    private InputEvent ReadText()
    {
        var text = new byte[32];
        Marshal.Copy(_event + 12, text, 0, 32);
        return new(InputEventKind.TextInput, Text: text);
    }

    public void PostEvent(InputEvent input)
    {
        EnsureOpen();
        Marshal.Copy(new byte[56], 0, _event, 56);
        uint type;
        switch (input.Kind)
        {
            case InputEventKind.FocusLost:
            case InputEventKind.FocusGained:
            case InputEventKind.WindowResized:
                type = 0x200;
                Marshal.WriteByte(_event, 12, input.Kind switch
                {
                    InputEventKind.FocusLost => 13,
                    InputEventKind.FocusGained => 12,
                    _ => 5
                });
                break;
            case InputEventKind.KeyDown:
            case InputEventKind.KeyUp:
                if ((uint)input.Scancode >= 512) throw new ArgumentOutOfRangeException(nameof(input));
                type = input.Kind == InputEventKind.KeyDown ? 0x300u : 0x301u;
                Marshal.WriteByte(_event, 12, input.Kind == InputEventKind.KeyDown ? (byte)1 : (byte)0);
                Marshal.WriteInt32(_event, 16, input.Scancode);
                Marshal.WriteInt16(_event, 24, unchecked((short)input.Modifiers));
                break;
            case InputEventKind.MouseMotion:
                type = 0x400;
                Marshal.WriteInt32(_event, 20, input.X); Marshal.WriteInt32(_event, 24, input.Y);
                Marshal.WriteInt32(_event, 28, input.RelativeX); Marshal.WriteInt32(_event, 32, input.RelativeY);
                break;
            case InputEventKind.MouseButtonDown:
            case InputEventKind.MouseButtonUp:
                type = input.Kind == InputEventKind.MouseButtonDown ? 0x401u : 0x402u;
                Marshal.WriteByte(_event, 16, input.Button);
                Marshal.WriteByte(_event, 17, input.Kind == InputEventKind.MouseButtonDown ? (byte)1 : (byte)0);
                Marshal.WriteInt32(_event, 20, input.X); Marshal.WriteInt32(_event, 24, input.Y);
                break;
            case InputEventKind.TextInput:
            case InputEventKind.TextEditing:
                if (input.Text.Length > 31) throw new ArgumentOutOfRangeException(nameof(input), "Posted SDL text must fit its 31-byte payload.");
                type = input.Kind == InputEventKind.TextInput ? 0x303u : 0x302u;
                var text = input.Text.ToArray(); Marshal.Copy(text, 0, _event + 12, text.Length);
                break;
            case InputEventKind.Quit: type = 0x100; break;
            case InputEventKind.Unknown: type = 0x8000; break;
            default: throw new ArgumentOutOfRangeException(nameof(input));
        }
        Marshal.WriteInt32(_event, unchecked((int)type));
        var result = SdlNative.PushEvent(_event);
        if (result < 0) throw SdlNative.Error("Failed to post SDL event");
        if (result == 0) throw new IOException("SDL event filter rejected the posted input event.");
    }

    public void Delay(uint milliseconds) { EnsureOpen(); SdlNative.Delay(milliseconds); }
    public void SetCursorVisible(bool visible)
    {
        EnsureOpen();
        if (SdlNative.ShowCursor(visible ? 1 : 0) < 0) throw SdlNative.Error("Failed to set SDL cursor visibility");
    }
    public void SetRelativeMouseMode(bool enabled)
    {
        EnsureOpen();
        if (SdlNative.SetRelativeMouseMode(enabled ? 1 : 0) != 0) throw SdlNative.Error("Failed to set SDL relative mouse mode");
    }
    public void EnableSystemMouseScaling()
    {
        EnsureOpen();
        if (SdlNative.SetHint("SDL_MOUSE_RELATIVE_SYSTEM_SCALE", "1") == 0)
            throw new IOException("SDL refused the system relative-mouse scaling hint.");
    }
    public void ToggleFullscreen() { EnsureOpen(); _video.ToggleFullscreen(); }
    public void WindowResized() { EnsureOpen(); _video.WindowResized(); }
    public void Quit() { EnsureOpen(); _quit(); }
    public void PushJoysticksAsKeyboard() { EnsureOpen(); _auxiliary.PushJoysticksAsKeyboard(); }
    public void PollJoysticks() { EnsureOpen(); _auxiliary.PollJoysticks(); }
    public void CheckNetwork() { EnsureOpen(); _auxiliary.CheckNetwork(); }

    private void EnsureOpen() => ObjectDisposedException.ThrowIf(_disposed, this);
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Marshal.FreeHGlobal(_event); _event = 0;
        SdlNative.QuitSubSystem(EventsSubsystem);
        GC.SuppressFinalize(this);
    }
}
