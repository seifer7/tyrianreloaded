/*
 * OpenTyrian: A modern cross-platform port of Tyrian
 * Copyright (C) 2007-2013 The OpenTyrian Development Team
 * Distributed under the GNU General Public License, version 2 or later.
 * See COPYING for details.
 */

namespace OpenTyrian.Core;

public readonly record struct EpisodeModeItemIds(
    byte Stalker21126,
    byte AtomicRailgun,
    byte VulcanCannon,
    byte AdvancedMr12,
    byte AdvancedIntegrityField);

public sealed class EpisodeModeState
{
    public bool ConstantDie { get; set; }
    public bool DoNotSaveBackup { get; set; }
}

public static class EpisodeModeConfiguration
{
    public static void Configure(LegacyGameState game, EpisodeModeState state,
        EpisodeModeItemIds itemIds, char command)
    {
        ArgumentNullException.ThrowIfNull(game);
        ArgumentNullException.ThrowIfNull(state);

        switch (command)
        {
            case 'g':
                game.GalagaMode = true;
                game.Players[1].Items = game.Players[0].Items.Copy();
                game.Players[1].Items.Weapons[(int)WeaponPort.Rear].Id = itemIds.VulcanCannon;
                Array.Clear(game.Players[1].Items.Sidekicks);
                break;
            case 'x':
                game.ExtraGame = true;
                break;
            case 'e':
                state.DoNotSaveBackup = true;
                state.ConstantDie = false;
                game.OnePlayerAction = true;
                game.SuperTyrian = true;
                game.TwoPlayerMode = false;
                game.Players[0].Cash = 0;
                game.Players[0].Items.Ship = itemIds.Stalker21126;
                game.Players[0].Items.Weapons[(int)WeaponPort.Front].Id = itemIds.AtomicRailgun;
                game.Players[0].Items.Weapons[(int)WeaponPort.Rear].Id = 0;
                Array.Clear(game.Players[0].Items.Sidekicks);
                game.Players[0].Items.Generator = itemIds.AdvancedMr12;
                game.Players[0].Items.Shield = itemIds.AdvancedIntegrityField;
                game.Players[0].Items.Special = 0;
                game.Players[0].Items.Weapons[(int)WeaponPort.Front].Power = 3;
                game.Players[0].Items.Weapons[(int)WeaponPort.Rear].Power = 1;
                break;
        }
    }
}
