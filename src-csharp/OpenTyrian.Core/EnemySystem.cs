/*
 * OpenTyrian: A modern cross-platform port of Tyrian
 * Copyright (C) 2007-2013 The OpenTyrian Development Team
 * Distributed under the GNU General Public License, version 2 or later.
 * See COPYING for details.
 */

namespace OpenTyrian.Core;

public sealed partial class EnemySystem(ItemDatabase items, TextWriter warnings)
{
    public EnemyState[] Enemies { get; } = Enumerable.Range(0, 100).Select(_ => new EnemyState()).ToArray();
    public byte[] SpriteIds { get; } = new byte[4];
    public SpriteSheet[] SpriteSheets { get; } = Enumerable.Range(0, 4).Select(_ => new SpriteSheet()).ToArray();
    public SpriteSheet SheetTen { get; } = new();
    public SpriteSheet SheetEleven { get; } = new();
    public sbyte Difficulty { get; set; } = 2;
    public byte SuperArcadeMode { get; set; }
    public ushort TotalEnemies { get; set; }
    public ushort KilledEnemies { get; set; }
    public byte ZinglonDuration { get; set; }
    public bool ContinualDamage { get; set; }
    public BossBar[] BossBars { get; } = [new(), new()];
    public ushort CubeCount { get; set; }
    public byte SuperArcadePowerUp { get; set; }
    public ushort Enemy254Jump { get; set; }
    public bool GalagaMode { get; set; }
    public bool[] GlobalFlags { get; } = new bool[10];

    public void ProcessShot(int index, PlayerShots shots, IReadOnlyList<PlayerState> players,
        SceneEffects effects, ushort mouseX, ushort mouseY, Action<ushort> jump)
    {
        if (!ReferenceEquals(shots.Enemies, Enemies) || !ReferenceEquals(shots.Players, players) ||
            !ReferenceEquals(shots.SoundQueue, effects.SoundQueue))
            throw new ArgumentException("Projectile collisions require shared enemy and sound state.", nameof(shots));
        var result = new PlayerShotResult();
        if (!shots.MoveAndDraw(index, ref result)) return;
        unchecked
        {
            for (var enemyIndex = 0; enemyIndex < Enemies.Length; enemyIndex++)
            {
                var enemy = Enemies[enemyIndex];
                if (enemy.Availability != 0) continue;
                var hit = ShotHits(index, enemyIndex, result.IsSpecial, result.X, result.Y,
                    result.RadiusWidth, result.RadiusHeight, players[0].X, result.Chain, result.Damage, result.BlastFilter);
                result.Chain = hit.Chain; result.Damage = hit.Damage; result.BlastFilter = hit.Filter;
                if (!hit.Hit) continue;
                if (result.Chain > 0)
                {
                    shots.MultiPosition[4] = 0;
                    shots.Create(0, 4, (ushort)result.X, (ushort)result.Y, mouseX, mouseY, result.Chain, result.PlayerNumber);
                    shots.Availability[index] = 0;
                    return;
                }
                var shot = shots.Shots[index];
                var infinite = shot.Infinite == 1;
                enemy.Iced = shot.IceTicks;
                var armor = enemy.Armor;
                var link = enemy.LinkNumber == 0 ? 255 : enemy.LinkNumber;
                MarkLinked(enemyIndex, link, result.BlastFilter, shot.IceTicks);
                if (armor > result.Damage)
                {
                    if (index != PlayerShots.Capacity - 1)
                    {
                        if (enemy.Armor != 255)
                        {
                            enemy.Armor = (byte)(enemy.Armor - result.Damage);
                            effects.SetupExplosion(result.X, result.Y, 0, 0, false, false);
                        }
                        else effects.SpawnSuperPixels((ushort)(result.X + 6), (ushort)(result.Y + 6),
                            (ushort)(result.Damage / 2 + 3), (byte)(result.Damage / 4 + 2), result.BlastFilter);
                    }
                    effects.SoundQueue[5] = 3;
                    if (armor - result.Damage <= enemy.DamageLevel && (!enemy.Damaged ^ enemy.DamageAnimation < 0))
                        AnimateDamaged(enemyIndex, link, effects);
                }
                else
                {
                    // The native reserved stripe leaves playerNum uninitialized; its owner is player one.
                    DestroyLinked(enemyIndex, index == PlayerShots.Capacity - 1 ? (byte)1 : result.PlayerNumber,
                        link, players, effects, jump);
                }
                if (infinite || index == PlayerShots.Capacity - 1) continue;
                if (result.Damage <= armor)
                {
                    shots.Availability[index] = 0;
                    return;
                }
                shot.Damage = (byte)(shot.Damage - armor);
            }
        }
    }

    public void AnimateDamaged(int enemyIndex, int link, SceneEffects effects)
    {
        unchecked
        {
            for (var i = 0; i < Enemies.Length; i++)
            {
                var enemy = Enemies[i];
                if (enemy.Availability == 1) continue;
                var linked = enemy.LinkNumber;
                if (!(i == enemyIndex || link != 255 &&
                    (enemy.DamageLevel > 0 && linked == link || ContinualDamage && link - 100 == linked ||
                     linked > 40 && linked / 20 == link / 20 && linked <= link))) continue;
                enemy.Cycle = 1;
                enemy.Damaged = !enemy.Damaged;
                if (enemy.DamageAnimation != 0)
                {
                    enemy.AnimationLength = (byte)Math.Abs((int)enemy.DamageAnimation);
                    enemy.AnimationActive = 1; enemy.AnimationMax = 0;
                    enemy.AnimationMin = (byte)enemy.DamageGraphic;
                    enemy.Cycle = (byte)(enemy.AnimationMin - 1);
                }
                else if (enemy.DamageGraphic > 0)
                {
                    enemy.Graphics[0] = enemy.DamageGraphic;
                    enemy.AnimationLength = 1; enemy.AnimationActive = enemy.AnimationMax = 0; enemy.AnimationMin = 1;
                }
                else
                {
                    enemy.Availability = 1;
                    KilledEnemies++;
                }
                enemy.AnimationWhenFire = 0;
                if (enemy.Armor > (byte)enemy.DamageLevel) enemy.Armor = (byte)enemy.DamageLevel;
                var x = (ushort)(enemy.X + enemy.MapOffset);
                var y = (ushort)enemy.Y;
                if (items.Enemies[enemy.Type].ESize != 1)
                    effects.SetupExplosion(x, y - 6, 0, 1, false, false);
                else effects.SetupLarge(enemy.Ground, (byte)(enemy.ExplosionNumber / 2), (short)x, (short)y);
            }
        }
    }

    public void DestroyLinked(int enemyIndex, byte playerNumber, int link, IReadOnlyList<PlayerState> players,
        SceneEffects effects, Action<ushort> jump)
    {
        if (players.Count != 2) throw new ArgumentException("Enemy rewards require two player slots.", nameof(players));
        if (playerNumber is < 1 or > 2) throw new ArgumentOutOfRangeException(nameof(playerNumber));
        if (link == 254 && Enemy254Jump > 0) jump(Enemy254Jump);
        unchecked
        {
            for (var i = 0; i < Enemies.Length; i++)
            {
                var enemy = Enemies[i];
                if (enemy.Availability == 1) continue;
                var linked = enemy.LinkNumber;
                if (!(i == enemyIndex || link == 254 || link != 255 &&
                    (link == linked || link - 100 == linked || linked > 40 && linked / 20 == link / 20 && linked <= link))) continue;
                var screenX = enemy.X + enemy.MapOffset;
                if (enemy.Special)
                {
                    if (enemy.FlagNumber is < 1 or > 10) throw new InvalidDataException("Enemy event flag is outside the native ten-entry flag array.");
                    GlobalFlags[enemy.FlagNumber - 1] = enemy.SetTo;
                }
                if (enemy.EnemyDie > 0 && !(SuperArcadeMode != 0 && items.Enemies[enemy.EnemyDie].Value == 30000))
                {
                    var definition = items.Enemies[enemy.EnemyDie];
                    var offset = definition.Value > 30000 ? 0 : i - i % 25;
                    var created = Create(offset, enemy.EnemyDie);
                    if (created != 0)
                    {
                        var pickup = Enemies[created - 1];
                        if (SuperArcadeMode != 0 && pickup.Value > 30000)
                        {
                            SuperArcadePowerUp++;
                            if (SuperArcadePowerUp > 5) SuperArcadePowerUp = 1;
                            pickup.Graphics[0] = (ushort)(5 + SuperArcadePowerUp * 2);
                            pickup.Value = (short)(30000 + SuperArcadePowerUp);
                        }
                        pickup.ScoreItem = pickup.Value != 0;
                        pickup.X = enemy.X; pickup.Y = enemy.Y;
                    }
                }
                if (enemy.Value > 0 && enemy.Value < 10000)
                {
                    if (enemy.Value == 1) CubeCount++;
                    else players[GalagaMode ? 0 : playerNumber - 1].Cash += (ulong)enemy.Value;
                }
                if (enemy.DamageLevel == -1 && link == linked)
                {
                    enemy.DamageLevel = 0; enemy.Availability = 2; enemy.Graphics[0] = enemy.DamageGraphic;
                    enemy.AnimationLength = 1; enemy.AnimationActive = enemy.AnimationMax = 0; enemy.AnimationMin = 1;
                    enemy.Damaged = true; enemy.Cycle = 1;
                }
                else
                {
                    enemy.Availability = 1;
                    KilledEnemies++;
                }
                if (items.Enemies[enemy.Type].ESize == 1)
                {
                    effects.SetupLarge(enemy.Ground, enemy.ExplosionNumber, (short)screenX, enemy.Y);
                    effects.SoundQueue[6] = 9;
                }
                else
                {
                    effects.SetupExplosion(screenX, enemy.Y, 0, 1, false, false);
                    effects.SoundQueue[6] = 8;
                }
            }
        }
    }

    public (bool Hit, ushort Chain, short Damage, byte Filter) ShotHits(int shotIndex, int enemyIndex,
        bool special, int shotX, int shotY, ushort width, ushort height, int playerX,
        ushort chain, short damage, byte filter)
    {
        var enemy = Enemies[enemyIndex];
        if (shotIndex == PlayerShots.Capacity - 1)
        {
            var range = unchecked((byte)(25 - Math.Abs(ZinglonDuration - 25)));
            return (Math.Abs(enemy.X + enemy.MapOffset - (playerX + 7)) < range, 0, 10, 9);
        }
        var big = enemy.Cycle == 0;
        var hit = special
            ? Math.Abs(enemy.X + enemy.MapOffset - shotX - width) < (big ? 25 : 13) + width &&
                Math.Abs(enemy.Y - shotY - (big ? 12 : 6) - height) < (big ? 29 : 15) + height
            : Math.Abs(enemy.X + enemy.MapOffset - shotX) < (big ? 25 : 13) &&
                Math.Abs(enemy.Y - shotY - (big ? 12 : 6)) < (big ? 29 : 15);
        return (hit, chain, damage, filter);
    }

    public void MarkLinked(int enemyIndex, int link, int filter, int iced)
    {
        var target = Enemies[enemyIndex];
        if (target.Armor >= 255) return;
        foreach (var bar in BossBars)
            if (link == bar.LinkNumber) bar.Color = 6;
        if (!target.Ground) target.Filter = unchecked((byte)filter);
        foreach (var enemy in Enemies)
        {
            if (enemy.LinkNumber != link || enemy.Availability == 1 || enemy.Ground) continue;
            if (iced != 0) enemy.Iced = unchecked((byte)iced);
            enemy.Filter = unchecked((byte)filter);
        }
    }

    public SpriteSheet? FindSpriteSheet(byte id)
    {
        if (id == 21) return SheetEleven;
        if (id == 26) return SheetTen;
        for (var i = 0; i < SpriteIds.Length; i++)
            if (SpriteIds[i] == id) return SpriteSheets[i];
        return null;
    }

    public short CreateLegacy(int offset, ushort id, short uniqueSprites)
    {
        if (!items.TryFind(ItemType.Enemy, id, out var index))
            throw new InvalidDataException($"Enemy object ID '{id}' is missing.");
        return Create(offset, index, uniqueSprites);
    }

    public short Create(int offset, int definitionIndex, short uniqueSprites = 0)
    {
        if (offset < 0 || offset > Enemies.Length - 25) throw new ArgumentOutOfRangeException(nameof(offset));
        for (var index = offset; index < offset + 25; index++)
        {
            var enemy = Enemies[index];
            if (enemy.Availability != 1) continue;
            enemy.Availability = Initialize(enemy, unchecked((ushort)definitionIndex), uniqueSprites);
            return (short)(index + 1);
        }
        return 0;
    }

    public byte Initialize(EnemyState enemy, ushort definitionIndex, short uniqueSprites = 0)
    {
        if (SuperArcadeMode != 0 && definitionIndex == items.KnownIndex(KnownItem.EnemyRearPowerup))
            definitionIndex = unchecked((ushort)items.KnownIndex(KnownItem.EnemyFrontPowerup));
        var data = items.Enemies[definitionIndex];
        var sprites = FindSpriteSheet(uniqueSprites > 0 ? unchecked((byte)uniqueSprites) : data.ShapeBank);
        if (sprites is not null) enemy.Sprites = sprites;
        else warnings.Write($"warning: ignoring sprite from unloaded shape table {(uniqueSprites > 0 ? unchecked((byte)uniqueSprites) : data.ShapeBank)}\n");
        enemy.Definition = data;
        enemy.MapOffset = 0;
        Array.Clear(enemy.ShotMultiPosition);
        enemy.Ground = data.EnemyGround;
        enemy.ExplosionNumber = data.ExplosionType;
        enemy.LaunchFrequency = enemy.LaunchWait = data.ELaunchFreq;
        enemy.LaunchType = unchecked((ushort)data.ELaunchType);
        enemy.LaunchTypePosition = data.ELaunchTypePos;
        enemy.LaunchSpecial = data.ELaunchSpecial;
        enemy.XAcceleration = unchecked((byte)data.XAccel);
        enemy.YAcceleration = unchecked((byte)data.YAccel);
        enemy.XMinBounce = enemy.YMinBounce = -10000;
        enemy.XMaxBounce = enemy.YMaxBounce = 10000;
        data.Tur.CopyTo(enemy.Turrets, 0);
        data.EShotWait.CopyTo(enemy.ShotWait, 0);
        enemy.AnimationLength = data.Ani;
        enemy.AnimationMin = 1;
        switch (data.Animate)
        {
            case 0:
                enemy.Cycle = 1; enemy.AnimationActive = enemy.AnimationMax = enemy.AnimationWhenFire = 0;
                break;
            case 1:
                enemy.Cycle = 0; enemy.AnimationActive = 1; enemy.AnimationMax = enemy.AnimationWhenFire = 0;
                break;
            case 2:
                enemy.Cycle = 1; enemy.AnimationActive = enemy.AnimationWhenFire = 2; enemy.AnimationMax = enemy.AnimationLength;
                break;
        }
        enemy.X = RandomStart(data.StartX, data.StartXc);
        enemy.Y = RandomStart(data.StartY, data.StartYc);
        enemy.XSpeed = data.XMove; enemy.YSpeed = data.YMove;
        enemy.XFixedAcceleration = data.XcAccel; enemy.YFixedAcceleration = data.YcAccel;
        enemy.XAccelerationWait = unchecked((sbyte)Math.Abs((int)enemy.XFixedAcceleration));
        enemy.YAccelerationWait = unchecked((sbyte)Math.Abs((int)enemy.YFixedAcceleration));
        enemy.XAccelerationWaitMax = unchecked((byte)enemy.XAccelerationWait);
        enemy.YAccelerationWaitMax = unchecked((byte)enemy.YAccelerationWait);
        enemy.XAccelerationAdd = (short)(enemy.XFixedAcceleration > 0 ? 1 : -1);
        enemy.YAccelerationAdd = (short)(enemy.YFixedAcceleration > 0 ? 1 : -1);
        enemy.Special = false; enemy.Iced = 0;
        enemy.XReverse = data.XRev switch { 0 => 100, -99 => 0, _ => data.XRev };
        enemy.YReverse = data.YRev switch { 0 => 100, -99 => 0, _ => data.YRev };
        enemy.XRandomAcceleration = (sbyte)(enemy.XAcceleration > 0 ? 1 : -1);
        enemy.YRandomAcceleration = (sbyte)(enemy.YAcceleration > 0 ? 1 : -1);
        enemy.Type = definitionIndex;
        data.EGraphic.CopyTo(enemy.Graphics, 0);
        enemy.Size = data.ESize; enemy.LinkNumber = 0;
        enemy.Damaged = data.DAni < 0;
        enemy.EnemyDie = unchecked((ushort)data.EEnemyDie);
        data.Freq.CopyTo(enemy.Frequency, 0);
        enemy.DamageAnimation = data.DAni; enemy.DamageGraphic = data.Dgr; enemy.DamageLevel = data.DLevel;
        enemy.FixedMoveY = 0; enemy.Filter = 0;
        return InitializeScoreAndArmor(enemy, data);
    }

    public int ScaleScore(EnemyItem data)
    {
        var value = (int)data.Value;
        if (value <= 1 || value >= 10000) return value;
        var score = Difficulty switch
        {
            -1 or 0 => (int)(value * 0.75f),
            1 or 2 => value,
            3 => (int)(value * 1.125f),
            4 => (int)(value * 1.5f),
            5 => value * 2,
            6 => (int)(value * 2.5f),
            7 or 8 => value * 4,
            9 or 10 => value * 8,
            _ => 0
        };
        return Math.Min(score, 10000);
    }

    public int ScaleArmor(EnemyItem data)
    {
        var armor = (int)data.Armor;
        if (armor == 0) return 1;
        if (armor == 255) return 255;
        armor = Difficulty switch
        {
            -1 or 0 => (int)(armor * 0.5f + 1),
            1 => (int)(armor * 0.75f + 1),
            2 => armor,
            3 => (int)(armor * 1.2f),
            4 => (int)(armor * 1.5f),
            5 => (int)(armor * 1.8f),
            6 => armor * 2,
            7 => armor * 3,
            8 => armor * 4,
            9 or 10 => armor * 8,
            _ => 1
        };
        return Math.Min(armor, 254);
    }

    private byte InitializeScoreAndArmor(EnemyState enemy, EnemyItem data)
    {
        enemy.Value = unchecked((short)ScaleScore(data));
        if (data.Armor > 0)
        {
            enemy.Armor = (byte)ScaleArmor(data);
            enemy.ScoreItem = false;
            TotalEnemies = unchecked((ushort)(TotalEnemies + 1));
            return 0;
        }
        enemy.Armor = 255;
        // Native zero-value armorless definitions retain the prior occupant's score flag.
        if (enemy.Value != 0) enemy.ScoreItem = true;
        if (!enemy.ScoreItem) TotalEnemies = unchecked((ushort)(TotalEnemies + 1));
        return 2;
    }

    private static short RandomStart(short position, sbyte radius)
    {
        if (radius == 0) return unchecked((short)(position + 1));
        // Unsigned-long RNG promotes a negative C divisor to the platform's ulong width.
        var divisor = OperatingSystem.IsWindows() || IntPtr.Size == 4
            ? unchecked((uint)(radius * 2)) : unchecked((ulong)(long)(radius * 2));
        return unchecked((short)((ulong)position + MersenneTwister.NextUInt32() % divisor - (ulong)radius + 1));
    }
}
