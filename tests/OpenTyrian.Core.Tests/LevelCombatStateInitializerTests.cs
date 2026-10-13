using System.Diagnostics;
using System.Text;
using OpenTyrian.Core;

namespace OpenTyrian.Core.Tests;

internal static class LevelCombatStateInitializerTests
{
    public static void Run(string? executable = null)
    {
        var trace = new StringBuilder();
        for (var mode = 0; mode < 256; mode++)
        {
            var fixture = new Fixture(mode);
            var buttons = new[] { true, true, true, true };
            var drawCount = 0;
            var initializer = new LevelCombatStateInitializer(fixture.Game, fixture.Combat,
                fixture.Movement, fixture.Weapons, fixture.Special, fixture.Shots, fixture.Enemies,
                fixture.Effects, fixture.Backgrounds, fixture.Level, fixture.Requests,
                () => { drawCount++; trace.AppendLine("draw"); });
            trace.AppendLine($"case:{mode}");
            initializer.Initialize((mode & 1) != 0, buttons);
            if (drawCount != 1 || buttons.Any(value => value))
                throw new InvalidOperationException("Level combat initialization missed its option draw or gameplay-button reset.");
            AssertState(fixture, mode);
            AppendState(trace, fixture, buttons);
        }

        if (executable is null) return;
        using var process = Process.Start(new ProcessStartInfo(executable)
        { RedirectStandardOutput = true, RedirectStandardError = true })
            ?? throw new InvalidOperationException("Cannot start level combat state reference.");
        var output = process.StandardOutput.ReadToEndAsync();
        var errors = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(30000))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException("Level combat state reference timed out.");
        }
        var expected = output.GetAwaiter().GetResult().Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var actual = trace.ToString().Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        if (process.ExitCode != 0 || !expected.SequenceEqual(actual))
        {
            var index = Enumerable.Range(0, Math.Min(expected.Length, actual.Length))
                .FirstOrDefault(i => expected[i] != actual[i], -1);
            throw new InvalidOperationException($"Level combat initialization differs at {index}: " +
                $"{expected.ElementAtOrDefault(index)} / {actual.ElementAtOrDefault(index)}; {errors.GetAwaiter().GetResult()}");
        }
    }

    private static void AssertState(Fixture f, int mode)
    {
        if (f.Combat.Linked || f.Movement.LinkGunDirection != (float)Math.PI ||
            f.Game.Players[0].PurpleBallsNeeded != PurpleBalls[(mode * 5) % 12] ||
            f.Game.Players[1].PurpleBallsNeeded != PurpleBalls[(mode * 5 + 3) % 12] ||
            f.Pickups.DamageRate != 2 || f.Weapons.ChargeWait != 5 || f.Weapons.ChargeLevel != 0 ||
            f.Weapons.ChargeMaximum != 5 || f.Weapons.ChargeGraphic != 0 || f.Weapons.ChargeGraphicWait != 3 ||
            f.Weapons.PortConfigChange || f.Enemies.TotalEnemies != 0 || f.Enemies.KilledEnemies != 0 ||
            f.Special.AstralDuration != 0 || f.Enemies.SuperArcadePowerUp != 1 ||
            f.Requests.YourInGameMenuRequest || f.Movement.ConstantLastX != -1 ||
            f.Game.Players.Any(player => player.ExplodingTicks != 0) ||
            f.Enemies.Enemies.Any(enemy => enemy.Availability != ((mode & 1) != 0 ? (byte)1 : enemy.Availability)) ||
            f.Enemies.EnemyShots.Any(shot => !shot.Available) ||
            f.Shots.Availability.Any(value => value != 0) || f.Shots.MultiPosition.Any(value => value != 0) ||
            f.Shots.Repeat.Any(value => value != 1) || f.Enemies.GlobalFlags.Any(value => value) ||
            f.Enemies.SpriteIds.Any(value => value != 0) || f.Weapons.SpecialCodeProgress.Any(code => code.Any(value => value != 0)) ||
            f.Weapons.ExecutedSpecial.Any(value => value != 0) || f.Enemies.ZinglonDuration != 0 ||
            f.Special.ZinglonDuration != 0 || f.Special.Wait != 0 || f.Special.NextWait != 0 ||
            f.Movement.AttachmentMove != 0 || !f.Movement.AttachmentLinked || f.Movement.AttachmentReturn ||
            f.Game.EditShip1 || f.Game.EditShip2 || f.Backgrounds.Smoothies.Any(value => value) ||
            f.Combat.LevelTimer || f.Level.RandomExplosions || f.Level.ReturnActive ||
            f.Enemies.GalagaShotFrequency != 0 || f.Enemies.GalagaLife != 10000 ||
            ((mode & 2) != 0 && f.Enemies.Difficulty != 2) ||
            f.Effects.LastSuperPixel != 0 || f.Effects.SoundQueue[3] != 33 ||
            f.Backgrounds.First.WrapAt != 14 || f.Backgrounds.First.WrapTo != 14 ||
            f.Backgrounds.Second.WrapAt != 14 || f.Backgrounds.Second.WrapTo != 14 ||
            f.Backgrounds.Third.WrapAt != 15 || f.Backgrounds.Third.WrapTo != 15)
            throw new InvalidOperationException($"Level combat state was not reset for scenario {mode}.");

        foreach (var shot in f.Shots.Shots)
            if (shot.GetType().GetProperties().Where(property => property.CanRead && property.PropertyType.IsValueType)
                .Any(property => !Equals(property.GetValue(shot), Activator.CreateInstance(property.PropertyType))))
                throw new InvalidOperationException($"Player projectile state survived reset for scenario {mode}.");
        foreach (var enemy in f.Enemies.Enemies)
        {
            if (enemy.GetType().GetProperties().Where(property => property.CanRead && property.PropertyType.IsValueType &&
                    property.Name is not nameof(EnemyState.Availability))
                .Any(property => !Equals(property.GetValue(enemy), Activator.CreateInstance(property.PropertyType))))
                throw new InvalidOperationException($"Enemy payload survived reset for scenario {mode}.");
            if (enemy.ShotWait.Any(value => value != 0) || enemy.ShotMultiPosition.Any(value => value != 0) ||
                enemy.Graphics.Any(value => value != 0) || enemy.Frequency.Any(value => value != 0) ||
                enemy.Turrets.Any(value => value != 0))
                throw new InvalidOperationException($"Enemy array state survived reset for scenario {mode}.");
        }
        if (f.Effects.Explosions.Any(entry => entry.Lifetime != 0 || entry.X != 0 || entry.Y != 0 ||
                entry.DeltaX != 0 || entry.DeltaY != 0 || entry.FixedPosition || entry.FollowPlayer || entry.Sprite != 0) ||
            f.Effects.Repeating.Any(entry => entry.Delay != 0 || entry.Lifetime != 0 || entry.X != 0 ||
                entry.Y != 0 || entry.Big) ||
            f.Effects.SuperPixels.Any(entry => entry.X != 0 || entry.Y != 0 || entry.Z != 0 ||
                entry.DeltaX != 0 || entry.DeltaY != 0 || entry.Color != 0))
            throw new InvalidOperationException($"Scene effects survived reset for scenario {mode}.");
    }

    private static void AppendState(StringBuilder trace, Fixture f, bool[] buttons)
    {
        trace.AppendLine($"state:{B(f.Combat.Linked)},{(int)(f.Movement.LinkGunDirection * 1000)},{f.Pickups.DamageRate}," +
            $"{f.Weapons.ChargeWait},{f.Weapons.ChargeLevel},{f.Weapons.ChargeMaximum},{f.Weapons.ChargeGraphic}," +
        $"{f.Weapons.ChargeGraphicWait},{B(f.Weapons.PortConfigChange)},{f.Enemies.TotalEnemies},{f.Enemies.KilledEnemies}," +
        $"{f.Special.AstralDuration},{f.Enemies.SuperArcadePowerUp},{B(f.Requests.YourInGameMenuRequest)}," +
        $"{f.Movement.ConstantLastX},{f.Enemies.Difficulty},{f.Movement.AttachmentMove}," +
        $"{B(f.Movement.AttachmentLinked)},{B(f.Game.EditShip1)},{B(f.Game.EditShip2)},{B(f.Combat.LevelTimer)}," +
        $"{B(f.Level.RandomExplosions)},{B(f.Level.ReturnActive)},{f.Enemies.GalagaShotFrequency},{f.Enemies.GalagaLife}," +
            $"{f.Backgrounds.First.WrapAt},{f.Backgrounds.First.WrapTo},{f.Backgrounds.Second.WrapAt}," +
            $"{f.Backgrounds.Second.WrapTo},{f.Backgrounds.Third.WrapAt},{f.Backgrounds.Third.WrapTo}," +
            $"{f.Effects.SoundQueue[3]},{f.Game.Players[0].PurpleBallsNeeded},{f.Game.Players[0].ExplodingTicks}," +
            $"{f.Game.Players[1].PurpleBallsNeeded},{f.Game.Players[1].ExplodingTicks}," +
        $"{B(f.Enemies.Enemies.All(enemy => enemy.Availability == 1))},{B(f.Enemies.EnemyShots.All(shot => shot.Available))}," +
        $"{B(f.Shots.Shots.All(ShotIsClear))},{B(f.Shots.Availability.All(value => value == 0) && f.Shots.MultiPosition.All(value => value == 0) && f.Shots.Repeat.All(value => value == 1) && buttons.All(value => !value))}," +
        $"{B(EnemyPayloadsAreClear(f.Enemies) && ScenePayloadsAreClear(f) && f.Enemies.GlobalFlags.All(value => !value) && f.Enemies.SpriteIds.All(value => value == 0) && f.Weapons.SpecialCodeProgress.All(code => code.All(value => value == 0)) && f.Weapons.ExecutedSpecial.All(value => value == 0) && f.Backgrounds.Smoothies.All(value => !value))}," +
        $"{f.Effects.LastSuperPixel}");
    }

    private static string B(bool value) => value ? "1" : "0";
    private static bool ShotIsClear(PlayerShot shot) =>
        shot.GetType().GetProperties().Where(property => property.CanRead && property.PropertyType.IsValueType)
        .All(property => Equals(property.GetValue(shot), Activator.CreateInstance(property.PropertyType)));
    private static bool EnemyPayloadsAreClear(EnemySystem enemies) => enemies.Enemies.All(enemy =>
        enemy.GetType().GetProperties().Where(property => property.CanRead && property.PropertyType.IsValueType &&
        property.Name is not nameof(EnemyState.Availability))
        .All(property => Equals(property.GetValue(enemy), Activator.CreateInstance(property.PropertyType))) &&
        enemy.ShotWait.All(value => value == 0) && enemy.ShotMultiPosition.All(value => value == 0) &&
        enemy.Graphics.All(value => value == 0) && enemy.Frequency.All(value => value == 0) && enemy.Turrets.All(value => value == 0));
    private static bool ScenePayloadsAreClear(Fixture f) =>
        f.Effects.Explosions.All(entry => entry.Lifetime == 0 && entry.X == 0 && entry.Y == 0 &&
        entry.DeltaX == 0 && entry.DeltaY == 0 && !entry.FixedPosition && !entry.FollowPlayer && entry.Sprite == 0) &&
        f.Effects.Repeating.All(entry => entry.Delay == 0 && entry.Lifetime == 0 && entry.X == 0 && entry.Y == 0 && !entry.Big) &&
        f.Effects.SuperPixels.All(entry => entry.X == 0 && entry.Y == 0 && entry.Z == 0 &&
        entry.DeltaX == 0 && entry.DeltaY == 0 && entry.Color == 0);

    private static readonly uint[] PurpleBalls = [1, 1, 2, 4, 8, 12, 16, 20, 25, 30, 40, 50];

    private sealed class Fixture
    {
        public LegacyGameState Game { get; } = new();
        public PlayerCombat Combat { get; }
        public PlayerMovement Movement { get; }
        public PlayerWeapons Weapons { get; }
        public PlayerSpecialWeapons Special { get; }
        public PlayerShots Shots { get; }
        public EnemySystem Enemies { get; }
        public SceneEffects Effects { get; }
        public BackgroundRenderer Backgrounds { get; } = new();
        public LevelEvents Level { get; }
        public PlayerPickups Pickups { get; }
        public NetworkRequests Requests { get; } = new();

        public Fixture(int mode)
        {
            var definitions = new ItemDefinitions();
            definitions.ParseBundle(Encoding.Latin1.GetBytes(ItemDatabaseTests.Fixture(0)), "level-combat-state.tyrr");
            var items = ItemDatabase.FromDefinitions(definitions);
            var surface = new IndexedSurface(320, 200, 320, TextWriter.Null);
            Effects = new(new byte[8]);
            Combat = new(Game.Players, Effects, surface);
            Enemies = new(items, TextWriter.Null);
            Level = new(items, Enemies, Game.Players, Backgrounds, Effects);
            Shots = new(items.Shots, items.Weapons, Game.Players,
                new SpritePlayerShotEffects(surface, new(), new(), new(), (_, _, _) => { }, (_, _, _, _, _) => { }),
                Enemies.Enemies, Effects.SoundQueue);
            Weapons = new(Shots, items, Combat);
            Special = new(items, Shots, Weapons, Combat, Enemies, Effects, Level, Backgrounds);
            Movement = new(Game.Players, Combat, Level, Shots, items);
            Pickups = new(Game.Players, Enemies, Combat, Shots, items, Level, Effects, [], []);
            Game.GalagaMode = (mode & 2) != 0;
            Enemies.Difficulty = (sbyte)(mode % 11);
            Combat.LevelTimer = true;
            Level.RandomExplosions = Level.ReturnActive = true;
            Requests.YourInGameMenuRequest = true;
            Effects.LastSuperPixel = (uint)(mode + 1);
            Effects.SoundQueue[3] = 77;
            Movement.LinkGunDirection = 0;
            Movement.ConstantLastX = (sbyte)mode;
            Movement.AttachmentMove = (short)(mode + 1);
            Movement.AttachmentLinked = false;
            Special.NextWait = Special.Wait = Special.AstralDuration = Special.ZinglonDuration = (byte)(mode + 1);
            Enemies.ZinglonDuration = (byte)(mode + 1);
            Enemies.TotalEnemies = Enemies.KilledEnemies = (ushort)(mode + 1);
            Enemies.GalagaShotFrequency = (ushort)(mode + 1);
            Enemies.GalagaLife = mode + 1;
            Enemies.SuperArcadePowerUp = (byte)(mode + 3);
            Weapons.ChargeWait = Weapons.ChargeLevel = Weapons.ChargeGraphic = 77;
            Weapons.ChargeMaximum = Weapons.ChargeGraphicWait = 77;
            Weapons.PortConfigChange = true;
            Pickups.DamageRate = 77;
            Game.EditShip1 = Game.EditShip2 = true;
            for (var i = 0; i < 2; i++)
            {
                var player = Game.Players[i];
                player.Lives = (byte)((mode * 5 + i * 3) % 12);
                player.PurpleBallsNeeded = 99;
                player.ExplodingTicks = (uint)(mode + i + 1);
            }
            for (var i = 0; i < Enemies.Enemies.Length; i++)
            {
                var enemy = Enemies.Enemies[i];
                enemy.Availability = (byte)(mode % 3);
                enemy.X = enemy.Y = 17;
                enemy.ScoreItem = enemy.Damaged = enemy.Ground = enemy.Special = enemy.SetTo = true;
                enemy.XSpeed = enemy.YSpeed = enemy.XRandomAcceleration = enemy.YRandomAcceleration = 8;
                enemy.XFixedAcceleration = enemy.YFixedAcceleration = enemy.XAccelerationWait = enemy.YAccelerationWait = 7;
                enemy.Armor = enemy.Cycle = enemy.AnimationLength = enemy.Size = enemy.LinkNumber = 9;
                enemy.AnimationActive = enemy.AnimationMax = enemy.AnimationWhenFire = 10;
                Array.Fill(enemy.ShotWait, (byte)11); Array.Fill(enemy.ShotMultiPosition, (byte)12);
                Array.Fill(enemy.Graphics, (ushort)13);
                enemy.Sprites = new(); enemy.XReverse = enemy.YReverse = 14;
                enemy.XAccelerationAdd = enemy.YAccelerationAdd = 15;
                enemy.XAccelerationWaitMax = enemy.YAccelerationWaitMax = 16;
                enemy.Definition = new EnemyItem(); enemy.Type = 17; enemy.AnimationMin = 18;
                enemy.DamageGraphic = 19; enemy.DamageLevel = enemy.DamageAnimation = 20; enemy.Filter = 21;
                enemy.Value = enemy.FixedMoveY = 22; Array.Fill(enemy.Frequency, (byte)23);
                enemy.LaunchWait = 24; enemy.LaunchType = enemy.LaunchTypePosition = 25;
                enemy.LaunchFrequency = enemy.XAcceleration = enemy.YAcceleration = 26;
                Array.Fill(enemy.Turrets, (byte)27); enemy.EnemyDie = 28; enemy.ExplosionNumber = 29;
                enemy.MapOffset = 30; enemy.FlagNumber = 31; enemy.Iced = enemy.LaunchSpecial = 32;
                enemy.XMinBounce = enemy.XMaxBounce = enemy.YMinBounce = enemy.YMaxBounce = 33;
            }
            Array.Fill(Enemies.SpriteIds, (byte)90); Array.Fill(Enemies.GlobalFlags, true);
            foreach (var shot in Enemies.EnemyShots) shot.Available = false;
            foreach (var shot in Shots.Shots) Seed(shot);
            Array.Fill(Shots.Availability, (byte)90); Array.Fill(Shots.MultiPosition, (byte)91);
            Array.Fill(Shots.Repeat, (byte)92);
            foreach (var explosion in Effects.Explosions)
            { explosion.Lifetime = 1; explosion.X = explosion.Y = 1; explosion.DeltaX = explosion.DeltaY = 1; explosion.FixedPosition = explosion.FollowPlayer = true; explosion.Sprite = 1; }
            foreach (var explosion in Effects.Repeating)
            { explosion.Delay = explosion.Lifetime = explosion.X = explosion.Y = 1; explosion.Big = true; }
            foreach (var pixel in Effects.SuperPixels)
            { pixel.X = pixel.Y = pixel.Z = 1; pixel.DeltaX = pixel.DeltaY = 1; pixel.Color = 1; }
            foreach (var code in Weapons.SpecialCodeProgress) Array.Fill(code, (byte)90);
            Array.Fill(Weapons.ExecutedSpecial, (byte)90);
            Array.Fill(Backgrounds.Smoothies, true);
        }

        private static void Seed(PlayerShot shot)
        {
            shot.X = shot.Y = shot.XM = shot.YM = shot.XC = shot.YC = 1;
            shot.Complicated = true; shot.DevX = shot.DirX = shot.DevY = shot.DirY = 1;
            shot.CircleSizeX = shot.CircleSizeY = 1; shot.Trail = 1;
            shot.Graphic = shot.Animation = shot.AnimationMax = shot.Chain = 1;
            shot.Damage = shot.BlastFilter = shot.PlayerNumber = shot.AimAtEnemy = 1;
            shot.AimDelay = shot.AimDelayMax = shot.IceTicks = shot.Infinite = 1;
        }
    }
}
