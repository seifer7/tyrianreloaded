/*
 * OpenTyrian: A modern cross-platform port of Tyrian
 * Copyright (C) 2007-2013 The OpenTyrian Development Team
 * Distributed under the GNU General Public License, version 2 or later.
 * See COPYING for details.
 */

using System.Globalization;
using System.Text;

namespace OpenTyrian.Core;

public interface IHelpSystemHost
{
    void EnsureCursor();
    void PlaySong();
    void LoadBackground();
    void RestoreBackground();
    void ClearFooter();
    int TextWidth(ReadOnlyMemory<byte> text);
    void Header(int y, ReadOnlyMemory<byte> text, Font font, sbyte brightness);
    void Footer(int x, ReadOnlyMemory<byte> text, FontAlignment alignment);
    void TextStyle(byte brightness, byte height);
    void Box(int y, uint number);
    void Cursor(byte shape);
    void FadeIn();
    void PresentCursor();
    void PushJoysticks();
    void Sample(byte number);
    void FadeBlack();
}

public sealed class HelpSystem(HelpTextData text, InputState input, IHelpSystemHost host)
{
    private static readonly byte[] TopicStarts = [0, 1, 2, 3, 7, 255];

    public void Run(byte startTopic = 1, CancellationToken cancellation = default)
    {
        if (startTopic is < 1 or > 6)
            throw new ArgumentOutOfRangeException(nameof(startTopic), "Help topic must be between one and six.");
        host.EnsureCursor();
        var topic = startTopic; var restart = true; var selected = 0; var widths = new int[5];
        while (true)
        {
            cancellation.ThrowIfCancellationRequested();
            if (restart) { host.PlaySong(); host.LoadBackground(); }
            if (topic > 1)
            {
                if (!ShowPages(ref topic, ref restart, cancellation)) return;
                selected = topic - 1; topic = 1;
                continue;
            }
            host.RestoreBackground(); host.Header(30, text.TopicNames[0], Font.Large, -3);
            for (var i = 0; i < 5; i++)
            {
                widths[i] = host.TextWidth(text.TopicNames[i + 1]);
                host.Header(60 + 20 * i, text.TopicNames[i + 1], Font.Normal, (sbyte)(i == selected ? -1 : -3));
            }
            host.Cursor(0);
            if (restart) { host.FadeIn(); restart = false; }
            input.ServiceEvents(true); host.PresentCursor();
            bool moved;
            do
            {
                cancellation.ThrowIfCancellationRequested();
                input.Delay(16);
                var oldX = unchecked((ushort)input.MouseX); var oldY = unchecked((ushort)input.MouseY);
                host.PushJoysticks(); input.ServiceEvents(false);
                moved = input.MouseX != oldX || input.MouseY != oldY;
            } while (!input.NewKey && !input.NewMouse && !moved);
            var action = false; var done = false;
            if (moved || input.NewMouse)
            {
                for (var i = 0; i < 5; i++)
                {
                    var x = 160 - widths[i] / 2; var y = 60 + 20 * i;
                    if (input.MouseX < x || input.MouseX >= x + widths[i] || input.MouseY < y || input.MouseY >= y + 13)
                        continue;
                    if (selected != i) { host.Sample(28); selected = i; }
                    if (input.NewMouse && input.LastMouseButton == 1 &&
                        input.LastMouseX >= x && input.LastMouseX < x + widths[i] &&
                        input.LastMouseY >= y && input.LastMouseY < y + 13) action = true;
                    break;
                }
            }
            if (input.NewMouse)
            {
                if (input.LastMouseButton == 3) { host.Sample(16); done = true; }
            }
            else if (input.NewKey)
            {
                switch (input.LastScancode)
                {
                    case 82: host.Sample(28); selected = selected == 0 ? 4 : selected - 1; break;
                    case 81: host.Sample(28); selected = selected == 4 ? 0 : selected + 1; break;
                    case 44 or 40 or 88: action = true; break;
                    case 41: host.Sample(16); done = true; break;
                }
            }
            if (action) { host.Sample(8); topic = (byte)(selected + 2); if (selected == 4) done = true; }
            if (done) { host.FadeBlack(); return; }
        }
    }

    public bool ShowPages(ref byte topic, ref bool restart, CancellationToken cancellation = default)
    {
        if (topic is < 1 or > 6)
            throw new ArgumentOutOfRangeException(nameof(topic), "Help topic must be between one and six.");
        var page = TopicStarts[topic - 1];
        while (true)
        {
            cancellation.ThrowIfCancellationRequested();
            if (page == 0) { topic = 1; return true; }
            if (page > 8) { topic = 5; return true; }
            for (var i = 0; i < TopicStarts.Length && TopicStarts[i] <= page; i++) topic = (byte)(i + 1);
            DrawPage(page, topic);
            if (restart) { host.FadeIn(); restart = false; }
            do
            {
                cancellation.ThrowIfCancellationRequested();
                host.Cursor(input.MouseX < 160 ? (byte)3 : (byte)4);
                input.ServiceEvents(true); host.PresentCursor(); input.Delay(16);
                host.PushJoysticks(); input.ServiceEvents(false);
            } while (!input.NewKey && !input.NewMouse);
            var done = false;
            if (input.NewMouse)
            {
                if (input.LastMouseButton == 1) { host.Sample(28); page = unchecked((byte)(page + (input.MouseX < 160 ? -1 : 1))); }
                else if (input.LastMouseButton == 3) { host.Sample(16); done = true; }
            }
            else
            {
                switch (input.LastScancode)
                {
                    case 80: host.Sample(28); page--; break;
                    case 79 or 44 or 40 or 88: host.Sample(28); page++; break;
                    case 41: host.Sample(16); done = true; break;
                }
            }
            if (done) { host.FadeBlack(); return false; }
        }
    }

    private void DrawPage(byte page, byte topic)
    {
        host.RestoreBackground(); host.ClearFooter();
        host.Header(1, text.TopicNames[topic - 1], Font.Normal, -3);
        host.Footer(10, Format(text.MiscText[24], $" {page - TopicStarts[topic - 1] + 1}"), FontAlignment.Left);
        host.Footer(310, Format(text.MiscText[25], $" {page} of 8"), FontAlignment.Right);
        host.TextStyle(3, 8);
        switch (page)
        {
            case 1: host.Box(20, 2); host.Box(50, 5); host.Box(80, 21); host.Box(110, 1); host.Box(140, 28); break;
            case 2: host.Box(20, 1); host.Box(60, 2); host.Box(100, 21); host.Box(140, 28); break;
            case 3: host.Box(20, 5); host.Box(70, 6); host.Box(110, 7); break;
            case 4: host.Box(20, 8); host.Box(55, 9); host.Box(87, 10); host.Box(120, 11); host.Box(170, 13); break;
            case 5: host.Box(20, 14); host.Box(80, 15); host.Box(120, 16); break;
            case 6: host.Box(20, 17); host.Box(40, 18); host.Box(130, 20); break;
            case 7: host.Box(20, 21); host.Box(70, 22); host.Box(110, 23); host.Box(140, 24); break;
            case 8: host.Box(20, 25); host.Box(60, 26); host.Box(100, 27); host.Box(140, 28); host.Box(170, 29); break;
        }
        host.TextStyle(1, 7);
    }

    private static byte[] Format(byte[] prefix, FormattableString suffix)
    {
        var bytes = Encoding.Latin1.GetBytes(Encoding.Latin1.GetString(prefix) + suffix.ToString(CultureInfo.InvariantCulture));
        return bytes.Length > 127 ? bytes[..127] : bytes;
    }
}

public sealed class HelpSystemHost(IndexedSurface primary, IndexedSurface background, FontRenderer font,
    HelpTextRenderer help, HelpTextData text, SpriteSheet sprites, MouseCursor cursor, PaletteSystem palette,
    ReadOnlyMemory<PaletteColor> colors, DelayTimer timer, Action loadCursor, Action loadPicture, Action<uint> song,
    Action present, Action pushJoysticks, Action<byte> sample) : IHelpSystemHost
{
    public void EnsureCursor() { if (sprites.Size == 0) loadCursor(); }
    public void PlaySong() => song(19);
    public void LoadBackground() => loadPicture();
    public void RestoreBackground()
    {
        if (primary.Width != background.Width || primary.Height != background.Height || primary.Pitch != background.Pitch)
            throw new InvalidDataException("Help system requires identical saved and primary surface geometry.");
        background.Pixels.Span.CopyTo(primary.Pixels.Span);
    }
    public void ClearFooter() => primary.FillRectangleSize(0, 192, 320, 8, 0);
    public int TextWidth(ReadOnlyMemory<byte> text) => font.TextWidth(text.Span, Font.Normal);
    public void Header(int y, ReadOnlyMemory<byte> text, Font selected, sbyte brightness) =>
        font.DrawShadow(primary, 160, y, text.Span, selected, FontAlignment.Centered, 15, brightness, false, 2);
    public void Footer(int x, ReadOnlyMemory<byte> text, FontAlignment alignment) =>
        font.DrawHue(primary, x, 192, text.Span, Font.Small, alignment, 13, 5);
    public void TextStyle(byte brightness, byte height) { help.Brightness = brightness; help.VerticalHeight = height; }
    public void Box(int y, uint number) => help.DrawHelp(primary, 10, y, text, number, 60);
    public void Cursor(byte shape) => cursor.Cursor = shape;
    public void FadeIn() => palette.FadePalette(colors.Span, 10, 0, 255, () => timer.SetDelay(1), present, timer.Wait);
    public void PresentCursor() { cursor.Start(); present(); cursor.Replace(); }
    public void PushJoysticks() => pushJoysticks();
    public void Sample(byte number) => sample(number);
    public void FadeBlack() => palette.FadeBlack(15, () => timer.SetDelay(1), present, timer.Wait);
}
