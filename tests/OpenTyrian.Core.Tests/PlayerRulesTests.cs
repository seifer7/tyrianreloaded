using OpenTyrian.Core;

namespace OpenTyrian.Core.Tests;

internal static class PlayerRulesTests
{
    public static void Run()
    {
        uint[] required = [1, 1, 2, 4, 8, 12, 16, 20, 25, 30, 40, 50];
        var first = new PlayerState(0);
        var second = new PlayerState(1) { IsDragonwing = true };
        for (byte lives = 0; lives < 12; lives++)
        {
            first.Lives = lives;
            PlayerRules.CalculatePurpleBalls(first);
            Check(first.PurpleBallsNeeded == required[lives], "Purple-ball table differs.");
        }

        first.Items.Weapons[0] = new PlayerWeapon { Id = 2, Power = 1 };
        byte[] shots = [4, 5, 6];
        Check(PlayerRules.PowerUpWeapon(first, WeaponPort.Front, shots), "Power-up should succeed.");
        Check(first.Lives == 2 && first.Items.Weapons[0].Power == 2 && shots[0] == 0 && shots[1] == 5, "Lives alias or shot reset differs.");
        Check(first.PurpleBallsNeeded == 2, "Power-up recalculation differs.");
        PlayerRules.GotPurpleBall(first, shots);
        Check(first.PurpleBallsNeeded == 1 && first.Lives == 2, "Purple-ball countdown differs.");
        PlayerRules.GotPurpleBall(first, shots);
        Check(first.Lives == 3 && first.PurpleBallsNeeded == 4, "Purple-ball upgrade differs.");
        first.Lives = 11;
        Check(!PlayerRules.PowerUpWeapon(first, WeaponPort.Front, shots) && first.Cash == 1000, "Max-power cash consolation differs.");
        first.Items.Weapons[0].Id = 0;
        first.Lives = 1;
        Check(!PlayerRules.PowerUpWeapon(first, WeaponPort.Front, shots) && first.Cash == 2000, "Empty-weapon consolation differs.");
        first.Cash = uint.MaxValue;
        PlayerRules.PowerUpWeapon(first, WeaponPort.Front, shots);
        var expectedCash = OperatingSystem.IsWindows() || IntPtr.Size == 4 ? 999ul : (ulong)uint.MaxValue + 1000;
        Check(first.Cash == expectedCash, "Native unsigned-long cash overflow differs.");
        second.Items.Weapons[1] = new PlayerWeapon { Id = 3, Power = 1 };
        second.PurpleBallsNeeded = 1;
        PlayerRules.GotPurpleBall(second, shots);
        Check(second.Lives == 2 && second.Items.Weapons[0].Power == 0, "Dragonwing rear-weapon upgrade differs.");
        first.LastItems = first.Items.Copy();
        first.Items.Weapons[0].Power = 5;
        Check(first.LastItems.Weapons[0].Power == 1, "Item snapshots must not share weapon storage.");
        foreach (var aliveFirst in new[] { false, true })
        {
            foreach (var aliveSecond in new[] { false, true })
            {
                first.IsAlive = aliveFirst;
                second.IsAlive = aliveSecond;
                Check(PlayerRules.AllDead(first, second, false) == !aliveFirst, "Single-player death differs.");
                Check(PlayerRules.AllDead(first, second, true) == (!aliveFirst && !aliveSecond), "Two-player death differs.");
                Check(PlayerRules.AllAlive(first, second, false) == aliveFirst, "Single-player survival differs.");
                Check(PlayerRules.AllAlive(first, second, true) == (aliveFirst && aliveSecond), "Two-player survival differs.");
            }
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
