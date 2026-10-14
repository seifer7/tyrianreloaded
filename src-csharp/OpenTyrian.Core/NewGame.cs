/*
 * OpenTyrian: A modern cross-platform port of Tyrian
 * Copyright (C) 2007-2013 The OpenTyrian Development Team
 * Distributed under the GNU General Public License, version 2 or later.
 * See COPYING for details.
 */

using System.Text;

namespace OpenTyrian.Core;

public interface INewGameHost
{
    bool SelectGameplay();
    bool SelectEpisode();
    bool SelectDifficulty();
    void LoadPicture();
    void Clear();
    void Title(int y, ReadOnlyMemory<byte> text, Font font);
    void Ship(ushort graphic);
    void Text(int y, ReadOnlyMemory<byte> text);
    void Present();
    void FadeIn(int steps);
    void FadeBlack();
    void WaitNoInput(CancellationToken cancellation);
    void WaitInput(CancellationToken cancellation);
    void PlaySong(uint song);
    void Sample(byte sample);
    byte InitializeEpisode(byte episode);
}

public sealed class NewGame(LegacyGameState game, ItemDatabase items, HelpTextData text, InputState input, INewGameHost host)
{
    public bool RichMode { get; set; }
    public bool ConstantDie { get; set; }

    public bool Run()
    {
        if (host.SelectGameplay())
        {
            if (host.SelectEpisode() && host.SelectDifficulty()) game.GameLoaded = true;
            game.InitialDifficulty = game.Difficulty;
            if (game.OnePlayerAction)
            {
                game.Players[0].Cash = 0;
                game.Players[0].Items.Ship = Known(KnownItem.ShipStalker);
            }
            else if (game.TwoPlayerMode)
            {
                foreach (var player in game.Players) player.Cash = 0;
                game.Players[0].Items.Ship = Known(KnownItem.ShipSilverShip);
                game.Difficulty = unchecked((sbyte)(game.Difficulty + 1));
                game.InputDevices[0] = 1; game.InputDevices[1] = 2;
            }
            else if (RichMode) game.Players[0].Cash = 1000000;
            else if (game.GameLoaded)
            {
                ReadOnlySpan<uint> cash = [10000, 15000, 20000, 30000];
                if (game.Episode is < 1 or > 4) throw new InvalidDataException("A loaded new game requires episode 1..4.");
                game.Players[0].Cash = cash[game.Episode - 1];
            }
        }
        return game.GameLoaded;
    }

    public bool RunSuperArcade(int mode, CancellationToken cancellation = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(mode);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(mode, LegacyGameTables.ArcadeShipCount);
        game.Players[0].Items.Ship = Index(ItemType.Ship, LegacyGameTables.ArcadeShip(mode));
        if (host.SelectEpisode() && host.SelectDifficulty())
        {
            host.LoadPicture(); host.Clear();
            host.Title(30, text.SuperShips[0], Font.Large);
            host.Title(100, text.SuperShips[mode + 1], Font.Normal);
            var graphic = items.Ships[game.Players[0].Items.Ship].ShipGraphic;
            if (graphic != 1) host.Ship(graphic);
            host.Present(); host.FadeIn(50); host.WaitInput(cancellation);
            game.TwoPlayerMode = false; game.OnePlayerAction = true; game.SuperArcadeMode = (byte)(mode + 1);
            game.GameLoaded = true;
            game.Difficulty = unchecked((sbyte)(game.Difficulty + 1)); game.InitialDifficulty = game.Difficulty;
            game.Players[0].Cash = 0;
            game.Players[0].Items.Weapons[0].Id = Index(ItemType.WeaponFront, LegacyGameTables.ArcadeFrontWeapon(mode));
            game.Players[0].Items.Special = Index(ItemType.Special, LegacyGameTables.SuperArcadeStartingSpecial(mode));
            if (mode == 6)
                Array.Fill(game.Players[0].Items.Sidekicks, Known(KnownItem.SidekickCompanionShipQuicksilver));
            host.FadeBlack();
        }
        return game.GameLoaded;
    }

    public void RunSuperTyrian(CancellationToken cancellation = default)
    {
        game.InitialDifficulty = input.KeysActive[71] != 0 ? (sbyte)6 : (sbyte)8;
        host.Clear();
        host.Text(10, "Cheat codes have been disabled."u8.ToArray());
        host.Text(20, game.InitialDifficulty == 8
            ? "Difficulty level has been set to Lord of Game."u8.ToArray()
            : "Difficulty level has been set to Suicide."u8.ToArray());
        host.Text(30, "It is imperative that you discover the special codes."u8.ToArray());
        if (game.InitialDifficulty == 8)
            host.Text(40, "(Next time, for an easier challenge hold down SCROLL LOCK.)"u8.ToArray());
        host.Text(60, "Prepare to play..."u8.ToArray());
        var title = CString(text.MiscTextB[4]) + " " + CString(text.PlanetNames[0]);
        var bytes = Encoding.Latin1.GetBytes(title);
        host.Title(110, bytes.AsMemory(0, Math.Min(bytes.Length, 26)), Font.Large);
        host.PlaySong(16); host.Sample(35);
        host.Present(); host.FadeIn(10);
        host.WaitNoInput(cancellation); host.WaitInput(cancellation);
        game.Episode = host.InitializeEpisode(1);
        ConstantDie = false;
        game.SuperTyrian = game.OnePlayerAction = game.GameLoaded = true;
        game.Difficulty = game.InitialDifficulty;
        game.Players[0].Cash = 0;
        game.Players[0].Items.Ship = Known(KnownItem.ShipStalker21126);
        game.Players[0].Items.Weapons[0].Id = Known(KnownItem.WeaponFrontAtomicRailgun);
        host.FadeBlack();
    }

    private byte Known(KnownItem item) => checked((byte)items.KnownIndex(item));
    private byte Index(ItemType type, int id) => items.TryFind(type, id, out var index) && index <= byte.MaxValue
        ? (byte)index : throw new InvalidDataException($"New-game {type} item {id} is missing or exceeds byte indices.");
    private static string CString(byte[] value)
    {
        var end = value.AsSpan().IndexOf((byte)0);
        return Encoding.Latin1.GetString(end < 0 ? value : value.AsSpan(0, end));
    }
}

public sealed class NewGameHost(LegacyGameState game, SelectionMenus menus, SelectionMenuState selection, HelpTextData text,
    IReadOnlyList<bool> episodes, IndexedSurface primary, FontRenderer font, SpriteSheet ships, PaletteSystem palette,
    ReadOnlyMemory<PaletteColor> colors, InputState input, DelayTimer timer, Action loadPicture,
    Action present, Action<uint> playSong, Action<byte> sample, Func<byte, byte> initializeEpisode) : INewGameHost
{
    public bool SelectGameplay()
    {
        selection.OnePlayerAction = game.OnePlayerAction; selection.TwoPlayerMode = game.TwoPlayerMode;
        var result = menus.SelectGameplay(Labels(text.GameplayNames));
        game.OnePlayerAction = selection.OnePlayerAction; game.TwoPlayerMode = selection.TwoPlayerMode;
        return result;
    }
    public bool SelectEpisode()
    {
        selection.Episode = game.Episode; selection.InitialEpisode = game.InitialEpisode;
        var result = menus.SelectEpisode(Labels(text.EpisodeNames), episodes);
        game.Episode = checked((byte)selection.Episode); game.InitialEpisode = checked((byte)selection.InitialEpisode);
        return result;
    }
    public bool SelectDifficulty()
    {
        selection.Difficulty = game.Difficulty;
        var result = menus.SelectDifficulty(Labels(text.DifficultyNames));
        game.Difficulty = checked((sbyte)selection.Difficulty);
        return result;
    }
    public void LoadPicture() => loadPicture();
    public void Clear() => primary.Pixels.Span.Clear();
    public void Title(int y, ReadOnlyMemory<byte> text, Font selected) => font.DrawString(primary, font.Center(text.Span, selected), y, text.Span, selected);
    public void Ship(ushort graphic) => ships.Blit2x2(primary, 148, 70, graphic);
    public void Text(int y, ReadOnlyMemory<byte> text) => font.OutText(primary, 10, y, text.Span, 15, 4);
    public void Present() => present();
    public void FadeIn(int steps) => palette.FadePalette(colors.Span, steps, 0, 255, () => timer.SetDelay(1), present, timer.Wait);
    public void FadeBlack() => palette.FadeBlack(10, () => timer.SetDelay(1), present, timer.Wait);
    public void WaitNoInput(CancellationToken cancellation) => input.WaitNoInput(true, true, true, cancellation);
    public void WaitInput(CancellationToken cancellation) => input.WaitInput(true, true, true, cancellation);
    public void PlaySong(uint song) => playSong(song);
    public void Sample(byte number) => sample(number);
    public byte InitializeEpisode(byte episode) => initializeEpisode(episode);
    private static ReadOnlyMemory<byte>[] Labels(byte[][] labels) => labels.Select(label => (ReadOnlyMemory<byte>)label).ToArray();
}
