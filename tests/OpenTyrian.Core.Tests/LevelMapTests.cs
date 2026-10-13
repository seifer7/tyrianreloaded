using System.Buffers.Binary;
using System.Diagnostics;
using System.Text;
using OpenTyrian.Core;

namespace OpenTyrian.Core.Tests;

internal static class LevelMapTests
{
    public static void Run(string? executable = null)
    {
        var directory = Path.Combine(Path.GetTempPath(), $"opentyrian-level-map-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var archivePath = Path.Combine(directory, "tyrian1.lvl");
        var shapePaths = new[] { Path.Combine(directory, "shapesa.dat"), Path.Combine(directory, "shapesb.dat") };
        try
        {
            for (var mode = 0; mode < 3; mode++)
            {
                var first = BuildLevel(mode * 2, 'A');
                var second = BuildLevel(mode * 2 + 1, 'B');
                var shapes = new[] { BuildShapes(mode * 2), BuildShapes(mode * 2 + 1) };
                using var archive = new MemoryStream();
                BinaryFile.WriteUInt16(archive, 4);
                foreach (var offset in new[] { 18, 18 + first.Length, 18 + first.Length, 18 + first.Length + second.Length })
                    BinaryFile.WriteInt32(archive, offset);
                archive.Write(first); archive.Write(second);
                File.WriteAllBytes(archivePath, archive.ToArray());
                for (var i = 0; i < shapePaths.Length; i++) File.WriteAllBytes(shapePaths[i], shapes[i]);
                var files = new DataFiles(directory, directory);
                var episode = new EpisodeState(files);
                episode.Initialize(1);
                for (byte number = 1; number <= 2; number++)
                {
                    var map = LevelMap.LoadSelected(files, episode, number);
                    var levelData = number == 1 ? first : second;
                    using var stream = new MemoryStream(levelData);
                    var direct = LevelMap.Load(stream, shape =>
                    {
                        Check(shape == (number == 1 ? 'A' : 'B'), "Shape-file header differs.");
                        return new MemoryStream(shapes[number - 1]);
                    });
                    Check(stream.Position == levelData.Length && Dump(map) == Dump(direct), "Selected-level seek or stream consumption differs.");
                    Check(map.Header.MapFile == 'M' && map.Header.FirstX == 65535 && map.Header.SecondX == 127 &&
                        map.Header.ThirdX == number, "Map header fields differ.");
                    Check(!ReferenceEquals(map.TileSets[0].Shapes[2], map.TileSets[0].Shapes[3]) &&
                        map.TileSets[0].Shapes[2]!.SequenceEqual(map.TileSets[0].Shapes[3]!),
                        "Native shape slots must own separate copies of duplicate shape references.");
                    Check(map.Maps.Select(layer => layer.Length).SequenceEqual(new[] { 4200, 8400, 9000 }),
                        "Layer dimensions differ.");
                    if (executable is not null) CompareNative(executable, directory, number, Dump(map));
                    TestIntegration(map);
                }
                Throws<InvalidDataException>(() => LevelMap.LoadSelected(files, episode, 0));
                Throws<InvalidDataException>(() => LevelMap.LoadSelected(files, episode, 3));
            }
            TestFailures();
        }
        finally
        {
            File.Delete(archivePath);
            foreach (var path in shapePaths) File.Delete(path);
            Directory.Delete(directory);
        }
    }

    internal static byte[] BuildLevel(int mode, char shapeFile)
    {
        using var output = new MemoryStream();
        output.WriteByte((byte)'M'); output.WriteByte((byte)shapeFile);
        BinaryFile.WriteUInt16(output, 65535);
        BinaryFile.WriteUInt16(output, 127);
        BinaryFile.WriteUInt16(output, (ushort)(mode % 2 + 1));
        var enemies = mode >= 4 ? 40 : mode;
        BinaryFile.WriteUInt16(output, (ushort)enemies);
        for (var i = 0; i < enemies; i++) BinaryFile.WriteUInt16(output, (ushort)(65535 - i * 17));
        var events = mode >= 4 ? 2499 : mode;
        BinaryFile.WriteUInt16(output, (ushort)events);
        for (var i = 0; i < events; i++)
        {
            BinaryFile.WriteUInt16(output, (ushort)(i * 23));
            output.WriteByte((byte)(i % 83));
            BinaryFile.WriteInt16(output, unchecked((short)(short.MinValue + i * 37)));
            BinaryFile.WriteInt16(output, unchecked((short)(short.MaxValue - i * 31)));
            output.WriteByte(unchecked((byte)(-128 + i)));
            output.WriteByte(unchecked((byte)(127 - i)));
            output.WriteByte(unchecked((byte)(-99 + i)));
            output.WriteByte(unchecked((byte)(255 - i)));
        }
        Span<byte> word = stackalloc byte[2];
        for (var layer = 0; layer < 3; layer++)
        {
            for (var i = 0; i < 128; i++)
            {
                var index = i == 3 ? 2 : i;
                var id = i == 71 ? 600 : (index * 7 + layer * 11 + mode) % 600 + 1;
                BinaryPrimitives.WriteUInt16BigEndian(word, (ushort)id);
                output.Write(word);
            }
        }
        foreach (var length in new[] { 4200, 8400, 9000 })
            for (var i = 0; i < length; i++) output.WriteByte((byte)((i * 5 + mode) % 72));
        return output.ToArray();
    }

    internal static byte[] BuildShapes(int mode)
    {
        using var output = new MemoryStream();
        for (var index = 1; index <= 600; index++)
        {
            var blank = index % 11 == 0;
            BinaryFile.WriteBoolean(output, blank);
            if (blank) continue;
            for (var i = 0; i < 672; i++)
            {
                var value = index % 5 == 0 ? (i == 671 ? 0 : 10) : (i * 13 + index * 7 + mode * 3) & 255;
                if (index % 7 == 0 && i == 335) value = 0;
                output.WriteByte((byte)value);
            }
        }
        return output.ToArray();
    }

    private static void TestIntegration(LevelMap map)
    {
        var definitions = new ItemDefinitions();
        definitions.ParseBundle(Encoding.Latin1.GetBytes(LevelEventTests.Fixture()), "level-map.tyrr");
        var items = ItemDatabase.FromDefinitions(definitions);
        using var warnings = new StringWriter();
        var enemies = new EnemySystem(items, warnings);
        var players = new[] { new PlayerState(0), new PlayerState(1) };
        var background = new BackgroundRenderer();
        var events = new LevelEvents(items, enemies, players, background, new SceneEffects(new byte[8]));
        map.Apply(background, events);
        Check(ReferenceEquals(background.First.Tiles, map.Maps[0]) && ReferenceEquals(background.Second.Tiles, map.Maps[1]) &&
            ReferenceEquals(background.Third.Tiles, map.Maps[2]) && ReferenceEquals(events.Events, map.Header.Events) &&
            events.MapX == 65535 && events.MapX3 == map.Header.ThirdX, "Loaded map and event gameplay integration differs.");
        background.First.MapOffset = 14 * 5;
        var screen = new IndexedSurface(320, 200, 320, warnings);
        background.DrawFirst(screen);
        Check(screen.Pixels.Span.ContainsAnyExcept((byte)0), "Loaded map tiles were not rendered.");
    }

    private static string Dump(LevelMap map)
    {
        var header = map.Header;
        var output = new StringBuilder();
        output.Append($"header:{header.FirstX},{header.SecondX},{header.ThirdX},{header.Enemies.Length},{header.Events.Length}\n");
        output.Append($"enemies:{string.Join(',', header.Enemies)}\n");
        foreach (var record in header.Events)
            output.Append($"event:{record.Time},{record.Type},{record.Data},{record.Data2},{record.Data3},{record.Data4},{record.Data5},{record.Data6}\n");
        output.Append($"event:{LevelMap.SentinelTime},0,0,0,0,0,0,0\n");
        for (var layer = 0; layer < 3; layer++)
            for (var i = 0; i < 72; i++)
            {
                var shape = map.TileSets[layer].Shapes[i];
                output.Append($"shape:{layer},{i},{(shape is null ? 0 : Hash(shape))},{map.TileSets[layer].Fill[i]}\n");
            }
        for (var layer = 0; layer < 3; layer++)
        {
            output.Append($"map:{layer}");
            foreach (var tile in map.Maps[layer])
                output.Append($",{(tile is null ? -1 : Array.IndexOf(map.TileSets[layer].Shapes, tile))}");
            output.Append('\n');
        }
        return output.ToString();
    }

    private static uint Hash(ReadOnlySpan<byte> data)
    {
        uint value = 2166136261;
        foreach (var pixel in data) value = unchecked((value ^ pixel) * 16777619);
        return value;
    }

    private static void CompareNative(string executable, string directory, byte number, string expected)
    {
        var start = new ProcessStartInfo(executable) { RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add(directory); start.ArgumentList.Add(number.ToString(System.Globalization.CultureInfo.InvariantCulture));
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Cannot start level-map oracle.");
        var output = process.StandardOutput.ReadToEndAsync();
        var errors = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(30000))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException("Level-map oracle timed out.");
        }
        var actual = output.GetAwaiter().GetResult().Replace("\r\n", "\n", StringComparison.Ordinal);
        var error = errors.GetAwaiter().GetResult();
        Check(process.ExitCode == 0 && actual == expected && error.Length == 0,
            $"Native level-map parity differs for level {number}: {error}");
    }

    private static void TestFailures()
    {
        var level = BuildLevel(0, 'A');
        var shapes = BuildShapes(0);
        foreach (var length in new[] { 0, 1, 5, 10, 13, 770, 10000, level.Length - 1 })
        {
            using var stream = new MemoryStream(level[..length]);
            Throws<EndOfStreamException>(() => LevelMap.Load(stream, _ => new MemoryStream(shapes)));
        }
        using (var stream = new MemoryStream(level))
            Throws<EndOfStreamException>(() => LevelMap.Load(stream, _ => new MemoryStream(shapes[..^1])));
        var invalidEnemies = level.ToArray();
        BinaryPrimitives.WriteUInt16LittleEndian(invalidEnemies.AsSpan(8), 41);
        using (var stream = new MemoryStream(invalidEnemies))
            Throws<InvalidDataException>(() => LevelMap.ReadHeader(stream));
        var invalidEvents = level.ToArray();
        BinaryPrimitives.WriteUInt16LittleEndian(invalidEvents.AsSpan(10), 2500);
        using (var stream = new MemoryStream(invalidEvents))
            Throws<InvalidDataException>(() => LevelMap.ReadHeader(stream));
        var invalidReference = level.ToArray();
        invalidReference[12] = invalidReference[13] = 0;
        using (var stream = new MemoryStream(invalidReference))
            Throws<InvalidDataException>(() => LevelMap.Load(stream, _ => new MemoryStream(shapes)));
        foreach (var index in new byte[] { 72, 128, 255 })
        {
            var invalidMap = level.ToArray();
            invalidMap[12 + 3 * 128 * 2] = index;
            using var stream = new MemoryStream(invalidMap);
            Throws<InvalidDataException>(() => LevelMap.Load(stream, _ => new MemoryStream(shapes)));
        }
        using var empty = new MemoryStream();
        Throws<InvalidDataException>(() => LevelMap.LoadMapLayer(empty, new LevelTileSet(), 1000, 1000));
    }

    private static void Throws<T>(Action action) where T : Exception
    {
        try { action(); }
        catch (T) { return; }
        throw new InvalidOperationException($"Expected {typeof(T).Name}.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
