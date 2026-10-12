using System.Collections;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using OpenTyrian.Core;

namespace OpenTyrian.Core.Tests;

internal static class EnemySystemTests
{
    public static void Run(string? executable = null)
    {
        var definitions = new ItemDefinitions();
        definitions.ParseBundle(Encoding.Latin1.GetBytes(Fixture()), "fixture.tyrr");
        var items = ItemDatabase.FromDefinitions(definitions);
        using var warnings = new StringWriter();
        var enemies = new EnemySystem(items, warnings);
        Check(enemies.FindSpriteSheet(21) == enemies.SheetEleven && enemies.FindSpriteSheet(26) == enemies.SheetTen,
            "Special enemy sprite banks differ.");
        var none = enemies.FindSpriteSheet(100);
        Check(none is null, "Unloaded enemy sprite bank accepted.");
        var definition = (ushort)items.Index(ItemType.Enemy, "variant0");
        Check(enemies.Create(0, definition) == 1 && enemies.Enemies[0].Availability == 2,
            "Enemy slot allocation or armorless availability differs.");
        enemies.Enemies[0].Availability = 1;
        enemies.Enemies[0].ScoreItem = true;
        enemies.Initialize(enemies.Enemies[0], definition);
        Check(enemies.Enemies[0].ScoreItem, "Native reused zero-score item flag changed.");
        enemies.SpriteIds[0] = 3;
        var players = new[] { new PlayerState(0), new PlayerState(1) };
        var effects = new SceneEffects(new byte[8]);
        var surface = new IndexedSurface(320, 200, 320, warnings);
        var shots = new PlayerShots(items.Shots, items.Weapons, players,
            effects.CreateProjectileEffects(surface, new SpriteSheet(), new SpriteSheet(), new SpriteTable()), enemies.Enemies);
        enemies.Enemies[0].X = 77;
        Check(ReferenceEquals(shots.Enemies[0], enemies.Enemies[0]) && shots.Enemies[0].X == 77,
            "Projectile targeting copied rather than shared live enemy state.");
        var stripeEnemies = new EnemySystem(items, warnings) { ZinglonDuration = 26 };
        var stripeTarget = stripeEnemies.Enemies[0];
        stripeEnemies.Initialize(stripeTarget, (ushort)items.Index(ItemType.Enemy, "variant1"));
        stripeTarget.Availability = 0; stripeTarget.Armor = 5; stripeTarget.Value = 20;
        stripeTarget.X = 60; stripeTarget.Y = 60; stripeTarget.EnemyDie = 0;
        players[0].X = 60;
        var stripeShots = new PlayerShots(items.Shots, items.Weapons, players, new TraceEffects(effects, new StringBuilder()),
            stripeEnemies.Enemies, effects.SoundQueue);
        stripeShots.Availability[80] = 2;
        stripeEnemies.ProcessShot(80, stripeShots, players, effects, 0, 0, _ => throw new InvalidOperationException("Unexpected event jump."));
        Check(stripeEnemies.KilledEnemies == 1 && players[0].Cash == 20 && players[1].Cash == 0 &&
            stripeShots.Availability[80] == 1, "Reserved stripe kill must credit player one without consuming the stripe.");
        using var spriteStream = new MemoryStream(new byte[] { 2, 0, 0x10, 0xBC, 0x0F });
        var enemySprites = new SpriteSheet();
        enemySprites.Load(spriteStream, 5);
        var rendered = enemies.Enemies[0];
        rendered.Sprites = enemySprites; rendered.X = rendered.Y = 40; rendered.Cycle = 1;
        rendered.AnimationActive = 0; rendered.Graphics[0] = 1; rendered.Size = 0; rendered.Filter = 0xA0;
        enemies.MapXOffset = 10;
        Check(enemies.AnimateAndDraw(0, surface) && surface.Pixels.Span[40 * 320 + 50] == 0xAC && rendered.Filter == 0,
            "Enemy sprite renderer did not apply/reset the hit filter at the scrolled position.");
        Check(enemies.AimVelocity(0, 0, 11, 0, 10) == (10, 10), "Native zero-distance enemy aim differs.");
        var (expected, expectedErrors) = Execute(items);
        if (executable is null) return;
        var path = Path.Combine(Path.GetTempPath(), $"opentyrian-enemies-{Guid.NewGuid():N}.tyrr");
        try
        {
            File.WriteAllBytes(path, Encoding.Latin1.GetBytes(Fixture()));
            var start = new ProcessStartInfo(executable) { RedirectStandardOutput = true, RedirectStandardError = true };
            start.ArgumentList.Add(path);
            using var process = Process.Start(start) ?? throw new InvalidOperationException("Cannot start enemy oracle.");
            var output = process.StandardOutput.ReadToEndAsync();
            var errors = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(30000))
            {
                process.Kill(entireProcessTree: true);
                throw new TimeoutException("Enemy oracle timed out.");
            }
            var actual = output.GetAwaiter().GetResult().Replace("\r\n", "\n", StringComparison.Ordinal);
            var actualErrors = errors.GetAwaiter().GetResult().Replace("\r\n", "\n", StringComparison.Ordinal);
            var actualLines = Normalize(actual);
            var expectedLines = Normalize(expected);
            var differences = actualLines.Except(expectedLines).Concat(expectedLines.Except(actualLines)).Take(8);
            Check(process.ExitCode == 0 && actualLines.SequenceEqual(expectedLines) && actualErrors == expectedErrors,
                $"Enemy initialization differs: {string.Join(" / ", differences)} errors: {actualErrors} / {expectedErrors}");
        }
        finally { File.Delete(path); }
    }

    internal static string Fixture()
    {
        var fixture = new StringBuilder(ItemDatabaseTests.Fixture(0));
        fixture.Append("[shot:late]\nmax=2\nmulti=2\nweapani=1\ndel=6 8\nattack=3 5\nsg=1 2\nbx=-2 2\nby=0 1\nsx=0 0\nsy=0 0\n");
        fixture.Append("[enemy:533]\nshapebank=3\nvalue=100\narmor=20\n");
        fixture.Append("[enemy:534]\nshapebank=7\nvalue=200\narmor=30\n");
        fixture.Append("[enemy:535]\nshapebank=3\nvalue=30000\narmor=0\n");
        fixture.Append("[enemy:536]\nshapebank=3\nvalue=30001\narmor=0\n");
        fixture.Append("[enemy:537]\nshapebank=3\nvalue=50\narmor=0\n");
        var armor = new[] { 0, 1, 254, 255, 10, 2, 50, 100, 150, 200, 250, 0 };
        var value = new[] { 0, 1, 2, 9999, 10000, -5, 30000, 32767, 3, 1500, 5000, 0 };
        for (var i = 0; i < 12; i++)
        {
            fixture.Append($"[enemy:variant{i}]\narmor={armor[i]}\nvalue={value[i]}\nshapebank={(i == 7 ? 99 : 3)}\n" +
                $"animate={i % 4}\nani=7\nstartx=-20\nstarty=30\nstartxc={(i % 3 - 1) * 5}\nstartyc={(i % 3 - 1) * -4}\n" +
                $"xmove=-3\nymove=4\nxaccel=-1\nyaccel=2\nxcaccel={(i == 4 ? -128 : -5)}\nycaccel=4\n" +
                $"xrev={(i % 3 == 0 ? 0 : i % 3 == 1 ? -99 : -5)}\nyrev=5\ndgr=10\ndani=-5\ndlevel=10\n" +
                $"tur=1 2 3\nfreq=4 5 6\negraphic=1 2 3 4\nesize=1\nexplosiontype=12\nelaunchfreq=20\n" +
                "elaunchtype=533\nelaunchtypepos=3\nelaunchspecial=5\neenemydie=534\neshotwait=5 10 15\nenemyground=1\n");
        }
        return fixture.ToString();
    }

    private static (string Output, string Errors) Execute(ItemDatabase items)
    {
        using var warnings = new StringWriter();
        var enemies = new EnemySystem(items, warnings);
        new byte[] { 3, 7, 9, 12 }.CopyTo(enemies.SpriteIds, 0);
        var output = new StringBuilder();
        MersenneTwister.Seed(5489);
        for (var difficulty = -1; difficulty <= 11; difficulty++)
        {
            enemies.Difficulty = (sbyte)difficulty;
            for (var i = 0; i < 12; i++)
            {
                var enemy = enemies.Enemies[0];
                enemy.Availability = 1;
                enemy.FlagNumber = 5; enemy.SetTo = true; enemy.ScoreItem = i % 2 != 0;
                enemy.ShotMultiPosition[0] = 7;
                var created = enemies.Create(0, items.Index(ItemType.Enemy, $"variant{i}"),
                    (short)(i == 4 ? 21 : i == 5 ? 26 : i == 6 ? 7 : 0));
                output.Append($"created:{difficulty}:{i}={created}\n");
                Dump(enemies, items, enemy, output);
            }
        }
        enemies.SuperArcadeMode = 1;
        output.Append($"legacy:{enemies.CreateLegacy(25, 534, 0)}\n");
        Dump(enemies, items, enemies.Enemies[25], output);
        foreach (var enemy in enemies.Enemies) enemy.Availability = 0;
        output.Append($"full:{enemies.Create(50, 0)}\n");
        output.Append($"rng:{MersenneTwister.NextUInt32()}\n");
        for (var i = 0; i < 5000; i++)
        {
            var slot = i % 100;
            var enemy = enemies.Enemies[slot];
            enemy.X = (short)(i % 57 - 28); enemy.Y = (short)(i % 61 - 30);
            enemy.MapOffset = (ushort)(i % 17); enemy.Cycle = (byte)(i % 2);
            enemies.ZinglonDuration = (byte)(i % 256);
            var result = enemies.ShotHits(i % 3 == 2 ? 80 : 0, slot, i % 3 == 1,
                i % 51 - 25, i % 53 - 26, (ushort)(i % 37), (ushort)(i % 41), (short)(i % 31 - 15), 123, -12, 4);
            output.Append($"hit:{i}={(result.Hit ? 1 : 0)},{result.Chain},{result.Damage},{result.Filter}\n");
        }
        for (var i = 0; i < 100; i++)
        {
            var enemy = enemies.Enemies[i];
            enemy.LinkNumber = (byte)(i % 7); enemy.Filter = 2; enemy.Iced = 3;
            enemy.Ground = i % 2 != 0; enemy.Armor = (byte)(i == 0 ? 255 : 20);
            enemy.Availability = (byte)(i % 3);
        }
        for (var i = 0; i < 16; i++)
        {
            enemies.BossBars[0].LinkNumber = (byte)(i % 7); enemies.BossBars[0].Color = 1;
            enemies.BossBars[1].LinkNumber = (byte)((i + 1) % 7); enemies.BossBars[1].Color = 1;
            enemies.MarkLinked(i, i % 7, 300 - i, i % 3 != 0 ? 0 : 10);
            output.Append($"mark:{i}={enemies.BossBars[0].Color},{enemies.BossBars[1].Color}\n");
            for (var j = 0; j < 100; j++)
                output.Append($"marked:{i}:{j}={enemies.Enemies[j].Filter},{enemies.Enemies[j].Iced}\n");
        }
        for (var mode = 0; mode < 8; mode++)
        {
            enemies = new EnemySystem(items, warnings);
            new byte[] { 3, 7, 9, 12 }.CopyTo(enemies.SpriteIds, 0);
            var effects = new SceneEffects(new byte[8]);
            var players = new[] { new PlayerState(0), new PlayerState(1) };
            players[0].Cash = players[1].Cash = ulong.MaxValue;
            enemies.ContinualDamage = (mode & 1) != 0; enemies.GalagaMode = (mode & 2) != 0;
            enemies.SuperArcadeMode = (byte)(mode & 4);
            enemies.KilledEnemies = 65530; enemies.CubeCount = 65535; enemies.TotalEnemies = 65530;
            enemies.SuperArcadePowerUp = 5; enemies.Enemy254Jump = 123;
            for (var j = 0; j < 16; j++)
            {
                var enemy = enemies.Enemies[j];
                enemies.Initialize(enemy, (ushort)items.Index(ItemType.Enemy, $"variant{j % 12}"));
                enemy.Availability = (byte)(j % 3 == 0 ? 1 : 0);
                enemy.X = (short)(60 + j); enemy.Y = (short)(j % 2 != 0 ? -2 : 60);
                enemy.MapOffset = (ushort)(j % 3 != 0 ? 5 : 65530);
                enemy.LinkNumber = (byte)(j % 4 == 0 ? 120 : j % 4 == 1 ? 20 : j % 4 == 2 ? 110 : 255);
                enemy.DamageLevel = (sbyte)(j % 3 == 0 ? -1 : j % 3 == 1 ? 10 : 0);
                enemy.DamageAnimation = (sbyte)(j % 4 == 0 ? 0 : j % 4 == 1 ? -128 : 5);
                enemy.DamageGraphic = (ushort)(j % 4 == 0 ? 0 : 300);
                enemy.Special = true; enemy.FlagNumber = (byte)(j % 10 + 1); enemy.SetTo = true;
                enemy.Value = (short)(j % 3 == 0 ? 1 : j % 3 == 1 ? 20 : 9999);
                enemy.EnemyDie = (ushort)items.Index(ItemType.Enemy, $"{535 + j % 3}");
            }
            output.Append($"damage:{mode}\n");
            enemies.AnimateDamaged(5, (mode & 1) != 0 ? 255 : 120, effects);
            for (var j = 0; j < 16; j++)
            {
                output.Append($"slot:{j}\n");
                Dump(enemies, items, enemies.Enemies[j], output);
            }
            DumpEffects(enemies, effects, players, output);
            output.Append($"destroy:{mode}\n");
            enemies.DestroyLinked(5, 2, (mode & 1) != 0 ? 254 : 120, players, effects, target => output.Append($"jump:{target}\n"));
            for (var j = 0; j < 100; j++)
            {
                output.Append($"slot:{j}\n");
                Dump(enemies, items, enemies.Enemies[j], output);
            }
            DumpEffects(enemies, effects, players, output);
        }
        for (var mode = 0; mode < 13; mode++)
        {
            enemies = new EnemySystem(items, warnings);
            new byte[] { 3, 7, 9, 12 }.CopyTo(enemies.SpriteIds, 0);
            var effects = new SceneEffects(new byte[8]);
            var players = new[] { new PlayerState(0), new PlayerState(1) };
            players[0].X = 60;
            var shots = new PlayerShots(items.Shots, items.Weapons, players, new TraceEffects(effects, output),
                enemies.Enemies, effects.SoundQueue)
            { Power = 100 };
            MersenneTwister.Seed(5489);
            enemies.ZinglonDuration = 26;
            for (var j = 0; j < 4; j++)
            {
                var enemy = enemies.Enemies[j];
                enemies.Initialize(enemy, (ushort)items.Index(ItemType.Enemy, "variant2"));
                enemy.Availability = 0; enemy.X = (short)(60 + j); enemy.Y = 60; enemy.MapOffset = 0; enemy.Cycle = 1;
                enemy.Armor = (byte)(mode is 3 or 12 ? 255 : 5 + j * 5);
                enemy.DamageAnimation = 1; enemy.DamageGraphic = 2; enemy.DamageLevel = (sbyte)(mode == 5 ? 15 : 0);
                enemy.Damaged = false; enemy.EnemyDie = 0; enemy.Value = 20; enemy.LinkNumber = (byte)(mode == 4 ? 120 : 0);
            }
            var index = mode == 12 ? 80 : 0;
            var shot = shots.Shots[index];
            shot.X = shot.Y = 60; shot.Trail = 255; shot.Graphic = (ushort)(mode == 2 ? 60001 : 1);
            shot.AnimationMax = 1; shot.PlayerNumber = 2; shot.Damage = (byte)(5 + mode * 5); shot.BlastFilter = 10;
            shot.IceTicks = 7; shot.Infinite = (byte)(mode % 3);
            if (mode == 1) shot.Chain = (ushort)items.Index(ItemType.Shot, "late");
            shots.Availability[index] = 10;
            output.Append($"process:{mode}\n");
            enemies.ProcessShot(index, shots, players, effects, 70, 50, target => output.Append($"jump:{target}\n"));
            if (mode == 0)
                Check(enemies.KilledEnemies == 1 && shots.Availability[index] == 0 && players[1].Cash == 20,
                    "Equal-damage projectile kill or reward differs.");
            if (mode == 1)
                Check(shots.Availability[index] == 0 && shots.MultiPosition[4] > 0 && enemies.KilledEnemies == 0,
                    "Chain reaction failed to create a misc shot without damaging its target.");
            if (mode == 2)
                Check(enemies.KilledEnemies == 3 && shots.Availability[index] == 0 && shots.Shots[index].Damage == 0,
                    "Penetrating damage changed the native local damage across targets.");
            if (mode == 3)
                Check(enemies.KilledEnemies == 0 && enemies.Enemies[0].Armor == 255 && effects.LastSuperPixel == 13,
                    "Invulnerable enemy particle response differs.");
            if (mode == 4)
                Check(enemies.KilledEnemies == 4 && shots.Availability[index] == 9,
                    "Infinite projectile or linked destruction differs.");
            if (mode == 12)
                Check(shots.Availability[index] == 9 && enemies.Enemies[3].Iced == 7 && effects.SoundQueue[5] == 3,
                    "Reserved stripe collision differs.");
            for (var j = 0; j < 100; j++)
            {
                output.Append($"slot:{j}\n");
                Dump(enemies, items, enemies.Enemies[j], output);
            }
            DumpEffects(enemies, effects, players, output);
            output.Append($"shotstate:{shots.Power}");
            for (var j = 0; j < 11; j++) output.Append($",{shots.MultiPosition[j]},{shots.Repeat[j]}");
            for (var j = 0; j < 8; j++) output.Append($",{shots.SoundQueue[j]}");
            output.Append('\n');
            for (var j = 0; j < 81; j++)
            {
                var s = shots.Shots[j];
                output.Append($"shot:{j}={shots.Availability[j]},{s.X},{s.Y},{s.Animation},{s.Graphic},{s.Damage},{s.PlayerNumber},{s.Infinite}\n");
            }
            for (var j = 0; j < 101; j++)
            {
                var p = effects.SuperPixels[j];
                output.Append($"P:{j}={p.X},{p.Y},{p.Z},{p.DeltaX},{p.DeltaY},{p.Color}\n");
            }
            output.Append($"ring:{effects.LastSuperPixel}:{MersenneTwister.NextUInt32()}\n");
        }
        enemies = new EnemySystem(items, warnings);
        MersenneTwister.Seed(12345);
        int[] xs = [-81, -80, -29, -24, -5, 60, 245, 295, 299, 340, 341, 32767];
        int[] ys = [-113, -112, -27, -26, -13, -12, 0, 181, 182, 190, 191, 32767];
        byte[] acceleration = [0, 1, 88, 89, 90, 100, 255];
        for (var mode = 0; mode < 32; mode++)
        {
            enemies.Enemies[0] = new EnemyState { Availability = 0 };
            var enemy = enemies.Enemies[0];
            enemy.Sprites = mode % 7 != 0 ? enemies.SpriteSheets[0] : null;
            enemy.X = (short)xs[mode % 12]; enemy.Y = (short)ys[mode * 7 % 12];
            enemy.XSpeed = (sbyte)(mode == 4 ? -128 : mode % 7 - 3); enemy.YSpeed = 3;
            enemy.XAcceleration = acceleration[mode % 7]; enemy.YAcceleration = acceleration[(mode + 3) % 7];
            enemy.XFixedAcceleration = (sbyte)(mode % 2 != 0 ? 3 : -3);
            enemy.YFixedAcceleration = (sbyte)(mode % 3 != 0 ? 2 : -2);
            enemy.XAccelerationWait = (sbyte)(mode == 4 ? -128 : 1); enemy.YAccelerationWait = 0;
            enemy.XAccelerationWaitMax = (byte)(mode == 4 ? 128 : 3); enemy.YAccelerationWaitMax = 2;
            enemy.XAccelerationAdd = (short)(mode % 2 != 0 ? 1 : -1); enemy.YAccelerationAdd = 1;
            enemy.XReverse = (sbyte)(mode % 2 != 0 ? 2 : -2); enemy.YReverse = 4;
            enemy.XMinBounce = -5; enemy.XMaxBounce = 245; enemy.YMinBounce = 0; enemy.YMaxBounce = 180;
            enemy.FixedMoveY = (short)(mode % 3 - 1); enemy.ScoreItem = mode % 2 != 0;
            enemy.Cycle = 1; enemy.AnimationLength = 3; enemy.AnimationMin = 1;
            enemy.AnimationActive = 1; enemy.AnimationMax = (byte)(mode % 3 != 0 ? 0 : 2); enemy.AnimationWhenFire = 2;
            enemy.Graphics[0] = 1; enemy.Graphics[1] = (ushort)(mode % 5 != 0 ? 2 : 999);
            enemy.Graphics[2] = 3; enemy.Size = (byte)(mode % 2);
            for (var frame = 0; frame < 8; frame++)
            {
                enemies.MapXOffset = (ushort)(frame % 3 != 0 ? 0 : 10);
                enemies.BackgroundMove = (ushort)(frame % 3); enemy.Filter = (byte)(frame % 2 != 0 ? 0 : 0xA0);
                output.Append($"movement:{mode}:{frame}\n");
                enemies.UpdateAcceleration(0, frame % 2 != 0 ? 40 : -40, frame % 2 != 0 ? 80 : -80);
                var drawn = enemies.AnimateAndDraw(0, (sprite, x, y, graphic, filter) =>
                    output.Append(filter == 0 ? $"D:8:{x}:{y}:{graphic}:0\n" : $"F:8:{x}:{y}:{graphic}:{filter}\n"));
                var positioned = enemies.UpdatePosition(0);
                output.Append($"moved:{(drawn ? 1 : 0)},{(positioned ? 1 : 0)}\n");
                Dump(enemies, items, enemy, output);
            }
        }
        for (var i = 0; i < 1000; i++)
        {
            enemies.MapXOffset = (ushort)(i % 17);
            var velocity = enemies.AimVelocity(i % 101 - 50, i % 91 - 45, i % 111 - 55, i % 121 - 60, i % 256);
            output.Append($"aim:{i}={velocity.X},{velocity.Y}\n");
        }
        output.Append($"movementrng:{MersenneTwister.NextUInt32()}\n");
        return (output.ToString(), warnings.ToString());
    }

    private sealed class TraceEffects(SceneEffects effects, StringBuilder output) : IPlayerShotEffects
    {
        public void Draw(int sheet, int x, int y, int frame, bool darken)
            => output.Append($"D:{sheet}:{x}:{y}:{frame}:{(darken ? 1 : 0)}\n");
        public (ushort Width, ushort Height) DrawSpecial(int x, int y, int frame)
        {
            output.Append($"S:{x}:{y}:{frame}\n");
            return (13, 17);
        }
        public void Explosion(int x, int y, byte type) => effects.SetupExplosion(x, y, 0, type, false, false);
        public void SuperPixels(int x, int y, ushort count, byte width, byte color)
            => effects.SpawnSuperPixels(unchecked((ushort)x), unchecked((ushort)y), count, width, color);
    }

    private static void DumpEffects(EnemySystem enemies, SceneEffects effects, PlayerState[] players, StringBuilder output)
    {
        output.Append($"stats:{enemies.KilledEnemies},{enemies.CubeCount},{enemies.SuperArcadePowerUp},{players[0].Cash},{players[1].Cash},{effects.SoundQueue[6]}\n");
        output.Append($"flags:{string.Concat(enemies.GlobalFlags.Select(flag => flag ? '1' : '0'))}\n");
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
    }

    internal static string[] Normalize(string text)
    {
        var lines = new List<string>();
        var state = new List<string>();
        foreach (var line in text.Split('\n'))
        {
            if (line.Contains(':') || line.Length == 0)
            {
                lines.AddRange(state.Order(StringComparer.Ordinal));
                state.Clear();
                lines.Add(line);
            }
            else state.Add(line);
        }
        return lines.ToArray();
    }

    internal static void Dump(EnemySystem system, ItemDatabase items, EnemyState enemy, StringBuilder output)
    {
        foreach (var property in typeof(EnemyState).GetProperties())
        {
            var value = property.GetValue(enemy);
            string text;
            if (property.Name == "Definition") text = items.Enemies.IndexOfReference(enemy.Definition).ToString(CultureInfo.InvariantCulture);
            else if (property.Name == "Sprites")
            {
                var identity = ReferenceEquals(enemy.Sprites, system.SheetEleven) ? 4 :
                    ReferenceEquals(enemy.Sprites, system.SheetTen) ? 5 : system.SpriteSheets.IndexOfReference(enemy.Sprites);
                text = identity.ToString(CultureInfo.InvariantCulture);
            }
            else text = value switch
            {
                bool flag => flag ? "1" : "0",
                IEnumerable array => string.Join(',', array.Cast<object>()),
                IFormattable number => number.ToString(null, CultureInfo.InvariantCulture),
                _ => throw new InvalidOperationException("Unexpected enemy property.")
            };
            output.Append($"{property.Name}={text}\n");
        }
        output.Append($"Total={system.TotalEnemies}\n");
    }

    private static int IndexOfReference<T>(this IReadOnlyList<T> values, T? value) where T : class
    {
        for (var i = 0; i < values.Count; i++)
            if (ReferenceEquals(values[i], value)) return i;
        return -1;
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
