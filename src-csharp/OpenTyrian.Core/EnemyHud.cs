/*
 * OpenTyrian: A modern cross-platform port of Tyrian
 * Copyright (C) 2007-2013 The OpenTyrian Development Team
 * Distributed under the GNU General Public License, version 2 or later.
 * See COPYING for details.
 */

namespace OpenTyrian.Core;

public sealed partial class EnemySystem
{
    public void DrawBossBars(IndexedSurface surface, bool levelTimer)
        => DrawBossBars((x1, y1, x2, y2, color) => surface.FillRectangle(x1, y1, x2, y2, color), levelTimer);

    public void DrawBossBars(Action<int, int, int, int, byte> fill, bool levelTimer)
    {
        foreach (var bar in BossBars)
        {
            if (bar.LinkNumber == 0) continue;
            var armor = 256;
            foreach (var enemy in Enemies)
                if (enemy.Availability != 1 && enemy.LinkNumber == bar.LinkNumber && enemy.Armor < armor)
                    armor = enemy.Armor;
            if (armor > 255 || armor == 0) bar.LinkNumber = 0;
            else bar.Armor = armor == 255 ? (byte)254 : (byte)armor;
        }
        var count = BossBars.Count(bar => bar.LinkNumber != 0);
        if (count == 1 && BossBars[0].LinkNumber == 0)
        {
            BossBars[0].LinkNumber = BossBars[1].LinkNumber;
            BossBars[0].Armor = BossBars[1].Armor;
            BossBars[0].Color = BossBars[1].Color;
            BossBars[1].LinkNumber = 0;
        }
        for (var i = 0; i < count; i++)
        {
            var x = count == 2 ? (i == 0 ? 125 : 185) : levelTimer ? 250 : 155;
            var bar = BossBars[i];
            BeveledBar(fill, x - 25, 7, x + 25, 12, 115);
            BeveledBar(fill, x - bar.Armor / 10, 7, x + (bar.Armor + 5) / 10, 12, unchecked((byte)(118 + bar.Color)));
            if (bar.Color > 0) bar.Color--;
        }
    }

    private static void BeveledBar(Action<int, int, int, int, byte> fill, int x1, int y1, int x2, int y2, byte color)
    {
        fill(x1, y1, x2, y1, unchecked((byte)(color + 1)));
        fill(x1, y1 + 1, x2, y2 - 1, color);
        fill(x1, y2, x2, y2, unchecked((byte)(color - 1)));
    }
}
