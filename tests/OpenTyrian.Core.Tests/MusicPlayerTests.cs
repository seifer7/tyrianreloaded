using System.Buffers.Binary;
using System.Diagnostics;
using System.Text;
using OpenTyrian.Core;

namespace OpenTyrian.Core.Tests;

internal static class MusicPlayerTests
{
    public static void Run(string? executable = null)
    {
        var directory = Path.Combine(Path.GetTempPath(), $"opentyrian-music-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "music.mus");
        try
        {
            for (var mode = 0; mode < 4; mode++)
            {
                var fixture = Fixture(mode);
                File.WriteAllBytes(path, fixture);
                using var file = new MemoryStream(fixture);
                var archive = MusicArchive.Load(file);
                var output = new StringBuilder($"count:{archive.Count}\n");
                var controller = new MusicController(archive, 44100, new Player(output));
                for (var i = 0; i < 12; i++)
                {
                    controller.Mixer.AudioDisabled = i % 5 == 3;
                    controller.PlaySong((uint)(i % 3)); State(i * 4);
                    controller.Fade(); State(i * 4 + 1);
                    controller.Stop(); State(i * 4 + 2);
                    controller.Restart(); State(i * 4 + 3);
                }
                if (executable is not null) CompareNative(executable, directory, output.ToString());
                void State(int step) => output.Append($"state:{step},{controller.SongPlaying},{(controller.Mixer.MusicStopped ? 1 : 0)}\n");
            }
            TestFailures();
        }
        finally { File.Delete(path); Directory.Delete(directory); }
    }

    private static byte[] Fixture(int mode)
    {
        using var output = new MemoryStream();
        BinaryFile.WriteUInt16(output, 3);
        int[] lengths = [0, 73 + mode, 65536 + mode];
        var position = 14;
        foreach (var length in lengths) { BinaryFile.WriteUInt32(output, (uint)position); position += length; }
        for (var song = 0; song < 3; song++)
            for (var i = 0; i < lengths[song]; i++) output.WriteByte((byte)((i * 31 + song * 53 + mode) & 255));
        return output.ToArray();
    }

    private static void TestFailures()
    {
        foreach (var length in new[] { 0, 1, 3, 13 })
        {
            using var input = new MemoryStream(Fixture(0)[..length]);
            Throws<EndOfStreamException>(() => MusicArchive.Load(input));
        }
        var malformed = Fixture(0);
        BinaryPrimitives.WriteUInt32LittleEndian(malformed.AsSpan(2), uint.MaxValue);
        using (var input = new MemoryStream(malformed)) Throws<InvalidDataException>(() => MusicArchive.Load(input));
        using var file = new MemoryStream(Fixture(0));
        var archive = MusicArchive.Load(file);
        var output = new StringBuilder();
        var player = new Player(output);
        var controller = new MusicController(archive, 44100, player);
        controller.PlaySong(1);
        Throws<InvalidDataException>(() => controller.PlaySong(uint.MaxValue));
        Check(controller.SongPlaying == 1 && controller.Mixer.MusicStopped, "Invalid song published a playing state.");
        player.Fail = true;
        Throws<IOException>(() => controller.PlaySong(2));
        Check(controller.SongPlaying == 1 && controller.Mixer.MusicStopped, "Failed music load discarded the previous selection.");
        Throws<IOException>(() => controller.Restart());
        Throws<IOException>(() => controller.Fade());
        controller.Mixer.AudioDisabled = true;
        controller.PlaySong(2); controller.Restart(); controller.Fade();
        Check(controller.SongPlaying == 1, "Disabled audio invoked music operations.");
        using var empty = new MemoryStream(new byte[] { 0, 0 });
        Check(MusicArchive.Load(empty).Count == 0, "Empty music directory differs.");
    }

    private static uint Hash(ReadOnlySpan<byte> bytes)
    {
        uint hash = 2166136261;
        foreach (var value in bytes) hash = unchecked((hash ^ value) * 16777619);
        return hash;
    }

    private static void CompareNative(string executable, string directory, string expected)
    {
        var start = new ProcessStartInfo(executable) { RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add(directory);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Cannot start music oracle.");
        var output = process.StandardOutput.ReadToEndAsync();
        var errors = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(30000))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException("Music oracle timed out.");
        }
        var actual = output.GetAwaiter().GetResult().Replace("\r\n", "\n", StringComparison.Ordinal);
        var error = errors.GetAwaiter().GetResult();
        Check(process.ExitCode == 0 && actual == expected && error.Length == 0, $"Native music control parity differs: {error}");
    }

    private static void Throws<T>(Action action) where T : Exception
    {
        try { action(); }
        catch (T) { return; }
        throw new InvalidOperationException($"Expected {typeof(T).Name}.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class Player(StringBuilder output) : IMusicPlayer
    {
        public bool Fail { get; set; }
        public void Load(ReadOnlySpan<byte> song)
        {
            if (Fail) throw new IOException("song failure");
            output.Append($"load:{song.Length},{Hash(song)}\n");
        }
        public void Rewind()
        {
            if (Fail) throw new IOException("rewind failure");
            output.Append("rewind\n");
        }
        public void Fade(byte speed)
        {
            if (Fail) throw new IOException("fade failure");
            output.Append($"fade:{speed}\n");
        }
        public void Update() => throw new InvalidOperationException("Playback synthesis is not exercised by the control oracle.");
        public void Render(Span<short> samples) => throw new InvalidOperationException("Playback synthesis is not exercised by the control oracle.");
    }
}
