/*
 * OpenTyrian: A modern cross-platform port of Tyrian
 * Copyright (C) 2007-2013 The OpenTyrian Development Team
 * Distributed under the GNU General Public License, version 2 or later.
 * See COPYING for details.
 */

namespace OpenTyrian.Core;

public static class MusicMaster
{
    public const byte DefaultSongBuy = 2;
    public const byte SongLevelEnd = 9;
    public const byte SongGameOver = 10;
    public const byte SongMapView = 19;
    public const byte SongEndGame1 = 7;
    public const byte SongZanac = 31;
    public const byte SongTitle = 29;

    public static byte SongBuy { get; set; } = DefaultSongBuy;
    public static bool MusicFade { get; set; }

    public static IReadOnlyList<string> Titles { get; } = Array.AsReadOnly(new[]
    {
        "Asteroid Dance Part 2", "Asteroid Dance Part 1", "Buy/Sell Music", "CAMANIS", "CAMANISE",
        "Deli Shop Quartet", "Deli Shop Quartet No. 2", "Ending Number 1", "Ending Number 2", "End of Level",
        "Game Over Solo", "Gryphons of the West", "Somebody pick up the Gryphone", "Gyges, Will You Please Help Me?",
        "I speak Gygese", "Halloween Ramble", "Tunneling Trolls", "Tyrian, The Level", "The MusicMan", "The Navigator",
        "Come Back to Me, Savara", "Come Back again to Savara", "Space Journey 1", "Space Journey 2", "The final edge",
        "START5", "Parlance", "Torm - The Gathering", "TRANSON", "Tyrian: The Song", "ZANAC3", "ZANACS",
        "Return me to Savara", "High Score Table", "One Mustn't Fall", "Sarah's Song", "A Field for Mag",
        "Rock Garden", "Quest for Peace", "Composition in Q", "BEER"
    });
}
