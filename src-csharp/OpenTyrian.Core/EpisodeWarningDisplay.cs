/*
 * OpenTyrian: A modern cross-platform port of Tyrian
 * Copyright (C) 2007-2013 The OpenTyrian Development Team
 * Distributed under the GNU General Public License, version 2 or later.
 * See COPYING for details.
 */

namespace OpenTyrian.Core;

public sealed class EpisodeWarningState
{
    public bool DisplayWarning { get; set; }
    public ReadOnlyMemory<byte>[] Lines { get; set; } = [];
}

public sealed class EpisodeWarningDisplay(GameRuntimeSettings settings, WarningBars warning,
    TextGlow glow, InputState input, EpisodeWarningState state,
    Action wipeKey, Action<int> setDelay2,
    Action<bool> setWarningEnabled, IWarningTextPresenter display)
{
    public void Show(Stream episode, ReadOnlySpan<byte> section, bool constantPlay,
        CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(episode);
        if (constantPlay || input.EscapePressed)
            return;
        if (!episode.CanRead)
            throw new ArgumentException("Episode warning input must be readable.", nameof(episode));

        wipeKey();
        warning.Color = 14 * 16 + 5;
        warning.ColorChange = 1;
        warning.SoundDelay = 0;
        state.DisplayWarning = section.Length > 2 && section[2] == (byte)'y';
        setWarningEnabled(state.DisplayWarning);

        var signedFrameCount = ParseAtoi(section.Length > 4 ? section[4..] : []);
        var frameCount = unchecked((ushort)signedFrameCount);
        var warningRed = frameCount / 10 != 0;
        settings.SetFrameCountMax((uint)(frameCount % 10));
        glow.WarningRed = warningRed;
        setDelay2(6);

        var buffer = new byte[256];
        var lines = new List<ReadOnlyMemory<byte>>(10);
        while (true)
        {
            Array.Clear(buffer);
            EncryptedText.ReadPascalString(episode, buffer);
            var end = Array.IndexOf(buffer, (byte)0);
            var length = end < 0 ? buffer.Length : end;
            if (buffer[0] != (byte)'#')
            {
                if (lines.Count == 10)
                    throw new InvalidDataException("Episode warning exceeds the native ten-line buffer.");
                if (length > 60)
                    throw new InvalidDataException("Episode warning line exceeds the native 60-byte buffer.");
                lines.Add(buffer.AsMemory(0, length).ToArray());
            }
            else
            {
                break;
            }
        }

        state.Lines = lines.ToArray();
        display.Run(state.Lines, cancellation);
        input.NewKey = false;
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
        return checked((int)(negative ? -value : value));
    }
}
