using System.Diagnostics;
using System.Text;
using OpenTyrian.Core;

namespace OpenTyrian.Core.Tests;

internal static class EnemyCollisionTests
{
    public static void Run(string? executable = null)
    {
        var definitions = new ItemDefinitions();
        definitions.ParseBundle(Encoding.Latin1.GetBytes(LevelEventTests.Fixture() +
            "[enemy:collision0]\nesize=0\n[enemy:collision1]\nesize=1\n[enemy:collision2]\nesize=0\n"), "collision.tyrr");
        var items = ItemDatabase.FromDefinitions(definitions);
        var trace = new StringBuilder();
        foreach (var pitch in new[] { 320, 327 })
            for (var mode = 0; mode < 4096; mode++)
            {
                MersenneTwister.Seed((uint)(mode + 7));
                var hud = new IndexedSurface(320, 200, pitch, TextWriter.Null);
                for (var i = 0; i < hud.Pixels.Length; i++) hud.Pixels.Span[i] = (byte)(i * 17 + i / 7);
                var players = new[] { new PlayerState(0), new PlayerState(1) };
                var effects = new SceneEffects(new byte[8]);
                var combat = new PlayerCombat(players, effects, hud)
                {
                    TwoPlayers = (mode & 1) != 0,
                    Galaga = (mode & 2) != 0,
                    Cheating = (mode & 4) != 0,
                    Linked = (mode & 8) != 0,
                    LevelTimer = true,
                    MusicVolume = 180,
                    TemporaryVolume = 42
                };
                for (var i = 0; i < 2; i++)
                {
                    var player = players[i];
                    player.X = 80 + i * 40; player.Y = 100 - i * 40; player.IsAlive = true;
                    player.Shield = (uint)(mode % 29 + i); player.Armor = (uint)(mode % 32 + i); player.ShieldMax = 30;
                    player.XVelocity = mode % 13 - 6; player.YVelocity = mode % 15 - 7;
                }
                var enemies = new EnemySystem(items, TextWriter.Null);
                for (var i = 0; i < 100; i++)
                {
                    var enemy = enemies.Enemies[i]; enemy.Availability = (byte)(i % 3);
                    enemy.X = (short)(i * 3 - 20); enemy.Y = (short)(mode % 221 - 20);
                    enemy.MapOffset = (ushort)(mode % 41); enemy.Type = (ushort)items.Index(ItemType.Enemy, $"collision{i % 3}");
                    enemy.XSpeed = (sbyte)((mode + i * 17) % 256 - 128); enemy.YSpeed = (sbyte)(127 - (mode + i * 31) % 256);
                    enemy.Armor = (byte)((mode + i * 3) % 256); enemy.LinkNumber = (byte)((mode + i * 17) % 256);
                    enemy.ExplosionNumber = (byte)(mode % 16); enemy.Ground = (mode & 16) != 0;
                }
                var index = mode % 100; var which = mode % 2;
                if (mode % 3 == 0) enemies.DamageOnCollision(players[which], index, unchecked((byte)(mode * 17)), combat);
                else if (mode % 3 == 1) enemies.DestroyOnCollision(index, enemies.Enemies[index].LinkNumber, effects);
                else enemies.CollectArmorPickup(players[which], index, (short)(20001 + mode % 9999), combat);
                trace.AppendLine($"case:{mode},{pitch}");
                trace.AppendLine($"combat:{Bit(combat.LevelTimer)},{combat.LevelEnd},{combat.TemporaryVolume}");
                foreach (var player in players) trace.AppendLine($"player:{player.Shield},{player.Armor},{Bit(player.IsAlive)}," +
                    $"{player.ExplodingTicks},{player.XVelocity},{player.YVelocity}");
                foreach (var enemy in enemies.Enemies) trace.AppendLine($"enemy:{enemy.Availability},{enemy.LinkNumber},{enemy.Armor}");
                foreach (var e in effects.Explosions)
                    trace.AppendLine($"E:{e.Lifetime},{e.X},{e.Y},{e.DeltaX},{e.DeltaY},{Bit(e.FixedPosition)},{Bit(e.FollowPlayer)},{e.Sprite}");
                foreach (var e in effects.Repeating) trace.AppendLine($"R:{e.Delay},{e.Lifetime},{e.X},{e.Y},{Bit(e.Big)}");
                trace.AppendLine("sounds:" + string.Concat(effects.SoundQueue.Select(value => $"{value},")));
                uint hash = 2166136261;
                foreach (var value in hud.Pixels.Span) hash = unchecked((hash ^ value) * 16777619);
                trace.AppendLine($"pixels:{hash}"); trace.AppendLine($"rng:{MersenneTwister.NextUInt32()}");
            }
        TestFailures(items);
        if (executable is null) return;
        using var process = Process.Start(new ProcessStartInfo(executable)
        { RedirectStandardOutput = true, RedirectStandardError = true }) ?? throw new InvalidOperationException("Cannot start collision oracle.");
        var output = process.StandardOutput.ReadToEndAsync(); var errors = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(120000)) { process.Kill(entireProcessTree: true); throw new TimeoutException("Collision oracle timed out."); }
        var expected = output.GetAwaiter().GetResult().Replace("\r\n", "\n", StringComparison.Ordinal);
        var actual = trace.ToString().Replace("\r\n", "\n", StringComparison.Ordinal);
        if (process.ExitCode != 0 || actual != expected)
        {
            var a = expected.Split('\n'); var b = actual.Split('\n');
            var index = Enumerable.Range(0, Math.Min(a.Length, b.Length)).FirstOrDefault(i => a[i] != b[i], -1);
            throw new InvalidOperationException($"Enemy collisions differ at {index}: {a.ElementAtOrDefault(index)} / " +
                $"{b.ElementAtOrDefault(index)}; {errors.GetAwaiter().GetResult()}");
        }
    }

    private static void TestFailures(ItemDatabase items)
    {
        var enemies = new EnemySystem(items, TextWriter.Null);
        var effects = new SceneEffects(new byte[8]);
        var players = new[] { new PlayerState(0), new PlayerState(1) };
        var combat = new PlayerCombat(players, effects, new(320, 200, 320, TextWriter.Null));
        Throws<ArgumentOutOfRangeException>(() => enemies.DestroyOnCollision(100, 0, effects));
        Throws<ArgumentOutOfRangeException>(() => enemies.DamageOnCollision(players[0], -1, 1, combat));
        Throws<ArgumentOutOfRangeException>(() => enemies.CollectArmorPickup(players[0], 0, 20000, combat));
        enemies.Enemies[0].Type = ushort.MaxValue;
        enemies.Enemies[0].Availability = 0;
        Throws<InvalidDataException>(() => enemies.DestroyOnCollision(0, 0, effects));
    }
    private static void Throws<T>(Action action) where T : Exception
    {
        try { action(); } catch (T) { return; }
        throw new InvalidOperationException($"Expected {typeof(T).Name}.");
    }
    private static int Bit(bool value) => value ? 1 : 0;
}
