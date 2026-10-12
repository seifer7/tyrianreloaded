/*
 * OpenTyrian: A modern cross-platform port of Tyrian
 * Copyright (C) 2007-2009 The OpenTyrian Development Team
 * Distributed under the GNU General Public License, version 2 or later.
 * See COPYING for details.
 */

using System.Buffers.Binary;
using System.Text;

namespace OpenTyrian.Core;

public sealed record SavedGameMetadata(string Name, byte[] LevelName, byte GameMode, bool TwoPlayerMode,
    byte Episode, ushort FormatVersion, ushort CompatibleVersion);

public sealed class SavedGameCatalog(string userDirectory, TextWriter diagnostics)
{
    public static SavedGameMetadata ReadMetadata(Stream stream, string filename)
    {
        ValidateFilename(filename);
        Span<byte> header = stackalloc byte[157];
        BinaryFile.ReadExactly(stream, header[..8]);
        if (!header[..4].SequenceEqual("TYRR"u8))
            throw new InvalidDataException("Save file does not contain the TYRR signature.");
        var version = BinaryPrimitives.ReadUInt16LittleEndian(header[4..]);
        var compatible = BinaryPrimitives.ReadUInt16LittleEndian(header[6..]);
        if (version != 1 && compatible != 1)
            throw new InvalidDataException($"Unsupported save format {version}, compatibility version {compatible}.");
        BinaryFile.ReadExactly(stream, header[8..]);
        var level = header.Slice(144, 10);
        var end = level.IndexOf((byte)0);
        var nameLength = Math.Min(14, filename.Length - 7);
        return new(filename[..nameLength], (end >= 0 ? level[..end] : level).ToArray(),
            header[155], header[156] == 2, header[9], version, compatible);
    }

    public SavedGameMetadata? ReadMetadataFile(string filename)
    {
        var path = NativePath(filename);
        try
        {
            using var stream = File.OpenRead(path);
            return ReadMetadata(stream, filename);
        }
        catch (FileNotFoundException) { diagnostics.WriteLine($"warning: save file not found: {path}"); return null; }
        catch (DirectoryNotFoundException) { diagnostics.WriteLine($"warning: save directory not found: {userDirectory}"); return null; }
    }

    public IReadOnlyList<SavedGameMetadata> BuildList(byte players, string listingDirectory = ".")
    {
        var directory = new DirectoryInfo(Path.Combine(listingDirectory, "saves"));
        var result = new List<SavedGameMetadata>();
        foreach (var file in directory.EnumerateFiles().OrderBy(file => file.Name, StringComparer.Ordinal))
        {
            if (file.Extension != ".tyrsav") continue;
            SavedGameMetadata? metadata;
            try { metadata = ReadMetadataFile(file.Name); }
            catch (InvalidDataException exception)
            {
                diagnostics.WriteLine($"warning: invalid save {file.Name}: {exception.Message}");
                continue;
            }
            catch (EndOfStreamException exception)
            {
                diagnostics.WriteLine($"warning: truncated save {file.Name}: {exception.Message}");
                continue;
            }
            if (metadata == null || (players == 1 && metadata.TwoPlayerMode) || (players == 2 && !metadata.TwoPlayerMode))
                continue;
            result.Add(metadata);
        }
        return result.AsReadOnly();
    }

    public string NativePath(string filename)
    {
        ValidateFilename(filename);
        var relative = $"saves\\{filename}";
        return Path.Combine(userDirectory, relative[..Math.Min(59, relative.Length)]);
    }

    private static void ValidateFilename(string filename)
    {
        ArgumentNullException.ThrowIfNull(filename);
        if (filename.Length < 7 || !filename.EndsWith(".tyrsav", StringComparison.Ordinal) ||
            filename.Any(character => character > 255 || character is '\0' or '/' or '\\'))
            throw new ArgumentException("Save filename must be a Latin-1 .tyrsav basename.", nameof(filename));
    }
}
