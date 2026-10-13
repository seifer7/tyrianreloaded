/*
 * OpenTyrian: A modern cross-platform port of Tyrian
 * Copyright (C) 2007-2013 The OpenTyrian Development Team
 * Distributed under the GNU General Public License, version 2 or later.
 * See COPYING for details.
 */

namespace OpenTyrian.Core;

public sealed class EnemyShot
{
    public bool Available { get; set; } = true;
    public short X { get; set; }
    public short Y { get; set; }
    public short XVelocity { get; set; }
    public short YVelocity { get; set; }
    public sbyte XAcceleration { get; set; }
    public sbyte YAcceleration { get; set; }
    public byte TrackX { get; set; }
    public byte TrackY { get; set; }
    public ushort Graphic { get; set; }
    public byte Damage { get; set; }
    public byte Duration { get; set; }
    public ushort Animation { get; set; }
    public ushort AnimationMax { get; set; }
}

public sealed partial class EnemySystem
{
    public EnemyShot[] EnemyShots { get; } = Enumerable.Range(0, 60).Select(_ => new EnemyShot()).ToArray();
    public bool TwoPlayers { get; set; }
    public ushort GalagaShotFrequency { get; set; }
    public int GalagaLife { get; set; }
    public ushort EnemiesOnScreen { get; set; }
    public ushort FirstBackgroundMove { get; set; }

    public void AimShot(int index, int weaponIndex, ushort enemyX, ushort enemyY, IReadOnlyList<PlayerState> players)
    {
        var speed = (int)items.Shots[weaponIndex].Aim;
        if (Difficulty > 2) speed += Difficulty - 2;
        var targetX = unchecked((ushort)players[0].X);
        var targetY = unchecked((ushort)players[0].Y);
        if (TwoPlayers)
        {
            var target = players[0].IsAlive && !players[1].IsAlive ? 0 :
                players[1].IsAlive && !players[0].IsAlive ? 1 : MersenneTwister.NextUInt32() % 2;
            if (target == 1)
            {
                targetX = unchecked((ushort)(players[1].X - 25));
                targetY = unchecked((ushort)players[1].Y);
            }
        }
        var velocity = AimVelocity(targetX, targetY, enemyX, enemyY, speed);
        EnemyShots[index].XVelocity = velocity.X;
        EnemyShots[index].YVelocity = velocity.Y;
    }

    public bool CreateEnemyShot(int enemyIndex, int turret, int weaponIndex,
        IReadOnlyList<PlayerState> players, SceneEffects effects)
    {
        if (turret is < 1 or > 3) throw new ArgumentOutOfRangeException(nameof(turret));
        var index = Array.FindIndex(EnemyShots, shot => shot.Available);
        if (index < 0) return false;
        var shot = EnemyShots[index];
        shot.Available = false;
        var data = items.Shots[weaponIndex];
        var enemy = Enemies[enemyIndex];
        if (data.Sound > 0) effects.SoundQueue[RandomSoundChannel()] = data.Sound;
        if (enemy.AnimationActive == 2) enemy.AnimationActive = 1;
        var multi = unchecked((byte)(enemy.ShotMultiPosition[turret - 1] + 1));
        if (multi > data.Max) multi = 1;
        enemy.ShotMultiPosition[turret - 1] = multi;
        if (multi is < 1 or > 8) throw new InvalidDataException("Enemy shot pattern exceeds the native eight-entry arrays.");
        var position = multi - 1;
        unchecked
        {
            shot.X = (short)((ushort)enemy.X + data.Bx[position] + MapXOffset);
            shot.Y = (short)((ushort)enemy.Y + data.By[position]);
            shot.Damage = data.Attack[position]; shot.TrackX = data.Tx; shot.TrackY = data.Ty;
            shot.Duration = data.Del[position]; shot.Animation = 0; shot.AnimationMax = data.WeapAni;
            shot.Graphic = data.Sg[position];
            switch (turret)
            {
                case 1:
                    shot.YAcceleration = data.Acceleration; shot.XAcceleration = data.AccelerationX;
                    shot.XVelocity = data.Sx[position]; shot.YVelocity = data.Sy[position];
                    break;
                case 2:
                    shot.XAcceleration = data.Acceleration; shot.YAcceleration = (sbyte)-data.Acceleration;
                    shot.XVelocity = data.Sy[position]; shot.YVelocity = (short)-data.Sx[position];
                    break;
                case 3:
                    shot.XAcceleration = (sbyte)-data.Acceleration; shot.YAcceleration = data.AccelerationX;
                    shot.XVelocity = (short)-data.Sy[position]; shot.YVelocity = (short)-data.Sx[position];
                    break;
            }
        }
        if (data.Aim > 0) AimShot(index, weaponIndex, unchecked((ushort)enemy.X), unchecked((ushort)enemy.Y), players);
        return true;
    }

    public bool ApplyWeaponSpecial(int index, int turret, int weaponId, IReadOnlyList<PlayerState> players, SceneEffects effects)
    {
        var enemy = Enemies[index];
        var x = unchecked((ushort)enemy.X);
        var y = unchecked((ushort)enemy.Y);
        unchecked
        {
            switch (weaponId)
            {
                case 252:
                    if (enemy.Y > 20)
                    {
                        effects.SetupExplosion(x - 8 + MapXOffset, y - 20 - FirstBackgroundMove * 8, -2, 6, false, false);
                        effects.SetupExplosion(x + 4 + MapXOffset, y - 20 - FirstBackgroundMove * 8, -2, 6, false, false);
                    }
                    return true;
                case 251:
                    var attraction = 4 - (Math.Abs(players[0].X - x) + Math.Abs(players[0].Y - y)) / 100;
                    players[0].XVelocity += players[0].X > x ? -attraction : attraction;
                    return true;
                case 253 or 254:
                    var push = weaponId == 253 ? 2 : -2;
                    if (Math.Abs(players[0].X + 25 - 14 - x) < 24 && Math.Abs(players[0].Y - y) < 28)
                        players[0].XVelocity += push;
                    if (TwoPlayers && Math.Abs(players[1].X - 14 - x) < 24 && Math.Abs(players[1].Y - y) < 28)
                        players[1].XVelocity += push;
                    return true;
                case 255:
                    if (Difficulty != 1)
                    {
                        if (turret == 3) enemy.Filter = 0x70;
                        else
                        {
                            var repulsion = 4 - (Math.Abs(players[0].X - x) + Math.Abs(players[0].Y - y)) / 20;
                            if (repulsion > 0) players[0].XVelocity += players[0].X > x ? repulsion : -repulsion;
                        }
                    }
                    return true;
                default: return false;
            }
        }
    }

    public bool FireShots(int index, IReadOnlyList<PlayerState> players, SceneEffects effects)
    {
        var enemy = Enemies[index];
        for (var turret = 3; turret > 0; turret--)
        {
            var slot = turret - 1;
            if (enemy.Frequency[slot] == 0) continue;
            var weaponId = enemy.Turrets[slot];
            if (!items.TryFind(ItemType.Shot, weaponId, out var weaponIndex))
                throw new InvalidDataException($"Enemy turret shot ID '{weaponId}' is missing.");
            enemy.ShotWait[slot] = unchecked((byte)(enemy.ShotWait[slot] - 1));
            if (enemy.ShotWait[slot] != 0 || weaponIndex == 0) continue;
            enemy.ShotWait[slot] = enemy.Frequency[slot];
            if (Difficulty > 2)
            {
                enemy.ShotWait[slot] = (byte)(enemy.ShotWait[slot] / 2 + 1);
                if (Difficulty > 7) enemy.ShotWait[slot] = (byte)(enemy.ShotWait[slot] / 2 + 1);
            }
            if (GalagaMode && (enemy.YSpeed == 0 || MersenneTwister.NextUInt32() % 400 >= GalagaShotFrequency)) return false;
            if (ApplyWeaponSpecial(index, turret, weaponId, players, effects)) continue;
            for (var count = (int)items.Shots[weaponIndex].Multi; count > 0; count--)
                if (!CreateEnemyShot(index, turret, weaponIndex, players, effects)) return false;
        }
        return true;
    }

    public bool ProcessAttacks(int index, IReadOnlyList<PlayerState> players, SceneEffects effects)
    {
        var enemy = Enemies[index];
        if (enemy.Damaged) return false;
        EnemiesOnScreen = unchecked((ushort)(EnemiesOnScreen + 1));
        if (enemy.Iced > 0)
        {
            enemy.Iced--;
            if (!enemy.Ground) enemy.Filter = 9;
            return false;
        }
        return FireShots(index, players, effects);
    }

    public void LaunchFromParent(int index, int offset, IReadOnlyList<PlayerState> players, SceneEffects effects)
    {
        var parent = Enemies[index];
        if (parent.LaunchFrequency == 0 || --parent.LaunchWait != 0) return;
        parent.LaunchWait = parent.LaunchFrequency;
        if (parent.LaunchSpecial != 0 && Math.Abs(parent.Y - players[0].Y) > 5) return;
        if (parent.AnimationActive == 2) parent.AnimationActive = 1;
        if (parent.LaunchType == 0) return;
        var x = unchecked((ushort)parent.X);
        var y = unchecked((ushort)parent.Y);
        var created = Create(offset == 50 ? 75 : offset - 25, parent.LaunchType);
        if (created == 0) return;
        var child = Enemies[created - 1];
        unchecked
        {
            child.X = (short)x;
            child.Y = (short)(y + items.Enemies[child.Type].StartYc);
            if (child.Size == 0) child.Y -= 7;
            if (child.LaunchTypePosition > 0 && child.LaunchFrequency == 0)
            {
                if (child.LaunchTypePosition > 90)
                    child.X = (short)(child.X + MersenneTwister.NextUInt32() % ((child.LaunchTypePosition - 90u) * 4) -
                        (child.LaunchTypePosition - 90u) * 2);
                else
                {
                    var velocity = AimVelocity(players[0].X, players[0].Y, x, y, child.LaunchTypePosition);
                    child.XSpeed = (sbyte)velocity.X; child.YSpeed = (sbyte)velocity.Y;
                }
            }
        }
        var channel = RandomSoundChannel();
        effects.SoundQueue[channel] = new byte[] { 13, 6, 26 }[MersenneTwister.NextUInt32() % 3];
        if (parent.LaunchSpecial == 1 && parent.LinkNumber < 100) child.LinkNumber = parent.LinkNumber;
    }

    public void DrawAndUpdate(int offset, IReadOnlyList<PlayerState> players, SceneEffects effects,
        Action<SpriteSheet, int, int, uint, byte> draw)
    {
        if (offset is < 25 or > 100 || offset % 25 != 0) throw new ArgumentOutOfRangeException(nameof(offset));
        if (players.Count != 2) throw new ArgumentException("Enemy updates require two player slots.", nameof(players));
        var originalX = players[0].X;
        players[0].X = unchecked(originalX - 25);
        try
        {
            for (var i = offset - 25; i < offset; i++)
            {
                var enemy = Enemies[i];
                if (enemy.Availability == 1) continue;
                enemy.MapOffset = MapXOffset;
                UpdateAcceleration(i, players[0].X, players[0].Y);
                if (!AnimateAndDraw(i, draw))
                {
                    enemy.Availability = 1;
                    continue;
                }
                if (!UpdatePosition(i) || !ProcessAttacks(i, players, effects)) continue;
                LaunchFromParent(i, offset, players, effects);
            }
        }
        finally { players[0].X = originalX; }
    }

    private static int RandomSoundChannel()
    {
        int channel;
        do { channel = (int)(MersenneTwister.NextUInt32() % 8); } while (channel == 3);
        return channel;
    }

    public void UpdateEnemyShots(bool endLevel, IReadOnlyList<PlayerState> players, SceneEffects effects,
        PlayerCombat combat, SpriteSheet first, SpriteSheet second, IndexedSurface surface)
    {
        if (!ReferenceEquals(combat.Players, players) || !ReferenceEquals(combat.Effects, effects) ||
            combat.TwoPlayers != TwoPlayers || combat.Galaga != GalagaMode)
            throw new ArgumentException("Enemy shots require shared combat players and scene effects.", nameof(combat));
        UpdateEnemyShots(endLevel, players, effects, combat.Damage,
            (sheet, x, y, frame) => (sheet == 8 ? first : second).Blit(surface, x, y, frame));
    }

    public void UpdateEnemyShots(bool endLevel, IReadOnlyList<PlayerState> players, SceneEffects effects,
        Func<byte, PlayerState, byte> damage, Action<int, int, int, int> draw)
    {
        if (players.Count != 2) throw new ArgumentException("Enemy shots require two player slots.", nameof(players));
        if (endLevel) return;
        unchecked
        {
            foreach (var shot in EnemyShots)
            {
                if (shot.Available) continue;
                shot.XVelocity += shot.XAcceleration; shot.X += shot.XVelocity;
                if (shot.TrackX != 0)
                {
                    if (shot.X > players[0].X)
                    {
                        if (shot.XVelocity > -shot.TrackX) shot.XVelocity--;
                    }
                    else if (shot.XVelocity < shot.TrackX) shot.XVelocity++;
                }
                shot.YVelocity += shot.YAcceleration; shot.Y += shot.YVelocity;
                if (shot.TrackY != 0)
                {
                    if (shot.Y > players[0].Y)
                    {
                        if (shot.YVelocity > -shot.TrackY) shot.YVelocity--;
                    }
                    else if (shot.YVelocity < shot.TrackY) shot.YVelocity++;
                }
                if (shot.Duration-- == 0 || shot.Y > 190 || shot.Y <= -14 || shot.X > 275 || shot.X <= 0)
                {
                    shot.Available = true;
                    continue;
                }
                for (var i = 0; i < (TwoPlayers ? 2 : 1); i++)
                {
                    var player = players[i];
                    if (!player.IsAlive || shot.X <= player.X - (int)player.ShotHitAreaX ||
                        shot.X >= player.X + (int)player.ShotHitAreaX || shot.Y <= player.Y - (int)player.ShotHitAreaY ||
                        shot.Y >= player.Y + (int)player.ShotHitAreaY) continue;
                    shot.Available = true;
                    effects.SetupExplosion((ushort)shot.X, (ushort)shot.Y, 0, 0, false, false);
                    if (player.InvulnerableTicks == 0)
                    {
                        var amount = damage(shot.Damage, player);
                        if (amount > 0)
                        {
                            player.XVelocity += shot.XVelocity * amount / 2;
                            player.YVelocity += shot.YVelocity * amount / 2;
                        }
                    }
                    break;
                }
                if (shot.Available) continue;
                if (shot.AnimationMax != 0 && ++shot.Animation >= shot.AnimationMax) shot.Animation = 0;
                draw(shot.Graphic >= 500 ? 12 : 8, shot.X, shot.Y,
                    shot.Graphic + shot.Animation - (shot.Graphic >= 500 ? 500 : 0));
            }
        }
    }
}
