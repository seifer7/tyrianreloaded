using System.Diagnostics;
using System.Globalization;
using System.Text;
using OpenTyrian.Core;

namespace OpenTyrian.Core.Tests;

internal static class EpisodeCompletionTests
{
    public static void Run(string? executable = null)
    {
        var trace = new StringBuilder();
        for (var scenario = 0; scenario < 48; scenario++)
        {
            var state = new EpisodeCompletionState
            {
                SecretHint = (byte)(scenario % 3 + 1),
                TwoPlayerMode = (scenario & 1) != 0,
                ConstantPlay = (scenario & 2) != 0,
                NetworkGame = (scenario & 4) != 0,
                EscapePressed = true,
                JumpBackToEpisode1 = (scenario & 8) != 0,
                SuperTyrian = (scenario & 16) != 0,
                InitialDifficulty = (sbyte)((scenario & 32) != 0 ? 4 : 2),
                SuperArcadeMode = (byte)(scenario % 11),
                MainLevel = 3,
                FrameCountMax = 9,
                TotalScore = (ulong)(123456 + scenario),
            };
            state.Cash[0] = (ulong)(9000 + scenario);
            state.Cash[1] = (ulong)(7000 + scenario);
            var host = new TraceHost(trace);
            var completion = new EpisodeCompletion(state, "Score"u8.ToArray(),
                ["Player One"u8.ToArray(), "Player Two"u8.ToArray()], MakeData(), host);
            var seed = (uint)(1100 + scenario * 13);
            MersenneTwister.Seed(seed);
            using var episode = MakeEpisode();
            trace.AppendLine($"case:{scenario},{seed},{state.SecretHint},{Bool(state.TwoPlayerMode)}," +
                $"{Bool(state.ConstantPlay)},{Bool(state.NetworkGame)},{Bool(state.JumpBackToEpisode1)}," +
                $"{Bool(state.SuperTyrian)},{state.InitialDifficulty},{state.SuperArcadeMode}");
            var result = completion.Show(episode);
            trace.AppendLine($"state:{Bool(result)},{Bool(state.EscapePressed)},{state.FrameCountMax}," +
                $"{state.WarningLines},{Bool(state.JumpSection)},{Bool(state.TrentWin)}," +
                $"{state.SuperArcadeMode},{state.MainLevel},{episode.Position},{DumpLines(state)}");
        }

        TestMalformedInput();
        if (executable is null)
            return;
        using var process = Process.Start(new ProcessStartInfo(executable)
        { RedirectStandardOutput = true, RedirectStandardError = true })
            ?? throw new InvalidOperationException("Cannot start episode-completion native reference.");
        var output = process.StandardOutput.ReadToEndAsync();
        var errors = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(30000))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException("Episode-completion native reference timed out.");
        }

        var expected = output.GetAwaiter().GetResult().Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var actual = trace.ToString().Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        if (process.ExitCode != 0 || !expected.SequenceEqual(actual))
        {
            var index = Enumerable.Range(0, Math.Min(expected.Length, actual.Length))
                .FirstOrDefault(i => expected[i] != actual[i], -1);
            var expectedCase = expected.Take(index + 1).LastOrDefault(line => line.StartsWith("case:", StringComparison.Ordinal));
            var actualCase = actual.Take(index + 1).LastOrDefault(line => line.StartsWith("case:", StringComparison.Ordinal));
            throw new InvalidOperationException($"Episode completion differs at {index}: " +
                $"{expected.ElementAtOrDefault(index)} / {actual.ElementAtOrDefault(index)}; " +
                $"cases {expectedCase} / {actualCase}; {errors.GetAwaiter().GetResult()}");
        }
    }

    private static EpisodeSuperArcadeData MakeData()
        => new(Enumerable.Range(0, 10).Select(i => Encoding.ASCII.GetBytes($"Ship{i}")).ToArray(),
            Enumerable.Range(0, 10).Select(i => Encoding.ASCII.GetBytes($"Code{i}")).ToArray(),
            "Engage"u8.ToArray(), "Ace"u8.ToArray(),
            Enumerable.Range(0, 16).Select(i => (ushort)(50 + i)).ToArray());

    private static MemoryStream MakeEpisode()
    {
        var stream = new MemoryStream();
        for (var section = 0; section < 12; section++)
        {
            WriteEncryptedString(stream, Encoding.ASCII.GetBytes($"Hint{section}"));
            WriteEncryptedString(stream, "#"u8);
        }
        stream.Position = 0;
        return stream;
    }

    private static void WriteEncryptedString(Stream stream, ReadOnlySpan<byte> plaintext)
    {
        stream.WriteByte(checked((byte)plaintext.Length));
        Span<byte> encrypted = stackalloc byte[255];
        ReadOnlySpan<byte> key = [204, 129, 63, 255, 71, 19, 25, 62, 1, 99];
        byte previous = 0;
        for (var index = 0; index < plaintext.Length; index++)
        {
            encrypted[index] = (byte)(plaintext[index] ^ key[index % key.Length] ^ previous);
            previous = encrypted[index];
        }
        stream.Write(encrypted[..plaintext.Length]);
    }

    private static string DumpLines(EpisodeCompletionState state)
        => string.Join(',', state.WarningText.Select(line => Convert.ToHexString(line)));

    private static string Bool(bool value) => value ? "1" : "0";

    private static void TestMalformedInput()
    {
        var state = new EpisodeCompletionState { SecretHint = 1 };
        var completion = new EpisodeCompletion(state, "Score"u8.ToArray(),
            ["One"u8.ToArray(), "Two"u8.ToArray()], MakeData(),
            new TraceHost(new StringBuilder()));
        using var episode = new MemoryStream();
        MersenneTwister.Seed(0);
        var skippedSections = (int)(MersenneTwister.NextUInt32() % 3) * 3;
        MersenneTwister.Seed(0);
        for (var section = 0; section < skippedSections; section++)
        {
            WriteEncryptedString(episode, "Hint"u8);
            WriteEncryptedString(episode, "#"u8);
        }
        for (var index = 0; index < 12; index++)
            WriteEncryptedString(episode, "too-long-warning-line-that-exceeds-native-storage-capacity-by-a-wide-margin"u8);
        episode.Position = 0;
        MersenneTwister.Seed(0);
        try
        {
            completion.Show(episode);
            throw new InvalidOperationException("Episode completion accepted too many warning lines.");
        }
        catch (InvalidDataException) { }
    }

    private sealed class TraceHost(StringBuilder trace) : IEpisodeCompletionHost
    {
        public void WipeKey() => trace.AppendLine("wipe");
        public void DisplayText() => trace.AppendLine("display");
        public void FadeBlack(int steps) => trace.AppendLine($"fadeblack:{steps}");
        public void NextEpisode() => trace.AppendLine("next");
        public void LoadPicture(int number) => trace.AppendLine($"picture:{number}");
        public void Clear() => trace.AppendLine("clear");
        public void DrawCentered(int y, ReadOnlyMemory<byte> text, int font)
        {
            var value = Encoding.ASCII.GetString(text.Span);
            trace.AppendLine($"center:{value},{font}");
            trace.AppendLine($"draw:100,{y},{value},{font}");
        }
        public void DrawText(int x, int y, ReadOnlyMemory<byte> text, int font)
            => trace.AppendLine($"draw:{x},{y},{Encoding.ASCII.GetString(text.Span)},{font}");
        public void DrawShip(int x, int y, ushort graphic) => trace.AppendLine($"ship:{x},{y},{graphic}");
        public void Present() => trace.AppendLine("present");
        public void FadePalette(int steps, int firstColor, int lastColor)
            => trace.AppendLine($"palette:{steps},{firstColor},{lastColor}");
        public void WaitInput(bool clearEvents, bool allowKeyboard, bool allowMouse)
            => trace.AppendLine($"wait:{Bool(clearEvents)},{Bool(allowKeyboard)},{Bool(allowMouse)}");
        public void ReadTextSync() => trace.AppendLine("sync");
    }
}
