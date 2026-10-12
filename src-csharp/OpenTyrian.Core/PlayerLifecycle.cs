/*
 * OpenTyrian: A modern cross-platform port of Tyrian
 * Copyright (C) 2007-2009 The OpenTyrian Development Team
 * Distributed under the GNU General Public License, version 2 or later.
 * See COPYING for details.
 */

namespace OpenTyrian.Core;

public sealed partial class PlayerCombat
{
    public bool Linked { get; set; }
    public bool ConstantDie { get; set; }
    public ushort LevelEndEffectsWait { get; set; }

    public bool UpdateDeath(PlayerState player, byte playerNumber, byte episode, bool isNetworkGame,
        LevelEvents level, Span<byte> shotMultiPosition)
    {
        if (playerNumber is < 1 or > 2 || shotMultiPosition.Length < 2)
            throw new ArgumentOutOfRangeException(nameof(playerNumber));
        if (!player.IsAlive)
        {
            if (player.ExplodingTicks > 0)
            {
                player.ExplodingTicks--;
                if (LevelEndEffectsWait > 0) LevelEndEffectsWait--;
                else
                {
                    LevelEndEffectsWait = (ushort)(MersenneTwister.NextUInt32() % 6 + 3);
                    if (MersenneTwister.NextUInt32() % 3 == 1) _effects.SoundQueue[6] = 9;
                    else _effects.SoundQueue[5] = 11;
                }
                var x = unchecked((int)((uint)player.X + MersenneTwister.NextUInt32() % 32 - 16));
                var y = unchecked((int)((uint)player.Y + MersenneTwister.NextUInt32() % 32 - 16));
                _effects.SetupLarge(false, 0, unchecked((short)x), unchecked((short)(y + 7)));
                _effects.SetupLarge(false, 0, unchecked((short)player.X), unchecked((short)(player.Y + 7)));
                if (LevelEnd > 0) LevelEnd--;
            }
            else if (TwoPlayers || level.OnePlayerAction)
            {
                if (player.Lives > 1)
                {
                    player.Lives--;
                    level.ReallyEndLevel = false;
                    shotMultiPosition[playerNumber - 1] = 0;
                    PlayerRules.CalculatePurpleBalls(player);
                    Linked = false;
                    if (Galaga) TwoPlayers = false;
                    player.Y = 160; player.InvulnerableTicks = 100; player.IsAlive = true;
                    level.EndLevel = false;
                    player.Armor = Galaga || episode == 4 ? player.InitialArmor : player.InitialArmor / 2;
                    player.Shield = Galaga ? 0 : player.ShieldMax / 2;
                    DrawArmor(); DrawShield();
                    return true;
                }
                if (Galaga) TwoPlayers = false;
                if (level.AllPlayersGone && isNetworkGame) level.ReallyEndLevel = true;
            }
        }
        else if (ConstantDie && player.ExplodingTicks == 0)
        {
            player.Shield = 0;
            if (player.Armor > 0) player.Armor--;
            else
            {
                player.IsAlive = false; player.ExplodingTicks = 60; LevelEnd = 40;
            }
            WipeBars(); DrawArmor();
            if (_players[0].Lives < 11) _players[0].Lives++;
        }
        return false;
    }
}
