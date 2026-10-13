using OpenTyrian.Core;

namespace OpenTyrian.Core.Tests;

internal static class TinyDirectoryTests
{
    public static void Run()
    {
        var root = Path.Combine(Path.GetTempPath(), $"opentyrian-tinydir-{Guid.NewGuid():N}");
        var childPath = Path.Combine(root, "nested");
        var emptyPath = Path.Combine(root, "empty");
        Directory.CreateDirectory(childPath);
        Directory.CreateDirectory(emptyPath);
        var files = new[] { "B.txt", "a", ".hidden", "trailing." };
        foreach (var name in files) File.WriteAllText(Path.Combine(root, name), "");
        File.WriteAllText(Path.Combine(childPath, "inside"), "");
        try
        {
            TestFileOpen(root, childPath);
            TestSubdirectoryOpen(root, childPath, emptyPath);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static void TestFileOpen(string root, string childPath)
    {
        foreach (var name in new[] { "B.txt", "a", ".hidden", "trailing." })
        {
            var path = Path.Combine(root, name);
            Check(TinyDirectory.TryOpenFile(path, out var entry) && entry is not null &&
                entry.Path == path && entry.Name == name && entry.IsRegularFile && !entry.IsDirectory,
                $"Opening file '{name}' returned incorrect metadata.");
        }

        Check(TinyDirectory.TryOpenFile(Path.Combine(root, ".hidden"), out var hidden) &&
            hidden!.Extension == "hidden", "A leading-dot filename has incorrect extension metadata.");
        Check(TinyDirectory.TryOpenFile(Path.Combine(root, "trailing."), out var trailing) &&
            trailing!.Extension.Length == 0, "A trailing-dot filename has an incorrect extension.");
        Check(TinyDirectory.TryOpenFile(childPath, out var directory) &&
            directory is { IsDirectory: true, IsRegularFile: false },
            "Opening a directory did not report directory metadata.");
        var separator = Path.DirectorySeparatorChar;
        Check(TinyDirectory.TryOpenFile(childPath + separator, out var trailingSeparator) &&
            trailingSeparator is { Name: "nested", IsDirectory: true, IsRegularFile: false },
            "Opening a directory with a trailing separator did not preserve its basename.");
        var volumeRoot = Path.GetPathRoot(childPath)
            ?? throw new InvalidOperationException("Temporary test directory has no volume root.");
        Check(TinyDirectory.TryOpenFile(volumeRoot, out var rootEntry) &&
            rootEntry is { Name: "", IsDirectory: true, IsRegularFile: false },
            "Opening a volume root did not return the native root-directory entry.");
        Check(!TinyDirectory.TryOpenFile(Path.Combine(root, "missing"), out var missing) && missing is null,
            "Opening a nonexistent file unexpectedly succeeded.");
        Check(!TinyDirectory.TryOpenFile("", out _), "Opening an empty path unexpectedly succeeded.");
    }

    private static void TestSubdirectoryOpen(string root, string childPath, string emptyPath)
    {
        Check(TinyDirectory.TryOpenSorted(root, out var directory) && directory is not null,
            "Opening a populated directory failed.");
        var names = directory!.Files.Select(file => file.Name).ToArray();
        var sorted = directory.Files.OrderByDescending(file => file.IsDirectory)
            .ThenBy(file => file.Name, StringComparer.Ordinal).Select(file => file.Name).ToArray();
        Check(names.SequenceEqual(sorted), "Directory entries are not ordered directories-first then ordinal name.");
        var fileIndexForRead = directory.Files.ToList().FindIndex(file => file.Name == ".hidden");
        Check(fileIndexForRead >= 0 && directory.TryReadFileAt(fileIndexForRead, out var readFile) &&
            readFile is { Name: ".hidden", Extension: "hidden", IsRegularFile: true },
            "Indexed file lookup did not copy the entry and recompute its extension.");
        Check(!directory.TryReadFileAt(-1, out var invalidRead) && invalidRead is null &&
            directory.LastError == 2, "Indexed file lookup accepted an invalid index.");
        var nestedIndex = directory.Files.ToList().FindIndex(file => file.Path == childPath);
        Check(nestedIndex >= 0 && directory.OpenSubdirectory(nestedIndex) == 0 &&
            directory.DirectoryPath == childPath && directory.Files.Any(file => file.Name == "inside"),
            "Opening a sorted subdirectory did not replace the listing.");

        Check(TinyDirectory.TryOpenSorted(root, out directory) && directory is not null,
            "Reopening the parent directory failed.");
        var fileIndex = directory!.Files.ToList().FindIndex(file => file.Name == "a");
        Check(fileIndex >= 0 && directory.OpenSubdirectory(fileIndex) == -1 &&
            directory.LastError == 2 && directory.DirectoryPath == root,
            "Opening a file as a subdirectory did not preserve the original listing and report ENOENT.");
        Check(directory.OpenSubdirectory(-1) == -1 && directory.OpenSubdirectory(directory.Files.Count) == -1,
            "An invalid subdirectory index was accepted.");

        var emptyIndex = directory.Files.ToList().FindIndex(file => file.Path == emptyPath);
        Check(emptyIndex >= 0 && directory.OpenSubdirectory(emptyIndex) == -1 &&
            directory.LastError == 2 && directory.DirectoryPath.Length == 0 && directory.Files.Count == 0,
            "Opening an empty sorted directory did not match tinydir's empty-directory failure behavior.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
