/*
 * OpenTyrian: A modern cross-platform port of Tyrian
 * Copyright (C) 2007-2009 The OpenTyrian Development Team
 * Distributed under the GNU General Public License, version 2 or later.
 * See COPYING for details.
 */

using System.Globalization;
using System.Text.RegularExpressions;

namespace OpenTyrian.Core;

public enum JoystickAssignmentKind { None, Axis, Button, Hat }

public readonly record struct JoystickAssignment(JoystickAssignmentKind Kind, int Number = 0,
    bool XAxis = false, bool Negative = false)
{
    public bool Matches(JoystickAssignment other) => Kind == other.Kind && Kind switch
    {
        JoystickAssignmentKind.None => true,
        JoystickAssignmentKind.Axis => Number == other.Number && Negative == other.Negative,
        JoystickAssignmentKind.Button => Number == other.Number,
        JoystickAssignmentKind.Hat => Number == other.Number && XAxis == other.XAxis && Negative == other.Negative,
        _ => false
    };

    public string ToCode()
    {
        var number = unchecked(Number + 1).ToString(CultureInfo.InvariantCulture);
        var code = Kind switch
        {
            JoystickAssignmentKind.None => "",
            JoystickAssignmentKind.Axis => $"AX {number}{(Negative ? '-' : '+')}",
            JoystickAssignmentKind.Button => $"BTN {number}",
            JoystickAssignmentKind.Hat => $"H {number}{(XAxis ? 'X' : 'Y')}{(Negative ? '-' : '+')}",
            _ => throw new InvalidDataException("Unknown joystick assignment type.")
        };
        return code[..Math.Min(code.Length, 6)];
    }

    public static JoystickAssignment Parse(string code)
    {
        var terminator = code.IndexOf('\0');
        if (terminator >= 0) code = code[..terminator];
        var number = 0;
        var kind = JoystickAssignmentKind.None;
        char axis = '\0', direction = '\0';
        foreach (var (prefix, type, characters) in new[]
        {
            ("AX", JoystickAssignmentKind.Axis, 1), ("BTN", JoystickAssignmentKind.Button, 0),
            ("H", JoystickAssignmentKind.Hat, 2)
        })
        {
            var match = Regex.Match(code, @"\A[ \t\r\n\v\f]*" + prefix + @"[ \t\r\n\v\f]*([+-]?[0-9]+)([\s\S]*)");
            if (!match.Success) continue;
            if (!int.TryParse(match.Groups[1].Value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out number))
                throw new InvalidDataException("Joystick assignment number exceeds native int bounds.");
            var suffix = match.Groups[2].Value;
            if (type == JoystickAssignmentKind.Axis && suffix.Length > 0) direction = suffix[0];
            if (type == JoystickAssignmentKind.Hat)
            {
                if (suffix.Length > 0) axis = suffix[0];
                if (suffix.Length > 1) direction = suffix[1];
            }
            if (suffix.Length >= characters) { kind = type; break; }
        }
        if (number == 0) kind = JoystickAssignmentKind.None;
        else number = unchecked(number - 1);
        return new(kind, number, axis is 'x' or 'X', direction == '-');
    }

    public static string Describe(ReadOnlySpan<JoystickAssignment> assignments)
    {
        if (assignments.Length != 2) throw new ArgumentException("Native joystick actions have two assignments.", nameof(assignments));
        return string.Join(", ", assignments.ToArray().Where(a => a.Kind != JoystickAssignmentKind.None).Select(a => a.ToCode()));
    }
}

public interface IJoystickDevice
{
    string Name { get; }
    int AxisCount { get; }
    int ButtonCount { get; }
    int HatCount { get; }
    short Axis(int index);
    byte Button(int index);
    byte Hat(int index);
}

public sealed class JoystickState(IJoystickDevice? device)
{
    public IJoystickDevice? Device { get; } = device;
    public JoystickAssignment[,] Assignments { get; } = new JoystickAssignment[10, 2];
    public bool Analog { get; set; }
    public int Sensitivity { get; set; } = 5;
    public int Threshold { get; set; } = 5;
    public int X { get; internal set; }
    public int Y { get; internal set; }
    public int[] AnalogDirection { get; } = new int[4];
    public bool[] Direction { get; } = new bool[4];
    public bool[] DirectionPressed { get; } = new bool[4];
    public bool[] Action { get; } = new bool[6];
    public bool[] ActionPressed { get; } = new bool[6];
    public bool Confirm { get; internal set; }
    public bool Cancel { get; internal set; }
    public uint RepeatAt { get; set; }
    public bool InputPressed { get; internal set; }
    private static readonly string[] Names = ["up", "right", "down", "left", "fire", "change fire", "left sidekick", "right sidekick", "menu", "pause"];

    public int AxisThreshold(int value)
    {
        var threshold = (long)Threshold * 1000;
        if (value == int.MinValue || threshold is < int.MinValue or > int.MaxValue)
            throw new InvalidDataException("Joystick threshold arithmetic exceeds native defined bounds.");
        var negative = value < 0;
        var magnitude = Math.Abs(value);
        if (magnitude <= threshold) return 0;
        var reduced = (long)magnitude - threshold;
        if (reduced > int.MaxValue) throw new InvalidDataException("Joystick threshold subtraction exceeds native int bounds.");
        return negative ? -(int)reduced : (int)reduced;
    }

    public int AxisReduce(int value)
    {
        value = AxisThreshold(value);
        if (value == 0) return 0;
        var divisor = 3000L - 200L * Sensitivity;
        if (divisor == 0 || divisor is < int.MinValue or > int.MaxValue)
            throw new InvalidDataException("Joystick sensitivity produces undefined native division.");
        return value / (int)divisor;
    }

    public bool AnalogAngle(ref float angle)
    {
        float x = AxisThreshold(X), y = AxisThreshold(Y);
        if (x != 0)
        {
            angle += MathF.Atan(-y / x);
            angle = (float)(angle + (x < 0 ? -Math.PI / 2 : Math.PI / 2));
            return true;
        }
        if (y == 0) return false;
        angle = (float)(angle + (y < 0 ? Math.PI : 0));
        return true;
    }

    public int CheckAssigned(int action)
    {
        if ((uint)action >= 10) throw new ArgumentOutOfRangeException(nameof(action));
        var device = Device ?? throw new InvalidOperationException("Joystick is not open.");
        var result = 0;
        for (var i = 0; i < 2; i++)
        {
            var assignment = Assignments[action, i];
            var value = assignment.Kind switch
            {
                JoystickAssignmentKind.None => 0,
                JoystickAssignmentKind.Axis => assignment.Negative ? -(int)device.Axis(assignment.Number) : device.Axis(assignment.Number),
                JoystickAssignmentKind.Button => device.Button(assignment.Number) == 1 ? 32767 : 0,
                JoystickAssignmentKind.Hat => HatValue(device.Hat(assignment.Number), assignment),
                _ => throw new InvalidDataException("Unknown joystick assignment type.")
            };
            result = Math.Max(result, value);
        }
        return result;
    }

    private static int HatValue(byte hat, JoystickAssignment assignment)
    {
        var directions = hat & (assignment.XAxis ? 8 | 2 : 1 | 4);
        return (directions & (assignment.Negative ? 8 | 1 : 2 | 4)) != 0 ? 32767 : 0;
    }

    public void ResetAssignments()
    {
        var device = Device ?? throw new InvalidOperationException("Joystick is not open.");
        for (var action = 0; action < 10; action++)
        {
            Assignments[action, 0] = Assignments[action, 0] with { Kind = JoystickAssignmentKind.None };
            Assignments[action, 1] = Assignments[action, 1] with { Kind = JoystickAssignmentKind.None };
            if (action < 4)
            {
                if (device.AxisCount >= 2) Assignments[action, 0] = Assignments[action, 0] with
                { Kind = JoystickAssignmentKind.Axis, Number = (action + 1) % 2, Negative = action is 0 or 3 };
                if (device.HatCount >= 1) Assignments[action, 1] = new(JoystickAssignmentKind.Hat, 0, action is 1 or 3, action is 0 or 3);
            }
            else if (action - 4 < device.ButtonCount) Assignments[action, 0] = Assignments[action, 0] with
            { Kind = JoystickAssignmentKind.Button, Number = action - 4 };
        }
        Analog = false; Sensitivity = Threshold = 5;
    }

    public bool LoadAssignments(Configuration config)
    {
        var section = config.FindSection("joystick", Device?.Name ?? throw new InvalidOperationException("Joystick is not open."));
        if (section == null) return false;
        Analog = section.TryGetBoolean("analog", out var analog) && analog;
        Sensitivity = section.GetOrSetInt32("sensitivity", 5);
        Threshold = section.GetOrSetInt32("threshold", 5);
        for (var a = 0; a < Names.Length; a++)
        {
            for (var i = 0; i < 2; i++) Assignments[a, i] = Assignments[a, i] with { Kind = JoystickAssignmentKind.None };
            var values = section.GetOption(Names[a])?.Values;
            if (values == null) continue;
            for (var i = 0; i < Math.Min(2, values.Count); i++) Assignments[a, i] = JoystickAssignment.Parse(values[i]);
        }
        return true;
    }

    public bool SaveAssignments(Configuration config)
    {
        var section = config.FindOrAddSection("joystick", Device?.Name ?? throw new InvalidOperationException("Joystick is not open."));
        section.SetBoolean("analog", Analog, ConfigBoolStyle.NoYes);
        section.SetInt32("sensitivity", Sensitivity); section.SetInt32("threshold", Threshold);
        for (var a = 0; a < Names.Length; a++)
        {
            var option = section.SetOption(Names[a], null);
            for (var i = 0; i < 2; i++)
                if (Assignments[a, i].Kind != JoystickAssignmentKind.None) option.AddValue(Assignments[a, i].ToCode());
        }
        return true;
    }
}

public sealed class JoystickInput(IReadOnlyList<JoystickState> joysticks, Func<uint> ticks, Action update, Action<InputEvent> post)
{
    public int RepeatDelay { get; set; } = 300;
    public bool Down { get; private set; }

    public void Poll(JoystickState joystick)
    {
        if (joystick.Device == null) return;
        update();
        joystick.InputPressed = false;
        var repeat = joystick.RepeatAt < ticks();
        for (var d = 0; d < 4; d++)
        {
            var old = joystick.Direction[d];
            joystick.AnalogDirection[d] = joystick.CheckAssigned(d);
            joystick.Direction[d] = joystick.AnalogDirection[d] > 32767 / 2;
            Down |= joystick.Direction[d];
            joystick.DirectionPressed[d] = joystick.Direction[d] && (!old || repeat);
            joystick.InputPressed |= joystick.DirectionPressed[d];
        }
        joystick.X = -joystick.AnalogDirection[3] + joystick.AnalogDirection[1];
        joystick.Y = -joystick.AnalogDirection[0] + joystick.AnalogDirection[2];
        for (var d = 0; d < 6; d++)
        {
            var old = joystick.Action[d];
            joystick.Action[d] = joystick.CheckAssigned(d + 4) > 32767 / 2;
            Down |= joystick.Action[d];
            joystick.ActionPressed[d] = joystick.Action[d] && (!old || repeat);
            joystick.InputPressed |= joystick.ActionPressed[d];
        }
        joystick.Confirm = joystick.Action[0] || joystick.Action[4];
        joystick.Cancel = joystick.Action[1] || joystick.Action[5];
        if (joystick.InputPressed) joystick.RepeatAt = unchecked(ticks() + (uint)RepeatDelay);
    }

    public void PollAll()
    {
        Down = false;
        foreach (var joystick in joysticks) Poll(joystick);
    }

    public void PushKey(int scancode)
    {
        post(new(InputEventKind.KeyDown, scancode)); post(new(InputEventKind.KeyUp, scancode));
    }

    public void PushAsKeyboard()
    {
        PollAll();
        foreach (var joystick in joysticks)
        {
            if (!joystick.InputPressed) continue;
            if (joystick.Confirm) PushKey(40);
            if (joystick.Cancel) PushKey(41);
            int[] directions = [82, 79, 81, 80];
            for (var d = 0; d < 4; d++) if (joystick.DirectionPressed[d]) PushKey(directions[d]);
        }
    }

    public bool DetectAssignment(JoystickState joystick, ref JoystickAssignment assignment,
        DelayTimer timer, InputState input, Action present)
    {
        var device = joystick.Device ?? throw new InvalidOperationException("Joystick is not open.");
        var axes = Enumerable.Range(0, device.AxisCount).Select(device.Axis).ToArray();
        var buttons = Enumerable.Range(0, device.ButtonCount).Select(device.Button).ToArray();
        var hats = Enumerable.Range(0, device.HatCount).Select(device.Hat).ToArray();
        var detected = false;
        do
        {
            timer.SetDelay(1);
            update();
            for (var i = 0; i < axes.Length; i++)
            {
                var value = device.Axis(i);
                if (Math.Abs(value - axes[i]) <= 32767 * 2 / 3) continue;
                assignment = assignment with { Kind = JoystickAssignmentKind.Axis, Number = i, Negative = value < axes[i] };
                detected = true;
                break;
            }
            for (var i = 0; i < buttons.Length; i++)
            {
                var value = device.Button(i);
                if ((buttons[i] ^ value) == 0) continue;
                if (value == 0) buttons[i] = value;
                else
                {
                    assignment = assignment with { Kind = JoystickAssignmentKind.Button, Number = i };
                    detected = true;
                    break;
                }
            }
            for (var i = 0; i < hats.Length; i++)
            {
                var value = device.Hat(i);
                var changed = hats[i] ^ value;
                if (changed == 0) continue;
                if ((value & changed) == 0) hats[i] = value;
                else
                {
                    assignment = assignment with
                    {
                        Kind = JoystickAssignmentKind.Hat,
                        Number = i,
                        XAxis = (changed & (8 | 2)) != 0,
                        Negative = (changed & (8 | 1)) != 0
                    };
                    detected = true;
                }
            }
            input.ServiceEvents(true);
            present();
            timer.Wait();
        } while (!detected && !input.NewKey && !input.NewMouse);
        return detected;
    }
}
