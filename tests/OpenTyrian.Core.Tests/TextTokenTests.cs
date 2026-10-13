using System.Diagnostics;
using System.Text;
using OpenTyrian.Core;

namespace OpenTyrian.Core.Tests;

internal static class TextTokenTests
{
    public static void Run(string? executable = null)
    {
        string[] inputs = ["", " \t ", "12 34", "-12+34", "+ 7", " -0\n8", "0x12", "2147483647", "2147483648",
            "-2147483648", "-2147483649", "4294967295", "9223372036854775807", "9223372036854775808",
            "-9223372036854775808", "-9223372036854775809", "999999999999999999999999999999999999999999",
            "A\tB C", "\nA B", "abcdefghij", "abcdefghijk", " \t0 \tABC\t", "12\0ignored", "\x80\xff ABC"];
        var trace = new StringBuilder();
        for (var mode = 0; mode < 192; mode++)
        {
            var reader = new TextTokenReader(Encoding.Latin1.GetBytes(inputs[mode % 24]));
            trace.AppendLine($"case:{mode}");
            for (var step = 0; step < 4; step++)
            {
                if (mode < 96)
                {
                    var result = reader.TryReadInt32(out var value);
                    trace.AppendLine($"int:{Bit(result)},{value},{Hex(reader.Remaining)}");
                }
                else
                {
                    int[] capacities = [0, 1, 11, 256]; var terminated = false; var result = false; ReadOnlyMemory<byte> value = default;
                    try { result = reader.TryReadToken(capacities[mode / 24 - 4], out value); }
                    catch (InvalidDataException) { terminated = true; }
                    trace.AppendLine($"token:{Bit(terminated)},{Bit(result)},{Hex(value)},{Hex(reader.Remaining)}");
                    if (terminated) break;
                }
            }
        }
        var utf8 = new TextTokenReader(Encoding.UTF8.GetBytes("\u00e9\u00e9\u00e9\u00e9\u00e9"));
        if (!utf8.TryReadToken(11, out var token) || token.Length != 10 || !utf8.Remaining.IsEmpty)
            throw new InvalidOperationException("Reference capacity is not measured in native bytes.");
        try
        {
            new TextTokenReader(Encoding.UTF8.GetBytes("\u00e9\u00e9\u00e9\u00e9\u00e9\u00e9")).TryReadToken(11, out _);
            throw new InvalidOperationException("Oversized UTF-8 reference accepted.");
        }
        catch (InvalidDataException) { }
        if (executable is null) return;
        using var process = Process.Start(new ProcessStartInfo(executable) { RedirectStandardOutput = true, RedirectStandardError = true })
            ?? throw new InvalidOperationException("Cannot start native text token reference.");
        var output = process.StandardOutput.ReadToEndAsync(); var errors = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(30000)) { process.Kill(entireProcessTree: true); throw new TimeoutException("Text token reference timed out."); }
        var expected = output.GetAwaiter().GetResult().Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var actual = trace.ToString().Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        if (process.ExitCode != 0 || !expected.SequenceEqual(actual))
        {
            var index = Enumerable.Range(0, Math.Min(expected.Length, actual.Length)).FirstOrDefault(i => expected[i] != actual[i], -1);
            throw new InvalidOperationException($"Text tokens differ at {index}: {expected.ElementAtOrDefault(index)} / " +
                $"{actual.ElementAtOrDefault(index)}; {errors.GetAwaiter().GetResult()}");
        }
    }
    private static string Hex(ReadOnlyMemory<byte> text)
    {
        var end = text.Span.IndexOf((byte)0);
        return Convert.ToHexString(end < 0 ? text.Span : text.Span[..end]);
    }
    private static int Bit(bool value) => value ? 1 : 0;
}
