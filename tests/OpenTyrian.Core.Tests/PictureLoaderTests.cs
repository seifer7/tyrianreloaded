using System.Buffers.Binary;
using OpenTyrian.Core;

namespace OpenTyrian.Core.Tests;

internal static class PictureLoaderTests
{
    public static void Run()
    {
        using var warnings = new StringWriter();
        var surface = new IndexedSurface(320, 200, 324, warnings);
        surface.Pixels.Span.Fill(0xEE);
        var encoded = new List<byte>();
        for (var row = 0; row < 200; row++)
        {
            for (var run = 0; run < 5; run++)
            {
                encoded.Add(0xFF);
                encoded.Add((byte)row);
            }

            encoded.Add(0xC5);
            encoded.Add((byte)row);
        }

        var pcx = new byte[128 + encoded.Count + 769];
        encoded.CopyTo(pcx, 128);
        pcx[^769] = 12;
        for (var index = 0; index < 256; index++)
        {
            pcx[pcx.Length - 768 + index * 3] = (byte)index;
            pcx[pcx.Length - 767 + index * 3] = 2;
            pcx[pcx.Length - 766 + index * 3] = 3;
        }

        var colors = new PaletteColor[256];
        using var input = new MemoryStream(pcx);
        PictureLoader.LoadPcx(input, surface, colors);
        Check(colors[255] == new PaletteColor(255, 2, 3), "PCX palette differs.");
        for (var row = 0; row < 200; row++)
        {
            Check(surface.Pixels.Span.Slice(row * 324, 320).ToArray().All(value => value == row), "PCX row differs.");
            Check(surface.Pixels.Span[row * 324 + 320] == 0xEE, "PCX pitch padding changed.");
        }

        pcx[^769] = 0;
        input.Position = 0;
        var preserved = colors[255];
        PictureLoader.LoadPcx(new MemoryStream(pcx), surface, colors);
        Check(colors[255] == preserved, "Missing palette marker must preserve colors.");

        var archive = new byte[2 + 13 * 4 + encoded.Count * 13];
        BinaryPrimitives.WriteUInt16LittleEndian(archive, 13);
        for (var index = 0; index < 13; index++)
        {
            var offset = 54 + encoded.Count * index;
            BinaryPrimitives.WriteInt32LittleEndian(archive.AsSpan(2 + index * 4), offset);
            encoded.CopyTo(archive, offset);
        }

        var palettes = Enumerable.Range(0, 23).Select(index => Enumerable.Repeat(new PaletteColor((byte)index, 2, 3), 256).ToArray()).ToArray();
        var active = new PaletteSystem(color => color.Red);
        using var pictures = new MemoryStream(archive);
        PictureLoader.LoadPicture(pictures, 2, surface, colors, palettes, false, active);
        Check(colors[0].Red == 7 && active.Active.Span[0].Red == 0, "Working/stored palette differs.");
        PictureLoader.LoadPicture(pictures, 13, surface, colors, palettes, true, active);
        Check(colors[0].Red == 5 && active.Active.Span[0].Red == 5, "Stored picture palette differs.");
        Check(PictureLoader.FacePaletteIndices.SequenceEqual(new byte[] { 1, 2, 3, 4, 6, 9, 11, 12, 16, 13, 14, 15 }), "Face palette table differs.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
