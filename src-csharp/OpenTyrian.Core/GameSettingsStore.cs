/*
 * OpenTyrian: A modern cross-platform port of Tyrian
 * Copyright (C) 2007-2009 The OpenTyrian Development Team
 * Distributed under the GNU General Public License, version 2 or later.
 * See COPYING for details.
 */

using System.Text;

namespace OpenTyrian.Core;

public sealed class DosConfiguration
{
    public bool Background2 { get; set; }
    public byte GameSpeed { get; set; }
    public byte InputDevice { get; set; }
    public byte JoystickConfiguration { get; set; }
    public byte Version { get; set; }
    public byte ProcessorType { get; set; }
    public byte MidiPort { get; set; } = 1;
    public byte SoundEffects { get; set; }
    public byte Gamma { get; set; }
    public sbyte Difficulty { get; set; }
    public byte[] JoystickButtons { get; } = [1, 4, 5, 5];
    public ushort MusicVolume { get; set; }
    public ushort EffectsVolume { get; set; }
    public byte[] InputDevices { get; } = [1, 2];
    public byte[] Keys { get; } = new byte[8];

    public void Defaults()
    {
        SoundEffects = 1;
        byte[] keys = [72, 80, 75, 77, 57, 28, 29, 56];
        keys.CopyTo(Keys, 0);
        Background2 = true;
        MusicVolume = EffectsVolume = 191;
        Gamma = 0;
        ProcessorType = 3;
        GameSpeed = 4;
    }

    public void Read(Stream stream)
    {
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
        Background2 = reader.ReadBoolean();
        GameSpeed = reader.ReadByte();
        InputDevice = reader.ReadByte();
        JoystickConfiguration = reader.ReadByte();
        Version = reader.ReadByte();
        ProcessorType = reader.ReadByte();
        MidiPort = reader.ReadByte();
        SoundEffects = reader.ReadByte();
        Gamma = reader.ReadByte();
        Difficulty = reader.ReadSByte();
        BinaryFile.ReadExactly(stream, JoystickButtons);
        MusicVolume = reader.ReadUInt16();
        EffectsVolume = reader.ReadUInt16();
        BinaryFile.ReadExactly(stream, InputDevices);
        BinaryFile.ReadExactly(stream, Keys);
    }

    public void Write(Stream stream)
    {
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write(Background2);
        writer.Write(GameSpeed);
        writer.Write(InputDevice);
        writer.Write(JoystickConfiguration);
        writer.Write(Version);
        writer.Write(ProcessorType);
        writer.Write(MidiPort);
        writer.Write(SoundEffects);
        writer.Write(Gamma);
        writer.Write(Difficulty);
        writer.Write(JoystickButtons);
        writer.Write(MusicVolume);
        writer.Write(EffectsVolume);
        writer.Write(InputDevices);
        writer.Write(Keys);
    }
}

public sealed class GameSettingsStore(GameConfiguration text, TextWriter diagnostics)
{
    private static readonly Lazy<string> UserDirectory = new(ResolveUserDirectory);
    private static readonly string[] HighScoreNames =
    [
        "The Prime Chair", "Transon Lohk", "Javi Onukala", "Mantori", "Nortaneous", "Dougan", "Reid",
        "General Zinglon", "Late Gyges Phildren", "Vykromod", "Beppo", "Borogar", "ShipMaster Carlos",
        "Jill", "Darcy", "Jake Stone", "Malvineous Havershim", "Marta Louise Velasquez",
        "Jazz Jackrabbit", "Eva Earlong", "Devan Shell", "Crystal Devroe", "Steffan Tommas", "Milano Angston",
        "Christian", "Shirro", "Jean-Paul", "Ibrahim Hothe", "Angel", "Cossette Akira", "Raven",
        "Hans Kreissack", "Tyler", "Rennis the Rat Guard"
    ];
    private static readonly string[] TeamNames =
    [
        "Jackrabbits", "Team Tyrian", "The Elam Brothers", "Dare to Dream Team", "Pinball Freaks",
        "Extreme Pinball Freaks", "Team Vykromod", "Epic All-Stars", "Hans Keissack's WARriors",
        "Team Overkill", "Pied Pipers", "Gencore Growlers", "Microsol Masters", "Beta Warriors",
        "Team Loco", "The Shellians", "Jungle Jills", "Murderous Malvineous", "The Traffic Department",
        "Clan Mikal", "Clan Patrok", "Carlos' Crawlers"
    ];
    private static readonly byte[] InitialAvailability =
    [
        1,1,1,0,0,1,1,0,1,1,1,1,1,0,1,0,1,1,1,0,0,0,0,0,0,0,0,1,0,0,0,0,0,0,0,0,0,0,0,0,
        0,0,0,0,0,0,0,0,0,0,1,
        1,0,0,0,0,1,0,0,0,1,1,0,1,0,0,0,0,0,
        0,0,0,0,0,0,0,0,0,0,0,
        1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
        0,0,0,0,0
    ];
    public DosConfiguration Dos { get; } = new();
    public GameRuntimeSettings Runtime { get; } = new();
    public GameConfiguration Text => text;
    public LegacySaveArchive Saves { get; private set; } = new();

    public static string GetUserDirectory() => UserDirectory.Value;

    private static string ResolveUserDirectory()
    {
        if (OperatingSystem.IsWindows()) return ".";
        var xdg = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        var home = Environment.GetEnvironmentVariable("HOME");
        var path = xdg != null ? $"{xdg}{Path.DirectorySeparatorChar}opentyrian" :
            home != null ? $"{home}{Path.DirectorySeparatorChar}.config{Path.DirectorySeparatorChar}opentyrian" : ".";
        if (Encoding.UTF8.GetByteCount(path) > 499)
            throw new PathTooLongException("Native user configuration directory exceeds its 499-byte limit.");
        return path;
    }

    public void Load(string directory, Action<ushort, ushort> setVolume)
    {
        ArgumentNullException.ThrowIfNull(setVolume);
        using (var binary = OpenOptional(directory, "tyrian.cfg"))
        {
            if (binary != null && binary.Length == 28) Dos.Read(binary);
            else
            {
                diagnostics.Write("\nInvalid or missing TYRIAN.CFG! Continuing using defaults.\n\n");
                Dos.Defaults();
            }
        }
        text.LoadFile(directory, diagnostics);
        Dos.MusicVolume = Math.Min(Dos.MusicVolume, (ushort)255);
        Dos.EffectsVolume = Math.Min(Dos.EffectsVolume, (ushort)255);
        setVolume(Dos.MusicVolume, Dos.EffectsVolume);
        using (var saved = OpenOptional(directory, "tyrian.sav"))
        {
            if (saved != null) Saves = LegacySaveArchive.Read(saved, diagnostics);
            else InitializeSaves();
        }
        Runtime.ProcessorType = Dos.ProcessorType;
        Runtime.GameSpeed = Dos.GameSpeed;
        Runtime.InitializeProcessor();
        Dos.Background2 = Runtime.Background2;
    }

    private FileStream? OpenOptional(string directory, string name)
    {
        var path = Path.Combine(directory, name);
        try { return File.OpenRead(path); }
        catch (FileNotFoundException) { diagnostics.WriteLine($"warning: file not found: {path}"); return null; }
        catch (DirectoryNotFoundException) { diagnostics.WriteLine($"warning: directory not found: {directory}"); return null; }
    }

    private void InitializeSaves()
    {
        Saves.EditorLevel = 800;
        InitialAvailability.CopyTo(Saves.ItemAvailability, 0);
        for (var index = 0; index < Saves.Slots.Length; index++)
        {
            var slot = Saves.Slots[index];
            slot.Level = 0;
            Array.Fill(slot.Name, (byte)' ');
            slot.HighScore1 = (int)((MersenneTwister.NextUInt32() % 20 + 1) * 1000);
            if (index % 6 > 2)
            {
                slot.HighScore2 = (int)((MersenneTwister.NextUInt32() % 20 + 1) * 1000);
                slot.HighScoreName = Encoding.ASCII.GetBytes(TeamNames[MersenneTwister.NextUInt32() % 22]);
            }
            else slot.HighScoreName = Encoding.ASCII.GetBytes(HighScoreNames[MersenneTwister.NextUInt32() % 34]);
        }
    }

    public void Save(string directory)
    {
        if (OperatingSystem.IsWindows()) Directory.CreateDirectory(directory);
        else Directory.CreateDirectory(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        Dos.Background2 = Runtime.Background2;
        Dos.GameSpeed = Runtime.GameSpeed;
        Dos.ProcessorType = Runtime.ProcessorType;
        SaveFile(Path.Combine(directory, "tyrian.sav"), Saves.Write);
        SaveFile(Path.Combine(directory, "tyrian.cfg"), Dos.Write);
        text.SaveFile(directory);
    }

    private static void SaveFile(string path, Action<Stream> write)
    {
        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write);
        write(stream);
        stream.Flush(flushToDisk: true);
    }
}
