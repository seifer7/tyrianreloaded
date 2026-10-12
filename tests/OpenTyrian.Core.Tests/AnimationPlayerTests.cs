using System.Buffers.Binary;
using System.Diagnostics;
using System.Text;
using OpenTyrian.Core;

namespace OpenTyrian.Core.Tests;

internal static class AnimationPlayerTests
{
    internal static void RunSdl(SdlVideoDevice video)
    {
        using var platform = new SdlInputPlatform(video, new Auxiliary(), () => throw new InvalidOperationException("Unexpected animation quit."));
        var input = new InputState(platform, video.Layout);
        input.FlushEvents();
        var presents = 0;
        var checks = 0;
        var player = new AnimationPlayer(video.Primary, video.Palette, input, new(input), () =>
        {
            video.Present();
            presents++;
            if (presents == 4) platform.PostEvent(new(InputEventKind.KeyDown, 40));
        }, () => checks++);
        player.Play(new MemoryStream(Fixture()), 0, 1);
        Check(presents == 4 && checks == 2 && input.NewKey, "SDL animation did not interrupt after its presented third frame.");
        Check(video.Primary.Pixels.Span[0] == 3 && video.Primary.Pixels.Span[5] == 19 &&
            video.Palette.Active.Span[0] == new PaletteColor(255, 0, 0), "SDL animation palette/frame did not reach the renderer.");
        input.FlushEvents();
    }

    private sealed class Auxiliary : IInputAuxiliaryServices
    {
        public bool JoystickDown => false;
        public bool IsNetworkGame => false;
        public void PushJoysticksAsKeyboard() { }
        public void PollJoysticks() { }
        public void CheckNetwork() { }
    }

    public static void Run(string? executable = null)
    {
        var bytes = Fixture();
        foreach (var start in new byte[] { 0, 1, 3, 5, 6, 255 })
            foreach (var speed in new byte[] { 0, 1, 7, 255 })
                foreach (var interrupt in new[] { -1, 1, 3 })
                {
                    var trace = new StringBuilder();
                    var shown = 0;
                    var platform = new Platform(() => shown - 1 == interrupt, trace);
                    var input = new InputState(platform, new VideoLayout());
                    var timer = new DelayTimer(input);
                    var screen = new IndexedSurface(320, 200, 320, TextWriter.Null);
                    screen.Pixels.Span.Fill(193);
                    var palette = new PaletteSystem(color => ((uint)color.Red << 16) | ((uint)color.Green << 8) | color.Blue);
                    var player = new AnimationPlayer(screen, palette, input, timer, () =>
                    {
                        if (shown++ == 0)
                        {
                            trace.Append("palette:0,255:");
                            foreach (var color in palette.Active.Span) trace.Append($"{color.Red:X2}{color.Green:X2}{color.Blue:X2}");
                            trace.AppendLine();
                        }
                        else trace.AppendLine($"delay:{speed}");
                        trace.AppendLine($"show:{Hash(screen.Pixels.Span):X8}");
                    }, () =>
                    {
                        trace.AppendLine("network");
                        if (timer.GetDelayTicks() == 0) trace.AppendLine("wait");
                    });
                    using var file = new MemoryStream(bytes);
                    player.Play(file, start, speed, leaveOpen: true);
                    Check(file.CanRead, "Playback leaveOpen lost stream ownership.");
                    var expectedFrames = start >= 6 ? 0 : interrupt > 0 ? Math.Min(6 - start, interrupt) : 6 - start;
                    Check(shown == expectedFrames + 1, "Playback skipped a frame or played the final loop delta.");
                    Check(platform.Clock == (uint)((interrupt > 0 && expectedFrames == interrupt ? expectedFrames - 1 : expectedFrames) *
                        (int)(speed * (0x4300 * (12.0f / 14318180.0f) * 1000.0f))), "PIT playback timing changed.");
                    if (executable != null)
                    {
                        var path = Path.Combine(Path.GetTempPath(), $"opentyrian-animation-{Guid.NewGuid():N}.anm");
                        try
                        {
                            File.WriteAllBytes(path, bytes);
                            var actual = RunNative(executable, path, start, speed, interrupt);
                            Check(actual == trace.ToString().Replace("\r\n", "\n", StringComparison.Ordinal),
                                $"Native animation playback differs for start {start}, speed {speed}, interrupt {interrupt}.");
                        }
                        finally { File.Delete(path); }
                    }
                }
        Failures(bytes);
    }

    private static void Failures(byte[] bytes)
    {
        var trace = new StringBuilder();
        var platform = new Platform(() => false, trace);
        var input = new InputState(platform, new VideoLayout());
        var screen = new IndexedSurface(320, 200, 320, TextWriter.Null);
        var palette = new PaletteSystem(_ => 0);
        var player = new AnimationPlayer(screen, palette, input, new(input), () => { }, () => { });
        var bad = bytes.ToArray(); bad[0] = 0;
        var invalid = new MemoryStream(bad);
        Throws<InvalidDataException>(() => player.Play(invalid, 0, 1));
        Check(!invalid.CanRead, "Invalid animation stream was not closed.");
        var rendered = new MemoryStream(bytes);
        Throws<IOException>(() => new AnimationPlayer(screen, palette, input, new(input),
            () => throw new IOException("display failed"), () => { }).Play(rendered, 0, 1));
        Check(!rendered.CanRead, "Failed presentation leaked the animation stream.");
        bad = bytes.ToArray(); BinaryPrimitives.WriteUInt16LittleEndian(bad.AsSpan(0x500), 1);
        Throws<InvalidDataException>(() => player.Play(new MemoryStream(bad), 0, 1));
        bad = bytes.ToArray(); BinaryPrimitives.WriteUInt16LittleEndian(bad.AsSpan(0xB08), 1);
        Throws<InvalidDataException>(() => player.Play(new MemoryStream(bad), 0, 1));
        bad = bytes.ToArray(); bad[0xB00 + 8 + 8 + 4] = 127;
        Throws<InvalidDataException>(() => player.Play(new MemoryStream(bad), 0, 1));
        Throws<ArgumentException>(() => new AnimationPlayer(new(320, 200, 321, TextWriter.Null), palette, input,
            new(input), () => { }, () => { }).Play(new MemoryStream(bytes), 0, 1));
        Throws<FileNotFoundException>(() => player.PlayFile(Path.Combine(Path.GetTempPath(), $"absent-{Guid.NewGuid():N}.anm"), 0, 1));
        var single = bytes.ToArray(); BinaryPrimitives.WriteUInt32LittleEndian(single.AsSpan(8), 1);
        var presentations = 0;
        new AnimationPlayer(screen, palette, input, new(input), () => presentations++,
            () => throw new InvalidOperationException("No frame should need network service.")).Play(new MemoryStream(single), 0, 255);
        Check(presentations == 1, "Single-record animation underflowed final-delta frame count.");
        var path = Path.Combine(Path.GetTempPath(), $"animation-file-{Guid.NewGuid():N}.anm");
        try { File.WriteAllBytes(path, bytes); player.PlayFile(path, 0, 0); }
        finally { File.Delete(path); }
    }

    private static byte[] Fixture()
    {
        var pages = new[] { 4, 3 };
        var data = new byte[0xB00 + 65536 + 8 + 6 + 45];
        "LPF "u8.CopyTo(data); Word(6, 2); BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(8), 7);
        for (var i = 0; i < 256; i++)
        {
            data[0x100 + i * 4] = (byte)i; data[0x101 + i * 4] = unchecked((byte)(i * 3));
            data[0x102 + i * 4] = unchecked((byte)(255 - i));
        }
        for (var page = 0; page < 2; page++)
        {
            var offset = 0xB00 + page * 65536;
            Word(0x500 + page * 6, (ushort)(page * 4)); Word(0x502 + page * 6, (ushort)pages[page]); Word(0x504 + page * 6, (ushort)(pages[page] * 15));
            Word(offset, (ushort)(page * 4)); Word(offset + 2, (ushort)pages[page]); Word(offset + 4, (ushort)(pages[page] * 15));
            for (var record = 0; record < pages[page]; record++)
            {
                Word(offset + 8 + record * 2, 15);
                var frame = page * 4 + record;
                byte[] compressed = [0, 3, (byte)(frame + 1), 0x82, 2, (byte)(frame + 17), (byte)(frame + 23), 0x81, 0x80, 0, 0];
                compressed.CopyTo(data, offset + 8 + pages[page] * 2 + record * 15 + 4);
            }
        }
        return data;
        void Word(int offset, ushort value) => BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(offset), value);
    }
    private static uint Hash(ReadOnlySpan<byte> bytes)
    {
        uint hash = 2166136261;
        foreach (var value in bytes) hash = unchecked((hash ^ value) * 16777619);
        return hash;
    }
    private sealed class Platform(Func<bool> interrupt, StringBuilder trace) : IInputPlatform
    {
        private bool _delivered;
        public uint Clock;
        public uint Ticks => Clock;
        public bool JoystickDown => false;
        public bool IsNetworkGame => false;
        public void Delay(uint milliseconds) { Clock += milliseconds; trace.AppendLine("wait"); }
        public bool PollEvent(out InputEvent input)
        {
            input = default;
            if (!_delivered) trace.AppendLine("events:1");
            if (_delivered) { _delivered = false; return false; }
            _delivered = true;
            if (interrupt()) { input = new(InputEventKind.KeyDown, 40); return true; }
            _delivered = false;
            return false;
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
    private static string RunNative(string executable, string path, byte start, byte speed, int interrupt)
    {
        var info = new ProcessStartInfo(Path.GetFullPath(executable)) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var value in new[] { path, start.ToString(), speed.ToString(), interrupt.ToString() }) info.ArgumentList.Add(value);
        using var process = Process.Start(info) ?? throw new InvalidOperationException("Cannot start animation oracle.");
        var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(30000)) { process.Kill(entireProcessTree: true); throw new TimeoutException("Animation oracle timed out."); }
        Check(process.ExitCode == 0, $"Animation oracle failed: {error.GetAwaiter().GetResult()}");
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
