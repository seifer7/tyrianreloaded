using System.Diagnostics;
using System.Text;
using OpenTyrian.Core;

namespace OpenTyrian.Core.Tests;

internal static class EpisodePictureDisplayTests
{
    private static readonly string[] Values =
    [
        "0", "1", "3", "899", "900", "901", "912", "913", "-1", "-901",
        " 0", "\t+2", "17tail", "abc", "-0", "  -10x", "909",
        "-2147483648", "   ", "+", "00009", "  905x",
    ];

    public static void Run(string? executable = null)
    {
        var trace = new StringBuilder();
        for (var mode = 0; mode < 256; mode++)
        {
            var constantPlay = (mode & 1) != 0;
            var suffix = Values[mode % Values.Length];
            var section = Encoding.ASCII.GetBytes($"P?x{suffix}\0ignored");
            var host = new TraceHost(trace);
            var display = new EpisodePictureDisplay(host) { TemporaryPicture = 777 };
            trace.AppendLine($"case:{mode},{(constantPlay ? 1 : 0)},{suffix}");
            display.Show(section, constantPlay);
            trace.AppendLine($"state:{display.TemporaryPicture},{host.PaletteValue}");
            AssertSequence(mode, suffix, constantPlay, display, host);
        }
        var rejected = false;
        try
        {
            new EpisodePictureDisplay(new TraceHost(trace)).Show("P?x914"u8, false);
        }
        catch (InvalidDataException)
        {
            rejected = true;
        }
        if (!rejected)
            throw new InvalidOperationException("Episode picture accepted a PCX palette index outside the native table.");

        if (executable is null) return;
        using var process = Process.Start(new ProcessStartInfo(executable)
        { RedirectStandardOutput = true, RedirectStandardError = true })
            ?? throw new InvalidOperationException("Cannot start episode-picture reference.");
        var output = process.StandardOutput.ReadToEndAsync();
        var errors = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(30000))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException("Episode-picture reference timed out.");
        }

        var expected = output.GetAwaiter().GetResult().Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var actual = trace.ToString().Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        if (process.ExitCode != 0 || !expected.SequenceEqual(actual))
        {
            var index = Enumerable.Range(0, Math.Min(expected.Length, actual.Length))
                .FirstOrDefault(i => expected[i] != actual[i], -1);
            throw new InvalidOperationException($"Episode-picture display differs at {index}: " +
                $"{expected.ElementAtOrDefault(index)} / {actual.ElementAtOrDefault(index)}; " +
                errors.GetAwaiter().GetResult());
        }
    }

    private static void AssertSequence(int mode, string suffix, bool constantPlay,
        EpisodePictureDisplay display, TraceHost host)
    {
        var value = ParseExpected(suffix);
        if (constantPlay)
        {
            if (display.TemporaryPicture != 777 || host.Actions.Count != 0)
                throw new InvalidOperationException($"Constant-play episode picture had side effects in case {mode}.");
            return;
        }

        var expectedPalette = value > 900 ? value - 901 : -1;
        if (display.TemporaryPicture != value || host.PaletteValue != expectedPalette)
            throw new InvalidOperationException($"Episode-picture value or palette differs in case {mode}.");

        string[] expected = value > 900
            ? [$"palette:{(value - 901) % 23}", "clear", "present", "fade:1:0:255"]
            : value == 0
                ? ["pcx:tshp2.pcx", "present", "fade:10:0:255"]
                : [$"picture:{value}:0", "present", "fade:10:0:255"];
        if (!host.Actions.SequenceEqual(expected))
            throw new InvalidOperationException($"Episode-picture action order differs in case {mode}.");
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
        long value = 0;
        var digits = 0;
        while (index < text.Length && text[index] is >= '0' and <= '9')
        {
            value = value * 10 + text[index] - '0';
            digits++;
            index++;
        }
        return digits == 0 ? 0 : checked((int)(negative ? -value : value));
    }

    private sealed class TraceHost(StringBuilder trace) : IEpisodePictureHost
    {
        public List<string> Actions { get; } = [];
        public int PaletteValue { get; private set; } = -1;

        public void SelectPcxPalette(int zeroBasedIndex)
        {
            PaletteValue = zeroBasedIndex;
            Add($"palette:{PaletteValue}");
        }

        public void Clear() => Add("clear");
        public void LoadPcx() => Add("pcx:tshp2.pcx");
        public void LoadPicture(int number, bool preservePalette) => Add($"picture:{number}:{(preservePalette ? 1 : 0)}");
        public void Present() => Add("present");
        public void FadePalette(int steps, int firstColor, int lastColor) => Add($"fade:{steps}:{firstColor}:{lastColor}");

        private void Add(string action)
        {
            Actions.Add(action);
            trace.AppendLine(action);
        }
    }
}
