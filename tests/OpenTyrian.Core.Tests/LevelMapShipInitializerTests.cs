using System.Diagnostics;
using System.Text;
using OpenTyrian.Core;

namespace OpenTyrian.Core.Tests;

internal static class LevelMapShipInitializerTests
{
    public static void Run(string? executable = null)
    {
        var definitions = new ItemDefinitions();
        definitions.ParseBundle(Encoding.Latin1.GetBytes(LevelEventTests.Fixture()), "level-map-ship-init.tyrr");
        var items = ItemDatabase.FromDefinitions(definitions);
        var players = new PlayerState[] { new(0), new(1) };
        var backgrounds = new BackgroundRenderer();
        using var warnings = new StringWriter();
        var effects = new SceneEffects(new byte[8]);
        var enemies = new EnemySystem(items, warnings);
        var level = new LevelEvents(items, enemies, players, backgrounds, effects);
        var shipInfo = new PlayerShipInfo(players, items, new SpriteSheet(), new SpriteSheet(), new byte[150], new ushort[2]);
        var shots = new PlayerShots([], [], players, new NoOpShotEffects());
        var combat = new PlayerCombat(players, effects, new IndexedSurface(320, 200, 320, TextWriter.Null));
        var weapons = new PlayerWeapons(shots, items, combat);
        var movement = new PlayerMovement(players, combat, level, shots, items);
        var input = new PlayerInput(new InputState(new NoOpInputPlatform(), new VideoLayout()),
            new JoystickInput([], () => 0, () => { }, _ => { }), [], [], movement, combat, level, new NetworkRequests());
        var special = new PlayerSpecialWeapons(items, shots, weapons, combat, enemies, effects, level, backgrounds);
        var playerFrame = new PlayerFrame(players, combat, level, movement, input, weapons, special, shots, items, effects, enemies);
        var initializer = new LevelMapShipInitializer(backgrounds, level, players, shipInfo, shots, playerFrame);
        var trace = new StringBuilder();
        using var mapStream = new MemoryStream(LevelMapTests.BuildLevel(0, 'A'));
        var mapShapes = LevelMapTests.BuildShapes(0);
        var map = LevelMap.Load(mapStream, _ => new MemoryStream(mapShapes));
        map.Apply(backgrounds, level, new LevelBackgroundSceneState());

        for (var mode = 0; mode < 1024; mode++)
        {
            Seed(mode);
            initializer.Initialize();
            if (!ReferenceEquals(backgrounds.First.Tiles, map.Maps[0]) ||
                !ReferenceEquals(backgrounds.Second.Tiles, map.Maps[1]) ||
                !ReferenceEquals(backgrounds.Third.Tiles, map.Maps[2]))
                throw new InvalidOperationException("Level initialization replaced a loaded map layer.");
            trace.AppendLine($"case:{mode}");
            trace.AppendLine(FormattableString.Invariant($"state:{backgrounds.First.MapY},{backgrounds.Second.MapY},{backgrounds.Third.MapY},{backgrounds.First.MapOffset},{backgrounds.Second.MapOffset},{backgrounds.Third.MapOffset},{level.MapX},{level.MapX3},{backgrounds.First.X},{playerFrame.MapXOffset},{playerFrame.OldMapXOffset},{backgrounds.Second.X},{playerFrame.MapX2Offset},{backgrounds.Third.X},{playerFrame.MapX3Offset},{backgrounds.First.HorizontalTileOffset},{backgrounds.Second.HorizontalTileOffset},{backgrounds.Third.HorizontalTileOffset},{level.FirstDelay},{level.FirstDelayMax},{backgrounds.SecondDelay},{backgrounds.SecondDelayMax},{(level.MusicFade ? 1 : 0)},{backgrounds.First.Position},{backgrounds.Second.Position},{backgrounds.Third.Position},{shots.Power},{level.StarfieldSpeed}"));
            foreach (var player in players)
                trace.AppendLine(FormattableString.Invariant(
                    $"player:{player.XVelocity},{player.YVelocity},{player.InvulnerableTicks}"));
        }

        level.MapX = map.Header.FirstX;
        level.MapX3 = map.Header.ThirdX;
        initializer.Initialize();
        if (level.MapX != map.Header.FirstX || level.MapX3 != map.Header.ThirdX)
            throw new InvalidOperationException("Level initialization changed loaded map X indices.");

        if (executable is null) return;
        var start = new ProcessStartInfo(executable) { RedirectStandardOutput = true, RedirectStandardError = true };
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Cannot start level-map/ship initializer reference.");
        var output = process.StandardOutput.ReadToEndAsync();
        var errors = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(30000))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException("Level-map/ship initializer reference timed out.");
        }
        var expected = output.GetAwaiter().GetResult().Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var actual = trace.ToString().Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        if (process.ExitCode != 0 || !expected.SequenceEqual(actual))
        {
            var index = Enumerable.Range(0, Math.Min(expected.Length, actual.Length))
                .FirstOrDefault(i => expected[i] != actual[i], -1);
            throw new InvalidOperationException($"Level-map/ship initialization differs at {index}: " +
                $"{expected.ElementAtOrDefault(index)} / {actual.ElementAtOrDefault(index)}; {errors.GetAwaiter().GetResult()}");
        }

        void Seed(int mode)
        {
            level.MapX = unchecked((ushort)(mode * 97));
            level.MapX3 = unchecked((ushort)(mode * 31));
            level.MusicFade = (mode & 1) != 0;
            backgrounds.First.MapY = backgrounds.Second.MapY = backgrounds.Third.MapY = (ushort)(mode + 10);
            backgrounds.First.MapOffset = backgrounds.Second.MapOffset = backgrounds.Third.MapOffset = mode - 30000;
            backgrounds.First.X = backgrounds.Second.X = backgrounds.Third.X = (ushort)(mode * 41);
            backgrounds.First.HorizontalTileOffset = backgrounds.Second.HorizontalTileOffset =
                backgrounds.Third.HorizontalTileOffset = mode - 30000;
            playerFrame.MapXOffset = unchecked((ushort)(mode * 43));
            playerFrame.OldMapXOffset = unchecked((ushort)(mode * 47));
            playerFrame.MapX2Offset = unchecked((ushort)(mode * 49));
            playerFrame.MapX3Offset = unchecked((ushort)(mode * 61));
            backgrounds.First.Position = backgrounds.Second.Position = backgrounds.Third.Position = (ushort)(mode * 17);
            level.StarfieldSpeed = unchecked((short)(mode * 37));
            level.FirstDelay = level.FirstDelayMax = backgrounds.SecondDelay = backgrounds.SecondDelayMax = (byte)(mode + 2);
            shots.Power = (uint)(mode * 29);
            for (var i = 0; i < players.Length; i++)
            {
                players[i].XVelocity = mode * 73 + i - 900;
                players[i].YVelocity = mode * 79 + i - 1000;
                players[i].InvulnerableTicks = (uint)(mode * 83 + i);
            }
        }
    }

    private sealed class NoOpShotEffects : IPlayerShotEffects
    {
        public void Draw(int sheet, int x, int y, int frame, bool darken) { }
        public (ushort Width, ushort Height) DrawSpecial(int x, int y, int frame) => (0, 0);
        public void Explosion(int x, int y, byte type) { }
        public void SuperPixels(int x, int y, ushort count, byte width, byte color) { }
    }

    private sealed class NoOpInputPlatform : IInputPlatform
    {
        public bool PollEvent(out InputEvent input) { input = default; return false; }
        public uint Ticks => 0;
        public void Delay(uint milliseconds) { }
        public void SetCursorVisible(bool visible) { }
        public void SetRelativeMouseMode(bool enabled) { }
        public void EnableSystemMouseScaling() { }
        public void ToggleFullscreen() { }
        public void WindowResized() { }
        public void Quit() { }
        public void PushJoysticksAsKeyboard() { }
        public void PollJoysticks() { }
        public bool JoystickDown => false;
        public bool IsNetworkGame => false;
        public void CheckNetwork() { }
    }
}
