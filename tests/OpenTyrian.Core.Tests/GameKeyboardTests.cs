using System.Diagnostics;
using System.Globalization;
using System.Text;
using OpenTyrian.Core;

namespace OpenTyrian.Core.Tests;

internal static class GameKeyboardTests
{
    public static void Run(string? executable = null)
    {
        var definitions = new ItemDefinitions();
        definitions.ParseBundle(Encoding.Latin1.GetBytes(ItemDatabaseTests.Fixture(0)), "keyboard.tyrr");
        var items = ItemDatabase.FromDefinitions(definitions);
        var trace = new StringBuilder();
        var edited = new EditedShips();
        var records = Enumerable.Range(0, 150).Select(i => (byte)(i % 3)).ToArray();
        using var archive = new MemoryStream([0, .. EditedShipTests.Encrypt(records)]);
        edited.Load(archive);
        int[] scans = [6, 19, 30, 31, 32, 38, 39, 41, 42, 43, 45, 49, 56, 57, 58, 59, 60, 61, 62, 63, 64, 65, 66, 67, 68, 69, 71, 83];
        int[][] groups = [[43, 57, 30, 33, 39], [59, 61, 63, 64, 66, 49, 56], [59, 60, 63, 6], [59, 60, 61],
            [59, 63, 64], [42, 69, 71, 31, 38, 39], [42, 45, 83, 67], [42, 30, 71]];
        for (var mode = 0; mode < 4096; mode++)
        {
            var game = new LegacyGameState
            {
                TwoPlayerMode = (mode & 4) != 0,
                SuperTyrian = (mode & 2) != 0,
                SuperArcadeMode = (byte)((mode & 8) != 0 ? 1 : 0)
            };
            for (var p = 0; p < 2; p++)
            { game.Players[p].Armor = (uint)(10 + p * 20); game.Players[p].WeaponMode = (uint)(3 + p); game.Players[p].Items.Ship = (byte)p; }
            var input = new InputState(new TextGlowTests.Platform(), new VideoLayout());
            var effects = new SceneEffects(new byte[8]);
            var combat = new PlayerCombat(game.Players, effects, new(320, 200, 327, TextWriter.Null))
            { TwoPlayers = game.TwoPlayerMode, Cheating = (mode & 32) != 0 };
            var backgrounds = new BackgroundRenderer { LevelFilter = (sbyte)(mode % 21 == 0 ? -99 : mode % 21 - 4) };
            var level = new LevelEvents(items, new(items, TextWriter.Null), game.Players, backgrounds, effects) { TimerCountdown = 123 };
            var requests = new NetworkRequests { PausePressed = (mode & 256) != 0, InGameMenuPressed = (mode & 512) != 0 };
            var settings = new GameRuntimeSettings { FastPlay = (byte)((mode & 1024) != 0 ? 255 : mode % 6) };
            var shots = new PlayerShots(items.Shots, items.Weapons, game.Players, new UnusedEffects());
            for (var i = 0; i < 11; i++) shots.MultiPosition[i] = (byte)(i + 1);
            ushort[] graphics = [10, 11];
            var host = new TraceHost(trace, mode, input, game.Players, graphics);
            var keyboard = new GameKeyboard(game, items, input, combat, level, backgrounds, requests, settings, shots,
                graphics, (mode & 128) != 0 ? edited : new EditedShips(), host)
            {
                NetworkGame = (mode & 1) != 0,
                ConstantPlay = (mode & 16) != 0,
                Debug = (mode & 64) != 0,
                SuperPause = (mode & 128) != 0
            };
            keyboard.EditedPlayer[0] = (mode & 256) != 0; keyboard.EditedPlayer[1] = (mode & 512) != 0;
            for (var i = 0; i < scans.Length; i++) input.KeysActive[scans[i]] = (byte)((mode * 37 + i * 13) % 7 == 0 ? 2 : 0);
            foreach (var scan in groups[mode / 128 % 8]) input.KeysActive[scan] = 1;
            MersenneTwister.Seed((uint)(mode + 1)); trace.AppendLine($"keyboard:{mode}");
            keyboard.Run();
            trace.AppendLine($"flags:{Bit(keyboard.EditedPlayer[0])},{Bit(keyboard.EditedPlayer[1])},{Bit(requests.Help)}," +
                $"{Bit(keyboard.SkipPresentation)},{Bit(requests.NortShip)},{Bit(combat.Cheating)},{Bit(keyboard.Debug)},{Bit(requests.SkipLevel)}," +
                $"{level.TimerCountdown},{Bit(combat.LevelTimer)},{Bit(level.EndLevel)},{Bit(requests.PausePressed)},{Bit(requests.InGameMenuPressed)}," +
                $"{Bit(keyboard.SuperPause)},{combat.LevelEnd},{backgrounds.LevelFilter},{keyboard.DebugHistory.ToString(CultureInfo.InvariantCulture)}," +
                $"{keyboard.DebugHistoryCount},{keyboard.LastDebugTime}");
            trace.AppendLine($"speed:{settings.FastPlay},{graphics[0]},{graphics[1]}");
            foreach (var p in game.Players)
                trace.AppendLine($"player:{p.Items.Ship},{p.Items.Weapons[0].Id},{p.Items.Weapons[1].Id},{p.Items.Special}," +
                    $"{p.Items.Sidekicks[0]},{p.Items.Sidekicks[1]},{p.Items.Generator},{p.Items.Shield},{p.Armor},{p.WeaponMode}");
            trace.AppendLine($"smooth,{string.Join(',', backgrounds.Smoothies.Select(Bit))}");
            trace.AppendLine($"multi,{string.Join(',', shots.MultiPosition)}");
            trace.AppendLine($"keys,{string.Join(',', input.KeysActive)}");
            trace.AppendLine($"rng:{MersenneTwister.NextUInt32()}");
        }
        TestHost();
        if (executable is null) return;
        var start = new ProcessStartInfo(executable) { RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var item in new[] { KnownItem.ShipNortship, KnownItem.SpecialAstralZone,
            KnownItem.WeaponFrontNortshipSuperPulse, KnownItem.WeaponRearNortshipSpreader })
            start.ArgumentList.Add(items.KnownIndex(item).ToString(CultureInfo.InvariantCulture));
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Cannot start game keyboard reference.");
        var output = process.StandardOutput.ReadToEndAsync(); var errors = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(30000)) { process.Kill(entireProcessTree: true); throw new TimeoutException("Game keyboard reference timed out."); }
        var expected = output.GetAwaiter().GetResult().Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var actual = trace.ToString().Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        if (process.ExitCode != 0 || !expected.SequenceEqual(actual))
        {
            var index = Enumerable.Range(0, Math.Min(expected.Length, actual.Length)).FirstOrDefault(i => expected[i] != actual[i], -1);
            throw new InvalidOperationException($"Game keyboard differs at {index}: {expected.ElementAtOrDefault(index)} / " +
                $"{actual.ElementAtOrDefault(index)}; {errors.GetAwaiter().GetResult()}");
        }
    }

    private static void TestHost()
    {
        var definitions = new ItemDefinitions();
        definitions.ParseBundle(Encoding.Latin1.GetBytes(ItemDatabaseTests.Fixture(0) +
            "[ship:0]\nshipgraphic=100\ndmg=12\nani=1\n[weapon:0]\nopnum=3\n[generator:0]\npower=20\n"), "keyboard-host.tyrr");
        var items = ItemDatabase.FromDefinitions(definitions);
        var players = new PlayerState[] { new(0), new(1) };
        var surface = new IndexedSurface(320, 200, 327, TextWriter.Null);
        var combat = new PlayerCombat(players, new SceneEffects(new byte[8]), surface) { TwoPlayers = true };
        var expected = new IndexedSurface(320, 200, 327, TextWriter.Null);
        var expectedCombat = new PlayerCombat(players, new SceneEffects(new byte[8]), expected) { TwoPlayers = true };
        var platform = new TextGlowTests.Platform(); var input = new InputState(platform, new VideoLayout());
        var palette = new PaletteSystem(c => (uint)(c.Red << 16 | c.Green << 8 | c.Blue));
        var gamma = new GameGamma(input, palette);
        var colors = Enumerable.Range(0, 256).Select(i => new PaletteColor((byte)i, (byte)i, (byte)i)).ToArray();
        var ships = new PlayerShipInfo(players, items, new SpriteSheet(), new SpriteSheet(), new byte[150], new ushort[2]);
        var helpTrace = new StringBuilder();
        var name = "";
        var window = new GameTextWindow(() => { }, text => name = Encoding.ASCII.GetString(text.Span));
        var help = new GameHelp(new GameHelpTests.TraceHost(helpTrace, 0), window);
        var settings = new GameRuntimeSettings { FastPlay = 1 }; var timer = new DelayTimer(input);
        var options = 0; var songs = new List<uint>();
        var host = new GameKeyboardHost(gamma, colors, combat, items, ships, help, "Continue"u8.ToArray(),
            window, "Cheat"u8.ToArray(), settings, timer, input, () => options++, songs.Add);
        input.KeysActive[68] = 1; input.NewKey = true; host.CheckGamma();
        if (gamma.Correction != 1 || input.NewKey || input.KeysActive[68] != 0)
            throw new InvalidOperationException("Keyboard gamma integration differs.");
        host.InitializeShips();
        if (players[0].Armor != 12 || players[1].Armor != 10 || ships.PowerAdd != 20 || host.PortConfigurations() != 3)
            throw new InvalidOperationException("Keyboard ship/combat integration differs.");
        players[0].Shield = players[1].Shield = 5;
        host.DrawBars(); expectedCombat.WipeBars(); expectedCombat.DrawArmor(); expectedCombat.DrawShield();
        if (!surface.Pixels.Span.SequenceEqual(expected.Pixels.Span))
            throw new InvalidOperationException("Keyboard HUD surface ownership differs.");
        host.DrawOptions(); host.TextWindow(); host.Help(default); host.SetSpeed(); host.PlaySong(4);
        if (options != 1 || name != "Cheat" || window.EraseTicks != 1 || !songs.SequenceEqual([4u]) ||
            settings.FrameCountMax != 2 || timer.GetDelayTicks() == 0 || host.Ticks != platform.Ticks ||
            !helpTrace.ToString().Contains("box:20,4,36,50,3,3", StringComparison.Ordinal))
            throw new InvalidOperationException("Keyboard help/text/speed/audio services differ.");
    }

    private static int Bit(bool value) => value ? 1 : 0;
    private sealed class TraceHost(StringBuilder trace, int mode, InputState input, IReadOnlyList<PlayerState> players,
        ushort[] graphics) : IGameKeyboardHost
    {
        public void CheckGamma() { trace.AppendLine("gamma"); input.KeysActive[68] = 0; }
        public ushort PortConfigurations() { trace.AppendLine("ports"); return (ushort)(mode % 4); }
        public void InitializeShips()
        { trace.AppendLine("ships"); players[0].Armor = (uint)(70 + mode % 40); players[1].Armor = (uint)(30 + mode % 50); graphics[0] = 30; graphics[1] = 31; }
        public void DrawBars() { trace.AppendLine("wipe"); trace.AppendLine("armor"); trace.AppendLine("shield"); }
        public void DrawOptions() => trace.AppendLine("options");
        public void Help(CancellationToken cancellation) => trace.AppendLine("help");
        public void TextWindow() => trace.AppendLine("text");
        public uint Ticks => (uint)(123456789 + mode);
        public void SetSpeed() => trace.AppendLine("speed");
        public void PlaySong(uint song) => trace.AppendLine($"song:{song}");
    }
    private sealed class UnusedEffects : IPlayerShotEffects
    {
        public void Draw(int sheet, int x, int y, int frame, bool darken) => throw new InvalidOperationException("Keyboard must not draw shots.");
        public (ushort Width, ushort Height) DrawSpecial(int x, int y, int frame) => throw new InvalidOperationException("Keyboard must not draw shots.");
        public void Explosion(int x, int y, byte type) => throw new InvalidOperationException("Keyboard must not create explosions.");
        public void SuperPixels(int x, int y, ushort count, byte width, byte color) => throw new InvalidOperationException("Keyboard must not create particles.");
    }
}
