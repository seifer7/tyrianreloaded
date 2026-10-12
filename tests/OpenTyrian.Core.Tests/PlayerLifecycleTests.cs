using System.Diagnostics;
using System.Text;
using OpenTyrian.Core;

namespace OpenTyrian.Core.Tests;

internal static class PlayerLifecycleTests
{
    public static void Run(string? executable = null)
    {
        var definitions = new ItemDefinitions();
        definitions.ParseBundle(Encoding.Latin1.GetBytes(LevelEventTests.Fixture()), "lifecycle.tyrr");
        var items = ItemDatabase.FromDefinitions(definitions);
        var trace = new StringBuilder();
        foreach (var pitch in new[] { 320, 327 })
        {
            var hud = new IndexedSurface(320, 200, pitch, TextWriter.Null);
            for (var mode = 0; mode < 2048; mode++)
                for (var which = 0; which < 2; which++)
                {
                    for (var i = 0; i < hud.Pixels.Length; i++) hud.Pixels.Span[i] = unchecked((byte)(i * 17 + i / 7));
                    var players = new[] { new PlayerState(0), new PlayerState(1) };
                    for (var i = 0; i < 2; i++)
                    {
                        players[i].Lives = 11; players[i].Armor = mode % 3 != 0 ? 40u : 0u;
                        players[i].InitialArmor = 57; players[i].Shield = 9; players[i].ShieldMax = 11;
                        players[i].X = 80 + i * 40; players[i].Y = 170 + i * 13;
                        players[i].IsAlive = (mode & 16) != 0; players[i].ExplodingTicks = (mode & 64) != 0 ? 2u : 0u;
                    }
                    players[which].Lives = (byte)((mode & 128) != 0 ? 3 : 1);
                    var effects = new SceneEffects(new byte[8]);
                    var combat = new PlayerCombat(players, effects, hud)
                    {
                        TwoPlayers = (mode & 1) != 0,
                        Galaga = (mode & 4) != 0,
                        ConstantDie = (mode & 32) != 0,
                        Linked = true,
                        LevelEnd = 17,
                        LevelEndEffectsWait = (ushort)((mode & 1024) != 0 ? 2 : 0)
                    };
                    var level = new LevelEvents(items, new(items, TextWriter.Null), players, new(), effects)
                    {
                        OnePlayerAction = (mode & 2) != 0,
                        AllPlayersGone = (mode & 256) != 0,
                        ReallyEndLevel = true,
                        EndLevel = true
                    };
                    byte[] positions = [7, 8];
                    MersenneTwister.Seed((uint)(98765 + mode));
                    var result = combat.UpdateDeath(players[which], (byte)(which + 1), (byte)((mode & 8) != 0 ? 4 : 1),
                        (mode & 512) != 0, level, positions);
                    trace.AppendLine($"case:{pitch},{mode},{which}");
                    trace.AppendLine($"state:{Bit(result)},{Bit(combat.TwoPlayers)},{Bit(level.ReallyEndLevel)},{Bit(level.EndLevel)},{Bit(combat.Linked)}," +
                        $"{combat.LevelEnd},{combat.LevelEndEffectsWait},{positions[0]},{positions[1]},{players[0].Lives},{players[1].Lives}");
                    for (var i = 0; i < 2; i++)
                    {
                        var player = players[i];
                        trace.AppendLine($"player:{i},{Bit(player.IsAlive)},{player.ExplodingTicks},{player.InvulnerableTicks},{player.Armor}," +
                            $"{player.Shield},{player.PurpleBallsNeeded},{player.Y}");
                    }
                    foreach (var explosion in effects.Explosions.Take(8))
                        trace.AppendLine($"explosion:{explosion.Lifetime},{explosion.X},{explosion.Y},{explosion.Sprite}," +
                            $"{Bit(explosion.FixedPosition)},{Bit(explosion.FollowPlayer)}");
                    uint hash = 2166136261;
                    foreach (var pixel in hud.Pixels.Span) hash = unchecked((hash ^ pixel) * 16777619);
                    trace.AppendLine($"pixels:{hash}");
                    trace.AppendLine($"sounds:{effects.SoundQueue[5]},{effects.SoundQueue[6]}");
                    trace.AppendLine($"rng:{MersenneTwister.NextUInt32()}");
                }
        }
        if (executable == null) return;
        using var process = Process.Start(new ProcessStartInfo(executable) { RedirectStandardOutput = true, RedirectStandardError = true })
            ?? throw new InvalidOperationException("Cannot start player lifecycle oracle.");
        var output = process.StandardOutput.ReadToEndAsync(); var errors = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(60000)) { process.Kill(entireProcessTree: true); throw new TimeoutException("Player lifecycle oracle timed out."); }
        var actual = trace.ToString().Replace("\r\n", "\n", StringComparison.Ordinal);
        var expected = output.GetAwaiter().GetResult().Replace("\r\n", "\n", StringComparison.Ordinal);
        if (process.ExitCode != 0 || actual != expected)
        {
            var a = expected.Split('\n'); var b = actual.Split('\n');
            var index = Enumerable.Range(0, Math.Min(a.Length, b.Length)).FirstOrDefault(i => a[i] != b[i], -1);
            throw new InvalidOperationException($"Player lifecycle differs at {index}: {a.ElementAtOrDefault(index)} / {b.ElementAtOrDefault(index)}; {errors.GetAwaiter().GetResult()}");
        }
    }
    private static int Bit(bool value) => value ? 1 : 0;
}
