using System.Diagnostics;
using System.Text;
using OpenTyrian.Core;

namespace OpenTyrian.Core.Tests;

internal static class EnemyAttackTests
{
    public static void Run(string? executable = null)
    {
        var definitions = new ItemDefinitions();
        definitions.ParseBundle(Encoding.Latin1.GetBytes(Fixture()), "attacks.tyrr");
        var items = ItemDatabase.FromDefinitions(definitions);
        var expected = Execute(items);
        using var warnings = new StringWriter();
        var enemies = new EnemySystem(items, warnings);
        var players = new[] { new PlayerState(0) { IsAlive = true, X = 60, Y = 60, Armor = 20, ShotHitAreaX = 12, ShotHitAreaY = 10 }, new PlayerState(1) };
        var effects = new SceneEffects(new byte[8]);
        var surface = new IndexedSurface(320, 200, 320, warnings);
        var combat = new PlayerCombat(players, effects, surface);
        var shot = enemies.EnemyShots[0];
        shot.Available = false; shot.X = 60; shot.Y = 60; shot.Duration = 5; shot.Damage = 5; shot.XVelocity = 2;
        enemies.UpdateEnemyShots(false, players, effects, combat, new SpriteSheet(), new SpriteSheet(), surface);
        Check(shot.Available && players[0].Armor == 15 && players[0].XVelocity == 5 && effects.Explosions[0].Lifetime > 0,
            "Hostile projectile did not integrate player damage, impulse, and scene effects.");
        var target = enemies.Enemies[0];
        target.Availability = 0; target.X = 80; target.Y = 60; target.Cycle = 1; target.Graphics[0] = 1;
        target.Sprites = enemies.SpriteSheets[0];
        try
        {
            enemies.DrawAndUpdate(25, players, effects, (_, _, _, _, _) => throw new IOException("render failure"));
            throw new InvalidOperationException("Draw error was swallowed.");
        }
        catch (IOException)
        {
            Check(players[0].X == 60, "Enemy update failed to restore player coordinates after rendering failed.");
        }
        if (executable is null) return;
        var path = Path.Combine(Path.GetTempPath(), $"opentyrian-attacks-{Guid.NewGuid():N}.tyrr");
        try
        {
            File.WriteAllBytes(path, Encoding.Latin1.GetBytes(Fixture()));
            var start = new ProcessStartInfo(executable) { RedirectStandardOutput = true, RedirectStandardError = true };
            start.ArgumentList.Add(path);
            using var process = Process.Start(start) ?? throw new InvalidOperationException("Cannot start enemy attack oracle.");
            var output = process.StandardOutput.ReadToEndAsync();
            var errors = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(30000))
            {
                process.Kill(entireProcessTree: true);
                throw new TimeoutException("Enemy attack oracle timed out.");
            }
            var actual = EnemySystemTests.Normalize(output.GetAwaiter().GetResult().Replace("\r\n", "\n", StringComparison.Ordinal));
            var reference = EnemySystemTests.Normalize(expected);
            var first = Enumerable.Range(0, Math.Min(actual.Length, reference.Length)).FirstOrDefault(i => actual[i] != reference[i], -1);
            Check(process.ExitCode == 0 && actual.SequenceEqual(reference) && errors.GetAwaiter().GetResult().Length == 0,
                $"Enemy attack parity differs at line {first}: {(first < 0 ? "" : actual[first] + " / " + reference[first])}; {errors.Result}");
        }
        finally { File.Delete(path); }
    }

    private static string Fixture()
    {
        var output = new StringBuilder(EnemySystemTests.Fixture());
        for (var i = 10; i <= 12; i++)
            output.Append($"[shot:{i}]\nmax=2\nmulti=2\nweapani=3\nsound=7\naim={(i == 11 ? 6 : 0)}\n" +
                "attack=3 5\ndel=10 12\nsx=-2 2\nsy=1 2\nbx=-4 4\nby=0 1\nsg=1 501\n" +
                $"acceleration={(i == 12 ? -128 : 1)}\naccelerationx=2\ntx=3\nty=4\n");
        for (var i = 251; i <= 255; i++) output.Append($"[shot:{i}]\nmax=1\nmulti=1\n");
        output.Append("[enemy:attacker]\nshapebank=3\narmor=50\nvalue=0\nani=2\nanimate=2\negraphic=1 2\n" +
            "tur=10 11 12\nfreq=2 3 4\neshotwait=1 1 1\nelaunchfreq=3\nelaunchtype=child\nstartx=55\nstarty=80\n");
        output.Append("[enemy:child]\nshapebank=3\narmor=5\nvalue=0\nani=1\negraphic=1\nstartyc=2\nelaunchtypepos=95\n");
        output.Append("[enemy:aimedchild]\nshapebank=3\narmor=5\nvalue=0\nani=1\negraphic=1\nstartyc=-2\nelaunchtypepos=80\n");
        return output.ToString();
    }

    private static string Execute(ItemDatabase items)
    {
        using var warnings = new StringWriter();
        var output = new StringBuilder();
        for (var mode = 0; mode < 16; mode++)
        {
            var enemies = Create(items, warnings);
            var players = new[]
            {
                new PlayerState(0) { X = 80, Y = 80, IsAlive = mode % 4 != 1, ShotHitAreaX = 12, ShotHitAreaY = 10, InvulnerableTicks = (uint)(mode % 3 == 0 ? 1 : 0) },
                new PlayerState(1) { X = 90, Y = 80, IsAlive = mode % 4 != 2, ShotHitAreaX = 12, ShotHitAreaY = 10 }
            };
            var effects = new SceneEffects(new byte[8]);
            enemies.EnemiesOnScreen = 65530; enemies.Difficulty = (sbyte)(mode % 11);
            enemies.TwoPlayers = mode % 2 != 0; enemies.GalagaMode = mode % 4 == 3;
            enemies.GalagaShotFrequency = (ushort)(mode % 2 != 0 ? 400 : 0); enemies.MapXOffset = 10;
            MersenneTwister.Seed(12345);
            var slot = mode % 4 * 25;
            var target = enemies.Enemies[slot];
            enemies.Initialize(target, (ushort)items.Index(ItemType.Enemy, "attacker"));
            target.Availability = 0; target.X = 55; target.Y = 80; target.YSpeed = (sbyte)(mode % 3 != 0 ? 1 : 0);
            target.LinkNumber = 20; target.LaunchSpecial = (byte)(mode % 3);
            target.Iced = (byte)(mode == 1 ? 2 : 0); target.Damaged = mode == 2;
            if (mode % 2 != 0) target.LaunchType = (ushort)items.Index(ItemType.Enemy, "aimedchild");
            if (mode == 6)
                for (var i = 0; i < 100; i++)
                {
                    if (i == slot) continue;
                    var enemy = enemies.Enemies[i];
                    enemy.Availability = 2; enemy.Cycle = 1; enemy.Graphics[0] = 1;
                    enemy.Sprites = enemies.SpriteSheets[0]; enemy.Damaged = true;
                }
            if (mode == 4) foreach (var shot in enemies.EnemyShots) shot.Available = false;
            for (var frame = 0; frame < 12; frame++)
            {
                enemies.FirstBackgroundMove = (ushort)(frame % 3); enemies.BackgroundMove = (ushort)(frame % 2);
                if (frame == 5) { target.Turrets[0] = 251; target.Turrets[1] = 253; target.Turrets[2] = 255; }
                if (frame == 8) { target.Turrets[0] = 252; target.Turrets[1] = 254; target.Turrets[2] = 255; }
                output.Append($"attack:{mode}:{frame}\n");
                enemies.DrawAndUpdate(slot + 25, players, effects, (_, x, y, graphic, filter) =>
                    output.Append(filter == 0 ? $"D:8:{x}:{y}:{graphic}:0\n" : $"F:8:{x}:{y}:{graphic}:{filter}\n"));
                UpdateShots(enemies, frame == 3, players, effects, output);
                Dump(enemies, items, effects, players, output);
            }
            output.Append($"attackrng:{MersenneTwister.NextUInt32()}\n");
        }
        for (var mode = 0; mode < 16; mode++)
        {
            var enemies = Create(items, warnings);
            enemies.TwoPlayers = mode % 2 != 0;
            var players = new[]
            {
                new PlayerState(0) { X = 60, Y = 60, IsAlive = true, ShotHitAreaX = 12, ShotHitAreaY = 10, InvulnerableTicks = (uint)(mode % 3) },
                new PlayerState(1) { X = 80, Y = 60, IsAlive = true, ShotHitAreaX = 12, ShotHitAreaY = 10, InvulnerableTicks = (uint)(mode % 3) }
            };
            var effects = new SceneEffects(new byte[8]);
            for (var j = 0; j < 12; j++)
            {
                var shot = enemies.EnemyShots[j]; shot.Available = false;
                shot.X = (short)(j == 0 ? 0 : j == 1 ? 275 : j == 2 ? 276 : j == 3 ? 1 : 60 + j - 4);
                shot.Y = (short)(j == 4 ? -14 : j == 5 ? -13 : j == 6 ? 190 : j == 7 ? 191 : 60);
                shot.XVelocity = (short)(mode == 2 ? 32767 : mode % 3 - 1);
                shot.YVelocity = (short)(mode == 3 ? -32768 : 1);
                shot.XAcceleration = (sbyte)(mode == 2 ? 127 : 0); shot.YAcceleration = (sbyte)(mode == 3 ? -128 : 0);
                shot.TrackX = (byte)(j % 3); shot.TrackY = (byte)(j % 4); shot.Duration = (byte)(j == 11 ? 0 : 2);
                shot.Damage = (byte)(j % 3 != 0 ? 5 : 0); shot.Graphic = (ushort)(j % 2 != 0 ? 500 : 1);
                shot.Animation = (ushort)(j % 3); shot.AnimationMax = (ushort)(j % 4);
            }
            output.Append($"hostilecase:{mode}\n");
            UpdateShots(enemies, mode == 15, players, effects, output);
            Dump(enemies, items, effects, players, output);
        }
        ExecuteSpecials(items, warnings, output);
        Check(warnings.ToString().Length == 0, $"Unexpected attack warning: {warnings}");
        return output.ToString();
    }

    private static void ExecuteSpecials(ItemDatabase items, TextWriter warnings, StringBuilder output)
    {
        var enemies = Create(items, warnings);
        var players = new[] { new PlayerState(0), new PlayerState(1) };
        for (var mode = 0; mode < 90; mode++)
        {
            var effects = new SceneEffects(new byte[8]);
            players[0].X = mode % 2 != 0 ? 60 : 400; players[0].Y = 80;
            players[1].X = 74; players[1].Y = 80;
            players[0].XVelocity = players[1].XVelocity = 0;
            var enemy = enemies.Enemies[0];
            enemy.X = 60; enemy.Y = (short)(mode % 3 != 0 ? 80 : -2); enemy.Filter = 0;
            enemies.FirstBackgroundMove = (ushort)(mode % 4); enemies.TwoPlayers = mode % 2 != 0;
            enemies.Difficulty = (sbyte)(mode % 3);
            var handled = enemies.ApplyWeaponSpecial(0, mode % 3 + 1, 251 + mode % 5, players, effects);
            output.Append($"special:{mode}={(handled ? 1 : 0)}\n");
            Dump(enemies, items, effects, players, output);
        }
        MersenneTwister.Seed(12345);
        for (var mode = 0; mode < 128; mode++)
        {
            enemies.TwoPlayers = mode % 2 != 0; enemies.Difficulty = (sbyte)(mode % 11);
            players[0].X = mode % 5 != 0 ? 60 : -10; players[0].Y = 80;
            players[1].X = mode % 5 != 0 ? 90 : 10; players[1].Y = 30;
            players[0].IsAlive = mode % 4 < 2; players[1].IsAlive = mode % 4 is 0 or 2;
            enemies.MapXOffset = (ushort)(mode % 17);
            enemies.AimShot(0, items.Index(ItemType.Shot, "11"), (ushort)(mode % 3 != 0 ? 60 : 65530),
                (ushort)(mode % 5 != 0 ? 80 : 65530), players);
            output.Append($"aimedshot:{mode}={enemies.EnemyShots[0].XVelocity},{enemies.EnemyShots[0].YVelocity}\n");
        }
        output.Append($"aimrng:{MersenneTwister.NextUInt32()}\n");
    }

    private static EnemySystem Create(ItemDatabase items, TextWriter warnings)
    {
        var enemies = new EnemySystem(items, warnings);
        new byte[] { 3, 7, 9, 12 }.CopyTo(enemies.SpriteIds, 0);
        return enemies;
    }

    private static void UpdateShots(EnemySystem enemies, bool endLevel, PlayerState[] players, SceneEffects effects, StringBuilder output)
        => enemies.UpdateEnemyShots(endLevel, players, effects, (amount, player) =>
        {
            output.Append($"damage:{(ReferenceEquals(player, players[0]) ? 0 : 1)}:{amount}\n");
            return amount;
        }, (sheet, x, y, frame) => output.Append($"D:{sheet}:{x}:{y}:{frame}:0\n"));

    private static void Dump(EnemySystem enemies, ItemDatabase items, SceneEffects effects, PlayerState[] players, StringBuilder output)
    {
        output.Append($"attackstats:{enemies.EnemiesOnScreen},{enemies.TotalEnemies},{players[0].X},{players[1].X},{players[0].XVelocity},{players[1].XVelocity}\n");
        output.Append($"sound:{string.Join(',', effects.SoundQueue)}\n");
        for (var i = 0; i < 100; i++)
        {
            output.Append($"slot:{i}\n");
            EnemySystemTests.Dump(enemies, items, enemies.Enemies[i], output);
        }
        for (var i = 0; i < 60; i++)
        {
            var s = enemies.EnemyShots[i];
            output.Append($"hostile:{i}={(s.Available ? 1 : 0)},{s.X},{s.Y},{s.XVelocity},{s.YVelocity},{s.XAcceleration},{s.YAcceleration},{s.TrackX},{s.TrackY},{s.Graphic},{s.Damage},{s.Duration},{s.Animation},{s.AnimationMax}\n");
        }
        for (var i = 0; i < 200; i++)
        {
            var e = effects.Explosions[i];
            output.Append($"E:{i}={e.Lifetime},{e.X},{e.Y},{e.DeltaX},{e.DeltaY},{(e.FixedPosition ? 1 : 0)},{(e.FollowPlayer ? 1 : 0)},{e.Sprite}\n");
        }
        output.Append($"velocity:{players[0].XVelocity},{players[0].YVelocity},{players[1].XVelocity},{players[1].YVelocity}\n");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
