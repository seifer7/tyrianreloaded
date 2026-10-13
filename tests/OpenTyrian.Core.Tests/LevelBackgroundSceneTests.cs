using System.Diagnostics;
using System.Text;
using OpenTyrian.Core;

namespace OpenTyrian.Core.Tests;

internal static class LevelBackgroundSceneTests
{
    public static void Run(string? executable = null)
    {
        var trace = new StringBuilder();
        for (var scenario = 0; scenario < 4096; scenario++)
        {
            var mode = scenario % 2048;
            var state = new LevelBackgroundSceneState
            {
                ForceEvents = Bit(mode, 0),
                StarfieldActive = Bit(mode, 1),
                Wild = Bit(mode, 2),
                Background2NotTransparent = Bit(mode, 3),
                SuperWild = Bit(mode, 4),
                EnemiesActive = Bit(mode, 5),
                SkyEnemyOverAll = Bit(mode, 6),
                Background3X1 = Bit(mode, 7),
                TopEnemyOver = Bit(mode, 8),
                Background2Over = (byte)(scenario % 4),
                Background3Over = (byte)(scenario / 3 % 3),
                Map1YDelay = (byte)(scenario % 5),
                Map1YDelayMax = (byte)(scenario % 4),
                BackMove = (ushort)(scenario % 4),
                BackPosition = (ushort)(scenario % 33),
                MapY = (ushort)(scenario % 17),
                MapYPositionOffset = 32,
                CurrentLocation = (ushort)(scenario * 3),
                AstralDuration = (byte)(scenario % 2 == 0 ? 3 : 0),
                ProcessorType = (byte)(scenario % 6),
                StarfieldSpeed = scenario % 9 - 4,
                EnemyOnScreen = (ushort)(scenario % 7),
                LastEnemyOnScreen = (ushort)(scenario * 13 % 11),
                StopBackgroundNumber = (byte)(scenario % 4),
                Neat = (ushort)(scenario * 11),
                MapXOffset = (ushort)(scenario * 2),
                MapX2Offset = (ushort)(scenario * 3),
                OldMapX3Offset = (ushort)(scenario * 5),
                MapX3Offset = (ushort)(scenario * 7),
                Background3Move = (ushort)(scenario % 8),
                TemporaryMapXOffset = (ushort)(scenario % 19),
                TemporaryBackgroundMove = (ushort)(scenario % 13),
                LevelEnemyFrequency = (ushort)(scenario % 101),
                LevelEnemyMaximum = (ushort)(scenario % 40 + 1),
                EnemySound = (byte)(scenario % 16),
                LastSpawnedEnemyIndex = 0,
            };
            state.Smoothies[0] = Bit(mode, 9);
            state.Smoothies[1] = Bit(mode, 10);
            state.Smoothies[2] = Bit(scenario, 0);
            state.Smoothies[3] = Bit(scenario, 1);
            state.Smoothies[4] = Bit(scenario, 2);
            state.SmoothieData[0] = (byte)(scenario % 3);
            for (var i = 0; i < state.LevelEnemies.Length; i++)
                state.LevelEnemies[i] = (ushort)((i + scenario) % 5);

            var host = new TraceHost(trace, scenario);
            trace.AppendLine($"case:{scenario}");
            AppendState(trace, state, host, state.LastEnemyOnScreen);
            new LevelBackgroundScene(state, host).Draw();
            AppendState(trace, state, host, state.LastEnemyOnScreen);
        }

        if (executable is null)
            return;

        using var process = Process.Start(new ProcessStartInfo(executable)
        { RedirectStandardOutput = true, RedirectStandardError = true })
            ?? throw new InvalidOperationException("Cannot start level-background native reference.");
        var output = process.StandardOutput.ReadToEndAsync();
        var errors = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(30000))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException("Level-background native reference timed out.");
        }

        var expected = output.GetAwaiter().GetResult().Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var actual = trace.ToString().Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        if (process.ExitCode != 0 || !expected.SequenceEqual(actual))
        {
            var index = Enumerable.Range(0, Math.Min(expected.Length, actual.Length))
                .FirstOrDefault(i => expected[i] != actual[i], -1);
            var testCase = expected.Take(index + 1).LastOrDefault(line => line.StartsWith("case:", StringComparison.Ordinal));
            throw new InvalidOperationException($"Level-background scene differs at {index}: " +
                $"{expected.ElementAtOrDefault(index)} / {actual.ElementAtOrDefault(index)}; case {testCase}; " +
                errors.GetAwaiter().GetResult());
        }
    }

    private static void AppendState(StringBuilder trace, LevelBackgroundSceneState state, TraceHost host, ushort lastEnemy)
        => trace.AppendLine($"state:{state.CurrentLocation},{state.BackMove},{state.BackPosition},{state.MapY}," +
            $"{state.MapYPositionOffset},{state.Map1YDelay},{state.AstralDuration},{state.CurrentScreen}," +
            $"{Bool(state.Background2)},{state.EnemyOnScreen},{lastEnemy},{state.TemporaryMapXOffset}," +
            $"{state.TemporaryBackgroundMove},{state.StopBackgroundNumber},{state.Neat},{state.EnemySound}," +
            $"{state.LastSpawnedEnemyIndex},{host.RandomCalls},{host.EnemyCalls},{state.Background2Over}," +
            $"{state.Background3Over},{state.MapXOffset},{state.MapX2Offset},{state.OldMapX3Offset}," +
            $"{state.MapX3Offset},{Bool(state.Background3X1)},{state.Background3Move}");

    private static bool Bit(int value, int bit) => (value & (1 << bit)) != 0;
    private static string Bool(bool value) => value ? "1" : "0";

    private sealed class TraceHost(StringBuilder trace, int scenario) : ILevelBackgroundSceneHost
    {
        public int RandomCalls { get; private set; }
        public int EnemyCalls { get; private set; }

        public void ClearScreen(int screen) => trace.AppendLine($"clear:{screen}");
        public void DrawBackground1(int screen) => trace.AppendLine($"bg1:{screen}");
        public void DrawBackground2(int screen) => trace.AppendLine($"bg2:{screen}");
        public void DrawBackground2Blend(int screen) => trace.AppendLine($"bg2blend:{screen}");
        public void DrawBackground3(int screen) => trace.AppendLine($"bg3:{screen}");
        public void DrawStarfield(int screen, int speed) => trace.AppendLine($"stars:{screen},{speed}");
        public void IcedBlur(int destination, int source) => trace.AppendLine($"iced:{destination},{source}");
        public void Lava(int destination, int source) => trace.AppendLine($"lava:{destination},{source}");
        public void Water(int destination, int source) => trace.AppendLine($"water:{destination},{source}");
        public void Blur(int destination, int source) => trace.AppendLine($"blur:{destination},{source}");

        public ushort DrawEnemy(int priority, int screen, ushort mapOffset, ushort backgroundMove, ushort enemiesOnScreen)
        {
            trace.AppendLine($"enemy:{priority},{screen},{mapOffset},{backgroundMove},{enemiesOnScreen}");
            EnemyCalls++;
            return priority is 50 or 25 ? unchecked((ushort)(enemiesOnScreen + 1)) : enemiesOnScreen;
        }

        public uint NextRandomUInt32()
        {
            RandomCalls++;
            return unchecked((uint)(scenario * 71 + RandomCalls * 13));
        }

        public int SpawnEnemy(ushort enemyType)
        {
            trace.AppendLine($"spawn:{enemyType}");
            return 17 + enemyType;
        }

        public void DarkenBackground(int screen, ushort amount)
            => trace.AppendLine($"darken:{screen},{amount}");
    }
}
