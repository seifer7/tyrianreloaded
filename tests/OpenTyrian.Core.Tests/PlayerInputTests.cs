using System.Diagnostics;
using System.Text;
using OpenTyrian.Core;

namespace OpenTyrian.Core.Tests;

internal static class PlayerInputTests
{
    public static void Run(string? executable = null)
    {
        var definitions = new ItemDefinitions();
        definitions.ParseBundle(Encoding.Latin1.GetBytes(LevelEventTests.Fixture()), "input.tyrr");
        var items = ItemDatabase.FromDefinitions(definitions);
        TestFailures(items);
        var trace = new StringBuilder();
        for (var mode = 0; mode < 4096; mode++)
        {
            var devices = new[] { new Device(), new Device() };
            var joysticks = devices.Select(device => new JoystickState(device)).ToArray();
            for (var j = 0; j < 2; j++) { joysticks[j].ResetAssignments(); joysticks[j].Analog = (mode & (256 << j)) != 0; }
            var platform = new Platform();
            var input = new InputState(platform, new());
            platform.Events.Enqueue(new(InputEventKind.FocusGained)); input.ServiceEvents(false);
            var updates = 0; uint ticks = 0;
            var joystickInput = new JoystickInput(joysticks, () => ticks, () => updates++,
                _ => throw new InvalidOperationException("Unexpected synthesized joystick key."));
            var players = new[] { new PlayerState(0), new PlayerState(1) };
            var effects = new SceneEffects(new byte[8]);
            var combat = new PlayerCombat(players, effects, new(320, 200, 320, TextWriter.Null));
            var level = new LevelEvents(items, new(items, TextWriter.Null), players, new(), effects);
            var shots = new PlayerShots(items.Shots, items.Weapons, players, new ShotEffects());
            var motion = new PlayerMovement(players, combat, level, shots, items) { ConstantLastX = (sbyte)(mode % 7 - 3) };
            var requests = new NetworkRequests();
            var collector = new PlayerInput(input, joystickInput, joysticks, Enumerable.Range(0, 8).ToArray(), motion, combat, level, requests)
            {
                NetworkGame = (mode & 2) != 0,
                LocalPlayer = (byte)((mode & 1024) != 0 ? 2 : 1),
                RecordDemo = (mode & 4) != 0,
                PlayDemo = (mode & 8) != 0,
                HasMouse = (mode & 16) != 0,
                ThreeMouseButtons = (mode & 32) != 0,
                ConstantPlay = (mode & 128) != 0,
                InvertVertical = (mode & 64) != 0,
                DemoKeys = (byte)(mode % 256),
                DemoWait = 65534
            };
            using var recording = new MemoryStream();
            for (var frame = 0; frame < 4; frame++)
            {
                level.EndLevel = (mode & 1) != 0; combat.LevelEnd = 0; updates = 0;
                platform.Events.Clear(); input.SetRelative(true); platform.Services = 0;
                platform.Events.Enqueue(new(InputEventKind.MouseMotion, RelativeX: mode % 101 * 7919 + frame * 13007 - 16000,
                    RelativeY: 16000 - mode % 103 * 1237 - frame * 7919));
                Array.Clear(input.KeysActive);
                for (var i = 0; i < 8; i++) input.KeysActive[i] = (byte)((mode >> (i % 12)) & 1);
                for (var i = 0; i < 3; i++) input.MousePressed[i] = ((mode >> (i + 7)) & 1) != 0;
                var buttons = Enumerable.Range(0, 4).Select(i => ((mode >> (i + 8)) & 1) != 0).ToArray();
                for (var j = 0; j < 2; j++)
                {
                    for (var i = 0; i < 2; i++) devices[j].Axes[i] = unchecked((short)(mode * 1009 + frame * 7919 + i * 2345 + j * 987));
                    for (var i = 0; i < 6; i++) devices[j].Buttons[i] = (byte)((mode + frame + i + j) % 3);
                    devices[j].HatValue = (byte)((mode * 7 + frame + j) & 15);
                }
                ticks = (uint)(frame * 137);
                var number = (byte)((mode & 2048) != 0 ? 2 : 1); var player = players[number - 1];
                player.X = 100; player.Y = 80; ushort oldX = 98, oldY = 77;
                var movement = new PlayerMovementState
                {
                    MouseX = unchecked((short)(mode * 73)),
                    MouseY = unchecked((short)(-mode * 113)),
                    AccelerationX = -3,
                    AccelerationY = 4,
                    LinkGunAngle = 0.37f
                };
                var packet = Enumerable.Repeat((byte)165, 28).ToArray();
                trace.AppendLine($"inputcase:{mode},{frame}");
                collector.Collect(player, (byte)(mode % 5), number, ref oldX, ref oldY, movement, buttons, packet,
                    () =>
                    {
                        trace.AppendLine("replay");
                        for (var i = 0; i < 4; i++) buttons[i] = ((mode >> (i + 8)) & 1) != 0;
                        return frame % 3 != 0;
                    }, recording);
                var bits = buttons.Select((value, i) => value ? 1 << i : 0).Sum();
                trace.AppendLine($"input:{player.X},{player.Y},{oldX},{oldY},{movement.MouseX},{movement.MouseY}," +
                    $"{movement.AccelerationX},{movement.AccelerationY},{Bit(movement.AnalogLinkGun)},{BitConverter.SingleToUInt32Bits(movement.LinkGunAngle)}," +
                    $"{bits},{Bit(requests.InGameMenuPressed)},{Bit(requests.PausePressed)},{Bit(level.EndLevel)},{combat.LevelEnd},{platform.Services},{updates}");
                trace.AppendLine("packet:" + Convert.ToHexString(packet));
                trace.AppendLine($"demo:{collector.DemoKeys},{collector.DemoWait}:" + Convert.ToHexString(recording.ToArray()));
            }
        }
        AppendReplayTraces(items, trace);
        AppendReplayCollection(items, trace);
        if (executable == null) return;
        using var process = Process.Start(new ProcessStartInfo(executable)
        { RedirectStandardOutput = true, RedirectStandardError = true }) ?? throw new InvalidOperationException("Cannot start player input oracle.");
        var output = process.StandardOutput.ReadToEndAsync(); var errors = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(120000)) { process.Kill(entireProcessTree: true); throw new TimeoutException("Player input oracle timed out."); }
        var actual = trace.ToString().Replace("\r\n", "\n", StringComparison.Ordinal);
        var expected = output.GetAwaiter().GetResult().Replace("\r\n", "\n", StringComparison.Ordinal);
        if (process.ExitCode != 0 || actual != expected)
        {
            var a = expected.Split('\n'); var b = actual.Split('\n');
            var index = Enumerable.Range(0, Math.Min(a.Length, b.Length)).FirstOrDefault(i => a[i] != b[i], -1);
            throw new InvalidOperationException($"Player input differs at {index}: {a.ElementAtOrDefault(index)} / {b.ElementAtOrDefault(index)}; {errors.GetAwaiter().GetResult()}");
        }
    }

    private static void AppendReplayCollection(ItemDatabase items, StringBuilder trace)
    {
        for (var mode = 0; mode < 512; mode++)
        {
            var players = new[] { new PlayerState(0), new PlayerState(1) };
            players[0].X = 100; players[0].Y = 80;
            var effects = new SceneEffects(new byte[8]);
            var combat = new PlayerCombat(players, effects, new(320, 200, 320, TextWriter.Null));
            var level = new LevelEvents(items, new(items, TextWriter.Null), players, new(), effects);
            var shots = new PlayerShots(items.Shots, items.Weapons, players, new ShotEffects());
            var collector = new PlayerInput(new(new Platform(), new()), new([], () => 0, () => { }, _ => { }), [],
                [], new(players, combat, level, shots, items), combat, level, new())
            { DemoKeys = (byte)(mode % 256), DemoWait = (ushort)(mode % 4), PlayDemo = true, HasMouse = false };
            var bytes = Enumerable.Range(0, mode % 22).Select(i => (byte)(i % 3 == 0 ? (mode + i * 37) % 256 : i % 3 == 1 ? 0 : i / 3 % 5)).ToArray();
            using var stream = new FragmentedStream(bytes);
            var buttons = new bool[4];
            for (var frame = 0; frame < 16; frame++)
            {
                Array.Clear(buttons);
                ushort x = (ushort)players[0].X, y = (ushort)players[0].Y;
                var movement = new PlayerMovementState();
                collector.Collect(players[0], 1, 1, ref x, ref y, movement, buttons, [],
                    () => collector.ReplayDemo(stream, players, buttons));
                var bits = buttons.Select((value, i) => value ? 1 << i : 0).Sum();
                trace.AppendLine($"democollect:{mode},{frame},{players[0].X},{players[0].Y},{movement.AccelerationX},{movement.AccelerationY}," +
                    $"{collector.DemoKeys},{collector.DemoWait},{bits},{Bit(level.EndLevel)},{combat.LevelEnd},{stream.Position}");
            }
        }
    }

    private static void AppendReplayTraces(ItemDatabase items, StringBuilder trace)
    {
        for (var mode = 0; mode < 512; mode++)
        {
            var players = new[] { new PlayerState(0), new PlayerState(1) };
            players[0].X = 100; players[0].Y = 80; players[1].X = 140; players[1].Y = 90;
            var effects = new SceneEffects(new byte[8]); var combat = new PlayerCombat(players, effects, new(320, 200, 320, TextWriter.Null));
            var level = new LevelEvents(items, new(items, TextWriter.Null), players, new(), effects);
            var shots = new PlayerShots(items.Shots, items.Weapons, players, new ShotEffects());
            var collector = new PlayerInput(new(new Platform(), new()), new([], () => 0, () => { }, _ => { }), [],
                [], new(players, combat, level, shots, items), combat, level, new())
            { DemoKeys = (byte)(mode % 256), DemoWait = (ushort)(mode % 4) };
            var bytes = Enumerable.Range(0, mode % 22).Select(i => (byte)(i % 3 == 0 ? (mode + i * 37) % 256 : i % 3 == 1 ? 0 : i / 3 % 5)).ToArray();
            using var stream = new MemoryStream(bytes);
            var buttons = Enumerable.Range(0, 4).Select(i => (mode & (1 << i)) != 0).ToArray();
            for (var frame = 0; frame < 16; frame++)
            {
                var result = collector.ReplayDemo(stream, players, buttons);
                var bits = buttons.Select((value, i) => value ? 1 << i : 0).Sum();
                trace.AppendLine($"demoplay:{mode},{frame},{Bit(result)},{players[0].X},{players[0].Y}," +
                    $"{collector.DemoKeys},{collector.DemoWait},{bits},{stream.Position}");
                if (players[1].X != 140 || players[1].Y != 90)
                    throw new InvalidOperationException("Demo replay changed the second player.");
            }
        }
    }

    private static void TestFailures(ItemDatabase items)
    {
        var platform = new Platform(); var input = new InputState(platform, new());
        var players = new[] { new PlayerState(0), new PlayerState(1) };
        var effects = new SceneEffects(new byte[8]); var combat = new PlayerCombat(players, effects, new(320, 200, 320, TextWriter.Null));
        var level = new LevelEvents(items, new(items, TextWriter.Null), players, new(), effects);
        var shots = new PlayerShots(items.Shots, items.Weapons, players, new ShotEffects());
        var motion = new PlayerMovement(players, combat, level, shots, items);
        var collector = new PlayerInput(input, new([], () => 0, () => { }, _ => { }), [], [], motion, combat, level, new());
        ushort x = 100, y = 80;
        Throws<ArgumentException>(() => collector.Collect(players[0], 0, 1, ref x, ref y, new(), new bool[3], []));
        collector.NetworkGame = true;
        Throws<ArgumentException>(() => collector.Collect(players[0], 0, 1, ref x, ref y, new(), new bool[4], []));
        collector.LocalPlayer = 0;
        Throws<InvalidDataException>(() => collector.Collect(players[0], 0, 1, ref x, ref y, new(), new bool[4], new byte[28]));
        collector.NetworkGame = false; collector.PlayDemo = true;
        Throws<InvalidOperationException>(() => collector.Collect(players[0], 0, 1, ref x, ref y, new(), new bool[4], []));
        collector.PlayDemo = false; collector.HasMouse = false;
        Throws<InvalidDataException>(() => collector.Collect(players[0], 1, 1, ref x, ref y, new(), new bool[4], []));
        using var replay = new MemoryStream([0, 0, 1]);
        Throws<ArgumentException>(() => collector.ReplayDemo(replay, players, new bool[3]));
        Throws<ArgumentException>(() => collector.ReplayDemo(replay, [players[0]], new bool[4]));
    }
    private sealed class FragmentedStream(byte[] bytes) : MemoryStream(bytes)
    {
        public override int Read(Span<byte> buffer) => base.Read(buffer[..Math.Min(buffer.Length, 1)]);
    }
    private static void Throws<T>(Action action) where T : Exception
    {
        try { action(); } catch (T) { return; }
        throw new InvalidOperationException($"Expected {typeof(T).Name}.");
    }
    private static int Bit(bool value) => value ? 1 : 0;
    private sealed class Device : IJoystickDevice
    {
        public string Name => "fixture";
        public int AxisCount => 2;
        public int ButtonCount => 6;
        public int HatCount => 1;
        public short[] Axes { get; } = new short[2];
        public byte[] Buttons { get; } = new byte[6];
        public byte HatValue { get; set; }
        public short Axis(int index) => Axes[index];
        public byte Button(int index) => Buttons[index];
        public byte Hat(int index) => HatValue;
    }
    private sealed class Platform : IInputPlatform
    {
        public Queue<InputEvent> Events { get; } = new();
        public int Services { get; set; }
        public bool PollEvent(out InputEvent input)
        {
            if (Events.TryDequeue(out input)) return true;
            Services++; return false;
        }
        public uint Ticks => 0;
        public bool JoystickDown => false;
        public bool IsNetworkGame => false;
        public void Delay(uint milliseconds) => throw new InvalidOperationException("Unexpected input wait.");
        public void SetCursorVisible(bool visible) { }
        public void SetRelativeMouseMode(bool enabled) { }
        public void EnableSystemMouseScaling() { }
        public void ToggleFullscreen() => throw new InvalidOperationException("Unexpected fullscreen toggle.");
        public void WindowResized() => throw new InvalidOperationException("Unexpected window resize.");
        public void Quit() => throw new InvalidOperationException("Unexpected quit.");
        public void PushJoysticksAsKeyboard() => throw new InvalidOperationException("Unexpected menu input.");
        public void PollJoysticks() => throw new InvalidOperationException("Unexpected polling order.");
        public void CheckNetwork() => throw new InvalidOperationException("Unexpected network service.");
    }
    private sealed class ShotEffects : IPlayerShotEffects
    {
        public void Draw(int sheet, int x, int y, int frame, bool darken) => throw new InvalidOperationException("Unexpected input projectile rendering.");
        public (ushort Width, ushort Height) DrawSpecial(int x, int y, int frame) => throw new InvalidOperationException("Unexpected input projectile rendering.");
        public void Explosion(int x, int y, byte type) => throw new InvalidOperationException("Unexpected input projectile movement.");
        public void SuperPixels(int x, int y, ushort count, byte width, byte color) => throw new InvalidOperationException("Unexpected input projectile movement.");
    }
}
