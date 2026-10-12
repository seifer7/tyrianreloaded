/*
 * OpenTyrian: A modern cross-platform port of Tyrian
 * Copyright (C) 2007-2009 The OpenTyrian Development Team
 * Distributed under the GNU General Public License, version 2 or later.
 * See COPYING for details.
 */

using System.Text;

namespace OpenTyrian.Core;

public sealed class NetworkRequests
{
    public bool Pause { get; set; }
    public bool InGameMenu { get; set; }
    public bool SkipLevel { get; set; }
    public bool Help { get; set; }
    public bool NortShip { get; set; }
    public bool YourInGameMenu { get; set; }

    public void ClearSpecialRequests() => Pause = InGameMenu = SkipLevel = Help = NortShip = false;
}

public interface INetworkShutdownPresentation
{
    void FadeBlack();
    void ShowMessage(string message);
    void Present();
    void FadeIn();
}

public sealed class NetworkShutdownPresentation(IndexedSurface screen, PaletteSystem palette,
    Stream pictures, PaletteColor[][] palettes, FontRenderer font, DelayTimer timer, Action present)
    : INetworkShutdownPresentation
{
    private readonly PaletteColor[] _colors = new PaletteColor[256];
    public void FadeBlack() => palette.FadeBlack(10, () => timer.SetDelay(1), present, timer.Wait);
    public void ShowMessage(string message)
    {
        PictureLoader.LoadPicture(pictures, 2, screen, _colors, palettes, false, palette);
        var text = Encoding.Latin1.GetBytes(message);
        font.DrawString(screen, font.Center(text, Font.Small), 140, text, Font.Small);
    }
    public void Present() => present();
    public void FadeIn() => palette.FadePalette(_colors, 10, 0, 255, () => timer.SetDelay(1), present, timer.Wait);
}
