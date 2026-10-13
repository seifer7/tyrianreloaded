/*
 * OpenTyrian: A modern cross-platform port of Tyrian
 * Copyright (C) 2007-2013 The OpenTyrian Development Team
 * Distributed under the GNU General Public License, version 2 or later.
 * See COPYING for details.
 */

namespace OpenTyrian.Core;

public sealed class LevelCombatStateInitializer(LegacyGameState game, PlayerCombat combat,
    PlayerMovement movement, PlayerWeapons weapons, PlayerSpecialWeapons specialWeapons,
    PlayerShots shots, EnemySystem enemies, SceneEffects effects, BackgroundRenderer backgrounds,
    LevelEvents level, NetworkRequests requests, Action drawOptions)
{
    public void Initialize(bool networkGame, Span<bool> buttons)
    {
        if (game.Players.Length != 2 || buttons.Length < 4)
            throw new ArgumentException("Level combat initialization requires two players and four gameplay buttons.");

        combat.Linked = false;
        movement.LinkGunDirection = (float)Math.PI;
        foreach (var player in game.Players) PlayerRules.CalculatePurpleBalls(player);

        weapons.PortConfigChange = false;
        weapons.ChargeWait = 5;
        weapons.ChargeLevel = 0;
        weapons.ChargeMaximum = 5;
        weapons.ChargeGraphic = 0;
        weapons.ChargeGraphicWait = 3;
        enemies.TotalEnemies = enemies.KilledEnemies = 0;
        specialWeapons.AstralDuration = 0;
        enemies.SuperArcadePowerUp = 1;
        requests.YourInGameMenuRequest = false;
        movement.ConstantLastX = -1;
        level.DamageRate = 2;
        foreach (var player in game.Players) player.ExplodingTicks = 0;

        if (networkGame)
            foreach (var enemy in enemies.Enemies) enemy.Availability = 1;
        foreach (var shot in enemies.EnemyShots) shot.Available = true;

        foreach (var shot in shots.Shots) shot.Reset();
        Array.Clear(shots.Availability);
        Array.Clear(shots.MultiPosition);
        Array.Fill(shots.Repeat, (byte)1);
        buttons[..4].Clear();
        Array.Clear(enemies.GlobalFlags);
        effects.ResetLevelState();
        Array.Clear(enemies.SpriteIds);
        foreach (var enemy in enemies.Enemies) enemy.ResetData();
        foreach (var code in weapons.SpecialCodeProgress) Array.Clear(code);
        Array.Clear(weapons.ExecutedSpecial);

        enemies.ZinglonDuration = 0;
        specialWeapons.ResetLevelState();
        movement.AttachmentMove = 0;
        movement.AttachmentLinked = true;
        movement.AttachmentReturn = false;
        game.EditShip1 = game.EditShip2 = false;
        backgrounds.ResetSmoothies();
        combat.LevelTimer = false;
        level.RandomExplosions = false;
        level.ReturnActive = false;
        enemies.GalagaShotFrequency = 0;

        if (game.GalagaMode) enemies.Difficulty = 2;
        enemies.GalagaLife = 10000;

        drawOptions();

        backgrounds.First.WrapAt = backgrounds.Second.WrapAt = 14;
        backgrounds.Third.WrapAt = 15;
        backgrounds.First.WrapTo = backgrounds.Second.WrapTo = 14;
        backgrounds.Third.WrapTo = 15;
    }
}
