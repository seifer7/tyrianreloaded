/*
 * OpenTyrian: A modern cross-platform port of Tyrian
 * Copyright (C) 2007-2009 The OpenTyrian Development Team
 * Distributed under the GNU General Public License, version 2 or later.
 * See COPYING for details.
 */

namespace OpenTyrian.Core;

public sealed class IndexedSurface
{
    public int Width { get; }
    public int Height { get; }
    public int Pitch { get; }
    public Memory<byte> Pixels { get; }
    private readonly TextWriter _warnings;

    public IndexedSurface(int width, int height, int pitch, TextWriter warnings)
        : this(width, height, pitch, new byte[checked(pitch * height)], warnings)
    {
    }

    public IndexedSurface(int width, int height, int pitch, Memory<byte> pixels, TextWriter warnings)
    {
        ArgumentNullException.ThrowIfNull(warnings);
        if (width <= 0 || height <= 0 || pitch < width || (long)pitch * height > pixels.Length)
        {
            throw new ArgumentException("An indexed surface requires positive dimensions, pitch >= width, and sufficient pixels.");
        }

        Width = width;
        Height = height;
        Pitch = pitch;
        Pixels = pixels[..checked(pitch * height)];
        _warnings = warnings;
    }

    public void Pixel(int x, int y, byte color)
    {
        if ((uint)x < Pitch && (uint)y < Height)
        {
            Pixels.Span[y * Pitch + x] = color;
        }
    }

    public void PixelCross(int x, int y, byte color)
    {
        Pixel(x, y, color);
        Pixel(x - 1, y, color);
        Pixel(x + 1, y, color);
        Pixel(x, y - 1, color);
        Pixel(x, y + 1, color);
    }

    public void Rectangle(int left, int top, int right, int bottom, int color)
    {
        if (!ValidRectangle(left, top, right, bottom))
        {
            _warnings.Write($"!!! WARNING: Rectangle clipped: {left} {top} {right} {bottom} {color}\n");
            return;
        }

        Pixels.Span.Slice(top * Pitch + left, right - left + 1).Fill(unchecked((byte)color));
        Pixels.Span.Slice(bottom * Pitch + left, right - left + 1).Fill(unchecked((byte)color));
        for (var row = top + 1; row < bottom; row++)
        {
            Pixels.Span[row * Pitch + left] = unchecked((byte)color);
            Pixels.Span[row * Pitch + right] = unchecked((byte)color);
        }
    }

    public void FillRectangle(int left, int top, int right, int bottom, byte color)
    {
        FillRectangleSize(left, top, (long)right - left + 1, (long)bottom - top + 1, color);
    }

    public void FillRectangleSize(int x, int y, long width, long height, byte color)
    {
        if (width <= 0 || height <= 0)
        {
            return;
        }

        var left = Math.Max(0, x);
        var top = Math.Max(0, y);
        var right = Math.Min(Width, (long)x + width);
        var bottom = Math.Min(Height, (long)y + height);
        if (left >= right || top >= bottom)
        {
            return;
        }

        for (var row = top; row < bottom; row++)
        {
            Pixels.Span.Slice(row * Pitch + left, (int)right - left).Fill(color);
        }
    }

    public void ShadeRectangle(int left, int top, int right, int bottom)
    {
        if (!ValidRectangle(left, top, right, bottom))
        {
            _warnings.Write($"!!! WARNING: Darker Rectangle clipped: {left} {top} {right} {bottom}\n");
            return;
        }

        for (var row = top; row <= bottom; row++)
        {
            for (var column = left; column <= right; column++)
            {
                ref var pixel = ref Pixels.Span[row * Pitch + column];
                pixel = (byte)(((pixel & 0x0F) >> 1) | (pixel & 0xF0));
            }
        }
    }

    public void BrightenRectangle(int left, int top, int right, int bottom)
    {
        if (!ValidRectangle(left, top, right, bottom))
        {
            _warnings.Write($"!!! WARNING: Brighter Rectangle clipped: {left} {top} {right} {bottom}\n");
            return;
        }

        for (var row = top; row <= bottom; row++)
        {
            for (var column = left; column <= right; column++)
            {
                ref var pixel = ref Pixels.Span[row * Pitch + column];
                pixel = (byte)(Math.Min((pixel & 0x0F) + 2, 15) + (pixel & 0xF0));
            }
        }
    }

    public void SegmentedGauge(int x, int y, byte color, uint segmentWidth, uint segmentHeight, uint segmentValue, uint value)
    {
        if (segmentWidth == 0 || segmentHeight == 0 || segmentValue == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(segmentValue), "Segment dimensions and value must be positive.");
        }

        var segments = value / segmentValue;
        var partial = value % segmentValue;
        for (uint index = 0; index < segments; index++)
        {
            FillRectangleSize(x, y, segmentWidth, segmentHeight, unchecked((byte)(color + 12)));
            x = unchecked(x + (int)(segmentWidth + 1));
        }

        if (partial > 0)
        {
            FillRectangleSize(x, y, segmentWidth, segmentHeight, unchecked((byte)(color + (12 * partial / segmentValue))));
        }
    }

    private bool ValidRectangle(int left, int top, int right, int bottom)
    {
        return left >= 0 && top >= 0 && right >= left && bottom >= top && right < Pitch && bottom < Height;
    }
}
