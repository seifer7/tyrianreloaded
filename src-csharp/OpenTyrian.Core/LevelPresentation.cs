/*
 * OpenTyrian: A modern cross-platform port of Tyrian
 * Copyright (C) 2007-2013 The OpenTyrian Development Team
 * Distributed under the GNU General Public License, version 2 or later.
 * See COPYING for details.
 */

namespace OpenTyrian.Core;

public sealed class LevelPresentation(IndexedSurface game, IndexedSurface primary, PlayerState player,
    GameRuntimeSettings settings, GameKeyboard keyboard, GameSetupSession setup, DelayTimer timer, Action present)
{
    public byte SpecialCode { get; set; }
    public bool PlayerEndLevel { get; set; }

    public void Show()
    {
        if (!PlayerEndLevel && !keyboard.SkipPresentation)
        {
            var light = SpecialCode == 2 && settings.ProcessorType >= 2;
            var sourcePitch = light ? primary.Pitch : game.Pitch;
            if (primary.Width < 264 || primary.Height < 184 || game.Width < 288 ||
                (long)sourcePitch * 183 + 288 > game.Pixels.Length)
                throw new InvalidOperationException("Level presentation requires a complete native 264-by-184 viewport and source.");
            if (settings.SmoothScroll)
            {
                timer.Wait();
                timer.SetDelay(unchecked((int)settings.FrameCountMax));
            }
            var source = game.Pixels.Span; var destination = primary.Pixels.Span;
            if (light)
            {
                var lightY = 172 - player.Y; var lightX = 281 - player.X;
                for (var row = 0; row < 184; row++)
                {
                    var y = 184 - row;
                    for (var column = 0; column < 264; column++)
                    {
                        var pixel = source[row * sourcePitch + 24 + column];
                        var distance = Math.Abs(lightX - (264 - column)) + lightY;
                        var value = lightY > y ? (pixel >> 2) & 3 :
                            distance < y ? pixel & 15 :
                            distance - y <= 5 ? ((pixel & 15) + 3 * (5 - (distance - y))) / 4 : (pixel & 15) >> 2;
                        destination[row * primary.Pitch + column] = (byte)((pixel & 240) | value);
                    }
                }
            }
            else
                for (var row = 0; row < 184; row++)
                {
                    var sourceRow = SpecialCode == 1 ? 183 - row : row;
                    source.Slice(sourceRow * game.Pitch + 24, 264).CopyTo(destination.Slice(row * primary.Pitch, 264));
                }
            present();
        }
        setup.QuitRequested = false;
        keyboard.SkipPresentation = false;
    }
}
