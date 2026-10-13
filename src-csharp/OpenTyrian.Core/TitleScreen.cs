/*
 * OpenTyrian: A modern cross-platform port of Tyrian
 * Copyright (C) 2007-2013 The OpenTyrian Development Team
 * Distributed under the GNU General Public License, version 2 or later.
 * See COPYING for details.
 */

namespace OpenTyrian.Core;

public enum TitleScreenResult { Continue, ReturnFalse, ReturnTrue }
public readonly record struct TitleScreenInput(bool Action, bool Done);

public interface ITitleScreenHost
{
    int Width { get; }
    void EnsureCursor();
    void PlayTitleSong();
    void LoadPicture();
    void Version();
    void Capture();
    void Restore();
    void Logo(int y);
    void FadeIn(int steps, int first, int last);
    void SetDelay(int ticks);
    void Present();
    void ServiceWait();
    void Reloaded();
    int TextWidth(ReadOnlyMemory<byte> text);
    void Label(int x, int y, ReadOnlyMemory<byte> text, bool centered, sbyte brightness);
    void ResetCursor();
    void PresentCursor();
    void PushJoysticks();
    void Sample(byte number);
    void FadeBlack(int steps);
    bool NewGame();
    bool LoadGame();
    void HighScores();
    void Instructions();
    void Setup();
    void Whoa();
    void BlackColors();
    void SuperTyrian();
    bool SuperArcade(int mode);
}

public sealed class TitleScreen(HelpTextData text, InputState input, ITitleScreenHost host)
{
    public bool MoveLogoUp { get; set; }
    public bool PlayDemo { get; set; }
    public bool LoadDestruct { get; set; }
    public int Selected { get; set; }

    public bool Run(CancellationToken cancellation = default)
    {
        text.MenuText[4] = "Setup"u8.ToArray();
        host.EnsureCursor();
        var restart = true;
        Selected = 0;
        var progress = new int[9]; var widths = new int[7];
        while (true)
        {
            cancellation.ThrowIfCancellationRequested();
            Draw(restart, widths, cancellation); restart = false;
            if (!Wait(out var moved, cancellation)) return true;
            var interaction = Read(widths, moved);
            var result = Secrets(progress, ref restart);
            if (result != TitleScreenResult.Continue) return result == TitleScreenResult.ReturnTrue;
            if (interaction.Action)
            {
                result = Dispatch(ref restart);
                if (result != TitleScreenResult.Continue) return result == TitleScreenResult.ReturnTrue;
            }
            if (interaction.Done) { host.FadeBlack(15); return false; }
        }
    }

    public void Draw(bool restart, Span<int> widths, CancellationToken cancellation = default)
    {
        ValidateSelected();
        if (widths.Length != 7) throw new ArgumentException("Title menu requires seven measured widths.", nameof(widths));
        if (restart)
        {
            host.PlayTitleSong(); host.LoadPicture(); host.Version();
            if (MoveLogoUp)
            {
                host.Capture(); host.Logo(62); host.FadeIn(10, 0, 239);
                for (var y = 60; y >= 4; y -= 2)
                {
                    cancellation.ThrowIfCancellationRequested();
                    host.SetDelay(2); host.Restore(); host.Logo(y); host.Present(); host.ServiceWait();
                }
                MoveLogoUp = false;
            }
            else { host.Logo(4); host.FadeIn(10, 0, 239); }
            host.Reloaded();
            for (var i = 0; i < 7; i++)
            {
                var label = text.MenuText[i];
                widths[i] = host.TextWidth(label);
                var x = host.Width / 2 - widths[i] / 2; var y = 104 + 13 * i;
                host.Label(x - 1, y - 1, label, false, -10);
                host.Label(x + 1, y + 1, label, false, -10);
                host.Label(x + 1, y - 1, label, false, -10);
                host.Label(x - 1, y + 1, label, false, -10);
                host.Label(x, y, label, false, -3);
            }
            host.Capture(); host.ResetCursor(); host.FadeIn(20, 240, 255);
        }
        host.Restore(); host.Label(host.Width / 2, 104 + 13 * Selected, text.MenuText[Selected], true, -1);
    }

    public bool Wait(out bool moved, CancellationToken cancellation = default)
    {
        input.ServiceEvents(true); host.PresentCursor();
        var started = input.Ticks;
        do
        {
            cancellation.ThrowIfCancellationRequested();
            if (unchecked(input.Ticks - started) > 30000)
            {
                host.FadeBlack(15); PlayDemo = true; moved = false; return false;
            }
            input.Delay(16);
            var x = unchecked((ushort)input.MouseX); var y = unchecked((ushort)input.MouseY);
            host.PushJoysticks(); input.ServiceEvents(false);
            moved = input.MouseX != x || input.MouseY != y;
        } while (!input.NewKey && !input.NewText && !input.NewMouse && !moved);
        return true;
    }

    public TitleScreenInput Read(ReadOnlySpan<int> widths, bool moved)
    {
        ValidateSelected();
        if (widths.Length != 7) throw new ArgumentException("Title menu requires seven measured widths.", nameof(widths));
        var action = false; var done = false;
        if (moved || input.NewMouse)
        {
            for (var i = 0; i < 7; i++)
            {
                var x = host.Width / 2 - widths[i] / 2; var y = 104 + 13 * i;
                if (input.MouseX < x || input.MouseX >= x + widths[i] || input.MouseY < y || input.MouseY >= y + 13) continue;
                if (Selected != i) { host.Sample(28); Selected = i; }
                if (input.NewMouse && input.LastMouseButton == 1 && input.LastMouseX >= x && input.LastMouseX < x + widths[i] &&
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
                case 82: host.Sample(28); Selected = Selected == 0 ? 6 : Selected - 1; break;
                case 81: host.Sample(28); Selected = Selected == 6 ? 0 : Selected + 1; break;
                case 44 or 40 or 88: action = true; break;
                case 41: host.Sample(16); done = true; break;
            }
        }
        return new(action, done);
    }

    public TitleScreenResult Dispatch(ref bool restart)
    {
        ValidateSelected();
        host.Sample(8); host.FadeBlack(15);
        switch (Selected)
        {
            case 0: if (host.NewGame()) return TitleScreenResult.ReturnTrue; break;
            case 1: if (host.LoadGame()) return TitleScreenResult.ReturnTrue; break;
            case 2: host.HighScores(); break;
            case 3: host.Instructions(); break;
            case 4: host.Setup(); break;
            case 5: PlayDemo = true; return TitleScreenResult.ReturnTrue;
            case 6: return TitleScreenResult.ReturnFalse;
        }
        restart = true; return TitleScreenResult.Continue;
    }

    public TitleScreenResult Secrets(Span<int> progress, ref bool restart)
    {
        if (progress.Length != 9) throw new ArgumentException("Title menu requires nine secret-code counters.", nameof(progress));
        if (!input.NewText) return TitleScreenResult.Continue;
        foreach (var value in input.LastText.Span)
        {
            if (value == 0) break;
            var c = value is >= (byte)'a' and <= (byte)'z' ? value - 32 : value;
            for (var i = 0; i < 9; i++)
            {
                var code = text.SpecialNames[i];
                if (code.Length > 10 || code.Length == 0 || progress[i] < 0)
                    throw new InvalidDataException("Title secret codes require native ten-byte storage and nonnegative progress.");
                if (progress[i] >= 9 || progress[i] >= code.Length || c != code[progress[i]])
                {
                    progress[i] = 0; continue;
                }
                progress[i]++;
                if (progress[i] < code.Length && code[progress[i]] != 0) continue;
                if (i == 7) { host.FadeBlack(10); LoadDestruct = true; return TitleScreenResult.ReturnTrue; }
                if (i == 8)
                {
                    host.Sample(37); host.Whoa(); host.BlackColors(); host.SuperTyrian(); return TitleScreenResult.ReturnTrue;
                }
                host.FadeBlack(10);
                if (host.SuperArcade(i)) return TitleScreenResult.ReturnTrue;
                restart = true;
            }
        }
        return TitleScreenResult.Continue;
    }

    private void ValidateSelected()
    {
        if (Selected is < 0 or > 6) throw new InvalidDataException("Title menu selection is outside its seven entries.");
    }
}

public sealed class TitleScreenHost(IndexedSurface primary, IndexedSurface background, FontRenderer font,
    SpriteTable planets, SpriteSheet sprites, MouseCursor cursor, PaletteSystem palette,
    ReadOnlyMemory<PaletteColor> colors, DelayTimer timer, ReadOnlyMemory<byte> version, Action loadCursor,
    Action loadPicture, Action<uint> playSong, Action present, Action pushJoysticks, Action<byte> sample,
    Func<bool> newGame, Func<bool> loadGame, Action highScores, Action instructions, Action setup,
    Action whoa, Action superTyrian, Func<int, bool> superArcade) : ITitleScreenHost
{
    public int Width => primary.Width;
    public void EnsureCursor() { if (sprites.Size == 0) loadCursor(); }
    public void PlayTitleSong() => playSong(29);
    public void LoadPicture() => loadPicture();
    public void Version() => font.DrawShadow(primary, 2, 192, version.Span, Font.Small, FontAlignment.Left, 15, 0, false, 1);
    public void Capture() => Copy(primary, background);
    public void Restore() => Copy(background, primary);
    public void Logo(int y) => planets.Blit(primary, 11, y, 146);
    public void FadeIn(int steps, int first, int last) => palette.FadePalette(colors.Span, steps, first, last, () => timer.SetDelay(1), present, timer.Wait);
    public void SetDelay(int ticks) => timer.SetDelay(ticks);
    public void Present() => present();
    public void ServiceWait() => timer.ServiceWait();
    public void Reloaded() => font.DrawString(primary, 150, 80, "RELOADED"u8, Font.Large);
    public int TextWidth(ReadOnlyMemory<byte> text) => font.TextWidth(text.Span, Font.Normal);
    public void Label(int x, int y, ReadOnlyMemory<byte> text, bool centered, sbyte brightness) =>
        font.DrawHue(primary, x, y, text.Span, Font.Normal, centered ? FontAlignment.Centered : FontAlignment.Left, 15, brightness);
    public void ResetCursor() => cursor.Cursor = 0;
    public void PresentCursor()
    {
        cursor.StartFiltered(0xf0);
        try { present(); }
        finally { cursor.Replace(); }
    }
    public void PushJoysticks() => pushJoysticks();
    public void Sample(byte number) => sample(number);
    public void FadeBlack(int steps) => palette.FadeBlack(steps, () => timer.SetDelay(1), present, timer.Wait);
    public bool NewGame() => newGame();
    public bool LoadGame() => loadGame();
    public void HighScores() => highScores();
    public void Instructions() => instructions();
    public void Setup() => setup();
    public void Whoa() => whoa();
    public void BlackColors() => palette.SetSolid(default, 0, 255);
    public void SuperTyrian() => superTyrian();
    public bool SuperArcade(int mode) => superArcade(mode);
    private static void Copy(IndexedSurface from, IndexedSurface to)
    {
        if (from.Width != to.Width || from.Height != to.Height || from.Pitch != to.Pitch)
            throw new InvalidDataException("Title menu requires identical saved and primary surface geometry.");
        from.Pixels.CopyTo(to.Pixels);
    }
}
