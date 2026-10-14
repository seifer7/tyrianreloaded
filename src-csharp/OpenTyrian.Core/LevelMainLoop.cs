/*
 * OpenTyrian: A modern cross-platform port of Tyrian
 * Copyright (C) 2007-2013 The OpenTyrian Development Team
 * Distributed under the GNU General Public License, version 2 or later.
 * See COPYING for details.
 */

namespace OpenTyrian.Core;

public sealed class LevelMainLoopState
{
    public bool PlayDemo { get; set; }
    public bool RecordDemo { get; set; }
    public bool NetworkGame { get; set; }
    public bool TwoPlayerMode { get; set; }
    public bool EndLevel { get; set; }
    public bool ReallyEndLevel { get; set; }
    public bool PlayerEndLevel { get; set; }
    public bool ExtraGame { get; set; }
    public bool DoNotSaveBackup { get; set; }
    public byte MainLevel { get; set; }
    public uint Power { get; set; }
    public uint LastPower { get; set; }
    public uint[] OldWeaponBars { get; } = new uint[2];
}

public interface ILevelMainLoopHost
{
    bool PrepareNextLevel();
    void SetVolume();
    void LoadMap(LevelMainLoopState state);
    void SetMouseRelative(bool enabled);
    void FadeSong();
    void InitializePlayersAndDisplay();
    void InitializeMapAndShipState();
    void InitializeFlags();
    void InitializeStatusDisplay();
    void ClearKeyboard();
    void ClearSpecialRequests();
    void SeedRandom(uint seed);
    void InitializeStarfield();
    void SetNewGameSpeed();
    void SaveGame(int slot, string description);
    void RecordLevelDemo();
    void InitializeCombatState();
    bool RunLevelFrame(uint[] oldWeaponBars, ref int lastEnemyOnScreen, byte[] buffer);
}

public static class LevelMainLoop
{
    public static void Run(LevelMainLoopState state, ILevelMainLoopHost host)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(host);

        var firstLevel = true;
        var lastEnemyOnScreen = 0;
        var buffer = new byte[256];
        while (true)
        {
            if (!firstLevel && !host.PrepareNextLevel())
                return;
            firstLevel = false;

            host.SetVolume();
            state.EndLevel = false;
            state.ReallyEndLevel = false;
            state.PlayerEndLevel = false;
            state.ExtraGame = false;
            state.DoNotSaveBackup = false;
            host.LoadMap(state);

            if (state.MainLevel == 0)
                return;

            if (!state.PlayDemo)
                host.SetMouseRelative(true);

            host.FadeSong();
            host.InitializePlayersAndDisplay();
            host.InitializeMapAndShipState();
            host.InitializeFlags();
            state.LastPower = state.Power / 10;
            host.InitializeStatusDisplay();
            host.ClearKeyboard();

            if (state.NetworkGame)
            {
                host.ClearSpecialRequests();
                host.SeedRandom(32402394);
            }

            host.InitializeStarfield();
            host.SetNewGameSpeed();
            host.SetVolume();

            if (!state.PlayDemo && !state.DoNotSaveBackup)
                host.SaveGame(state.TwoPlayerMode ? 22 : 11, "LAST LEVEL    ");

            if (!state.PlayDemo && state.RecordDemo)
                host.RecordLevelDemo();

            host.InitializeCombatState();
            while (host.RunLevelFrame(state.OldWeaponBars, ref lastEnemyOnScreen, buffer))
            {
            }
        }
    }
}
