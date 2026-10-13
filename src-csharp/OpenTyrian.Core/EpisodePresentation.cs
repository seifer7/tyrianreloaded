/*
 * OpenTyrian: A modern cross-platform port of Tyrian
 * Copyright (C) 2007-2013 The OpenTyrian Development Team
 * Distributed under the GNU General Public License, version 2 or later.
 * See COPYING for details.
 */

namespace OpenTyrian.Core;

public interface IEpisodePresentationHost
{
    void FadeBlack(int steps);
    void FadeWhite(int steps);
    void Clear();
    void Present();
    void SelectPalette(int index);
}

public sealed class EpisodePresentation(
    Action<ReadOnlyMemory<byte>, bool> showPicture,
    Action<ReadOnlyMemory<byte>, bool> playMapTransition,
    Action<Stream, ReadOnlyMemory<byte>, bool> showWarning,
    Func<Stream, ReadOnlyMemory<byte>, Memory<byte>, bool> showCompletion,
    IEpisodePresentationHost host)
{
    public bool Handle(char command, Stream episode, ReadOnlyMemory<byte> section,
        Memory<byte> buffer, bool constantPlay, bool networkGame)
    {
        ArgumentNullException.ThrowIfNull(episode);
        ArgumentNullException.ThrowIfNull(showPicture);
        ArgumentNullException.ThrowIfNull(playMapTransition);
        ArgumentNullException.ThrowIfNull(showWarning);
        ArgumentNullException.ThrowIfNull(showCompletion);
        ArgumentNullException.ThrowIfNull(host);
        if (buffer.Length < 256)
            throw new ArgumentException("Episode presentation requires a 256-byte scratch buffer.", nameof(buffer));

        switch (command)
        {
            case 'P':
                showPicture(section, constantPlay);
                break;
            case 'U':
            case 'V':
            case 'R':
                playMapTransition(section, constantPlay);
                break;
            case 'C':
                if (!networkGame)
                    host.FadeBlack(10);
                host.Clear();
                host.Present();
                host.SelectPalette(7);
                break;
            case 'B':
                if (!networkGame)
                    host.FadeBlack(10);
                break;
            case 'F':
                if (!networkGame)
                {
                    host.FadeWhite(100);
                    host.FadeBlack(30);
                }
                host.Clear();
                host.Present();
                break;
            case 'Q':
                return showCompletion(episode, section, buffer);
            case 'W':
                showWarning(episode, section, constantPlay);
                break;
        }
        return false;
    }
}
