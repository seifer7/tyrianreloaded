/*
 * OpenTyrian: A modern cross-platform port of Tyrian
 * Copyright (C) 2007-2009 The OpenTyrian Development Team
 * Distributed under the GNU General Public License, version 2 or later.
 * See COPYING for details.
 */

namespace OpenTyrian.Core;

public interface IChristmasPromptHost
{
    void EnsureCursor();
    void Fill();
    void Flake(int x, int y, int sprite);
    void Label(int x, int y, ReadOnlyMemory<byte> text, byte hue, sbyte brightness);
    void FadeIn();
    void PresentCursor();
    void PushJoysticks();
    void FadeBlack();
}

public sealed class ChristmasPrompt(InputState input, IChristmasPromptHost host, Func<int> random)
{
    private sealed class Flake
    {
        public short X, Y;
        public byte Accumulator, Speed;
    }

    public static bool IsChristmasTime(DateTimeOffset now) => now.ToLocalTime().Month == 12;

    public bool Run(CancellationToken cancellation = default)
    {
        host.EnsureCursor();
        var flakes = Enumerable.Range(0, 80).Select(_ => new Flake()).ToArray();
        var restart = true; var selected = 0;
        while (true)
        {
            bool moved;
            do
            {
                cancellation.ThrowIfCancellationRequested();
                if (restart)
                {
                    for (var i = 0; i < flakes.Length; i++)
                    {
                        flakes[i].Y = checked((short)(200 + random() % 200));
                        flakes[i].Speed = (byte)(64 + i * 128 / 80);
                    }
                }
                foreach (var flake in flakes)
                {
                    if (flake.Y >= 200)
                    {
                        flake.X = checked((short)(random() % 320)); flake.Y = (short)(186 - flake.Y);
                        flake.Accumulator = flake.Speed;
                    }
                    var temporary = random() & 15;
                    if ((temporary & 14) == 0) flake.X = unchecked((short)(flake.X + (temporary & 1) * 2 - 1));
                    var numerator = flake.Accumulator + flake.Speed;
                    flake.Accumulator = (byte)(numerator % 128); flake.Y = unchecked((short)(flake.Y + numerator / 128));
                }
                host.Fill();
                for (var i = 0; i < 53; i++) host.Flake(flakes[i].X, flakes[i].Y, 225);
                host.Label(160, 85, "Christmas has been detected."u8.ToArray(), 4, -2);
                host.Label(160, 100, "Activate Christmas?"u8.ToArray(), 2, -2);
                host.Label(140, 120, "Yes"u8.ToArray(), 15, (sbyte)(selected == 0 ? -2 : -4));
                host.Label(180, 120, "No"u8.ToArray(), 15, (sbyte)(selected == 1 ? -2 : -4));
                for (var i = 53; i < flakes.Length; i++) host.Flake(flakes[i].X, flakes[i].Y, 226);
                if (restart) { host.FadeIn(); restart = false; }
                input.ServiceEvents(true); host.PresentCursor(); input.Delay(16);
                var x = unchecked((ushort)input.MouseX); var y = unchecked((ushort)input.MouseY);
                host.PushJoysticks(); input.ServiceEvents(false);
                moved = input.MouseX != x || input.MouseY != y;
            } while (!input.NewKey && !input.NewMouse && !moved);
            var action = false; var cancel = false;
            if ((moved || input.NewMouse) && input.MouseY is >= 120 and < 133)
            {
                for (var i = 0; i < 2; i++)
                {
                    var x = 120 + 40 * i;
                    if (input.MouseX < x || input.MouseX >= x + 40) continue;
                    selected = i;
                    if (input.NewMouse && input.LastMouseButton == 1 && input.LastMouseX >= x && input.LastMouseX < x + 40 &&
                        input.LastMouseY is >= 120 and < 133) action = true;
                    break;
                }
            }
            if (input.NewMouse) { if (input.LastMouseButton == 3) cancel = true; }
            else if (input.NewKey)
            {
                switch (input.LastScancode)
                {
                    case 80 or 79: selected = selected == 0 ? 1 : 0; break;
                    case 44 or 40 or 88: action = true; break;
                    case 41: cancel = true; break;
                }
            }
            if (action || cancel) { host.FadeBlack(); return !cancel && selected == 0; }
        }
    }
}

public sealed class ChristmasPromptHost(IndexedSurface primary, FontRenderer font, SpriteSheet flakes, SpriteSheet cursorSheet,
    MouseCursor cursor, PaletteSystem palette, ReadOnlyMemory<PaletteColor> colors, DelayTimer timer,
    Action loadCursor, Action present, Action pushJoysticks) : IChristmasPromptHost
{
    public void EnsureCursor() { if (cursorSheet.Size == 0) loadCursor(); }
    public void Fill() => primary.FillRectangleSize(0, 0, 320, 200, 0x8f);
    public void Flake(int x, int y, int sprite) => flakes.Blit(primary, x, y, sprite, SpriteEffect.Blend);
    public void Label(int x, int y, ReadOnlyMemory<byte> text, byte hue, sbyte brightness) =>
        font.DrawFullShadow(primary, x, y, text.Span, Font.Normal, FontAlignment.Centered, hue, brightness, true, 1);
    public void FadeIn()
    {
        cursor.Cursor = 0;
        palette.FadePalette(colors.Span, 10, 0, 255, () => timer.SetDelay(1), present, timer.Wait);
    }
    public void PresentCursor() { cursor.Start(); present(); cursor.Replace(); }
    public void PushJoysticks() => pushJoysticks();
    public void FadeBlack() => palette.FadeBlack(10, () => timer.SetDelay(1), present, timer.Wait);
}
