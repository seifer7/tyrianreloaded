/*
 * OpenTyrian: A modern cross-platform port of Tyrian
 * Copyright (C) 2007-2013 The OpenTyrian Development Team
 * Distributed under the GNU General Public License, version 2 or later.
 * See COPYING for details.
 */

namespace OpenTyrian.Core;

public sealed class LevelHudState
{
    public bool EndLevel { get; set; }
    public ushort TextErase { get; set; }
    public byte ShieldRechargeCost { get; set; }
    public uint Power { get; set; }
    public uint PowerAdd { get; set; }
    public uint LastPower { get; set; }
    public ushort MapX3Offset { get; set; }
    public ushort OldMapX3Offset { get; set; }
}

public interface ILevelHudHost
{
    void UseHudSurface();
    void EraseMessageBar();
    void FillRectangle(int x1, int y1, int x2, int y2, byte color);
    void Rectangle(int x1, int y1, int x2, int y2, byte color);
    void DrawShield();
}

public sealed class LevelHud(LegacyGameState game, EnemySystem enemies, PlayerCombat combat,
    SceneEffects effects, LevelHudState state, ILevelHudHost host)
{
    public void Draw(Span<uint> oldWeaponBars)
    {
        ArgumentNullException.ThrowIfNull(game);
        ArgumentNullException.ThrowIfNull(enemies);
        ArgumentNullException.ThrowIfNull(combat);
        ArgumentNullException.ThrowIfNull(effects);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(host);
        if (oldWeaponBars.Length < 2)
            throw new ArgumentException("Level HUD requires both prior weapon-bar values.", nameof(oldWeaponBars));

        if (state.EndLevel)
            return;

        host.UseHudSurface();
        if (state.TextErase > 0 && --state.TextErase == 0)
            host.EraseMessageBar();

        if (game.GalagaMode)
        {
            foreach (var player in game.Players)
                player.Shield = 0;

            if (game.Players[1].Lives == 0 || game.Players[1].Armor == 0)
            {
                game.TwoPlayerMode = false;
                combat.TwoPlayers = false;
            }

            if (game.Players[0].Cash >= unchecked((uint)enemies.GalagaLife))
            {
                effects.SoundQueue[6] = 11;
                effects.SoundQueue[7] = 9;
                if (game.Players[0].Lives < 11)
                    game.Players[0].Lives = unchecked((byte)(game.Players[0].Lives + 1));
                else
                    game.Players[0].Cash = unchecked(game.Players[0].Cash + 1000);

                enemies.GalagaLife = enemies.GalagaLife == 10000
                    ? 20000
                    : unchecked(enemies.GalagaLife + 25000);
            }
        }
        else if (game.TwoPlayerMode)
        {
            combat.ShieldWait = unchecked((byte)(combat.ShieldWait - 1));
            if (combat.ShieldWait == 0)
            {
                combat.ShieldWait = 15;
                foreach (var player in game.Players)
                {
                    if (player.Shield < player.ShieldMax && player.IsAlive)
                        player.Shield++;
                }
                host.DrawShield();
            }
        }
        else if (game.Players[0].IsAlive &&
                 game.Players[0].Shield < game.Players[0].ShieldMax &&
                 state.Power > state.ShieldRechargeCost)
        {
            combat.ShieldWait = unchecked((byte)(combat.ShieldWait - 1));
            if (combat.ShieldWait == 0)
            {
                combat.ShieldWait = 15;
                state.Power -= state.ShieldRechargeCost;
                game.Players[0].Shield++;
                if (game.Players[1].Shield < game.Players[0].ShieldMax)
                    game.Players[1].Shield++;
                host.DrawShield();
            }
        }

        for (var weapon = 0; weapon < 2; weapon++)
        {
            var owner = game.Players[game.TwoPlayerMode ? weapon : 0];
            var itemPower = owner.Items.Weapons[weapon].Power;
            if (oldWeaponBars[weapon] == itemPower)
                continue;

            oldWeaponBars[weapon] = itemPower;
            var x = game.TwoPlayerMode ? 286 : 289;
            var y = weapon == 0 ? (game.TwoPlayerMode ? 6 : 17) : (game.TwoPlayerMode ? 100 : 38);
            host.FillRectangle(x, y, x + 21, y + 2, 0);
            for (var segment = 1; segment <= itemPower; segment++)
            {
                host.Rectangle(x, y, x + 1, y + 2, unchecked((byte)(115 + segment)));
                x += 2;
            }
        }

        if (game.TwoPlayerMode || game.OnePlayerAction)
        {
            state.Power = 900;
        }
        else
        {
            state.Power = unchecked(state.Power + state.PowerAdd);
            if (state.Power > 900)
                state.Power = 900;

            var powerBar = state.Power / 10;
            if (powerBar != state.LastPower)
            {
                if (powerBar > state.LastPower)
                    host.FillRectangle(269, 102 - (int)powerBar, 276, 103 - (int)state.LastPower,
                        (byte)(113 + powerBar / 7));
                else
                    host.FillRectangle(269, 102 - (int)state.LastPower, 276, 103 - (int)powerBar, 0);

                state.LastPower = powerBar;
            }
        }

        state.OldMapX3Offset = state.MapX3Offset;
        enemies.EnemiesOnScreen = 0;
    }
}
