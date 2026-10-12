using System.Buffers.Binary;
using System.Diagnostics;
using System.Text;
using OpenTyrian.Core;

namespace OpenTyrian.Core.Tests;

internal static class LevelNetworkTests
{
    public static void Run(string? executable = null)
    {
        var definitions = new ItemDefinitions();
        definitions.ParseBundle(Encoding.Latin1.GetBytes(LevelEventTests.Fixture()), "network-level.tyrr");
        var items = ItemDatabase.FromDefinitions(definitions);
        var trace = new StringBuilder();
        var players = new[] { new PlayerState(0), new PlayerState(1) };
        var effects = new SceneEffects(new byte[8]);
        var screen = new IndexedSurface(320, 200, 320, TextWriter.Null);
        var combat = new PlayerCombat(players, effects, screen);
        var enemies = new EnemySystem(items, TextWriter.Null) { Difficulty = -3 };
        var events = new LevelEvents(items, enemies, players, new(), effects) { Location = 54321 };
        for (var mode = 0; mode < 5120; mode++)
        {
            var transport = new Transport(mode, trace);
            var session = new NetworkSession(transport, new(new Platform(), new()), 1, TextWriter.Null);
            var local = mode / 16 % 16;
            var requests = session.Requests;
            requests.Pause = (local & 1) != 0; requests.InGameMenu = (local & 2) != 0;
            requests.SkipLevel = (local & 4) != 0; requests.NortShip = (local & 8) != 0; requests.Help = true;
            players[0].Items = new(); players[1].Items = new();
            players[0].X = -7; players[1].X = 65537; players[0].Y = 456; players[1].Y = -123;
            events.ReallyEndLevel = mode / 2560 != 0; events.EndLevel = false; events.TimerCountdown = 123;
            combat.LevelTimer = false; combat.LevelEnd = 17;
            ushort ship = 99;
            session.PrepareState();
            trace.AppendLine($"mode:{mode}");
            if ((mode & 128) != 0)
            {
                // A reset delay of two frames exercises the native no-state-yet branch.
                session = new NetworkSession(transport, new(new Platform(), new()), 2, TextWriter.Null);
                CopyRequests(requests, session.Requests); requests = session.Requests; session.PrepareState();
            }
            events.UpdateNetworkState(mode / 1280 % 2 == 0, session, combat, new Services(mode, trace), ref ship);
            trace.AppendLine($"state:{Bit(events.ReallyEndLevel)},{Bit(combat.LevelTimer)},{events.TimerCountdown},{combat.LevelEnd},{Bit(events.EndLevel)},{ship}," +
                $"{players[0].Items.Ship},{players[0].Items.Special},{players[0].Items.Weapons[0].Id},{players[0].Items.Weapons[1].Id}");
            trace.AppendLine($"requests:{Bit(requests.Pause)},{Bit(requests.InGameMenu)},{Bit(requests.SkipLevel)},{Bit(requests.Help)},{Bit(requests.NortShip)},{Bit(requests.YourInGameMenu)}");
        }
        if (executable != null)
        {
            var start = new ProcessStartInfo(executable) { RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var item in new[] { KnownItem.ShipNortship, KnownItem.SpecialAstralZone,
                KnownItem.WeaponFrontNortshipSuperPulse, KnownItem.WeaponRearNortshipSpreader })
                start.ArgumentList.Add(items.KnownIndex(item).ToString(System.Globalization.CultureInfo.InvariantCulture));
            using var process = Process.Start(start) ?? throw new InvalidOperationException("Cannot start gameplay network oracle.");
            var output = process.StandardOutput.ReadToEndAsync(); var errors = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(30000)) { process.Kill(entireProcessTree: true); throw new TimeoutException("Gameplay network oracle timed out."); }
            var expected = output.GetAwaiter().GetResult().Replace("\r\n", "\n", StringComparison.Ordinal);
            var actual = trace.ToString().Replace("\r\n", "\n", StringComparison.Ordinal);
            if (process.ExitCode != 0 || expected != actual)
            {
                var a = expected.Split('\n'); var b = actual.Split('\n');
                var index = Enumerable.Range(0, Math.Min(a.Length, b.Length)).FirstOrDefault(i => a[i] != b[i], -1);
                throw new InvalidOperationException($"Gameplay network parity differs at {index}: {a.ElementAtOrDefault(index)} / {b.ElementAtOrDefault(index)}; {errors.GetAwaiter().GetResult()}");
            }
        }
        Failures(combat, events);
    }

    internal static void RunSdl(SdlNetTransport first, SdlNetTransport second)
    {
        var definitions = new ItemDefinitions();
        definitions.ParseBundle(Encoding.Latin1.GetBytes(LevelEventTests.Fixture()), "network-sdl-level.tyrr");
        var items = ItemDatabase.FromDefinitions(definitions);
        var sessions = new[]
        {
            new NetworkSession(first, new(new Platform(realTime: true), new()), 2, TextWriter.Null),
            new NetworkSession(second, new(new Platform(realTime: true), new()), 2, TextWriter.Null)
        };
        var players = Enumerable.Range(0, 2).Select(_ => new[] { new PlayerState(0) { X = 10, Y = 20 },
            new PlayerState(1) { X = 30, Y = 40 } }).ToArray();
        var effects = new[] { new SceneEffects(new byte[8]), new SceneEffects(new byte[8]) };
        var events = Enumerable.Range(0, 2).Select(i => new LevelEvents(items, new(items, TextWriter.Null),
            players[i], new(), effects[i])).ToArray();
        var combats = Enumerable.Range(0, 2).Select(i => new PlayerCombat(players[i], effects[i],
            new(320, 200, 320, TextWriter.Null))).ToArray();
        var services = new[] { new Services(0, new()), new Services(0, new()) };
        ushort[] graphics = [99, 99];
        using var cancellation = new CancellationTokenSource(5000);
        for (var frame = 0; frame < 12; frame++)
        {
            for (var i = 0; i < 2; i++)
            {
                events[i].Location = (ushort)(frame * 10);
                sessions[i].PrepareState();
                sessions[i].Requests.Pause = i == 0 && frame == 0;
                sessions[i].Requests.NortShip = i == 1 && frame == 3;
            }
            var firstPeer = Task.Run(() => events[0].UpdateNetworkState(true, sessions[0], combats[0], services[0],
                ref graphics[0], cancellation.Token));
            var secondPeer = Task.Run(() => events[1].UpdateNetworkState(true, sessions[1], combats[1], services[1],
                ref graphics[1], cancellation.Token));
            Task.WhenAll(firstPeer, secondPeer).GetAwaiter().GetResult();
        }
        for (var i = 0; i < 2; i++)
        {
            if (services[i].Pauses != 1 || services[i].Diagnostics != 0 || graphics[i] != 1 ||
                players[i][0].Items.Ship != items.KnownIndex(KnownItem.ShipNortship) || sessions[i].Requests.Pause)
                throw new InvalidOperationException("Actual SDL2_net gameplay did not apply identical delayed requests to both players.");
        }
        while (first.Receive() != null) { }
        while (second.Receive() != null) { }
    }

    private static void Failures(PlayerCombat combat, LevelEvents events)
    {
        var trace = new StringBuilder();
        var transport = new Transport(15, trace) { BadLocation = true };
        var session = new NetworkSession(transport, new(new Platform(), new()), 1, TextWriter.Null);
        events.ReallyEndLevel = false; session.PrepareState(); ushort ship = 99;
        try { events.UpdateNetworkState(true, session, combat, new Services(0, trace), ref ship); }
        catch (InvalidDataException)
        {
            if (ship != 99 || trace.ToString().Contains("pause", StringComparison.Ordinal))
                throw new InvalidOperationException("Desynchronized location applied gameplay requests.");
            return;
        }
        throw new InvalidOperationException("Desynchronized location was accepted.");
    }

    private static void CopyRequests(NetworkRequests source, NetworkRequests target)
    {
        target.Pause = source.Pause; target.InGameMenu = source.InGameMenu;
        target.SkipLevel = source.SkipLevel; target.Help = source.Help; target.NortShip = source.NortShip;
    }
    private static int Bit(bool value) => value ? 1 : 0;

    private sealed class Transport(int mode, StringBuilder trace) : INetworkTransport
    {
        private NetworkPacket? _incoming;
        public bool BadLocation;
        public void Send(ReadOnlySpan<byte> packet)
        {
            trace.AppendLine($"send:{Convert.ToHexString(packet)}");
            var data = packet.ToArray();
            BinaryPrimitives.WriteUInt16BigEndian(data.AsSpan(14), (ushort)(mode % 16));
            var offset = mode / 256 % 5;
            if (offset < 4)
            {
                var position = 18 + offset * 2;
                BinaryPrimitives.WriteUInt16BigEndian(data.AsSpan(position),
                    unchecked((ushort)(BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(position)) + 1)));
            }
            if (BadLocation) BinaryPrimitives.WriteUInt16BigEndian(data.AsSpan(26), 1);
            _incoming = new(data);
        }
        public NetworkPacket? Receive() { var packet = _incoming; _incoming = null; return packet; }
    }
    private sealed class Services(int mode, StringBuilder trace) : ILevelNetworkServices
    {
        public int Pauses, Diagnostics;
        public void Pause() { Pauses++; trace.AppendLine("pause"); }
        public bool ShowInGameSetup(bool yourRequest) { trace.AppendLine($"menu:{Bit(yourRequest)}"); return (mode & 64) != 0; }
        public void DrawDesynchronization(int playerNumber)
        {
            Diagnostics++;
            trace.AppendLine($"text:40,{110 + (playerNumber - 1) * 10},Player {playerNumber} is unsynchronized!,9,2,1");
        }
    }
    private sealed class Platform(bool realTime = false) : IInputPlatform
    {
        public uint Ticks => realTime ? unchecked((uint)Environment.TickCount) : 0;
        public bool JoystickDown => false;
        public bool IsNetworkGame => false;
        public bool PollEvent(out InputEvent input) { input = default; return false; }
        public void Delay(uint milliseconds)
        {
            if (!realTime) throw new InvalidOperationException("Immediate gameplay loopback should not wait.");
            Thread.Sleep(checked((int)milliseconds));
        }
        public void PushJoysticksAsKeyboard() { }
        public void PollJoysticks() { }
        public void CheckNetwork() { }
        public void SetRelativeMouseMode(bool enabled) { }
        public void SetCursorVisible(bool visible) { }
        public void EnableSystemMouseScaling() { }
        public void ToggleFullscreen() { }
        public void WindowResized() { }
        public void Quit() => throw new InvalidOperationException("Unexpected gameplay network quit.");
    }
}
