using System.Buffers.Binary;
using OpenTyrian.Core;

namespace OpenTyrian.Core.Tests;

internal static class SpriteTests
{
    public static void Run()
    {
        using var warnings = new StringWriter();
        var table = new SpriteTable();
        using var encoded = new MemoryStream();
        BinaryFile.WriteUInt16(encoded, 2);
        BinaryFile.WriteBoolean(encoded, true);
        BinaryFile.WriteUInt16(encoded, 4);
        BinaryFile.WriteUInt16(encoded, 2);
        byte[] sprite = [0xAE, 253, 0xB4, 254, 255, 2, 0xC8, 0xD1];
        BinaryFile.WriteUInt16(encoded, (ushort)sprite.Length);
        BinaryFile.WriteExactly(encoded, sprite);
        BinaryFile.WriteBoolean(encoded, false);
        encoded.Position = 0;
        table.Load(encoded);
        Check(table.Count == 2 && table.Width(0) == 4 && table.Height(0) == 2 && !table.Exists(1) && table.Width(1) == 0, "Sprite metadata differs.");
        var surface = new IndexedSurface(8, 4, 10, warnings);
        table.Blit(surface, 1, 1, 0);
        Check(surface.Pixels.Span[11] == 0xAE && surface.Pixels.Span[12] == 0 && surface.Pixels.Span[13] == 0xB4 && surface.Pixels.Span[23] == 0xC8 && surface.Pixels.Span[24] == 0xD1, "Sprite transparency or row advance differs.");
        foreach (var (effect, hue, value, expected) in new (SpriteEffect, byte, sbyte, byte)[]
        {
            (SpriteEffect.Blend, 0, 0, 0xAA),
            (SpriteEffect.HueUnsafe, 2, 3, 0x31),
            (SpriteEffect.Hue, 2, 3, 0x2F),
            (SpriteEffect.Hue, 2, -15, 0x20),
            (SpriteEffect.HueBlend, 2, 3, 0x2A),
            (SpriteEffect.Darken, 0, 0, 0xA3),
            (SpriteEffect.Black, 0, 0, 0)
        })
        {
            surface.Pixels.Span.Fill(0xA6);
            table.Blit(surface, 0, 0, 0, effect, hue, value);
            Check(surface.Pixels.Span[0] == expected, $"Sprite effect {effect} differs.");
        }

        table.Blit(surface, 0, -1, 0);
        Check(surface.Pixels.Span[2] == 0xC8, "Sprite top clipping differs.");
        table.Clear();
        Check(table.Count == 0 && !table.Exists(0), "Sprite release differs.");

        var sheet = new SpriteSheet();
        // One-based pointer table with 21 identical 12-wide compressed sprites.
        var sheetData = new byte[42 + 7];
        for (var index = 0; index < 21; index++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(sheetData.AsSpan(index * 2), 42);
        }

        new byte[] { 0x21, 0xBE, 0xC4, 0x09, 0x10, 0xD8, 0x0F }.CopyTo(sheetData, 42);
        using var sheetStream = new MemoryStream(sheetData);
        sheet.Load(sheetStream, sheetData.Length);
        var destination = new IndexedSurface(30, 30, 32, warnings);
        destination.Pixels.Span.Fill(0xA6);
        sheet.Blit(destination, 0, 0, 1);
        Check(destination.Pixels.Span[1] == 0xBE && destination.Pixels.Span[2] == 0xC4 && destination.Pixels.Span[32] == 0xD8, "Compressed sprite rows differ.");
        sheet.Blit(destination, -2, -1, 1, SpriteEffect.Filter, 0x50, clip: true);
        Check(destination.Pixels.Span[0] == 0xA6, "Clipped sprite modified invisible pixels.");
        foreach (var effect in new[] { SpriteEffect.Copy, SpriteEffect.Blend, SpriteEffect.Darken, SpriteEffect.Filter })
        {
            foreach (var clipped in new[] { false, true })
            {
                destination.Pixels.Span.Fill(0xA6);
                sheet.Blit2x2(destination, 0, 0, 1, effect, 0x50, clipped);
                var expected = effect switch { SpriteEffect.Copy => 0xBE, SpriteEffect.Blend => 0xBA, SpriteEffect.Darken => 0xA3, _ => 0x5E };
                foreach (var position in new[] { 1, 13, 14 * 32 + 1, 14 * 32 + 13 })
                {
                    Check(destination.Pixels.Span[position] == expected, "Composite sprite effect or placement differs.");
                }
            }
        }

        encoded.Position = 0;
        table.Load(encoded);
        var explosions = new List<(int X, int Y, byte Type)>();
        var pixels = new List<(int X, int Y, ushort Count, byte Width, byte Color)>();
        var effects = new SpritePlayerShotEffects(destination, sheet, sheet, table,
            (x, y, type) => explosions.Add((x, y, type)), (x, y, count, width, color) => pixels.Add((x, y, count, width, color)));
        destination.Pixels.Span.Fill(0xA6);
        effects.Draw(8, 0, 0, 1, false);
        effects.Draw(12, 12, 0, 1, false);
        Check(destination.Pixels.Span[1] == 0xBE && destination.Pixels.Span[13] == 0xBE, "Projectile sprite-sheet routing differs.");
        destination.Pixels.Span.Fill(0xA6);
        effects.Draw(8, 0, 0, 1, true);
        Check(destination.Pixels.Span[1] == 0xA3, "Projectile shadow adapter differs.");
        Check(effects.DrawSpecial(0, 0, 0) == ((ushort)4, (ushort)2) && destination.Pixels.Span[0] == 0xAA,
            "Projectile special sprite dimensions or blend differ.");
        effects.Explosion(1, 2, 3);
        effects.SuperPixels(4, 5, 5, 3, 6);
        Check(explosions.SequenceEqual(new[] { (1, 2, (byte)3) }) && pixels.SequenceEqual(new[] { (4, 5, (ushort)5, (byte)3, (byte)6) }),
            "Projectile effect callback routing differs.");
        var scene = new SceneEffects(new byte[8]);
        var integrated = scene.CreateProjectileEffects(destination, sheet, sheet, table);
        integrated.Explosion(10, 20, 98);
        MersenneTwister.Seed(0);
        integrated.SuperPixels(10, 20, 5, 3, 0x40);
        Check(scene.Explosions[0].Lifetime == 7 && scene.Explosions[0].Sprite == 133 &&
            scene.SuperPixels.Count(pixel => pixel.Z == 15) == 5, "Projectile scene-effect integration differs.");
        scene.Explosions[0].X = scene.Explosions[0].Y = 0;
        scene.Explosions[0].Sprite = 0;
        scene.Explosions[0].FixedPosition = true;
        destination.Pixels.Span.Fill(0xA6);
        scene.UpdateExplosions(destination, sheet);
        Check(destination.Pixels.Span[1] == 0xBE && scene.Explosions[0].Lifetime == 6,
            "Explosion sprite adapter differs.");
        sheet.Clear();
        Check(sheet.Size == 0, "Compressed sprite release differs.");
        TestFailures();
        TestFileLoading(encoded.ToArray(), sheetData);
        TestMainShapes();
    }

    private static void TestFailures()
    {
        var table = new SpriteTable();
        using var invalidCount = new MemoryStream(new byte[] { 152, 0 });
        Throws<InvalidDataException>(() => table.Load(invalidCount));
        using var truncated = new MemoryStream(new byte[] { 1, 0, 1 });
        Throws<EndOfStreamException>(() => table.Load(truncated));
        using var run = new MemoryStream(new byte[] { 1, 0, 1, 1, 0, 1, 0, 1, 0, 255 });
        table.Load(run);
        using var warnings = new StringWriter();
        var surface = new IndexedSurface(10, 3, 12, warnings);
        Throws<InvalidDataException>(() => table.Blit(surface, 0, 0, 0));
        var sheet = new SpriteSheet();
        using var missingStop = new MemoryStream(new byte[] { 2, 0, 0x10, 0xAB });
        sheet.Load(missingStop, 4);
        Throws<InvalidDataException>(() => sheet.Blit(surface, 0, 0, 1));
        Throws<ArgumentOutOfRangeException>(() => sheet.Blit(surface, 0, 0, 0));
        using var invalidOffset = new MemoryStream(new byte[] { 255, 255 });
        sheet.Load(invalidOffset, 2);
        Throws<InvalidDataException>(() => sheet.Blit(surface, 0, 0, 1));
        using var invalidArchive = new MemoryStream(new byte[] { 11, 0 });
        Throws<InvalidDataException>(() => new MainShapeTables().Load(invalidArchive));
    }

    private static void TestFileLoading(byte[] tableData, byte[] sheetData)
    {
        var directory = Path.Combine(Path.GetTempPath(), $"opentyrian-sprites-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var tablePath = Path.Combine(directory, "table.shp");
        var sheetPath = Path.Combine(directory, "newsha.shp");
        try
        {
            File.WriteAllBytes(tablePath, tableData);
            File.WriteAllBytes(sheetPath, sheetData);
            var table = new SpriteTable();
            table.LoadFile(tablePath);
            Check(table.Count == 2 && table.Exists(0), "Sprite file loader differs.");
            var sheet = new SpriteSheet();
            sheet.LoadFile(directory, 'A');
            Check(sheet.Size == sheetData.Length, "Compressed sprite filename/loading differs.");
        }
        finally
        {
            File.Delete(tablePath);
            File.Delete(sheetPath);
            Directory.Delete(directory);
        }
    }

    private static void TestMainShapes()
    {
        using var stream = new MemoryStream();
        BinaryFile.WriteUInt16(stream, 12);
        for (var index = 0; index < 12; index++)
        {
            BinaryFile.WriteInt32(stream, index < 7 ? 50 + index * 2 : 64 + (index - 7) * 3);
        }

        for (var index = 0; index < 7; index++)
        {
            BinaryFile.WriteUInt16(stream, 0);
        }

        for (var index = 0; index < 5; index++)
        {
            BinaryFile.WriteUInt8(stream, new byte[] { 2, 0, 0x0F });
        }

        stream.Position = 0;
        var main = new MainShapeTables();
        main.Load(stream);
        Check(main.Tables.All(table => table.Count == 0) && main.Sheets.All(sheet => sheet.Size == 3), "Main shape archive differs.");
        main.Clear();
        Check(main.Sheets.All(sheet => sheet.Size == 0), "Main shape archive release differs.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static void Throws<T>(Action action) where T : Exception
    {
        try
        {
            action();
        }
        catch (T)
        {
            return;
        }

        throw new InvalidOperationException($"Expected {typeof(T).Name}.");
    }
}
