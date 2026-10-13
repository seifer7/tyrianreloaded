using System.Diagnostics;
using System.Text;
using OpenTyrian.Core;

namespace OpenTyrian.Core.Tests;

internal static class EpisodeModeConfigurationTests
{
    private static readonly char[] Commands = ['g', 'x', 'e', 'q'];
    private static readonly EpisodeModeItemIds ItemIds = new(211, 39, 15, 12, 22);

    public static void Run(string? executable = null)
    {
        var trace = new StringBuilder();
        for (var mode = 0; mode < 256; mode++)
        {
            var game = new LegacyGameState
            {
                GalagaMode = (mode & 1) != 0,
                ExtraGame = (mode & 2) != 0,
                SuperTyrian = (mode & 4) != 0,
                OnePlayerAction = (mode & 8) != 0,
                TwoPlayerMode = (mode & 16) != 0,
            };
            SeedPlayer(game.Players[0], mode, 0);
            SeedPlayer(game.Players[1], mode, 1);
            var episode = new EpisodeModeState
            {
                ConstantDie = (mode & 32) != 0,
                DoNotSaveBackup = (mode & 64) != 0,
            };
            var command = Commands[mode % Commands.Length];
            trace.AppendLine($"case:{mode},{command},{Dump(game, episode)}");
            EpisodeModeConfiguration.Configure(game, episode, ItemIds, command);
            trace.AppendLine($"state:{Dump(game, episode)}");
            AssertState(mode, game, episode, command);
        }

        if (executable is null) return;
        using var process = Process.Start(new ProcessStartInfo(executable)
        { RedirectStandardOutput = true, RedirectStandardError = true })
            ?? throw new InvalidOperationException("Cannot start episode-mode reference.");
        var output = process.StandardOutput.ReadToEndAsync();
        var errors = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(30000))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException("Episode-mode reference timed out.");
        }

        var expected = output.GetAwaiter().GetResult().Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var actual = trace.ToString().Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        if (process.ExitCode != 0 || !expected.SequenceEqual(actual))
        {
            var index = Enumerable.Range(0, Math.Min(expected.Length, actual.Length))
                .FirstOrDefault(i => expected[i] != actual[i], -1);
            throw new InvalidOperationException($"Episode-mode state differs at {index}: " +
                $"{expected.ElementAtOrDefault(index)} / {actual.ElementAtOrDefault(index)}; " +
                errors.GetAwaiter().GetResult());
        }
    }

    private static void SeedPlayer(PlayerState player, int mode, int number)
    {
        player.Cash = (uint)(mode * 101 + number);
        var items = player.Items;
        items.Ship = (byte)(mode + number);
        items.Generator = (byte)(mode + 2 + number);
        items.Shield = (byte)(mode + 3 + number);
        items.Special = (byte)(mode + 4 + number);
        items.Weapons[0] = new PlayerWeapon { Id = (byte)(mode + 5 + number), Power = (byte)(mode + 6 + number) };
        items.Weapons[1] = new PlayerWeapon { Id = (byte)(mode + 7 + number), Power = (byte)(mode + 8 + number) };
        items.Sidekicks[0] = (byte)(mode + 9 + number);
        items.Sidekicks[1] = (byte)(mode + 10 + number);
    }

    private static void AssertState(int mode, LegacyGameState game, EpisodeModeState state, char command)
    {
        var originalFirst = SnapshotPlayer(mode, 0);
        var originalSecond = SnapshotPlayer(mode, 1);
        if (command == 'g')
        {
            originalSecond.Items = originalFirst.Items.Copy();
            originalSecond.Items.Weapons[1].Id = ItemIds.VulcanCannon;
            Array.Clear(originalSecond.Items.Sidekicks);
        }
        else if (command == 'e')
        {
            originalFirst.Cash = 0;
            originalFirst.Items.Ship = ItemIds.Stalker21126;
            originalFirst.Items.Weapons[0] = new PlayerWeapon { Id = ItemIds.AtomicRailgun, Power = 3 };
            originalFirst.Items.Weapons[1] = new PlayerWeapon { Id = 0, Power = 1 };
            Array.Clear(originalFirst.Items.Sidekicks);
            originalFirst.Items.Generator = ItemIds.AdvancedMr12;
            originalFirst.Items.Shield = ItemIds.AdvancedIntegrityField;
            originalFirst.Items.Special = 0;
        }

        var expectedGalaga = ((mode & 1) != 0) || command == 'g';
        var expectedExtra = ((mode & 2) != 0) || command == 'x';
        var expectedSuperTyrian = ((mode & 4) != 0) || command == 'e';
        var expectedOnePlayer = ((mode & 8) != 0) || command == 'e';
        var expectedTwoPlayer = (mode & 16) != 0 && command != 'e';
        var expectedConstantDie = (mode & 32) != 0 && command != 'e';
        var expectedNoBackup = ((mode & 64) != 0) || command == 'e';
        if (game.GalagaMode != expectedGalaga || game.ExtraGame != expectedExtra ||
            game.SuperTyrian != expectedSuperTyrian || game.OnePlayerAction != expectedOnePlayer ||
            game.TwoPlayerMode != expectedTwoPlayer || state.ConstantDie != expectedConstantDie ||
            state.DoNotSaveBackup != expectedNoBackup ||
            DumpPlayer(game.Players[0]) != DumpPlayer(originalFirst) ||
            DumpPlayer(game.Players[1]) != DumpPlayer(originalSecond))
            throw new InvalidOperationException($"Episode-mode configuration differs in scenario {mode}.");
    }

    private static PlayerState SnapshotPlayer(int mode, int number)
    {
        var player = new PlayerState(number);
        SeedPlayer(player, mode, number);
        return player;
    }

    private static string Dump(LegacyGameState game, EpisodeModeState state) =>
        $"{(game.GalagaMode ? 1 : 0)},{(game.ExtraGame ? 1 : 0)},{(game.SuperTyrian ? 1 : 0)}," +
        $"{(game.OnePlayerAction ? 1 : 0)},{(game.TwoPlayerMode ? 1 : 0)}," +
        $"{(state.ConstantDie ? 1 : 0)},{(state.DoNotSaveBackup ? 1 : 0)};" +
        $"{DumpPlayer(game.Players[0])};{DumpPlayer(game.Players[1])}";

    private static string DumpPlayer(PlayerState player)
    {
        var items = player.Items;
        return $"{player.Cash},{items.Ship},{items.Weapons[0].Id},{items.Weapons[0].Power}," +
            $"{items.Weapons[1].Id},{items.Weapons[1].Power},{items.Generator},{items.Shield}," +
            $"{items.Special},{items.Sidekicks[0]},{items.Sidekicks[1]}";
    }
}
