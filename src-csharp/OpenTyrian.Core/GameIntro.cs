/*
 * OpenTyrian: A modern cross-platform port of Tyrian
 * Copyright (C) 2007-2013 The OpenTyrian Development Team
 * Distributed under the GNU General Public License, version 2 or later.
 * See COPYING for details.
 */

namespace OpenTyrian.Core;

public interface IIntroLogoHost
{
    void Clear();
    void FadeWhite();
    void LoadPicture(int number);
    void Present();
    void FadeIn(int steps);
    void WaitInput();
    void FadeBlack();
}

public sealed class IntroLogos(Action moveLogoUp, IIntroLogoHost host)
{
    public void Run(CancellationToken cancellation = default)
    {
        moveLogoUp();
        host.Clear(); host.FadeWhite();
        Show(10, 25);
        Show(12, 10);

        void Show(int number, int steps)
        {
            cancellation.ThrowIfCancellationRequested();
            host.LoadPicture(number); host.Present(); host.FadeIn(steps); host.WaitInput(); host.FadeBlack();
        }

    }
}

public sealed class IntroLogoHost(IndexedSurface primary, PaletteSystem palette,
    ReadOnlyMemory<PaletteColor> colors, DelayTimer timer, Action<int> loadPicture, Action present) : IIntroLogoHost
{
    public void Clear() => primary.Pixels.Span.Clear();
    public void FadeWhite() => palette.FadeWhite(25, () => timer.SetDelay(1), present, timer.Wait);
    public void LoadPicture(int number) => loadPicture(number);
    public void Present() => present();
    public void FadeIn(int steps) => palette.FadePalette(colors.Span, steps, 0, 255, () => timer.SetDelay(1), present, timer.Wait);
    public void WaitInput() { timer.SetDelay(200); timer.WaitOrInput(); }
    public void FadeBlack() => palette.FadeBlack(10, () => timer.SetDelay(1), present, timer.Wait);
}

public sealed class ScreenBleed(IndexedSurface primary, IndexedSurface game, IndexedSurface background,
    InputState input, DelayTimer timer, Action present, Action<byte> setWarningLines)
{
    public void Run(CancellationToken cancellation = default)
    {
        var size = primary.Pixels.Length;
        var top = primary.Pitch * 4; var bottom = primary.Pitch * 7;
        if (primary.Height < 12 || primary.Pitch < 4 || game.Pixels.Length < size || background.Pixels.Length < size ||
            primary.Pixels.Span.Overlaps(game.Pixels.Span) || primary.Pixels.Span.Overlaps(background.Pixels.Span) ||
            game.Pixels.Span.Overlaps(background.Pixels.Span))
            throw new InvalidDataException("Screen bleed requires independent primary and two sufficiently large scratch surfaces.");
        primary.Pixels.Span[..top].Clear();
        primary.Pixels.Span[(size - bottom)..].Clear();
        var first = game.Pixels[..size]; var second = background.Pixels[..size];
        first.Span.Clear(); primary.Pixels.Span.CopyTo(second.Span);
        input.ServiceEvents(true);
        var frames = 300;
        do
        {
            cancellation.ThrowIfCancellationRequested();
            timer.SetDelay(1);
            var destination = first.Span; var source = second.Span;
            for (int remaining = size - bottom, j = top / 2; remaining > 0; remaining--, j++)
            {
                var offset = j + remaining / 8192 - 4;
                var color = (source[offset] * 12 + destination[offset - primary.Pitch] +
                    destination[offset - 1] + destination[offset + 1] + destination[offset + primary.Pitch]) / 16;
                destination[j] = (byte)color;
            }
            // Native copy length is not reduced by the top border.
            destination.Slice(top, size - bottom).CopyTo(primary.Pixels.Span[top..]);
            present(); frames--; timer.Wait();
            (first, second) = (second, first);
        } while (frames != 0 && !input.AnyButton());
        setWarningLines(4);
    }
}
