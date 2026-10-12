/*
 * OpenTyrian: A modern cross-platform port of Tyrian
 * Copyright (C) 2007-2013 The OpenTyrian Development Team
 * Distributed under the GNU General Public License, version 2 or later.
 * See COPYING for details.
 */

using System.Buffers.Binary;
using System.Text;

namespace OpenTyrian.Core;

public sealed class DemoSession(DataFiles files, ItemDatabase items, PlayerState player, PlayerInput input,
    EnemySystem enemies, Action<byte> initializeEpisode, TextWriter output, TextWriter errors) : IDisposable
{
    public byte Number { get; set; }
    public byte Episode { get; set; }
    public string LevelName { get; set; } = "";
    public byte LevelFileNumber { get; set; }
    public byte InitialEpisode { get; set; }
    public byte Song { get; set; }
    public ushort MainLevel { get; set; }
    public bool BonusLevel { get; set; }
    public Stream? Stream { get; private set; }

    public bool LoadNext()
    {
        Number = unchecked((byte)(Number + 1));
        var name = $"demo.{Number}";
        if (!files.Exists(files.Directory, name)) { Number = 1; name = "demo.1"; }
        Dispose();
        Stream = files.OpenWarn(files.Directory, name, errors);
        if (Stream is null) { input.PlayDemo = false; MainLevel = 0; return false; }
        enemies.Difficulty = 2; BonusLevel = false;
        Episode = ReadByte();
        initializeEpisode(Episode);
        if (Episode is > 0 and < 5)
        {
            Span<byte> nameBytes = stackalloc byte[10]; BinaryFile.ReadExactly(Stream, nameBytes);
            var end = nameBytes.IndexOf((byte)0);
            LevelName = Encoding.Latin1.GetString(end < 0 ? nameBytes : nameBytes[..end]);
            LevelFileNumber = ReadByte();
            player.Items.Weapons[0].Id = ReadItem(ItemType.WeaponFront);
            player.Items.Weapons[1].Id = ReadItem(ItemType.WeaponRear);
            player.Items.SuperArcadeMode = ReadByte();
            player.Items.Sidekicks[0] = ReadItem(ItemType.SidekickLeft);
            player.Items.Sidekicks[1] = ReadItem(ItemType.SidekickRight);
            player.Items.Generator = ReadItem(ItemType.Generator);
            player.Items.SidekickLevel = ReadByte(); player.Items.SidekickSeries = ReadByte();
            InitialEpisode = ReadByte();
            player.Items.Shield = ReadItem(ItemType.Shield);
            player.Items.Special = ReadItem(ItemType.Special);
            player.Items.Ship = ReadItem(ItemType.Ship);
            player.Items.Weapons[0].Power = ReadByte(); player.Items.Weapons[1].Power = ReadByte();
        }
        Span<byte> unused = stackalloc byte[3]; BinaryFile.ReadExactly(Stream, unused);
        Song = ReadByte();
        input.DemoKeys = 0;
        Span<byte> wait = stackalloc byte[2]; wait.Clear(); BinaryFile.ReadBytes(Stream, wait);
        input.DemoWait = BinaryPrimitives.ReadUInt16BigEndian(wait);
        output.WriteLine($"loaded demo '{name}'");
        return true;
    }

    public string RecordLevel(string directory)
    {
        Dispose();
        string? name = null;
        for (var number = 0; number <= byte.MaxValue; number++)
        {
            var candidate = $"demorec.{number}";
            if (files.Exists(directory, candidate)) continue;
            Stream = files.OpenWarn(directory, candidate, errors, FileMode.CreateNew, FileAccess.Write);
            if (Stream is null) throw new IOException($"Cannot create demo recording '{candidate}'.");
            name = candidate;
            break;
        }
        if (name is null) throw new IOException("All 256 native demo recording slots are occupied.");
        Stream!.WriteByte(Episode);
        Span<byte> nameBytes = stackalloc byte[10]; nameBytes.Clear();
        var end = LevelName.IndexOf('\0');
        var text = end < 0 ? LevelName : LevelName[..end];
        Encoding.Latin1.GetBytes(text.AsSpan(0, Math.Min(text.Length, 10)), nameBytes);
        Stream.Write(nameBytes);
        Stream.WriteByte(LevelFileNumber);
        Stream.Write(player.Items.ToLegacyItems(InitialEpisode));
        Stream.WriteByte(player.Items.Weapons[0].Power); Stream.WriteByte(player.Items.Weapons[1].Power);
        Span<byte> unused = stackalloc byte[3]; unused.Clear(); Stream.Write(unused);
        Stream.WriteByte(Song);
        input.DemoKeys = 0; input.DemoWait = 0;
        return name;
    }

    public void Dispose()
    {
        Stream?.Dispose(); Stream = null;
    }

    private byte ReadByte()
    {
        Span<byte> value = stackalloc byte[1]; BinaryFile.ReadExactly(Stream!, value);
        return value[0];
    }

    private byte ReadItem(ItemType type)
    {
        var id = ReadByte();
        if (!items.TryFind(type, id, out var index) || index > byte.MaxValue)
            throw new InvalidDataException($"Demo {type} item {id} is unavailable or exceeds native byte indices.");
        return (byte)index;
    }
}
