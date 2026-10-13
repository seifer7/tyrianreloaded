using System.Buffers.Binary;
using System.Diagnostics;
using System.Text;
using OpenTyrian.Core;

namespace OpenTyrian.Core.Tests;

internal static class NetworkStartScreenTests
{
    public static void Run(string? executable = null)
    {
        var items = Items(); var text = new HelpTextData(); text.NetworkText[3] = "Peer chooses episode/difficulty."u8.ToArray();
        sbyte[] difficulties = [-128, -1, 0, 1, 2, 9, 127]; var trace = new StringBuilder();
        for (var mode = 0; mode < 112; mode++)
        {
            var game = Game(difficulties[mode / 2 % 7]); game.TwoPlayerMode = (mode & 16) != 0; game.GameLoaded = (mode & 32) != 0;
            var input = new InputState(new Platform(trace), new()); var channel = new Channel(mode, trace);
            var host = new Host(mode, game, trace);
            var screen = new NetworkStartScreen(game, items, text, input, host, channel);
            trace.AppendLine($"case:{mode}"); var terminated = false;
            try { screen.Run(mode % 2 + 1); }
            catch (NetworkTerminationException exception) when (exception.Code == 0 && exception.AttemptSync) { terminated = true; }
            trace.AppendLine($"state:{Bit(terminated)},{game.Episode},{game.Difficulty},{game.InitialDifficulty},{Bit(game.TwoPlayerMode)}," +
                $"{Bit(game.GameLoaded)},{game.Players[0].Cash},{game.Players[1].Cash},{game.Players[0].Items.Ship},{game.Players[1].Items.Ship}");
        }
        if (executable != null)
        {
            using var process = Process.Start(new ProcessStartInfo(executable, items.KnownIndex(KnownItem.ShipSilverShip).ToString(
                System.Globalization.CultureInfo.InvariantCulture))
            { RedirectStandardOutput = true, RedirectStandardError = true })
                ?? throw new InvalidOperationException("Cannot start network-start oracle.");
            var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(30000)) { process.Kill(entireProcessTree: true); throw new TimeoutException("Network-start oracle timed out."); }
            var expected = output.GetAwaiter().GetResult().Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
            var actual = trace.ToString().Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
            if (process.ExitCode != 0 || !expected.SequenceEqual(actual))
            {
                var index = Enumerable.Range(0, Math.Min(expected.Length, actual.Length)).FirstOrDefault(i => expected[i] != actual[i], -1);
                throw new InvalidOperationException($"Network start differs at {index}: {expected.ElementAtOrDefault(index)} / " +
                    $"{actual.ElementAtOrDefault(index)}; {error.GetAwaiter().GetResult()}");
            }
        }
        TestHost();
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        var canceled = new NetworkStartScreen(Game(2), items, text, new(new Platform(new()), new()), new Host(6, Game(2), new()), new Channel(6, new()));
        try { canceled.Run(1, cancellation.Token); throw new InvalidOperationException("Network start ignored cancellation."); }
        catch (OperationCanceledException) { }
        try { canceled.Run(3); throw new InvalidOperationException("Network start accepted invalid player."); }
        catch (ArgumentOutOfRangeException) { }
    }

    private static ItemDatabase Items()
    {
        var definitions = new ItemDefinitions(); definitions.ParseBundle(Encoding.Latin1.GetBytes(ItemDatabaseTests.Fixture(0)), "network-start.tyrr");
        return ItemDatabase.FromDefinitions(definitions);
    }
    private static LegacyGameState Game(sbyte difficulty)
    {
        var game = new LegacyGameState { Difficulty = difficulty, InitialDifficulty = -5, Episode = 3 };
        game.Players[0].Cash = 123; game.Players[1].Cash = 456; game.Players[0].Items.Ship = 7; game.Players[1].Items.Ship = 8;
        return game;
    }
    private static void TestHost()
    {
        var primary = new IndexedSurface(320, 200, 327, TextWriter.Null); var background = new IndexedSurface(320, 200, 327, TextWriter.Null);
        var expected = new IndexedSurface(320, 200, 327, TextWriter.Null);
        var font = new FontRenderer(HighScoreScreenTests.CreateShapes()); var input = new InputState(new TextGlowTests.Platform(), new());
        var timer = new DelayTimer(input); var palette = new PaletteSystem(c => (uint)(c.Red << 16 | c.Green << 8 | c.Blue));
        var colors = Enumerable.Range(0, 256).Select(i => new PaletteColor((byte)i, (byte)i, (byte)i)).ToArray();
        var calls = new List<string>(); var presents = 0;
        var host = new NetworkStartHost(primary, background, font, palette, colors, timer,
            () => { primary.Pixels.Span.Fill(0x75); calls.Add("picture"); }, () => presents++, () => calls.Add("connect"),
            () => { calls.Add("episode"); return true; }, () => { calls.Add("difficulty"); return false; }, episode => { calls.Add($"initialize{episode}"); return episode; });
        host.LoadPicture(); host.Capture(); host.Message("Waiting"u8.ToArray());
        expected.Pixels.Span.Fill(0x75); font.DrawString(expected, font.Center("Waiting"u8, Font.Small), 140, "Waiting"u8, Font.Small);
        if (!primary.Pixels.Span.SequenceEqual(expected.Pixels.Span) || background.Pixels.Span.ContainsAnyExcept((byte)0x75))
            throw new InvalidOperationException("Network-start message/capture/padded rendering differs.");
        host.Restore(); if (primary.Pixels.Span.ContainsAnyExcept((byte)0x75)) throw new InvalidOperationException("Network-start restore differs.");
        host.Present(); host.FadeIn(); host.Connect();
        if (!host.SelectEpisode() || host.SelectDifficulty() || host.InitializeEpisode(4) != 4 ||
            !calls.SequenceEqual(["picture", "connect", "episode", "difficulty", "initialize4"]) ||
            presents != 11 || !palette.Active.Span.SequenceEqual(colors))
            throw new InvalidOperationException("Network-start concrete palette/selection/connection services differ.");
        host.FadeBlack();
        if (presents != 21 || palette.Active.Span.ToArray().Any(color => color != default))
            throw new InvalidOperationException("Network-start concrete black fade differs.");
    }

    internal static void RunSdlNetwork(SdlNetTransport first, SdlNetTransport second)
    {
        var items = Items(); var games = new[] { Game(2), Game(8) };
        var clocks = new[] { new Platform(new(), true), new Platform(new(), true) };
        var inputs = clocks.Select(clock => new InputState(clock, new())).ToArray();
        var sessions = new[] { new NetworkSession(first, inputs[0], 2, TextWriter.Null), new NetworkSession(second, inputs[1], 2, TextWriter.Null) };
        bool[][] episodes = [[true, true, true, false, false], [true, true, false, true, false]];
        var text = new HelpTextData(); text.NetworkText[3] = "Peer chooses episode/difficulty."u8.ToArray();
        using var cancellation = new CancellationTokenSource(10000);
        var selects = new int[2]; var initialized = 0; var presentations = new int[2];
        var screens = Enumerable.Range(0, 2).Select(i =>
        {
            var surface = new IndexedSurface(320, 200, 327, TextWriter.Null);
            var saved = new IndexedSurface(320, 200, 327, TextWriter.Null);
            var font = new FontRenderer(HighScoreScreenTests.CreateShapes()); var timer = new DelayTimer(inputs[i]);
            var palette = new PaletteSystem(c => (uint)(c.Red << 16 | c.Green << 8 | c.Blue));
            var colors = Enumerable.Range(0, 256).Select(n => new PaletteColor((byte)n, (byte)n, (byte)n)).ToArray();
            var host = new NetworkStartHost(surface, saved, font, palette, colors, timer,
                () => surface.Pixels.Span.Fill(0x75), () => Interlocked.Increment(ref presentations[i]),
                () => sessions[i].Connect(episodes[i], (ushort)(i + 1), i == 0 ? "Alpha"u8 : "Beta"u8, () => { }, cancellation.Token),
                () => { selects[i]++; games[i].Episode = 2; return true; }, () => { selects[i]++; games[i].Difficulty = 2; return true; },
                episode => { Interlocked.Increment(ref initialized); return episode; });
            return new NetworkStartScreen(games[i], items, text, inputs[i], host, new NetworkStartChannel(sessions[i]));
        }).ToArray();
        Task.WhenAll(Enumerable.Range(0, 2).Select(i => Task.Run(() => screens[i].Run(i + 1, cancellation.Token)))).GetAwaiter().GetResult();
        if (games.Any(game => !game.TwoPlayerMode || game.Episode != 2 || game.Difficulty != 3 || game.InitialDifficulty != 2 ||
                game.Players.Any(player => player.Cash != 0) || game.Players[0].Items.Ship != items.KnownIndex(KnownItem.ShipSilverShip)) ||
            sessions.Any(session => !session.Connected || !session.IsSynchronized) || !selects.SequenceEqual([2, 0]) || initialized != 1 ||
            presentations.Any(count => count < 21) || !episodes[0].SequenceEqual(episodes[1]) || !episodes[0].SequenceEqual([true, true, false, false, false]))
            throw new InvalidOperationException("Actual SDL2_net connect/details/start did not synchronize game and episode state.");
        while (first.Receive() != null) { }
        while (second.Receive() != null) { }
        for (var cancelAt = 0; cancelAt < 2; cancelAt++)
        {
            var failedGames = new[] { Game(2), Game(8) };
            var failedInputs = Enumerable.Range(0, 2).Select(_ => new InputState(new Platform(new(), true), new())).ToArray();
            var failedSessions = new[] { new NetworkSession(first, failedInputs[0], 2, TextWriter.Null),
                new NetworkSession(second, failedInputs[1], 2, TextWriter.Null) };
            bool[][] failedEpisodes = [[true, true, false, false, false], [true, true, false, false, false]];
            var calls = new int[2];
            var tasks = Enumerable.Range(0, 2).Select(i => Task.Run(() =>
            {
                var surface = new IndexedSurface(320, 200, 320, TextWriter.Null);
                var palette = new PaletteSystem(c => (uint)(c.Red << 16 | c.Green << 8 | c.Blue));
                var host = new NetworkStartHost(surface, new(320, 200, 320, TextWriter.Null),
                    new(HighScoreScreenTests.CreateShapes()), palette, new PaletteColor[256], new(failedInputs[i]),
                    () => { }, () => { },
                    () => failedSessions[i].Connect(failedEpisodes[i], (ushort)(i + 1), "Peer"u8, () => { }, cancellation.Token),
                    () => { calls[0]++; return cancelAt != 0; }, () => { calls[1]++; return false; },
                    _ => throw new InvalidOperationException("Canceled network selection initialized the peer episode."));
                try
                {
                    new NetworkStartScreen(failedGames[i], items, text, failedInputs[i], host, new NetworkStartChannel(failedSessions[i]))
                        .Run(i + 1, cancellation.Token);
                    throw new InvalidOperationException("Canceled network start returned successfully.");
                }
                catch (NetworkTerminationException exception) { return (exception.Code, exception.AttemptSync); }
            })).ToArray();
            var results = Task.WhenAll(tasks).GetAwaiter().GetResult();
            if (results[0] != (0, true) || results[1] != (1, true) || calls[0] != 1 || calls[1] != (cancelAt == 0 ? 0 : 1) ||
                failedGames.Any(game => !game.TwoPlayerMode || game.Players[0].Cash != 123 || game.Players[1].Cash != 456 ||
                    game.Players[0].Items.Ship != 7 || game.InitialDifficulty != -5))
                throw new InvalidOperationException("Actual SDL2_net canceled episode/difficulty selection failed to propagate quit without starting a game.");
            while (first.Receive() != null) { }
            while (second.Receive() != null) { }
        }
    }

    private static int Bit(bool value) => value ? 1 : 0;
    private sealed class Host(int mode, LegacyGameState game, StringBuilder trace) : INetworkStartHost
    {
        public void LoadPicture() => trace.AppendLine("picture:2");
        public void Capture() { }
        public void Restore() { }
        public void Message(ReadOnlyMemory<byte> text) => trace.AppendLine($"message:{160 - text.Length},140,{Encoding.Latin1.GetString(text.Span)},2");
        public void Present() => trace.AppendLine("present");
        public void FadeIn() => trace.AppendLine("fadein:10,0,255");
        public void FadeBlack() => trace.AppendLine("fade:10");
        public void Connect() => trace.AppendLine("connect");
        public bool SelectEpisode() { trace.AppendLine("episode"); game.Episode = (byte)(mode % 4 + 1); return (mode & 2) != 0; }
        public bool SelectDifficulty() { trace.AppendLine("difficulty"); return (mode & 4) != 0; }
        public int InitializeEpisode(int episode) { trace.AppendLine($"initialize:{episode}"); return episode; }
    }
    private sealed class Channel(int mode, StringBuilder trace) : INetworkStartChannel
    {
        private int _checks, _acknowledgments, _stage;
        public NetworkPacket? Packet { get; private set; }
        public bool Synchronized => _acknowledgments >= mode % 3 + 1;
        public void SendDetails(ushort episode, ushort difficulty) { trace.AppendLine("prepare:17"); trace.AppendLine($"send:8,{episode},{difficulty}"); _stage = 1; }
        public void SendQuit() { trace.AppendLine("prepare:32"); trace.AppendLine("send:4"); _stage = 1; }
        public void Update() { trace.AppendLine("update"); if (Packet != null) { Packet = null; _stage = 1; } }
        public void Check()
        {
            trace.AppendLine("check");
            if (_stage == 0)
            {
                if (++_checks != 3) return;
                ushort[] difficulties = [0, 1, 9, 127, 128, 255, 65535]; var bytes = new byte[8];
                BinaryPrimitives.WriteUInt16BigEndian(bytes, 17);
                BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(4), (ushort)(mode % 4 + 1));
                BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(6), difficulties[mode / 2 % 7]);
                Packet = new(bytes);
            }
            else _acknowledgments++;
        }
        public void Quit() { trace.AppendLine("quit:0,1"); throw new NetworkTerminationException(0, true); }
    }
    private sealed class Platform(StringBuilder trace, bool realTime = false) : IInputPlatform
    {
        private readonly long _start = Environment.TickCount64;
        private uint _ticks;
        public uint Ticks => realTime ? unchecked((uint)(Environment.TickCount64 - _start)) : _ticks;
        public bool JoystickDown => false;
        public bool IsNetworkGame => false;
        public bool PollEvent(out InputEvent input) { trace.AppendLine("events"); input = default; return false; }
        public void Delay(uint milliseconds) { trace.AppendLine($"delay:{milliseconds}"); if (realTime) Thread.Sleep((int)milliseconds); else _ticks += milliseconds; }
        public void PushJoysticksAsKeyboard() { }
        public void SetRelativeMouseMode(bool enabled) { }
        public void SetCursorVisible(bool visible) { }
        public void EnableSystemMouseScaling() { }
        public void ToggleFullscreen() { }
        public void WindowResized() { }
        public void PollJoysticks() { }
        public void CheckNetwork() { }
        public void Quit() => throw new InvalidOperationException("Unexpected network-start input quit.");
    }
}
