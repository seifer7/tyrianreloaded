/*
 * OpenTyrian: A modern cross-platform port of Tyrian
 * Copyright (C) 2007-2009 The OpenTyrian Development Team
 * Distributed under the GNU General Public License, version 2 or later.
 * See COPYING for details.
 */

namespace OpenTyrian.Core;

public sealed record TinyDirectoryEntry(string Path, string Name, string Extension,
    bool IsDirectory, bool IsRegularFile);

public sealed class TinyDirectory
{
    private TinyDirectory(string path, TinyDirectoryEntry[] files)
    {
        DirectoryPath = path;
        Files = files;
    }

    public string DirectoryPath { get; private set; }
    public IReadOnlyList<TinyDirectoryEntry> Files { get; private set; }
    public int LastError { get; private set; }

    public bool TryReadFileAt(int index, out TinyDirectoryEntry? file)
    {
        file = null;
        if ((uint)index >= (uint)Files.Count)
        {
            LastError = 2;
            return false;
        }

        var entry = Files[index];
        file = entry with { Extension = GetExtension(entry.Name) };
        LastError = 0;
        return true;
    }

    public static bool TryOpenSorted(string path, out TinyDirectory? directory)
    {
        ArgumentNullException.ThrowIfNull(path);
        directory = null;
        try
        {
            if (path.Length == 0 || !System.IO.Directory.Exists(path))
                return false;

            var files = new List<TinyDirectoryEntry>();
            if (!OperatingSystem.IsWindows())
            {
                files.Add(new TinyDirectoryEntry(System.IO.Path.Combine(path, "."), ".", "", true, false));
                files.Add(new TinyDirectoryEntry(System.IO.Path.Combine(path, ".."), "..", "", true, false));
            }

            foreach (var entryPath in System.IO.Directory.EnumerateFileSystemEntries(path))
            {
                var attributes = File.GetAttributes(entryPath);
                var isDirectory = (attributes & FileAttributes.Directory) != 0;
                var isRegular = IsRegularFile(attributes);
                var name = System.IO.Path.GetFileName(entryPath);
                files.Add(new TinyDirectoryEntry(entryPath, name, GetExtension(name), isDirectory, isRegular));
            }

            if (files.Count == 0)
                return false;

            directory = new TinyDirectory(path, files
                .OrderByDescending(file => file.IsDirectory)
                .ThenBy(file => file.Name, StringComparer.Ordinal)
                .ToArray());
            return true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or
            ArgumentException or NotSupportedException)
        {
            return false;
        }
    }

    public int OpenSubdirectory(int index)
    {
        if ((uint)index >= (uint)Files.Count || !Files[index].IsDirectory)
        {
            LastError = 2;
            return -1;
        }

        var path = Files[index].Path;
        DirectoryPath = "";
        Files = Array.Empty<TinyDirectoryEntry>();
        if (!TryOpenSorted(path, out var child) || child is null)
        {
            LastError = 2;
            return -1;
        }

        DirectoryPath = child.DirectoryPath;
        Files = child.Files;
        LastError = 0;
        return 0;
    }

    public static bool TryOpenFile(string path, out TinyDirectoryEntry? file)
    {
        ArgumentNullException.ThrowIfNull(path);
        file = null;
        if (path.Length == 0)
            return false;

        try
        {
            var normalizedPath = System.IO.Path.TrimEndingDirectorySeparator(path);
            var name = System.IO.Path.GetFileName(normalizedPath);
            if (name.Length == 0)
            {
                var attributes = File.GetAttributes(path);
                file = new TinyDirectoryEntry(path, "", "",
                    (attributes & FileAttributes.Directory) != 0, false);
                return true;
            }

            var attributesForEntry = File.GetAttributes(path);
            var isDirectory = (attributesForEntry & FileAttributes.Directory) != 0;
            var isRegular = IsRegularFile(attributesForEntry);
            file = new TinyDirectoryEntry(path, name, GetExtension(name), isDirectory, isRegular);
            return true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or
            ArgumentException or NotSupportedException)
        {
            return false;
        }
    }

    private static string GetExtension(string name)
    {
        var dot = name.LastIndexOf('.');
        return dot < 0 ? "" : name[(dot + 1)..];
    }

    private static bool IsRegularFile(FileAttributes attributes) =>
        (attributes & (FileAttributes.Device | FileAttributes.Directory |
            FileAttributes.Encrypted | FileAttributes.Offline | FileAttributes.Temporary)) == 0;
}
