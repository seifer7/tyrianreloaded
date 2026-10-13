/*
 * OpenTyrian: A modern cross-platform port of Tyrian
 * Copyright (C) 2007-2013 The OpenTyrian Development Team
 * Distributed under the GNU General Public License, version 2 or later.
 * See COPYING for details.
 */

using System.Globalization;
using System.Text;

namespace OpenTyrian.Core;

public interface IGameHudPresentation
{
    void Text(int x, int y, string text, byte hue, int brightness, bool shaded);
    int TextWidth(string text);
    void Icon(int sheet, int x, int y, int graphic, bool large);
    void Cube(int x, int y, byte hue, sbyte brightness);
}

public sealed class GameHud(IReadOnlyList<PlayerState> players, ItemDatabase items,
    PlayerCombat combat, LevelEvents level, PlayerPickups pickups)
{
    public void Draw(IGameHudPresentation presentation)
    {
        if (players.Count != 2) throw new InvalidOperationException("The game HUD requires two player states.");
        for (var i = 0; i < (combat.TwoPlayers && !combat.Galaga ? 2 : 1); i++)
            presentation.Text(30 + 200 * i, 175, players[i].Cash.ToString(CultureInfo.InvariantCulture), 2, 4, true);

        var special = players[0].Items.Special;
        if (special > 0)
        {
            if (special >= items.Specials.Count) throw new InvalidDataException("HUD special exceeds loaded item definitions.");
            presentation.Icon(10, 25, 1, items.Specials[special].ItemGraphic, true);
        }

        if (level.OnePlayerAction || combat.TwoPlayers)
        {
            for (var i = 0; i < (level.OnePlayerAction ? 1 : 2); i++)
            {
                var extra = unchecked((uint)(players[i].Lives - 1));
                var y = i == 0 && special > 0 ? 35 : 15;
                var x = i == 0 ? 30 : 270;
                if (extra >= 5)
                {
                    presentation.Icon(9, x, y, 285, false);
                    // Native prints the unsigned subtraction with a signed %d conversion.
                    presentation.Text(i == 0 ? 45 : 250, y + 3,
                        unchecked((int)extra).ToString(CultureInfo.InvariantCulture), 15, 1, true);
                }
                else
                {
                    for (var j = 0u; j < extra; j++)
                    {
                        presentation.Icon(9, x, y, 285, false);
                        x += i == 0 ? 12 : -12;
                    }
                }
                var name = pickups.NetworkGame ? pickups.GetName((byte)(i + 1)) : pickups.DefaultName((byte)(i + 1));
                presentation.Text(i == 0 ? 28 : 285 - presentation.TextWidth(name), y - 7, name, 2, 6, true);
            }
        }

        for (var i = 0; i < 2; i++)
        {
            var x = i == 0 ? 30 : 270;
            for (var j = players[i].Superbombs; j > 0; j--)
            {
                presentation.Icon(9, x, 160, 304, false);
                x = unchecked(x + (i == 0 ? 12 : -12));
            }
        }
        if (combat.Cheating) presentation.Text(90, 170, "Cheaters always prosper.", 3, 4, false);
    }

    public static void DrawCube(IGameHudPresentation presentation, ushort x, ushort y, byte hue, byte brightness)
        => presentation.Cube(x, y, hue, unchecked((sbyte)brightness));
}

public sealed class GameHudPresentation(IndexedSurface surface, FontRenderer font,
    SpriteSheet icons, SpriteSheet specials, SpriteTable options) : IGameHudPresentation
{
    public void Text(int x, int y, string text, byte hue, int brightness, bool shaded)
    {
        var bytes = Encoding.Latin1.GetBytes(text);
        if (shaded) font.ShadeText(surface, x, y, bytes, hue, brightness, TextShade.Full);
        else font.OutText(surface, x, y, bytes, hue, brightness);
    }

    public int TextWidth(string text) => font.TextWidth(Encoding.Latin1.GetBytes(text), Font.Small);

    public void Icon(int sheet, int x, int y, int graphic, bool large)
    {
        var sprites = sheet switch { 9 => icons, 10 => specials, _ => throw new ArgumentOutOfRangeException(nameof(sheet)) };
        if (large) sprites.Blit2x2(surface, x, y, graphic);
        else sprites.Blit(surface, x, y, graphic);
    }

    public void Cube(int x, int y, byte hue, sbyte brightness)
    {
        options.Blit(surface, x + 4, y + 4, 25, SpriteEffect.Darken);
        options.Blit(surface, x + 3, y + 3, 25, SpriteEffect.Darken);
        options.Blit(surface, x, y, 25, SpriteEffect.Hue, hue, brightness);
    }
}
