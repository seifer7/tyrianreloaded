/*
 * OpenTyrian: A modern cross-platform port of Tyrian
 * Copyright (C) 2007-2009 The OpenTyrian Development Team
 * Distributed under the GNU General Public License, version 2 or later.
 * See COPYING for details.
 */

namespace OpenTyrian.Core;

public enum InputEventKind
{
    Unknown, FocusLost, FocusGained, WindowResized, KeyDown, KeyUp,
    MouseMotion, MouseButtonDown, MouseButtonUp, TextInput, TextEditing, Quit
}

public readonly record struct InputEvent(InputEventKind Kind, int Scancode = 0, ushort Modifiers = 0,
    byte Button = 0, int X = 0, int Y = 0, int RelativeX = 0, int RelativeY = 0,
    ReadOnlyMemory<byte> Text = default);

public interface IInputPlatform
{
    bool PollEvent(out InputEvent input);
    uint Ticks { get; }
    void Delay(uint milliseconds);
    void SetCursorVisible(bool visible);
    void SetRelativeMouseMode(bool enabled);
    void EnableSystemMouseScaling();
    void ToggleFullscreen();
    void WindowResized();
    void Quit();
    void PushJoysticksAsKeyboard();
    void PollJoysticks();
    bool JoystickDown { get; }
    bool IsNetworkGame { get; }
    void CheckNetwork();
}

public sealed class InputState(IInputPlatform platform, VideoLayout layout) : IDelayPlatform
{
    private bool _relativeEnabled;
    private int _relativeX, _relativeY;
    private readonly byte[] _lastText = new byte[32];
    public byte[] KeysActive { get; } = new byte[512];
    public bool[] MousePressed { get; } = new bool[3];
    public bool EscapePressed { get; set; }
    public bool InputDetected { get; set; }
    public bool NewKey { get; set; }
    public bool NewMouse { get; set; }
    public bool NewText { get; set; }
    public bool KeyDown { get; private set; }
    public bool MouseDown { get; private set; }
    public bool WindowHasFocus { get; private set; }
    public bool MouseInactive { get; set; } = true;
    public int LastScancode { get; private set; }
    public ushort LastModifiers { get; private set; }
    public byte LastMouseButton { get; private set; }
    public int LastMouseX { get; private set; }
    public int LastMouseY { get; private set; }
    public int MouseX { get; private set; }
    public int MouseY { get; private set; }
    public ReadOnlyMemory<byte> LastText => _lastText;
    public bool RelativeEnabled => _relativeEnabled;
    public bool JoystickDown => platform.JoystickDown;
    public uint Ticks => platform.Ticks;
    public void Delay(uint milliseconds) => platform.Delay(milliseconds);
    public void PollJoysticks() => platform.PollJoysticks();

    public void Initialize()
    {
        NewKey = NewMouse = false;
        KeyDown = MouseDown = false;
        platform.SetCursorVisible(false);
        platform.EnableSystemMouseScaling();
    }

    public void FlushEvents()
    {
        while (platform.PollEvent(out _)) { }
    }

    public void ClearKeyboard() { }

    public bool AnyButton()
    {
        platform.PollJoysticks();
        ServiceEvents(true);
        return NewKey || MouseDown || JoystickDown;
    }

    public void SetRelative(bool enabled)
    {
        platform.SetRelativeMouseMode(enabled && WindowHasFocus);
        _relativeEnabled = enabled;
        _relativeX = _relativeY = 0;
    }

    public (ushort X, ushort Y, ushort Button) MousePosition()
    {
        ServiceEvents(false);
        return (unchecked((ushort)MouseX), unchecked((ushort)MouseY), MouseDown ? LastMouseButton : (ushort)0);
    }

    public (int X, int Y) RelativePosition()
    {
        ServiceEvents(false);
        var distance = layout.WindowDistanceToScreen(_relativeX, _relativeY);
        _relativeX = _relativeY = 0;
        return distance;
    }

    public void WaitInput(bool keyboard, bool mouse, bool joystick, CancellationToken cancellation = default) => Wait(keyboard, mouse, joystick, true, cancellation);
    public void WaitNoInput(bool keyboard, bool mouse, bool joystick, CancellationToken cancellation = default) => Wait(keyboard, mouse, joystick, false, cancellation);

    private void Wait(bool keyboard, bool mouse, bool joystick, bool pressed, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        ServiceEvents(false);
        while (((keyboard && KeyDown) || (mouse && MouseDown) || (joystick && JoystickDown)) != pressed)
        {
            cancellation.ThrowIfCancellationRequested();
            platform.Delay(10);
            if (pressed) platform.PushJoysticksAsKeyboard();
            else platform.PollJoysticks();
            ServiceEvents(false);
            if (platform.IsNetworkGame) platform.CheckNetwork();
        }
    }

    public void ServiceEvents(bool clearNew)
    {
        if (clearNew) NewKey = NewMouse = NewText = false;
        while (platform.PollEvent(out var input))
        {
            switch (input.Kind)
            {
                case InputEventKind.FocusLost:
                case InputEventKind.FocusGained:
                    WindowHasFocus = input.Kind == InputEventKind.FocusGained;
                    SetRelative(_relativeEnabled);
                    break;
                case InputEventKind.WindowResized:
                    platform.WindowResized();
                    break;
                case InputEventKind.KeyDown:
                    if ((input.Modifiers & 0x300) != 0 && input.Scancode == 40)
                    {
                        platform.ToggleFullscreen();
                        break;
                    }
                    ValidateScancode(input.Scancode);
                    KeysActive[input.Scancode] = 1;
                    NewKey = KeyDown = true;
                    LastScancode = input.Scancode; LastModifiers = input.Modifiers;
                    MouseInactive = true;
                    return;
                case InputEventKind.KeyUp:
                    ValidateScancode(input.Scancode);
                    KeysActive[input.Scancode] = 0;
                    KeyDown = false;
                    return;
                case InputEventKind.MouseMotion:
                    (MouseX, MouseY) = layout.WindowToScreen(input.X, input.Y);
                    if (_relativeEnabled && WindowHasFocus)
                    {
                        _relativeX = unchecked(_relativeX + input.RelativeX);
                        _relativeY = unchecked(_relativeY + input.RelativeY);
                    }
                    platform.SetCursorVisible(MouseX < 0 || MouseX >= 320 || MouseY < 0 || MouseY >= 200);
                    if (input.RelativeX != 0 || input.RelativeY != 0) MouseInactive = false;
                    break;
                case InputEventKind.MouseButtonDown:
                case InputEventKind.MouseButtonUp:
                    MouseDown = input.Kind == InputEventKind.MouseButtonDown;
                    if (MouseDown)
                    {
                        MouseInactive = false; NewMouse = true;
                        LastMouseButton = input.Button;
                        (LastMouseX, LastMouseY) = layout.WindowToScreen(input.X, input.Y);
                    }
                    var buttonIndex = input.Button switch { 1 => 0, 3 => 1, 2 => 2, _ => -1 };
                    if (buttonIndex >= 0) MousePressed[buttonIndex] = MouseDown;
                    break;
                case InputEventKind.TextInput:
                    var text = input.Text.Span;
                    var end = text.IndexOf((byte)0);
                    var length = Math.Min(end < 0 ? text.Length : end, 31);
                    text[..length].CopyTo(_lastText);
                    _lastText[length] = 0;
                    NewText = true;
                    break;
                case InputEventKind.Quit:
                    platform.Quit();
                    return;
            }
        }
    }

    private static void ValidateScancode(int scancode)
    {
        if ((uint)scancode >= 512) throw new InvalidDataException("Input event scancode is outside SDL's 512-key table.");
    }
}
