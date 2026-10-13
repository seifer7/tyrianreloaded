/*
 * OpenTyrian: A modern cross-platform port of Tyrian
 * Copyright (C) 2007-2013 The OpenTyrian Development Team
 * Distributed under the GNU General Public License, version 2 or later.
 * See COPYING for details.
 */

using System.Text;

namespace OpenTyrian.Core;

public readonly record struct LoadScreenInput(bool LeftAction, bool RightAction, bool Action, bool Done);
public enum LoadScreenResult { Continue, Exit, Loaded }

public interface ILoadScreenHost
{
    bool GameLoaded { get; }
    void EnsureCursor();
    IReadOnlyList<SavedGameMetadata> BuildSaves();
    void LoadBackground();
    void RestoreBackground();
    void Header();
    void Shaded(int x, int y, ReadOnlyMemory<byte> text, byte hue, int brightness);
    void Arrow(int x, int sprite);
    void Help(ReadOnlyMemory<byte> text);
    void FadeIn();
    void PresentCursor();
    void PushJoysticks();
    void Sample(byte number);
    void LoadSave(string name);
    void FadeBlack();
}

public sealed class LoadScreen(HelpTextData text, InputState input, ILoadScreenHost host)
{
    public void Draw(IReadOnlyList<SavedGameMetadata> saves, int page, int selected, bool restart)
    {
        if (restart) host.LoadBackground();
        host.RestoreBackground();
        host.Header();
        for (var i = 0; i < 12; i++)
        {
            var y = 30 + 13 * i; var brightness = i == selected ? 6 : 2;
            if (i == 11) { host.Shaded(10, y, text.MiscText[33], 13, brightness); continue; }
            var index = page * 11 + i;
            if (index >= saves.Count) continue;
            var save = saves[index];
            host.Shaded(10, y, Encoding.Latin1.GetBytes(save.Name), 13, brightness);
            host.Shaded(120, y, Label(text.MiscTextB[2], save.LevelName), 5, brightness);
            host.Shaded(250, y, Label(text.MiscTextB[1], Encoding.ASCII.GetBytes(
                save.Episode.ToString(System.Globalization.CultureInfo.InvariantCulture))), 5, brightness);
        }
        if (page > 0) host.Arrow(83, 279);
        if ((page + 1) * 11 < saves.Count) host.Arrow(213, 281);
        host.Help(text.MiscText[55]);
        if (restart) host.FadeIn();
    }

    public bool WaitInput(CancellationToken cancellation = default)
    {
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
        return moved;
    }

    public LoadScreenInput ReadInput(int savedCount, int page, ref int selected, bool moved)
    {
        var left = false; var right = false; var action = false; var done = false;
        var pageCount = Math.Max(0, savedCount - page * 11);
        if (moved || input.NewMouse)
        {
            if (page > 0 && input.MouseY >= 179 && input.MouseX is >= 83 and < 107)
            {
                if (input.NewMouse && input.LastMouseButton == 1) { host.Sample(28); left = true; }
            }
            else if ((page + 1) * 11 < savedCount && input.MouseY >= 179 && input.MouseX is >= 213 and < 237)
            {
                if (input.NewMouse && input.LastMouseButton == 1) { host.Sample(28); right = true; }
            }
            else if (input.MouseX is >= 10 and < 310)
            {
                for (var i = 0; i < 12; i++)
                {
                    var y = 30 + 13 * i;
                    if (input.MouseY < y || input.MouseY >= y + 8) continue;
                    if (selected != i) { host.Sample(28); selected = i; }
                    if (input.NewMouse && input.LastMouseButton == 1 &&
                        input.LastMouseX is >= 10 and < 310 && input.LastMouseY >= y && input.LastMouseY < y + 8)
                        action = true;
                    break;
                }
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
                case 80: host.Sample(28); left = true; break;
                case 79: host.Sample(28); right = true; break;
                case 82:
                    host.Sample(28);
                    if (pageCount > 0)
                    {
                        selected = selected == 0 ? 11 : selected - 1;
                        if (selected != 11 && selected >= pageCount) selected = pageCount - 1;
                    }
                    else selected = 11;
                    break;
                case 81:
                    host.Sample(28);
                    if (pageCount > 0)
                    {
                        selected = selected == 11 ? 0 : selected + 1;
                        if (selected >= pageCount) selected = 11;
                    }
                    else selected = 11;
                    break;
                case 44 or 40 or 88: action = true; break;
                case 41: host.Sample(16); done = true; break;
            }
        }
        return new(left, right, action, done);
    }

    public LoadScreenResult HandleAction(IReadOnlyList<SavedGameMetadata> saves, ref int page,
        int selected, LoadScreenInput action)
    {
        if (action.LeftAction) { if (page > 0) page--; }
        else if (action.RightAction) { if ((page + 1) * 11 < saves.Count) page++; }
        else if (action.Action)
        {
            if (selected == 11) { host.Sample(8); return LoadScreenResult.Exit; }
            var index = page * 11 + selected;
            if (index >= saves.Count) host.Sample(23);
            else
            {
                host.Sample(8); host.LoadSave(saves[index].Name); host.FadeBlack();
                return LoadScreenResult.Loaded;
            }
        }
        return action.Done ? LoadScreenResult.Exit : LoadScreenResult.Continue;
    }

    public bool Run(CancellationToken cancellation = default)
    {
        host.EnsureCursor();
        var saves = host.BuildSaves();
        var page = 0; var selected = saves.Count == 0 ? 11 : 0; var restart = true;
        while (true)
        {
            cancellation.ThrowIfCancellationRequested();
            Draw(saves, page, selected, restart); restart = false;
            var moved = WaitInput(cancellation);
            var action = ReadInput(saves.Count, page, ref selected, moved);
            var result = HandleAction(saves, ref page, selected, action);
            if (result == LoadScreenResult.Continue) continue;
            if (result == LoadScreenResult.Exit) host.FadeBlack();
            return result == LoadScreenResult.Loaded && host.GameLoaded;
        }
    }

    private static byte[] Label(byte[] prefix, byte[] suffix)
    {
        var bytes = new byte[Math.Min(21, prefix.Length + 1 + suffix.Length)];
        var count = Math.Min(prefix.Length, bytes.Length);
        prefix.AsSpan(0, count).CopyTo(bytes);
        if (count < bytes.Length)
        {
            bytes[count++] = (byte)' ';
            suffix.AsSpan(0, Math.Min(suffix.Length, bytes.Length - count)).CopyTo(bytes.AsSpan(count));
        }
        return bytes;
    }
}

public sealed class LoadScreenHost(IndexedSurface primary, IndexedSurface background, FontRenderer font,
    HelpTextRenderer help, SpriteSheet sprites, MouseCursor cursor, PaletteSystem palette,
    ReadOnlyMemory<PaletteColor> colors, DelayTimer timer, Action loadCursor, Action loadPicture, Action present,
    Action pushJoysticks, Action<byte> sample, Func<IReadOnlyList<SavedGameMetadata>> buildSaves,
    Action clearPerformSave, Action<string> loadSave, Func<bool> gameLoaded) : ILoadScreenHost
{
    public bool GameLoaded => gameLoaded();
    public void EnsureCursor() { if (sprites.Size == 0) loadCursor(); }
    public IReadOnlyList<SavedGameMetadata> BuildSaves() => buildSaves();
    public void LoadBackground() { loadPicture(); background.FillRectangleSize(0, 192, 320, 8, 0); }
    public void RestoreBackground()
    {
        if (primary.Width != background.Width || primary.Height != background.Height || primary.Pitch != background.Pitch)
            throw new InvalidDataException("Load screen requires identical saved and primary surface geometry.");
        background.Pixels.Span.CopyTo(primary.Pixels.Span);
    }
    public void Header() => font.DrawShadow(primary, 160, 5, "Saved Games"u8, Font.Large, FontAlignment.Centered, 15, -3, false, 2);
    public void Shaded(int x, int y, ReadOnlyMemory<byte> text, byte hue, int brightness) =>
        font.ShadeText(primary, x, y, text.Span, hue, brightness, TextShade.Full);
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
    public void LoadSave(string name) { clearPerformSave(); loadSave(name); }
    public void FadeBlack() => palette.FadeBlack(15, () => timer.SetDelay(1), present, timer.Wait);
}
