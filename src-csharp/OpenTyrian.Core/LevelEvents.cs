/*
 * OpenTyrian: A modern cross-platform port of Tyrian
 * Copyright (C) 2007-2013 The OpenTyrian Development Team
 * Distributed under the GNU General Public License, version 2 or later.
 * See COPYING for details.
 */

namespace OpenTyrian.Core;

public sealed class LevelEvent
{
    public ushort Time { get; set; }
    public byte Type { get; set; }
    public short Data { get; set; }
    public short Data2 { get; set; }
    public sbyte Data3 { get; set; }
    public byte Data4 { get; set; }
    public sbyte Data5 { get; set; }
    public sbyte Data6 { get; set; }
}

public sealed partial class LevelEvents(ItemDatabase items, EnemySystem enemies, IReadOnlyList<PlayerState> players,
    BackgroundRenderer backgrounds, SceneEffects effects)
{
    public LevelEvent[] Events { get; set; } = [];
    public ushort Location { get; set; }
    public ushort ReturnLocation { get; set; }
    public ushort EventLocation { get; set; } = 1;
    public byte[] NewLinks { get; } = new byte[10];
    public ushort MapX { get; set; }
    public ushort MapX3 { get; set; }
    public bool ThirdAtFirstX { get; set; }
    public bool ThirdAtFirstXB { get; set; }
    public bool SmallEnemyAdjust { get; set; }
    public byte ThirdOver { get; set; }
    public bool TopEnemyOver { get; set; }
    public bool QuitRequested { get; set; }
    public byte FirstDelay { get; set; }
    public byte FirstDelayMax { get; set; }
    public bool OnePlayerAction { get; set; }
    public bool SuperTyrian { get; set; }
    public LevelEvent Current => EventLocation > 0 && EventLocation <= Events.Length
        ? Events[EventLocation - 1] : throw new InvalidDataException("Level event cursor is outside the event array.");

    public void Jump(ushort target)
    {
        var location = target == ushort.MaxValue ? ReturnLocation : target;
        var index = Array.FindIndex(Events, record => record.Time >= location);
        if (index < 0) throw new InvalidDataException("Level jump has no event at or after its target.");
        if (target != ushort.MaxValue) ReturnLocation = unchecked((ushort)(Location + 1));
        Location = location;
        EventLocation = checked((ushort)index);
    }

    public bool SearchEnemy(byte link, out byte index)
    {
        var found = -1;
        for (var i = 0; i < enemies.Enemies.Length; i++)
        {
            var enemy = enemies.Enemies[i];
            if (enemy.Availability != 0 || enemy.LinkNumber != link) continue;
            found = i;
            if (enemies.GalagaMode) enemy.Value = unchecked((short)(enemy.Value + enemy.Value));
        }
        index = found >= 0 ? (byte)found : (byte)0;
        return found >= 0;
    }

    public short CreateEnemy(byte typeOffset, ushort offset, short uniqueSprites = 0)
    {
        if (offset > 75 || offset % 25 != 0) throw new ArgumentOutOfRangeException(nameof(offset));
        if (!enemies.Enemies.Skip(offset).Take(25).Any(enemy => enemy.Availability == 1)) return 0;
        var record = Current;
        if (!items.TryFind(ItemType.Enemy, record.Data + typeOffset, out var definition))
            throw new InvalidDataException($"Event enemy ID '{record.Data + typeOffset}' is missing.");
        var created = enemies.Create(offset, definition, uniqueSprites);
        var enemy = enemies.Enemies[created - 1];
        unchecked
        {
            if (record.Data2 != -99)
            {
                enemy.X = (short)(offset switch
                {
                    0 => record.Data2 - (MapX - 1) * 24,
                    25 or 75 => record.Data2 - (MapX - 1) * 24 - 12,
                    50 => ThirdAtFirstX ? record.Data2 - (MapX - 1) * 24 - 12 : record.Data2 - MapX3 * 24 - 48 + 6,
                    _ => throw new InvalidOperationException()
                });
                if (ThirdAtFirstXB && offset == 50) enemy.X -= 6;
                // Native scroll subtraction is overwritten by this spawn height.
                enemy.Y = (short)(ThirdAtFirstXB && offset == 50 ? -24 : -28);
            }
            if (SmallEnemyAdjust && enemy.Size == 0) { enemy.X -= 10; enemy.Y -= 7; }
            enemy.Y += record.Data5;
            enemy.YSpeed = (sbyte)(enemy.YSpeed + record.Data3);
            enemy.LinkNumber = record.Data4;
            enemy.FixedMoveY = record.Data6;
        }
        return created;
    }

    public short CreateEnemyAtY(byte typeOffset, ushort offset, int y, bool addData5)
    {
        var created = CreateEnemy(typeOffset, offset);
        if (created > 0) enemies.Enemies[created - 1].Y = unchecked((short)(y + (addData5 ? Current.Data5 : 0)));
        return created;
    }

    public void ResolveEnemyLink()
    {
        if (Current.Data3 is > 79 and < 90) Current.Data4 = NewLinks[Current.Data3 - 80];
    }

    public void ApplyRandomSpecialDrop()
    {
        var lives = players[0].Lives;
        if (Current.Data == 533 && (lives == 11 || MersenneTwister.NextUInt32() % 15 < lives))
            Current.Data = (short)(829 + MersenneTwister.NextUInt32() % 6);
    }

    public void SetEnemyDeath()
    {
        if (!items.TryFind(ItemType.Enemy, Current.Data, out var index))
            throw new InvalidDataException($"Event death-drop enemy ID '{Current.Data}' is missing.");
        foreach (var enemy in enemies.Enemies)
            if (enemy.LinkNumber == Current.Data4) enemy.EnemyDie = unchecked((ushort)index);
    }

    public void GlobalMove()
    {
        var record = Current;
        var first = 0; var last = 0; var all = false;
        if (record.Data3 is > 79 and < 90) { last = 100; ResolveEnemyLink(); }
        else
        {
            switch (record.Data3)
            {
                case 0: last = 100; break;
                case 2: last = 25; all = true; break;
                case 1: first = 25; last = 50; all = true; break;
                case 3: first = 50; last = 75; all = true; break;
                case 99: last = 100; all = true; break;
            }
        }
        for (var i = first; i < last; i++)
        {
            var enemy = enemies.Enemies[i];
            if (!all && enemy.LinkNumber != record.Data4) continue;
            if (record.Data != -99) enemy.XSpeed = unchecked((sbyte)record.Data);
            if (record.Data2 != -99) enemy.YSpeed = unchecked((sbyte)record.Data2);
            if (record.Data6 != 0) enemy.FixedMoveY = record.Data6;
            if (record.Data6 == -99) enemy.FixedMoveY = 0;
            if (record.Data5 > 0) enemy.Cycle = (byte)record.Data5;
        }
    }

    public void GlobalAcceleration()
    {
        ResolveEnemyLink();
        var record = Current;
        foreach (var enemy in enemies.Enemies)
        {
            if (enemy.Availability == 1 || enemy.LinkNumber != record.Data4 && record.Data4 != 0) continue;
            unchecked
            {
                if (record.Data != -99)
                {
                    enemy.XFixedAcceleration = (sbyte)record.Data;
                    enemy.XAccelerationWait = (sbyte)Math.Abs((int)record.Data);
                    enemy.XAccelerationWaitMax = (byte)Math.Abs((int)record.Data);
                    enemy.XAccelerationAdd = (short)(record.Data > 0 ? 1 : -1);
                }
                if (record.Data2 != -99)
                {
                    enemy.YFixedAcceleration = (sbyte)record.Data2;
                    enemy.YAccelerationWait = (sbyte)Math.Abs((int)record.Data2);
                    enemy.YAccelerationWaitMax = (byte)Math.Abs((int)record.Data2);
                    enemy.YAccelerationAdd = (short)(record.Data2 > 0 ? 1 : -1);
                }
                if (record.Data5 > 0) enemy.Cycle = (byte)record.Data5;
                if (record.Data6 > 0)
                {
                    enemy.AnimationLength = (byte)record.Data6; enemy.AnimationMin = (byte)record.Data5;
                    enemy.AnimationMax = 0; enemy.AnimationActive = 1;
                }
            }
        }
    }

    public void GlobalAnimation()
    {
        var record = Current;
        foreach (var enemy in enemies.Enemies)
        {
            if (enemy.LinkNumber != record.Data4) continue;
            enemy.AnimationActive = 1; enemy.AnimationWhenFire = 0;
            if (record.Data2 > 0)
            {
                enemy.Cycle = unchecked((byte)record.Data2);
                enemy.AnimationMin = enemy.Cycle;
            }
            else enemy.Cycle = 0;
            if (record.Data > 0) enemy.AnimationLength = unchecked((byte)record.Data);
            if (record.Data3 == 1) enemy.AnimationMax = enemy.AnimationLength;
            else if (record.Data3 == 2)
            {
                enemy.AnimationActive = 2; enemy.AnimationMax = enemy.AnimationLength; enemy.AnimationWhenFire = 2;
            }
        }
    }

    public void GlobalDamage()
    {
        var value = enemies.GalagaMode ? Current.Data * (enemies.Difficulty / 2) : Current.Data;
        if (enemies.GalagaMode && value is < 0 or > 255)
            throw new InvalidDataException("Galaga enemy armor is outside the native float-to-byte range.");
        foreach (var enemy in enemies.Enemies)
        {
            if (Current.Data4 == 0 || enemy.LinkNumber == Current.Data4)
                // Native integer division occurs before the roundf conversion.
                enemy.Armor = unchecked((byte)value);
        }
    }

    public void ReverseAcceleration()
    {
        ResolveEnemyLink();
        var record = Current;
        foreach (var enemy in enemies.Enemies)
        {
            if (record.Data4 != 0 && enemy.LinkNumber != record.Data4) continue;
            if (record.Data != -99) enemy.XReverse = unchecked((sbyte)record.Data);
            if (record.Data2 != -99) enemy.YReverse = unchecked((sbyte)record.Data2);
            if (record.Data3 != 0 && record.Data3 < 17) enemy.Filter = unchecked((byte)record.Data3);
        }
    }

    public void OverrideFire()
    {
        var record = Current;
        foreach (var enemy in enemies.Enemies)
        {
            if (record.Data4 != 99 && enemy.LinkNumber != record.Data4) continue;
            enemy.Frequency[0] = unchecked((byte)record.Data); enemy.Frequency[1] = unchecked((byte)record.Data2);
            enemy.Frequency[2] = unchecked((byte)record.Data3);
            Array.Fill(enemy.ShotWait, (byte)1);
            if (enemy.LaunchType > 0) { enemy.LaunchFrequency = unchecked((byte)record.Data5); enemy.LaunchWait = 1; }
        }
    }

    public void HandleDeath()
    {
        var record = Current;
        if (record.Data is 512 or 513 && (enemies.TwoPlayers || OnePlayerAction || SuperTyrian)) return;
        if (enemies.SuperArcadeMode != 0)
        {
            if (record.Data == 534) record.Data = 827;
        }
        else if (!SuperTyrian) ApplyRandomSpecialDrop();
        if (record.Data == 534 && SuperTyrian)
            record.Data = (short)(828 + LegacyGameTables.SuperTyrianSpecialDrop(
                (int)(MersenneTwister.NextUInt32() % LegacyGameTables.SuperTyrianSpecialDropCount)));
        SetEnemyDeath();
    }

    public void HandleEnemyEvent()
    {
        switch (Current.Type)
        {
            case 19: GlobalMove(); break;
            case 20: GlobalAcceleration(); break;
            case 21: ThirdOver = 1; break;
            case 22: ThirdOver = 0; break;
            case 23: CreateEnemyAtY(0, 50, 180, true); break;
            case 24: GlobalAnimation(); break;
            case 25: GlobalDamage(); break;
            case 26: SmallEnemyAdjust = Current.Data != 0; break;
            case 27: ReverseAcceleration(); break;
            case 28: TopEnemyOver = false; break;
            case 29: TopEnemyOver = true; break;
            case 30:
                FirstDelay = FirstDelayMax = backgrounds.SecondDelay = backgrounds.SecondDelayMax = 1;
                backgrounds.First.Move = unchecked((ushort)Current.Data);
                backgrounds.Second.Move = unchecked((ushort)Current.Data2);
                effects.ExplosionMove = backgrounds.Second.Move;
                backgrounds.Third.Move = unchecked((ushort)Current.Data3);
                enemies.FirstBackgroundMove = backgrounds.First.Move;
                effects.BackgroundMove = backgrounds.Second.Move;
                break;
            case 31: OverrideFire(); break;
            case 32: CreateEnemyAtY(0, 50, 190, false); break;
            case 33: HandleDeath(); break;
            default: throw new InvalidDataException($"Event {Current.Type} is outside the enemy-event group.");
        }
    }
}
