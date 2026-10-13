using System.Diagnostics;
using System.Text;
using OpenTyrian.Core;

namespace OpenTyrian.Core.Tests;

internal static class MapTransitionTests
{
    public static void Run(string? executable = null)
    {
        var trace = new StringBuilder();
        var transitions = new[] { (byte)'U', (byte)'V', (byte)'H', (byte)'?' };
        var pictureTexts = new[] { "17tail", "  +3", "abc" };
        var scenario = 0;
        foreach (var pitch in new[] { 320, 327 })
            foreach (var transition in transitions)
                foreach (var newKey in new[] { false, true })
                {
                    var screen = new IndexedSurface(320, 200, pitch, TextWriter.Null);
                    var background = new IndexedSurface(320, 200, pitch, TextWriter.Null);
                    FillPattern(screen.Pixels.Span, scenario, 17);
                    FillPattern(background.Pixels.Span, scenario + 1, 31);
                    var picture = new byte[320 * 200];
                    Array.Fill(picture, (byte)0xA5);
                    var host = new TraceHost(trace, screen, background, newKey, (scenario + 1) % 2 != 0);
                    var section = Encoding.ASCII.GetBytes($"M{(char)transition}?{pictureTexts[scenario % pictureTexts.Length]}");
                    trace.AppendLine($"case:{scenario},{pitch},{(char)transition},{(newKey ? 1 : 0)}," +
                        $"{(host.NetworkGame ? 1 : 0)},{Encoding.ASCII.GetString(section)}");
                    MapTransition.Play(section, screen, background, picture, scenario % 13 == 0, host);
                    trace.AppendLine($"state:{Hash(screen.Pixels.Span)},{Hash(background.Pixels.Span)},{Hash(picture)}");
                    scenario++;
                }

        if (executable is null)
            return;
        using var process = Process.Start(new ProcessStartInfo(executable)
        { RedirectStandardOutput = true, RedirectStandardError = true })
            ?? throw new InvalidOperationException("Cannot start map-transition native reference.");
        var output = process.StandardOutput.ReadToEndAsync();
        var errors = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(30000))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException("Map-transition native reference timed out.");
        }

        var expected = output.GetAwaiter().GetResult().Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var actual = trace.ToString().Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        if (process.ExitCode != 0 || !expected.SequenceEqual(actual))
        {
            var index = Enumerable.Range(0, Math.Min(expected.Length, actual.Length))
                .FirstOrDefault(i => expected[i] != actual[i], -1);
            var expectedCase = expected.Take(index + 1).LastOrDefault(line => line.StartsWith("case:", StringComparison.Ordinal));
            var actualCase = actual.Take(index + 1).LastOrDefault(line => line.StartsWith("case:", StringComparison.Ordinal));
            throw new InvalidOperationException($"Map transition differs at {index}: " +
                $"{expected.ElementAtOrDefault(index)} / {actual.ElementAtOrDefault(index)}; " +
                $"cases {expectedCase} / {actualCase}; " +
                errors.GetAwaiter().GetResult());
        }
    }

    private static void FillPattern(Span<byte> pixels, int seed, int multiplier)
    {
        for (var index = 0; index < pixels.Length; index++)
            pixels[index] = unchecked((byte)(index * multiplier + seed * 19));
    }

    private static uint Hash(ReadOnlySpan<byte> bytes)
    {
        var hash = 2166136261u;
        foreach (var value in bytes)
            hash = unchecked((hash ^ value) * 16777619u);
        return hash;
    }

    private sealed class TraceHost(StringBuilder trace, IndexedSurface screen, IndexedSurface background,
        bool newKey, bool networkGame) : IMapTransitionHost
    {
        public bool NewKey => newKey;
        public bool NetworkGame => networkGame;

        public void LoadPicture(IndexedSurface target, int number)
        {
            trace.AppendLine($"picture:{number}");
            var pixels = target.Pixels.Span;
            for (var index = 0; index < pixels.Length; index++)
                pixels[index] = unchecked((byte)(index * 7 + number * 13));
        }

        public void ServiceEvents(bool clear) => trace.AppendLine($"events:{(clear ? 1 : 0)}");
        public void SetDelay(int ticks) => trace.AppendLine($"delay:{ticks}");
        public void Present()
            => trace.AppendLine($"frame:{Hash(screen.Pixels.Span)},{Hash(background.Pixels.Span)}");
        public void WaitDelay() => trace.AppendLine("wait");
    }
}
