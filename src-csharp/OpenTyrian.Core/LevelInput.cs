/*
 * OpenTyrian: A modern cross-platform port of Tyrian
 * Copyright (C) 2007-2013 The OpenTyrian Development Team
 * Distributed under the GNU General Public License, version 2 or later.
 * See COPYING for details.
 */

namespace OpenTyrian.Core;

public interface ILevelInputHost
{
    bool SkipPresentation { get; set; }
    void DrawGameOver();
    void PlayGameOver();
    void SetVolume(ushort music, ushort effects);
    void PushJoysticks();
    void Keyboard(CancellationToken cancellation);
    void Pause(CancellationToken cancellation);
    void Setup(CancellationToken cancellation);
}

public sealed class LevelInput(IReadOnlyList<PlayerState> players, PlayerCombat combat, LevelEvents level,
    PlayerInput playerInput, InputState input, NetworkRequests requests, ILevelInputHost host)
{
    public bool NormalBonusLevel { get; set; }
    public bool BonusLevel { get; set; }
    public bool StoppedDemo { get; set; }

    public bool Handle(CancellationToken cancellation = default)
    {
        cancellation.ThrowIfCancellationRequested();
        if (!playerInput.ConstantPlay && !combat.ConstantDie && level.AllPlayersGone)
        {
            if (players[0].ExplodingTicks > 0 || players[1].ExplodingTicks > 0)
            {
                if (combat.Galaga) players[1].ExplodingTicks = 0;
                level.MusicFade = true;
            }
            else
            {
                if (playerInput.PlayDemo || NormalBonusLevel || BonusLevel) level.ReallyEndLevel = true;
                else host.DrawGameOver();
                if (level.FirstGameOver)
                {
                    if (!playerInput.PlayDemo)
                    {
                        host.PlayGameOver();
                        host.SetVolume(combat.MusicVolume, level.EffectsVolume);
                    }
                    level.FirstGameOver = false;
                }
                if (!playerInput.PlayDemo)
                {
                    host.PushJoysticks();
                    input.ServiceEvents(true);
                    if (input.NewKey || input.MousePressed.Any(pressed => pressed) || input.NewMouse)
                        level.ReallyEndLevel = true;
                }
                if (playerInput.NetworkGame) level.ReallyEndLevel = true;
            }
        }
        if (playerInput.PlayDemo)
        {
            host.PushJoysticks();
            input.ServiceEvents(false);
            if (input.NewKey || input.NewMouse) { level.ReallyEndLevel = true; StoppedDemo = true; }
        }
        else
        {
            input.ServiceEvents(false);
            if (input.NewKey)
            {
                host.SkipPresentation = false;
                host.Keyboard(cancellation);
                input.NewKey = false;
                if (host.SkipPresentation) return true;
            }
            if (requests.PausePressed || !input.WindowHasFocus)
            {
                requests.PausePressed = false;
                if (playerInput.NetworkGame) requests.Pause = true;
                else host.Pause(cancellation);
            }
            if (requests.InGameMenuPressed)
            {
                requests.InGameMenuPressed = false;
                if (playerInput.NetworkGame) requests.InGameMenu = true;
                else
                {
                    requests.YourInGameMenu = true;
                    host.Setup(cancellation);
                    host.SkipPresentation = true;
                }
            }
        }
        return false;
    }
}

public sealed class LevelInputHost(GameKeyboard keyboard, GamePause pause, GameSetupSession setup,
    GameSettingsStore settings, IndexedSurface primary, FontRenderer font, HelpTextData text,
    Action<uint> playSong, Action<ushort, ushort> setVolume, Action pushJoysticks) : ILevelInputHost
{
    public bool SkipPresentation { get => keyboard.SkipPresentation; set => keyboard.SkipPresentation = value; }
    public void DrawGameOver() => font.DrawString(primary, 120, 60, text.MiscText[21], Font.Large);
    public void PlayGameOver() => playSong(10);
    public void SetVolume(ushort music, ushort effects) => setVolume(music, effects);
    public void PushJoysticks() => pushJoysticks();
    public void Keyboard(CancellationToken cancellation) => keyboard.Run(cancellation);
    public void Pause(CancellationToken cancellation) => pause.Run(unchecked((byte)settings.Dos.MusicVolume),
        unchecked((byte)settings.Dos.EffectsVolume), keyboard.SuperPause, cancellation: cancellation);
    public void Setup(CancellationToken cancellation) => setup.Run(cancellation: cancellation);
}
