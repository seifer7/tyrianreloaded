/*
 * OpenTyrian: A modern cross-platform port of Tyrian
 * Copyright (C) 2007-2013 The OpenTyrian Development Team
 * Distributed under the GNU General Public License, version 2 or later.
 * See COPYING for details.
 */

namespace OpenTyrian.Core;

public sealed class LevelFrameState
{
    public required IReadOnlyList<PlayerState> Players { get; init; }
    public required bool[] ShotAvailable { get; init; }
    public required bool[] Smoothies { get; init; }
    public int[] MapYPositions { get; } = new int[3];
    public int[] BackgroundWraps { get; } = new int[3];
    public int[] BackgroundWrapTargets { get; } = new int[3];
    public bool NetworkGame { get; set; }
    public bool OnePlayerAction { get; set; }
    public bool TwoPlayerMode { get; set; }
    public int StarShowVgaSpecialCode { get; set; }
    public bool MusicFade
    {
        get => MusicMaster.MusicFade;
        set => MusicMaster.MusicFade = value;
    }
    public ushort TemporaryVolume { get; set; }
    public ushort EffectsVolume { get; set; }
    public bool EndLevel { get; set; }
    public bool Playing { get; set; }
    public bool FirstGameOver { get; set; }
    public int LevelSong { get; set; }
    public byte LevelEnd { get; set; }
    public bool ReallyEndLevel { get; set; }
    public bool AnySmoothies { get; set; }
    public int Screen { get; set; }
    public int GameScreen { get; set; }
    public int SecondaryScreen { get; set; }
    public int ScreenSegment { get; set; }
    public ushort CurrentLocation { get; set; }
    public ushort EventLocation { get; set; } = 1;
    public ushort MaximumEvent { get; set; }
    public ushort[] EventTimes { get; init; } = [];
    public int CurrentMapXOffset { get; set; }
    public int OldMapX3Offset { get; set; }
    public int Background3Move { get; set; }
    public int TemporaryMapXOffset { get; set; }
    public int TemporaryBackgroundMove { get; set; }
    public int MapX2Offset { get; set; }
    public int EnemyOnScreen { get; set; }
    public int StopBackgroundNumber { get; set; }
    public int ProcessorType { get; set; }
    public int Background2Over { get; set; }
    public bool Wild { get; set; }
    public bool Background2NotTransparent { get; set; }
    public int Background3Over { get; set; }
    public bool TopEnemyOver { get; set; }
    public bool SkyEnemyOverAll { get; set; }
    public bool PortConfigChange { get; set; }
    public bool PortConfigDone { get; set; }
    public bool RandomExplosions { get; set; }
    public bool ReturnActive { get; set; }
    public bool FilterActive { get; set; }
    public int LevelFilter { get; set; }
    public int LevelBrightness { get; set; }
    public bool HasDueEvent => EventLocation > 0 && EventLocation <= MaximumEvent &&
        EventLocation <= EventTimes.Length && EventTimes[EventLocation - 1] <= CurrentLocation;
    public bool AllPlayersGone { get; set; }

    public void UpdatePlayersGone()
    {
        if (Players.Count != 2) throw new InvalidOperationException("Level frames require two player states.");
        AllPlayersGone = PlayerRules.AllDead(Players[0], Players[1], TwoPlayerMode) &&
            ((Players[0].Lives == 1 && Players[0].ExplodingTicks == 0) || (!OnePlayerAction && !TwoPlayerMode)) &&
            ((Players[1].Lives == 1 && Players[1].ExplodingTicks == 0) || !TwoPlayerMode);
    }
}

public interface ILevelFrameHost
{
    void SetVolume(ushort music, ushort effects);
    void PlaySong(int song);
    void DrawHud(uint[] oldWeaponBars);
    void ProcessEvent(LevelFrameState state);
    void CheckSmoothies(LevelFrameState state);
    void DrawBackgrounds(LevelFrameState state, ref int lastEnemyOnScreen);
    void ProcessPlayerShot(LevelFrameState state, int shotIndex);
    void CollidePlayer(LevelFrameState state, int playerNumber);
    void RunPlayerFunctions(LevelFrameState state);
    void DrawEnemyShots(LevelFrameState state);
    void DrawBackground3(LevelFrameState state);
    void DrawEnemy(LevelFrameState state, int priority);
    void UpdateExplosions(LevelFrameState state);
    void DrawBackground2(LevelFrameState state, bool blend);
    void DrawArmorWarning(LevelFrameState state);
    uint NextRandomUInt32();
    void SetupLargeExplosion(bool special, int size, int x, int y);
    void PlayLevelSounds(LevelFrameState state);
    void JumpEvent(LevelFrameState state, ushort target);
    void DrawDebugAndTimer(LevelFrameState state, byte[] buffer);
    bool HandleInput(LevelFrameState state);
    void UpdateNetwork(LevelFrameState state);
    void DrawSuperpixels(LevelFrameState state);
    void FilterScreen(LevelFrameState state, int filter, int brightness);
    void DrawBossBar(LevelFrameState state);
    void DrawInGameDisplays(LevelFrameState state);
    void ShowStars(LevelFrameState state);
    void UpdateEndState(LevelFrameState state);
    void HandleChat(LevelFrameState state);
}

public static class LevelFrame
{
    public static bool Run(LevelFrameState state, uint[] oldWeaponBars,
        ref int lastEnemyOnScreen, byte[] buffer, ILevelFrameHost host)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(oldWeaponBars);
        ArgumentNullException.ThrowIfNull(buffer);
        ArgumentNullException.ThrowIfNull(host);
        if (oldWeaponBars.Length < 2) throw new ArgumentException("Two weapon bars are required.", nameof(oldWeaponBars));
        if (state.Smoothies.Length < 9) throw new ArgumentException("Nine smoothie flags are required.", nameof(state));
        if (state.NetworkGame)
        {
            state.Smoothies[8] = false;
            state.Smoothies[5] = false;
        }
        else
        {
            state.StarShowVgaSpecialCode = (state.Smoothies[8] ? 1 : 0) +
                (state.Smoothies[5] ? 2 : 0);
        }

        for (var layer = 0; layer < 3; layer++)
            if (state.MapYPositions[layer] <= state.BackgroundWraps[layer])
                state.MapYPositions[layer] = state.BackgroundWrapTargets[layer];

        state.UpdatePlayersGone();
        if (state.MusicFade)
        {
            if (state.TemporaryVolume > 10)
            {
                state.TemporaryVolume--;
                host.SetVolume(state.TemporaryVolume, state.EffectsVolume);
            }
            else
            {
                state.MusicFade = false;
            }
        }

        if (!state.AllPlayersGone && state.LevelEnd > 0 && state.EndLevel)
        {
            host.PlaySong(9);
            state.MusicFade = false;
        }
        else if (!state.Playing && state.FirstGameOver)
        {
            host.PlaySong(state.LevelSong - 1);
        }

        host.DrawHud(oldWeaponBars);
        state.Screen = state.GameScreen;
        while (state.HasDueEvent)
        {
            host.ProcessEvent(state);
        }

        if (state.NetworkGame && state.ReallyEndLevel)
            return false;

        host.CheckSmoothies(state);
        if (state.AnySmoothies)
            state.Screen = state.SecondaryScreen;

        host.DrawBackgrounds(state, ref lastEnemyOnScreen);
        for (var shot = 0; shot < state.ShotAvailable.Length; shot++)
            if (state.ShotAvailable[shot])
                host.ProcessPlayerShot(state, shot);

        foreach (var player in state.Players)
        {
            player.LastXShotMove = player.X;
            player.LastYShotMove = player.Y;
        }

        var playerCount = state.TwoPlayerMode ? 2 : 1;
        for (var player = 0; player < playerCount; player++)
            if (state.Players[player].IsAlive && !state.EndLevel)
                host.CollidePlayer(state, player + 1);

        if (state.FirstGameOver)
            host.RunPlayerFunctions(state);
        host.DrawEnemyShots(state);

        if (state.Background3Over == 1)
            host.DrawBackground3(state);

        if (state.TopEnemyOver)
        {
            state.TemporaryMapXOffset = state.OldMapX3Offset;
            state.TemporaryBackgroundMove = state.Background3Move;
            host.DrawEnemy(state, 75);
        }

        if (state.SkyEnemyOverAll)
        {
            var lastEnemyCount = state.EnemyOnScreen;
            state.TemporaryMapXOffset = state.MapX2Offset;
            state.TemporaryBackgroundMove = 0;
            host.DrawEnemy(state, 25);
            if (state.EnemyOnScreen == lastEnemyCount && state.StopBackgroundNumber == 2)
                state.StopBackgroundNumber = 9;
        }

        host.UpdateExplosions(state);
        if (!state.PortConfigChange)
            state.PortConfigDone = true;

        if (!(state.Smoothies[1] && state.ProcessorType < 4) &&
            !(state.Smoothies[0] && state.ProcessorType == 3) &&
            state.Background2Over == 2)
            host.DrawBackground2(state, state.Wild && !state.Background2NotTransparent);

        host.DrawArmorWarning(state);
        if (state.RandomExplosions && host.NextRandomUInt32() % 10 == 1)
            host.SetupLargeExplosion(false, 20, (int)(host.NextRandomUInt32() % 280),
                (int)(host.NextRandomUInt32() % 180));

        host.PlayLevelSounds(state);
        if (state.ReturnActive && state.EnemyOnScreen == 0)
        {
            host.JumpEvent(state, ushort.MaxValue);
            state.ReturnActive = false;
        }

        host.DrawDebugAndTimer(state, buffer);
        if (host.HandleInput(state))
            return true;

        if (state.NetworkGame)
            host.UpdateNetwork(state);
        host.DrawSuperpixels(state);
        if (state.FilterActive)
            host.FilterScreen(state, state.LevelFilter, state.LevelBrightness);
        host.DrawBossBar(state);
        host.DrawInGameDisplays(state);
        state.Screen = state.ScreenSegment;
        host.ShowStars(state);
        host.UpdateEndState(state);
        host.HandleChat(state);
        return !state.ReallyEndLevel;
    }
}
