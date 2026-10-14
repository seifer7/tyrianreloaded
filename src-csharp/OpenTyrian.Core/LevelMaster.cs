/*
 * OpenTyrian: A modern cross-platform port of Tyrian
 * Copyright (C) 2007-2013 The OpenTyrian Development Team
 * Distributed under the GNU General Public License, version 2 or later.
 * See COPYING for details.
 */

namespace OpenTyrian.Core;

public static class LevelMaster
{
    public const int EventMaximum = 2500;
    public const int WeaponCount = 780;
    public const int PortCount = 42;
    public const int ArmorCount = 4;
    public const int PowerCount = 6;
    public const int EngineCount = 6;
    public const int OptionCount = 30;
    public const int ShipCount = 13;
    public const int ShieldCount = 10;
    public const int SpecialCount = 46;
    public const int EnemyCount = 850;

    public static ReadOnlySpan<byte> ShapeFiles =>
    [
        (byte)'2', (byte)'4', (byte)'7', (byte)'8', (byte)'A', (byte)'B', (byte)'C', (byte)'D',
        (byte)'E', (byte)'F', (byte)'G', (byte)'H', (byte)'I', (byte)'J', (byte)'K', (byte)'L',
        (byte)'M', (byte)'N', (byte)'O', (byte)'P', (byte)'Q', (byte)'R', (byte)'S', (byte)'T',
        (byte)'U', (byte)'5', (byte)'#', (byte)'V', (byte)'0', (byte)'@', (byte)'3', (byte)'^',
        (byte)'5', (byte)'9'
    ];
}
