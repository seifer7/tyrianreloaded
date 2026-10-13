using System.Diagnostics;
using System.Text;
using OpenTyrian.Core;

namespace OpenTyrian.Core.Tests;

internal static class EpisodeLevelJumpTests
{
    private static readonly char[] Commands = ['J', '2', 'w', 't', 'l', 'H', 'x'];
    private static readonly string[] Destinations =
    [
        "0", "1", "255", "256", "-1", "-257", "  +17tail", "word", "  -12",
    ];

    public static void Run(string? executable = null)
    {
        var trace = new StringBuilder();
        for (var mode = 0; mode < 2048; mode++)
        {
            var state = new EpisodeLevelJumpState
            {
                MainLevel = (byte)(mode * 7),
                JumpSection = (mode & 1) != 0,
                TwoPlayerMode = (mode & 2) != 0,
                OnePlayerAction = (mode & 4) != 0,
                LevelTimer = (mode & 8) != 0,
                TimerCountdown = mode % 14 == 3 ? (ushort)0 : (ushort)(mode * 31),
                InitialDifficulty = unchecked((sbyte)(mode % 8 - 2)),
                PlayerShip = (byte)(mode % 3),
                StalkerShip = (byte)((mode / 3) % 3),
                AllPlayersAlive = () => (mode & 16) != 0,
            };
            var command = Commands[mode % Commands.Length];
            var number = Destinations[mode / Commands.Length % Destinations.Length];
            var section = Encoding.ASCII.GetBytes($"abc{number}");
            var before = AppendState(state);
            trace.AppendLine($"case:{mode},{command},{Encoding.ASCII.GetString(section)},{before}");
            EpisodeLevelJump.Handle(state, command, section);
            trace.AppendLine($"state:{AppendState(state)}");
            AssertState(mode, state, command, number);
        }

        if (executable is null) return;
        using var process = Process.Start(new ProcessStartInfo(executable)
        { RedirectStandardOutput = true, RedirectStandardError = true })
            ?? throw new InvalidOperationException("Cannot start episode-level-jump reference.");
        var output = process.StandardOutput.ReadToEndAsync();
        var errors = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(30000))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException("Episode-level-jump reference timed out.");
        }

        var expected = output.GetAwaiter().GetResult().Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var actual = trace.ToString().Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        if (process.ExitCode != 0 || !expected.SequenceEqual(actual))
        {
            var index = Enumerable.Range(0, Math.Min(expected.Length, actual.Length))
                .FirstOrDefault(i => expected[i] != actual[i], -1);
            throw new InvalidOperationException($"Episode-level-jump behavior differs at {index}: " +
                $"{expected.ElementAtOrDefault(index)} / {actual.ElementAtOrDefault(index)}; " +
                errors.GetAwaiter().GetResult());
        }
    }

    private static void AssertState(int mode, EpisodeLevelJumpState state, char command, string number)
    {
        var shouldJump = command switch
        {
            'J' => true,
            '2' => state.TwoPlayerMode || state.OnePlayerAction,
            'w' => state.PlayerShip == state.StalkerShip,
            't' => state.LevelTimer && state.TimerCountdown == 0,
            'l' => (mode & 16) == 0,
            'H' => state.InitialDifficulty < 3,
            _ => false,
        };
        var initialLevel = unchecked((byte)(mode * 7));
        var initialJumpSection = (mode & 1) != 0;
        var parsedDestination = command == 'H' ? number[1..] : number;
        if (state.JumpSection != (shouldJump || initialJumpSection) ||
            state.MainLevel != (shouldJump ? unchecked((byte)ParseExpected(parsedDestination)) : initialLevel))
            throw new InvalidOperationException($"Episode-level-jump state differs in scenario {mode}.");
    }

    private static int ParseExpected(string text)
    {
        var index = 0;
        while (index < text.Length && text[index] is '\t' or '\n' or '\v' or '\f' or '\r' or ' ') index++;
        var negative = false;
        if (index < text.Length && text[index] is '+' or '-')
        {
            negative = text[index] == '-';
            index++;
        }
        var value = 0;
        var digits = 0;
        while (index < text.Length && text[index] is >= '0' and <= '9')
        {
            value = unchecked(value * 10 + text[index] - '0');
            digits++;
            index++;
        }
        return digits == 0 ? 0 : negative ? unchecked(-value) : value;
    }

    private static string AppendState(EpisodeLevelJumpState state) =>
        $"{state.MainLevel},{(state.JumpSection ? 1 : 0)}";
}
