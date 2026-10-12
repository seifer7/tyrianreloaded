/*
 * OpenTyrian: A modern cross-platform port of Tyrian
 * Copyright (C) 2007-2009 The OpenTyrian Development Team
 * Distributed under the GNU General Public License, version 2 or later.
 * See COPYING for details.
 */

namespace OpenTyrian.Core;

public interface ISelectionMenuHost
{
    void EnsureCursorSprites();
    void LoadBackground();
    void RestoreBackground();
    int TextWidth(ReadOnlyMemory<byte> text);
    void DrawText(bool background, int x, int y, ReadOnlyMemory<byte> text, Font font,
        FontAlignment alignment, sbyte brightness);
    void FadeIn();
    void PresentWithCursor();
    void PlaySound(byte sample);
    void FadeBlack(int steps);
    void PushJoysticksAsKeyboard();
    void KeepAlive();
    ushort Modifiers { get; }
    int InitializeEpisode(int episode);
}

public sealed class SelectionMenuState
{
    public bool OnePlayerAction { get; set; }
    public bool TwoPlayerMode { get; set; }
    public int Episode { get; set; }
    public int InitialEpisode { get; set; }
    public int Difficulty { get; set; } = 2;
}

public sealed class SelectionMenus(InputState input, ISelectionMenuHost host, SelectionMenuState state)
{
    public bool SelectGameplay(IReadOnlyList<ReadOnlyMemory<byte>> names)
        => Select(Kind.Gameplay, names, null);

    public bool SelectEpisode(IReadOnlyList<ReadOnlyMemory<byte>> names, IReadOnlyList<bool> available)
        => Select(Kind.Episode, names, available);

    public bool SelectDifficulty(IReadOnlyList<ReadOnlyMemory<byte>> names)
        => Select(Kind.Difficulty, names, null);

    private enum Kind { Gameplay, Episode, Difficulty }

    private bool Select(Kind kind, IReadOnlyList<ReadOnlyMemory<byte>> names, IReadOnlyList<bool>? available)
    {
        ArgumentNullException.ThrowIfNull(names);
        var count = kind == Kind.Difficulty ? 6 : 4;
        if (names.Count < count + 1)
            throw new ArgumentException("Menu labels must include the header and every native menu entry.", nameof(names));
        if (kind == Kind.Episode && (available == null || available.Count < 4))
            throw new ArgumentException("Episode selection requires four availability flags.", nameof(available));
        host.EnsureCursorSprites();
        var visible = kind == Kind.Difficulty ? 3 : count;
        var selected = kind == Kind.Difficulty ? 1 : 0;
        var lordProgress = 0;
        var widths = new int[count];
        host.LoadBackground();
        host.DrawText(true, 160, 20, names[0], Font.Large, FontAlignment.Centered, -3);
        var restart = true;
        while (true)
        {
            host.RestoreBackground();
            for (var i = 0; i < visible; i++)
            {
                widths[i] = host.TextWidth(names[i + 1]);
                var x = kind == Kind.Episode ? 20 : 160 - widths[i] / 2;
                var y = kind == Kind.Episode ? 50 + 30 * i : 54 + 24 * i;
                var disabled = kind == Kind.Gameplay ? i == 3 : kind == Kind.Episode && !available![i];
                host.DrawText(false, x, y, names[i + 1], Font.Normal, FontAlignment.Left,
                    (sbyte)(-4 + (i == selected ? 2 : 0) + (disabled ? -4 : 0)));
            }
            if (restart)
            {
                host.FadeIn();
                restart = false;
            }
            input.ServiceEvents(true);
            host.PresentWithCursor();
            bool mouseMoved;
            do
            {
                input.Delay(16);
                var oldX = unchecked((ushort)input.MouseX);
                var oldY = unchecked((ushort)input.MouseY);
                host.PushJoysticksAsKeyboard();
                input.ServiceEvents(false);
                if (kind != Kind.Gameplay) host.KeepAlive();
                mouseMoved = input.MouseX != oldX || input.MouseY != oldY;
            } while (!input.NewKey && !input.NewMouse && !mouseMoved);

            if (kind == Kind.Difficulty)
            {
                if (visible == 5)
                {
                    int[] lord = [15, 18, 21, 7];
                    if (lord.All(key => input.KeysActive[key] != 0)) visible = 6;
                    if (input.NewKey)
                    {
                        if (lordProgress < lord.Length && input.LastScancode == lord[lordProgress])
                        {
                            if (++lordProgress == lord.Length) visible = 6;
                        }
                        else lordProgress = 0;
                    }
                }
                if ((host.Modifiers & 3) != 0)
                {
                    if (visible == 4 && input.KeysActive[48] != 0) visible = 5;
                    if (visible == 3 && input.KeysActive[10] != 0) visible = 4;
                }
            }
            var action = false;
            var cancel = false;
            if (mouseMoved || input.NewMouse)
            {
                for (var i = 0; i < visible; i++)
                {
                    var x = kind == Kind.Episode ? 20 : 160 - widths[i] / 2;
                    var y = kind == Kind.Episode ? 50 + 30 * i : 54 + 24 * i;
                    if (input.MouseX < x || input.MouseX >= x + widths[i] ||
                        input.MouseY < y || input.MouseY >= y + 13) continue;
                    if (selected != i)
                    {
                        host.PlaySound(28);
                        selected = i;
                    }
                    if (input.NewMouse && input.LastMouseButton == 1 &&
                        input.LastMouseX >= x && input.LastMouseX < x + widths[i] &&
                        input.LastMouseY >= y && input.LastMouseY < y + 13) action = true;
                    break;
                }
            }
            if (input.NewMouse)
            {
                if (input.LastMouseButton == 3)
                {
                    host.PlaySound(16);
                    cancel = true;
                }
            }
            else if (input.NewKey)
            {
                switch (input.LastScancode)
                {
                    case 82:
                        host.PlaySound(28);
                        selected = selected == 0 ? visible - 1 : selected - 1;
                        break;
                    case 81:
                        host.PlaySound(28);
                        selected = selected == visible - 1 ? 0 : selected + 1;
                        break;
                    case 44 or 40 or 88: action = true; break;
                    case 41: host.PlaySound(16); cancel = true; break;
                }
            }
            if (action)
            {
                if (kind == Kind.Gameplay && selected == 3 ||
                    kind == Kind.Episode && !available![selected])
                {
                    host.PlaySound(16);
                }
                else
                {
                    host.PlaySound(8);
                    if (kind == Kind.Difficulty)
                    {
                        int[] levels = [1, 2, 3, 4, 6, 8];
                        state.Difficulty = levels[selected];
                    }
                    host.FadeBlack(10);
                    if (kind == Kind.Gameplay)
                    {
                        state.OnePlayerAction = selected == 1;
                        state.TwoPlayerMode = selected == 2;
                    }
                    else if (kind == Kind.Episode)
                    {
                        state.Episode = host.InitializeEpisode(selected + 1);
                        state.InitialEpisode = state.Episode;
                    }
                    return true;
                }
            }
            if (cancel)
            {
                host.FadeBlack(15);
                return false;
            }
        }
    }
}

public sealed class SelectionMenuPresentation(
    IndexedSurface screen, IndexedSurface background, FontRenderer fonts, MouseCursor cursor,
    Action ensureCursorSprites, Action loadBackground, Action fadeIn, Action present,
    Action<byte> playSound, Action<int> fadeBlack, Action pushJoysticks, Action keepAlive,
    Func<ushort> modifiers, Func<int, int> initializeEpisode) : ISelectionMenuHost
{
    public void EnsureCursorSprites() => ensureCursorSprites();
    public void LoadBackground() => loadBackground();
    public void RestoreBackground()
    {
        if (screen.Width != background.Width || screen.Height != background.Height || screen.Pitch != background.Pitch)
            throw new ArgumentException("Menu screen and background must have matching dimensions and pitch.");
        background.Pixels.CopyTo(screen.Pixels);
    }
    public int TextWidth(ReadOnlyMemory<byte> text) => fonts.TextWidth(text.Span, Font.Normal);
    public void DrawText(bool onBackground, int x, int y, ReadOnlyMemory<byte> text, Font font,
        FontAlignment alignment, sbyte brightness)
        => fonts.DrawShadow(onBackground ? background : screen, x, y, text.Span, font, alignment, 15, brightness, false, 2);
    public void FadeIn() { cursor.Cursor = 0; fadeIn(); }
    public void PresentWithCursor()
    {
        cursor.Start();
        try { present(); }
        finally { cursor.Replace(); }
    }
    public void PlaySound(byte sample) => playSound(sample);
    public void FadeBlack(int steps) => fadeBlack(steps);
    public void PushJoysticksAsKeyboard() => pushJoysticks();
    public void KeepAlive() => keepAlive();
    public ushort Modifiers => modifiers();
    public int InitializeEpisode(int episode) => initializeEpisode(episode);
}
