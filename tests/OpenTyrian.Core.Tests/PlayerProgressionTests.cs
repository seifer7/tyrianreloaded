using System.Diagnostics;
using System.Text;
using OpenTyrian.Core;

namespace OpenTyrian.Core.Tests;

internal static class PlayerProgressionTests
{
    public static void Run(string? executable = null)
    {
        var fixture = ItemDatabaseTests.Fixture(0);
        foreach (var (category, ids) in new (string, string)[]
        {
            ("ship", "0 late 12 11 8 13 2 1"), ("weapon", "0 late 39 23 25 36 1 16 24 26 37 15"),
            ("shield", "0 late 2 4"), ("generator", "0 late 2"), ("sidekick", "0 late 24")
        })
        {
            var i = 0;
            foreach (var id in ids.Split(' ')) fixture += $"[{category}:{id}]\ncost={i++ * 37}\n" + (category == "ship" ? "dmg=23\n" : "");
        }
        var definitions = new ItemDefinitions(); definitions.ParseBundle(Encoding.Latin1.GetBytes(fixture), "progression.tyrr");
        var items = ItemDatabase.FromDefinitions(definitions);
        var trace = new StringBuilder();
        long[] baseCosts = [int.MinValue, int.MaxValue, -1, 0, 1, 65535];
        for (var i = 0; i < baseCosts.Length; i++)
            for (var power = 0u; power <= 11; power++)
                trace.AppendLine($"upgrade:{i},{power},{PlayerProgression.WeaponUpgradeCost(baseCosts[i], power)}");
        ulong[] scores = [0, 1, 9999, 10000, 19999, 20000, 39999, 40000, 49999, 50000, 69999, 70000,
            79999, 80000, 124999, 125000, 149999, 150000, 199999, 200000, 299999, 300000, 399999, 400000,
            599999, 600000, 999999, 1000000, 1999999, 2000000, 2999999, 3000000];
        for (var mode = 0; mode < 512; mode++)
        {
            var game = new LegacyGameState
            {
                TwoPlayerMode = (mode & 1) != 0,
                OnePlayerAction = true,
                SuperTyrian = true,
                SuperArcadeMode = 7,
                GameHasRepeated = true
            };
            var progression = new PlayerProgression(game, items);
            MersenneTwister.Seed((uint)(mode + 1));
            for (var i = 0; i < 2; i++)
            {
                var p = game.Players[i]; p.Items.SidekickLevel = unchecked((byte)mode); p.Items.SidekickSeries = unchecked((byte)(mode + 1));
                p.Items.SuperArcadeMode = unchecked((byte)(mode + 2));
                p.Items.Weapons[0].Power = (byte)(mode % 11 + 1); p.Items.Weapons[1].Power = (byte)((mode + 1) % 11 + 1);
                p.Cash = (mode & 256) != 0 ?
                    (OperatingSystem.IsWindows() || IntPtr.Size == 4 ? uint.MaxValue : ulong.MaxValue) - (ulong)(mode + i) : (ulong)(mode * 97 + i);
            }
            trace.AppendLine($"init:{mode}"); progression.Initialize("Start"u8);
            trace.AppendLine($"flags:{Bit(game.GameHasRepeated)},{Bit(game.OnePlayerAction)},{Bit(game.SuperTyrian)},{game.SuperArcadeMode}," +
                $"{Bit(game.TwoPlayerMode)},{game.SecretHint},{game.MainLevel},{game.SaveLevel},{Encoding.Latin1.GetString(game.LastLevelName)}");
            foreach (var p in game.Players)
            {
                Dump(p.Items); Dump(p.LastItems);
                trace.AppendLine($"extras:{p.Items.Shield},{p.Armor},{p.WeaponMode},{Bit(p.IsDragonwing)},{p.Cash},{p.Lives}");
            }
            trace.AppendLine($"rng:{MersenneTwister.NextUInt32()}");
            game.TwoPlayerMode = (mode & 1) != 0;
            foreach (var p in game.Players)
            {
                p.Items.Weapons[0].Power = (byte)(mode % 11 + 1); p.Items.Weapons[1].Power = (byte)((mode + 3) % 11 + 1);
            }
            for (var type = 0; type < 10; type++)
                for (var index = 0; index < 3; index++)
                {
                    var cost = progression.GetCost((byte)type, (ushort)index); var value = progression.GetValue((byte)type, (ushort)index);
                    var upgrade = type < 3 ? 12 : progression.UpgradeCost;
                    var downgrade = type < 3 ? 13 : progression.DowngradeCost;
                    trace.AppendLine($"value:{type},{index},{cost},{value},{upgrade},{downgrade}");
                }
            trace.AppendLine($"specialship:{progression.GetCost(2, 91)}");
            trace.AppendLine($"total:{progression.TotalScore(game.Players[0])},{progression.TotalScore(game.Players[1])}");
            for (var difficulty = 1; difficulty < 10; difficulty++)
                for (var j = 0; j < scores.Length; j++)
                {
                    game.Players[0].Cash = scores[j]; game.Players[1].Cash = (ulong)j;
                    game.InitialDifficulty = (sbyte)difficulty; game.Difficulty = (sbyte)(mode % 11);
                    progression.AdjustDifficulty(); trace.AppendLine($"difficulty:{difficulty},{j},{game.Difficulty}");
                }
            void Dump(PlayerItems p) => trace.AppendLine($"items:{p.Weapons[0].Id},{p.Weapons[0].Power},{p.Weapons[1].Id},{p.Weapons[1].Power}," +
                $"{p.Sidekicks[0]},{p.Sidekicks[1]},{p.Special},{p.Ship},{p.SidekickLevel},{p.SidekickSeries},{p.SuperArcadeMode},{p.Generator},0,0,0");
        }
        TestFailures(items);
        if (executable is null) return;
        var path = Path.Combine(Path.GetTempPath(), $"opentyrian-progression-{Guid.NewGuid():N}.tyrr");
        try
        {
            File.WriteAllBytes(path, Encoding.Latin1.GetBytes(fixture));
            var start = new ProcessStartInfo(executable) { RedirectStandardOutput = true, RedirectStandardError = true };
            start.ArgumentList.Add(path);
            using var process = Process.Start(start) ?? throw new InvalidOperationException("Cannot start progression reference.");
            var output = process.StandardOutput.ReadToEndAsync(); var errors = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(60000)) { process.Kill(entireProcessTree: true); throw new TimeoutException("Progression reference timed out."); }
            var expected = output.GetAwaiter().GetResult().Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
            var actual = trace.ToString().Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
            if (process.ExitCode != 0 || !expected.SequenceEqual(actual))
            {
                var index = Enumerable.Range(0, Math.Min(expected.Length, actual.Length)).FirstOrDefault(i => expected[i] != actual[i], -1);
                throw new InvalidOperationException($"Progression differs at {index}: {expected.ElementAtOrDefault(index)} / " +
                    $"{actual.ElementAtOrDefault(index)}; {errors.GetAwaiter().GetResult()}");
            }
        }
        finally { File.Delete(path); }
    }

    private static void TestFailures(ItemDatabase items)
    {
        var game = new LegacyGameState(); var progression = new PlayerProgression(game, items);
        Throws<ArgumentOutOfRangeException>(() => PlayerProgression.WeaponUpgradeCost(1, 12));
        Throws<InvalidDataException>(() => progression.GetValue(3, 0));
        Throws<InvalidDataException>(() => progression.GetCost(3, 0));
        Throws<InvalidDataException>(() => progression.GetValue(2, 65535));
        Throws<InvalidDataException>(() => progression.AdjustDifficulty());
        progression.Initialize("Start\0tail"u8);
        if (!game.LastLevelName.AsSpan().SequenceEqual("Start"u8)) throw new InvalidOperationException("Initial name ignored NUL termination.");
        game.Players[0].Items.Weapons[0].Power = 12;
        _ = progression.GetValue(3, 0);
        Throws<InvalidDataException>(() => progression.GetCost(3, 0));
        game.TwoPlayerMode = true; game.InitialDifficulty = 9; game.Players[0].Cash = ulong.MaxValue; game.Players[1].Cash = 0;
        Throws<InvalidDataException>(() => progression.AdjustDifficulty());
        if (PlayerProgression.WeaponUpgradeCost(int.MaxValue, 11) !=
            (OperatingSystem.IsWindows() || IntPtr.Size == 4 ? unchecked((int)(int.MaxValue * 66L)) : int.MaxValue * 66L))
            throw new InvalidOperationException("Native long weapon upgrade arithmetic differs.");
    }
    private static int Bit(bool value) => value ? 1 : 0;
    private static void Throws<T>(Action action) where T : Exception
    {
        try { action(); } catch (T) { return; }
        throw new InvalidOperationException($"Expected {typeof(T).Name}.");
    }
}
