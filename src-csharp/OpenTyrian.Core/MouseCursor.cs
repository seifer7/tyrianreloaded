/*
 * OpenTyrian: A modern cross-platform port of Tyrian
 * Copyright (C) 2007-2009 The OpenTyrian Development Team
 * Distributed under the GNU General Public License, version 2 or later.
 * See COPYING for details.
 */

namespace OpenTyrian.Core;

public sealed class MouseCursor(InputState input, IndexedSurface surface, SpriteSheet sprites)
{
    private readonly byte[] _saved = new byte[24 * 28];
    private int _grabX, _grabY;
    private static readonly (int Index, int X, int Y, int Width, int Height, int FocusX, int FocusY)[] Shapes =
    [
        (273, 0, 0, 11, 16, 0, 0), (275, 0, 0, 21, 16, 10, 8), (277, 0, 0, 21, 16, 10, 7),
        (279, 0, 0, 16, 21, 8, 10), (281, 8, 0, 16, 21, 7, 10)
    ];

    public bool HasMouse { get; set; } = true;
    public bool HasThreeButtons { get; set; } = true;
    public byte Cursor { get; set; }
    public ushort Button { get; private set; }

    public void Start() => StartCore(null);
    public void StartFiltered(byte filter) => StartCore(filter);

    private void StartCore(byte? filter)
    {
        if (!HasMouse) return;
        Validate(true);
        if (filter == null) input.ServiceEvents(false);
        Button = input.MouseDown ? input.LastMouseButton : (ushort)0;
        var shape = Shapes[Cursor];
        _grabX = Math.Clamp(input.MouseX, shape.FocusX, 320 - (shape.Width - shape.FocusX)) - shape.FocusX;
        _grabY = Math.Clamp(input.MouseY, shape.FocusY, 200 - (shape.Height - shape.FocusY)) - shape.FocusY;
        Capture();
        if (!input.MouseInactive)
            sprites.Blit2x2(surface, input.MouseX - shape.X - shape.FocusX, input.MouseY - shape.Y - shape.FocusY,
                shape.Index, filter == null ? SpriteEffect.Copy : SpriteEffect.Filter, filter ?? 0, clip: true);
    }

    public void Replace()
    {
        if (!HasMouse) return;
        Validate(false);
        CopyBlock(false);
    }

    private void Capture() => CopyBlock(true);

    private void CopyBlock(bool capture)
    {
        var pixels = surface.Pixels.Span;
        for (var row = 0; row < 28; row++)
        {
            var offset = (_grabY + row) * surface.Pitch + _grabX;
            for (var column = 0; column < 24; column++)
            {
                // Native cursor grabs can cross a scanline into padding/next row.
                if (offset + column >= pixels.Length) return;
                if (capture) _saved[row * 24 + column] = pixels[offset + column];
                else pixels[offset + column] = _saved[row * 24 + column];
            }
        }
    }

    private void Validate(bool cursor)
    {
        if (surface.Width != 320 || surface.Height != 200) throw new ArgumentException("Mouse cursors require the native 320x200 indexed surface.");
        if (cursor && Cursor >= Shapes.Length) throw new ArgumentOutOfRangeException(nameof(Cursor));
    }
}
