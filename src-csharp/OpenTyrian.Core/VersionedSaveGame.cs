/*
 * OpenTyrian: A modern cross-platform port of Tyrian
 * Copyright (C) 2007-2009 The OpenTyrian Development Team
 * Distributed under the GNU General Public License, version 2 or later.
 * See COPYING for details.
 */

using System.Buffers.Binary;
using System.Globalization;
using System.Text;

namespace OpenTyrian.Core;

public static class VersionedSaveGame
{
    private static ReadOnlySpan<byte> CipherKey => "TYRIANRELOADED"u8;

    public static void SaveFile(string directory, string name, LegacyGameState game, ItemDatabase items)
    {
        var path = new SavedGameCatalog(directory, TextWriter.Null).NativePath($"{name}.tyrsav");
        using var encoded = new MemoryStream();
        Write(encoded, game, items);
        using var file = new FileStream(path, FileMode.Create, FileAccess.Write);
        encoded.Position = 0;
        encoded.CopyTo(file);
        file.Flush(flushToDisk: true);
    }

    public static bool LoadFile(string directory, string name, LegacyGameState game, ItemDatabase items,
        Func<int, int> initializeEpisode, TextWriter diagnostics)
    {
        var path = new SavedGameCatalog(directory, diagnostics).NativePath($"{name}.tyrsav");
        try
        {
            using var file = File.OpenRead(path);
            Load(file, game, items, initializeEpisode);
            return true;
        }
        catch (FileNotFoundException) { diagnostics.WriteLine($"warning: save file not found: {path}"); return false; }
        catch (DirectoryNotFoundException) { diagnostics.WriteLine($"warning: save directory not found: {directory}"); return false; }
    }

    public static void Write(Stream stream, LegacyGameState game, ItemDatabase items)
    {
        var fields = new List<string>();
        for (var p = 0; p < (game.TwoPlayerMode ? 2 : 1); p++)
        {
            var player = game.Players[p];
            Append(player.Items, player.Items.Weapons[0].Power.ToString(CultureInfo.InvariantCulture),
                player.Items.Weapons[1].Power.ToString(CultureInfo.InvariantCulture));
            fields.Add(player.Cash.ToString(CultureInfo.InvariantCulture));
            fields.AddRange(Enumerable.Repeat("", 12));
            Append(player.LastItems, "", "");
            fields.AddRange(Enumerable.Repeat("", 12));
        }
        var payload = Encoding.Latin1.GetBytes(string.Join('*', fields) + "E");
        if (payload.Length >= 1024) throw new InvalidDataException("Save item payload exceeds the native 1024-byte buffer.");
        LegacySaveCipher.XorCString(payload, CipherKey);
        if (payload.Contains((byte)0))
            throw new InvalidDataException("Save item IDs produce a NUL-truncated native cipher.");
        if (game.LastLevelName.Length > 11)
            throw new InvalidDataException("Save level name exceeds its eleven-byte field.");
        var header = new byte[157];
        "TYRR"u8.CopyTo(header);
        BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(4), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(6), 1);
        header[8] = unchecked((byte)game.Difficulty);
        header[9] = game.Episode;
        header[10] = game.SaveLevel;
        header[11] = unchecked((byte)game.InitialDifficulty);
        header[12] = game.InitialEpisode;
        BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(13), game.LastCubeMax);
        header[15] = game.GameHasRepeated ? (byte)1 : (byte)0;
        game.LastLevelName.CopyTo(header, 144);
        header[155] = game.SuperTyrian ? (byte)254 :
            game.SuperArcadeMode == 0 && game.OnePlayerAction ? (byte)255 : game.SuperArcadeMode;
        header[156] = game.TwoPlayerMode ? (byte)2 : (byte)1;
        BinaryFile.WriteExactly(stream, header);
        BinaryFile.WriteExactly(stream, payload);

        void Append(PlayerItems player, string frontPower, string rearPower)
        {
            fields.Add(Id(ItemType.Ship, player.Ship));
            fields.Add(Id(ItemType.WeaponFront, player.Weapons[0].Id)); fields.Add(frontPower);
            fields.Add(Id(ItemType.WeaponRear, player.Weapons[1].Id)); fields.Add(rearPower);
            fields.Add(Id(ItemType.Generator, player.Generator)); fields.Add(Id(ItemType.Shield, player.Shield));
            fields.Add(Id(ItemType.SidekickLeft, player.Sidekicks[0])); fields.Add(Id(ItemType.SidekickRight, player.Sidekicks[1]));
            fields.Add(Id(ItemType.Special, player.Special));
        }
        string Id(ItemType type, byte index)
        {
            var table = items.Items(type);
            if (index >= table.Count) throw new InvalidDataException($"Save item index {index} is missing for {type}.");
            var id = table[index].Id;
            if (id.Length > 10 || id.Any(character => character > 255 || character is '\0' or '*'))
                throw new InvalidDataException("Save item ID exceeds its native field or contains a separator.");
            return id;
        }
    }

    public static void Load(Stream stream, LegacyGameState game, ItemDatabase items, Func<int, int> initializeEpisode)
    {
        ArgumentNullException.ThrowIfNull(initializeEpisode);
        var header = new byte[157];
        BinaryFile.ReadExactly(stream, header);
        using var metadataStream = new MemoryStream(header, writable: false);
        var metadata = SavedGameCatalog.ReadMetadata(metadataStream, "state.tyrsav");
        var buffer = new byte[1024];
        var count = BinaryFile.ReadBytes(stream, buffer);
        if (count == 0 || count == buffer.Length || buffer.AsSpan(0, count).Contains((byte)0))
            throw new InvalidDataException("Save item payload is empty, oversized, or NUL-truncated.");
        LegacySaveCipher.XorCString(buffer.AsSpan(0, count), CipherKey);
        if (buffer[count - 1] != (byte)'E' || buffer.AsSpan(0, count).Contains((byte)0))
            throw new InvalidDataException("Save item payload has no valid terminal marker.");
        var values = Encoding.Latin1.GetString(buffer, 0, count - 1).Split('*');
        var players = metadata.TwoPlayerMode ? 2 : 1;
        if (values.Length != players * 45 || values.Any(value => value.Length > 10) || values[^1].Length >= 10)
            throw new InvalidDataException("Save item payload does not fit its native player/field layout.");
        var loadedItems = new PlayerItems[players];
        var loadedLastItems = new PlayerItems[players];
        var cash = new ulong[players];
        for (var p = 0; p < players; p++)
        {
            loadedItems[p] = game.Players[p].Items.Copy();
            loadedLastItems[p] = game.Players[p].LastItems.Copy();
            var start = p * 45;
            Apply(loadedItems[p], start);
            loadedItems[p].Weapons[0].Power = unchecked((byte)Atoi(values[start + 2]));
            loadedItems[p].Weapons[1].Power = unchecked((byte)Atoi(values[start + 4]));
            cash[p] = unchecked((ulong)(long)Atoi(values[start + 10]));
            Apply(loadedLastItems[p], start + 23);
        }
        game.SuperTyrian = metadata.GameMode == 254;
        game.OnePlayerAction = metadata.GameMode == 255;
        game.SuperArcadeMode = metadata.GameMode is 254 or 255 ? (byte)0 : metadata.GameMode;
        game.TwoPlayerMode = metadata.TwoPlayerMode;
        game.ExtraGame = game.GalagaMode = false;
        game.Difficulty = unchecked((sbyte)header[8]);
        game.MainLevel = header[10];
        game.InitialDifficulty = unchecked((sbyte)header[11]);
        game.InitialEpisode = header[12];
        game.CubeMax = game.LastCubeMax = BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(13));
        game.GameHasRepeated = header[15] == 1;
        game.LevelName = metadata.LevelName;
        for (var p = 0; p < players; p++)
        {
            game.Players[p].Items = loadedItems[p]; game.Players[p].LastItems = loadedLastItems[p];
            game.Players[p].Cash = cash[p];
        }
        var episode = (int)metadata.Episode;
        if (game.LevelName.AsSpan().SequenceEqual("Completed"u8))
        {
            if (episode == 4) episode = 1;
            else if (episode < 4) episode++;
        }
        game.Episode = checked((byte)initializeEpisode(episode));
        game.SaveLevel = game.MainLevel;
        game.LastLevelName = game.LevelName.ToArray();
        game.GameJustLoaded = game.GameLoaded = true;

        void Apply(PlayerItems target, int start)
        {
            target.Ship = Index(ItemType.Ship, values[start]);
            target.Weapons[0].Id = Index(ItemType.WeaponFront, values[start + 1]);
            target.Weapons[1].Id = Index(ItemType.WeaponRear, values[start + 3]);
            target.Generator = Index(ItemType.Generator, values[start + 5]);
            target.Shield = Index(ItemType.Shield, values[start + 6]);
            target.Sidekicks[0] = Index(ItemType.SidekickLeft, values[start + 7]);
            target.Sidekicks[1] = Index(ItemType.SidekickRight, values[start + 8]);
            target.Special = Index(ItemType.Special, values[start + 9]);
        }
        byte Index(ItemType type, string id)
        {
            if (!items.TryFind(type, id, out var index))
                throw new InvalidDataException($"Required save item ID '{id}' for {type} is missing.");
            return unchecked((byte)index);
        }
    }

    private static int Atoi(string value)
    {
        var text = value.AsSpan().TrimStart(" \t\r\n\v\f");
        var end = text.Length > 0 && text[0] is '+' or '-' ? 1 : 0;
        var digits = end;
        while (end < text.Length && text[end] is >= '0' and <= '9') end++;
        if (end == digits) return 0;
        if (!int.TryParse(text[..end], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var number))
            throw new InvalidDataException("Save numeric value exceeds defined native signed-int range.");
        return number;
    }
}
