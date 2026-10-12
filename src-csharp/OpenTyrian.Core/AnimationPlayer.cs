/*
 * OpenTyrian: A modern cross-platform port of Tyrian
 * Copyright (C) 2007-2009 The OpenTyrian Development Team
 * Distributed under the GNU General Public License, version 2 or later.
 * See COPYING for details.
 */

namespace OpenTyrian.Core;

public sealed class AnimationPlayer(IndexedSurface screen, PaletteSystem palette, InputState input,
    DelayTimer timer, Action present, Action keepAlive)
{
    public void PlayFile(string path, byte startingFrame, byte speed)
    {
        using var file = File.OpenRead(path);
        Play(file, startingFrame, speed, leaveOpen: true);
    }

    public void Play(Stream stream, byte startingFrame, byte speed, bool leaveOpen = false)
    {
        ArgumentNullException.ThrowIfNull(present);
        ArgumentNullException.ThrowIfNull(keepAlive);
        if (screen.Width != 320 || screen.Height != 200 || screen.Pitch != 320)
            throw new ArgumentException("Animation playback requires a contiguous 320x200 indexed screen.");
        using var animation = new AnimationFile(stream, leaveOpen);
        palette.SetPalette(animation.Palette.ToArray(), 0, 255);
        screen.Pixels.Span.Clear();
        present();
        for (var frame = (uint)startingFrame; frame < animation.RecordCount - 1; frame++)
        {
            timer.SetDelay(speed);
            var page = animation.FindPage(frame);
            if (page < 0) throw new InvalidDataException($"Animation frame {frame} has no page.");
            animation.LoadPage(page);
            if (animation.RenderFrame(frame, screen.Pixels) != 0)
                throw new InvalidDataException($"Animation frame {frame} cannot be decoded.");
            present();
            input.ServiceEvents(true);
            if (input.NewKey) break;
            keepAlive();
            timer.Wait();
        }
    }
}
