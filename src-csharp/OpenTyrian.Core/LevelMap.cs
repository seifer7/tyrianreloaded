/*
 * OpenTyrian: A modern cross-platform port of Tyrian
 * Copyright (C) 2007-2013 The OpenTyrian Development Team
 * Distributed under the GNU General Public License, version 2 or later.
 * See COPYING for details.
 */

using System.Buffers.Binary;

namespace OpenTyrian.Core;

public sealed record LevelMapHeader(char MapFile, char ShapeFile, ushort FirstX, ushort SecondX,
    ushort ThirdX, ushort[] Enemies, LevelEvent[] Events);

public sealed class LevelTileSet
{
    public byte[]?[] Shapes { get; } = new byte[128][];
    public byte[] Fill { get; } = new byte[128];
    internal bool[] Defined { get; } = new bool[128];
}

public sealed class LevelMap
{
    public const ushort SentinelTime = 65500;
    public LevelMapHeader Header { get; }
    public LevelTileSet[] TileSets { get; }
    public byte[]?[][] Maps { get; }

    private LevelMap(LevelMapHeader header, LevelTileSet[] tileSets, byte[]?[][] maps)
    {
        Header = header;
        TileSets = tileSets;
        Maps = maps;
    }

    public static LevelMapHeader ReadHeader(Stream stream)
    {
        Span<byte> characters = stackalloc byte[2];
        Span<ushort> words = stackalloc ushort[4];
        BinaryFile.ReadUInt8(stream, characters);
        BinaryFile.ReadUInt16(stream, words);
        if (words[3] > 40) throw new InvalidDataException("Level enemy list exceeds the native 40-entry limit.");
        var enemies = new ushort[words[3]];
        BinaryFile.ReadUInt16(stream, enemies);
        Span<ushort> count = stackalloc ushort[1];
        BinaryFile.ReadUInt16(stream, count);
        if (count[0] >= 2500) throw new InvalidDataException("Level events leave no room for the native sentinel.");
        var events = new LevelEvent[count[0]];
        Span<byte> record = stackalloc byte[11];
        for (var i = 0; i < events.Length; i++)
        {
            BinaryFile.ReadExactly(stream, record);
            events[i] = new LevelEvent
            {
                Time = BinaryPrimitives.ReadUInt16LittleEndian(record),
                Type = record[2],
                Data = BinaryPrimitives.ReadInt16LittleEndian(record[3..]),
                Data2 = BinaryPrimitives.ReadInt16LittleEndian(record[5..]),
                Data3 = unchecked((sbyte)record[7]),
                Data5 = unchecked((sbyte)record[8]),
                Data6 = unchecked((sbyte)record[9]),
                Data4 = record[10]
            };
        }
        return new LevelMapHeader((char)characters[0], (char)characters[1], words[0], words[1], words[2], enemies, events);
    }

    public static LevelTileSet[] LoadShapes(Stream level, Stream shapes)
    {
        var references = new ushort[3][];
        for (var layer = 0; layer < references.Length; layer++)
        {
            references[layer] = new ushort[128];
            BinaryFile.ReadUInt16(level, references[layer]);
            for (var i = 0; i < references[layer].Length; i++)
                // Shape IDs are stored big-endian, unlike the level header.
                references[layer][i] = BinaryPrimitives.ReverseEndianness(references[layer][i]);
        }
        LevelTileSet[] sets = [new(), new(), new()];
        Span<byte> shape = stackalloc byte[24 * 28];
        for (var index = 1; index <= 600; index++)
        {
            var blank = BinaryFile.ReadBoolean(shapes);
            if (blank) shape.Clear();
            else BinaryFile.ReadExactly(shapes, shape);
            for (var layer = 0; layer < sets.Length; layer++)
            {
                for (var i = 0; i < 72; i++)
                {
                    if (references[layer][i] != index) continue;
                    sets[layer].Defined[i] = true;
                    if (layer == 0 || !blank && (layer == 1 ? i != 71 : i < 70))
                    {
                        sets[layer].Shapes[i] = shape.ToArray();
                        if (layer > 0) sets[layer].Fill[i] = shape[..336].Contains((byte)0) ? (byte)0 : (byte)1;
                    }
                }
            }
        }
        return sets;
    }

    public static byte[]?[] LoadMapLayer(Stream stream, LevelTileSet shapes, ushort width, ushort height)
    {
        var length = (int)width * height;
        if (length > 15 * 600) throw new InvalidDataException("Level map exceeds the native layer buffer.");
        var indices = new byte[length];
        BinaryFile.ReadUInt8(stream, indices);
        var tiles = new byte[]?[length];
        for (var i = 0; i < tiles.Length; i++)
        {
            var index = indices[i];
            if (index >= shapes.Defined.Length || !shapes.Defined[index])
                throw new InvalidDataException($"Level map uses undefined shape reference {index}.");
            tiles[i] = shapes.Shapes[index];
        }
        return tiles;
    }

    public static LevelMap Load(Stream level, Func<char, Stream> openShapes)
    {
        var header = ReadHeader(level);
        LevelTileSet[] shapes;
        using (var stream = openShapes(header.ShapeFile)) shapes = LoadShapes(level, stream);
        byte[]?[][] maps =
        [
            LoadMapLayer(level, shapes[0], 14, 300),
            LoadMapLayer(level, shapes[1], 14, 600),
            LoadMapLayer(level, shapes[2], 15, 600)
        ];
        return new LevelMap(header, shapes, maps);
    }

    public static LevelMap LoadSelected(DataFiles files, EpisodeState episode, byte levelNumber)
    {
        var index = (levelNumber - 1) * 2;
        if (index < 0 || index >= episode.Levels.Count)
            throw new InvalidDataException("Selected level is outside the episode directory.");
        using var stream = files.OpenRequired(files.Directory, episode.LevelFile);
        var position = episode.Levels.Positions[index];
        if (position < 0 || position >= stream.Length) throw new InvalidDataException("Selected level offset is outside its archive.");
        stream.Position = position;
        return Load(stream, shape => files.OpenRequired(files.Directory, $"shapes{char.ToLowerInvariant(shape)}.dat"));
    }

    public void Apply(BackgroundRenderer backgrounds, LevelEvents events, LevelBackgroundSceneState scene)
    {
        ArgumentNullException.ThrowIfNull(backgrounds);
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(scene);
        if (scene.LevelEnemies.Length < Header.Enemies.Length)
            throw new ArgumentException("Level background state cannot hold the loaded enemy roster.", nameof(scene));

        backgrounds.First.Tiles = Maps[0];
        backgrounds.Second.Tiles = Maps[1];
        backgrounds.Third.Tiles = Maps[2];
        events.MapX = Header.FirstX;
        events.MapX3 = Header.ThirdX;
        events.Events = Header.Events;
        Array.Clear(scene.LevelEnemies);
        Header.Enemies.CopyTo(scene.LevelEnemies, 0);
        scene.LevelEnemyMaximum = checked((ushort)Header.Enemies.Length);
    }
}
