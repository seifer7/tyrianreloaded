using System.Diagnostics;
using System.Text;
using OpenTyrian.Core;

namespace OpenTyrian.Core.Tests;

internal static class EpisodePresentationTests
{
    public static void Run(string? executable = null)
    {
        var trace = new StringBuilder();
        var commands = new[] { 'P', 'U', 'V', 'R', 'C', 'B', 'F', 'Q', 'W', '?' };
        var caseNumber = 0;
        foreach (var command in commands)
            foreach (var networkGame in new[] { false, true })
                foreach (var constantPlay in new[] { false, true })
                {
                    var section = Encoding.ASCII.GetBytes($"X{command}?17");
                    var buffer = new byte[256];
                    using var episode = new MemoryStream([1, 2, 3]);
                    var host = new TraceHost(trace);
                    var flow = new EpisodePresentation(
                        (value, constant) => trace.AppendLine($"picture:{Section(value.Span)},{Bool(constant)}"),
                        (value, constant) => trace.AppendLine($"transition:{Section(value.Span)},{Bool(constant)}"),
                        (stream, value, constant) =>
                            trace.AppendLine($"warning:{stream.Position},{Section(value.Span)},{Bool(constant)}"),
                        (stream, value, scratch) =>
                        {
                            trace.AppendLine($"completion:{stream.Position},{Section(value.Span)},{scratch.Length}");
                            return (caseNumber & 1) != 0;
                        }, host);
                    trace.AppendLine($"case:{caseNumber},{command},{Bool(networkGame)},{Bool(constantPlay)}");
                    var result = flow.Handle(command, episode, section, buffer, constantPlay, networkGame);
                    trace.AppendLine($"state:{Bool(result)},{episode.Position}");
                    caseNumber++;
                }

        if (executable is null)
            return;
        using var process = Process.Start(new ProcessStartInfo(executable)
        { RedirectStandardOutput = true, RedirectStandardError = true })
            ?? throw new InvalidOperationException("Cannot start episode-presentation native reference.");
        var output = process.StandardOutput.ReadToEndAsync();
        var errors = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(30000))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException("Episode-presentation native reference timed out.");
        }

        var expected = output.GetAwaiter().GetResult().Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var actual = trace.ToString().Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        if (process.ExitCode != 0 || !expected.SequenceEqual(actual))
        {
            var index = Enumerable.Range(0, Math.Min(expected.Length, actual.Length))
                .FirstOrDefault(i => expected[i] != actual[i], -1);
            throw new InvalidOperationException($"Episode presentation differs at {index}: " +
                $"{expected.ElementAtOrDefault(index)} / {actual.ElementAtOrDefault(index)}; " +
                errors.GetAwaiter().GetResult());
        }
    }

    private static string Bool(bool value) => value ? "1" : "0";
    private static string Section(ReadOnlySpan<byte> value)
    {
        var end = value.IndexOf((byte)0);
        return Encoding.ASCII.GetString(end < 0 ? value : value[..end]);
    }

    private sealed class TraceHost(StringBuilder trace) : IEpisodePresentationHost
    {
        public void FadeBlack(int steps) => trace.AppendLine($"fadeblack:{steps}");
        public void FadeWhite(int steps) => trace.AppendLine($"fadewhite:{steps}");
        public void Clear() => trace.AppendLine("clear");
        public void Present() => trace.AppendLine("present");
        public void SelectPalette(int index) => trace.AppendLine($"palette:{index}");
    }
}
