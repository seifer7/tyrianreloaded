using System.Diagnostics;
using OpenTyrian.Core;

namespace OpenTyrian.Core.Tests;

internal static class BackgroundTests
{
    public static void Run(string? executable = null)
    {
        using var warnings = new StringWriter();
        foreach (var pitch in new[] { 320, 327 })
        {
            for (var mode = 0; mode < 10; mode++)
            {
                var destination = new IndexedSurface(320, 200, pitch, warnings);
                var source = new IndexedSurface(320, 200, pitch + 3, warnings);
                for (var index = 0; index < destination.Pixels.Length; index++)
                {
                    destination.Pixels.Span[index] = unchecked((byte)(index * 73 + 19));
                }

                for (var index = 0; index < source.Pixels.Length; index++)
                {
                    source.Pixels.Span[index] = unchecked((byte)(index * 37 + 11));
                }

                var renderer = new BackgroundRenderer();
                switch (mode)
                {
                    case 0: BackgroundRenderer.Darken(destination, ushort.MaxValue); break;
                    case 1: BackgroundRenderer.Lava(destination, source); break;
                    case 2:
                        renderer.SmoothieData[1] = 9;
                        renderer.Water(destination, source);
                        break;
                    case 3: BackgroundRenderer.Blur(destination, source, iced: true); break;
                    case 4: BackgroundRenderer.Blur(destination, source); break;
                    case 5:
                    case 6:
                        renderer.FiltrationAvailable = true;
                        renderer.ExplosionTransparent = true;
                        renderer.FilterScreen(destination, mode == 5 ? (sbyte)13 : (sbyte)-99, mode == 5 ? (sbyte)-8 : (sbyte)20);
                        break;
                    case 7:
                        MersenneTwister.Seed(5489);
                        var stars = new Starfield();
                        stars.Initialize(pitch);
                        Check(stars.Stars.Length == 100 && stars.Stars.ToArray().All(star => star.Speed is >= 2 and <= 4 && star.Color is >= 0x90 and <= 0x9F),
                            "Starfield initialization differs.");
                        destination.Pixels.Span.Clear();
                        for (var frame = 0; frame < 200; frame++)
                        {
                            stars.UpdateAndDraw(destination, frame % 7 - 3);
                        }

                        Check(destination.Pixels.Span.ContainsAnyExcept((byte)0), "Starfield did not draw.");
                        break;
                    default:
                        var tile = Enumerable.Range(0, 672).Select(index => unchecked((byte)(index * 11))).ToArray();
                        var tiles = Enumerable.Range(0, 12).Select(index => index % 3 == 0 ? null : tile).ToArray();
                        BackgroundRenderer.BlitRow(destination, mode == 8 ? -27 : 19, mode == 8 ? -2 : 190, tiles, mode == 9);
                        break;
                }

                if (mode is 3 or 4)
                {
                    var expectedHue = mode == 3 ? 0x80 : source.Pixels.Span[0] & 0xF0;
                    Check(destination.Pixels.Span[0] == (expectedHue | 7), "Blur hue/brightness differs.");
                }

                if (executable is not null)
                {
                    var native = Execute(executable, mode, pitch);
                    Check(native.AsSpan().SequenceEqual(destination.Pixels.Span), $"Native background pixels differ: mode {mode}, pitch {pitch}.");
                }
            }
        }

        TestLayers(warnings);
        TestFade(warnings);
    }

    private static void TestLayers(TextWriter warnings)
    {
        var renderer = new BackgroundRenderer();
        var surface = new IndexedSurface(320, 200, 322, warnings);
        var tile = Enumerable.Repeat((byte)0xBE, 672).ToArray();
        foreach (var layer in new[] { renderer.First, renderer.Second, renderer.Third })
        {
            layer.Tiles = Enumerable.Repeat<byte[]?>(tile, 200).ToArray();
            layer.MapOffset = 27;
            layer.MapY = 20;
        }

        surface.Pixels.Span.Fill(0xCD);
        renderer.DrawFirst(surface);
        Check(surface.Pixels.Span[0] == 0xBE && surface.Pixels.Span[300] == 0 &&
            surface.Pixels.Span[321] == 0xCD, "Background clear or padding differs.");
        renderer.SecondBackground = 0;
        renderer.SecondDelay = 1;
        renderer.SecondDelayMax = 3;
        renderer.Second.Position = 27;
        renderer.DrawSecond(surface);
        Check(renderer.Second.Move == 1 && renderer.Second.Position == 0 && renderer.Second.MapY == 19 &&
            renderer.Second.MapOffset == 13 && renderer.SecondDelay == 3, "Delayed second-layer scrolling differs.");
        renderer.DrawSecond(surface);
        Check(renderer.Second.Move == 0 && renderer.SecondDelay == 2 && renderer.Second.Position == 0, "Second-layer subpixel delay differs.");
        renderer.SecondDelay = 0;
        renderer.DrawSecond(surface);
        Check(renderer.SecondDelay == 255, "Second-layer delay byte wrap differs.");
        renderer.SecondDelay = 2;
        renderer.DrawSecond(surface, blend: true);
        Check(surface.Pixels.Span[0] == 0xBE, "Blended background did not draw while disabled.");
        renderer.Third.Position = 26;
        renderer.Third.Move = 3;
        renderer.DrawThird(surface);
        Check(renderer.Third.Position == 1 && renderer.Third.MapOffset == 12 && renderer.Third.MapY == 19, "Third-layer movement order differs.");
        renderer.Smoothies[0] = true;
        renderer.CheckSmoothies(2);
        Check(!renderer.AnySmoothies, "Smoothies enabled below processor threshold.");
        renderer.CheckSmoothies(3);
        Check(renderer.AnySmoothies, "Smoothies processor gate differs.");
        renderer.Smoothies[0] = false;
        renderer.Smoothies[4] = true;
        renderer.CheckSmoothies(2);
        Check(renderer.AnySmoothies, "Secondary smoothies gate differs.");
        var stars = new Starfield();
        MersenneTwister.Seed(1);
        stars.Initialize(320);
        var occupied = new IndexedSurface(320, 200, 320, warnings);
        occupied.Pixels.Span.Fill(1);
        stars.UpdateAndDraw(occupied, 2000);
        Check(!occupied.Pixels.Span.ContainsAnyExcept((byte)1), "Starfield overwrote nonzero pixels.");
    }

    private static void TestFade(TextWriter warnings)
    {
        var renderer = new BackgroundRenderer
        {
            FilterFade = true,
            FilterFadeStart = true,
            Brightness = -14,
            BrightnessChange = -1,
            NewLevelFilter = 7
        };
        var surface = new IndexedSurface(320, 200, 320, warnings);
        renderer.FilterScreen(surface, -99, -99);
        Check(renderer.Brightness == -15 && renderer.BrightnessChange == 1 && !renderer.FilterFadeStart &&
            renderer.LevelFilter == 7, "Filter fade turning point differs.");
        for (var frame = 0; frame < 15; frame++)
        {
            renderer.FilterScreen(surface, -99, -99);
        }

        Check(!renderer.FilterFade && renderer.Brightness == -99, "Filter fade completion differs.");
    }

    private static byte[] Execute(string executable, int mode, int pitch)
    {
        var start = new ProcessStartInfo(executable) { RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add(mode.ToString(System.Globalization.CultureInfo.InvariantCulture));
        start.ArgumentList.Add(pitch.ToString(System.Globalization.CultureInfo.InvariantCulture));
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start background oracle.");
        var output = process.StandardOutput.ReadToEndAsync();
        var errors = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(30000))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException("Background oracle timed out.");
        }

        Check(process.ExitCode == 0, $"Background oracle failed: {errors.GetAwaiter().GetResult()}");
        return Convert.FromHexString(output.GetAwaiter().GetResult().Trim());
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
