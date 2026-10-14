/*
 * OpenTyrian: A modern cross-platform port of Tyrian
 * Copyright (C) 2007-2009 The OpenTyrian Development Team
 * Distributed under the GNU General Public License, version 2 or later.
 * See COPYING for details.
 */

namespace OpenTyrian.Core;

public static class LegacyGameTables
{
    private static readonly byte[] ArcadeShips = [3, 1, 5, 10, 2, 11, 12];
    private static readonly byte[][] ArcadeWeapons =
    [
        [9, 31, 32, 33, 34], [19, 8, 22, 41, 34], [27, 5, 20, 42, 31],
        [15, 3, 28, 22, 12], [23, 35, 25, 14, 6], [2, 5, 21, 4, 7], [40, 38, 37, 41, 36]
    ];
    private static readonly byte[] ArcadeSpecials =
    [
        17, 17, 18, 0, 0, 0, 10, 0, 0, 0, 0, 0, 44, 0, 10, 0, 19, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 45, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0
    ];
    private static readonly byte[][][] SidekickOptions =
    [
        [[0, 0], [0, 0], [0, 0]], [[1, 1], [16, 16], [30, 30]], [[2, 2], [29, 29], [29, 20]],
        [[3, 3], [21, 21], [12, 0]], [[4, 4], [18, 18], [16, 23]], [[0, 0], [0, 0], [0, 0]],
        [[6, 6], [29, 16], [0, 22]], [[7, 7], [19, 19], [19, 28]], [[0, 0], [0, 0], [0, 0]],
        [[0, 0], [0, 0], [0, 0]], [[10, 10], [21, 21], [21, 27]], [[0, 0], [0, 0], [0, 0]],
        [[0, 0], [0, 0], [0, 0]], [[13, 13], [17, 17], [13, 26]], [[0, 0], [0, 0], [0, 0]],
        [[15, 15], [15, 16], [15, 16]]
    ];
    private static readonly byte[][] PlayerSpecialShipCombos =
    [
        [5, 4, 7], [1, 2, 0], [14, 4, 0], [4, 5, 0], [6, 5, 0], [7, 8, 0],
        [7, 9, 0], [10, 3, 5], [5, 8, 9], [1, 3, 0], [7, 16, 17],
        [2, 11, 12], [3, 8, 10], [0, 0, 0]
    ];
    private static readonly byte[] SuperTyrianShipCombos =
        [15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 7, 8, 5, 25, 14, 4, 6, 3, 9, 2, 26];
    private static readonly byte[][] SpecialKeyboardCombos =
    [
        [2, 1, 2, 5, 137, 0, 0, 0], [4, 3, 2, 5, 138, 0, 0, 0],
        [3, 4, 6, 139, 0, 0, 0, 0], [2, 5, 142, 0, 0, 0, 0, 0],
        [6, 2, 6, 143, 0, 0, 0, 0], [6, 7, 5, 8, 6, 7, 5, 112],
        [7, 8, 101, 0, 0, 0, 0, 0], [1, 7, 6, 146, 0, 0, 0, 0],
        [8, 6, 7, 1, 120, 0, 0, 0], [3, 6, 8, 5, 121, 0, 0, 0],
        [1, 2, 7, 8, 119, 0, 0, 0], [3, 4, 3, 6, 123, 0, 0, 0],
        [6, 7, 5, 8, 124, 0, 0, 0], [1, 6, 125, 0, 0, 0, 0, 0],
        [9, 5, 126, 0, 0, 0, 0, 0], [1, 7, 127, 0, 0, 0, 0, 0],
        [1, 8, 128, 0, 0, 0, 0, 0], [9, 7, 129, 0, 0, 0, 0, 0],
        [9, 8, 130, 0, 0, 0, 0, 0], [4, 2, 3, 5, 131, 0, 0, 0],
        [3, 1, 2, 8, 132, 0, 0, 0], [2, 4, 5, 133, 0, 0, 0, 0],
        [3, 4, 2, 8, 134, 0, 0, 0], [1, 4, 6, 135, 0, 0, 0, 0],
        [1, 3, 6, 137, 0, 0, 0, 0], [1, 4, 3, 4, 7, 136, 0, 0]
    ];
    private static readonly ushort[] PlanetSprites =
        [4, 1, 2, 3, 20, 36, 52, 68, 84, 100, 116, 132, 151, 151, 151, 151, 52, 52, 1, 2, 4];
    private static readonly bool[] AnimatedPlanets =
        [true, false, false, false, true, true, true, true, true, true, true, true, false, false, false,
            false, false, false, false, false, true];
    private static readonly byte[] EnemyLaunchSounds = [13, 6, 26];
    private static readonly byte[] SuperTyrianSpecialDrops = [1, 2, 4, 5];
    private static readonly byte[] SuperArcadeNextShips = [3, 9, 6, 2, 5, 1, 4, 3, 7];
    private static readonly ushort[] SuperArcadeStartingSpecials = [7, 8, 9, 10, 11, 12, 13];
    private static readonly byte[][] SuperArcadeSpecialWeaponPairs =
    [
        [7, 37], [8, 6], [9, 15], [10, 40], [11, 16], [12, 14], [13, 41]
    ];

    public const int ArcadeShipCount = 7;
    public const int ArcadeWeaponUpgradeCount = 5;
    public const int ArcadeSpecialCount = 42;
    public const int SidekickSeriesCount = 16;
    public const int SidekickUpgradeCount = 3;
    public const int PlayerSpecialShipComboCount = 14;
    public const int SuperTyrianSpecialCount = 21;
    public const int KeyboardComboCount = 26;
    public const int KeyboardComboLength = 8;
    public const int PlanetCount = 21;
    public const int EnemyLaunchSoundCount = 3;
    public const int SuperTyrianSpecialDropCount = 4;
    public const int SuperArcadeModeCount = 7;
    public const int SuperArcadeNextShipCount = 9;

    public static byte ArcadeShip(int mode) => ArcadeShips[CheckedIndex(mode, ArcadeShipCount, nameof(mode))];

    public static byte ArcadeWeapon(int mode, int upgrade)
    {
        if ((uint)mode >= ArcadeWeapons.Length) throw new ArgumentOutOfRangeException(nameof(mode));
        return ArcadeWeapons[mode][CheckedIndex(upgrade, ArcadeWeaponUpgradeCount, nameof(upgrade))];
    }

    public static byte ArcadeFrontWeapon(int mode) => ArcadeWeapon(mode, 0);

    public static byte ArcadeSpecial(int index) => ArcadeSpecials[CheckedIndex(index, ArcadeSpecialCount, nameof(index))];

    public static byte SidekickOption(int series, int upgrade, int port)
    {
        if ((uint)series >= SidekickOptions.Length) throw new ArgumentOutOfRangeException(nameof(series));
        if ((uint)upgrade >= SidekickUpgradeCount) throw new ArgumentOutOfRangeException(nameof(upgrade));
        return SidekickOptions[series][upgrade][CheckedIndex(port, 2, nameof(port))];
    }

    public static byte PlayerSpecialShipCombo(int ship, int slot)
    {
        if ((uint)ship >= PlayerSpecialShipCombos.Length) throw new ArgumentOutOfRangeException(nameof(ship));
        return PlayerSpecialShipCombos[ship][CheckedIndex(slot, 3, nameof(slot))];
    }

    public static byte SuperTyrianSpecialCombo(int index)
        => SuperTyrianShipCombos[CheckedIndex(index, SuperTyrianSpecialCount, nameof(index))];

    public static byte KeyboardComboStep(int combo, int step)
    {
        if ((uint)combo >= SpecialKeyboardCombos.Length) throw new ArgumentOutOfRangeException(nameof(combo));
        return SpecialKeyboardCombos[combo][CheckedIndex(step, KeyboardComboLength, nameof(step))];
    }

    public static ushort PlanetSprite(int planet)
        => PlanetSprites[CheckedIndex(planet, PlanetCount, nameof(planet))];

    public static bool IsPlanetAnimated(int planet)
        => AnimatedPlanets[CheckedIndex(planet, PlanetCount, nameof(planet))];

    public static byte EnemyLaunchSound(int index)
        => EnemyLaunchSounds[CheckedIndex(index, EnemyLaunchSoundCount, nameof(index))];

    public static byte SuperTyrianSpecialDrop(int index)
        => SuperTyrianSpecialDrops[CheckedIndex(index, SuperTyrianSpecialDropCount, nameof(index))];

    public static byte SuperArcadeNextShip(int mode)
        => SuperArcadeNextShips[CheckedIndex(mode, SuperArcadeNextShipCount, nameof(mode))];

    public static ushort SuperArcadeStartingSpecial(int mode)
        => SuperArcadeStartingSpecials[CheckedIndex(mode, SuperArcadeModeCount, nameof(mode))];

    public static byte SuperArcadeSpecialWeapon(int mode, int variant)
    {
        if ((uint)mode >= SuperArcadeSpecialWeaponPairs.Length) throw new ArgumentOutOfRangeException(nameof(mode));
        return SuperArcadeSpecialWeaponPairs[mode][CheckedIndex(variant, 2, nameof(variant))];
    }

    private static int CheckedIndex(int index, int count, string parameterName)
        => (uint)index < count ? index : throw new ArgumentOutOfRangeException(parameterName);
}
