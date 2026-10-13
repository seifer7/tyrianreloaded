/*
 * OpenTyrian: A modern cross-platform port of Tyrian
 * Copyright (C) 2007-2013 The OpenTyrian Development Team
 * Distributed under the GNU General Public License, version 2 or later.
 * See COPYING for details.
 */

namespace OpenTyrian.Core;

public interface ILevelStatusDisplayHost
{
    void DrawTextWindow(ReadOnlyMemory<byte> text);
    void PlaySong(uint song);
    void DrawPortConfigButtons();
}

public sealed class LevelStatusDisplayHost(GameTextWindow textWindow, Action<uint> playSong,
    Action drawPortConfigButtons) : ILevelStatusDisplayHost
{
    public void DrawTextWindow(ReadOnlyMemory<byte> text) => textWindow.Show(text);
    public void PlaySong(uint song) => playSong(song);
    public void DrawPortConfigButtons() => drawPortConfigButtons();
}

public sealed class LevelStatusDisplayInitializer(LegacyGameState game, ItemDatabase items,
    PlayerCombat combat, LevelTimerDisplay timer, PlayerPickups pickups, HelpTextData text,
    ILevelStatusDisplayHost host)
{
    public void Initialize(byte levelSong)
    {
        if (game.Players.Length != 2) throw new InvalidOperationException("Level status initialization requires both player slots.");

        host.DrawTextWindow(text.MiscText[20]);
        combat.ShieldWait = 1;
        combat.ShieldT = unchecked((byte)(items.Shields[game.Players[0].Items.Shield].Tpwr * 20));
        foreach (var player in game.Players)
        {
            var shield = items.Shields[player.Items.Shield].Mpwr;
            player.Shield = shield;
            player.ShieldMax = (uint)shield * 2;
        }

        combat.DrawShield();
        combat.DrawArmor();

        foreach (var player in game.Players) player.Superbombs = 0;

        game.CubeMax = 0;
        timer.Flash = 0;
        timer.FlashChange = 1;
        pickups.DisplayTime = 0;

        host.PlaySong(unchecked((uint)levelSong - 1));
        host.DrawPortConfigButtons();
    }
}
