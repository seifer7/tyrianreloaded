/*
 * OpenTyrian: A modern cross-platform port of Tyrian
 * Copyright (C) 2007-2009 The OpenTyrian Development Team
 * Distributed under the GNU General Public License, version 2 or later.
 * See COPYING for details.
 */

namespace OpenTyrian.Core;

public sealed class LegacyGameState
{
    public PlayerState[] Players { get; } = [new(0), new(1)];
    public bool SuperTyrian { get; set; }
    public bool OnePlayerAction { get; set; }
    public byte SuperArcadeMode { get; set; }
    public bool TwoPlayerMode { get; set; }
    public bool ExtraGame { get; set; }
    public bool GalagaMode { get; set; }
    public bool GameJustLoaded { get; set; }
    public bool GameLoaded { get; set; }
    public sbyte InitialDifficulty { get; set; }
    public sbyte Difficulty { get; set; }
    public bool GameHasRepeated { get; set; }
    public byte InitialEpisode { get; set; }
    public byte MainLevel { get; set; }
    public byte SaveLevel { get; set; }
    public ushort CubeMax { get; set; }
    public ushort LastCubeMax { get; set; }
    public byte SecretHint { get; set; }
    public byte[] InputDevices { get; } = new byte[2];
    public byte Episode { get; set; }
    public byte[] LevelName { get; set; } = [];
    public byte[] LastLevelName { get; set; } = [];
}

public static class LegacySaveGame
{
    public static void Save(LegacySaveArchive archive, LegacyGameState game, byte slotNumber,
        ReadOnlySpan<byte> name, Action<LegacySaveArchive> persist)
    {
        ArgumentNullException.ThrowIfNull(persist);
        var slot = Slot(archive, slotNumber);
        var end = name.IndexOf((byte)0);
        if (end >= 0) name = name[..end];
        if (name.Length > 14) throw new ArgumentException("Legacy save names contain at most 14 bytes.", nameof(name));
        if (game.LastLevelName.Length > 9)
            throw new InvalidDataException("Legacy level names contain at most 9 bytes.");
        slot.InitialDifficulty = unchecked((byte)game.InitialDifficulty);
        slot.GameHasRepeated = game.GameHasRepeated;
        slot.Level = game.SaveLevel;
        game.Players[0].Items.SuperArcadeMode = game.SuperTyrian ? (byte)254 :
            game.SuperArcadeMode == 0 && game.OnePlayerAction ? (byte)255 : game.SuperArcadeMode;
        game.Players[0].Items.ToLegacyItems(game.InitialEpisode).CopyTo(slot.Items, 0);
        (game.TwoPlayerMode ? game.Players[1].Items : game.Players[0].LastItems).ToLegacyItems(0).CopyTo(slot.LastItems, 0);
        slot.Score = unchecked((int)game.Players[0].Cash);
        slot.Score2 = unchecked((int)game.Players[1].Cash);
        slot.LevelName = game.LastLevelName.ToArray();
        slot.Cubes = unchecked((byte)game.LastCubeMax);
        var episode = game.Episode;
        if (IsCompleted(game.LastLevelName))
        {
            episode = unchecked((byte)(episode - 1));
            if (episode < 1) episode = 4;
        }
        slot.Episode = episode;
        slot.Difficulty = unchecked((byte)game.Difficulty);
        slot.SecretHint = game.SecretHint;
        slot.Input1 = game.InputDevices[0];
        slot.Input2 = game.InputDevices[1];
        name.CopyTo(slot.Name);
        if (name.Length < slot.Name.Length) slot.Name[name.Length] = 0;
        for (var port = 0; port < 2; port++)
            slot.Power[port] = game.Players[game.TwoPlayerMode ? port : 0].Items.Weapons[port].Power;
        persist(archive);
    }

    public static void Load(LegacySaveArchive archive, LegacyGameState game, byte slotNumber,
        Func<int, int> initializeEpisode)
    {
        ArgumentNullException.ThrowIfNull(initializeEpisode);
        var slot = Slot(archive, slotNumber);
        game.SuperTyrian = game.OnePlayerAction = game.TwoPlayerMode = game.ExtraGame = game.GalagaMode = false;
        game.InitialDifficulty = unchecked((sbyte)slot.InitialDifficulty);
        game.GameHasRepeated = slot.GameHasRepeated;
        game.TwoPlayerMode = slotNumber > 11;
        game.Difficulty = unchecked((sbyte)slot.Difficulty);
        game.InitialEpisode = game.Players[0].Items.LoadLegacyItems(slot.Items);
        game.SuperArcadeMode = game.Players[0].Items.SuperArcadeMode;
        if (game.SuperArcadeMode == 254) game.SuperTyrian = true;
        if (game.SuperArcadeMode != 0) game.OnePlayerAction = true;
        if (game.SuperArcadeMode > 7) game.SuperArcadeMode = 0;
        if (game.TwoPlayerMode)
        {
            game.OnePlayerAction = false;
            game.Players[1].Items.LoadLegacyItems(slot.LastItems);
        }
        else game.Players[0].LastItems.LoadLegacyItems(slot.LastItems);
        if (game.Players[1].Items.SidekickLevel < 101)
        {
            game.Players[1].Items.SidekickLevel = 101;
            game.Players[1].Items.SidekickSeries = game.Players[1].Items.Sidekicks[0];
        }
        game.Players[0].Cash = unchecked((ulong)(long)slot.Score);
        game.Players[1].Cash = unchecked((ulong)(long)slot.Score2);
        game.MainLevel = unchecked((byte)slot.Level);
        game.CubeMax = game.LastCubeMax = slot.Cubes;
        game.SecretHint = slot.SecretHint;
        game.InputDevices[0] = slot.Input1;
        game.InputDevices[1] = slot.Input2;
        for (var port = 0; port < 2; port++)
            game.Players[game.TwoPlayerMode ? port : 0].Items.Weapons[port].Power = slot.Power[port];
        var episode = (int)slot.Episode;
        game.LevelName = slot.LevelName.ToArray();
        if (IsCompleted(game.LevelName))
        {
            if (episode == 4) episode = 1;
            else if (episode < 4) episode++;
        }
        game.Episode = checked((byte)initializeEpisode(episode));
        game.SaveLevel = game.MainLevel;
        game.LastLevelName = game.LevelName.ToArray();
    }

    private static LegacySaveSlot Slot(LegacySaveArchive archive, byte number)
    {
        if (number is < 1 or > LegacySaveArchive.SlotCount) throw new ArgumentOutOfRangeException(nameof(number));
        return archive.Slots[number - 1];
    }

    private static bool IsCompleted(ReadOnlySpan<byte> name)
    {
        var end = name.IndexOf((byte)0);
        return (end >= 0 ? name[..end] : name).SequenceEqual("Completed"u8);
    }
}
