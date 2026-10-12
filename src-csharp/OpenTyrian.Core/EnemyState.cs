/*
 * OpenTyrian: A modern cross-platform port of Tyrian
 * Copyright (C) 2007-2013 The OpenTyrian Development Team
 * Distributed under the GNU General Public License, version 2 or later.
 * See COPYING for details.
 */

namespace OpenTyrian.Core;

public sealed class EnemyState : ShotEnemy
{
    public sbyte XSpeed { get; set; }
    public sbyte YSpeed { get; set; }
    public sbyte XRandomAcceleration { get; set; }
    public sbyte YRandomAcceleration { get; set; }
    public sbyte XFixedAcceleration { get; set; }
    public sbyte YFixedAcceleration { get; set; }
    public sbyte XAccelerationWait { get; set; }
    public sbyte YAccelerationWait { get; set; }
    public byte Armor { get; set; }
    public byte[] ShotWait { get; } = new byte[3];
    public byte[] ShotMultiPosition { get; } = new byte[3];
    public byte Cycle { get; set; }
    public byte AnimationLength { get; set; }
    public ushort[] Graphics { get; } = new ushort[20];
    public byte Size { get; set; }
    public byte LinkNumber { get; set; }
    public byte AnimationActive { get; set; }
    public byte AnimationMax { get; set; }
    public byte AnimationWhenFire { get; set; }
    public SpriteSheet? Sprites { get; set; }
    public sbyte XReverse { get; set; }
    public sbyte YReverse { get; set; }
    public short XAccelerationAdd { get; set; }
    public short YAccelerationAdd { get; set; }
    public byte XAccelerationWaitMax { get; set; }
    public byte YAccelerationWaitMax { get; set; }
    public EnemyItem? Definition { get; set; }
    public bool Damaged { get; set; }
    public ushort Type { get; set; }
    public byte AnimationMin { get; set; }
    public ushort DamageGraphic { get; set; }
    public sbyte DamageLevel { get; set; }
    public sbyte DamageAnimation { get; set; }
    public byte Filter { get; set; }
    public short Value { get; set; }
    public short FixedMoveY { get; set; }
    public byte[] Frequency { get; } = new byte[3];
    public byte LaunchWait { get; set; }
    public ushort LaunchType { get; set; }
    public ushort LaunchTypePosition { get; set; }
    public byte LaunchFrequency { get; set; }
    public byte XAcceleration { get; set; }
    public byte YAcceleration { get; set; }
    public byte[] Turrets { get; } = new byte[3];
    public ushort EnemyDie { get; set; }
    public bool Ground { get; set; }
    public byte ExplosionNumber { get; set; }
    public ushort MapOffset { get; set; }
    public bool Special { get; set; }
    public byte FlagNumber { get; set; }
    public bool SetTo { get; set; }
    public byte Iced { get; set; }
    public byte LaunchSpecial { get; set; }
    public short XMinBounce { get; set; }
    public short XMaxBounce { get; set; }
    public short YMinBounce { get; set; }
    public short YMaxBounce { get; set; }
}

public sealed class BossBar
{
    public byte LinkNumber { get; set; }
    public byte Armor { get; set; }
    public byte Color { get; set; }
}
