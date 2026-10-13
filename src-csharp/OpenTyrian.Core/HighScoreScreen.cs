/*
 * OpenTyrian: A modern cross-platform port of Tyrian
 * Copyright (C) 2007-2013 The OpenTyrian Development Team
 * Distributed under the GNU General Public License, version 2 or later.
 * See COPYING for details.
 */

using System.Globalization;
using System.Text;

namespace OpenTyrian.Core;

public interface IHighScoreScreenHost
{
    void EnsureCursor();
    void LoadBackground();
    void RestoreBackground();
    void Header(bool background, int y, ReadOnlyMemory<byte> text, Font font, sbyte brightness);
    void Shaded(int x, int y, ReadOnlyMemory<byte> text, int brightness);
    void Arrow(int x, int sprite);
    void Help(ReadOnlyMemory<byte> text);
    void FadeIn();
    void PresentCursor();
    void PushJoysticks();
    void Sample(byte number);
    void FadeBlack();
}

public sealed class HighScoreScreen(LegacySaveArchive saves, HelpTextData text, IReadOnlyList<bool> episodes,
    InputState input, IHighScoreScreenHost host)
{
    public void Run(CancellationToken cancellation = default)
    {
        if (episodes.Count < 3 || saves.Slots.Length < 18)
            throw new InvalidDataException("High-score paging requires the first three episodes and eighteen score slots.");
        host.EnsureCursor();
        var restart = true; var episode = 0;
        while (true)
        {
            cancellation.ThrowIfCancellationRequested();
            if (restart)
            {
                host.LoadBackground();
                host.Header(true, 3, text.MiscText[50], Font.Large, -3);
            }
            host.RestoreBackground();
            host.Header(false, 30, text.EpisodeNames[episode + 1], Font.Normal, (sbyte)(episodes[episode] ? -3 : -7));
            for (var group = 0; group < 2; group++)
            {
                host.Header(false, group == 0 ? 55 : 120, text.MiscText[46 + group], Font.Normal, -3);
                for (var i = 0; i < 3; i++)
                {
                    var y = (group == 0 ? 75 : 135) + i * 10;
                    var save = saves.Slots[episode * 6 + group * 3 + i];
                    var rank = Math.Min(save.HighScoreDifficulty, text.HighScoreDifficultyNames.Length - 1);
                    var number = Encoding.ASCII.GetBytes($"~#{i + 1}:~  {save.HighScore1.ToString(CultureInfo.InvariantCulture)}");
                    host.Shaded(20, y, number, 0);
                    host.Shaded(110, y, save.HighScoreName, 2);
                    host.Shaded(250, y, text.HighScoreDifficultyNames[rank], rank == 0 ? 0 : rank - 1);
                }
            }
            var left = episode > 0; var right = episode < 2;
            if (left) host.Arrow(83, 279);
            if (right) host.Arrow(213, 281);
            host.Help(text.MiscText[56]);
            if (restart) { host.FadeIn(); restart = false; }
            do
            {
                cancellation.ThrowIfCancellationRequested();
                input.ServiceEvents(true); host.PresentCursor(); input.Delay(16);
                host.PushJoysticks(); input.ServiceEvents(false);
            } while (!input.NewKey && !input.NewMouse);
            if (input.NewMouse)
            {
                if (input.LastMouseButton == 3) { host.Sample(16); host.FadeBlack(); return; }
                if (input.LastMouseButton != 1 || input.MouseY < 179) continue;
                if (left && input.MouseX is >= 83 and < 107) { host.Sample(28); episode--; }
                else if (right && input.MouseX is >= 213 and < 237) { host.Sample(28); episode++; }
            }
            else
            {
                switch (input.LastScancode)
                {
                    case 80: host.Sample(28); episode = episode == 0 ? 2 : episode - 1; break;
                    case 79: host.Sample(28); episode = episode == 2 ? 0 : episode + 1; break;
                    case 44 or 40 or 88 or 41: host.Sample(16); host.FadeBlack(); return;
                }
            }
        }
    }
}

public sealed class HighScoreScreenHost(IndexedSurface primary, IndexedSurface background, FontRenderer font,
    HelpTextRenderer help, SpriteSheet sprites, MouseCursor cursor, PaletteSystem palette,
    ReadOnlyMemory<PaletteColor> colors, DelayTimer timer, Action loadCursor, Action loadPicture, Action present,
    Action pushJoysticks, Action<byte> sample) : IHighScoreScreenHost
{
    public void EnsureCursor() { if (sprites.Size == 0) loadCursor(); }
    public void LoadBackground()
    {
        loadPicture();
        background.FillRectangleSize(0, 192, 320, 8, 0);
    }
    public void RestoreBackground()
    {
        if (primary.Width != background.Width || primary.Height != background.Height || primary.Pitch != background.Pitch)
            throw new InvalidDataException("High-score screen requires identical saved and primary surface geometry.");
        background.Pixels.Span.CopyTo(primary.Pixels.Span);
    }
    public void Header(bool saved, int y, ReadOnlyMemory<byte> text, Font selected, sbyte brightness) =>
        font.DrawShadow(saved ? background : primary, 160, y, text.Span, selected, FontAlignment.Centered, 15, brightness, false, 2);
    public void Shaded(int x, int y, ReadOnlyMemory<byte> text, int brightness) =>
        font.ShadeText(primary, x, y, text.Span, 15, brightness, TextShade.Full);
    public void Arrow(int x, int sprite) => sprites.Blit2x2(primary, x, 179, sprite);
    public void Help(ReadOnlyMemory<byte> text) { help.Color = 15; help.DrawBox(primary, 103, 182, text.Span, 25); }
    public void FadeIn()
    {
        cursor.Cursor = 0;
        palette.FadePalette(colors.Span, 10, 0, 255, () => timer.SetDelay(1), present, timer.Wait);
    }
    public void PresentCursor() { cursor.Start(); present(); cursor.Replace(); }
    public void PushJoysticks() => pushJoysticks();
    public void Sample(byte number) => sample(number);
    public void FadeBlack() => palette.FadeBlack(15, () => timer.SetDelay(1), present, timer.Wait);
}
