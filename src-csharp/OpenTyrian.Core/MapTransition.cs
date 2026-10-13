/*
 * OpenTyrian: A modern cross-platform port of Tyrian
 * Copyright (C) 2007-2013 The OpenTyrian Development Team
 * Distributed under the GNU General Public License, version 2 or later.
 * See COPYING for details.
 */

namespace OpenTyrian.Core;

public interface IMapTransitionHost
{
    bool NewKey { get; }
    bool NetworkGame { get; }
    void LoadPicture(IndexedSurface screen, int number);
    void ServiceEvents(bool clear);
    void SetDelay(int ticks);
    void Present();
    void WaitDelay();
}

public static class MapTransition
{
    private const int Width = 320;
    private const int Height = 200;
    private const int FrameLength = Width * Height;

    public static void Play(ReadOnlySpan<byte> section, IndexedSurface screen,
        IndexedSurface background, Span<byte> picture, bool constantPlay, IMapTransitionHost host)
    {
        ArgumentNullException.ThrowIfNull(screen);
        ArgumentNullException.ThrowIfNull(background);
        ArgumentNullException.ThrowIfNull(host);
        if (constantPlay)
            return;
        if (screen.Width != Width || screen.Height != Height || background.Width != Width ||
            background.Height != Height || screen.Pitch != background.Pitch ||
            picture.Length < FrameLength || screen.Pixels.Length < FrameLength ||
            background.Pixels.Length < checked(background.Pitch * background.Height))
        {
            throw new ArgumentException("Map transitions require compatible 320x200 indexed surfaces and a full picture buffer.");
        }

        screen.Pixels.Span.CopyTo(background.Pixels.Span);
        var transition = section.Length > 1 ? section[1] : (byte)0;
        var pictureNumber = ParseAtoi(section.Length > 3 ? section[3..] : []);
        host.LoadPicture(screen, pictureNumber);
        screen.Pixels.Span[..FrameLength].CopyTo(picture);
        host.ServiceEvents(true);

        if (transition == (byte)'U')
        {
            for (var step = 0; step < Height; step++)
            {
                if (host.NewKey)
                    continue;

                host.SetDelay(1);
                var screenPixels = screen.Pixels.Span;
                var backgroundPixels = background.Pixels.Span;
                var pictureOffset = (Height - 1 - step) * Width;
                var backgroundOffset = 0;
                for (var row = 0; row < Height; row++)
                {
                    var screenOffset = row * screen.Pitch;
                    if (row <= step)
                    {
                        picture.Slice(pictureOffset, Width).CopyTo(screenPixels[screenOffset..]);
                        pictureOffset += Width;
                    }
                    else
                    {
                        backgroundPixels.Slice(backgroundOffset, screen.Pitch)
                            .CopyTo(screenPixels[screenOffset..]);
                        backgroundOffset += background.Pitch;
                    }
                }
                host.Present();
                if (host.NetworkGame)
                {
                    // The native routine reserves this point for network synchronization.
                }
                host.WaitDelay();
            }
        }
        else if (transition == (byte)'V')
        {
            for (var step = 0; step < Height; step++)
            {
                if (host.NewKey)
                    continue;

                host.SetDelay(1);
                var screenPixels = screen.Pixels.Span;
                var backgroundPixels = background.Pixels.Span;
                var pictureOffset = 0;
                for (var row = 0; row < Height - 1; row++)
                {
                    var screenOffset = row * screen.Pitch;
                    if (row <= Height - 1 - step)
                    {
                        backgroundPixels.Slice(row * background.Pitch, screen.Pitch)
                            .CopyTo(screenPixels[screenOffset..]);
                    }
                    else
                    {
                        picture.Slice(pictureOffset, Width).CopyTo(screenPixels[screenOffset..]);
                        pictureOffset += Width;
                    }
                }
                host.Present();
                if (host.NetworkGame)
                {
                    // The native routine reserves this point for network synchronization.
                }
                host.WaitDelay();
            }
        }
        else
        {
            for (var step = 0; step < Width - 1; step++)
            {
                if (host.NewKey)
                    continue;

                host.SetDelay(1);
                var screenPixels = screen.Pixels.Span;
                var backgroundPixels = background.Pixels.Span;
                for (var row = 0; row < Height; row++)
                {
                    var screenOffset = row * Width;
                    var oldWidth = Width - 1 - step;
                    backgroundPixels.Slice(row * background.Pitch + step, oldWidth)
                        .CopyTo(screenPixels[screenOffset..]);
                    screenOffset += Width - step;
                    var newWidth = Math.Min(step + 1, screenPixels.Length - screenOffset);
                    picture.Slice(row * Width, newWidth).CopyTo(screenPixels[screenOffset..]);
                }
                host.Present();
                if (host.NetworkGame)
                {
                    // The native routine reserves this point for network synchronization.
                }
                host.WaitDelay();
            }
        }

        picture[..FrameLength].CopyTo(screen.Pixels.Span);
    }

    private static int ParseAtoi(ReadOnlySpan<byte> text)
    {
        var index = 0;
        while (index < text.Length && text[index] is 9 or 10 or 11 or 12 or 13 or 32)
            index++;

        var negative = false;
        if (index < text.Length && text[index] is (byte)'+' or (byte)'-')
        {
            negative = text[index] == '-';
            index++;
        }

        long value = 0;
        var digits = 0;
        while (index < text.Length && text[index] is >= (byte)'0' and <= (byte)'9')
        {
            value = checked(value * 10 + text[index] - (byte)'0');
            digits++;
            index++;
        }

        if (digits == 0)
            return 0;

        if (negative)
            value = -value;
        return checked((int)value);
    }
}
