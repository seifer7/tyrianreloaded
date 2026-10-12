/*
 * OpenTyrian: A modern cross-platform port of Tyrian
 * Copyright (C) 2007-2013 The OpenTyrian Development Team
 * Distributed under the GNU General Public License, version 2 or later.
 * See COPYING for details.
 */

namespace OpenTyrian.Core;

public sealed class PlayerShot
{
    public short X { get; set; }
    public short Y { get; set; }
    public short XM { get; set; }
    public short YM { get; set; }
    public short XC { get; set; }
    public short YC { get; set; }
    public bool Complicated { get; set; }
    public short DevX { get; set; }
    public short DirX { get; set; }
    public short DevY { get; set; }
    public short DirY { get; set; }
    public short CircleSizeX { get; set; }
    public short CircleSizeY { get; set; }
    public byte Trail { get; set; }
    public ushort Graphic { get; set; }
    public ushort Animation { get; set; }
    public ushort AnimationMax { get; set; }
    public ushort Chain { get; set; }
    public byte Damage { get; set; }
    public byte BlastFilter { get; set; }
    public byte PlayerNumber { get; set; }
    public byte AimAtEnemy { get; set; }
    public byte AimDelay { get; set; }
    public byte AimDelayMax { get; set; }
    public byte IceTicks { get; set; }
    public byte Infinite { get; set; }
}

public class ShotEnemy
{
    public short X { get; set; }
    public short Y { get; set; }
    public byte Availability { get; set; } = 1;
    public bool ScoreItem { get; set; }
}

public struct PlayerShotResult
{
    public bool IsSpecial;
    public int X, Y;
    public short Damage;
    public byte BlastFilter;
    public ushort Chain;
    public byte PlayerNumber;
    public ushort RadiusWidth, RadiusHeight;
}

public interface IPlayerShotEffects
{
    void Draw(int sheet, int x, int y, int frame, bool darken);
    (ushort Width, ushort Height) DrawSpecial(int x, int y, int frame);
    void Explosion(int x, int y, byte type);
    void SuperPixels(int x, int y, ushort count, byte width, byte color);
}

public sealed class SpritePlayerShotEffects(IndexedSurface surface, SpriteSheet first, SpriteSheet second,
    SpriteTable options, Action<int, int, byte> explosion, Action<int, int, ushort, byte, byte> superPixels) : IPlayerShotEffects
{
    public void Draw(int sheet, int x, int y, int frame, bool darken)
    {
        var sprites = sheet switch { 8 => first, 12 => second, _ => throw new ArgumentOutOfRangeException(nameof(sheet)) };
        sprites.Blit(surface, x, y, frame, darken ? SpriteEffect.Darken : SpriteEffect.Copy);
    }

    public (ushort Width, ushort Height) DrawSpecial(int x, int y, int frame)
    {
        options.Blit(surface, x, y, frame, SpriteEffect.Blend);
        var sprite = options.Get(frame);
        return (sprite.Width, sprite.Height);
    }

    public void Explosion(int x, int y, byte type) => explosion(x, y, type);
    public void SuperPixels(int x, int y, ushort count, byte width, byte color) => superPixels(x, y, count, width, color);
}

public sealed class PlayerShots
{
    public const int Capacity = 81;
    private readonly IReadOnlyList<ShotItem> _shots;
    private readonly IReadOnlyList<WeaponItem> _weapons;
    private readonly IReadOnlyList<PlayerState> _players;
    private readonly IPlayerShotEffects _effects;
    public PlayerShot[] Shots { get; } = Enumerable.Range(0, Capacity + 1).Select(_ => new PlayerShot()).ToArray();
    public byte[] Availability { get; } = new byte[Capacity];
    public byte[] MultiPosition { get; } = new byte[11];
    public byte[] Repeat { get; } = new byte[11];
    public byte[] SoundQueue { get; }
    public IReadOnlyList<ShotEnemy> Enemies { get; }
    internal IReadOnlyList<PlayerState> Players => _players;
    public uint Power { get; set; }
    public bool BackgroundTwo { get; set; }
    public uint ShadowDistance { get; set; } = 10;

    public PlayerShots(IReadOnlyList<ShotItem> shots, IReadOnlyList<WeaponItem> weapons,
        IReadOnlyList<PlayerState> players, IPlayerShotEffects effects, IReadOnlyList<ShotEnemy>? enemies = null,
        byte[]? soundQueue = null)
    {
        if (players.Count != 2) throw new ArgumentException("Player shots require the native two-player state.", nameof(players));
        _shots = shots;
        _weapons = weapons;
        _players = players;
        _effects = effects;
        if (enemies is not null && enemies.Count != 100)
            throw new ArgumentException("Projectile targeting requires 100 native enemy slots.", nameof(enemies));
        Enemies = enemies ?? Enumerable.Range(0, 100).Select(_ => new ShotEnemy()).ToArray();
        if (soundQueue is not null && soundQueue.Length != 8)
            throw new ArgumentException("Player shots require eight sound channels.", nameof(soundQueue));
        SoundQueue = soundQueue ?? new byte[8];
    }

    public short Create(ushort port, uint bay, ushort x, ushort y, ushort mouseX, ushort mouseY,
        ushort weaponIndex, byte playerNumber)
    {
        if (port >= _weapons.Count || weaponIndex == 0 || weaponIndex > _shots.Count) return Capacity;
        if (weaponIndex == _shots.Count) throw new InvalidDataException("Native projectile index equals the item count and would access past the array.");
        if (bay >= MultiPosition.Length) throw new ArgumentOutOfRangeException(nameof(bay));
        if (playerNumber is < 1 or > 2) throw new ArgumentOutOfRangeException(nameof(playerNumber));
        var weapon = _shots[weaponIndex];
        if (Power < _weapons[port].PowerUse) return Capacity;
        Power -= _weapons[port].PowerUse;
        if (weapon.Sound > 0) SoundQueue[SoundChannels[bay]] = weapon.Sound;
        short id = Capacity;
        unchecked
        {
            for (var multi = 1; multi <= weapon.Multi; multi++)
            {
                for (id = 0; id < Capacity && Availability[id] != 0; id++) { }
                if (id == Capacity) return Capacity;
                MultiPosition[bay] = MultiPosition[bay] == weapon.Max || MultiPosition[bay] > 8
                    ? (byte)1 : (byte)(MultiPosition[bay] + 1);
                var slot = MultiPosition[bay] - 1;
                if (slot >= 8) throw new InvalidDataException("Projectile multi-position exceeds the native eight-entry weapon arrays.");
                var shot = Shots[id];
                shot.Chain = weapon.Chain[slot] > 0 ? (ushort)weapon.Chain[slot] : (ushort)0;
                shot.PlayerNumber = playerNumber;
                shot.Animation = 0;
                shot.Complicated = weapon.CircleSize != 0;
                shot.DevX = shot.DevY = shot.DirX = shot.DirY = shot.CircleSizeX = shot.CircleSizeY = 0;
                if (shot.Complicated)
                {
                    var size = weapon.CircleSize;
                    shot.CircleSizeX = size > 19 ? (short)(size % 20) : size;
                    shot.CircleSizeY = size > 19 ? (short)(size / 20) : size;
                    shot.DevX = (short)(shot.CircleSizeX >> 1);
                    shot.DevY = (short)(shot.CircleSizeY >> 1);
                    shot.DirX = 1;
                    shot.DirY = -1;
                }

                shot.Trail = weapon.Trail;
                shot.Damage = weapon.Attack[slot];
                shot.IceTicks = weapon.IceTicks[slot];
                shot.Infinite = weapon.Infinite[slot];
                shot.BlastFilter = weapon.ShipBlastFilter;
                shot.X = (short)(x + weapon.Bx[slot]);
                shot.Y = (short)(y + weapon.By[slot]);
                shot.YC = (short)-weapon.Acceleration;
                shot.XC = weapon.AccelerationX;
                shot.XM = weapon.Sx[slot];
                var duration = weapon.Del[slot];
                if (duration == 121) { shot.Trail = 0; duration = 255; }
                shot.Graphic = weapon.Sg[slot];
                Availability[id] = shot.Graphic == 0 ? (byte)0 : duration;
                shot.AnimationMax = duration is > 100 and < 120 ? (ushort)(duration - 99) : (ushort)(weapon.WeapAni + 1);
                if (duration is 99 or 98)
                    shot.XM = (short)(shot.XM + Math.Clamp((int)(short)(x - mouseX), -5, 5));
                if (duration is 99 or 100)
                    shot.YM = (short)Math.Clamp((int)(short)(y - mouseY - weapon.Sy[slot]), -4, 4);
                else if (weapon.Sy[slot] == 98)
                {
                    shot.YM = 0;
                    shot.YC = -1;
                }
                else if (weapon.Sy[slot] > 100)
                {
                    shot.YM = weapon.Sy[slot];
                    shot.Y = (short)(shot.Y - _players[playerNumber - 1].DeltaYShotMove);
                }
                else shot.YM = (short)-weapon.Sy[slot];
                if (weapon.Sx[slot] > 100)
                {
                    shot.XM = weapon.Sx[slot];
                    shot.X = (short)(shot.X - _players[playerNumber - 1].DeltaXShotMove);
                    if (shot.XM == 101) shot.Y = (short)(shot.Y - _players[playerNumber - 1].DeltaYShotMove);
                }

                shot.AimAtEnemy = 0;
                if (weapon.Aim > 5)
                {
                    uint best = 65000;
                    for (var enemyIndex = 0; enemyIndex < Enemies.Count; enemyIndex++)
                    {
                        var enemy = Enemies[enemyIndex];
                        if (enemy.Availability == 1 || enemy.ScoreItem) continue;
                        // Native scratch distance is a 16-bit word, even though the sum is an int.
                        var distance = (ushort)(Math.Abs(enemy.X - shot.X) + Math.Abs(enemy.Y - shot.Y));
                        if (distance >= best) continue;
                        best = distance;
                        shot.AimAtEnemy = (byte)(enemyIndex + 1);
                    }
                    shot.AimDelay = 5;
                    shot.AimDelayMax = (byte)(weapon.Aim - 5);
                }
                Repeat[bay] = weapon.ShotRepeat;
            }
        }
        return id;
    }

    public void SetDirection(int id, int weaponIndex, float direction)
    {
        var shot = Shots[id];
        shot.XM = RoundedShort(-MathF.Round(MathF.Sin(direction) * shot.YM, MidpointRounding.AwayFromZero));
        shot.YM = RoundedShort(-MathF.Round(MathF.Cos(direction) * shot.YM, MidpointRounding.AwayFromZero));
        var weapon = _weapons[weaponIndex].Id;
        ushort[]? graphics = weapon switch
        {
            "27" or "32" or "10" => MultiGraphics,
            "28" or "33" or "11" => SonicGraphics,
            "22" => MultiTwoGraphics,
            _ => null
        };
        if (graphics is not null)
        {
            var rounded = (int)MathF.Round((float)(direction * (16 / (2 * Math.PI))), MidpointRounding.AwayFromZero);
            if ((uint)rounded >= graphics.Length) throw new ArgumentOutOfRangeException(nameof(direction), "Native directional sprite index is outside 0..16.");
            shot.Graphic = graphics[rounded];
        }
        else if (weapon is "30" or "35" or "14" && direction > Math.PI / 2 && direction < 3 * Math.PI / 2)
            shot.YC = 1;
    }

    public void Simulate()
    {
        unchecked
        {
            for (var id = 0; id < Capacity; id++)
            {
                if (Availability[id] == 0) continue;
                Availability[id]--;
                if (id == Capacity - 1) continue;
                var shot = Shots[id];
                shot.XM = (short)(shot.XM + shot.XC);
                if (shot.XM <= 100) shot.X = (short)(shot.X + shot.XM);
                shot.YM = (short)(shot.YM + shot.YC);
                shot.Y = (short)(shot.Y + shot.YM);
                if (shot.YM > 100) shot.Y = (short)(shot.Y - 120 + _players[0].DeltaYShotMove);
                MoveCircle(shot);
                if (shot.X is < 0 or > 140 || shot.Y is < 0 or > 170) { Availability[id] = 0; continue; }
                var frame = AdvanceAnimation(shot);
                if (frame >= 60000) continue;
                if (frame > 1000) frame %= 1000;
                _effects.Draw(frame > 500 ? 12 : 8, shot.X + 1, shot.Y, frame > 500 ? frame - 500 : frame, false);
            }
        }
    }

    public bool MoveAndDraw(int id, ref PlayerShotResult result)
    {
        var shot = Shots[id];
        unchecked
        {
            Availability[id]--;
            if (id == Capacity - 1) return true;
            if (shot.PlayerNumber is < 1 or > 2) throw new InvalidDataException("Projectile player number is outside 1..2.");
            var player = _players[shot.PlayerNumber - 1];
            shot.XM = (short)(shot.XM + shot.XC);
            shot.X = (short)(shot.X + shot.XM);
            var priorXM = shot.XM;
            if (shot.XM > 100)
            {
                shot.X = (short)(shot.X - (shot.XM == 101 ? 101 : 120) + player.DeltaXShotMove);
                if (shot.XM == 101) shot.Y = (short)(shot.Y + player.DeltaYShotMove);
            }
            shot.YM = (short)(shot.YM + shot.YC);
            shot.Y = (short)(shot.Y + shot.YM);
            if (shot.YM > 100) shot.Y = (short)(shot.Y - 120 + player.DeltaYShotMove);
            MoveCircle(shot);
            result.X = shot.X;
            result.Y = shot.Y;
            if (shot.X is < -34 or > 290 || shot.Y is < -15 or > 190) { Availability[id] = 0; return false; }
            if (shot.Trail != 255)
                _effects.Explosion(shot.X - (shot.Trail == 98 ? shot.XM : 0), shot.Y - (shot.Trail == 98 ? shot.YM : 0), shot.Trail);
            if (shot.AimAtEnemy != 0 && --shot.AimDelay == 0)
            {
                shot.AimDelay = shot.AimDelayMax;
                var enemy = Enemies[shot.AimAtEnemy - 1];
                shot.XM = (short)(shot.XM + (enemy.Availability != 1 ? shot.X < enemy.X ? 1 : -1 : shot.XM > 0 ? 1 : -1));
                if (enemy.Availability != 1) shot.YM = (short)(shot.YM + (shot.Y < enemy.Y ? 1 : -1));
            }
            var frame = AdvanceAnimation(shot);
            result.Damage = shot.Damage;
            result.BlastFilter = shot.BlastFilter;
            result.Chain = shot.Chain;
            result.PlayerNumber = shot.PlayerNumber;
            result.IsSpecial = frame > 60000;
            if (result.IsSpecial)
            {
                var dimensions = _effects.DrawSpecial(shot.X + 1, shot.Y, frame - 60001);
                result.RadiusWidth = (ushort)(dimensions.Width / 2);
                result.RadiusHeight = (ushort)(dimensions.Height / 2);
            }
            else
            {
                if (frame > 1000)
                {
                    _effects.SuperPixels((ushort)(shot.X + 7), (ushort)(shot.Y + 6), 5, 3, (byte)((frame / 1000) << 4));
                    frame %= 1000;
                }
                var sheet = frame > 500 ? 12 : 8;
                var index = frame > 500 ? frame - 500 : frame;
                // Native unsigned shadow distance promotes the addition to uint.
                var shadowY = (uint)shot.Y + ShadowDistance;
                if (BackgroundTwo && shadowY < 190 && priorXM < 100)
                    _effects.Draw(sheet, shot.X + 1, (int)shadowY, index, true);
                _effects.Draw(sheet, shot.X + 1, shot.Y, index, false);
            }
        }
        return true;
    }

    private static void MoveCircle(PlayerShot shot)
    {
        if (!shot.Complicated) return;
        unchecked
        {
            shot.DevX = (short)(shot.DevX + shot.DirX);
            shot.X = (short)(shot.X + shot.DevX);
            if (Math.Abs((int)shot.DevX) == shot.CircleSizeX) shot.DirX = (short)-shot.DirX;
            shot.DevY = (short)(shot.DevY + shot.DirY);
            shot.Y = (short)(shot.Y + shot.DevY);
            if (Math.Abs((int)shot.DevY) == shot.CircleSizeY) shot.DirY = (short)-shot.DirY;
        }
    }

    private static ushort AdvanceAnimation(PlayerShot shot)
    {
        var frame = unchecked((ushort)(shot.Graphic + shot.Animation));
        shot.Animation = unchecked((ushort)(shot.Animation + 1));
        if (shot.Animation == shot.AnimationMax) shot.Animation = 0;
        return frame;
    }

    private static short RoundedShort(float value)
    {
        if (!float.IsFinite(value) || value is < short.MinValue or > short.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(value), "Native floating conversion would be undefined.");
        return (short)value;
    }

    private static readonly byte[] SoundChannels = [0, 2, 4, 4, 2, 2, 5, 5, 1, 4, 1];
    private static readonly ushort[] MultiGraphics = [77, 221, 183, 301, 1, 282, 164, 202, 58, 201, 163, 281, 39, 300, 182, 220, 77];
    private static readonly ushort[] SonicGraphics = [85, 242, 131, 303, 47, 284, 150, 223, 66, 224, 149, 283, 9, 302, 130, 243, 85];
    private static readonly ushort[] MultiTwoGraphics = [78, 299, 295, 297, 2, 278, 276, 280, 59, 279, 275, 277, 40, 296, 294, 298, 78];
}
