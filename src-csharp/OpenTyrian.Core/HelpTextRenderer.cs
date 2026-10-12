/*
 * OpenTyrian: A modern cross-platform port of Tyrian
 * Copyright (C) 2007-2009 The OpenTyrian Development Team
 * Distributed under the GNU General Public License, version 2 or later.
 * See COPYING for details.
 */

namespace OpenTyrian.Core;

public readonly record struct HelpTextLine(int Y, byte[] Text);

public sealed class HelpTextRenderer(FontRenderer fonts)
{
    public byte VerticalHeight { get; set; } = 7;
    public byte Color { get; set; } = 12;
    public byte Brightness { get; set; } = 1;
    public TextShade Shade { get; set; } = TextShade.Full;

    public void DrawBox(IndexedSurface surface, int x, int y, ReadOnlySpan<byte> message, uint width)
    {
        var lines = Layout(message, width, VerticalHeight);
        if (lines.Count == 0) return;
        foreach (var line in lines)
            fonts.ShadeText(surface, x, y + line.Y, line.Text, Color, Brightness, Shade);
        Color = 12;
        Shade = TextShade.Full;
    }

    public void DrawHelp(IndexedSurface surface, int x, int y, HelpTextData data, uint messageNumber, uint width)
    {
        if (messageNumber == 0 || messageNumber > data.Help.Length)
            throw new ArgumentOutOfRangeException(nameof(messageNumber));
        DrawBox(surface, x, y, data.Help[messageNumber - 1], width);
    }

    public static IReadOnlyList<HelpTextLine> Layout(ReadOnlySpan<byte> message, uint width, byte verticalHeight = 7)
    {
        var terminator = message.IndexOf((byte)0);
        if (terminator >= 0) message = message[..terminator];
        if (message.IsEmpty) return Array.Empty<HelpTextLine>();
        if (message.Length == 1 || message.Length > 255)
            throw new InvalidDataException("Help text length would cause native byte-counter wrapping or an infinite loop.");

        var lines = new List<HelpTextLine>();
        byte position = 1, end = 0;
        var finished = false;
        var y = 0;
        do
        {
            var start = unchecked((byte)(end + 1));
            do
            {
                end = position;
                do
                {
                    position = unchecked((byte)(position + 1));
                    if (position == message.Length)
                    {
                        finished = true;
                        if (unchecked((uint)(position - start)) < width)
                            end = unchecked((byte)(position + 1));
                    }
                } while (message[position - 1] != (byte)' ' && !finished);
            } while (unchecked((uint)(position - start)) <= width && !finished);

            // SDL_strlcpy's size includes the terminator, not just visible bytes.
            var capacity = Math.Min(unchecked((uint)(end - start + 1)), 256u);
            var suffix = message[(start - 1)..];
            var count = Math.Min(suffix.Length, checked((int)capacity) - 1);
            if (count < 0) throw new InvalidDataException("Help text would read an uninitialized native substring.");
            lines.Add(new HelpTextLine(y, suffix[..count].ToArray()));
            y += verticalHeight;
        } while (!finished);

        if (end != position + 1)
            lines.Add(new HelpTextLine(y, message[end..].ToArray()));
        return lines.AsReadOnly();
    }
}
