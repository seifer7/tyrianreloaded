using System.Buffers.Binary;
using System.Diagnostics;
using System.Text;
using OpenTyrian.Core;

namespace OpenTyrian.Core.Tests;

internal static class NetworkSessionTests
{
    public static void Run(string? executable = null)
    {
        for (var scene = 0; scene < 9; scene++)
        {
            var managed = Trace(scene);
            if (executable != null)
            {
                var native = RunNative(executable, scene);
                if (native != managed)
                {
                    var a = native.Split('\n'); var b = managed.Split('\n');
                    var line = Enumerable.Range(0, Math.Min(a.Length, b.Length)).FirstOrDefault(i => a[i] != b[i], -1);
                    throw new InvalidOperationException(line < 0 ? "Network trace lengths differ." :
                        $"Native network scene {scene} line {line}: {a[line]} != {b[line]}.");
                }
            }
        }
        for (var mode = 0; mode < 32; mode++)
        {
            var trace = new StringBuilder();
            using var errors = new StringWriter(trace) { NewLine = "\n" };
            var transport = new Transport(mode, trace) { HandshakeMode = mode };
            var platform = new Platform { HandshakeTrace = trace, Escape = (mode & 8) != 0 };
            var session = new NetworkSession(transport, new(platform, new()), transport.DelayFrames, errors);
            var episodes = Enumerable.Range(0, 5).Select(i => (i + mode) % 3 != 0).ToArray();
            try
            {
                session.Connect(episodes, 1, "Local player name too long"u8, platform.PushJoysticksAsKeyboard);
                trace.AppendLine($"connected:{Bit(session.Connected)},{Encoding.Latin1.GetString(session.OpponentName.Span)}");
                trace.AppendLine($"episodes:{string.Concat(episodes.Select(value => Bit(value)))}");
            }
            catch (NetworkTerminationException e) { trace.AppendLine($"halt:{e.Code},{Bit(e.AttemptSync)}"); }
            if (executable != null)
                Check(RunNative(executable, mode + 10) == trace.ToString().Replace("\r\n", "\n", StringComparison.Ordinal),
                    $"Native network handshake mode {mode} differs.");
        }
        Failures();
        Shutdown(executable);
    }

    private static void Shutdown(string? executable)
    {
        for (var scene = 0; scene < 32; scene++)
        {
            var trace = new StringBuilder();
            var platform = new ShutdownPlatform(trace) { Ticks = scene / 8 == 1 ? 15984u : 0u };
            var input = new InputState(platform, new());
            var transport = new Transport(0, trace);
            var session = new NetworkSession(transport, input, 2, TextWriter.Null);
            if (scene / 8 >= 2)
            {
                transport.Tracing = false;
                session.Prepare(NetworkPacketType.Waiting); session.Send(4);
                transport.Inject(NetworkPacketType.Acknowledge, 0, 4);
                if (scene / 8 == 2) session.Check();
                transport.Tracing = true;
            }
            session.Shutdown((uint)(scene % 8), scene >= 8, new ShutdownPresentation(trace),
                () => trace.AppendLine("release"), code => trace.AppendLine($"game-halt:{code}"));
            if (executable != null)
                Check(RunNative(executable, 100 + scene) == trace.ToString().Replace("\r\n", "\n", StringComparison.Ordinal),
                    $"Native network shutdown scene {scene} differs: {trace}");
            Check(platform.Polls == (scene % 8 is 0 or 7 ? 0 : 3),
                "Network shutdown waited for input on the wrong error code.");
        }
        for (var mask = 0; mask < 32; mask++)
        {
            var requests = new NetworkRequests
            {
                Pause = (mask & 1) != 0,
                InGameMenu = (mask & 2) != 0,
                SkipLevel = (mask & 4) != 0,
                Help = (mask & 8) != 0,
                NortShip = (mask & 16) != 0,
                YourInGameMenu = true
            };
            requests.ClearSpecialRequests();
            var actual = $"requests:{Bit(requests.Pause)},{Bit(requests.InGameMenu)},{Bit(requests.SkipLevel)},{Bit(requests.Help)},{Bit(requests.NortShip)},{Bit(requests.YourInGameMenu)}\n";
            Check(actual == "requests:0,0,0,0,0,1\n", "Clearing local requests changed the peer-menu flag.");
            if (executable != null) Check(RunNative(executable, 200 + mask) == actual, "Native special-request reset differs.");
        }
        var failureTrace = new StringBuilder();
        var failing = new NetworkSession(new Transport(0, failureTrace), new(new ShutdownPlatform(failureTrace), new()), 2, TextWriter.Null);
        var released = 0;
        Throws<IOException>(() => failing.Shutdown(0, false, new ShutdownPresentation(failureTrace) { Failing = true },
            () => released++, _ => throw new InvalidOperationException("Failed rendering must not halt the game.")));
        Check(released == 1, "Failed shutdown leaked its transport.");
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        Throws<OperationCanceledException>(() => failing.Shutdown(0, false, new ShutdownPresentation(failureTrace),
            () => released++, _ => throw new InvalidOperationException("Canceled shutdown must not halt the game."), cancellation.Token));
        Check(released == 2, "Canceled shutdown leaked its transport.");
        failing.Shutdown(uint.MaxValue, false, new ShutdownPresentation(failureTrace), () => released++, _ => { });
        Check(released == 3 && failureTrace.ToString().Contains("Quitting...", StringComparison.Ordinal),
            "Out-of-range shutdown code did not select the native quit message.");
    }

    internal static void RunShutdownSdl(SdlVideoDevice video)
    {
        var trace = new StringBuilder();
        SdlInputPlatform? platform = null;
        var auxiliary = new ShutdownAuxiliary(() => platform!.PostEvent(new(InputEventKind.KeyDown, 40)));
        using var ownedPlatform = platform = new SdlInputPlatform(video, auxiliary,
            () => throw new InvalidOperationException("Unexpected SDL quit during network shutdown."));
        var input = new InputState(platform, video.Layout);
        input.FlushEvents();
        var timer = new DelayTimer(input);
        var palette = Enumerable.Range(0, 256).Select(i => new PaletteColor((byte)i, (byte)(i / 2), (byte)(255 - i))).ToArray();
        var palettes = Enumerable.Range(0, 23).Select(_ => palette).ToArray();
        using var pictures = new MemoryStream();
        BinaryFile.WriteUInt16(pictures, 13);
        for (var i = 0; i < 13; i++) BinaryFile.WriteInt32(pictures, i < 2 ? 54 : 54 + 2 * ((64000 + 62) / 63));
        for (var remaining = 64000; remaining > 0;)
        {
            var count = Math.Min(63, remaining);
            pictures.WriteByte((byte)(0xc0 | count)); pictures.WriteByte(42); remaining -= count;
        }
        var presents = 0; var released = 0; var haltCode = 0;
        var shapes = new MainShapeTables();
        using var glyphs = new MemoryStream();
        BinaryFile.WriteUInt16(glyphs, 127);
        for (var i = 0; i < 127; i++)
        {
            BinaryFile.WriteBoolean(glyphs, true);
            BinaryFile.WriteUInt16(glyphs, 1); BinaryFile.WriteUInt16(glyphs, 1);
            BinaryFile.WriteUInt16(glyphs, 2); glyphs.WriteByte(0xa8); glyphs.WriteByte(254);
        }
        glyphs.Position = 0; shapes.Tables[(int)Font.Small].Load(glyphs);
        var font = new FontRenderer(shapes);
        var presentation = new NetworkShutdownPresentation(video.Primary, video.Palette, pictures, palettes,
            font, timer, () =>
            {
                video.Present(); presents++;
                if (presents == 21)
                    Check(video.Primary.Pixels.Span[0] == 42 &&
                        video.Primary.Pixels.Span[140 * 320 + font.Center("Other player quit the game."u8, Font.Small)] == 0xf5 &&
                        video.Palette.Active.Span.SequenceEqual(palette), "Shutdown did not load picture 2 and fade to its palette.");
            });
        var session = new NetworkSession(new Transport(0, trace), input, 2, TextWriter.Null);
        session.Shutdown(1, false, presentation, () => released++, code => haltCode = code);
        Check(presents == 31 && released == 1 && haltCode == 5 && auxiliary.Polls == 3 &&
            video.Palette.Active.Span.ToArray().All(color => color == new PaletteColor(0, 0, 0)),
            "Actual SDL network shutdown did not present three fades and release before halt.");
        input.FlushEvents();
        var pauseCalls = 0; var setupCalls = 0;
        var gameplay = new LevelNetworkServices(() => pauseCalls++,
            yourRequest => { setupCalls++; return yourRequest; }, video.Primary, font);
        gameplay.Pause();
        Check(gameplay.ShowInGameSetup(true) && pauseCalls == 1 && setupCalls == 1,
            "Gameplay network presentation did not forward pause/setup context.");
        gameplay.DrawDesynchronization(1);
        gameplay.DrawDesynchronization(2);
        video.Present();
        Check(video.Primary.Pixels.Span[110 * 320 + 40] == 0x9a &&
            video.Primary.Pixels.Span[120 * 320 + 40] == 0x9a,
            "Gameplay network desync diagnostics did not use native shade and placement.");
        Throws<ArgumentOutOfRangeException>(() => gameplay.DrawDesynchronization(0));
    }

    private sealed class ShutdownAuxiliary(Action press) : IInputAuxiliaryServices
    {
        public int Polls;
        public bool JoystickDown => false;
        public bool IsNetworkGame => false;
        public void PollJoysticks() { if (++Polls == 3) press(); }
        public void PushJoysticksAsKeyboard() { }
        public void CheckNetwork() { }
    }

    internal sealed class ShutdownPresentation(StringBuilder trace) : INetworkShutdownPresentation
    {
        public bool Failing;
        public void FadeBlack()
        {
            if (Failing) throw new IOException("shutdown renderer failed");
            trace.AppendLine("fade-black:10");
        }
        public void ShowMessage(string message)
        {
            var font = new FontRenderer(new MainShapeTables());
            trace.AppendLine("load:2,0");
            trace.AppendLine($"text:{font.Center(Encoding.Latin1.GetBytes(message), Font.Small)},140,{message}");
        }
        public void Present() => trace.AppendLine("present");
        public void FadeIn() => trace.AppendLine("fade-palette:10,0,255");
    }

    private sealed class ShutdownPlatform(StringBuilder trace) : IInputPlatform
    {
        public uint Ticks { get; set; }
        public int Polls;
        private bool _button;
        public bool JoystickDown => false;
        public bool IsNetworkGame => false;
        public bool PollEvent(out InputEvent input)
        {
            trace.AppendLine($"events:{Bit(_button)}");
            input = _button && Polls == 3 ? new(InputEventKind.KeyDown, 40) : default;
            var result = _button && Polls == 3; _button = false;
            return result;
        }
        public void Delay(uint milliseconds) { trace.AppendLine($"wait:{milliseconds}"); Ticks += milliseconds; }
        public void PollJoysticks() { trace.AppendLine("poll"); Polls++; _button = true; }
        public void PushJoysticksAsKeyboard() { }
        public void CheckNetwork() { }
        public void SetRelativeMouseMode(bool enabled) { }
        public void SetCursorVisible(bool visible) { }
        public void EnableSystemMouseScaling() { }
        public void ToggleFullscreen() { }
        public void WindowResized() { }
        public void Quit() => throw new InvalidOperationException("Unexpected SDL quit during network shutdown.");
    }

    private static string Trace(int scene)
    {
        var trace = new StringBuilder();
        using var diagnostics = new StringWriter(trace) { NewLine = "\n" };
        var platform = new Platform();
        var transport = new Transport(scene, trace);
        var session = new NetworkSession(transport, new(platform, new()), transport.DelayFrames, diagnostics);
        if (scene == 8)
        {
            transport.Tracing = false;
            for (var i = 0; i < 65530; i++)
            {
                session.Prepare(NetworkPacketType.Waiting); session.Send(4);
                transport.Inject(NetworkPacketType.Acknowledge, unchecked((ushort)i), 4); session.Check();
            }
            transport.Tracing = true;
            for (var i = 0; i < 12; i++)
            {
                session.Prepare(NetworkPacketType.Waiting);
                var sequence = BinaryPrimitives.ReadUInt16BigEndian(session.Outgoing[2..]);
                Result(trace, "sent", Bit(session.Send(4)));
                transport.Inject(NetworkPacketType.Acknowledge, sequence, 4);
                Result(trace, "check", session.Check()); trace.AppendLine($"sync:{Bit(session.IsSynchronized)}");
            }
            return trace.ToString().Replace("\r\n", "\n", StringComparison.Ordinal);
        }
        for (var step = 0; step < 64; step++)
        {
            platform.Ticks = (uint)(step * 113);
            if (step % 4 == 0)
            {
                session.Prepare(NetworkPacketType.Details);
                BinaryPrimitives.WriteUInt16BigEndian(session.Outgoing[4..], (ushort)step);
                BinaryPrimitives.WriteUInt16BigEndian(session.Outgoing[6..], (ushort)scene);
                Result(trace, "sent", Bit(session.Send(8))); session.Outgoing.Slice(4, 4).Fill(255);
            }
            switch (step % 8)
            {
                case 0: transport.Inject(NetworkPacketType.Acknowledge, unchecked((ushort)(step / 4 - 1)), 4); break;
                case 1: transport.Inject(NetworkPacketType.Details, (ushort)(step / 8), 8); break;
                case 2: transport.Inject(NetworkPacketType.Waiting, (ushort)(step / 8), 4); break;
                case 3: transport.Inject(NetworkPacketType.GamePause, (ushort)(step / 8 + 1), 4); break;
                case 4: transport.Inject(NetworkPacketType.Acknowledge, (ushort)(step / 4), 4); break;
                case 5: transport.Inject((NetworkPacketType)0x99, (ushort)step, 4); break;
                case 6: transport.Inject(NetworkPacketType.KeepAlive, 0, 4); break;
                case 7: transport.Inject(NetworkPacketType.StateResend, 0, 4); break;
            }
            Result(trace, "check", session.Check()); Dump(trace, "control", session.ControlPacket);
            trace.AppendLine($"update:{Bit(session.Update())}"); trace.AppendLine($"sync:{Bit(session.IsSynchronized)}");
        }
        platform.Ticks = 10000; transport.StatePhase = true; session.ResetState();
        for (var frame = 0; frame < 20; frame++)
        {
            session.PrepareState(); if (frame == 0) session.PrepareState();
            for (var i = 4; i < 28; i++) session.OutgoingState[i] = unchecked((byte)(scene * 17 + frame * 7 + i * 13));
            session.SendState();
            while (session.Check() != 0) { }
            trace.AppendLine($"reset:{Bit(session.StateIsReset)}");
            Result(trace, "stateupdate", Bit(session.UpdateState()));
            Dump(trace, "state", session.IncomingState); Dump(trace, "delayed", session.OutgoingStates[transport.DelayFrames]);
            platform.Ticks += 11;
        }
        session.ResetState(); trace.AppendLine($"reset:{Bit(session.StateIsReset)}");
        Dump(trace, "state", session.IncomingState); Dump(trace, "delayed", session.OutgoingStates[transport.DelayFrames]);
        return trace.ToString().Replace("\r\n", "\n", StringComparison.Ordinal);
    }

    private static void Failures()
    {
        var platform = new Platform(); var transport = new Transport(0, new());
        using var diagnostics = new StringWriter();
        var input = new InputState(platform, new());
        Throws<ArgumentOutOfRangeException>(() => new NetworkSession(transport, input, 0, diagnostics));
        Throws<ArgumentOutOfRangeException>(() => new NetworkSession(transport, input, 8, diagnostics));
        var session = new NetworkSession(transport, input, 1, diagnostics);
        Throws<InvalidOperationException>(() => session.SendState());
        Throws<ArgumentOutOfRangeException>(() => session.Send(3));
        Throws<ArgumentOutOfRangeException>(() => session.Send(257));
        Throws<InvalidDataException>(() => new NetworkPacket(new byte[257]));
        transport.Received.Enqueue(new(new byte[3]));
        Check(session.Check() == 0 && diagnostics.ToString().Contains("short", StringComparison.Ordinal), "Short packet was not diagnosed.");
        transport.Inject(NetworkPacketType.State, 0, 27);
        Throws<InvalidDataException>(() => session.Check());
        transport.Inject(NetworkPacketType.Connect, 0, 13);
        Throws<InvalidDataException>(() => session.Check());
        transport.Inject(NetworkPacketType.Details, 0, 4);
        Throws<InvalidDataException>(() => session.Check());
        transport.Fail = true; session.Prepare(NetworkPacketType.Waiting);
        Throws<IOException>(() => session.Send(4));
        transport.Fail = false;
        for (var i = 0; i < 16; i++) { session.Prepare(NetworkPacketType.Busy); Check(session.Send(4), "Queue filled prematurely."); }
        session.Prepare(NetworkPacketType.Busy);
        Check(!session.Send(4) && diagnostics.ToString().Contains("overflow", StringComparison.Ordinal), "Queue overflow was not explicit.");
        session.ResetState(); session.PrepareState(); session.SendState();
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        Throws<OperationCanceledException>(() => session.UpdateState(cancellation.Token));
        session.Connected = true; platform.Ticks = 17000;
        try { session.Check(); throw new InvalidOperationException("Timeout was ignored."); }
        catch (NetworkTerminationException e) { Check(e.Code == 2 && !e.AttemptSync, "Timeout termination differed."); }
        transport = new Transport(0, new()); session = new(transport, input, 1, diagnostics);
        transport.Inject(NetworkPacketType.Quit, 1, 4);
        try { session.Check(); throw new InvalidOperationException("Opponent quit was ignored."); }
        catch (NetworkTerminationException e) { Check(e.Code == 1 && e.AttemptSync, "Peer quit termination differed."); }
        Check(transport.Sent.Count == 2 && transport.Sent[0].Type == NetworkPacketType.Quit &&
            transport.Sent[1].Type == NetworkPacketType.Acknowledge, "Peer quit was not replied to and acknowledged.");
        platform.Ticks = 1601; session = new(transport, input, 1, diagnostics) { Connected = true };
        session.Check(); Check(transport.Sent[^1].Type == NetworkPacketType.KeepAlive, "Keep-alive threshold differed.");
    }

    private static int Bit(bool value) => value ? 1 : 0;
    private static void Result(StringBuilder trace, string label, int result) => trace.AppendLine($"{label}:{result}");
    private static void Dump(StringBuilder trace, string label, NetworkPacket? packet)
        => trace.AppendLine($"{label}:{(packet == null ? "-" : Convert.ToHexString(packet.Data.Span))}");

    private sealed class Transport(int scene, StringBuilder trace) : INetworkTransport
    {
        public readonly Queue<NetworkPacket> Received = new();
        public readonly List<NetworkPacket> Sent = new();
        private readonly Dictionary<ushort, NetworkPacket> _history = new();
        public int DelayFrames => 1 + scene % 7;
        public bool Tracing = true, StatePhase, Fail;
        public int? HandshakeMode;
        private int _connectSends;
        public void Send(ReadOnlySpan<byte> bytes)
        {
            if (Fail) throw new IOException("UDP fixture failed.");
            var packet = new NetworkPacket(bytes); Sent.Add(packet);
            if (Tracing) Dump(trace, "send", packet);
            if (HandshakeMode is { } mode && packet.Type == NetworkPacketType.Connect)
            {
                if (_connectSends++ == 0)
                {
                    var reply = new byte[19];
                    BinaryPrimitives.WriteUInt16BigEndian(reply, 0x10);
                    BinaryPrimitives.WriteUInt16BigEndian(reply.AsSpan(4), (ushort)((mode & 1) != 0 ? 3 : 2));
                    BinaryPrimitives.WriteUInt16BigEndian(reply.AsSpan(6), (ushort)(DelayFrames + ((mode & 2) != 0 ? 1 : 0)));
                    BinaryPrimitives.WriteUInt16BigEndian(reply.AsSpan(8), (ushort)(mode % 2 != 0 ? 0x0b : 0x17));
                    BinaryPrimitives.WriteUInt16BigEndian(reply.AsSpan(10), (ushort)((mode & 16) != 0 ? 1 : 2));
                    "Remote"u8.CopyTo(reply.AsSpan(12)); Received.Enqueue(new(reply));
                    if ((mode & 4) != 0) Received.Enqueue(new(reply));
                }
                Inject(NetworkPacketType.Acknowledge, packet.Sequence, 4);
            }
            if (!StatePhase) return;
            if (packet.Type == NetworkPacketType.State)
            {
                _history[packet.Sequence] = packet;
                if (!(scene % 2 != 0 && packet.Sequence % DelayFrames == 0)) Received.Enqueue(packet);
            }
            else if (packet.Type == NetworkPacketType.StateXor) Received.Enqueue(packet);
            else if (packet.Type == NetworkPacketType.StateResend && _history.TryGetValue(packet.Sequence, out var state)) Received.Enqueue(state);
        }
        public NetworkPacket? Receive() => Received.TryDequeue(out var packet) ? packet : null;
        public void Inject(NetworkPacketType type, ushort sequence, int length)
        {
            var bytes = new byte[length];
            BinaryPrimitives.WriteUInt16BigEndian(bytes, (ushort)type);
            BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(2), sequence);
            for (var i = 4; i < length; i++) bytes[i] = unchecked((byte)(sequence * 13 + i * 7));
            Received.Enqueue(new(bytes));
        }
    }
    private sealed class Platform : IInputPlatform
    {
        public uint Ticks { get; set; }
        public StringBuilder? HandshakeTrace;
        public bool Escape;
        private bool _escaped, _polling;
        public bool JoystickDown => false;
        public bool IsNetworkGame => false;
        public bool PollEvent(out InputEvent e)
        {
            if (_polling) { _polling = false; e = default; return false; }
            HandshakeTrace?.AppendLine("events:0");
            if (Escape && !_escaped) { _escaped = _polling = true; e = new(InputEventKind.KeyDown, 41); return true; }
            e = default; return false;
        }
        public void Delay(uint milliseconds) { HandshakeTrace?.AppendLine($"wait:{milliseconds}"); Ticks = unchecked(Ticks + milliseconds); }
        public void PushJoysticksAsKeyboard() => HandshakeTrace?.AppendLine("push");
        public void PollJoysticks() { }
        public void CheckNetwork() { }
        public void SetRelativeMouseMode(bool enabled) { }
        public void SetCursorVisible(bool visible) { }
        public void EnableSystemMouseScaling() { }
        public void ToggleFullscreen() { }
        public void WindowResized() { }
        public void Quit() { }
    }
    private static string RunNative(string executable, int scene)
    {
        var info = new ProcessStartInfo(executable) { RedirectStandardOutput = true, RedirectStandardError = true };
        info.ArgumentList.Add(scene.ToString());
        using var process = Process.Start(info) ?? throw new InvalidOperationException("Cannot start native network oracle.");
        var output = process.StandardOutput.ReadToEndAsync(); var errors = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(60000)) { process.Kill(entireProcessTree: true); throw new TimeoutException("Native network oracle timed out."); }
        Check(process.ExitCode == 0, $"Native network oracle failed: {errors.GetAwaiter().GetResult()}");
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
