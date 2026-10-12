using OpenTyrian.Core;

namespace OpenTyrian.Core.Tests;

internal static class LevelTests
{
    public static void Run()
    {
        using var archive = new MemoryStream();
        BinaryFile.WriteUInt16(archive, 3);
        foreach (var offset in new[] { 14, 20, 26 })
        {
            BinaryFile.WriteInt32(archive, offset);
        }

        archive.Write(new byte[18]);
        archive.Position = 0;
        var levels = new LevelArchive();
        levels.Analyze(archive);
        Check(levels.Count == 3 && levels.Positions.AsSpan(0, 4).SequenceEqual(new[] { 14, 20, 26, 32 }) &&
            archive.Position == 14, "Level directory or stream position differs.");
        using var invalid = new MemoryStream(new byte[] { 43, 0 });
        try
        {
            levels.Analyze(invalid);
            throw new InvalidOperationException("Oversized level directory accepted.");
        }
        catch (InvalidDataException)
        {
        }

        var directory = Path.Combine(Path.GetTempPath(), $"opentyrian-levels-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var firstPath = Path.Combine(directory, "tyrian1.lvl");
        var thirdPath = Path.Combine(directory, "tyrian3.lvl");
        try
        {
            File.WriteAllBytes(firstPath, archive.ToArray());
            File.WriteAllBytes(thirdPath, archive.ToArray());
            var episodes = new EpisodeState(new DataFiles(directory, directory));
            episodes.Scan();
            Check(episodes.Available.SequenceEqual(new[] { true, false, true, false, false }), "Episode scan differs.");
            episodes.Initialize(1);
            Check(episodes.LevelFile == "tyrian1.lvl" && episodes.CubeFile == "cubetxt1.dat" &&
                episodes.EpisodeFile == "levels1.dat" && episodes.Levels.Count == 3, "Episode file naming differs.");
            Check(episodes.FindNext() == 3 && !episodes.JumpBackToFirst, "Available episode selection differs.");
            episodes.Initialize(3);
            Check(episodes.FindNext() == 1 && episodes.JumpBackToFirst && episodes.GameHasRepeated, "Episode wrap flags differ.");
            Array.Clear(episodes.Available);
            Check(episodes.FindNext() == 3, "No alternative episode should return the current episode.");
            File.Delete(thirdPath);
            episodes.Initialize(3);
            Check(episodes.Number == 3, "Same-episode initialization should not reopen data.");
        }
        finally
        {
            File.Delete(firstPath);
            File.Delete(thirdPath);
            Directory.Delete(directory);
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
