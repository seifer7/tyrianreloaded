/*
 * OpenTyrian: A modern cross-platform port of Tyrian
 * Copyright (C) 2007-2009 The OpenTyrian Development Team
 * Distributed under the GNU General Public License, version 2 or later.
 * See COPYING for details.
 */

namespace OpenTyrian.Core;

public sealed class DataFiles(string workingDirectory, string? customDirectory = null, string? installedDirectory = null)
{
    private string? _directory;

    public string Directory
    {
        get
        {
            if (_directory is not null)
            {
                return _directory;
            }

            installedDirectory ??= OperatingSystem.IsWindows() ? @"C:\TYRIAN" : "/usr/local/share/games/tyrian";
            foreach (var candidate in new[] { customDirectory, installedDirectory, "data", "." })
            {
                if (candidate is not null && Exists(candidate, "tyrian1.lvl"))
                {
                    return _directory = candidate;
                }
            }

            return _directory = "";
        }
    }

    public FileStream Open(string directory, string filename, FileMode mode = FileMode.Open, FileAccess access = FileAccess.Read)
        => new(Resolve(directory, filename), mode, access, FileShare.ReadWrite);

    public FileStream? OpenWarn(string directory, string filename, TextWriter errors,
        FileMode mode = FileMode.Open, FileAccess access = FileAccess.Read)
    {
        try
        {
            return Open(directory, filename, mode, access);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            errors.WriteLine($"warning: failed to open '{filename}': {exception.Message}");
            return null;
        }
    }

    public FileStream OpenRequired(string directory, string filename,
        FileMode mode = FileMode.Open, FileAccess access = FileAccess.Read)
    {
        try
        {
            return Open(directory, filename, mode, access);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new IOException($"error: failed to open '{filename}': {exception.Message}\n" +
                "error: One or more of the required Tyrian 2.1 data files could not be found.\n" +
                "       Please read the README file.", exception);
        }
    }

    public bool Exists(string directory, string filename)
    {
        try
        {
            using var file = Open(directory, filename);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private string Resolve(string directory, string filename)
    {
        // Concatenation preserves the native empty-directory rooted-path behavior.
        var path = directory + Path.DirectorySeparatorChar + filename;
        return Path.GetFullPath(path, workingDirectory);
    }
}
