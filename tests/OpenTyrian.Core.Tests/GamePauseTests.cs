using System.Buffers.Binary;
using System.Diagnostics;
using System.Text;
using OpenTyrian.Core;

namespace OpenTyrian.Core.Tests;

internal static class GamePauseTests
{
    public static void Run(string? executable = null)
    {
        for (var mode = 0; mode < 16; mode++)
        {
            if ((mode & 8) != 0 && (mode & 2) == 0) continue;
            var trace = new StringBuilder();
            var platform = new Platform(mode, trace);
            var input = new InputState(platform, new());
            platform.Focus(); input.ServiceEvents(false); trace.Clear();
            var transport = new Transport(mode, platform);
            var session = (mode & 2) != 0 ? new NetworkSession(transport, input, 2, TextWriter.Null) : null;
            var pause = new GamePause(input, new(input),
                () => { trace.AppendLine("draw:1,120,90,1"); trace.AppendLine("present"); },
                platform.PushJoysticksAsKeyboard,
                (music, effects) => trace.AppendLine($"volume:{music},{effects}"));
            pause.Run(191, 123, (mode & 1) != 0, session);
            trace.AppendLine("restored:1");
            if (!input.RelativeEnabled || !trace.ToString().Contains("volume:191,123", StringComparison.Ordinal))
                throw new InvalidOperationException("Pause did not restore volume and mouse-relative state.");
            if (executable != null)
            {
                using var process = Process.Start(new ProcessStartInfo(executable, mode.ToString(System.Globalization.CultureInfo.InvariantCulture))
                { RedirectStandardOutput = true, RedirectStandardError = true }) ?? throw new InvalidOperationException("Cannot start pause oracle.");
                var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
                if (!process.WaitForExit(30000)) { process.Kill(entireProcessTree: true); throw new TimeoutException("Pause oracle timed out."); }
                var actual = Normalize(trace.ToString()); var expected = Normalize(output.GetAwaiter().GetResult());
                if (process.ExitCode != 0 || actual != expected)
                    throw new InvalidOperationException($"Pause mode {mode} differs:\n{expected}\nManaged:\n{actual}\n{error.GetAwaiter().GetResult()}");
            }
        }
        var failures = new StringBuilder();
        var failingPlatform = new Platform(0, failures);
        var failingInput = new InputState(failingPlatform, new());
        var restored = false;
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        var canceled = new GamePause(failingInput, new(failingInput), () => { }, () => { },
            (music, _) => restored = music == 191);
        var canceledWait = false;
        try { canceled.Run(191, 123, true, cancellation: cancellation.Token); }
        catch (OperationCanceledException)
        {
            canceledWait = true;
            if (!restored || !failingInput.RelativeEnabled) throw new InvalidOperationException("Canceled pause leaked input/audio state.");
        }
        if (!canceledWait || !restored) throw new InvalidOperationException("Canceled pause did not unwind and restore volume.");
        var failing = new GamePause(failingInput, new(failingInput), () => throw new IOException("pause drawing failed"),
            () => { }, (music, _) => restored = music == 191);
        try { failing.Run(191, 123, false); }
        catch (IOException)
        {
            if (!restored || !failingInput.RelativeEnabled) throw new InvalidOperationException("Pause failure left muted audio or mouse state.");
            return;
        }
        throw new InvalidOperationException("Pause draw failure was suppressed.");
    }

    internal static void RunSdlNetwork(SdlNetTransport first, SdlNetTransport second)
    {
        var platforms = new[] { new Platform(0, new(), realTime: true), new Platform(0, new(), realTime: true) };
        var inputs = platforms.Select(platform => new InputState(platform, new())).ToArray();
        var sessions = new[] { new NetworkSession(first, inputs[0], 2, TextWriter.Null),
            new NetworkSession(second, inputs[1], 2, TextWriter.Null) };
        var restored = new bool[2];
        using var cancellation = new CancellationTokenSource(5000);
        var tasks = Enumerable.Range(0, 2).Select(i => Task.Run(() =>
            new GamePause(inputs[i], new(inputs[i]), () => { }, platforms[i].PushJoysticksAsKeyboard,
                (music, effects) => restored[i] = music == 191 && effects == 123)
                .Run(191, 123, true, sessions[i], cancellation.Token))).ToArray();
        Task.WhenAll(tasks).GetAwaiter().GetResult();
        if (sessions.Any(session => !session.IsSynchronized) || restored.Any(value => !value) ||
            inputs.Any(input => !input.RelativeEnabled))
            throw new InvalidOperationException("Actual SDL2_net pause/resume did not acknowledge and restore both peers.");
        while (first.Receive() != null) { }
        while (second.Receive() != null) { }
    }

    internal static void RunSdl(SdlVideoDevice video)
    {
        SdlInputPlatform? platform = null;
        var pushes = 0;
        var auxiliary = new Auxiliary(() =>
        {
            pushes++;
            platform!.PostEvent(new(InputEventKind.KeyDown, pushes == 1 ? 224 : 40));
        });
        using var ownedPlatform = platform = new SdlInputPlatform(video, auxiliary,
            () => throw new InvalidOperationException("Unexpected SDL pause quit."));
        var input = new InputState(platform, video.Layout); input.FlushEvents();
        var shapes = new MainShapeTables();
        using var data = new MemoryStream();
        BinaryFile.WriteUInt16(data, 127);
        for (var i = 0; i < 127; i++)
        {
            BinaryFile.WriteBoolean(data, true); BinaryFile.WriteUInt16(data, 1); BinaryFile.WriteUInt16(data, 1);
            BinaryFile.WriteUInt16(data, 2); data.WriteByte(0xa8); data.WriteByte(254);
        }
        data.Position = 0; shapes.Tables[(int)Font.Normal].Load(data);
        video.Primary.Pixels.Span.Clear();
        var drawn = 0; var volumes = new List<(byte Music, byte Effects)>();
        var presentation = new GamePausePresentation(video.Primary, new(shapes), "Paused"u8.ToArray(),
            () => { video.Present(); drawn++; });
        new GamePause(input, new(input), presentation.Draw, platform.PushJoysticksAsKeyboard,
            (music, effects) => volumes.Add((music, effects))).Run(191, 123, false);
        if (drawn != 1 || pushes != 2 || video.Primary.Pixels.Span[90 * 320 + 120] != 0xf5 ||
            !input.RelativeEnabled || !volumes.SequenceEqual(new[] { ((byte)95, (byte)123), ((byte)191, (byte)123) }))
            throw new InvalidOperationException("Actual SDL pause did not render/filter modifier input/restore volume.");
        input.FlushEvents();
    }

    private sealed class Auxiliary(Action push) : IInputAuxiliaryServices
    {
        public bool JoystickDown => false;
        public bool IsNetworkGame => false;
        public void PushJoysticksAsKeyboard() => push();
        public void PollJoysticks() { }
        public void CheckNetwork() { }
    }

    private static string Normalize(string trace)
    {
        // Protocol primitives are covered separately; compare the pause loop's
        // externally ordered input, timing, presentation and volume effects.
        return string.Join('\n', trace.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n')
            .Where(line => !line.StartsWith("prepare:", StringComparison.Ordinal) &&
                !line.StartsWith("send:", StringComparison.Ordinal) && !line.StartsWith("update:", StringComparison.Ordinal) &&
                line != "check"));
    }

    private sealed class Transport(int mode, Platform platform) : INetworkTransport
    {
        private readonly Queue<NetworkPacket> _packets = new();
        private ushort _sequence;
        public void Send(ReadOnlySpan<byte> packet)
        {
            var type = (NetworkPacketType)BinaryPrimitives.ReadUInt16BigEndian(packet);
            if (type is NetworkPacketType.GamePause or NetworkPacketType.Waiting)
            {
                var ack = new byte[4];
                BinaryPrimitives.WriteUInt16BigEndian(ack.AsSpan(2), BinaryPrimitives.ReadUInt16BigEndian(packet[2..]));
                if (type == NetworkPacketType.GamePause) Queue(NetworkPacketType.GamePause);
                _packets.Enqueue(new(ack));
            }
        }
        private void Queue(NetworkPacketType type)
        {
            var data = new byte[4]; BinaryPrimitives.WriteUInt16BigEndian(data, (ushort)type);
            BinaryPrimitives.WriteUInt16BigEndian(data.AsSpan(2), _sequence++); _packets.Enqueue(new(data));
        }
        public NetworkPacket? Receive()
        {
            if (_packets.TryDequeue(out var packet)) return packet;
            if ((mode & 8) != 0 && platform.Frame == 3)
            {
                Queue(NetworkPacketType.Waiting); platform.Frame++;
                return _packets.Dequeue();
            }
            return null;
        }
    }
    private sealed class Platform(int mode, StringBuilder trace, bool realTime = false) : IInputPlatform
    {
        private readonly Queue<InputEvent> _events = new();
        public int Frame;
        public void Focus() => _events.Enqueue(new(InputEventKind.FocusGained));
        public uint Ticks { get; private set; }
        public bool JoystickDown => false;
        public bool IsNetworkGame => false;
        public bool PollEvent(out InputEvent input) => _events.TryDequeue(out input);
        public void Delay(uint milliseconds)
        {
            trace.AppendLine($"delay:{milliseconds}"); Ticks += milliseconds;
            if (realTime) Thread.Sleep(checked((int)milliseconds));
        }
        public void PushJoysticksAsKeyboard()
        {
            Frame++; trace.AppendLine($"push:{Frame}");
            if ((mode & 8) == 0)
            {
                int[] keys = [224, 228, 226, 230, 40];
                _events.Enqueue(new(InputEventKind.KeyDown, keys[(Frame - 1) % 5]));
            }
            if ((mode & 4) != 0 && Frame == 3) _events.Enqueue(new(InputEventKind.MouseButtonDown, Button: 1));
        }
        public void SetRelativeMouseMode(bool enabled) => trace.AppendLine($"relative:{(enabled ? 1 : 0)}");
        public void SetCursorVisible(bool visible) { }
        public void EnableSystemMouseScaling() { }
        public void ToggleFullscreen() { }
        public void WindowResized() { }
        public void PollJoysticks() { }
        public void CheckNetwork() { }
        public void Quit() => throw new InvalidOperationException("Unexpected pause quit.");
    }
}
