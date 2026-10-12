using System.Diagnostics;
using System.Globalization;
using System.Text;
using OpenTyrian.Core;

namespace OpenTyrian.Core.Tests;

internal static class SceneEffectsTests
{
    public static void Run(string? executable = null)
    {
        var effects = new SceneEffects(new byte[8]);
        effects.SetupExplosion(10, 20, -1, 6, true, true);
        effects.SetupExplosion(10, 20, -1, 98, false, false);
        Check(effects.Explosions[0].X == 12 && effects.Explosions[0].Y == 32 &&
            effects.Explosions[1].X == 10 && effects.Explosions[1].Y == 20 &&
            effects.Explosions[0].Sprite == effects.Explosions[1].Sprite, "Smoke coordinate special cases differ.");
        effects.FollowX = -2; effects.FollowY = 3; effects.ExplosionMove = 1;
        var draws = new List<(int, int, uint, bool)>();
        effects.UpdateExplosions((x, y, frame, blend) => draws.Add((x, y, frame, blend)));
        Check(draws[0] == (10, 34, 134u, false) && draws[1] == (10, 20, 135u, false),
            "Fixed/following explosion animation and movement differ.");
        effects.SetupLarge(false, 12, -5, 20);
        Check(effects.Repeating[0].X == unchecked((uint)-5) && effects.Repeating[0].Lifetime == 2 &&
            effects.Repeating[0].Big, "Repeating explosion unsigned coordinates differ.");
        using var warnings = new StringWriter();
        var surface = new IndexedSurface(3, 3, 5, warnings);
        var pixel = effects.SuperPixels[0];
        pixel.X = pixel.Y = 1; pixel.Z = 15; pixel.Color = 0xA0;
        effects.DrawSuperPixels(surface);
        Check(surface.Pixels.Span[6] == 0xA7 && surface.Pixels.Span[5] == 0xA3 &&
            surface.Pixels.Span[7] == 0xA3 && surface.Pixels.Span[1] == 0xA3 &&
            surface.Pixels.Span[11] == 0xA3 && pixel.Z == 14, "Superpixel cross blending differs.");
        try
        {
            effects.SpawnSuperPixels(0, 0, 256, 1, 0);
            throw new InvalidOperationException("Native infinite superpixel loop accepted.");
        }
        catch (ArgumentOutOfRangeException) { }
        if (executable is null) return;
        foreach (var mode in new[] { 0, 1 })
            foreach (var pitch in new[] { 320, 327 })
            {
                var expected = Execute(mode, pitch);
                var start = new ProcessStartInfo(executable) { RedirectStandardOutput = true, RedirectStandardError = true };
                start.ArgumentList.Add(mode.ToString(CultureInfo.InvariantCulture));
                start.ArgumentList.Add(pitch.ToString(CultureInfo.InvariantCulture));
                using var process = Process.Start(start) ?? throw new InvalidOperationException("Cannot start scene effects oracle.");
                var output = process.StandardOutput.ReadToEndAsync();
                var errors = process.StandardError.ReadToEndAsync();
                if (!process.WaitForExit(30000))
                {
                    process.Kill(entireProcessTree: true);
                    throw new TimeoutException("Scene effects oracle timed out.");
                }
                var actual = output.GetAwaiter().GetResult().Replace("\r\n", "\n", StringComparison.Ordinal);
                var firstDifference = actual.Split('\n').Zip(expected.Split('\n')).FirstOrDefault(pair => pair.First != pair.Second);
                Check(process.ExitCode == 0 && actual == expected,
                    $"Scene effects mode {mode}, pitch {pitch} differ: {firstDifference.First} / {firstDifference.Second} {errors.GetAwaiter().GetResult()}");
            }
    }

    private static string Execute(int mode, int pitch)
    {
        using var warnings = new StringWriter();
        var surface = new IndexedSurface(320, 200, pitch, warnings);
        for (var i = 0; i < surface.Pixels.Length; i++) surface.Pixels.Span[i] = unchecked((byte)(i * 17 + i / 7));
        var effects = new SceneEffects(new byte[8]);
        MersenneTwister.Seed(12345);
        for (var type = 0; type < 53; type++)
            effects.SetupExplosion(type * 3 - 10, type * 3 - 15, type % 3 - 1, (uint)type, type % 2 != 0, type % 3 == 0);
        effects.SetupExplosion(10, 10, 0, 98, false, false);
        effects.SetupExplosion(1, -16, 0, 0, false, false);
        effects.SetupExplosion(1, 190, 0, 0, false, false);
        effects.SetupLarge(false, 12, -5, 5);
        effects.SetupLarge(true, 3, 130, 160);
        effects.SetupLarge(false, 1, 10, -1);
        effects.SpawnSuperPixels(0, 0, 150, 8, 0xA0);
        effects.SpawnSuperPixels(319, 199, 40, 4, 0xF8);
        effects.SpawnSuperPixels(100, 100, 10, 0, 0x40);
        if (mode == 1)
            for (var i = 0; i < 500; i++) effects.SetupLarge(i % 2 != 0, 15, 80, 50);
        var output = new StringBuilder();
        Dump(effects, surface, output);
        for (var frame = 0; frame < 35; frame++)
        {
            effects.BackgroundMove = (ushort)(frame % 3);
            effects.ExplosionMove = (ushort)(frame % 2);
            effects.FollowX = (short)(frame % 2 != 0 ? 3 : -2);
            effects.FollowY = -1;
            effects.Transparent = frame % 2 != 0;
            if (frame == 8) effects.SpawnSuperPixels(65535, 65535, 255, 255, 0xF0);
            effects.UpdateExplosions((x, y, sprite, blend) => output.Append($"D:{x}:{y}:{sprite}:{(blend ? 1 : 0)}\n"));
            effects.DrawSuperPixels(surface);
            Dump(effects, surface, output);
        }
        output.Append($"rng:{MersenneTwister.NextUInt32()}\n");
        return output.ToString();
    }

    private static void Dump(SceneEffects effects, IndexedSurface surface, StringBuilder output)
    {
        output.Append($"state:{effects.LastSuperPixel}:{(effects.EnemyStillExploding ? 1 : 0)}");
        foreach (var sound in effects.SoundQueue) output.Append($",{sound}");
        output.Append('\n');
        for (var i = 0; i < effects.Explosions.Length; i++)
        {
            var e = effects.Explosions[i];
            output.Append($"E:{i}={e.Lifetime},{e.X},{e.Y},{e.DeltaX},{e.DeltaY},{(e.FixedPosition ? 1 : 0)},{(e.FollowPlayer ? 1 : 0)},{e.Sprite}\n");
        }
        for (var i = 0; i < effects.Repeating.Length; i++)
        {
            var e = effects.Repeating[i];
            output.Append($"R:{i}={e.Delay},{e.Lifetime},{e.X},{e.Y},{(e.Big ? 1 : 0)}\n");
        }
        for (var i = 0; i < effects.SuperPixels.Length; i++)
        {
            var p = effects.SuperPixels[i];
            output.Append($"P:{i}={p.X},{p.Y},{p.Z},{p.DeltaX},{p.DeltaY},{p.Color}\n");
        }
        uint hash = 2166136261;
        foreach (var pixel in surface.Pixels.Span) hash = unchecked((hash ^ pixel) * 16777619);
        output.Append($"pixels:{hash}\n");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
