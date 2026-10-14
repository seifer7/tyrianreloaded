/*
 * OpenTyrian: A modern cross-platform port of Tyrian
 * Copyright (C) 2007-2009 The OpenTyrian Development Team
 * Distributed under the GNU General Public License, version 2 or later.
 * See COPYING for details.
 */

namespace OpenTyrian.Core;

public enum GameDifficulty : byte
{
    Wimp,
    Easy,
    Normal,
    Hard,
    Impossible,
    Insanity,
    Suicide,
    Maniacal,
    Zinglon,
    Nortaneous,
    Ten
}

public enum KeySetting : byte
{
    Up,
    Down,
    Left,
    Right,
    Fire,
    ChangeFire,
    LeftSidekick,
    RightSidekick
}

public enum PlayerShotSlot : byte
{
    Front,
    Rear,
    LeftSidekick,
    RightSidekick,
    Miscellaneous,
    PlayerTwoCharge,
    PlayerOneSuperbomb,
    PlayerTwoSuperbomb,
    Special,
    NortSparks,
    SpecialTwo
}
