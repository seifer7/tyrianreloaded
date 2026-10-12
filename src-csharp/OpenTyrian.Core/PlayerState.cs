/*
 * OpenTyrian: A modern cross-platform port of Tyrian
 * Copyright (C) 2007-2009 The OpenTyrian Development Team
 * Distributed under the GNU General Public License, version 2 or later.
 * See COPYING for details.
 */

namespace OpenTyrian.Core;

public enum WeaponPort { Front, Rear }
public enum SidekickPort { Left, Right }

public struct PlayerWeapon
{
    public byte Id;
    public byte Power;
}

public sealed class PlayerItems
{
    public byte Ship { get; set; }
    public byte Generator { get; set; }
    public byte Shield { get; set; }
    public PlayerWeapon[] Weapons { get; } = new PlayerWeapon[2];
    public byte[] Sidekicks { get; } = new byte[2];
    public byte Special { get; set; }
    public byte SidekickSeries { get; set; }
    public byte SidekickLevel { get; set; }
    public byte SuperArcadeMode { get; set; }

    public byte[] ToLegacyItems(byte initialEpisode)
        => [Weapons[0].Id, Weapons[1].Id, SuperArcadeMode, Sidekicks[0], Sidekicks[1], Generator,
            SidekickLevel, SidekickSeries, initialEpisode, Shield, Special, Ship];

    public byte LoadLegacyItems(ReadOnlySpan<byte> items)
    {
        if (items.Length != 12) throw new ArgumentException("Legacy player items require exactly 12 bytes.", nameof(items));
        Weapons[0].Id = items[0];
        Weapons[1].Id = items[1];
        SuperArcadeMode = items[2];
        Sidekicks[0] = items[3];
        Sidekicks[1] = items[4];
        Generator = items[5];
        SidekickLevel = items[6];
        SidekickSeries = items[7];
        Shield = items[9];
        Special = items[10];
        Ship = items[11];
        return items[8];
    }

    public PlayerItems Copy()
    {
        var copy = new PlayerItems
        {
            Ship = Ship,
            Generator = Generator,
            Shield = Shield,
            Special = Special,
            SidekickSeries = SidekickSeries,
            SidekickLevel = SidekickLevel,
            SuperArcadeMode = SuperArcadeMode
        };
        Weapons.CopyTo(copy.Weapons, 0);
        Sidekicks.CopyTo(copy.Sidekicks, 0);
        return copy;
    }
}

public sealed class PlayerSidekick
{
    public int AmmoMax { get; set; }
    public uint AmmoRefillTicksMax { get; set; }
    public uint Style { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int Ammo { get; set; }
    public uint AmmoRefillTicks { get; set; }
    public bool AnimationEnabled { get; set; }
    public uint AnimationFrame { get; set; }
    public uint Charge { get; set; }
    public uint ChargeTicks { get; set; }
}

public sealed class PlayerState
{
    private readonly int _lifeWeaponPort;
    private ulong _cash;
    // C unsigned long is 32-bit on Windows, but 64-bit on 64-bit Unix.
    public ulong Cash
    {
        get => _cash;
        set => _cash = OperatingSystem.IsWindows() || IntPtr.Size == 4 ? unchecked((uint)value) : value;
    }
    public PlayerItems Items { get; set; } = new();
    public PlayerItems LastItems { get; set; } = new();
    public bool IsDragonwing { get; set; }
    public byte Lives
    {
        get => Items.Weapons[_lifeWeaponPort].Power;
        set => Items.Weapons[_lifeWeaponPort].Power = value;
    }
    public uint ShieldMax { get; set; }
    public uint InitialArmor { get; set; }
    public uint ShotHitAreaX { get; set; }
    public uint ShotHitAreaY { get; set; }
    public bool IsAlive { get; set; }
    public uint InvulnerableTicks { get; set; }
    public uint ExplodingTicks { get; set; }
    public uint Shield { get; set; }
    public uint Armor { get; set; }
    public uint WeaponMode { get; set; }
    public uint Superbombs { get; set; }
    public uint PurpleBallsNeeded { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int[] OldX { get; } = new int[20];
    public int[] OldY { get; } = new int[20];
    public int XVelocity { get; set; }
    public int YVelocity { get; set; }
    public uint XFrictionTicks { get; set; }
    public uint YFrictionTicks { get; set; }
    public int DeltaXShotMove { get; set; }
    public int DeltaYShotMove { get; set; }
    public int LastXShotMove { get; set; }
    public int LastYShotMove { get; set; }
    public int LastXExplosionFollow { get; set; }
    public int LastYExplosionFollow { get; set; }
    public PlayerSidekick[] Sidekicks { get; } = [new(), new()];

    public PlayerState(int playerIndex)
    {
        if (playerIndex is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(playerIndex));
        }

        _lifeWeaponPort = playerIndex;
    }
}

public static class PlayerRules
{
    private static readonly uint[] PurpleBallsRequired = [1, 1, 2, 4, 8, 12, 16, 20, 25, 30, 40, 50];

    public static void CalculatePurpleBalls(PlayerState player)
    {
        if (player.Lives >= PurpleBallsRequired.Length)
        {
            throw new InvalidDataException("Player lives/weapon power exceeds the native 0..11 table.");
        }

        player.PurpleBallsNeeded = PurpleBallsRequired[player.Lives];
    }

    public static bool PowerUpWeapon(PlayerState player, WeaponPort port, Span<byte> shotMultiPosition)
    {
        var index = (int)port;
        if ((uint)index > 1 || shotMultiPosition.Length <= index)
        {
            throw new ArgumentOutOfRangeException(nameof(port));
        }

        ref var weapon = ref player.Items.Weapons[index];
        var canPowerUp = weapon.Id != 0 && weapon.Power < 11;
        if (canPowerUp)
        {
            weapon.Power++;
            shotMultiPosition[index] = 0;
            CalculatePurpleBalls(player);
        }
        else
        {
            player.Cash = unchecked(player.Cash + 1000);
        }

        return canPowerUp;
    }

    public static void GotPurpleBall(PlayerState player, Span<byte> shotMultiPosition)
    {
        if (player.PurpleBallsNeeded > 1)
        {
            player.PurpleBallsNeeded--;
        }
        else
        {
            PowerUpWeapon(player, player.IsDragonwing ? WeaponPort.Rear : WeaponPort.Front, shotMultiPosition);
        }
    }

    public static bool AllDead(PlayerState first, PlayerState second, bool twoPlayerMode)
    {
        return !first.IsAlive && (!twoPlayerMode || !second.IsAlive);
    }

    public static bool AllAlive(PlayerState first, PlayerState second, bool twoPlayerMode)
    {
        return first.IsAlive && (!twoPlayerMode || second.IsAlive);
    }
}
