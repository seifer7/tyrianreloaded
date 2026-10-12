/*
 * OpenTyrian: A modern cross-platform port of Tyrian
 * Copyright (C) 2007-2013 The OpenTyrian Development Team
 * Distributed under the GNU General Public License, version 2 or later.
 * See COPYING for details.
 */

namespace OpenTyrian.Core;

public sealed partial class EnemySystem
{
    public ushort MapXOffset { get; set; }
    public ushort BackgroundMove { get; set; }

    public void UpdateAcceleration(int index, int targetX, int targetY)
    {
        var enemy = Enemies[index];
        unchecked
        {
            if (enemy.XAcceleration != 0 && (uint)(enemy.XAcceleration - 89) > MersenneTwister.NextUInt32() % 11)
            {
                if (targetX > enemy.X)
                {
                    if (enemy.XSpeed < enemy.XAcceleration - 89) enemy.XSpeed++;
                }
                else if (enemy.XSpeed >= 0 || -enemy.XSpeed < enemy.XAcceleration - 89) enemy.XSpeed--;
            }
            if (enemy.YAcceleration != 0 && (uint)(enemy.YAcceleration - 89) > MersenneTwister.NextUInt32() % 11)
            {
                if (targetY > enemy.Y)
                {
                    if (enemy.YSpeed < enemy.YAcceleration - 89) enemy.YSpeed++;
                }
                else if (enemy.YSpeed >= 0 || -enemy.YSpeed < enemy.YAcceleration - 89) enemy.YSpeed--;
            }
        }
    }

    public bool AnimateAndDraw(int index, IndexedSurface surface)
        => AnimateAndDraw(index, (sprites, x, y, frame, filter) => sprites.Blit(surface, x, y, checked((int)frame),
            filter == 0 ? SpriteEffect.Copy : SpriteEffect.Filter, filter));

    public bool AnimateAndDraw(int index, Action<SpriteSheet, int, int, uint, byte> draw)
    {
        var enemy = Enemies[index];
        if (enemy.X + MapXOffset is <= -29 or >= 300) return true;
        if (enemy.AnimationActive == 1)
        {
            enemy.Cycle = unchecked((byte)(enemy.Cycle + 1));
            if (enemy.Cycle == enemy.AnimationMax) enemy.AnimationActive = enemy.AnimationWhenFire;
            else if (enemy.Cycle > enemy.AnimationLength) enemy.Cycle = enemy.AnimationMin;
        }
        if (enemy.Cycle is < 1 or > 20) throw new InvalidDataException("Enemy cycle is outside the native twenty-frame graphics array.");
        if (enemy.Graphics[enemy.Cycle - 1] == 999) return false;
        if (enemy.Size == 1)
        {
            if (enemy.Y > -13)
            {
                BlitEnemy(index, -6, -7, 0, draw);
                BlitEnemy(index, 6, -7, 1, draw);
            }
            if (enemy.Y > -26 && enemy.Y < 182)
            {
                BlitEnemy(index, -6, 7, 19, draw);
                BlitEnemy(index, 6, 7, 20, draw);
            }
        }
        else if (enemy.Y > -13) BlitEnemy(index, 0, 0, 0, draw);
        enemy.Filter = 0;
        return true;
    }

    private void BlitEnemy(int index, int xOffset, int yOffset, int spriteOffset, Action<SpriteSheet, int, int, uint, byte> draw)
    {
        var enemy = Enemies[index];
        if (enemy.Sprites is null)
        {
            warnings.Write($"warning: enemy {index} sprite missing\n");
            return;
        }
        draw(enemy.Sprites, enemy.X + xOffset + MapXOffset, enemy.Y + yOffset,
            unchecked((uint)(enemy.Graphics[enemy.Cycle - 1] + spriteOffset)), enemy.Filter);
    }

    public bool UpdatePosition(int index)
    {
        var enemy = Enemies[index];
        unchecked
        {
            (enemy.XFixedAcceleration, enemy.XAccelerationWait, enemy.XSpeed, enemy.XReverse, enemy.XAccelerationAdd) =
                Oscillate(enemy.XFixedAcceleration, enemy.XAccelerationWait, enemy.XSpeed, enemy.XReverse,
                    enemy.XAccelerationAdd, (sbyte)enemy.XAccelerationWaitMax);
            (enemy.YFixedAcceleration, enemy.YAccelerationWait, enemy.YSpeed, enemy.YReverse, enemy.YAccelerationAdd) =
                Oscillate(enemy.YFixedAcceleration, enemy.YAccelerationWait, enemy.YSpeed, enemy.YReverse,
                    enemy.YAccelerationAdd, (sbyte)enemy.YAccelerationWaitMax);
            enemy.Y = (short)(enemy.Y + enemy.FixedMoveY);
            enemy.X = (short)(enemy.X + enemy.XSpeed);
            if (enemy.X is < -80 or > 340)
            {
                enemy.Availability = 1;
                return false;
            }
            enemy.Y = (short)(enemy.Y + enemy.YSpeed);
            if (enemy.Y is < -112 or > 190)
            {
                enemy.Availability = 1;
                return false;
            }
            if (enemy.X <= enemy.XMinBounce || enemy.X >= enemy.XMaxBounce) enemy.XSpeed = (sbyte)-enemy.XSpeed;
            if (enemy.Y <= enemy.YMinBounce || enemy.Y >= enemy.YMaxBounce) enemy.YSpeed = (sbyte)-enemy.YSpeed;
            if (enemy.ScoreItem)
            {
                if (enemy.X < -5) enemy.X++;
                if (enemy.X > 245) enemy.X--;
            }
            enemy.Y = (short)(enemy.Y + BackgroundMove);
            return enemy.X is > -24 and < 296;
        }
    }

    private static (sbyte Change, sbyte Wait, sbyte Velocity, sbyte Reverse, short Increment) Oscillate(
        sbyte change, sbyte wait, sbyte velocity, sbyte reverse, short increment, sbyte waitMax)
    {
        unchecked
        {
            if (change != 0 && --wait <= 0)
            {
                if (velocity == reverse)
                {
                    change = (sbyte)-change; reverse = (sbyte)-reverse; increment = (short)-increment;
                }
                else
                {
                    velocity = (sbyte)(velocity + increment);
                    wait = waitMax;
                    if (velocity == reverse)
                    {
                        change = (sbyte)-change; reverse = (sbyte)-reverse; increment = (short)-increment;
                    }
                }
            }
        }
        return (change, wait, velocity, reverse, increment);
    }

    public (short X, short Y) AimVelocity(int targetX, int targetY, int enemyX, int enemyY, int speed)
    {
        var relativeX = unchecked(targetX + 25 - enemyX - MapXOffset - 4);
        var relativeY = unchecked(targetY - enemyY);
        if (relativeX == 0) relativeX = 1;
        if (relativeY == 0) relativeY = 1;
        var longest = Math.Max(Math.Abs(relativeX), Math.Abs(relativeY));
        return (RoundVelocity((float)relativeX / longest * speed), RoundVelocity((float)relativeY / longest * speed));
    }

    private static short RoundVelocity(float value)
    {
        var rounded = MathF.Round(value, MidpointRounding.AwayFromZero);
        if (!float.IsFinite(rounded) || rounded is < short.MinValue or > short.MaxValue)
            throw new InvalidDataException("Enemy aim velocity cannot be represented by the native signed word.");
        return (short)rounded;
    }
}
