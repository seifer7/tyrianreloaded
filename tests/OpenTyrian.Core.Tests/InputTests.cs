using System.Diagnostics;
using System.Text;

namespace OpenTyrian.Core.Tests;

internal static class InputTests
{
    internal static void Run(string? native = null)
    {
        var managed = Execute();
        if (native != null) CompareNative(native, managed);
        var platform = new Platform(new StringBuilder());
        var layout = new VideoLayout();
        var input = new InputState(platform, layout);
        input.Initialize();
        platform.Events.Enqueue(new(InputEventKind.KeyDown, 8));
        input.ServiceEvents(true);
        Check(input.KeyDown && input.KeysActive[8] == 1 && input.NewKey, "Key state was not published.");
        platform.Events.Enqueue(new(InputEventKind.KeyDown, 9));
        platform.Events.Enqueue(new(InputEventKind.KeyUp, 8));
        input.ServiceEvents(false); input.ServiceEvents(false);
        Check(!input.KeyDown && input.KeysActive[9] == 1, "Native any-key release quirk changed.");
        platform.Events.Enqueue(new(InputEventKind.TextInput, Text: Encoding.UTF8.GetBytes(new string('x', 30) + "\u00e9")));
        input.ServiceEvents(true);
        Check(input.LastText.Span[30] == 0xc3 && input.LastText.Span[31] == 0, "SDL text bytes were not truncated at byte 31.");
        platform.Events.Enqueue(new(InputEventKind.KeyDown, 512));
        Throws<InvalidDataException>(() => input.ServiceEvents(false));
        platform.Events.Enqueue(new(InputEventKind.Quit)); platform.Events.Enqueue(new(InputEventKind.KeyDown, 20));
        input.ServiceEvents(false);
        Check(platform.QuitRequested && platform.Events.Count == 1, "Quit did not leave subsequent events for shutdown.");
        platform.Events.Clear();
        var timer = new DelayTimer(input);
        timer.SetDelay(1); timer.ServiceWait();
        Check(platform.Ticks > 0, "Input platform was not integrated with PIT delay timing.");
        platform.IsNetworkGame = true; platform.JoystickDown = true;
        input.WaitNoInput(false, false, true);
        Check(!platform.JoystickDown, "Joystick release wait failed.");
        platform.Events.Clear();
        platform.DelayAction = () => platform.JoystickDown = true;
        input.WaitInput(false, false, true);
        Check(platform.JoystickDown, "Joystick press wait failed.");
        platform.Events.Clear();
        layout.Output = new(0, 0, 128, 80);
        Check(layout.WindowDistanceToScreen(0, 0) == (1, 1), "Native scaled-zero rounding changed.");
        layout.Mode = ScalingMode.Fit4By3;
        Throws<InvalidOperationException>(() => layout.Calculate(1, 2, 320, 200));
        Throws<ArgumentOutOfRangeException>(() => layout.Output = new(0, 0, 0, 200));
        layout.Mode = (ScalingMode)99;
        Throws<InvalidOperationException>(() => layout.Calculate(640, 400, 320, 200));
        platform.Failure = true;
        Throws<IOException>(() => input.SetRelative(true));
        Check(!input.RelativeEnabled, "Failed relative-mode change published an enabled state.");
    }

    private static string Execute()
    {
        var output = new StringBuilder();
        for (var i = 0; i < 128; i++)
        {
            var layout = new VideoLayout();
            for (var mode = 0; mode < 4; mode++)
            {
                layout.Mode = (ScalingMode)mode;
                var rect = layout.Calculate(210 + i * 37, 130 + i * 17, 320 * (i % 4 + 1), 200 * (i % 4 + 1));
                layout.Output = rect;
                var point = layout.ScreenToWindow(i * 13 - 500, i * 7 - 300);
                var window = layout.WindowToScreen(i * 17 - 600, i * 11 - 500);
                var distance = layout.WindowDistanceToScreen(i * 5 - 300, i * 3 - 200);
                output.AppendLine($"geometry:{i},{mode},{rect.X},{rect.Y},{rect.Width},{rect.Height},{point.X},{point.Y},{window.X},{window.Y},{distance.X},{distance.Y}");
            }
            layout.Mode = (ScalingMode)(i % 4);
            layout.Output = layout.Calculate(210 + i * 37, 130 + i * 17, 320 * (i % 4 + 1), 200 * (i % 4 + 1));
            var platform = new Platform(output) { IsNetworkGame = i % 2 != 0 };
            var input = new InputState(platform, layout);
            void State(int step) => output.AppendLine($"state:{i},{step},{Bit(input.NewKey)},{Bit(input.NewMouse)},{Bit(input.NewText)},{Bit(input.KeyDown)},{Bit(input.MouseDown)},{input.LastScancode},{input.LastModifiers},{input.LastMouseButton},{input.LastMouseX},{input.LastMouseY},{input.MouseX},{input.MouseY},{Bit(input.WindowHasFocus)},{Bit(input.MouseInactive)},{Hash(input.KeysActive)},{Hash(input.MousePressed.Select(v => (byte)Bit(v)).ToArray())},{Hash(input.LastText.Span)},{platform.Events.Count}");
            input.Initialize(); input.SetRelative(i % 3 != 0); State(0);
            var events = platform.Events;
            events.Enqueue(new(InputEventKind.FocusGained));
            events.Enqueue(new(InputEventKind.MouseMotion, X: i * 37 - 20, Y: i * 17 - 10, RelativeX: i - 63, RelativeY: 31 - i));
            events.Enqueue(new(InputEventKind.MouseButtonDown, Button: (byte)(i % 5 + 1), X: i * 11 - 5, Y: i * 7 - 3));
            events.Enqueue(new(InputEventKind.KeyDown, 5 + i, 0x42));
            events.Enqueue(new(InputEventKind.TextInput, Text: Encoding.ASCII.GetBytes("0123456789abcdefghijklmnopqrstu")));
            events.Enqueue(new(InputEventKind.KeyDown, 40, 0x300));
            events.Enqueue(new(InputEventKind.KeyUp, 5 + i));
            events.Enqueue(new(InputEventKind.FocusLost));
            events.Enqueue(new(InputEventKind.MouseMotion, X: -300, Y: -300, RelativeX: 500, RelativeY: 500));
            events.Enqueue(new(InputEventKind.FocusGained)); events.Enqueue(new(InputEventKind.WindowResized));
            events.Enqueue(new(InputEventKind.TextInput, Text: "X"u8.ToArray()));
            events.Enqueue(new(InputEventKind.TextEditing));
            events.Enqueue(new(InputEventKind.MouseButtonUp, Button: (byte)(i % 5 + 1), X: 200, Y: 100));
            events.Enqueue(new(InputEventKind.MouseMotion, X: 100, Y: 50, RelativeX: i * 2 - 128, RelativeY: 64 - i));
            events.Enqueue(new(InputEventKind.Unknown));
            input.ServiceEvents(false); State(1);
            input.ServiceEvents(true); State(2);
            input.ServiceEvents(false); State(3);
            var mouse = input.MousePosition(); output.AppendLine($"mouse:{mouse.X},{mouse.Y},{mouse.Button}"); State(4);
            for (var j = 0; j < 2; j++)
            {
                var distance = input.RelativePosition(); output.AppendLine($"distance:{distance.X},{distance.Y}"); State(5 + j);
            }
            input.SetRelative(false); State(7);
            events.Enqueue(new(InputEventKind.KeyDown, 34)); events.Enqueue(new(InputEventKind.KeyDown, 35));
            events.Enqueue(new(InputEventKind.KeyUp, 34));
            events.Enqueue(new(InputEventKind.MouseMotion, X: -1, Y: -1, RelativeX: 1, RelativeY: 1));
            events.Enqueue(new(InputEventKind.KeyUp, 35));
            input.WaitInput(true, false, false); State(8);
            input.WaitNoInput(true, false, false); State(9);
            input.WaitNoInput(true, false, false); State(10);
            input.FlushEvents(); input.WaitInput(true, false, false); State(11);
            input.WaitNoInput(true, false, false); State(12);
            events.Enqueue(new(InputEventKind.KeyDown, 99)); input.FlushEvents(); input.ClearKeyboard(); State(13);
        }
        var names = new VideoLayout();
        output.AppendLine($"names:{Bit(names.SetModeByName("Center"))},{Bit(names.SetModeByName("Integer"))},{Bit(names.SetModeByName("Fit 8:5"))},{Bit(names.SetModeByName("Fit 4:3"))},{Bit(names.SetModeByName("integer"))}");
        Check(names.Mode == ScalingMode.Fit4By3, "Unknown scaling name changed selected mode.");
        return output.ToString().Replace("\r\n", "\n", StringComparison.Ordinal);
    }

    private sealed class Platform(StringBuilder output) : IInputPlatform
    {
        internal Queue<InputEvent> Events { get; } = new();
        internal bool QuitRequested, Failure;
        internal Action? DelayAction;
        public bool JoystickDown { get; set; }
        public bool IsNetworkGame { get; set; }
        public uint Ticks { get; private set; }
        public bool PollEvent(out InputEvent input) => Events.TryDequeue(out input);
        public void Delay(uint milliseconds)
        {
            output.AppendLine($"delay:{milliseconds}"); Ticks += milliseconds; DelayAction?.Invoke();
        }
        public void SetCursorVisible(bool visible) => output.AppendLine($"cursor:{Bit(visible)}");
        public void SetRelativeMouseMode(bool enabled)
        {
            if (Failure) throw new IOException("Synthetic platform failure.");
            output.AppendLine($"relative:{Bit(enabled)}");
        }
        public void EnableSystemMouseScaling() => output.AppendLine("systemscale");
        public void ToggleFullscreen() => output.AppendLine("fullscreen");
        public void WindowResized() => output.AppendLine("resize");
        public void Quit() { QuitRequested = true; output.AppendLine("quit"); }
        public void PushJoysticksAsKeyboard()
        {
            output.AppendLine("pushjoy"); Events.Enqueue(new(InputEventKind.KeyDown, 9));
        }
        public void PollJoysticks()
        {
            output.AppendLine("polljoy"); JoystickDown = false; Events.Enqueue(new(InputEventKind.KeyUp, 9));
        }
        public void CheckNetwork() => output.AppendLine("network");
    }

    private static int Bit(bool value) => value ? 1 : 0;
    private static uint Hash(ReadOnlySpan<byte> bytes)
    {
        var hash = 2166136261u;
        foreach (var value in bytes) hash = unchecked((hash ^ value) * 16777619);
        return hash;
    }

    private static void CompareNative(string executable, string expected)
    {
        using var process = Process.Start(new ProcessStartInfo(Path.GetFullPath(executable))
        { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true })
            ?? throw new InvalidOperationException("Could not start native input reference.");
        var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(30000)) { process.Kill(entireProcessTree: true); throw new TimeoutException("Native input reference timed out."); }
        var actual = stdout.GetAwaiter().GetResult().Replace("\r\n", "\n", StringComparison.Ordinal);
        Check(process.ExitCode == 0, $"Native input reference failed: {stderr.GetAwaiter().GetResult()}");
        if (actual == expected) return;
        var a = actual.Split('\n'); var b = expected.Split('\n');
        var line = Enumerable.Range(0, Math.Min(a.Length, b.Length)).FirstOrDefault(i => a[i] != b[i], -1);
        throw new InvalidOperationException(line < 0 ? "Input trace length differs." : $"Input trace differs at line {line + 1}: native={a[line]}, managed={b[line]}.");
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
