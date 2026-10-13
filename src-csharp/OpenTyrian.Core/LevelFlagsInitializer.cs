/*
 * OpenTyrian: A modern cross-platform port of Tyrian
 * Copyright (C) 2007-2013 The OpenTyrian Development Team
 * Distributed under the GNU General Public License, version 2 or later.
 * See COPYING for details.
 */

namespace OpenTyrian.Core;

public sealed class LevelFlagsInitializer(InputState input, PlayerCombat combat, PlayerMovement movement,
    LevelEvents level, BackgroundRenderer backgrounds, SceneEffects effects, EnemySystem enemies,
    PlayerFrame playerFrame, PlayerPickups pickups, WarningBars warning, ArmorWarning armorWarning)
{
    public void Initialize()
    {
        input.NewKey = input.NewMouse = false;
        combat.LevelEnd = byte.MaxValue;
        movement.LevelEndWarp = -4;
        combat.LevelEndFxWait = 0;
        warning.Color = 120;
        warning.ColorChange = 1;
        warning.SoundDelay = 0;
        armorWarning.ShipDelay = 50;

        pickups.BonusLevel = false;
        level.ReadyToEndLevel = false;
        level.FirstGameOver = true;
        level.EventLocation = 1;
        level.Location = 0;
        backgrounds.First.Move = 1;
        backgrounds.Second.Move = 2;
        backgrounds.Third.Move = 3;
        effects.ExplosionMove = 2;
        level.EnemiesActive = true;
        Array.Clear(playerFrame.Buttons);
        level.StopBackgrounds = false;
        level.StopBackgroundNumber = 0;
        level.ThirdAtFirstX = false;
        level.ThirdAtFirstXB = false;
        level.ThirdOver = 0;
        level.SecondOver = 1;
        level.TopEnemyOver = false;
        level.SkyOverAll = false;
        level.SmallEnemyAdjust = false;
        level.StarsActive = true;
        enemies.ContinualDamage = false;
        level.EnemyFrequency = 96;
        level.QuitRequested = false;

        foreach (var bar in enemies.BossBars) bar.LinkNumber = 0;

        level.ForceEvents = false;
        enemies.Enemy254Jump = 0;
        level.FilterActive = true;
        backgrounds.FilterFade = true;
        backgrounds.FilterFadeStart = false;
        backgrounds.LevelFilter = -99;
        backgrounds.Brightness = -14;
        backgrounds.BrightnessChange = 1;
        level.SecondNotTransparent = false;
    }
}
