using System.Buffers.Binary;
using System.Diagnostics;

namespace OpenTyrian.Core.Tests;

internal static class MouseCursorTests
{
    internal static void Run(string? native = null)
    {
        var sheet = Fixture();
        var coordinates = new[] { -20, -1, 0, 1, 159, 319, 320, 400 };
        var output = new System.Text.StringBuilder();
        for (var pitch = 320; pitch <= 336; pitch += 16)
        {
            var surface = new IndexedSurface(320, 200, pitch, TextWriter.Null);
            for (byte cursor = 0; cursor < 5; cursor++)
                for (var i = 0; i < 64; i++)
                    for (var mode = 0; mode < 4; mode++)
                    {
                        for (var p = 0; p < surface.Pixels.Length; p++) surface.Pixels.Span[p] = unchecked((byte)(p * 13 + i * 7 + cursor));
                        var platform = new Platform();
                        var input = new InputState(platform, new VideoLayout());
                        platform.Events.Enqueue(new(InputEventKind.MouseMotion, X: coordinates[i % 8], Y: coordinates[i / 8]));
                        if (i % 2 != 0) platform.Events.Enqueue(new(InputEventKind.MouseButtonDown, Button: (byte)(i % 5 + 1)));
                        input.ServiceEvents(false); input.MouseInactive = mode == 2;
                        var pointer = new MouseCursor(input, surface, sheet) { Cursor = cursor, HasMouse = mode != 3 };
                        if (mode == 1) pointer.StartFiltered(unchecked((byte)(i * 17)));
                        else pointer.Start();
                        var started = Hash(surface.Pixels.Span);
                        pointer.Replace();
                        output.AppendLine($"cursor:{pitch},{cursor},{i},{mode},{pointer.Button},{started},{Hash(surface.Pixels.Span)}");
                    }
        }
        if (native != null) CompareNative(native, output.ToString());
        var host = new Platform();
        var state = new InputState(host, new VideoLayout());
        var screen = new IndexedSurface(320, 200, 320, TextWriter.Null);
        var mouse = new MouseCursor(state, screen, sheet);
        host.Events.Enqueue(new(InputEventKind.MouseMotion, X: 100, Y: 100, RelativeX: 1));
        mouse.StartFiltered(0x40);
        Check(host.Events.Count == 1 && state.MouseX == 0, "Filtered cursor polled events unlike native.");
        mouse.Start();
        Check(host.Events.Count == 0 && state.MouseX == 100, "Normal cursor did not poll events.");
        mouse.Cursor = 5;
        Throws<ArgumentOutOfRangeException>(mouse.Start);
        mouse.Replace();
        mouse.HasMouse = false; mouse.Start(); mouse.Replace();
        Check(host.Events.Count == 0, "Disabled cursor touched input.");
        var wrongSurface = new MouseCursor(state, new IndexedSurface(32, 20, 32, TextWriter.Null), sheet);
        Throws<ArgumentException>(wrongSurface.Start);
    }

    private static SpriteSheet Fixture()
    {
        var bytes = new byte[600 + 300 * 197];
        var position = 600;
        for (var shape = 0; shape < 300; shape++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(shape * 2, 2), (ushort)position);
            for (var y = 0; y < 14; y++)
            {
                bytes[position++] = 0xc0;
                for (var x = 0; x < 12; x++) bytes[position++] = unchecked((byte)(shape * 7 + x * 3 + y * 5));
                bytes[position++] = 0;
            }
            bytes[position++] = 0x0f;
        }
        var sheet = new SpriteSheet();
        using var stream = new MemoryStream(bytes);
        sheet.Load(stream, position);
        return sheet;
    }

    private sealed class Platform : IInputPlatform
    {
        internal Queue<InputEvent> Events { get; } = new();
        public bool PollEvent(out InputEvent input) => Events.TryDequeue(out input);
        public uint Ticks => 0;
        public bool JoystickDown => false;
        public bool IsNetworkGame => false;
        public void Delay(uint milliseconds) => throw new InvalidOperationException("Unexpected cursor delay.");
        public void SetCursorVisible(bool visible) { }
        public void SetRelativeMouseMode(bool enabled) => throw new InvalidOperationException("Unexpected relative mode change.");
        public void EnableSystemMouseScaling() => throw new InvalidOperationException("Unexpected input initialization.");
        public void ToggleFullscreen() => throw new InvalidOperationException("Unexpected fullscreen change.");
        public void WindowResized() => throw new InvalidOperationException("Unexpected resize.");
        public void Quit() => throw new InvalidOperationException("Unexpected quit.");
        public void PushJoysticksAsKeyboard() => throw new InvalidOperationException("Unexpected joystick keyboard events.");
        public void PollJoysticks() => throw new InvalidOperationException("Unexpected joystick polling.");
        public void CheckNetwork() => throw new InvalidOperationException("Unexpected network polling.");
    }

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
            ?? throw new InvalidOperationException("Could not start native mouse reference.");
        var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(30000)) { process.Kill(entireProcessTree: true); throw new TimeoutException("Native mouse reference timed out."); }
        var actual = stdout.GetAwaiter().GetResult().Replace("\r\n", "\n", StringComparison.Ordinal);
        expected = expected.Replace("\r\n", "\n", StringComparison.Ordinal);
        Check(process.ExitCode == 0, $"Native mouse reference failed: {stderr.GetAwaiter().GetResult()}");
        if (actual == expected) return;
        var a = actual.Split('\n'); var b = expected.Split('\n');
        var line = Enumerable.Range(0, Math.Min(a.Length, b.Length)).FirstOrDefault(i => a[i] != b[i], -1);
        throw new InvalidOperationException(line < 0 ? "Mouse trace length differs." : $"Mouse trace differs at line {line + 1}: native={a[line]}, managed={b[line]}.");
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
