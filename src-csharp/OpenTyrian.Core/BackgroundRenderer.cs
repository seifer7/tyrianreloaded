/*
 * OpenTyrian: A modern cross-platform port of Tyrian
 * Copyright (C) 2007-2009 The OpenTyrian Development Team
 * Distributed under the GNU General Public License, version 2 or later.
 * See COPYING for details.
 */

namespace OpenTyrian.Core;

public sealed class BackgroundLayer
{
    public byte[]?[] Tiles { get; set; } = [];
    public int MapOffset { get; set; }
    public int HorizontalTileOffset { get; set; }
    public ushort X { get; set; }
    public ushort MapY { get; set; }
    public ushort Position { get; set; }
    public ushort Move { get; set; }
}

public sealed class BackgroundRenderer
{
    public BackgroundLayer First { get; } = new();
    public BackgroundLayer Second { get; } = new();
    public BackgroundLayer Third { get; } = new();
    public byte SecondDelay { get; set; }
    public byte SecondDelayMax { get; set; }
    public byte SecondBackground { get; set; }
    public bool[] Smoothies { get; } = new bool[9];
    public byte[] SmoothieData { get; } = new byte[9];
    public bool AnySmoothies { get; private set; }
    public bool FilterFade { get; set; }
    public bool FilterFadeStart { get; set; }
    public sbyte Brightness { get; set; }
    public sbyte BrightnessChange { get; set; }
    public sbyte LevelFilter { get; set; }
    public sbyte NewLevelFilter { get; set; }
    public bool FiltrationAvailable { get; set; }
    public bool ExplosionTransparent { get; set; }

    public static void BlitRow(IndexedSurface surface, int x, int y, ReadOnlySpan<byte[]?> tiles, bool blend = false)
    {
        if (tiles.Length < 12)
        {
            throw new ArgumentException("A background row requires twelve tiles.", nameof(tiles));
        }

        foreach (var tile in tiles[..12])
        {
            if (tile is not null && tile.Length < 24 * 28)
            {
                throw new InvalidDataException("A background tile requires 24 by 28 pixels.");
            }
        }

        var pixels = surface.Pixels.Span;
        long position = (long)y * surface.Pitch + x;
        for (var row = 0; row < 28; row++)
        {
            if (position + 288 < 0)
            {
                position += surface.Pitch;
                continue;
            }

            for (var index = 0; index < 12; index++)
            {
                var tile = tiles[index];
                if (tile is null)
                {
                    position += 24;
                    continue;
                }

                for (var column = 0; column < 24; column++, position++)
                {
                    if (position >= pixels.Length)
                    {
                        return;
                    }

                    var source = tile[row * 24 + column];
                    if (position >= 0 && source != 0)
                    {
                        ref var destination = ref pixels[(int)position];
                        destination = blend ? (byte)((source & 0xF0) | (((destination & 15) + (source & 15)) / 2)) : source;
                    }
                }
            }

            position += surface.Pitch - 288;
        }
    }

    public void DrawFirst(IndexedSurface surface)
    {
        surface.FillRectangleSize(0, 0, surface.Width, surface.Height, 0);
        DrawLayer(surface, First, 14, First.X, First.HorizontalTileOffset, false);
    }

    public void DrawSecond(IndexedSurface surface, bool blend = false)
    {
        if (SecondDelayMax > 1 && Second.Move < 2)
        {
            Second.Move = SecondDelay == 1 ? (ushort)1 : (ushort)0;
        }

        if (blend || SecondBackground != 0)
        {
            var synchronize = !blend && Smoothies[1];
            DrawLayer(surface, Second, 14, synchronize ? First.X : Second.X,
                synchronize ? First.HorizontalTileOffset : Second.HorizontalTileOffset, blend);
        }

        SecondDelay = unchecked((byte)(SecondDelay - 1));
        if (SecondDelay == 0)
        {
            SecondDelay = SecondDelayMax;
            MoveLayer(Second, 14);
        }
    }

    public void DrawThird(IndexedSurface surface)
    {
        MoveLayer(Third, 15);
        DrawLayer(surface, Third, 15, Third.X, Third.HorizontalTileOffset, false);
    }

    private static void MoveLayer(BackgroundLayer layer, int stride)
    {
        layer.Position = unchecked((ushort)(layer.Position + layer.Move));
        if (layer.Position > 27)
        {
            layer.Position -= 28;
            layer.MapY = unchecked((ushort)(layer.MapY - 1));
            layer.MapOffset -= stride;
        }
    }

    private static void DrawLayer(IndexedSurface surface, BackgroundLayer layer, int stride, int x, int horizontalOffset, bool blend)
    {
        var offset = layer.MapOffset + horizontalOffset - 12;
        for (var row = -1; row < 7; row++, offset += stride)
        {
            if (offset < 0 || offset > layer.Tiles.Length - 12)
            {
                throw new InvalidDataException("Background map row lies outside the tile map.");
            }

            BlitRow(surface, x, row * 28 + layer.Position, layer.Tiles.AsSpan(offset, 12), blend);
        }
    }

    public void CheckSmoothies(byte processorType)
        => AnySmoothies = (processorType > 2 && (Smoothies[0] || Smoothies[1])) ||
            (processorType > 1 && (Smoothies[2] || Smoothies[3] || Smoothies[4]));

    public static void Darken(IndexedSurface surface, ushort neat)
    {
        RequireScreen(surface, 288, 184);
        var pixels = surface.Pixels.Span;
        for (var row = 0; row < 184; row++)
        {
            var y = 184 - row;
            for (var column = 0; column < 264; column++)
            {
                var x = 264 - column;
                var position = row * surface.Pitch + 24 + column;
                var value = pixels[position] & 15;
                var previous = y == 184 ? 0 : pixels[position - (surface.Pitch - 1)];
                var noise = (((x - neat - y) >> 2) + pixels[position - 2] + previous) & 15;
                pixels[position] = (byte)((((value << 4) - value + noise) >> 4) | (pixels[position] & 0xF0));
            }
        }
    }

    public void FilterScreen(IndexedSurface surface, sbyte color, sbyte intensity)
    {
        if (FilterFade)
        {
            Brightness = unchecked((sbyte)(Brightness + BrightnessChange));
            if ((FilterFadeStart && Brightness < -14) || Brightness > 14)
            {
                BrightnessChange = unchecked((sbyte)-BrightnessChange);
                FilterFadeStart = false;
                LevelFilter = NewLevelFilter;
            }

            if (!FilterFadeStart && Brightness == 0)
            {
                FilterFade = false;
                Brightness = -99;
            }
        }

        RequireScreen(surface, 288, 184);
        var pixels = surface.Pixels.Span;
        if (color != -99 && FiltrationAvailable)
        {
            var hue = unchecked((byte)(color << 4));
            for (var row = 0; row < 184; row++)
            {
                for (var column = 24; column < 288; column++)
                {
                    ref var pixel = ref pixels[row * surface.Pitch + column];
                    pixel = unchecked((byte)(hue | (pixel & 15)));
                }
            }
        }

        if (intensity != -99 && ExplosionTransparent)
        {
            for (var row = 0; row < 184; row++)
            {
                for (var column = 24; column < 288; column++)
                {
                    ref var pixel = ref pixels[row * surface.Pitch + column];
                    var value = unchecked((uint)((pixel & 15) + intensity));
                    pixel = (byte)((uint)(pixel & 0xF0) | (value >= 31 ? 0u : value >= 15 ? 15u : value));
                }
            }
        }
    }

    public static void Blur(IndexedSurface destination, IndexedSurface source, bool iced = false)
    {
        RequireScreen(destination, 320, 184);
        RequireScreen(source, 320, 184);
        var target = destination.Pixels.Span;
        var input = source.Pixels.Span;
        for (var row = 0; row < 184; row++)
        {
            for (var column = 0; column < 320; column++)
            {
                ref var pixel = ref target[row * destination.Pitch + column];
                var value = input[row * source.Pitch + column];
                pixel = (byte)((((value & 15) + (pixel & 15)) / 2) | (iced ? 0x80 : value & 0xF0));
            }
        }
    }

    public static void Lava(IndexedSurface destination, IndexedSurface source) => Wave(destination, source, true, 0x70);
    public void Water(IndexedSurface destination, IndexedSurface source) => Wave(destination, source, false, unchecked((byte)(SmoothieData[1] << 4)));

    private static void Wave(IndexedSurface destination, IndexedSurface source, bool lava, byte hue)
    {
        RequireScreen(destination, 320, 187);
        RequireScreen(source, 320, 186);
        var target = destination.Pixels.Span;
        var input = source.Pixels.Span;
        var w = 320 * 185 - 1;
        for (var row = 184; row >= 0; row--)
        {
            for (var block = 319; block >= 0; block -= 8)
            {
                var waver = lava ? Math.Abs(((w >> 9) & 15) - 8) - 1 : Math.Abs(((w >> 10) & 7) - 4) - 1;
                w -= 8;
                for (var offset = 0; offset < 8; offset++)
                {
                    var column = block - offset;
                    var position = row * destination.Pitch + column;
                    var sourcePosition = row * source.Pitch + column;
                    if (!lava && (input[sourcePosition] & 0x30) == 0)
                    {
                        target[position] = input[sourcePosition];
                        continue;
                    }

                    var value = lava ? (sourcePosition + waver >= 0 ? (input[sourcePosition + waver] & 15) * 2 : 0) : input[sourcePosition] & 15;
                    value += target[position + waver + destination.Pitch] & 15;
                    if (lava && position + waver - destination.Pitch >= 0)
                    {
                        value += target[position + waver - destination.Pitch] & 15;
                    }

                    target[position] = (byte)((value / (lava ? 4 : 2)) | hue);
                }
            }
        }
    }

    internal static void RequireScreen(IndexedSurface surface, int width, int height)
    {
        if (surface.Width < width || surface.Height < height)
        {
            throw new ArgumentException($"Background operation requires at least {width} by {height} pixels.", nameof(surface));
        }
    }
}

public sealed class Starfield
{
    public struct Star
    {
        public byte Color;
        public ushort Position;
        public int Speed;
    }

    private readonly Star[] _stars = new Star[100];
    public ReadOnlySpan<Star> Stars => _stars;

    public void Initialize(int primaryPitch)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(primaryPitch, 320);
        for (var index = _stars.Length - 1; index >= 0; index--)
        {
            ref var star = ref _stars[index];
            star.Position = unchecked((ushort)(MersenneTwister.NextUInt32() % 320 + MersenneTwister.NextUInt32() % 200 * primaryPitch));
            star.Speed = (int)(MersenneTwister.NextUInt32() % 3 + 2);
            star.Color = (byte)(MersenneTwister.NextUInt32() % 16 + 0x90);
        }
    }

    public void UpdateAndDraw(IndexedSurface surface, int moveSpeed)
    {
        BackgroundRenderer.RequireScreen(surface, 320, 178);
        var pixels = surface.Pixels.Span;
        for (var index = _stars.Length - 1; index >= 0; index--)
        {
            ref var star = ref _stars[index];
            star.Position = unchecked((ushort)(star.Position + (star.Speed + moveSpeed) * surface.Pitch));
            if (star.Position >= 177L * surface.Pitch)
            {
                continue;
            }

            DrawPixel(pixels, star.Position, star.Color);
            if (star.Color - 4 >= 0x90)
            {
                var color = (byte)(star.Color - 4);
                DrawPixel(pixels, star.Position + 1, color);
                if (star.Position > 0)
                {
                    DrawPixel(pixels, star.Position - 1, color);
                }

                DrawPixel(pixels, star.Position + surface.Pitch, color);
                if (star.Position >= surface.Pitch)
                {
                    DrawPixel(pixels, star.Position - surface.Pitch, color);
                }
            }
        }
    }

    private static void DrawPixel(Span<byte> pixels, int position, byte color)
    {
        if (pixels[position] == 0)
        {
            pixels[position] = color;
        }
    }
}
