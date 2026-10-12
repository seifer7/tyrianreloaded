/*
 * OpenTyrian: A modern cross-platform port of Tyrian
 * Copyright (C) 2007-2009 The OpenTyrian Development Team
 * Distributed under the GNU General Public License, version 2 or later.
 * See COPYING for details.
 */

namespace OpenTyrian.Core;

public enum ScalingMode { Center, Integer, Fit8By5, Fit4By3 }

public readonly record struct OutputRectangle(int X, int Y, int Width, int Height);

public sealed class VideoLayout
{
    private OutputRectangle _output = new(0, 0, 320, 200);
    public ScalingMode Mode { get; set; } = ScalingMode.Integer;
    public OutputRectangle Output
    {
        get => _output;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value.Width);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value.Height);
            _output = value;
        }
    }

    public bool SetModeByName(string name)
    {
        var mode = name switch
        {
            "Center" => ScalingMode.Center,
            "Integer" => ScalingMode.Integer,
            "Fit 8:5" => ScalingMode.Fit8By5,
            "Fit 4:3" => ScalingMode.Fit4By3,
            _ => (ScalingMode)(-1)
        };
        if ((int)mode < 0) return false;
        Mode = mode;
        return true;
    }

    public OutputRectangle Calculate(int windowWidth, int windowHeight, int textureWidth, int textureHeight)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(windowWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(windowHeight);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(textureWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(textureHeight);
        int width, height;
        switch (Mode)
        {
            case ScalingMode.Center:
                width = textureWidth; height = textureHeight;
                break;
            case ScalingMode.Integer:
                var scale = Math.Max(1, Math.Min(windowWidth / 320, windowHeight / 200));
                width = 320 * scale; height = 200 * scale;
                break;
            case ScalingMode.Fit8By5:
            case ScalingMode.Fit4By3:
                var maxWidth = (int)(windowHeight * (Mode == ScalingMode.Fit8By5 ? 8f / 5f : 4f / 3f));
                var maxHeight = (int)(windowWidth * (Mode == ScalingMode.Fit8By5 ? 5f / 8f : 3f / 4f));
                if (maxWidth > windowWidth) { width = windowWidth; height = maxHeight; }
                else { width = maxWidth; height = windowHeight; }
                break;
            default:
                throw new InvalidOperationException("Unknown video scaling mode.");
        }
        if (width <= 0 || height <= 0) throw new InvalidOperationException("Output aspect ratio produces an empty viewport.");
        return new((windowWidth - width) / 2, (windowHeight - height) / 2, width, height);
    }

    public (int X, int Y) ScreenToWindow(int x, int y) =>
        (unchecked((2 * x + 1) * _output.Width) / 640 + _output.X,
         unchecked((2 * y + 1) * _output.Height) / 400 + _output.Y);

    public (int X, int Y) WindowToScreen(int x, int y) =>
        (unchecked((2 * (x - _output.X) + 1) * 320) / unchecked(2 * _output.Width),
         unchecked((2 * (y - _output.Y) + 1) * 200) / unchecked(2 * _output.Height));

    public (int X, int Y) WindowDistanceToScreen(int x, int y) =>
        (unchecked((2 * x + 1) * 320) / unchecked(2 * _output.Width),
         unchecked((2 * y + 1) * 200) / unchecked(2 * _output.Height));
}
