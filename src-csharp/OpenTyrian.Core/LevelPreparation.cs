/*
 * OpenTyrian: A modern cross-platform port of Tyrian
 * Copyright (C) 2007-2013 The OpenTyrian Development Team
 * Distributed under the GNU General Public License, version 2 or later.
 * See COPYING for details.
 */

namespace OpenTyrian.Core;

public sealed class LevelPreparationState
{
    public bool NormalBonusLevel { get; set; }
    public bool BonusLevel { get; set; }
    public bool PlayerEndLevel { get; set; }
    public bool DoNotSaveBackup { get; set; }
    public bool DemoFileOpen { get; set; }
    public bool PlayDemo { get; set; }
    public bool RecordDemo { get; set; }
    public byte NextLevel { get; set; }
}

public interface ILevelPreparationHost
{
    void SetMouseRelative(bool enabled);
    void ClearKeyboard();
    void FreeEnemySpriteSheet(int index);
    void SetDelaySpeed(ushort speed);
    void CloseDemoFile();
    void StopSong();
    void FadeBlack(int steps);
    void WaitNoInput(bool clearEvents, bool allowKeyboard, bool allowMouse);
    void EndLevelAnimation();
    void FadeSong();
    void LoadGame(int slot);
}

public sealed class LevelPreparation(LegacyGameState game, GameRuntimeSettings settings,
    LevelPreparationState state, ILevelPreparationHost host)
{
    public bool Prepare()
    {
        host.SetMouseRelative(false);
        if (game.GalagaMode)
            game.TwoPlayerMode = false;

        host.ClearKeyboard();
        for (var sheet = 0; sheet < 4; sheet++)
            host.FreeEnemySpriteSheet(sheet);

        if (settings.FastPlay != 0)
        {
            settings.RestoreLevelTransitionSpeed(host.SetDelaySpeed);
        }

        if (state.PlayDemo || state.RecordDemo)
        {
            if (state.DemoFileOpen)
            {
                host.CloseDemoFile();
                state.DemoFileOpen = false;
            }

            if (state.PlayDemo)
            {
                host.StopSong();
                host.FadeBlack(10);
                host.WaitNoInput(true, true, true);
            }
        }

        game.Difficulty = game.OldDifficulty;

        if (!state.PlayDemo)
        {
            if ((!PlayerRules.AllDead(game.Players[0], game.Players[1], game.TwoPlayerMode) ||
                 state.NormalBonusLevel || state.BonusLevel) && !state.PlayerEndLevel)
            {
                game.MainLevel = state.NextLevel;
                host.EndLevelAnimation();
                host.FadeSong();
            }
            else
            {
                host.FadeSong();
                host.FadeBlack(10);
                host.LoadGame(game.TwoPlayerMode ? 22 : 11);
                if (state.DoNotSaveBackup)
                {
                    game.SuperTyrian = false;
                    game.OnePlayerAction = false;
                    game.Players[0].Items.SuperArcadeMode = 0;
                }
                if (state.BonusLevel && !state.PlayerEndLevel)
                    game.MainLevel = state.NextLevel;
            }
        }

        state.DoNotSaveBackup = false;
        return !state.PlayDemo;
    }
}
