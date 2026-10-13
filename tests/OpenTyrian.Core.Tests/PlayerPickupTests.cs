using System.Diagnostics;
using System.Text;
using OpenTyrian.Core;

namespace OpenTyrian.Core.Tests;

internal static class PlayerPickupTests
{
    private static readonly short[] Values = [1, -1, -2, -3, -4, -5, 0, 50, 9999, -6, 10001, 20001, 29999, 30000,
        30001, 30003, 30031, 31001, 32001, 32002, 32015, 32101];

    public static void Run(string? executable = null)
    {
        var fixture = LevelEventTests.Fixture();
        foreach (var category in new[] { "weapon", "sidekick", "special" })
            for (var i = 0; i <= 45; i++) fixture += $"[{category}:{i}]\nname={category}{i}\n";
        for (var i = 0; i < 120; i++) fixture += $"[shot:pickup{i}]\nmulti=1\nmax=1\ndel=5\nsg=7\nsy=2\nsx=3\nshotrepeat=4\n";
        var definitions = new ItemDefinitions(); definitions.ParseBundle(Encoding.Latin1.GetBytes(fixture), "pickups.tyrr");
        var items = ItemDatabase.FromDefinitions(definitions);
        TestFailures(items);
        var trace = new StringBuilder();
        for (var mode = 0; mode < 8192; mode++)
        {
            MersenneTwister.Seed((uint)(mode + 7));
            var hud = new IndexedSurface(320, 200, 327, TextWriter.Null); hud.Pixels.Span.Fill(69);
            var players = new[] { new PlayerState(0), new PlayerState(1) };
            var effects = new SceneEffects(new byte[8]);
            var combat = new PlayerCombat(players, effects, hud)
            {
                TwoPlayers = (mode & 1) != 0,
                Galaga = (mode & 2) != 0,
                Linked = (mode & 4) != 0,
                Cheating = (mode & 1024) != 0,
                LevelTimer = true,
                TemporaryVolume = 42,
                MusicVolume = 180
            };
            var enemies = new EnemySystem(items, TextWriter.Null) { CubeCount = 65535 };
            var level = new LevelEvents(items, enemies, players, new(), effects) { OnePlayerAction = (mode & 8) != 0 };
            var shots = new PlayerShots(items.Shots, items.Weapons, players, new ShotEffects(), enemies.Enemies, effects.SoundQueue) { Power = 100 };
            Array.Fill(shots.MultiPosition, (byte)3); Array.Fill(shots.Repeat, (byte)4);
            var weapons = new PlayerWeapons(shots, items, combat);
            var pickups = new PlayerPickups(players, enemies, combat, shots, items, level, effects,
                Enumerable.Range(0, 80).Select(i => $"text{i}").ToArray(), ["", "", "", "got"])
            {
                NetworkGame = (mode & 16) != 0,
                LocalPlayer = (byte)((mode & 32) != 0 ? 2 : 1),
                LocalName = (mode & 64) != 0 ? "local" : "",
                OpponentName = (mode & 128) != 0 ? "opponent" : "",
                BonusLevel = (mode & 256) != 0,
                NextLevel = 17,
                DisplayTime = 19,
                ArcadeMode = (byte)((mode & 512) != 0 ? mode % 7 + 1 : 0),
                DamageRate = (byte)(mode % 13)
            };
            for (var i = 0; i < 2; i++)
            {
                var p = players[i]; p.IsDragonwing = i == 1; p.IsAlive = true; p.X = 100 + i * 4; p.Y = 80 + i * 4;
                p.Cash = (mode & 2048) != 0 ? (OperatingSystem.IsWindows() || IntPtr.Size == 4 ? uint.MaxValue : ulong.MaxValue) - 50ul : 100;
                p.Items.Weapons[0].Id = (byte)items.KnownIndex(KnownItem.WeaponFrontNortshipSuperPulse);
                p.Items.Weapons[1].Id = (byte)items.KnownIndex(KnownItem.WeaponRearNortshipSpreader);
                if ((mode & 1024) != 0) p.Items.Weapons[0].Id = 0;
                p.Items.Weapons[0].Power = (byte)(mode % 12); p.Items.Weapons[1].Power = (byte)((mode + 1) % 12);
                p.PurpleBallsNeeded = (uint)(mode % 5); p.WeaponMode = 3; p.Superbombs = (uint)(mode % 12);
                p.Items.SidekickLevel = (byte)(mode % 3 + 101); p.Items.SidekickSeries = (byte)(mode % 16);
                p.Shield = (uint)(mode % 9); p.Armor = (uint)(mode % 30); p.ShieldMax = 31;
                p.InvulnerableTicks = (mode & 2048) != 0 ? 1u : 0u;
            }
            foreach (var e in enemies.Enemies)
            {
                e.Type = 0; e.Y = 80; e.X = 100; e.ExplosionNumber = (byte)(mode % 53);
            }
            var index = mode % 81; var number = (byte)(mode / 2 % 2 + 1); var player = players[number - 1];
            var value = Values[mode % Values.Length];
            if (pickups.ArcadeMode != 0 && value > 30000) value = (short)(30001 + mode % 5);
            trace.AppendLine($"pickupcase:{mode}"); trace.AppendLine($"name:{pickups.GetName(1)},{pickups.GetName(2)}");
            enemies.Enemies[index].Availability = mode % 3 == 0 ? (byte)0 : (byte)2;
            Action<string> show = message => trace.AppendLine($"text:{message}");
            Action options = () => weapons.DrawOptions(hud, (x, y, frame) => trace.AppendLine($"P:{x},{y},{frame}"));
            Action<uint> song = id => trace.AppendLine($"song:{id}");
            if (mode < 4096)
            {
                if (value >= 30000) pickups.CollectArcade(player, number, index, value, show, options);
                else if (value > 20000) enemies.CollectArmorPickup(player, index, value, combat);
                else if (value > 10000) pickups.CollectBonusLevel(index, value, song);
                else pickups.CollectScore(player, number, index, value, 100, 98, 77, show);
            }
            else
            {
                for (var i = 0; i < 12; i++)
                {
                    var e = enemies.Enemies[i]; e.Availability = (byte)(i % 3);
                    e.X = (short)(100 + i % 4 * 4); e.Y = (short)(80 + i % 5 * 4);
                    e.Value = Values[(mode + i) % Values.Length];
                    if (pickups.ArcadeMode != 0 && e.Value > 30000) e.Value = (short)(30001 + i % 5);
                    e.ScoreItem = i % 2 != 0; e.Armor = (byte)(mode % 5);
                    e.XSpeed = 2; e.YSpeed = -1; e.Ground = i % 3 == 0;
                    if (e.Value == -3 && i == 0) shots.Availability[0] = 7;
                }
                pickups.Collide(player, number, 98, 77, show, options, song);
            }
            trace.AppendLine($"state:{Bit(combat.TwoPlayers)},{Bit(combat.Linked)},{Bit(pickups.BonusLevel)}," +
                $"{pickups.NextLevel},{pickups.DisplayTime},{enemies.CubeCount},{Bit(combat.LevelTimer)},{combat.LevelEnd},{combat.TemporaryVolume}");
            foreach (var p in players)
            {
                trace.AppendLine($"owner:{p.Cash},{p.PurpleBallsNeeded},{p.WeaponMode},{p.Superbombs},{p.Armor},{p.Shield}," +
                    $"{p.XVelocity},{p.YVelocity},{Bit(p.IsAlive)},{p.ExplodingTicks}");
                Dump(p.Items); Dump(p.LastItems);
            }
            foreach (var e in enemies.Enemies) trace.AppendLine($"enemy:{e.Availability},{e.Armor},{e.LinkNumber}");
            foreach (var e in effects.Explosions)
                trace.AppendLine($"E:{e.Lifetime},{e.X},{e.Y},{e.DeltaX},{e.DeltaY},{Bit(e.FixedPosition)},{Bit(e.FollowPlayer)},{e.Sprite}");
            foreach (var e in effects.Repeating) trace.AppendLine($"R:{e.Delay},{e.Lifetime},{e.X},{e.Y},{Bit(e.Big)}");
            trace.AppendLine($"shots:{shots.Power}" + string.Concat(Enumerable.Range(0, 11).Select(i => $",{shots.Repeat[i]},{shots.MultiPosition[i]}")));
            for (var i = 0; i < PlayerShots.Capacity; i++)
            {
                var s = shots.Shots[i]; trace.AppendLine($"projectile:{shots.Availability[i]},{s.X},{s.Y},{s.XM},{s.YM},{s.Graphic},{s.PlayerNumber}");
            }
            trace.AppendLine("sounds:" + string.Concat(effects.SoundQueue.Select(sound => $"{sound},")));
            uint hash = 2166136261;
            foreach (var pixel in hud.Pixels.Span) hash = unchecked((hash ^ pixel) * 16777619);
            trace.AppendLine($"pixels:{hash}"); trace.AppendLine($"rng:{MersenneTwister.NextUInt32()}");
            void Dump(PlayerItems p) => trace.AppendLine($"items:{p.Weapons[0].Id},{p.Weapons[0].Power},{p.Weapons[1].Id}," +
                $"{p.Weapons[1].Power},{p.Sidekicks[0]},{p.Sidekicks[1]},{p.Special},{p.Ship},{p.SidekickLevel},{p.SidekickSeries}," +
                $"{p.SuperArcadeMode},{p.Generator},0,0,0");
        }
        if (executable is null) return;
        var path = Path.Combine(Path.GetTempPath(), $"opentyrian-pickups-{Guid.NewGuid():N}.tyrr");
        try
        {
            File.WriteAllBytes(path, Encoding.Latin1.GetBytes(fixture));
            var start = new ProcessStartInfo(executable)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.Latin1
            };
            start.ArgumentList.Add(path);
            using var process = Process.Start(start) ?? throw new InvalidOperationException("Cannot start pickup oracle.");
            var output = process.StandardOutput.ReadToEndAsync(); var errors = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(120000)) { process.Kill(entireProcessTree: true); throw new TimeoutException("Pickup oracle timed out."); }
            var a = output.GetAwaiter().GetResult().Replace("\r\n", "\n", StringComparison.Ordinal);
            var b = trace.ToString().Replace("\r\n", "\n", StringComparison.Ordinal);
            if (process.ExitCode != 0 || a != b)
            {
                var expected = a.Split('\n'); var actual = b.Split('\n');
                var index = Enumerable.Range(0, Math.Min(expected.Length, actual.Length)).FirstOrDefault(i => expected[i] != actual[i], -1);
                var context = index < 0 ? "length" : actual.Take(index).LastOrDefault(line => line.StartsWith("pickupcase:", StringComparison.Ordinal));
                throw new InvalidOperationException($"Pickups differ at {index} ({context}): {expected.ElementAtOrDefault(index)} / " +
                    $"{actual.ElementAtOrDefault(index)}; {errors.GetAwaiter().GetResult()}");
            }
        }
        finally { File.Delete(path); }
    }

    private static void TestFailures(ItemDatabase items)
    {
        var players = new[] { new PlayerState(0), new PlayerState(1) };
        var effects = new SceneEffects(new byte[8]); var hud = new IndexedSurface(320, 200, 320, TextWriter.Null);
        var combat = new PlayerCombat(players, effects, hud); var enemies = new EnemySystem(items, TextWriter.Null);
        var level = new LevelEvents(items, enemies, players, new(), effects);
        var shots = new PlayerShots(items.Shots, items.Weapons, players, new ShotEffects());
        var pickups = new PlayerPickups(players, enemies, combat, shots, items, level, effects, [], []);
        Throws<ArgumentOutOfRangeException>(() => pickups.GetName(0));
        Throws<InvalidDataException>(() => pickups.GetName(1));
        Throws<ArgumentOutOfRangeException>(() => pickups.Collide(players[0], 3, 0, 0, _ => { }, () => { }, _ => { }));
        Throws<ArgumentOutOfRangeException>(() => pickups.CollectBonusLevel(100, 10001, _ => { }));
        Throws<InvalidDataException>(() => pickups.CollectScore(players[0], 1, 81, -3, 100, 0, 0, _ => { }));
        pickups.ArcadeMode = 8;
        Throws<InvalidDataException>(() => pickups.CollectArcade(players[0], 1, 0, 30001, _ => { }, () => { }));
        pickups.LocalName = "player\0ignored";
        if (pickups.GetName(1) != "player") throw new InvalidOperationException("Native player name NUL termination differs.");
    }
    private static void Throws<T>(Action action) where T : Exception
    {
        try { action(); } catch (T) { return; }
        throw new InvalidOperationException($"Expected {typeof(T).Name}.");
    }
    private static int Bit(bool value) => value ? 1 : 0;
    private sealed class ShotEffects : IPlayerShotEffects
    {
        public void Draw(int sheet, int x, int y, int frame, bool darken) => throw new InvalidOperationException("Unexpected pickup rendering.");
        public (ushort Width, ushort Height) DrawSpecial(int x, int y, int frame) => throw new InvalidOperationException("Unexpected pickup rendering.");
        public void Explosion(int x, int y, byte type) => throw new InvalidOperationException("Unexpected pickup projectile movement.");
        public void SuperPixels(int x, int y, ushort count, byte width, byte color) => throw new InvalidOperationException("Unexpected pickup projectile movement.");
    }
}
