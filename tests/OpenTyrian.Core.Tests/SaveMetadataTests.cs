using System.Buffers.Binary;
using System.Diagnostics;
using System.Text;
using OpenTyrian.Core;

namespace OpenTyrian.Core.Tests;

internal static class SaveMetadataTests
{
    public static void Run(string? executable = null)
    {
        var root = Path.Combine(Path.GetTempPath(), $"opentyrian-save-catalog-{Guid.NewGuid():N}");
        var directory = Path.Combine(root, "saves");
        Directory.CreateDirectory(directory);
        using var diagnostics = new StringWriter();
        var catalog = new SavedGameCatalog(root, diagnostics);
        var paths = new HashSet<string>();
        var childDirectory = Path.Combine(directory, "folder.tyrsav");
        Directory.CreateDirectory(childDirectory);
        try
        {
            string[] names = ["alpha.tyrsav", "Beta.tyrsav", "long-name-is-truncated.tyrsav", ".tyrsav",
                "team.tyrsav", "future-compatible.tyrsav"];
            for (var index = 0; index < names.Length; index++)
            {
                var bytes = Fixture(index);
                Write(names[index], bytes);
                var metadata = catalog.ReadMetadataFile(names[index]) ?? throw new InvalidOperationException("Save metadata missing.");
                Check(metadata.Name == names[index][..Math.Min(14, names[index].Length - 7)] &&
                    metadata.GameMode == bytes[155] && metadata.TwoPlayerMode == (bytes[156] == 2) &&
                    metadata.Episode == bytes[9], "Save metadata fields/truncation changed.");
                Check(metadata.LevelName.SequenceEqual(Encoding.ASCII.GetBytes(index % 2 == 0 ? "Completed" : "ABCDEFGHIJ")),
                    "Level name was not NUL-terminated at ten bytes.");
                if (executable != null)
                    Compare($"success:1\n{Trace(metadata)}", RunNative(executable, root, names[index]));
            }
            var invalid = Fixture(0); invalid[0] = 0;
            Write("bad.tyrsav", invalid);
            Write("skip.TYRSAV", Fixture(0));
            Write("skip.txt", Fixture(0));
            if (executable != null)
                Compare("success:0\n", RunNative(executable, root, "bad.tyrsav"));
            for (byte players = 0; players < 4; players++)
            {
                var list = catalog.BuildList(players, root);
                Check(list.Count == (players is 1 or 2 ? 3 : 6), "Save catalog player filter/count changed.");
                Check(list.Select(save => save.Name).SequenceEqual(list.Select(save => save.Name).Order(StringComparer.Ordinal)),
                    "Save catalog sort order changed.");
                var trace = $"count:{list.Count}\n" + string.Concat(list.Select(Trace));
                if (executable != null) Compare(trace, RunNative(executable, root, "list", players.ToString()));
            }
            Check(diagnostics.ToString().Contains("invalid save bad.tyrsav", StringComparison.Ordinal),
                "Rejected save was not diagnosed.");
            Check(catalog.ReadMetadataFile("missing.tyrsav") == null &&
                diagnostics.ToString().Contains("save file not found", StringComparison.Ordinal), "Missing save was not diagnosed.");
            var unsupported = Fixture(0);
            BinaryPrimitives.WriteUInt16LittleEndian(unsupported.AsSpan(4), 2);
            BinaryPrimitives.WriteUInt16LittleEndian(unsupported.AsSpan(6), 2);
            Throws<InvalidDataException>(() => SavedGameCatalog.ReadMetadata(new MemoryStream(unsupported), "unsupported.tyrsav"));
            Write("unsupported.tyrsav", unsupported);
            Check(catalog.BuildList(0, root).Count == 6 &&
                diagnostics.ToString().Contains("Unsupported save format", StringComparison.Ordinal), "Unsupported version was accepted silently.");
            var complete = Fixture(0);
            for (var size = 0; size < complete.Length; size++)
                Throws<EndOfStreamException>(() => SavedGameCatalog.ReadMetadata(new MemoryStream(complete[..size]), "short.tyrsav"));
            Write("short.tyrsav", complete[..100]);
            Check(catalog.BuildList(0, root).Count == 6 &&
                diagnostics.ToString().Contains("truncated save short.tyrsav", StringComparison.Ordinal), "Truncated save was not diagnosed.");
            Throws<ArgumentException>(() => catalog.NativePath("..\\unsafe.tyrsav"));
            Throws<ArgumentException>(() => catalog.NativePath("wrong.txt"));
            Check(catalog.NativePath(new string('a', 70) + ".tyrsav") == Path.Combine(root, "saves\\" + new string('a', 53)),
                "Native 60-byte path truncation changed.");
            using var locked = new FileStream(catalog.NativePath(names[0]), FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            Throws<IOException>(() => catalog.ReadMetadataFile(names[0]));
            Throws<DirectoryNotFoundException>(() => catalog.BuildList(0, Path.Combine(root, "missing")));
        }
        finally
        {
            foreach (var path in paths) File.Delete(path);
            Directory.Delete(childDirectory);
            Directory.Delete(directory);
            Directory.Delete(root);
        }

        void Write(string name, byte[] bytes)
        {
            var targets = new List<string> { Path.Combine(directory, name) };
            if (name.EndsWith(".tyrsav", StringComparison.Ordinal)) targets.Add(catalog.NativePath(name));
            foreach (var path in targets)
                if (paths.Add(path)) File.WriteAllBytes(path, bytes);
        }
    }

    private static byte[] Fixture(int index)
    {
        var bytes = new byte[157];
        "TYRR"u8.CopyTo(bytes);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(4), (ushort)(index == 5 ? 3 : 1));
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(6), (ushort)(index == 4 ? 3 : 1));
        bytes[8] = 254; bytes[9] = (byte)(index + 1); bytes[10] = 255; bytes[11] = 253; bytes[12] = 4;
        bytes[13] = 255; bytes[14] = 255; bytes[15] = 1;
        Array.Fill(bytes, (byte)193, 16, 128);
        Encoding.ASCII.GetBytes(index % 2 == 0 ? "Completed" : "ABCDEFGHIJK").CopyTo(bytes, 144);
        bytes[155] = (byte)(index % 3 == 0 ? 254 : index % 3 == 1 ? 255 : 7);
        bytes[156] = (byte)(index % 2 == 0 ? 1 : 2);
        return bytes;
    }

    private static string Trace(SavedGameMetadata save) =>
        $"metadata:{Convert.ToHexString(Encoding.Latin1.GetBytes(save.Name))},{Convert.ToHexString(save.LevelName)},{save.GameMode},{(save.TwoPlayerMode ? 1 : 0)},{save.Episode}\n";

    private static string RunNative(string executable, string directory, params string[] args)
    {
        var start = new ProcessStartInfo(Path.GetFullPath(executable))
        { WorkingDirectory = directory, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in args) start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Cannot start native save metadata oracle.");
        var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(30000))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException("Native save metadata oracle timed out.");
        }
        Check(process.ExitCode == 0, $"Native metadata oracle failed: {error.GetAwaiter().GetResult()}");
        return output.GetAwaiter().GetResult().Replace("\r\n", "\n", StringComparison.Ordinal);
    }
    private static void Compare(string expected, string actual) => Check(expected == actual, $"Save metadata differs: expected {expected}; actual {actual}");
    private static void Throws<T>(Action action) where T : Exception
    {
        try { action(); } catch (T) { return; }
        throw new InvalidOperationException($"Expected {typeof(T).Name}.");
    }
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
