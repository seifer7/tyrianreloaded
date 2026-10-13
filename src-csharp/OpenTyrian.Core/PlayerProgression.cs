/*
 * OpenTyrian: A modern cross-platform port of Tyrian
 * Copyright (C) 2007-2013 The OpenTyrian Development Team
 * Distributed under the GNU General Public License, version 2 or later.
 * See COPYING for details.
 */

namespace OpenTyrian.Core;

public sealed class PlayerProgression(LegacyGameState game, ItemDatabase items)
{
    public ushort UpgradeCost { get; private set; }
    public ushort DowngradeCost { get; private set; }
    private static bool NarrowLong => OperatingSystem.IsWindows() || IntPtr.Size == 4;
    private static long NativeLong(long value) => NarrowLong ? unchecked((int)value) : value;
    private static ulong NativeUnsignedLong(ulong value) => NarrowLong ? unchecked((uint)value) : value;

    public static long WeaponUpgradeCost(long baseCost, uint power)
    {
        if (power > 11) throw new ArgumentOutOfRangeException(nameof(power));
        return NativeLong(unchecked(NativeLong(baseCost) * (power * (power + 1) / 2)));
    }

    public ulong GetCost(byte type, ushort index)
    {
        var cost = type == 2 && index > 90 ? 100 : BaseCost(type, index);
        if (type is 3 or 4)
        {
            var power = game.Players[0].Items.Weapons[type - 3].Power;
            if (power is < 1 or > 11) throw new InvalidDataException("Shop weapon power must be between 1 and 11.");
            DowngradeCost = unchecked((ushort)WeaponUpgradeCost(cost, (uint)(power - 1)));
            UpgradeCost = unchecked((ushort)WeaponUpgradeCost(cost, power));
        }
        return NativeUnsignedLong(unchecked((ulong)(long)cost));
    }

    public int GetValue(byte type, ushort index)
    {
        long value = BaseCost(type, index);
        if (type is 3 or 4)
        {
            var port = type - 3;
            var power = game.Players[game.TwoPlayerMode ? port : 0].Items.Weapons[port].Power;
            if (power is < 1 or > 12) throw new InvalidDataException("Scored weapon power must be between 1 and 12.");
            for (var i = 1u; i < power; i++) value = NativeLong(unchecked(value + WeaponUpgradeCost(BaseCost(type, index), i)));
        }
        return unchecked((int)value);
    }

    public ulong TotalScore(PlayerState player)
    {
        var score = player.Cash;
        var ids = new ushort[] { player.Items.Ship, player.Items.Weapons[0].Id, player.Items.Weapons[1].Id,
            player.Items.Shield, player.Items.Generator, player.Items.Sidekicks[0], player.Items.Sidekicks[1] };
        for (var i = 0; i < ids.Length; i++)
            score = NativeUnsignedLong(unchecked(score + (ulong)(long)GetValue((byte)(i + 2), ids[i])));
        return score;
    }

    public void Initialize(ReadOnlySpan<byte> initialLevelName)
    {
        var owner = game.Players[0];
        owner.Items.Ship = Known(KnownItem.ShipUspTalon);
        owner.Items.Weapons[0].Id = Known(KnownItem.WeaponFrontPulseCannon);
        owner.Items.Weapons[1].Id = 0;
        owner.Items.Shield = Known(KnownItem.ShieldGencoreHighEnergy);
        owner.Items.Generator = Known(KnownItem.GeneratorAdvancedMr12);
        Array.Clear(owner.Items.Sidekicks);
        owner.Items.Special = 0;
        owner.LastItems = owner.Items.Copy();
        var second = game.Players[1];
        second.Items = owner.Items.Copy();
        second.Items.Weapons[1].Id = Known(KnownItem.WeaponRearVulcanCannon);
        second.Items.SidekickLevel = 101; second.Items.SidekickSeries = 0;
        game.GameHasRepeated = game.OnePlayerAction = game.SuperTyrian = game.TwoPlayerMode = false;
        game.SuperArcadeMode = 0;
        game.SecretHint = (byte)(MersenneTwister.NextUInt32() % 3 + 1);
        for (var i = 0; i < 2; i++)
        {
            var player = game.Players[i];
            foreach (var port in new[] { 0, 1 }) player.Items.Weapons[port].Power = 1;
            player.WeaponMode = 1;
            player.Armor = items.Ships[player.Items.Ship].Dmg;
            player.IsDragonwing = i == 1;
        }
        game.MainLevel = game.SaveLevel = 1;
        var end = initialLevelName.IndexOf((byte)0);
        game.LastLevelName = (end < 0 ? initialLevelName : initialLevelName[..end]).ToArray();
    }

    public void AdjustDifficulty()
    {
        if (game.InitialDifficulty is < 1 or > 9) throw new InvalidDataException("Initial difficulty must be between 1 and 9.");
        float[] multipliers = [0, 0.4f, 0.8f, 1.3f, 1.6f, 2, 2, 3, 3, 3];
        var score = game.TwoPlayerMode ? NativeUnsignedLong(unchecked(game.Players[0].Cash + game.Players[1].Cash)) : TotalScore(game.Players[0]);
        var rounded = MathF.Round(score * multipliers[game.InitialDifficulty], MidpointRounding.AwayFromZero);
        if (rounded < 0 || rounded >= (NarrowLong ? 4294967296f : 18446744073709551616f))
            throw new InvalidDataException("Adjusted score exceeds native unsigned long range.");
        var adjusted = (ulong)rounded;
        uint[] thresholds = game.TwoPlayerMode ? [10000, 20000, 50000, 80000, 125000, 200000, 400000, 600000] :
            [40000, 70000, 150000, 300000, 600000, 1000000, 2000000, 3000000];
        var difficulty = 1u;
        foreach (var threshold in thresholds) { if (adjusted < threshold) break; difficulty++; }
        game.Difficulty = unchecked((sbyte)Math.Max(unchecked((uint)game.Difficulty), difficulty));
    }

    private byte Known(KnownItem item) => checked((byte)items.KnownIndex(item));

    private int BaseCost(byte type, ushort index)
    {
        IReadOnlyList<ShopItem>? table = type switch
        {
            2 => items.Ships,
            3 or 4 => items.Weapons,
            5 => items.Shields,
            6 => items.Generators,
            7 or 8 => items.Sidekicks,
            _ => null
        };
        if (table is null) return 0;
        if (index >= table.Count) throw new InvalidDataException("Shop item exceeds loaded item definitions.");
        return table[index].Cost;
    }
}
