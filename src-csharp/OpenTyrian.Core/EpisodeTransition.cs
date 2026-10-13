/*
 * OpenTyrian: A modern cross-platform port of Tyrian
 * Copyright (C) 2007-2013 The OpenTyrian Development Team
 * Distributed under the GNU General Public License, version 2 or later.
 * See COPYING for details.
 */

namespace OpenTyrian.Core;

public interface IEpisodeTransitionHost
{
    void CheckHighScores();
    void Credits();
    void PlaySong(uint song);
    void Clear();
    void SelectPalette();
    void Text(int y, ReadOnlyMemory<byte> text);
    void Present();
    void FadeIn(int steps);
    void WipeKey();
    void KeepAlive();
    void Delay(uint milliseconds);
    bool AnyButton();
    void FadeBlack(int steps);
}

public sealed class EpisodeTransition(LegacyGameState game, EpisodeState episodes, ItemDatabase items,
    IReadOnlyList<byte[]> episodeNames, ReadOnlyMemory<byte> continueText, IEpisodeTransitionHost host)
{
    public bool NetworkGame { get; set; }
    public bool ConstantPlay { get; set; }

    public void Run(CancellationToken cancellation = default)
    {
        if (game.Episode != episodes.Number) throw new InvalidOperationException("Game and episode archive ownership disagree.");
        game.LastLevelName = "Completed"u8.ToArray();
        if (game.Episode == game.InitialEpisode && !game.GameHasRepeated && game.Episode != 4 && !NetworkGame && !ConstantPlay)
            host.CheckHighScores();
        episodes.GameHasRepeated = game.GameHasRepeated;
        var next = episodes.FindNext();
        game.GameHasRepeated = episodes.GameHasRepeated;
        if (episodes.JumpBackToFirst)
        {
            if (game.Episode > 2 && !ConstantPlay) host.Credits();
            if (MersenneTwister.NextUInt32() % 6 == 0)
            {
                var player = game.Players[0];
                player.Items.Ship = Known(KnownItem.ShipSuperCarrot);
                player.Items.Weapons[0].Id = Known(KnownItem.WeaponFrontBananaBlast);
                player.Items.Weapons[1].Id = Known(KnownItem.WeaponRearBananaBlast);
                player.Items.Weapons[0].Power = player.Items.Weapons[1].Power = 1;
                game.Players[1].Items.Weapons[1].Id = Known(KnownItem.WeaponRearBananaBlast);
                player.LastItems = player.Items.Copy();
            }
        }
        if (next != game.Episode) episodes.Initialize(checked((byte)next));
        game.Episode = episodes.Number;
        game.GameLoaded = true;
        game.MainLevel = game.SaveLevel = 1;
        host.PlaySong(26);
        host.Clear(); host.SelectPalette();
        if (game.Episode >= episodeNames.Count) throw new InvalidDataException("Episode title text is missing.");
        host.Text(130, episodeNames[game.Episode]); host.Text(185, continueText);
        host.Present(); host.FadeIn(15); host.WipeKey();
        if (!ConstantPlay)
        {
            do
            {
                cancellation.ThrowIfCancellationRequested();
                host.KeepAlive(); host.Delay(16);
            } while (!host.AnyButton());
        }
        host.FadeBlack(15);
    }

    private byte Known(KnownItem item) => checked((byte)items.KnownIndex(item));
}

public sealed class EpisodeTransitionHost(IndexedSurface surface, FontRenderer font, PaletteSystem palette,
    ReadOnlyMemory<PaletteColor> episodePalette, InputState input, DelayTimer timer,
    Action highScores, Action credits, Action<uint> playSong, Action present, Action keepAlive) : IEpisodeTransitionHost
{
    private readonly PaletteColor[] _colors = new PaletteColor[256];
    public void CheckHighScores() => highScores();
    public void Credits() => credits();
    public void PlaySong(uint song) => playSong(song);
    public void Clear() => surface.Pixels.Span.Clear();
    public void SelectPalette()
    {
        if (episodePalette.Length != 256) throw new InvalidDataException("Episode transition requires a 256-color palette.");
        episodePalette.Span.CopyTo(_colors);
    }
    public void Text(int y, ReadOnlyMemory<byte> text)
        => font.DrawString(surface, font.Center(text.Span, Font.Normal), y, text.Span, Font.Normal);
    public void Present() => present();
    public void FadeIn(int steps) => palette.FadePalette(_colors, steps, 0, 255, () => timer.SetDelay(1), present, timer.Wait);
    public void WipeKey() => input.ClearKeyboard();
    public void KeepAlive() => keepAlive();
    public void Delay(uint milliseconds) => input.Delay(milliseconds);
    public bool AnyButton() => input.AnyButton();
    public void FadeBlack(int steps) => palette.FadeBlack(steps, () => timer.SetDelay(1), present, timer.Wait);
}
