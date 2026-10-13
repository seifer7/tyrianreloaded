using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace OpenTyrian.Core.Tests;

internal static class JoystickTests
{
    internal static void Run(string? native = null)
    {
        var output = Execute();
        if (native != null) CompareNative(native, output);
        var state = new JoystickState(new Device());
        state.ResetAssignments();
        state.Threshold = 0; state.Sensitivity = 15;
        Throws<InvalidDataException>(() => state.AxisReduce(100));
        Throws<InvalidDataException>(() => state.AxisThreshold(int.MinValue));
        Throws<InvalidDataException>(() => JoystickAssignment.Parse("AX 999999999999999999+"));
        Throws<ArgumentException>(() => JoystickAssignment.Describe([]));
        Throws<InvalidOperationException>(() => new JoystickState(null).ResetAssignments());
        state.Threshold = -5;
        Check(state.AxisThreshold(0) == 5000 && state.AxisThreshold(-100) == -5100, "Defined native negative thresholds were changed.");
        Check(JoystickAssignment.Parse("AX 1\0-").Kind == JoystickAssignmentKind.None, "Assignment parsing crossed a C-string terminator.");
        state.Assignments[0, 0] = new(JoystickAssignmentKind.Axis, -1);
        Check(state.CheckAssigned(0) == 0, "Invalid native device indices should read neutral.");
        state.Assignments[0, 0] = new(JoystickAssignmentKind.Axis, 0, Negative: true);
        ((Device)state.Device!).Axes[0] = short.MinValue;
        Check(state.CheckAssigned(0) == 32768, "Native negated signed-16 minimum was narrowed.");
        var events = new Queue<InputEvent>();
        var joystick = new JoystickInput([state], () => 0, () => { }, events.Enqueue);
        joystick.PushKey(40);
        Check(events.Dequeue().Kind == InputEventKind.KeyDown && events.Dequeue().Kind == InputEventKind.KeyUp,
            "Joystick keyboard synthesis order changed.");

        AssignmentRouting();
    }

    private static void AssignmentRouting()
    {
        var state = new JoystickState(new Device());
        var axis = new JoystickAssignment(JoystickAssignmentKind.Axis, 1, Negative: true);
        var button = new JoystickAssignment(JoystickAssignmentKind.Button, 2);
        var hat = new JoystickAssignment(JoystickAssignmentKind.Hat, 0, XAxis: true);

        state.Assignments[0, 0] = axis;
        state.Assignments[0, 1] = button;
        var host = new AssignmentHost(axis);
        JoystickConfigurationSelection.Assign(state, 6, host);
        Check(state.Assignments[0, 0] == axis with { Kind = JoystickAssignmentKind.None } &&
            state.Assignments[0, 1] == button && host.Polls == 1,
            "Matching joystick assignment was not removed while preserving its stored fields.");
        Check(host.Rectangle == (235, 69, 310, 78, 248), "Joystick assignment highlight used the wrong native row.");

        state.Assignments[0, 0] = axis;
        state.Assignments[0, 1] = new(JoystickAssignmentKind.None, 44, XAxis: true);
        host = new AssignmentHost(hat);
        JoystickConfigurationSelection.Assign(state, 6, host);
        Check(state.Assignments[0, 0] == axis && state.Assignments[0, 1] == hat,
            "Joystick assignment did not fill the first unassigned slot.");

        state.Assignments[0, 0] = axis;
        state.Assignments[0, 1] = button;
        host = new AssignmentHost(hat);
        JoystickConfigurationSelection.Assign(state, 6, host);
        Check(state.Assignments[0, 0] == button && state.Assignments[0, 1] == hat,
            "A third joystick assignment did not shift the older assignment out.");

        host = new AssignmentHost(null);
        var before = (state.Assignments[0, 0], state.Assignments[0, 1]);
        JoystickConfigurationSelection.Assign(state, 6, host);
        Check((state.Assignments[0, 0], state.Assignments[0, 1]) == before && host.Polls == 0,
            "Unsuccessful joystick detection modified the assignments or polled devices.");
        Throws<ArgumentOutOfRangeException>(() => JoystickConfigurationSelection.Assign(state, 5, host));

        JoystickState[] joysticks = [new(new Device()), new(new Device())];
        var selectedJoystick = 0;
        JoystickConfigurationSelection.AdjustSetting(2, -1, joysticks, ref selectedJoystick);
        Check(selectedJoystick == 1, "Joystick selection did not wrap backward.");
        JoystickConfigurationSelection.AdjustSetting(4, 1, joysticks, ref selectedJoystick);
        Check(joysticks[1].Sensitivity == 5, "Sensitivity changed while analog controls were disabled.");
        JoystickConfigurationSelection.AdjustSetting(3, 1, joysticks, ref selectedJoystick);
        JoystickConfigurationSelection.AdjustSetting(4, 1, joysticks, ref selectedJoystick);
        JoystickConfigurationSelection.AdjustSetting(5, -1, joysticks, ref selectedJoystick);
        Check(joysticks[1].Analog && joysticks[1].Sensitivity == 6 && joysticks[1].Threshold == 4,
            "Joystick analog settings did not toggle or wrap with their native direction.");
        joysticks[1].Assignments[0, 0] = hat;
        JoystickConfigurationSelection.Handle(16, joysticks, ref selectedJoystick, false,
            _ => throw new InvalidOperationException("Reset unexpectedly returned to options."), new AssignmentHost(null));
        Check(joysticks[1].Assignments[0, 0].Kind == JoystickAssignmentKind.Axis &&
            !joysticks[1].Analog && joysticks[1].Sensitivity == 5 && joysticks[1].Threshold == 5,
            "Joystick reset did not restore capability-dependent assignments and analog defaults.");

        byte destinationMenu = 0;
        JoystickConfigurationSelection.Handle(17, [], ref selectedJoystick, true,
            menu => destinationMenu = menu, new AssignmentHost(null));
        Check(destinationMenu == 11, "No-joystick exit did not return to network options.");
        destinationMenu = 0;
        JoystickConfigurationSelection.Handle(17, joysticks, ref selectedJoystick, false,
            menu => destinationMenu = menu, new AssignmentHost(null));
        Check(destinationMenu == 2, "Joystick configuration did not return to local options.");
        JoystickConfigurationSelection.Handle(3, [], ref selectedJoystick, false,
            _ => throw new InvalidOperationException("No joystick setting should exit."), new AssignmentHost(null));
    }

    private sealed class AssignmentHost(JoystickAssignment? detection) : IJoystickAssignmentHost
    {
        internal int Polls;
        internal (int Left, int Top, int Right, int Bottom, int Color) Rectangle;
        public JoystickAssignment? DetectAssignment() => detection;
        public void DrawSelectionRectangle(int left, int top, int right, int bottom, int color)
            => Rectangle = (left, top, right, bottom, color);
        public void PollJoysticks() => Polls++;
    }

    private static string Execute()
    {
        var output = new StringBuilder();
        string[] codes = ["", "AX 1-", "AX 2+", "BTN 6", "H 1X-", "H 2y+", " AX -2?", "AX 0+", "H 0X+",
            "AX 1", "H 1X", "BTN -1", "AX 12345+", "BTN 12345", "H 123X-", "ax 1+", " AX2-", "H1Yx", "junk", "BTN +7tail", "H -3q-", "AX +3 -"];
        var parsed = codes.Select(JoystickAssignment.Parse).ToArray();
        for (var i = 0; i < parsed.Length; i++)
        {
            var a = parsed[i]; output.AppendLine($"code:{i},{(int)a.Kind},{a.Number},{Bit(a.XAxis)},{Bit(a.Negative)},{a.ToCode()}");
        }
        for (var i = 0; i < parsed.Length; i++)
            for (var j = 0; j < parsed.Length; j++) output.AppendLine($"cmp:{i},{j},{Bit(parsed[i].Matches(parsed[j]))}");
        for (var mode = 0; mode < 32; mode++)
        {
            Device[] devices = [new(), new() { AxisCount = mode % 4, ButtonCount = mode % 8, HatCount = mode % 2, Name = "Other pad" }];
            JoystickState[] states = [new(devices[0]), new(devices[1]), new(null)];
            var updates = 0;
            uint ticks = 0;
            var input = new JoystickInput(states, () => ticks, () => updates++,
                e => output.AppendLine($"key:{(e.Kind == InputEventKind.KeyDown ? 1 : 2)},{e.Scancode}"))
            { RepeatDelay = mode * 17 };
            states[0].ResetAssignments(); states[1].ResetAssignments();
            for (var j = 0; j < 2; j++)
                for (var a = 0; a < 10; a++)
                    output.AppendLine($"default:{mode},{j},{a},{JoystickAssignment.Describe([states[j].Assignments[a, 0], states[j].Assignments[a, 1]])}");
            states[0].Threshold = mode % 34; states[0].Sensitivity = mode % 15;
            for (var frame = 0; frame < 48; frame++)
            {
                ticks = mode % 7 == 0 ? unchecked(0xfffffff0u + (uint)frame * 17) : (uint)frame * 137;
                for (var j = 0; j < 2; j++)
                {
                    for (var i = 0; i < 4; i++) devices[j].Axes[i] = unchecked((short)(frame * 7919 + mode * 1009 + i * 2345 + j * 987));
                    for (var i = 0; i < 8; i++) devices[j].Buttons[i] = (byte)((frame + mode + i + j) % 3);
                    for (var i = 0; i < 2; i++) devices[j].Hats[i] = (byte)((frame * 7 + mode + i + j) & 15);
                }
                if (frame % 3 == 0) input.PushAsKeyboard(); else input.PollAll();
                for (var j = 0; j < 3; j++)
                {
                    var s = states[j];
                    output.AppendLine($"state:{mode},{frame},{j},{Bit(input.Down)},{s.X},{s.Y},{Bit(s.Confirm)},{Bit(s.Cancel)},{Bits(s.Direction)},{Bits(s.DirectionPressed)},{Bits(s.Action)},{Bits(s.ActionPressed)},{s.RepeatAt},{Bit(s.InputPressed)},{s.AnalogDirection[0]},{s.AnalogDirection[1]},{s.AnalogDirection[2]},{s.AnalogDirection[3]}");
                }
                var angle = .37f; var has = states[0].AnalogAngle(ref angle);
                output.AppendLine($"angle:{mode},{frame},{Bit(has)},{angle.ToString("G9", CultureInfo.InvariantCulture)}");
                output.AppendLine($"reduce:{mode},{frame},{states[0].AxisThreshold(states[0].X)},{states[0].AxisReduce(states[0].X)},{updates}");
            }
            var config = new Configuration();
            output.AppendLine($"config:{mode},{Bit(states[0].LoadAssignments(config))}");
            var section = config.AddSection("joystick", "Synthetic pad");
            section.SetBoolean("analog", mode % 2 != 0, ConfigBoolStyle.NoYes);
            if (mode % 2 != 0) section.SetInt32("sensitivity", mode % 15);
            section.SetInt32("threshold", mode % 34);
            section.SetOption("up", null).AddValue("AX 2-").AddValue("H 1Y-").AddValue("BTN 7");
            section.SetOption("fire", "BTN 3");
            Check(states[0].LoadAssignments(config) && states[0].SaveAssignments(config), "Joystick config load/save failed.");
            using var text = new StringWriter(); config.Write(text); output.Append(text);
        }
        for (var mode = 0; mode < 8; mode++)
        {
            var device = new Device { AxisCount = 2, ButtonCount = 2, HatCount = 2 };
            if (mode == 2) device.Buttons[0] = 1;
            if (mode == 3) device.Hats[0] = 1;
            var state = new JoystickState(device);
            var phase = 0; var presents = 0;
            var platform = new DetectPlatform();
            var input = new InputState(platform, new VideoLayout());
            void Update()
            {
                phase++;
                switch (mode)
                {
                    case 0: device.Axes[0] = 32767; break;
                    case 1: device.Axes[0] = -32768; break;
                    case 2: device.Buttons[0] = phase == 1 ? (byte)0 : (byte)1; break;
                    case 3:
                        device.Hats[0] = phase == 1 ? (byte)0 : (byte)12;
                        device.Hats[1] = phase == 1 ? (byte)0 : (byte)2;
                        break;
                    case 4: device.Axes[0] = 32767; device.Buttons[0] = 1; device.Hats[0] = 8; break;
                    case 5:
                        if (phase >= 3) platform.Events.Enqueue(new(InputEventKind.KeyDown, 10));
                        break;
                    case 6:
                        if (phase >= 2) platform.Events.Enqueue(new(InputEventKind.MouseButtonDown, Button: 1));
                        break;
                    case 7: device.Axes[0] = phase == 1 ? (short)21844 : (short)21845; break;
                }
            }
            var joystick = new JoystickInput([state], () => 0, Update, _ => throw new InvalidOperationException());
            var assignment = new JoystickAssignment(JoystickAssignmentKind.None, 44, true);
            var detected = joystick.DetectAssignment(state, ref assignment, new DelayTimer(input), input, () => presents++);
            output.AppendLine($"detect:{mode},{Bit(detected)},{(int)assignment.Kind},{assignment.Number},{Bit(assignment.XAxis)},{Bit(assignment.Negative)},{phase},{phase},{presents},{platform.Waits}");
        }
        return output.ToString().Replace("\r\n", "\n", StringComparison.Ordinal);
    }

    private sealed class DetectPlatform : IInputPlatform
    {
        internal Queue<InputEvent> Events { get; } = new();
        internal int Waits;
        public uint Ticks => 0;
        public bool JoystickDown => false;
        public bool IsNetworkGame => false;
        public bool PollEvent(out InputEvent input) => Events.TryDequeue(out input);
        public void Delay(uint milliseconds) => Waits++;
        public void SetCursorVisible(bool visible) => throw new InvalidOperationException();
        public void SetRelativeMouseMode(bool enabled) => throw new InvalidOperationException();
        public void EnableSystemMouseScaling() => throw new InvalidOperationException();
        public void ToggleFullscreen() => throw new InvalidOperationException();
        public void WindowResized() => throw new InvalidOperationException();
        public void Quit() => throw new InvalidOperationException();
        public void PushJoysticksAsKeyboard() => throw new InvalidOperationException();
        public void PollJoysticks() => throw new InvalidOperationException();
        public void CheckNetwork() => throw new InvalidOperationException();
    }
    private sealed class Device : IJoystickDevice
    {
        public string Name { get; set; } = "Synthetic pad";
        public int AxisCount { get; set; } = 2;
        public int ButtonCount { get; set; } = 6;
        public int HatCount { get; set; } = 1;
        internal short[] Axes = new short[4];
        internal byte[] Buttons = new byte[8], Hats = new byte[2];
        public short Axis(int index) => (uint)index < AxisCount ? Axes[index] : (short)0;
        public byte Button(int index) => (uint)index < ButtonCount ? Buttons[index] : (byte)0;
        public byte Hat(int index) => (uint)index < HatCount ? Hats[index] : (byte)0;
    }

    private static int Bit(bool value) => value ? 1 : 0;
    private static uint Bits(bool[] values)
    {
        uint bits = 0; for (var i = 0; i < values.Length; i++) if (values[i]) bits |= 1u << i;
        return bits;
    }
    private static void CompareNative(string executable, string expected)
    {
        using var process = Process.Start(new ProcessStartInfo(Path.GetFullPath(executable))
        { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true })
            ?? throw new InvalidOperationException("Could not start native joystick oracle.");
        var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(30000)) { process.Kill(entireProcessTree: true); throw new TimeoutException("Native joystick oracle timed out."); }
        Check(process.ExitCode == 0, $"Native joystick oracle failed: {stderr.GetAwaiter().GetResult()}");
        var a = stdout.GetAwaiter().GetResult().Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var b = expected.Split('\n');
        Check(a.Length == b.Length, "Joystick trace length differs.");
        for (var i = 0; i < a.Length; i++)
        {
            if (a[i] == b[i]) continue;
            if (a[i].StartsWith("angle:", StringComparison.Ordinal) && b[i].StartsWith("angle:", StringComparison.Ordinal))
            {
                var left = a[i].LastIndexOf(','); var right = b[i].LastIndexOf(',');
                if (a[i][..left] == b[i][..right] &&
                    Math.Abs(float.Parse(a[i][(left + 1)..], CultureInfo.InvariantCulture) -
                        float.Parse(b[i][(right + 1)..], CultureInfo.InvariantCulture)) <= 0.000001f) continue;
            }
            throw new InvalidOperationException($"Joystick trace differs at {i + 1}: native={a[i]}, managed={b[i]}.");
        }
    }
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
    private static void Throws<T>(Action action) where T : Exception
    {
        try { action(); } catch (T) { return; }
        throw new InvalidOperationException($"Expected {typeof(T).Name}.");
    }
}
