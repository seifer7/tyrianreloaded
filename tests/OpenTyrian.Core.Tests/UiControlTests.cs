using System.Diagnostics;
using System.Text;
using OpenTyrian.Core;

namespace OpenTyrian.Core.Tests;

internal static class UiControlTests
{
    public static void Run(string? executable = null)
    {
        var output = new StringBuilder();
        for (var pitch = 320; pitch <= 328; pitch += 8)
            for (var mode = 0; mode < 128; mode++)
            {
                using var warnings = new StringWriter();
                var surface = new IndexedSurface(320, 200, pitch, warnings);
                for (var i = 0; i < pitch * 200; i++) surface.Pixels.Span[i] = unchecked((byte)(i * 17 + i / 7));
                output.AppendLine($"bar:{pitch},{mode}");
                surface.ShadowedGauge((ushort)((mode & 1) != 0 ? 310 : 20), (ushort)((mode & 2) != 0 ? 195 : 50),
                    (ushort)(mode % 17 + 1), unchecked((ushort)(mode * 515)), (ushort)(mode % 32), (ushort)(mode % 6), (ushort)(mode % 15));
                output.Append(warnings);
                uint hash = 2166136261;
                foreach (var pixel in surface.Pixels.Span) hash = unchecked((hash ^ pixel) * 16777619);
                output.AppendLine($"pixels:{hash}");
            }
        for (var flags = 0; flags < 16; flags++)
        {
            var platform = new Platform(output) { Flags = flags };
            var input = new InputState(platform, new VideoLayout());
            platform.Events.Enqueue(new(InputEventKind.MouseButtonDown, Button: 1));
            input.ServiceEvents(true);
            if ((flags & 2) == 0)
            {
                platform.Events.Enqueue(new(InputEventKind.MouseButtonUp, Button: 1));
                input.ServiceEvents(true);
            }
            input.NewKey = input.NewMouse = true;
            input.InputDetected = (flags & 1) != 0;
            if ((flags & 8) != 0) platform.Events.Enqueue(new(InputEventKind.MouseButtonDown, Button: 2));
            // A mouse press flag may be stale while the button is no longer held.
            if ((flags & 8) != 0 && (flags & 2) == 0)
                platform.Events.Enqueue(new(InputEventKind.MouseButtonUp, Button: 2));
            if ((flags & 1) != 0) platform.Events.Enqueue(new(InputEventKind.KeyDown, 40));
            var result = input.AnyButton();
            input.ClearKeyboard();
            output.AppendLine($"button:{flags},{Bit(result)},{Bit(input.NewKey)},{Bit(input.NewMouse)},{Bit(input.MouseDown)},{Bit(input.JoystickDown)},{Bit(input.InputDetected)}");
        }
        if (executable != null)
        {
            using var process = Process.Start(new ProcessStartInfo(Path.GetFullPath(executable))
            { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true })
                ?? throw new InvalidOperationException("Cannot start native UI control oracle.");
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(30000))
            {
                process.Kill(entireProcessTree: true);
                throw new TimeoutException("Native UI control oracle timed out.");
            }
            if (process.ExitCode != 0) throw new InvalidOperationException($"Native UI control oracle failed: {stderr.GetAwaiter().GetResult()}");
            var native = stdout.GetAwaiter().GetResult().Replace("\r\n", "\n", StringComparison.Ordinal);
            var managed = output.ToString().Replace("\r\n", "\n", StringComparison.Ordinal);
            if (native != managed)
            {
                var expected = native.Split('\n'); var actual = managed.Split('\n');
                var i = Enumerable.Range(0, Math.Max(expected.Length, actual.Length))
                    .First(i => expected.ElementAtOrDefault(i) != actual.ElementAtOrDefault(i));
                throw new InvalidOperationException($"UI control trace mismatch at {i + 1}: native '{expected.ElementAtOrDefault(i)}', managed '{actual.ElementAtOrDefault(i)}'.");
            }
        }
        using var ignored = new StringWriter();
        var check = new IndexedSurface(8, 8, 8, ignored);
        try { check.ShadowedGauge(0, 0, 0, 0, 1, 1, 1); throw new InvalidOperationException("Zero-resolution gauge was accepted."); }
        catch (ArgumentOutOfRangeException) { }
        check.ShadowedGauge(1, 1, 16, 160, 15, 2, 2);
        if (check.Pixels.Span[9] != 160) throw new InvalidOperationException("Partial gauge division order changed.");
    }

    private sealed class Platform(StringBuilder output) : IInputPlatform
    {
        internal readonly Queue<InputEvent> Events = new();
        internal int Flags;
        public bool PollEvent(out InputEvent input) => Events.TryDequeue(out input);
        public uint Ticks => 0;
        public bool JoystickDown { get; private set; }
        public bool IsNetworkGame => false;
        public void PollJoysticks()
        {
            output.AppendLine("poll");
            output.AppendLine("events:1");
            JoystickDown = (Flags & 4) != 0;
        }
        public void SetCursorVisible(bool visible) { }
        public void Delay(uint milliseconds) => throw new InvalidOperationException();
        public void SetRelativeMouseMode(bool enabled) => throw new InvalidOperationException();
        public void EnableSystemMouseScaling() => throw new InvalidOperationException();
        public void ToggleFullscreen() => throw new InvalidOperationException();
        public void WindowResized() => throw new InvalidOperationException();
        public void Quit() => throw new InvalidOperationException();
        public void PushJoysticksAsKeyboard() => throw new InvalidOperationException();
        public void CheckNetwork() => throw new InvalidOperationException();
    }
    private static int Bit(bool value) => value ? 1 : 0;
}
