using System.Diagnostics;
using System.Text;
using OpenTyrian.Core;

namespace OpenTyrian.Core.Tests;

internal static class LevelHudTests
{
    public static void Run(string? executable = null)
    {
        TestInvalidWeaponBars();
        var trace = new StringBuilder();
        var itemDefinitions = new ItemDefinitions();
        itemDefinitions.ParseBundle(Encoding.Latin1.GetBytes(ItemDatabaseTests.Fixture(0)), "level-hud.tyrr");
        var itemDatabase = ItemDatabase.FromDefinitions(itemDefinitions);
        for (var scenario = 0; scenario < 4096; scenario++)
        {
            var mode = scenario % 1024;
            var game = new LegacyGameState
            {
                GalagaMode = Bit(mode, 1),
                TwoPlayerMode = Bit(mode, 2),
                OnePlayerAction = Bit(mode, 3),
            };
            for (var playerIndex = 0; playerIndex < 2; playerIndex++)
            {
                var player = game.Players[playerIndex];
                player.IsAlive = Bit(mode, playerIndex == 0 ? 4 : 5);
                player.Shield = (uint)((scenario * 7 + playerIndex * 11) % 28);
                player.ShieldMax = (uint)(20 + scenario % 13);
                player.Armor = (uint)(Bit(mode, 6) ? 1 : 0);
                player.Cash = (ulong)(scenario * 31 + playerIndex);
                player.Items.Weapons[0].Power = (byte)((scenario + playerIndex) % 5);
                player.Items.Weapons[1].Power = (byte)((scenario * 3 + playerIndex) % 5);
            }
            if (Bit(mode, 7))
                game.Players[1].Armor = 0;
            game.Players[0].Items.Weapons[0].Power = (byte)(scenario % 12);
            game.Players[0].Items.Weapons[1].Power = (byte)(scenario / 3 % 12);
            var effects = new SceneEffects(new byte[8]);
            effects.SoundQueue[6] = 21;
            effects.SoundQueue[7] = 22;
            var combat = new PlayerCombat(game.Players, effects, new IndexedSurface(320, 200, 320, TextWriter.Null))
            {
                TwoPlayers = game.TwoPlayerMode,
                Galaga = game.GalagaMode,
                ShieldWait = (byte)(scenario % 5 == 0 ? 1 : scenario % 5 == 1 ? 0 : scenario % 16),
            };
            var enemies = new EnemySystem(itemDatabase, TextWriter.Null)
            {
                GalagaLife = scenario % 3 == 0 ? 10000 : scenario * 13,
                EnemiesOnScreen = (ushort)(scenario % 19 + 1),
            };
            var state = new LevelHudState
            {
                EndLevel = Bit(mode, 0),
                TextErase = (ushort)(scenario % 4),
                ShieldRechargeCost = (byte)(scenario % 7),
                Power = (uint)(scenario * 11 % 1100),
                PowerAdd = (uint)(scenario * 3 % 50),
                LastPower = (uint)(scenario * 7 % 100),
                MapX3Offset = (ushort)(scenario * 5),
                OldMapX3Offset = 999,
            };
            var oldBars = new uint[] { (uint)(scenario % 15), (uint)(scenario % 12) };
            var host = new TraceHost(trace);
            trace.AppendLine($"case:{scenario},{mode}");
            AppendState(trace, game, enemies, combat, effects, state, oldBars);
            new LevelHud(game, enemies, combat, effects, state, host).Draw(oldBars);
            AppendState(trace, game, enemies, combat, effects, state, oldBars);
        }
        if (executable is null)
            return;

        using var process = Process.Start(new ProcessStartInfo(executable)
        { RedirectStandardOutput = true, RedirectStandardError = true })
            ?? throw new InvalidOperationException("Cannot start level-HUD native reference.");
        var output = process.StandardOutput.ReadToEndAsync();
        var errors = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(30000))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException("Level-HUD native reference timed out.");
        }

        var expected = output.GetAwaiter().GetResult().Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var actual = trace.ToString().Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        if (process.ExitCode != 0 || !expected.SequenceEqual(actual))
        {
            var index = Enumerable.Range(0, Math.Min(expected.Length, actual.Length))
                .FirstOrDefault(i => expected[i] != actual[i], -1);
            var testCase = expected.Take(index + 1).LastOrDefault(line => line.StartsWith("case:", StringComparison.Ordinal));
            throw new InvalidOperationException($"Level HUD differs at {index}: " +
                $"{expected.ElementAtOrDefault(index)} / {actual.ElementAtOrDefault(index)}; " +
                $"case {testCase}; {errors.GetAwaiter().GetResult()}");
        }
    }

    private static void AppendState(StringBuilder trace, LegacyGameState game, EnemySystem enemies,
        PlayerCombat combat, SceneEffects effects, LevelHudState state, ReadOnlySpan<uint> oldBars)
    {
        trace.AppendLine($"state:{Bool(game.TwoPlayerMode)},{state.TextErase},{combat.ShieldWait}," +
            $"{game.Players[0].Shield},{game.Players[1].Shield},{game.Players[0].Lives},{game.Players[1].Lives}," +
            $"{game.Players[0].Cash},{enemies.GalagaLife},{effects.SoundQueue[6]},{effects.SoundQueue[7]}," +
            $"{oldBars[0]},{oldBars[1]},{state.Power},{state.LastPower},{state.OldMapX3Offset},{enemies.EnemiesOnScreen}");
    }

    private static bool Bit(int value, int bit) => (value & (1 << bit)) != 0;
    private static string Bool(bool value) => value ? "1" : "0";

    private static void TestInvalidWeaponBars()
    {
        var game = new LegacyGameState();
        var effects = new SceneEffects(new byte[8]);
        var combat = new PlayerCombat(game.Players, effects, new IndexedSurface(320, 200, 320, TextWriter.Null));
        var definitions = new ItemDefinitions();
        definitions.ParseBundle(Encoding.Latin1.GetBytes(ItemDatabaseTests.Fixture(0)), "level-hud.tyrr");
        var enemies = new EnemySystem(ItemDatabase.FromDefinitions(definitions), TextWriter.Null);
        var hud = new LevelHud(game, enemies, combat, effects, new LevelHudState(), new TraceHost(new()));
        try
        {
            hud.Draw([]);
            throw new InvalidOperationException("Level HUD accepted a short weapon-bar state.");
        }
        catch (ArgumentException) { }
    }

    private sealed class TraceHost(StringBuilder trace) : ILevelHudHost
    {
        public void UseHudSurface() { }
        public void EraseMessageBar() => trace.AppendLine("message");
        public void FillRectangle(int x1, int y1, int x2, int y2, byte color)
            => trace.AppendLine($"fill:{x1},{y1},{x2},{y2},{color}");
        public void Rectangle(int x1, int y1, int x2, int y2, byte color)
            => trace.AppendLine($"rectangle:{x1},{y1},{x2},{y2},{color}");
        public void DrawShield() => trace.AppendLine("shield");
    }
}
