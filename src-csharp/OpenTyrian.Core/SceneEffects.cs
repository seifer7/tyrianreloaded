/*
 * OpenTyrian: A modern cross-platform port of Tyrian
 * Copyright (C) 2007-2013 The OpenTyrian Development Team
 * Distributed under the GNU General Public License, version 2 or later.
 * See COPYING for details.
 */

namespace OpenTyrian.Core;

public sealed class Explosion
{
    public uint Lifetime { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int DeltaX { get; set; }
    public int DeltaY { get; set; }
    public bool FixedPosition { get; set; }
    public bool FollowPlayer { get; set; }
    public uint Sprite { get; set; }
}

public sealed class RepeatingExplosion
{
    public uint Delay { get; set; }
    public uint Lifetime { get; set; }
    public uint X { get; set; }
    public uint Y { get; set; }
    public bool Big { get; set; }
}

public sealed class SuperPixel
{
    public uint X { get; set; }
    public uint Y { get; set; }
    public uint Z { get; set; }
    public int DeltaX { get; set; }
    public int DeltaY { get; set; }
    public byte Color { get; set; }
}

public sealed class SceneEffects
{
    public Explosion[] Explosions { get; } = Enumerable.Range(0, 200).Select(_ => new Explosion()).ToArray();
    public RepeatingExplosion[] Repeating { get; } = Enumerable.Range(0, 20).Select(_ => new RepeatingExplosion()).ToArray();
    public SuperPixel[] SuperPixels { get; } = Enumerable.Range(0, 101).Select(_ => new SuperPixel()).ToArray();
    public uint LastSuperPixel { get; set; }
    public ushort BackgroundMove { get; set; }
    public ushort ExplosionMove { get; set; }
    public short FollowX { get; set; }
    public short FollowY { get; set; }
    public bool Transparent { get; set; }
    public bool EnemyStillExploding { get; private set; }
    public byte[] SoundQueue { get; }

    public SceneEffects(byte[] soundQueue)
    {
        if (soundQueue.Length != 8) throw new ArgumentException("Scene effects require eight sound channels.", nameof(soundQueue));
        SoundQueue = soundQueue;
    }

    public void ResetLevelState()
    {
        foreach (var explosion in Explosions)
        {
            explosion.Lifetime = 0;
            explosion.X = explosion.Y = explosion.DeltaX = explosion.DeltaY = 0;
            explosion.FixedPosition = explosion.FollowPlayer = false;
            explosion.Sprite = 0;
        }
        foreach (var repeating in Repeating)
        {
            repeating.Delay = repeating.Lifetime = repeating.X = repeating.Y = 0;
            repeating.Big = false;
        }
        foreach (var pixel in SuperPixels)
        {
            pixel.X = pixel.Y = pixel.Z = 0;
            pixel.DeltaX = pixel.DeltaY = 0;
            pixel.Color = 0;
        }
        LastSuperPixel = 0;
        EnemyStillExploding = false;
        Array.Clear(SoundQueue);
        SoundQueue[3] = 33;
    }

    public void SetupExplosion(int x, int y, int deltaY, uint type, bool fixedPosition, bool followPlayer)
    {
        if (y <= -16 || y >= 190) return;
        var explosion = Explosions.FirstOrDefault(entry => entry.Lifetime == 0);
        if (explosion is null) return;
        if (type != 98 && type >= ExplosionData.Length)
            throw new ArgumentOutOfRangeException(nameof(type), "Explosion type is outside the native table.");
        explosion.X = x;
        explosion.Y = y;
        if (type == 6)
        {
            explosion.Y = unchecked(explosion.Y + 12);
            explosion.X = unchecked(explosion.X + 2);
        }
        else if (type == 98) type = 6;
        var data = ExplosionData[type];
        explosion.Sprite = data.Sprite;
        explosion.Lifetime = data.Lifetime;
        explosion.FollowPlayer = followPlayer;
        explosion.FixedPosition = fixedPosition;
        explosion.DeltaX = 0;
        explosion.DeltaY = deltaY;
    }

    public void SetupLarge(bool ground, byte count, short x, short y)
    {
        if (y < 0) return;
        SetupExplosion(x - 6, y - 14, 0, ground ? 7u : 2u, false, false);
        SetupExplosion(x + 6, y - 14, 0, ground ? 9u : 4u, false, false);
        SetupExplosion(x - 6, y, 0, ground ? 8u : 3u, false, false);
        SetupExplosion(x + 6, y, 0, ground ? 10u : 5u, false, false);
        var big = count > 10;
        if (big) count -= 10;
        if (count == 0) return;
        var repeating = Repeating.FirstOrDefault(entry => entry.Lifetime == 0);
        if (repeating is null) return;
        repeating.Lifetime = count;
        repeating.Delay = 2;
        repeating.X = unchecked((uint)x);
        repeating.Y = (uint)y;
        repeating.Big = big;
    }

    public void UpdateExplosions(Action<int, int, uint, bool> draw)
    {
        EnemyStillExploding = false;
        unchecked
        {
            for (var index = 0; index < Repeating.Length; index++)
            {
                var repeating = Repeating[index];
                if (repeating.Lifetime == 0) continue;
                EnemyStillExploding = true;
                if (repeating.Delay > 0) { repeating.Delay--; continue; }
                repeating.Y += (uint)(BackgroundMove + 1);
                var x = (ushort)(repeating.X + MersenneTwister.NextUInt32() % 24 - 12);
                var y = (ushort)(repeating.Y + MersenneTwister.NextUInt32() % 27 - 24);
                if (repeating.Big)
                {
                    SetupLarge(false, 2, (short)x, (short)y);
                    if (repeating.Lifetime == 1 || MersenneTwister.NextUInt32() % 5 == 1)
                        SoundQueue[7] = 11;
                    else SoundQueue[6] = 9;
                    repeating.Delay = 4 + MersenneTwister.NextUInt32() % 3;
                }
                else
                {
                    SetupExplosion(x, y, 0, 1, false, false);
                    SoundQueue[5] = 4;
                    repeating.Delay = 3;
                }
                repeating.Lifetime--;
            }

            foreach (var explosion in Explosions)
            {
                if (explosion.Lifetime == 0) continue;
                if (!explosion.FixedPosition)
                {
                    explosion.Sprite++;
                    explosion.Y += ExplosionMove;
                }
                else if (explosion.FollowPlayer)
                {
                    explosion.X += FollowX;
                    explosion.Y += FollowY;
                }
                explosion.Y += explosion.DeltaY;
                explosion.X += explosion.DeltaX;
                if (explosion.Y > 186) explosion.Lifetime = 0;
                else
                {
                    draw(explosion.X, explosion.Y, explosion.Sprite + 1, Transparent);
                    explosion.Lifetime--;
                }
            }
        }
    }

    public void UpdateExplosions(IndexedSurface surface, SpriteSheet sprites)
        => UpdateExplosions((x, y, frame, blend) => sprites.Blit(surface, x, y, checked((int)frame),
            blend ? SpriteEffect.Blend : SpriteEffect.Copy));

    public void SpawnSuperPixels(ushort x, ushort y, ushort count, byte width, byte color)
    {
        // The native loop counter is a byte, so larger counts never terminate.
        if (count > byte.MaxValue) throw new ArgumentOutOfRangeException(nameof(count), "Native superpixel counts above 255 cause an infinite loop.");
        unchecked
        {
            for (var i = 0; i < count; i++)
            {
                var angle = (float)(MersenneTwister.NextFloatExclusive() * (2 * Math.PI));
                var deltaY = (int)MathF.Round(MathF.Cos(angle) * MersenneTwister.NextFloatInclusive() * width, MidpointRounding.AwayFromZero);
                var deltaX = (int)MathF.Round(MathF.Sin(angle) * MersenneTwister.NextFloatInclusive() * width, MidpointRounding.AwayFromZero);
                if (++LastSuperPixel >= SuperPixels.Length) LastSuperPixel = 0;
                var pixel = SuperPixels[LastSuperPixel];
                pixel.X = (uint)(deltaX + x);
                pixel.Y = (uint)(deltaY + y);
                pixel.DeltaX = deltaX;
                pixel.DeltaY = deltaY + 1;
                pixel.Color = color;
                pixel.Z = 15;
            }
        }
    }

    public void DrawSuperPixels(IndexedSurface surface)
    {
        var pixels = surface.Pixels.Span;
        unchecked
        {
            for (var i = SuperPixels.Length - 1; i >= 0; i--)
            {
                var pixel = SuperPixels[i];
                if (pixel.Z == 0) continue;
                pixel.X += (uint)pixel.DeltaX;
                pixel.Y += (uint)pixel.DeltaY;
                if (pixel.X < surface.Width && pixel.Y < surface.Height)
                {
                    var position = (int)pixel.Y * surface.Pitch + (int)pixel.X;
                    pixels[position] = Blend(pixels[position], pixel.Z, pixel.Color);
                    var half = pixel.Z >> 1;
                    if (pixel.X > 0) pixels[position - 1] = Blend(pixels[position - 1], half, pixel.Color);
                    if (pixel.X < surface.Width - 1u) pixels[position + 1] = Blend(pixels[position + 1], half, pixel.Color);
                    if (pixel.Y > 0) pixels[position - surface.Pitch] = Blend(pixels[position - surface.Pitch], half, pixel.Color);
                    if (pixel.Y < surface.Height - 1u) pixels[position + surface.Pitch] = Blend(pixels[position + surface.Pitch], half, pixel.Color);
                }
                pixel.Z--;
            }
        }
    }

    public SpritePlayerShotEffects CreateProjectileEffects(IndexedSurface surface, SpriteSheet first, SpriteSheet second, SpriteTable options)
        => new(surface, first, second, options,
            (x, y, type) => SetupExplosion(x, y, 0, type, false, false),
            (x, y, count, width, color) => SpawnSuperPixels(unchecked((ushort)x), unchecked((ushort)y), count, width, color));

    private static byte Blend(byte background, uint brightness, byte color)
        => unchecked((byte)((((background & 15u) + brightness) >> 1) + color));

    private static readonly (ushort Sprite, byte Lifetime)[] ExplosionData =
    [
        (144,7), (120,12), (190,12), (209,12), (152,12), (171,12), (133,7),
        (1,12), (20,12), (39,12), (58,12), (110,3), (76,7), (91,3), (227,3),
        (230,3), (233,3), (252,3), (246,3), (249,3), (265,3), (268,3),
        (271,3), (236,3), (239,3), (242,3), (261,3), (274,3), (277,3),
        (280,3), (299,3), (284,3), (287,3), (290,3), (293,3), (165,8),
        (184,8), (203,8), (222,8), (168,8), (187,8), (206,8), (225,10),
        (169,10), (188,10), (207,20), (226,14), (170,14), (189,14), (208,14),
        (246,14), (227,14), (265,14)
    ];
}
