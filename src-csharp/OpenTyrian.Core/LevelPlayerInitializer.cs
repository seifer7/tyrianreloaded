/*
 * OpenTyrian: A modern cross-platform port of Tyrian
 * Copyright (C) 2007-2013 The OpenTyrian Development Team
 * Distributed under the GNU General Public License, version 2 or later.
 * See COPYING for details.
 */

namespace OpenTyrian.Core;

public interface ILevelPlayerInitializationHost
{
    void LoadPicture(int number, bool preservePalette);
    void DrawOptions();
    void DrawLevelName(int x, int y, ReadOnlyMemory<byte> name, byte hue, int brightness);
    void Present();
    void CorrectGamma(byte correction);
    void FadePalette(int steps, int first, int last);
    void LoadExplosionShapes(char suffix);
}

public sealed class LevelPlayerInitializer(LegacyGameState game, ILevelPlayerInitializationHost host)
{
    public void Initialize(byte gammaCorrection, SpriteSheet explosionSprites)
    {
        foreach (var player in game.Players) player.IsAlive = true;
        game.OldDifficulty = game.Difficulty;
        if (game.Episode == 4) game.Difficulty = unchecked((sbyte)(game.Difficulty - 1));
        if (game.Difficulty < 1) game.Difficulty = 1;
        game.Players[0].X = 100; game.Players[0].Y = 180;
        game.Players[1].X = 190; game.Players[1].Y = 180;
        foreach (var player in game.Players)
        {
            if (player.OldX.Length != player.OldY.Length)
                throw new InvalidDataException("Player history arrays must have matching lengths.");
            for (var index = 0; index < player.OldX.Length; index++)
            {
                player.OldX[index] = player.X - (19 - index);
                player.OldY[index] = player.Y - 18;
            }
            player.LastXShotMove = player.X;
            player.LastYShotMove = player.Y;
        }

        host.LoadPicture(game.TwoPlayerMode ? 6 : 3, false);
        host.DrawOptions();
        host.DrawLevelName(268, game.TwoPlayerMode ? 76 : 118, game.LevelName, 12, 4);
        host.Present();
        host.CorrectGamma(gammaCorrection);
        host.FadePalette(50, 0, 255);
        if (explosionSprites.Size == 0) host.LoadExplosionShapes('6');
    }
}

public sealed class LevelPlayerInitializationHost(ReadOnlyMemory<PaletteColor> sourceColors,
    PaletteSystem palette, IndexedSurface surface, FontRenderer font, Action<int, bool> loadPicture,
    Action drawOptions, Action present, Action<char> loadExplosionShapes,
    Action fadeDelay, Action fadePresent, Action fadeWait) : ILevelPlayerInitializationHost
{
    private readonly PaletteColor[] _colors = new PaletteColor[PaletteSystem.ColorCount];
    private bool _gammaApplied;

    public void LoadPicture(int number, bool preservePalette) => loadPicture(number, preservePalette);
    public void DrawOptions() => drawOptions();
    public void DrawLevelName(int x, int y, ReadOnlyMemory<byte> name, byte hue, int brightness) =>
        font.OutText(surface, x, y, name.Span, hue, brightness);
    public void Present() => present();
    public void CorrectGamma(byte correction)
    {
        if (sourceColors.Length != PaletteSystem.ColorCount)
            throw new InvalidDataException("Level palette must contain exactly 256 colors.");
        sourceColors.Span.CopyTo(_colors);
        PaletteSystem.CorrectGamma(_colors, correction);
        palette.SetPalette(_colors, 0, 255);
        _gammaApplied = true;
    }
    public void FadePalette(int steps, int first, int last)
    {
        if (sourceColors.Length != PaletteSystem.ColorCount)
            throw new InvalidDataException("Level palette must contain exactly 256 colors.");
        if (!_gammaApplied)
            throw new InvalidOperationException("Level gamma must be applied before fading the palette.");
        palette.FadePalette(_colors, steps, first, last, fadeDelay, fadePresent, fadeWait);
    }
    public void LoadExplosionShapes(char suffix) => loadExplosionShapes(suffix);
}
