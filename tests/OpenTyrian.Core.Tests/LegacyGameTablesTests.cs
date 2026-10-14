using OpenTyrian.Core;

namespace OpenTyrian.Core.Tests;

internal static class LegacyGameTablesTests
{
    public static void Run()
    {
        byte[] ships = [3, 1, 5, 10, 2, 11, 12];
        byte[][] weapons =
        [
            [9, 31, 32, 33, 34], [19, 8, 22, 41, 34], [27, 5, 20, 42, 31],
            [15, 3, 28, 22, 12], [23, 35, 25, 14, 6], [2, 5, 21, 4, 7], [40, 38, 37, 41, 36]
        ];
        byte[] specials =
        [
            17, 17, 18, 0, 0, 0, 10, 0, 0, 0, 0, 0, 44, 0, 10, 0, 19, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 45, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0
        ];
        byte[][][] sidekicks =
        [
            [[0, 0], [0, 0], [0, 0]], [[1, 1], [16, 16], [30, 30]], [[2, 2], [29, 29], [29, 20]],
            [[3, 3], [21, 21], [12, 0]], [[4, 4], [18, 18], [16, 23]], [[0, 0], [0, 0], [0, 0]],
            [[6, 6], [29, 16], [0, 22]], [[7, 7], [19, 19], [19, 28]], [[0, 0], [0, 0], [0, 0]],
            [[0, 0], [0, 0], [0, 0]], [[10, 10], [21, 21], [21, 27]], [[0, 0], [0, 0], [0, 0]],
            [[0, 0], [0, 0], [0, 0]], [[13, 13], [17, 17], [13, 26]], [[0, 0], [0, 0], [0, 0]],
            [[15, 15], [15, 16], [15, 16]]
        ];
        byte[][] shipCombos =
        [
            [5, 4, 7], [1, 2, 0], [14, 4, 0], [4, 5, 0], [6, 5, 0], [7, 8, 0],
            [7, 9, 0], [10, 3, 5], [5, 8, 9], [1, 3, 0], [7, 16, 17],
            [2, 11, 12], [3, 8, 10], [0, 0, 0]
        ];
        byte[] superTyrianCombos = [15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 7, 8, 5, 25, 14, 4, 6, 3, 9, 2, 26];
        byte[][] keyboardCombos =
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
        ushort[] planetSprites = [4, 1, 2, 3, 20, 36, 52, 68, 84, 100, 116, 132, 151, 151, 151, 151, 52, 52, 1, 2, 4];
        bool[] animatedPlanets = [true, false, false, false, true, true, true, true, true, true, true, true,
            false, false, false, false, false, false, false, false, true];
        byte[] launchSounds = [13, 6, 26];
        byte[] superTyrianDrops = [1, 2, 4, 5];
        byte[] superArcadeNextShips = [3, 9, 6, 2, 5, 1, 4, 3, 7];
        ushort[] superArcadeSpecials = [7, 8, 9, 10, 11, 12, 13];
        byte[][] superArcadeWeaponPairs =
        [
            [7, 37], [8, 6], [9, 15], [10, 40], [11, 16], [12, 14], [13, 41]
        ];

        for (var mode = 0; mode < ships.Length; mode++)
        {
            Check(LegacyGameTables.ArcadeShip(mode) == ships[mode], $"Arcade ship table differs at {mode}.");
            for (var upgrade = 0; upgrade < weapons[mode].Length; upgrade++)
                Check(LegacyGameTables.ArcadeWeapon(mode, upgrade) == weapons[mode][upgrade],
                    $"Arcade weapon table differs at {mode},{upgrade}.");
        }
        for (var index = 0; index < specials.Length; index++)
            Check(LegacyGameTables.ArcadeSpecial(index) == specials[index], $"Arcade special table differs at {index}.");
        for (var series = 0; series < sidekicks.Length; series++)
            for (var upgrade = 0; upgrade < sidekicks[series].Length; upgrade++)
                for (var port = 0; port < sidekicks[series][upgrade].Length; port++)
                    Check(LegacyGameTables.SidekickOption(series, upgrade, port) == sidekicks[series][upgrade][port],
                        $"Sidekick option table differs at {series},{upgrade},{port}.");
        for (var ship = 0; ship < shipCombos.Length; ship++)
            for (var slot = 0; slot < shipCombos[ship].Length; slot++)
                Check(LegacyGameTables.PlayerSpecialShipCombo(ship, slot) == shipCombos[ship][slot],
                    $"Player special ship table differs at {ship},{slot}.");
        for (var combo = 0; combo < superTyrianCombos.Length; combo++)
            Check(LegacyGameTables.SuperTyrianSpecialCombo(combo) == superTyrianCombos[combo],
                $"SuperTyrian ship table differs at {combo}.");
        for (var combo = 0; combo < keyboardCombos.Length; combo++)
            for (var step = 0; step < keyboardCombos[combo].Length; step++)
                Check(LegacyGameTables.KeyboardComboStep(combo, step) == keyboardCombos[combo][step],
                    $"Keyboard special table differs at {combo},{step}.");
        for (var planet = 0; planet < planetSprites.Length; planet++)
        {
            Check(LegacyGameTables.PlanetSprite(planet) == planetSprites[planet],
                $"Planet sprite table differs at {planet}.");
            Check(LegacyGameTables.IsPlanetAnimated(planet) == animatedPlanets[planet],
                $"Planet animation table differs at {planet}.");
        }
        for (var index = 0; index < launchSounds.Length; index++)
            Check(LegacyGameTables.EnemyLaunchSound(index) == launchSounds[index],
                $"Enemy launch sound table differs at {index}.");
        for (var index = 0; index < superTyrianDrops.Length; index++)
            Check(LegacyGameTables.SuperTyrianSpecialDrop(index) == superTyrianDrops[index],
                $"SuperTyrian special drop table differs at {index}.");
        for (var index = 0; index < superArcadeNextShips.Length; index++)
            Check(LegacyGameTables.SuperArcadeNextShip(index) == superArcadeNextShips[index],
                $"SuperArcade progression table differs at {index}.");
        for (var mode = 0; mode < superArcadeSpecials.Length; mode++)
            Check(LegacyGameTables.SuperArcadeStartingSpecial(mode) == superArcadeSpecials[mode],
                $"SuperArcade starting special table differs at {mode}.");
        for (var mode = 0; mode < superArcadeWeaponPairs.Length; mode++)
            for (var variant = 0; variant < superArcadeWeaponPairs[mode].Length; variant++)
                Check(LegacyGameTables.SuperArcadeSpecialWeapon(mode, variant) == superArcadeWeaponPairs[mode][variant],
                    $"SuperArcade special weapon table differs at {mode},{variant}.");

        Throws<ArgumentOutOfRangeException>(() => LegacyGameTables.ArcadeShip(-1));
        Throws<ArgumentOutOfRangeException>(() => LegacyGameTables.ArcadeWeapon(0, 5));
        Throws<ArgumentOutOfRangeException>(() => LegacyGameTables.ArcadeSpecial(42));
        Throws<ArgumentOutOfRangeException>(() => LegacyGameTables.SidekickOption(16, 0, 0));
        Throws<ArgumentOutOfRangeException>(() => LegacyGameTables.SidekickOption(0, 3, 0));
        Throws<ArgumentOutOfRangeException>(() => LegacyGameTables.PlayerSpecialShipCombo(14, 0));
        Throws<ArgumentOutOfRangeException>(() => LegacyGameTables.SuperTyrianSpecialCombo(21));
        Throws<ArgumentOutOfRangeException>(() => LegacyGameTables.KeyboardComboStep(26, 0));
        Throws<ArgumentOutOfRangeException>(() => LegacyGameTables.PlanetSprite(21));
        Throws<ArgumentOutOfRangeException>(() => LegacyGameTables.IsPlanetAnimated(-1));
        Throws<ArgumentOutOfRangeException>(() => LegacyGameTables.EnemyLaunchSound(3));
        Throws<ArgumentOutOfRangeException>(() => LegacyGameTables.SuperTyrianSpecialDrop(4));
        Throws<ArgumentOutOfRangeException>(() => LegacyGameTables.SuperArcadeNextShip(9));
        Throws<ArgumentOutOfRangeException>(() => LegacyGameTables.SuperArcadeStartingSpecial(7));
        Throws<ArgumentOutOfRangeException>(() => LegacyGameTables.SuperArcadeSpecialWeapon(7, 0));
        Throws<ArgumentOutOfRangeException>(() => LegacyGameTables.SuperArcadeSpecialWeapon(0, 2));
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void Throws<T>(Action action) where T : Exception
    {
        try { action(); } catch (T) { return; }
        throw new InvalidOperationException($"Expected {typeof(T).Name}.");
    }
}
