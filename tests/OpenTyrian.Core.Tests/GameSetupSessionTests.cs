using System.Buffers.Binary;
using System.Diagnostics;
using System.Text;
using OpenTyrian.Core;

namespace OpenTyrian.Core.Tests;

internal static class GameSetupSessionTests
{
    public static void Run(string? executable = null)
    {
        for (var mode = 0; mode < 64; mode++)
        {
            var trace = new StringBuilder();
            var platform = new Platform(trace);
            var input = new InputState(platform, new());
            input.ServiceEvents(false); trace.Clear();
            var network = (mode & 2) != 0 ? new NetworkSession(new Transport(mode, trace), input, 2, TextWriter.Null) : null;
            var requests = network?.Requests ?? new NetworkRequests();
            requests.YourInGameMenu = (mode & 1) != 0;
            var level = CreateLevel(); level.ReallyEndLevel = (mode & 32) != 0;
            var host = new Host(mode, trace);
            var setup = new InGameSetup(new() { ProcessorType = 1, GameSpeed = 4 }, new(), new(), host)
            { ConstantPlay = (mode & 8) != 0, HaltGame = true, PlayerEndLevel = (mode & 16) != 0 };
            var wrapper = new GameSetupSession(input, setup, level, requests,
                () =>
                {
                    trace.AppendLine("shade:3,60,257,80"); trace.AppendLine("shade:5,62,255,78");
                    trace.AppendLine("draw:10,65,Other player in options menu.,1"); trace.AppendLine("present");
                },
                () => trace.AppendLine("present"))
            { QuitRequested = true };
            input.KeysActive[41] = 1;
            wrapper.Run(network);
            trace.AppendLine($"state:{Bit(setup.HaltGame)},{Bit(setup.PlayerEndLevel)},{Bit(level.ReallyEndLevel)}," +
                $"{Bit(wrapper.QuitRequested)},{input.KeysActive[41]},{Bit(requests.YourInGameMenu)},1");
            if (!input.RelativeEnabled) throw new InvalidOperationException("Game setup leaked absolute mouse mode.");
            if (executable is null) continue;
            using var process = Process.Start(new ProcessStartInfo(executable, mode.ToString(System.Globalization.CultureInfo.InvariantCulture))
            { RedirectStandardOutput = true, RedirectStandardError = true }) ?? throw new InvalidOperationException("Cannot start game setup oracle.");
            var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(30000)) { process.Kill(entireProcessTree: true); throw new TimeoutException("Game setup oracle timed out."); }
            var expected = Normalize(output.GetAwaiter().GetResult()); var actual = Normalize(trace.ToString());
            if (process.ExitCode != 0 || expected != actual)
                throw new InvalidOperationException($"Setup wrapper mode {mode} differs:\n{expected}\nManaged:\n{actual}\n{error.GetAwaiter().GetResult()}");
        }
        TestFailure();
        TestWaiting();
    }

    internal static void RunSdlNetwork(SdlNetTransport first, SdlNetTransport second)
    {
        for (var quit = 0; quit < 2; quit++)
        {
            var inputs = Enumerable.Range(0, 2).Select(_ => new InputState(new Platform(new(), realTime: true), new())).ToArray();
            foreach (var input in inputs) input.ServiceEvents(false);
            var sessions = new[] { new NetworkSession(first, inputs[0], 2, TextWriter.Null), new NetworkSession(second, inputs[1], 2, TextWriter.Null) };
            var levels = new[] { CreateLevel(), CreateLevel() };
            var setups = Enumerable.Range(0, 2).Select(_ => new InGameSetup(new() { ProcessorType = 1, GameSpeed = 4 }, new(), new(),
                new Host(quit == 0 ? 0 : 4, new()))).ToArray();
            sessions[0].Requests.YourInGameMenu = true;
            var waiting = 0;
            using var cancellation = new CancellationTokenSource(5000);
            var tasks = Enumerable.Range(0, 2).Select(i => Task.Run(() =>
                new GameSetupSession(inputs[i], setups[i], levels[i], sessions[i].Requests,
                    () => Interlocked.Increment(ref waiting), () => { }).Run(sessions[i], cancellation.Token))).ToArray();
            Task.WhenAll(tasks).GetAwaiter().GetResult();
            if (sessions.Any(session => !session.IsSynchronized || session.Requests.YourInGameMenu) ||
                inputs.Any(input => !input.RelativeEnabled) || waiting != 1 ||
                levels.Any(level => level.ReallyEndLevel != (quit != 0)) ||
                setups.Any(setup => setup.PlayerEndLevel != (quit != 0)) || setups[0].HaltGame != (quit != 0) || setups[1].HaltGame)
                throw new InvalidOperationException("Actual SDL2_net settings resume/quit did not synchronize both peers.");
            while (first.Receive() != null) { }
            while (second.Receive() != null) { }
        }
    }

    private static LevelEvents CreateLevel()
    {
        var definitions = new ItemDefinitions();
        definitions.ParseBundle(Encoding.Latin1.GetBytes(LevelEventTests.Fixture()), "game-setup.tyrr");
        var items = ItemDatabase.FromDefinitions(definitions);
        return new(items, new(items, TextWriter.Null), [new(0), new(1)], new(), new(new byte[8]));
    }

    private static void TestFailure()
    {
        var input = new InputState(new Platform(new()), new());
        var requests = new NetworkRequests { YourInGameMenu = true };
        var wrapper = new GameSetupSession(input,
            new(new() { ProcessorType = 0, GameSpeed = 4 }, new(), new(), new Host(0, new())),
            CreateLevel(), requests, () => { }, () => { });
        try { wrapper.Run(); throw new InvalidOperationException("Invalid setup settings were accepted."); }
        catch (InvalidDataException)
        {
            if (!input.RelativeEnabled || requests.YourInGameMenu) throw new InvalidOperationException("Failed setup leaked input/request state.");
        }
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        requests.YourInGameMenu = true;
        var network = new NetworkSession(new Transport(0, new()), input, 2, TextWriter.Null);
        try { wrapper.Run(network, cancellation.Token); throw new InvalidOperationException("Setup ignored cancellation."); }
        catch (OperationCanceledException)
        {
            if (!input.RelativeEnabled || requests.YourInGameMenu) throw new InvalidOperationException("Canceled setup leaked input/request state.");
        }
    }

    private static void TestWaiting()
    {
        var shapes = new MainShapeTables();
        using var data = new MemoryStream();
        BinaryFile.WriteUInt16(data, 127);
        for (var i = 0; i < 127; i++)
        {
            BinaryFile.WriteBoolean(data, true); BinaryFile.WriteUInt16(data, 1); BinaryFile.WriteUInt16(data, 1);
            BinaryFile.WriteUInt16(data, 2); data.WriteByte(0xa8); data.WriteByte(254);
        }
        data.Position = 0; shapes.Tables[(int)Font.Normal].Load(data);
        var primary = new IndexedSurface(320, 200, 320, TextWriter.Null); primary.Pixels.Span.Fill(0xa8);
        var unrelated = new IndexedSurface(320, 200, 320, TextWriter.Null); unrelated.Pixels.Span.Fill(0xbb);
        var presented = 0;
        new GameSetupWaiting(primary, new(shapes), () => presented++).Draw();
        if (presented != 1 || primary.Pixels.Span[60 * 320 + 3] != 0xa4 || primary.Pixels.Span[62 * 320 + 5] != 0xa2 ||
            primary.Pixels.Span[65 * 320 + 10] != 0xf5 || unrelated.Pixels.Span.ContainsAnyExcept((byte)0xbb))
            throw new InvalidOperationException("Settings peer-wait presentation changed shades/font or unrelated surface.");
    }

    private static int Bit(bool value) => value ? 1 : 0;
    private static string Normalize(string text)
    {
        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        return string.Join('\n', lines.Where((line, i) => i == 0 || line != "present" || lines[i - 1] != "present"));
    }

    private sealed class Transport(int mode, StringBuilder trace) : INetworkTransport
    {
        private readonly Queue<NetworkPacket> _packets = new();
        public void Send(ReadOnlySpan<byte> packet)
        {
            var type = (NetworkPacketType)BinaryPrimitives.ReadUInt16BigEndian(packet);
            if (type == NetworkPacketType.Acknowledge) return;
            trace.AppendLine($"send:{(ushort)type}");
            var ack = new byte[4]; packet.Slice(2, 2).CopyTo(ack.AsSpan(2)); _packets.Enqueue(new(ack));
            if (type != NetworkPacketType.GameMenu) return;
            Queue(NetworkPacketType.GameMenu, 0);
            if ((mode & 1) == 0) Queue((mode & 4) != 0 ? NetworkPacketType.GameQuit : NetworkPacketType.Waiting, 1);
        }
        private void Queue(NetworkPacketType type, ushort sequence)
        {
            var data = new byte[4]; BinaryPrimitives.WriteUInt16BigEndian(data, (ushort)type);
            BinaryPrimitives.WriteUInt16BigEndian(data.AsSpan(2), sequence); _packets.Enqueue(new(data));
        }
        public NetworkPacket? Receive() => _packets.TryDequeue(out var packet) ? packet : null;
    }

    private sealed class Platform(StringBuilder trace, bool realTime = false) : IInputPlatform
    {
        private bool _focus = true;
        private readonly uint _started = unchecked((uint)Environment.TickCount);
        private uint _ticks;
        public uint Ticks => realTime ? unchecked((uint)Environment.TickCount - _started) : _ticks;
        public bool JoystickDown => false;
        public bool IsNetworkGame => false;
        public bool PollEvent(out InputEvent input)
        {
            input = new(InputEventKind.FocusGained);
            var result = _focus; _focus = false; return result;
        }
        public void Delay(uint milliseconds)
        {
            if (realTime) Thread.Sleep(checked((int)milliseconds));
            else _ticks += milliseconds;
        }
        public void SetRelativeMouseMode(bool enabled) => trace.AppendLine($"relative:{Bit(enabled)}");
        public void SetCursorVisible(bool visible) { }
        public void EnableSystemMouseScaling() { }
        public void ToggleFullscreen() { }
        public void WindowResized() { }
        public void PushJoysticksAsKeyboard() { }
        public void PollJoysticks() { }
        public void CheckNetwork() { }
        public void Quit() => throw new InvalidOperationException("Unexpected settings quit.");
    }

    private sealed class Host(int mode, StringBuilder trace) : IInGameSetupHost
    {
        private int _frame;
        public SetupInput Input { get; private set; }
        public bool MusicDisabled { get; set; }
        public bool SamplesDisabled { get; set; }
        public void EnsureCursor() => trace.AppendLine("menu");
        public void Shade(int left, int top, int right, int bottom) { }
        public void Capture() { }
        public void Restore() { }
        public void ResetCursor() { }
        public void Label(int x, int y, ReadOnlyMemory<byte> text, int brightness) { }
        public void Gauge(int x, int y, int color, int amount) { }
        public void Help(ReadOnlyMemory<byte> text) { }
        public void ServiceEvents(bool clear)
        {
            if (clear) Input = default;
            else { _frame++; Input = new(true, false, (mode & 4) == 0 ? 41 : _frame <= 5 ? 81 : 40, 0, 0, 0, 0, 0); }
        }
        public void MouseStart() { }
        public void Present() { }
        public void MouseReplace() { }
        public void Delay(uint milliseconds) { }
        public void PushJoysticks() { }
        public void KeepAlive() { }
        public void Sample(byte number) { }
        public void Volume(ushort music, ushort effects) { }
        public void InitializeProcessor() { }
        public void SetSpeed() { }
        public void Halt() => trace.AppendLine("halt");
    }
}
