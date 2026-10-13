/*
 * OpenTyrian: A modern cross-platform port of Tyrian
 * Copyright (C) 2007-2013 The OpenTyrian Development Team
 * Distributed under the GNU General Public License, version 2 or later.
 * See COPYING for details.
 */

namespace OpenTyrian.Core;

public interface IGameHelpHost
{
    bool NewKey { get; }
    bool NewMouse { get; }
    void ClearKeyboard();
    void WipeKey();
    void Shade(int left, int top, int right, int bottom);
    void Fill(int left, int top, int right, int bottom, byte color);
    void Option(int x, int y, int sprite);
    void Box(int x, int y, uint number, uint width, byte hue, byte brightness);
    int TextWidth(ReadOnlyMemory<byte> text);
    void Text(int x, int y, ReadOnlyMemory<byte> text);
    void ServiceEvents(bool clear);
    void MouseStart();
    void Present();
    void MouseReplace();
    void Delay(uint milliseconds);
    void PushJoysticks();
    void KeepAlive();
}

public sealed class GameHelp(IGameHelpHost host, GameTextWindow window)
{
    public void Run(bool twoPlayers, ReadOnlyMemory<byte> continueText, CancellationToken cancellation = default)
    {
        host.ClearKeyboard(); host.WipeKey();
        for (var i = 1; i <= 7; i += 2) host.Shade(i, i, 263 - i, 183 - i);
        host.Fill(9, 9, 254, 174, 0);
        if (twoPlayers)
        {
            host.Box(20, 4, 36, 50, 3, 3);
            host.Option(2, 21, 43); host.Box(55, 20, 37, 40, 5, 3);
            host.Option(5, 36, 41); host.Box(40, 43, 34, 44, 5, 3);
            host.Option(2, 79, 42); host.Box(54, 84, 35, 40, 5, 3);
            host.Box(5, 126, 38, 55, 5, 3); host.Box(5, 160, 39, 55, 5, 3);
        }
        else
        {
            host.Option(15, 5, 40); host.Box(40, 10, 31, 45, 5, 3);
            host.Option(5, 37, 39); host.Box(40, 40, 32, 44, 5, 3); host.Box(40, 60, 33, 44, 5, 3);
            host.Option(5, 98, 41); host.Box(40, 103, 34, 44, 5, 3);
            host.Option(2, 138, 42); host.Box(54, 143, 35, 40, 5, 3);
        }
        host.Option(16, 189, 36);
        host.Text(140 - host.TextWidth(continueText) / 2, 190, continueText);
        do
        {
            cancellation.ThrowIfCancellationRequested();
            host.ServiceEvents(true);
            host.MouseStart(); host.Present(); host.MouseReplace();
            host.Delay(16); host.PushJoysticks(); host.ServiceEvents(false); host.KeepAlive();
        } while (!host.NewKey && !host.NewMouse);
        window.EraseTicks = 1;
    }
}

public sealed class GameHelpHost(IndexedSurface primary, FontRenderer font, SpriteTable options,
    HelpTextRenderer help, HelpTextData data, InputState input, MouseCursor cursor,
    Action present, Action pushJoysticks, Action keepAlive) : IGameHelpHost
{
    public bool NewKey => input.NewKey;
    public bool NewMouse => input.NewMouse;
    public void ClearKeyboard() => input.ClearKeyboard();
    public void WipeKey() => input.ClearKeyboard();
    public void Shade(int left, int top, int right, int bottom) => primary.ShadeRectangle(left, top, right, bottom);
    public void Fill(int left, int top, int right, int bottom, byte color) => primary.FillRectangle(left, top, right, bottom, color);
    public void Option(int x, int y, int sprite) => options.Blit(primary, x, y, sprite);
    public void Box(int x, int y, uint number, uint width, byte hue, byte brightness)
    {
        help.Color = hue; help.Brightness = brightness;
        help.DrawHelp(primary, x, y, data, number, width);
    }
    public int TextWidth(ReadOnlyMemory<byte> text) => font.TextWidth(text.Span, Font.Small);
    public void Text(int x, int y, ReadOnlyMemory<byte> text) => font.OutText(primary, x, y, text.Span, 0, 4);
    public void ServiceEvents(bool clear) => input.ServiceEvents(clear);
    public void MouseStart() => cursor.Start();
    public void Present() => present();
    public void MouseReplace() => cursor.Replace();
    public void Delay(uint milliseconds) => input.Delay(milliseconds);
    public void PushJoysticks() => pushJoysticks();
    public void KeepAlive() => keepAlive();
}
