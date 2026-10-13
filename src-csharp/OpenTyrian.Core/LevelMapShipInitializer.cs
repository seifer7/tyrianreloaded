/*
 * OpenTyrian: A modern cross-platform port of Tyrian
 * Copyright (C) 2007-2013 The OpenTyrian Development Team
 * Distributed under the GNU General Public License, version 2 or later.
 * See COPYING for details.
 */

namespace OpenTyrian.Core;

public sealed class LevelMapShipInitializer(BackgroundRenderer backgrounds, LevelEvents level,
    IReadOnlyList<PlayerState> players, PlayerShipInfo shipInfo, PlayerShots shots, PlayerFrame playerFrame)
{
    public void Initialize()
    {
        if (players.Count != 2) throw new ArgumentException("Level initialization requires both player slots.", nameof(players));

        backgrounds.First.MapY = 292;
        backgrounds.Second.MapY = 592;
        backgrounds.Third.MapY = 592;
        backgrounds.First.MapOffset = checked(backgrounds.First.MapY * 14 - 1);
        backgrounds.Second.MapOffset = checked(backgrounds.Second.MapY * 14 - 1);
        backgrounds.Third.MapOffset = checked(backgrounds.Third.MapY * 15 - 1);
        backgrounds.First.X = backgrounds.Second.X = backgrounds.Third.X = 0;
        backgrounds.First.HorizontalTileOffset = backgrounds.Second.HorizontalTileOffset =
            backgrounds.Third.HorizontalTileOffset = 0;
        playerFrame.MapXOffset = 0;
        playerFrame.MapX3Offset = 0;
        backgrounds.SecondDelay = backgrounds.SecondDelayMax = 1;
        level.FirstDelay = level.FirstDelayMax = 1;
        level.MusicFade = false;
        backgrounds.First.Position = backgrounds.Second.Position = backgrounds.Third.Position = 0;
        shots.Power = 0;
        level.StarfieldSpeed = 1;

        shipInfo.Initialize();
        foreach (var player in players)
        {
            player.XVelocity = 0;
            player.YVelocity = 0;
            player.InvulnerableTicks = 100;
        }
    }
}
