using System.Buffers.Binary;
using System.Diagnostics;
using System.Text;
using OpenTyrian.Core;

namespace OpenTyrian.Core.Tests;

internal static class JukeboxStarfieldTests
{
    public static void Run(string? executable = null)
    {
        for (var scene = 0; scene <= 20; scene++)
        {
            var trace = new StringBuilder();
            MersenneTwister.Seed(314159);
            var stars = new JukeboxStarfield();
            stars.Initialize();
            var platform = new Platform(trace);
            var input = new InputState(platform, new VideoLayout());
            var screen = new IndexedSurface(320, 200, 320, TextWriter.Null);
            stars.Initialize();
            stars.ChangeSetup((byte)(scene % 15));
            for (var frame = 0; frame < 40; frame++)
            {
                input.NewKey = false;
                if (scene >= 15 && frame < 20)
                {
                    int[] keys = [87, 86, 47, 48, 30, 31, 32, 33, 22, 53, 6, 27, 41, 19, 89, 98, 46, 39, 38, 97];
                    var modifiers = scene == 15 ? frame is >= 4 and <= 7 ? 1 : 0 : scene == 16 ? 192 : scene == 17 ? 1 : 4096;
                    platform.Events.Enqueue(new(InputEventKind.KeyDown, keys[frame], (ushort)modifiers));
                    input.ServiceEvents(true);
                    platform.Events.Enqueue(new(InputEventKind.KeyUp, keys[frame]));
                    if (keys[frame] == 19)
                    {
                        platform.Pause = true;
                        if ((modifiers & 0xFC0) == 0) trace.AppendLine("release:1,0,0");
                    }
                }
                if (scene == 19 && frame == 0) { stars.Phase2 = 65534; stars.Phase2Increment = 1; stars.Speed = -1; stars.SpeedChange = -1; stars.Color = 0; }
                if (scene == 20 && frame == 0) { stars.Phase2 = 1; stars.Phase2Increment = -1; stars.PhaseIncrement = -0.1f; stars.Phase = 0; stars.Color = 255; }
                stars.Update(screen, input);
                input.NewKey = false;
                input.ServiceEvents(true);
                platform.Pause = false;
                Dump(trace, frame, stars, screen);
            }
            for (byte type = 0; type <= 14; type++)
            {
                stars.Reset(); stars.ChangeSetup(type);
                stars.Phase2 = 65534; stars.Phase2Increment = 1;
                for (var i = 0; i < 32; i++) { stars.NewStar(); Dump(trace, 1000 + type * 32 + i, stars, screen); }
            }
            if (executable != null)
            {
                var expected = RunNative(executable, scene).Split('\n');
                var actual = trace.ToString().Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
                for (var i = 0; i < Math.Max(actual.Length, expected.Length); i++)
                    Check(actual.ElementAtOrDefault(i) == expected.ElementAtOrDefault(i),
                        $"Jukebox stars scene {scene}, line {i}: native '{expected.ElementAtOrDefault(i)}', managed '{actual.ElementAtOrDefault(i)}'.");
            }
        }
        var invalid = new JukeboxStarfield();
        Throws<ArgumentOutOfRangeException>(() => invalid.ChangeSetup(15));
        var state = new InputState(new Platform(new()), new VideoLayout());
        Throws<InvalidDataException>(() => invalid.Update(new(320, 200, 320, TextWriter.Null), state));
        Throws<ArgumentException>(() => invalid.Update(new(320, 200, 321, TextWriter.Null), state));
    }

    private static void Dump(StringBuilder trace, int frame, JukeboxStarfield stars, IndexedSurface screen)
    {
        var data = new byte[10000];
        for (var i = 0; i < stars.Stars.Length; i++)
        {
            var star = stars.Stars[i];
            short[] values = [star.X, star.Y, star.Z, star.LastX, star.LastY];
            for (var j = 0; j < 5; j++) BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(i * 10 + j * 2), values[j]);
        }
        trace.AppendLine($"frame:{frame},{stars.TemporaryX},{stars.TemporaryY},{Bit(stars.Running)},{stars.Setup},{stars.Steps},{stars.Phase2},{stars.Phase2Increment},{Bits(stars.Phase):X8},{Bits(stars.PhaseIncrement):X8},{Bits(stars.PhaseAcceleration):X8},{stars.ChangeTime},{Bit(stars.AutomaticChanges)},{stars.Speed},{stars.SpeedChange},{stars.Color},{Hash(data):X8},{Hash(screen.Pixels.Span):X8},{MersenneTwister.NextUInt32()}");
    }
    private static uint Bits(float value) => BitConverter.SingleToUInt32Bits(value);
    private static int Bit(bool value) => value ? 1 : 0;
    private static uint Hash(ReadOnlySpan<byte> bytes)
    {
        uint hash = 2166136261;
        foreach (var value in bytes) hash = unchecked((hash ^ value) * 16777619);
        return hash;
    }
    private sealed class Platform(StringBuilder trace) : IInputPlatform
    {
        public Queue<InputEvent> Events { get; } = new();
        public bool Pause;
        public uint Ticks => 0;
        public bool JoystickDown => false;
        public bool IsNetworkGame => false;
        public bool PollEvent(out InputEvent input) => Events.TryDequeue(out input);
        public void Delay(uint milliseconds)
        {
            if (!Pause) throw new InvalidOperationException("Unexpected jukebox input wait.");
            trace.AppendLine("press:1,0,0");
            Events.Enqueue(new(InputEventKind.KeyDown, 40));
        }
        public void SetCursorVisible(bool visible) { }
        public void SetRelativeMouseMode(bool enabled) { }
        public void EnableSystemMouseScaling() { }
        public void ToggleFullscreen() { }
        public void WindowResized() { }
        public void Quit() => throw new InvalidOperationException("Unexpected quit.");
        public void PushJoysticksAsKeyboard() { }
        public void PollJoysticks() { }
        public void CheckNetwork() { }
    }
    private static string RunNative(string executable, int scene)
    {
        var info = new ProcessStartInfo(Path.GetFullPath(executable)) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        info.ArgumentList.Add(scene.ToString());
        using var process = Process.Start(info) ?? throw new InvalidOperationException("Cannot start jukebox star oracle.");
        var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(30000)) { process.Kill(entireProcessTree: true); throw new TimeoutException("Jukebox star oracle timed out."); }
        Check(process.ExitCode == 0, $"Jukebox star oracle failed: {error.GetAwaiter().GetResult()}");
        return output.GetAwaiter().GetResult().Replace("\r\n", "\n", StringComparison.Ordinal);
    }
    private static void Throws<T>(Action action) where T : Exception
    {
        try { action(); } catch (T) { return; }
        throw new InvalidOperationException($"Expected {typeof(T).Name}.");
    }
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
