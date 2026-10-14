using OpenTyrian.Core;

namespace OpenTyrian.Core.Tests;

internal static class GameRuntimeAudioTests
{
    public static void Run()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"opentyrian-runtime-audio-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            File.WriteAllBytes(Path.Combine(directory, "tyrian1.lvl"), []);
            File.WriteAllBytes(Path.Combine(directory, "music.mus"), MusicArchiveBytes());
            File.WriteAllBytes(Path.Combine(directory, "tyrian.snd"), SoundBankTests.BuildBank(29, false, 0));
            File.WriteAllBytes(Path.Combine(directory, "voices.snd"), SoundBankTests.BuildBank(9, true, 1));
            File.WriteAllBytes(Path.Combine(directory, "voicesc.snd"), SoundBankTests.BuildBank(9, true, 2));

            var converter = new IdentityConverter();
            var files = new DataFiles(directory, directory);
            using (var audio = GameRuntimeAudio.Load(files, false, converter))
            {
                Check(audio.Music.Count == 1 && audio.Music.Song(0).Span.SequenceEqual(new byte[] { 1, 2, 3 }) &&
                    audio.Sounds.Samples.Count == 38 && converter.InputLengths.Count == 38,
                    "Runtime audio loader did not compose music and all standard samples.");
                Check(audio.Sounds.Samples[32].AsSpan().SequenceEqual(new short[] { 58 }),
                    "Runtime audio loader did not select the standard voice bank.");
            }

            using (var audio = GameRuntimeAudio.Load(files, true, new IdentityConverter()))
                Check(audio.Sounds.Samples[32].AsSpan().SequenceEqual(new short[] { 59 }),
                    "Runtime audio loader did not select the seasonal voice bank.");

            using (var audio = GameRuntimeAudio.Load(files, false, new IdentityConverter()))
            {
                var samples = audio.Sounds.Samples;
                audio.Dispose();
                Check(samples.All(sample => sample.Length == 0),
                    "Disposing runtime audio did not release converted sound samples.");
            }

            File.Delete(Path.Combine(directory, "voicesc.snd"));
            Throws<IOException>(() => GameRuntimeAudio.Load(files, true, new IdentityConverter()));
            File.WriteAllBytes(Path.Combine(directory, "voicesc.snd"), SoundBankTests.BuildBank(9, true, 2));
            File.Delete(Path.Combine(directory, "music.mus"));
            Throws<IOException>(() => GameRuntimeAudio.Load(files, false, new IdentityConverter()));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static byte[] MusicArchiveBytes()
    {
        using var stream = new MemoryStream();
        BinaryFile.WriteUInt16(stream, 1);
        BinaryFile.WriteUInt32(stream, 6);
        stream.Write([1, 2, 3]);
        return stream.ToArray();
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void Throws<T>(Action action) where T : Exception
    {
        try { action(); }
        catch (T) { return; }
        throw new InvalidOperationException($"Expected {typeof(T).Name}.");
    }

    private sealed class IdentityConverter : IAudioSampleConverter
    {
        public List<int> InputLengths { get; } = [];

        public short[] Convert(ReadOnlySpan<sbyte> samples)
        {
            InputLengths.Add(samples.Length);
            return samples.ToArray().Select(sample => (short)sample).ToArray();
        }
    }
}
