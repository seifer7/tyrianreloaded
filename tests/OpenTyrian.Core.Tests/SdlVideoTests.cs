using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace OpenTyrian.Core.Tests;

internal static class SdlVideoTests
{
    internal static void Run(string native)
    {
        if (Environment.GetEnvironmentVariable("SDL_VIDEODRIVER") != "dummy")
            throw new InvalidOperationException("SDL video checks require SDL_VIDEODRIVER=dummy to avoid visible windows.");
        var output = new StringBuilder();
        using (var video = new SdlVideoDevice(TextWriter.Null, fullscreenDisplay: -1))
        {
            var colors = Enumerable.Range(0, 256).Select(i => new PaletteColor((byte)i, (byte)(255 - i), (byte)(i ^ 85))).ToArray();
            video.Palette.SetPalette(colors, 0, 255);
            for (var i = 0; i < 64000; i++) video.Primary.Pixels.Span[i] = unchecked((byte)(i * 17 + i / 320 * 3));
            var stage = 0;
            void State()
            {
                var (w, h) = video.WindowSize; var r = video.Layout.Output;
                var hash = Hash(MemoryMarshal.AsBytes(video.ReadPixels().AsSpan()));
                output.AppendLine($"frame:{stage++},{w},{h},{video.FullscreenDisplay},{r.X},{r.Y},{r.Width},{r.Height},{hash}");
            }
            foreach (var scaler in VideoScaler.Scalers.Select(s => s.Name))
            {
                Check(video.SetScalerByName(scaler), "Native scaler could not be selected.");
                for (var mode = 0; mode < 4; mode++)
                {
                    video.Layout.Mode = (ScalingMode)mode;
                    video.SetWindowSize(853, 517); video.Present(); State();
                }
                video.SetWindowSize(17, 23); video.WindowResized(); video.Present(); State();
            }
            video.ReinitializeFullscreen(999); video.Present(); State();
            video.ToggleFullscreen(); video.Present(); State();
            SdlVideoDevice.Clear(video.Primary); video.Present(); State();
            CompareNative(native, output.ToString());
            Check(!ReferenceEquals(video.Primary, video.Secondary) && !ReferenceEquals(video.Primary, video.GameScreen), "Game video surfaces share ownership.");
            Check(!video.SetScalerByName("none") && video.ScalerName == "hq4x", "Unknown scaler changed state.");
            Throws<ArgumentOutOfRangeException>(() => video.ReinitializeFullscreen(-2));
            Throws<ArgumentException>(() => video.Present(new IndexedSurface(1, 1, 1, TextWriter.Null)));
            foreach (var name in new[] { "Scale2x", "Scale3x", "hq2x", "hq3x", "hq4x" })
            {
                video.SetScalerByName(name); video.Layout.Mode = ScalingMode.Center;
                video.Secondary.Pixels.Span.Fill(17); video.Present(video.Secondary);
                var pixel = video.Palette.Rgb.Span[17];
                if (name.StartsWith("hq", StringComparison.Ordinal)) pixel &= 0xfcfcfc;
                Check(video.ReadPixels().All(p => (p & 0xffffff) == pixel), "Managed edge-aware scaler was not uploaded to SDL texture.");
            }
            video.Dispose(); video.Dispose();
            Throws<ObjectDisposedException>(video.Present);
        }
        using var reopened = new SdlVideoDevice(TextWriter.Null, -1);
        reopened.Present();
    }

    private static uint Hash(ReadOnlySpan<byte> bytes)
    {
        var hash = 2166136261u;
        foreach (var value in bytes) hash = unchecked((hash ^ value) * 16777619u);
        return hash;
    }
    private static void CompareNative(string executable, string expected)
    {
        using var process = Process.Start(new ProcessStartInfo(Path.GetFullPath(executable))
        { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true })
            ?? throw new InvalidOperationException("Could not start native SDL video oracle.");
        var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(30000)) { process.Kill(entireProcessTree: true); throw new TimeoutException("Native SDL video oracle timed out."); }
        var actual = stdout.GetAwaiter().GetResult().Replace("\r\n", "\n", StringComparison.Ordinal);
        expected = expected.Replace("\r\n", "\n", StringComparison.Ordinal);
        Check(process.ExitCode == 0, $"Native SDL video failed ({process.ExitCode}): {stderr.GetAwaiter().GetResult()}");
        if (actual == expected) return;
        var a = actual.Split('\n'); var b = expected.Split('\n');
        var line = Enumerable.Range(0, Math.Min(a.Length, b.Length)).FirstOrDefault(i => a[i] != b[i], -1);
        throw new InvalidOperationException(line < 0 ? "SDL frame trace length differs." : $"SDL frame differs at {line + 1}: native={a[line]}, managed={b[line]}.");
    }
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
    private static void Throws<T>(Action action) where T : Exception
    {
        try { action(); } catch (T) { return; }
        throw new InvalidOperationException($"Expected {typeof(T).Name}.");
    }
}
