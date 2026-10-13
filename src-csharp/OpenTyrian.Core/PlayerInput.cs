/*
 * OpenTyrian: A modern cross-platform port of Tyrian
 * Copyright (C) 2007-2013 The OpenTyrian Development Team
 * Distributed under the GNU General Public License, version 2 or later.
 * See COPYING for details.
 */

using System.Buffers.Binary;

namespace OpenTyrian.Core;

public sealed class PlayerInput(InputState input, JoystickInput joystickInput, IReadOnlyList<JoystickState> joysticks,
    IReadOnlyList<int> keys, PlayerMovement motion, PlayerCombat combat, LevelEvents level, NetworkRequests requests)
{
    public bool NetworkGame { get; set; }
    public byte LocalPlayer { get; set; } = 1;
    public bool HasMouse { get; set; } = true;
    public bool ThreeMouseButtons { get; set; } = true;
    public bool RecordDemo { get; set; }
    public bool PlayDemo { get; set; }
    public bool ConstantPlay { get; set; }
    public bool InvertVertical { get; set; }
    private const int KeySpeed = 1;
    public byte DemoKeys { get; set; }
    public ushort DemoWait { get; set; }

    public bool ReplayDemo(Stream stream, IReadOnlyList<PlayerState> players, Span<bool> buttons)
    {
        if (players.Count != 2) throw new ArgumentException("Demo playback requires both player states.", nameof(players));
        if (buttons.Length < 4) throw new ArgumentException("Demo playback requires four buttons.", nameof(buttons));
        Span<byte> wait = stackalloc byte[2];
        while (DemoWait == 0)
        {
            var key = stream.ReadByte(); DemoKeys = key < 0 ? (byte)0 : (byte)key;
            wait.Clear();
            var read = BinaryFile.ReadBytes(stream, wait);
            DemoWait = BinaryPrimitives.ReadUInt16BigEndian(wait);
            if (key < 0 || read != 2) return false;
        }
        DemoWait--;
        var player = players[0];
        if ((DemoKeys & 1) != 0) player.Y -= KeySpeed;
        if ((DemoKeys & 2) != 0) player.Y += KeySpeed;
        if ((DemoKeys & 4) != 0) player.X -= KeySpeed;
        if ((DemoKeys & 8) != 0) player.X += KeySpeed;
        buttons[0] = (DemoKeys & 16) != 0; buttons[3] = (DemoKeys & 32) != 0;
        buttons[1] = (DemoKeys & 64) != 0; buttons[2] = (DemoKeys & 128) != 0;
        return true;
    }

    public void Collect(PlayerState player, byte device, byte playerNumber, ref ushort oldX, ref ushort oldY,
        PlayerMovementState movement, Span<bool> buttons, Span<byte> outgoingState,
        Func<bool>? replayDemo = null, Stream? demoRecording = null)
    {
        if (playerNumber is < 1 or > 2) throw new ArgumentOutOfRangeException(nameof(playerNumber));
        if (buttons.Length < 4) throw new ArgumentException("Player input requires four buttons.", nameof(buttons));
        if (NetworkGame && LocalPlayer is < 1 or > 2) throw new InvalidDataException("Local network player must be one or two.");
        if (NetworkGame && playerNumber != LocalPlayer) return;
        if (NetworkGame && outgoingState.Length < 28) throw new ArgumentException("Player input requires a prepared network state.", nameof(outgoingState));
        if (level.EndLevel) player.Y -= 2;
        else
        {
            if (RecordDemo || PlayDemo) device = 1;
            if (PlayDemo && !(replayDemo ?? throw new InvalidOperationException("Demo playback requires a replay service."))())
            { level.EndLevel = true; combat.LevelEnd = 40; }
            if ((device == 0 || device >= 3) && joysticks.Count > 0)
            {
                var first = device == 0 ? 0 : device - 3;
                var last = device == 0 ? joysticks.Count : first + 1;
                if (last > joysticks.Count) throw new InvalidDataException("Player input device exceeds connected joysticks.");
                for (var j = first; j < last; j++)
                {
                    var joystick = joysticks[j]; joystickInput.Poll(joystick);
                    if (joystick.Analog)
                    {
                        movement.MouseX = unchecked((short)(movement.MouseX + joystick.AxisReduce(joystick.X)));
                        movement.MouseY = unchecked((short)(movement.MouseY + joystick.AxisReduce(joystick.Y)));
                        var angle = movement.LinkGunAngle;
                        movement.AnalogLinkGun = joystick.AnalogAngle(ref angle); movement.LinkGunAngle = angle;
                    }
                    else
                    {
                        player.X += (joystick.Direction[3] ? -KeySpeed : 0) + (joystick.Direction[1] ? KeySpeed : 0);
                        player.Y += (joystick.Direction[0] ? -KeySpeed : 0) + (joystick.Direction[2] ? KeySpeed : 0);
                    }
                    buttons[0] |= joystick.Action[0]; buttons[1] |= joystick.Action[2];
                    buttons[2] |= joystick.Action[3]; buttons[3] |= joystick.ActionPressed[1];
                    requests.InGameMenuPressed |= joystick.ActionPressed[4]; requests.PausePressed |= joystick.ActionPressed[5];
                }
            }
            input.ServiceEvents(false);
            if ((device == 0 || device == 2) && HasMouse)
            {
                buttons[0] |= input.MousePressed[0]; buttons[1] |= input.MousePressed[1];
                buttons[2] |= input.MousePressed[ThreeMouseButtons ? 2 : 1];
                var relative = input.RelativePosition();
                movement.MouseX = unchecked((short)(movement.MouseX + relative.X));
                movement.MouseY = unchecked((short)(movement.MouseY + relative.Y));
            }
            if ((device == 0 || device == 1) && !PlayDemo)
            {
                if (keys.Count < 8) throw new InvalidDataException("Player input requires eight keyboard bindings.");
                for (var i = 0; i < 8; i++)
                    if ((uint)keys[i] >= input.KeysActive.Length) throw new InvalidDataException("Player keyboard binding exceeds SDL scancodes.");
                if (Down(0)) player.Y -= KeySpeed;
                if (Down(1)) player.Y += KeySpeed;
                if (Down(2)) player.X -= KeySpeed;
                if (Down(3)) player.X += KeySpeed;
                buttons[0] |= Down(4); buttons[3] |= Down(5); buttons[1] |= Down(6); buttons[2] |= Down(7);
                if (ConstantPlay) { buttons[..4].Fill(true); player.Y++; player.X += motion.ConstantLastX; }
                if (RecordDemo)
                {
                    var changed = false;
                    for (var i = 0; i < 8; i++) changed |= ((DemoKeys & (1 << i)) != 0) != Down(i);
                    DemoWait = unchecked((ushort)(DemoWait + 1));
                    if (changed)
                    {
                        var stream = demoRecording ?? throw new InvalidOperationException("Demo recording requires an output stream.");
                        Span<byte> record = stackalloc byte[3]; BinaryPrimitives.WriteUInt16BigEndian(record, DemoWait);
                        DemoKeys = 0;
                        for (var i = 0; i < 8; i++) if (Down(i)) DemoKeys |= (byte)(1 << i);
                        record[2] = DemoKeys; stream.Write(record); DemoWait = 0;
                    }
                }
            }
            if (InvertVertical) { oldY = unchecked((ushort)(player.Y - (oldY - player.Y))); movement.MouseY = unchecked((short)-movement.MouseY); }
            movement.AccelerationX = unchecked((short)(movement.AccelerationX + player.X - oldX));
            movement.AccelerationY = unchecked((short)(movement.AccelerationY + player.Y - oldY));
            movement.MouseX = (short)Math.Clamp((int)movement.MouseX, -30, 30);
            movement.MouseY = (short)Math.Clamp((int)movement.MouseY, -30, 30);
            player.X += movement.MouseX > 0 ? (movement.MouseX + 3) / 4 : movement.MouseX < 0 ? (movement.MouseX - 3) / 4 : 0;
            player.Y += movement.MouseY > 0 ? (movement.MouseY + 3) / 4 : movement.MouseY < 0 ? (movement.MouseY - 3) / 4 : 0;
            if (movement.MouseX > 3) movement.AccelerationX = unchecked((short)(movement.AccelerationX + 1));
            else if (movement.MouseX < -2) movement.AccelerationX = unchecked((short)(movement.AccelerationX - 1));
            if (movement.MouseY > 2) movement.AccelerationY = unchecked((short)(movement.AccelerationY + 1));
            else if (movement.MouseY < -2) movement.AccelerationY = unchecked((short)(movement.AccelerationY - 1));
        }
        if (NetworkGame)
        {
            ushort bits = 0;
            for (var i = 3; i >= 0; i--) bits = (ushort)((bits << 1) | (buttons[i] ? 1 : 0));
            BinaryPrimitives.WriteUInt16BigEndian(outgoingState[4..], unchecked((ushort)(player.X - oldX)));
            BinaryPrimitives.WriteUInt16BigEndian(outgoingState[6..], unchecked((ushort)(player.Y - oldY)));
            BinaryPrimitives.WriteUInt16BigEndian(outgoingState[8..], unchecked((ushort)movement.AccelerationX));
            BinaryPrimitives.WriteUInt16BigEndian(outgoingState[10..], unchecked((ushort)movement.AccelerationY));
            BinaryPrimitives.WriteUInt16BigEndian(outgoingState[12..], bits);
            player.X = oldX; player.Y = oldY; buttons[..4].Clear();
            movement.AccelerationX = movement.AccelerationY = 0;
        }

        bool Down(int binding) => input.KeysActive[keys[binding]] != 0;
    }
}
