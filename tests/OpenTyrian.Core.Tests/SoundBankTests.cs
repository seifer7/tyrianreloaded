using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using OpenTyrian.Core;

namespace OpenTyrian.Core.Tests;

internal static class SoundBankTests
{
    public static void Run(string? executable = null)
    {
        var directory = Path.Combine(Path.GetTempPath(), $"opentyrian-sounds-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var paths = new[] { "tyrian1.lvl", "tyrian.snd", "voices.snd", "voicesc.snd" }.Select(name => Path.Combine(directory, name)).ToArray();
        try
        {
            File.WriteAllBytes(paths[0], []);
            for (var mode = 0; mode < 3; mode++)
            {
                File.WriteAllBytes(paths[1], BuildBank(29, false, mode));
                File.WriteAllBytes(paths[2], BuildBank(9, true, mode));
                File.WriteAllBytes(paths[3], BuildBank(9, true, mode + 3));
                for (var christmas = 0; christmas < 2; christmas++)
                {
                    var output = new StringBuilder();
                    var bank = new SoundBank();
                    bank.Load(new DataFiles(directory, directory), christmas != 0, new Converter(output));
                    for (var i = 0; i < bank.Samples.Count; i++)
                    {
                        var sample = bank.Samples[i];
                        output.Append($"sample:{i},{sample.Length},{Hash(MemoryMarshal.AsBytes(sample.AsSpan()))}\n");
                    }
                    for (byte id = 1; id <= 38; id++)
                        bank.PlaySample(id, (sample, channel, volume) =>
                            output.Append($"play:{channel},{volume},{sample.Length},{Hash(MemoryMarshal.AsBytes(sample.Span))}\n"));
                    if (executable is not null) CompareNative(executable, directory, christmas, output.ToString());
                    TestSoundDispatch(bank);
                }
            }
            TestFailures();
        }
        finally
        {
            foreach (var path in paths) File.Delete(path);
            Directory.Delete(directory);
        }
    }

    private static byte[] BuildBank(int count, bool voice, int mode)
    {
        var samples = Enumerable.Range(0, count).Select(i =>
        {
            var size = i switch
            {
                0 => 0,
                1 => voice ? 50 : 1,
                2 => voice ? 100 : 255,
                3 => voice ? 101 : 256,
                4 => ushort.MaxValue + (voice ? 100 : 0),
                _ => i * 11 + mode * 7
            };
            return Enumerable.Range(0, size).Select(j => (byte)((j * 37 + i * 19 + mode) & 255)).ToArray();
        }).ToArray();
        using var output = new MemoryStream();
        BinaryFile.WriteUInt16(output, (ushort)count);
        var offset = 2 + count * 4;
        foreach (var sample in samples)
        {
            BinaryFile.WriteUInt32(output, (uint)offset);
            offset += sample.Length;
        }
        foreach (var sample in samples) output.Write(sample);
        return output.ToArray();
    }

    private static void TestSoundDispatch(SoundBank bank)
    {
        var definitions = new ItemDefinitions();
        definitions.ParseBundle(Encoding.Latin1.GetBytes(LevelEventTests.Fixture()), "sound-dispatch.tyrr");
        var items = ItemDatabase.FromDefinitions(definitions);
        using var warnings = new StringWriter();
        var effects = new SceneEffects(new byte[8]);
        var events = new LevelEvents(items, new EnemySystem(items, warnings),
            new[] { new PlayerState(0), new PlayerState(1) }, new BackgroundRenderer(), effects)
        { FirstGameOver = true };
        effects.SoundQueue[3] = 15; effects.SoundQueue[7] = 38;
        var played = new List<(int Length, byte Channel, byte Volume)>();
        events.PlaySounds((id, channel, volume) =>
            bank.Play(id, channel, volume, (samples, actualChannel, actualVolume) => played.Add((samples.Length, actualChannel, actualVolume))));
        Check(played.SequenceEqual(new[] { (bank.Samples[14].Length, (byte)3, (byte)4), (bank.Samples[37].Length, (byte)7, (byte)2) }) &&
            effects.SoundQueue.All(id => id == 0), "Sound-bank data did not reach queued playback.");
    }

    private static void TestFailures()
    {
        var bank = new SoundBank();
        var converter = new Converter(new StringBuilder());
        var effects = BuildBank(29, false, 0);
        var voices = BuildBank(9, true, 0);
        using (var first = new MemoryStream(effects))
        using (var second = new MemoryStream(voices)) bank.Load(first, second, converter);
        var retained = bank.Samples;
        foreach (var offset in new[] { 0, 2, 100 })
        {
            using var first = new MemoryStream(effects[..offset]);
            using var second = new MemoryStream(voices);
            Throws<EndOfStreamException>(() => bank.Load(first, second, converter));
        }
        var wrongCount = effects.ToArray();
        wrongCount[0] = 28;
        using (var first = new MemoryStream(wrongCount))
        using (var second = new MemoryStream(voices)) Throws<InvalidDataException>(() => bank.Load(first, second, converter));
        var wrongPosition = effects.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(wrongPosition.AsSpan(2), uint.MaxValue);
        using (var first = new MemoryStream(wrongPosition))
        using (var second = new MemoryStream(voices)) Throws<InvalidDataException>(() => bank.Load(first, second, converter));
        var oversized = effects.Concat(new byte[65536]).ToArray();
        using (var first = new MemoryStream(oversized))
        using (var second = new MemoryStream(voices)) Throws<InvalidDataException>(() => bank.Load(first, second, converter));
        using (var first = new MemoryStream(effects))
        using (var second = new MemoryStream(voices)) Throws<IOException>(() => bank.Load(first, second, new BrokenConverter()));
        Check(ReferenceEquals(bank.Samples, retained), "Failed loading replaced a valid bank.");
        foreach (var id in new byte[] { 0, 39 }) Throws<InvalidDataException>(() => bank.PlaySample(id, (_, _, _) => { }));
        Throws<ArgumentOutOfRangeException>(() => bank.Play(1, 8, 4, (_, _, _) => { }));
        Throws<ArgumentOutOfRangeException>(() => bank.Play(1, 0, 8, (_, _, _) => { }));
    }

    private static uint Hash(ReadOnlySpan<byte> bytes)
    {
        uint hash = 2166136261;
        foreach (var value in bytes) hash = unchecked((hash ^ value) * 16777619);
        return hash;
    }

    private static void CompareNative(string executable, string directory, int christmas, string expected)
    {
        var start = new ProcessStartInfo(executable) { RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add(directory); start.ArgumentList.Add(christmas.ToString(System.Globalization.CultureInfo.InvariantCulture));
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Cannot start sound-bank oracle.");
        var output = process.StandardOutput.ReadToEndAsync();
        var errors = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(30000))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException("Sound-bank oracle timed out.");
        }
        var actual = output.GetAwaiter().GetResult().Replace("\r\n", "\n", StringComparison.Ordinal);
        var error = errors.GetAwaiter().GetResult();
        Check(process.ExitCode == 0 && actual == expected && error.Length == 0, $"Native sound-bank parity differs: {error}");
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

    private sealed class Converter(StringBuilder output) : IAudioSampleConverter
    {
        public short[] Convert(ReadOnlySpan<sbyte> samples)
        {
            output.Append($"convert:{samples.Length},{Hash(MemoryMarshal.AsBytes(samples))}\n");
            var converted = new short[samples.Length];
            for (var i = 0; i < samples.Length; i++) converted[i] = (short)(samples[i] * 256);
            return converted;
        }
    }

    private sealed class BrokenConverter : IAudioSampleConverter
    {
        public short[] Convert(ReadOnlySpan<sbyte> samples) => throw new IOException("conversion failure");
    }
}
