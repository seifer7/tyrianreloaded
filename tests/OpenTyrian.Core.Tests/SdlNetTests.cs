using System.Diagnostics;
using System.Text;
using OpenTyrian.Core;

namespace OpenTyrian.Core.Tests;

internal static class SdlNetTests
{
    public static void Run(string executable)
    {
        var trace = new StringBuilder();
        using var first = new SdlNetTransport(0);
        using var second = new SdlNetTransport(0);
        Check(first.LocalPort != 0 && second.LocalPort != 0 && first.LocalPort != second.LocalPort, "SDL2_net local ports differ.");
        Throws<InvalidOperationException>(() => first.Send(new byte[4]));
        first.Bind("127.0.0.1", second.LocalPort); second.Bind("127.0.0.1", first.LocalPort);
        Check(first.Receive() == null && second.Receive() == null, "Empty SDL2_net receive was not empty.");
        for (var i = 0; i < 128; i++)
        {
            var output = new byte[i == 127 ? 256 : 4 + i];
            for (var j = 0; j < output.Length; j++) output[j] = unchecked((byte)(i * 31 + j * 17));
            first.Send(output);
            var received = Receive(second);
            trace.AppendLine($"packet:{i},{received.Data.Length},{received.Channel},{Convert.ToHexString(received.Data.Span)}");
            second.Send(received.Data.Span);
            var echoed = Receive(first);
            Check(echoed.Channel == 0 && echoed.Data.Span.SequenceEqual(output), "SDL2_net echo bytes/channel differ.");
        }
        CompareNative(executable, trace.ToString().Replace("\r\n", "\n", StringComparison.Ordinal));
        var sender = new NetworkSession(first, new(new Platform(), new()), 2, TextWriter.Null);
        var receiver = new NetworkSession(second, new(new Platform(), new()), 2, TextWriter.Null);
        bool[] firstEpisodes = [true, true, false, true, true], secondEpisodes = [false, true, true, true, false];
        using (var cancellation = new CancellationTokenSource(5000))
        {
            var peer = Task.Run(() => sender.Connect(firstEpisodes, 1, "Alpha"u8, () => { }, cancellation.Token));
            var otherPeer = Task.Run(() => receiver.Connect(secondEpisodes, 2, "Beta"u8, () => { }, cancellation.Token));
            Task.WhenAll(peer, otherPeer).Wait();
        }
        Check(sender.Connected && receiver.Connected && sender.OpponentName.Span.SequenceEqual("Beta"u8) &&
            receiver.OpponentName.Span.SequenceEqual("Alpha"u8) && firstEpisodes.SequenceEqual(secondEpisodes) &&
            firstEpisodes.SequenceEqual(new[] { false, true, false, true, false }), "Real SDL2_net handshake did not negotiate players/episodes/names.");
        for (var pass = 0; pass < 16; pass++)
        {
            while (sender.Check() != 0) sender.Update();
            while (receiver.Check() != 0) receiver.Update();
            Thread.Sleep(1);
        }
        Check(sender.IsSynchronized && receiver.IsSynchronized, "Final SDL2_net connection packets did not synchronize.");
        sender.Prepare(NetworkPacketType.GamePause); Check(sender.Send(4), "Real SDL2_net control send failed.");
        Poll(() => receiver.Check() == 1);
        Check(receiver.ControlPacket?.Type == NetworkPacketType.GamePause, "Real SDL2_net queue did not receive pause.");
        Poll(() => sender.Check() == 1);
        Check(sender.IsSynchronized && receiver.Update(), "Real SDL2_net acknowledgment did not synchronize.");
        sender.ResetState(); receiver.ResetState();
        for (var frame = 0; frame < 12; frame++)
        {
            sender.PrepareState(); receiver.PrepareState();
            for (var i = 4; i < 28; i++)
            {
                sender.OutgoingState[i] = unchecked((byte)(frame * 3 + i));
                receiver.OutgoingState[i] = unchecked((byte)(frame * 5 + i));
            }
            sender.SendState(); receiver.SendState();
            var packets = frame % 2 == 1 ? 2 : 1;
            for (var i = 0; i < packets; i++) { Poll(() => sender.Check() == 1); Poll(() => receiver.Check() == 1); }
            if (sender.UpdateState() && receiver.UpdateState())
                Check(sender.IncomingState!.Data.Span[4..].SequenceEqual(receiver.OutgoingStates[2]!.Data.Span[4..]) &&
                    receiver.IncomingState!.Data.Span[4..].SequenceEqual(sender.OutgoingStates[2]!.Data.Span[4..]),
                    "Real SDL2_net delayed gameplay states differed.");
        }
        Throws<ArgumentOutOfRangeException>(() => first.Send(new byte[257]));
        Throws<ArgumentException>(() => first.Bind("", second.LocalPort));
        LevelNetworkTests.RunSdl(first, second);
        GamePauseTests.RunSdlNetwork(first, second);
        var haltCode = 0;
        sender.Shutdown(0, false, new NetworkSessionTests.ShutdownPresentation(trace), first.Dispose, code => haltCode = code);
        Check(haltCode == 5, "Actual SDL2_net shutdown did not reach the game halt callback.");
        first.Dispose();
        Throws<ObjectDisposedException>(() => first.Receive());
        using var reopened = new SdlNetTransport(0);
        Check(reopened.LocalPort != 0, "SDL2_net transport did not reopen.");
    }

    private static NetworkPacket Receive(SdlNetTransport transport)
    {
        NetworkPacket? packet = null;
        Poll(() => (packet = transport.Receive()) != null);
        return packet!;
    }
    private static void Poll(Func<bool> ready)
    {
        var start = Stopwatch.StartNew();
        while (!ready())
        {
            if (start.ElapsedMilliseconds > 2000) throw new TimeoutException("SDL2_net loopback test timed out.");
            Thread.Sleep(1);
        }
    }
    private sealed class Platform : IInputPlatform
    {
        public uint Ticks => unchecked((uint)Environment.TickCount);
        public bool JoystickDown => false;
        public bool IsNetworkGame => false;
        public bool PollEvent(out InputEvent e) { e = default; return false; }
        public void Delay(uint milliseconds) => Thread.Sleep(checked((int)milliseconds));
        public void PushJoysticksAsKeyboard() { }
        public void PollJoysticks() { }
        public void CheckNetwork() { }
        public void SetRelativeMouseMode(bool enabled) { }
        public void SetCursorVisible(bool visible) { }
        public void EnableSystemMouseScaling() { }
        public void ToggleFullscreen() { }
        public void WindowResized() { }
        public void Quit() { }
    }
    private static void CompareNative(string executable, string expected)
    {
        using var process = Process.Start(new ProcessStartInfo(executable) { RedirectStandardOutput = true, RedirectStandardError = true })
            ?? throw new InvalidOperationException("Cannot start SDL2_net oracle.");
        var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(30000)) { process.Kill(entireProcessTree: true); throw new TimeoutException("SDL2_net oracle timed out."); }
        Check(process.ExitCode == 0 && output.GetAwaiter().GetResult().Replace("\r\n", "\n", StringComparison.Ordinal) == expected,
            $"Native SDL2_net bytes/channel/ownership differ: {error.GetAwaiter().GetResult()}");
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
