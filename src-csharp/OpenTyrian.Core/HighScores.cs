/*
 * OpenTyrian: A modern cross-platform port of Tyrian
 * Copyright (C) 2007-2013 The OpenTyrian Development Team
 * Distributed under the GNU General Public License, version 2 or later.
 * See COPYING for details.
 */

using System.Globalization;
using System.Text;

namespace OpenTyrian.Core;

public readonly record struct HighScoreInput(bool NewKey, bool NewMouse, bool NewText, int Scancode,
    int MouseButton, int MouseX, int MouseY, ReadOnlyMemory<byte> Text);

public interface IHighScoreHost
{
    void EnsureCursor();
    void WaitNoMouse(CancellationToken cancellation);
    void Clear();
    void Present();
    void SelectPalette();
    void PlaySong(uint song);
    bool Playing { get; }
    void Shade(int left, int top, int right, int bottom);
    void Fill(int left, int top, int right, int bottom, byte color);
    void ServiceEvents(bool clearNew);
    HighScoreInput Input { get; }
    int Center(ReadOnlyMemory<byte> text, Font font);
    int Width(ReadOnlyMemory<byte> text);
    void Title(int x, int y, ReadOnlyMemory<byte> text, Font font);
    void MessageBox();
    void ShadedText(int x, int y, ReadOnlyMemory<byte> text, byte color, int brightness);
    void Name(int x, int y, ReadOnlyMemory<byte> text);
    void SetDelay(int ticks);
    void MouseStart();
    void MouseReplace();
    void PushJoysticks();
    void ServiceWait();
    void FadeIn(int steps);
    void FadeBlack(int steps);
    void LoadPicture();
    uint GlowFrames { get; set; }
    void Glow(int x, int y, ReadOnlyMemory<byte> text, CancellationToken cancellation);
    void WaitInput(CancellationToken cancellation);
}

public sealed class HighScores(LegacyGameState game, LegacySaveArchive saves, PlayerProgression progression,
    IReadOnlyList<byte[]> miscText, IReadOnlyList<byte[]> episodeNames, IHighScoreHost host)
{
    public void Run(CancellationToken cancellation = default)
    {
        if (game.InitialEpisode < 1) throw new InvalidDataException("High scores require a valid initial episode.");
        var first = (game.InitialEpisode - 1) * 6 + (game.TwoPlayerMode ? 3 : 0);
        var limit = first + 3;
        if (limit > saves.Slots.Length || miscText.Count < 59 || game.Episode >= episodeNames.Count)
            throw new InvalidDataException("High-score group or presentation text is missing.");
        host.EnsureCursor();
        for (var index = 0; index < (game.TwoPlayerMode ? 2 : 1); index++)
        {
            cancellation.ThrowIfCancellationRequested();
            saves.SortHighScores();
            var player = game.TwoPlayerMode && game.Players[0].Cash < game.Players[1].Cash ? 1 - index : index;
            var score = unchecked((int)(game.TwoPlayerMode ? game.Players[player].Cash : progression.TotalScore(game.Players[0])));
            var slot = first;
            while (slot < limit && score <= saves.Slots[slot].HighScore1) slot++;
            if (slot == limit) continue;
            for (var i = limit - 1; i > slot; i--)
            {
                saves.Slots[i].HighScore1 = saves.Slots[i - 1].HighScore1;
                saves.Slots[i].HighScoreName = CString(saves.Slots[i - 1].HighScoreName);
            }
            host.WaitNoMouse(cancellation);
            host.Clear(); host.Present(); host.SelectPalette(); host.PlaySong(33);
            var name = Enumerable.Repeat((byte)' ', 29).ToArray();
            var length = 0;
            byte flash = 138;
            var fadeIn = true;
            var quit = false; var cancel = false;
            host.Shade(65, 55, 255, 155);
            do
            {
                cancellation.ThrowIfCancellationRequested();
                host.ServiceEvents(true);
                Title(51, 3, Font.Large);
                Title(game.TwoPlayerMode ? 57 + player : 52, 30, Font.Normal);
                host.MessageBox();
                host.ShadedText(60, 55, game.TwoPlayerMode ? Join(miscText[48 + player], miscText[53]) : miscText[53], 11, 4);
                host.ShadedText(70, 70, Join(miscText[37], Number(score)), 11, 4);
                do
                {
                    cancellation.ThrowIfCancellationRequested();
                    flash = flash == 138 ? (byte)130 : (byte)138;
                    var current = name.AsMemory(0, length);
                    host.Name(65, 89, current);
                    var cursor = unchecked((ushort)(65 + host.Width(current)));
                    host.Shade(cursor + 2, 90, cursor + 6, 95);
                    host.Fill(cursor + 1, 89, cursor + 5, 94, flash);
                    for (var i = 0; i < 14; i++)
                    {
                        cancellation.ThrowIfCancellationRequested();
                        host.SetDelay(1); host.MouseStart(); host.Present();
                        if (fadeIn) { host.FadeIn(15); fadeIn = false; }
                        host.MouseReplace(); host.PushJoysticks(); host.ServiceWait();
                        if (host.Input.NewKey || host.Input.NewMouse) break;
                    }
                } while (!host.Input.NewKey && !host.Input.NewMouse && !host.Input.NewText);
                if (!host.Playing) host.PlaySong(31);
                var input = host.Input;
                if (input.MouseButton > 0)
                {
                    if (input.MouseX is > 56 and < 142 && input.MouseY is > 123 and < 149) quit = true;
                    else if (input.MouseX is > 151 and < 237 && input.MouseY is > 123 and < 149) { quit = true; cancel = true; }
                }
                else if (input.NewText)
                {
                    foreach (var raw in input.Text.Span)
                    {
                        if (raw == 0) break;
                        var character = raw <= 127 ? (byte)char.ToUpperInvariant((char)raw) : (byte)0;
                        if ((character == ' ' || FontRenderer.SpriteIndex(character) != -1) && length < 28)
                            name[length++] = character;
                    }
                }
                else if (input.NewKey)
                {
                    switch (input.Scancode)
                    {
                        case 42:
                        case 76:
                            if (length > 0) name[--length] = (byte)' ';
                            break;
                        case 41: quit = cancel = true; break;
                        case 40:
                        case 88: quit = true; break;
                    }
                }
            } while (!quit);
            if (!cancel)
            {
                saves.Slots[slot].HighScore1 = score;
                saves.Slots[slot].HighScoreName = name;
                saves.Slots[slot].HighScoreDifficulty = unchecked((byte)game.Difficulty);
            }
            host.FadeBlack(15); host.LoadPicture();
            Title(50, 10, Font.Large);
            host.Title(host.Center(episodeNames[game.Episode], Font.Normal), 35, episodeNames[game.Episode], Font.Normal);
            for (var i = first; i < limit; i++)
            {
                if (i == slot) continue;
                host.ShadedText(20, (i - first + 1) * 12 + 65, Rank(i), 15, 0);
                host.ShadedText(150, (i - first + 1) * 12 + 65, saves.Slots[i].HighScoreName, 15, 2);
            }
            host.Present(); host.FadeIn(15);
            host.GlowFrames = 6;
            host.Glow(20, (slot - first + 1) * 12 + 65, Rank(slot), cancellation);
            host.Glow(150, (slot - first + 1) * 12 + 65, saves.Slots[slot].HighScoreName, cancellation);
            host.Glow(host.Center(miscText[4], Font.Small), 180, miscText[4], cancellation);
            host.Present();
            if (host.GlowFrames != 0) host.WaitInput(cancellation);
            host.FadeBlack(15);

            byte[] Rank(int i) => Encoding.ASCII.GetBytes($"~#{i - first + 1}:~  {saves.Slots[i].HighScore1.ToString(CultureInfo.InvariantCulture)}");
        }
        void Title(int text, int y, Font font) => host.Title(host.Center(miscText[text], font), y, miscText[text], font);
    }

    private static byte[] Number(int value) => Encoding.ASCII.GetBytes(value.ToString(CultureInfo.InvariantCulture));
    private static byte[] CString(ReadOnlySpan<byte> text)
    {
        var end = text.IndexOf((byte)0);
        return (end < 0 ? text : text[..end]).ToArray();
    }
    private static byte[] Join(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
        => [.. CString(left), (byte)' ', .. CString(right)];
}

public sealed class HighScoreHost(IndexedSurface surface, SpriteSheet cursorSheet, SpriteTable options,
    FontRenderer font, PaletteSystem palette, PaletteColor[] colors, ReadOnlyMemory<PaletteColor> initialPalette,
    InputState input, MouseCursor cursor, DelayTimer timer, TextGlow glow, Action loadCursor,
    Action loadPicture, Action<uint> playSong, Func<bool> playing, Action present, Action pushJoysticks) :
    IHighScoreHost, ISavedGameNamePopupHost
{
    // Native score entry reads the shared mouse-position words, not mouse_x/y.
    public ushort LegacyMouseX { get; set; }
    public ushort LegacyMouseY { get; set; }
    public Action<string>? SaveGameOperation { get; init; }
    public Action<string>? LoadGameOperation { get; init; }
    public void EnsureCursor() { if (cursorSheet.Size == 0) loadCursor(); }
    public void WaitNoMouse(CancellationToken cancellation) => input.WaitNoInput(false, true, false, cancellation);
    public void Clear() => surface.Pixels.Span.Clear();
    public void Present() => present();
    public void SelectPalette()
    {
        if (initialPalette.Length != 256 || colors.Length != 256) throw new InvalidDataException("High scores require a complete palette.");
        initialPalette.Span.CopyTo(colors);
    }
    public void PlaySong(uint song) => playSong(song);
    public bool Playing => playing();
    public void Shade(int left, int top, int right, int bottom) => surface.ShadeRectangle(left, top, right, bottom);
    public void Fill(int left, int top, int right, int bottom, byte color) => surface.FillRectangle(left, top, right, bottom, color);
    public void ServiceEvents(bool clearNew) => input.ServiceEvents(clearNew);
    public HighScoreInput Input => new(input.NewKey, input.NewMouse, input.NewText, input.LastScancode,
        cursor.Button, LegacyMouseX, LegacyMouseY, input.LastText);
    public int Center(ReadOnlyMemory<byte> text, Font selected) => font.Center(text.Span, selected);
    public int Width(ReadOnlyMemory<byte> text) => font.TextWidth(text.Span, Font.Small);
    public void Title(int x, int y, ReadOnlyMemory<byte> text, Font selected) => font.DrawString(surface, x, y, text.Span, selected);
    public void MessageBox() => options.Blit(surface, 50, 50, 35);
    public void ShadedText(int x, int y, ReadOnlyMemory<byte> text, byte color, int brightness)
        => font.ShadeText(surface, x, y, text.Span, color, brightness, TextShade.Full);
    public void Name(int x, int y, ReadOnlyMemory<byte> text) => font.OutText(surface, x, y, text.Span, 8, 3);
    public void SetDelay(int ticks) => timer.SetDelay(ticks);
    public void MouseStart() => cursor.Start();
    public void MouseReplace() => cursor.Replace();
    public void PushJoysticks() => pushJoysticks();
    public void ServiceWait() => timer.ServiceWait();
    public void FadeIn(int steps) => palette.FadePalette(colors, steps, 0, 255, () => timer.SetDelay(1), present, timer.Wait);
    public void FadeBlack(int steps) => palette.FadeBlack(steps, () => timer.SetDelay(1), present, timer.Wait);
    public void LoadPicture() => loadPicture();
    public uint GlowFrames { get => glow.FrameCount; set { glow.FrameCount = value; glow.Font = Font.Small; } }
    public void Glow(int x, int y, ReadOnlyMemory<byte> text, CancellationToken cancellation)
    {
        glow.Brightness = 10;
        glow.Draw(x, y, text, cancellation);
    }
    public void WaitInput(CancellationToken cancellation) => input.WaitInput(true, true, true, cancellation);

    void ISavedGameNamePopupHost.EnsureMouseSprite() => EnsureCursor();
    void ISavedGameNamePopupHost.WaitForNoInput(CancellationToken cancellation) =>
        input.WaitNoInput(false, true, false, cancellation);
    void ISavedGameNamePopupHost.DrawMessageBox() => MessageBox();
    void ISavedGameNamePopupHost.DrawShadedText(int x, int y, ReadOnlyMemory<byte> text,
        byte color, int brightness, bool darken)
        => font.ShadeText(surface, x, y, text.Span, color, brightness, darken ? TextShade.Darken : TextShade.Full);
    void ISavedGameNamePopupHost.DrawName(int x, int y, ReadOnlyMemory<byte> text) => Name(x, y, text);
    int ISavedGameNamePopupHost.TextWidth(ReadOnlyMemory<byte> text, Font selected)
        => font.TextWidth(text.Span, selected);
    void ISavedGameNamePopupHost.DrawAdjustedText(int x, int y, ReadOnlyMemory<byte> text,
        byte color, int adjustment, Font selected, bool centered)
        => font.OutTextAdjust(surface, x, y, text.Span, color, adjustment, selected, centered);
    void ISavedGameNamePopupHost.ReadMousePosition()
    {
        var position = input.MousePosition();
        LegacyMouseX = position.X;
        LegacyMouseY = position.Y;
    }
    void ISavedGameNamePopupHost.SaveGame(string name)
        => (SaveGameOperation ?? throw new InvalidOperationException("Save-game callback is not configured."))(name);
    void ISavedGameNamePopupHost.LoadGame(string name)
        => (LoadGameOperation ?? throw new InvalidOperationException("Load-game callback is not configured."))(name);
}
