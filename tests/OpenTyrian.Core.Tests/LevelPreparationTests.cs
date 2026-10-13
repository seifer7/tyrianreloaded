using System.Diagnostics;
using System.Text;
using OpenTyrian.Core;

namespace OpenTyrian.Core.Tests;

internal static class LevelPreparationTests
{
    public static void Run(string? executable = null)
    {
        var trace = new StringBuilder();
        for (var scenario = 0; scenario < 49152; scenario++)
        {
            var mode = scenario / 6;
            var settings = new GameRuntimeSettings { FastPlay = (byte)(scenario % 6) };
            settings.InitializeProcessor();
            settings.FastPlay = (byte)(scenario % 6);
            var game = new LegacyGameState
            {
                GalagaMode = Bit(mode, 0),
                TwoPlayerMode = Bit(mode, 1),
                Difficulty = (sbyte)(mode % 8 - 2),
                OldDifficulty = (sbyte)((mode + 3) % 8 - 2),
                MainLevel = (byte)(mode % 19),
                SuperTyrian = Bit(mode, 2),
                OnePlayerAction = Bit(mode, 3),
            };
            game.Players[0].IsAlive = Bit(mode, 4);
            game.Players[1].IsAlive = Bit(mode, 5);
            game.Players[0].Items.SuperArcadeMode = (byte)(mode % 9);
            var state = new LevelPreparationState
            {
                NormalBonusLevel = Bit(mode, 8),
                BonusLevel = Bit(mode, 9),
                PlayerEndLevel = Bit(mode, 10),
                DoNotSaveBackup = Bit(mode, 11),
                DemoFileOpen = Bit(mode, 12),
                PlayDemo = Bit(mode, 6),
                RecordDemo = Bit(mode, 7),
                NextLevel = (byte)(mode % 23 + 1),
            };
            var host = new TraceHost(trace);
            var preparation = new LevelPreparation(game, settings, state, host);
            trace.AppendLine($"case:{scenario},{mode},{scenario % 6}");
            DumpState(trace, game, settings, state);
            var result = preparation.Prepare();
            trace.AppendLine($"result:{Bool(result)}");
            DumpState(trace, game, settings, state);
        }

        if (executable is null)
            return;

        using var process = Process.Start(new ProcessStartInfo(executable)
        { RedirectStandardOutput = true, RedirectStandardError = true })
            ?? throw new InvalidOperationException("Cannot start level-preparation native reference.");
        var output = process.StandardOutput.ReadToEndAsync();
        var errors = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(30000))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException("Level-preparation native reference timed out.");
        }

        var expected = output.GetAwaiter().GetResult().Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var actual = trace.ToString().Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        if (process.ExitCode != 0 || !expected.SequenceEqual(actual))
        {
            var index = Enumerable.Range(0, Math.Min(expected.Length, actual.Length))
                .FirstOrDefault(i => expected[i] != actual[i], -1);
            var testCase = expected.Take(index + 1).LastOrDefault(line => line.StartsWith("case:", StringComparison.Ordinal));
            throw new InvalidOperationException($"Level preparation differs at {index}: " +
                $"{expected.ElementAtOrDefault(index)} / {actual.ElementAtOrDefault(index)}; " +
                $"case {testCase}; {errors.GetAwaiter().GetResult()}");
        }
    }

    private static void DumpState(StringBuilder trace, LegacyGameState game, GameRuntimeSettings settings,
        LevelPreparationState state)
        => trace.AppendLine($"state:{Bool(game.TwoPlayerMode)},{game.Difficulty},{game.MainLevel}," +
            $"{Bool(settings.SmoothScroll)},{Bool(game.SuperTyrian)},{Bool(game.OnePlayerAction)}," +
            $"{game.Players[0].Items.SuperArcadeMode},{Bool(state.DoNotSaveBackup)}," +
            $"{Bool(state.DemoFileOpen)},{Bool(state.PlayDemo)},{Bool(state.RecordDemo)}");

    private static bool Bit(int value, int bit) => (value & (1 << bit)) != 0;
    private static string Bool(bool value) => value ? "1" : "0";

    private sealed class TraceHost(StringBuilder trace) : ILevelPreparationHost
    {
        public void SetMouseRelative(bool enabled) => trace.AppendLine($"relative:{Bool(enabled)}");
        public void ClearKeyboard() => trace.AppendLine("clear-keyboard");
        public void FreeEnemySpriteSheet(int index) => trace.AppendLine($"free:{index}");
        public void SetDelaySpeed(ushort speed) => trace.AppendLine($"speed:{speed}");
        public void CloseDemoFile() => trace.AppendLine("close-demo");
        public void StopSong() => trace.AppendLine("stop-song");
        public void FadeBlack(int steps) => trace.AppendLine($"fade-black:{steps}");
        public void WaitNoInput(bool clearEvents, bool allowKeyboard, bool allowMouse)
            => trace.AppendLine($"wait-no-input:{Bool(clearEvents)},{Bool(allowKeyboard)},{Bool(allowMouse)}");
        public void EndLevelAnimation() => trace.AppendLine("end-animation");
        public void FadeSong() => trace.AppendLine("fade-song");
        public void LoadGame(int slot) => trace.AppendLine($"load-game:{slot}");
    }
}
