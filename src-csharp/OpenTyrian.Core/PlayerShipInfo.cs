/*
 * OpenTyrian: A modern cross-platform port of Tyrian
 * Copyright (C) 2007-2013 The OpenTyrian Development Team
 * Distributed under the GNU General Public License, version 2 or later.
 * See COPYING for details.
 */

namespace OpenTyrian.Core;

public sealed class PlayerShipInfo(IReadOnlyList<PlayerState> players, ItemDatabase items, SpriteSheet normal,
    SpriteSheet extra, ReadOnlyMemory<byte> extraShips, ushort[] graphics)
{
    public SpriteSheet[] Sheets { get; } = [normal, normal];
    public byte PowerAdd { get; private set; }
    private static readonly ushort[] ExtraGraphics = [233, 157, 195, 271, 81, 0, 119, 5, 43, 81, 119, 157, 195, 233, 271];

    public void Initialize()
    {
        if (players.Count != 2 || graphics.Length != 2) throw new ArgumentException("Ship initialization requires both players and graphics.");
        Sheets[0] = Sheets[1] = normal;
        PowerAdd = items.Generators[players[0].Items.Generator].Power;
        for (var i = 0; i < 2; i++)
        {
            var player = players[i];
            var custom = player.Items.Ship > 90;
            if (custom)
            {
                graphics[i] = ExtraGraphic((ushort)(player.Items.Ship - 90), ref Sheets[i]);
                var offset = unchecked((byte)((player.Items.Ship - 91) * 15));
                if (offset + 7 >= extraShips.Length) throw new InvalidDataException("Custom ship armor record is truncated.");
                player.Armor = extraShips.Span[offset + 7];
            }
            else if (i == 0)
            {
                graphics[i] = items.Ships[player.Items.Ship].ShipGraphic;
                player.Armor = items.Ships[player.Items.Ship].Dmg;
            }
            else { graphics[i] = 0; player.Armor = 10; }
            player.InitialArmor = player.Armor;
            var animation = custom ? 2 : items.Ships[player.Items.Ship].Ani;
            player.ShotHitAreaX = animation == 0 ? 12u : 11u;
            player.ShotHitAreaY = animation == 0 ? 10u : 14u;
        }
    }

    public ushort ExtraGraphic(ushort ship, ref SpriteSheet sheet)
    {
        var offset = (ship - 1) * 15;
        if (ship == 0 || offset >= extraShips.Length) throw new InvalidDataException("Custom ship graphics record is missing.");
        var selection = extraShips.Span[offset];
        if (selection is < 1 or > 15) throw new InvalidDataException("Custom ship graphics selector must be between 1 and 15.");
        if (selection > 7) sheet = extra;
        return ExtraGraphics[selection - 1];
    }

    public void Draw(int player, IndexedSurface surface, int x, int y, int graphic, bool large, SpriteEffect effect)
    {
        if (player is < 0 or > 1) throw new ArgumentOutOfRangeException(nameof(player));
        if (large) Sheets[player].Blit2x2(surface, x, y, graphic, effect);
        else Sheets[player].Blit(surface, x, y, graphic, effect);
    }
}
