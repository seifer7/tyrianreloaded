using System.Diagnostics;
using System.Globalization;
using System.Text;
using OpenTyrian.Core;

namespace OpenTyrian.Core.Tests;

internal static class HelpTextRendererTests
{
    public static void Run(string? executable = null)
    {
        Check(HelpTextRenderer.Layout("AB"u8, 2)[0].Text.SequenceEqual("AB"u8.ToArray()), "Short help line differs.");
        var narrow = HelpTextRenderer.Layout("AB"u8, 1);
        Check(narrow.Count == 2 && narrow[0].Text.Length == 0 && narrow[1].Text.SequenceEqual("B"u8.ToArray()),
            "Native narrow-word truncation differs.");
        Check(HelpTextRenderer.Layout("\0ignored"u8, 20).Count == 0, "NUL-terminated empty help differs.");
        foreach (var input in new[] { new byte[] { (byte)'A' }, Enumerable.Repeat((byte)'A', 256).ToArray() })
        {
            try
            {
                HelpTextRenderer.Layout(input, 20);
                throw new InvalidOperationException("Native infinite-loop help length accepted.");
            }
            catch (InvalidDataException) { }
        }

        var renderer = new HelpTextRenderer(new FontRenderer(new MainShapeTables()))
        {
            Color = 3,
            Shade = TextShade.Partial,
            Brightness = 5,
            VerticalHeight = 9
        };
        using var warnings = new StringWriter();
        var surface = new IndexedSurface(320, 200, 320, warnings);
        renderer.DrawBox(surface, 0, 0, ""u8, 20);
        Check(renderer.Color == 3 && renderer.Shade == TextShade.Partial, "Empty help reset state.");
        renderer.DrawBox(surface, 0, 0, "  "u8, 20);
        Check(renderer.Color == 12 && renderer.Shade == TextShade.Full && renderer.Brightness == 5 &&
            renderer.VerticalHeight == 9, "Help style reset differs.");
        var data = new HelpTextData();
        data.Help[0][0] = data.Help[0][1] = (byte)' ';
        renderer.DrawHelp(surface, 0, 0, data, 1, 20);
        try
        {
            renderer.DrawHelp(surface, 0, 0, data, 0, 20);
            throw new InvalidOperationException("Invalid help index accepted.");
        }
        catch (ArgumentOutOfRangeException) { }

        if (executable is null) return;
        var path = Path.Combine(Path.GetTempPath(), $"opentyrian-help-{Guid.NewGuid():N}.txt");
        try
        {
            foreach (var message in new[]
            {
                "", "AB", "A B", "ABC DEF GHI", " ABC DEF", "A  B   C ", "ABCDEFGHIJKLMN", "~~ \x80\xFF text",
                new string('A', 254), new string('A', 255), string.Concat(Enumerable.Repeat("word ", 51))
            })
            {
                var bytes = Encoding.Latin1.GetBytes(message);
                File.WriteAllBytes(path, bytes);
                foreach (var width in new uint[] { 0, 1, 2, 3, 5, 10, 40, 254, 255, uint.MaxValue })
                {
                    if (bytes.Length == 255 && width >= 255) continue; // Native strlcpy size zero leaves its buffer uninitialized.
                    var expected = string.Concat(HelpTextRenderer.Layout(bytes, width)
                        .Select(line => $"{line.Y}={Convert.ToHexString(line.Text)}\n"));
                    var start = new ProcessStartInfo(executable) { RedirectStandardOutput = true, RedirectStandardError = true };
                    start.ArgumentList.Add(path);
                    start.ArgumentList.Add(width.ToString(CultureInfo.InvariantCulture));
                    using var process = Process.Start(start) ?? throw new InvalidOperationException("Cannot start help oracle.");
                    var output = process.StandardOutput.ReadToEndAsync();
                    var error = process.StandardError.ReadToEndAsync();
                    if (!process.WaitForExit(30000))
                    {
                        process.Kill(entireProcessTree: true);
                        throw new TimeoutException("Help wrapping oracle timed out.");
                    }

                    var actual = output.GetAwaiter().GetResult().Replace("\r\n", "\n", StringComparison.Ordinal);
                    Check(process.ExitCode == 0 && actual == expected,
                        $"Help wrapping differs at width {width}, length {bytes.Length}: {expected} / {actual} {error.GetAwaiter().GetResult()}");
                }
            }
        }
        finally { File.Delete(path); }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
