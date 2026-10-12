using System.Diagnostics;
using System.Text;
using OpenTyrian.Core;

namespace OpenTyrian.Core.Tests;

internal static class AudioMixerTests
{
    public static void Run(string? executable = null)
    {
        var output = Execute();
        TestFailuresAndOwnership();
        if (executable is null) return;
        var start = new ProcessStartInfo(executable) { RedirectStandardOutput = true, RedirectStandardError = true };
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Cannot start native audio mixer.");
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(30000))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException("Native audio mixer timed out.");
        }
        var actual = stdout.GetAwaiter().GetResult().Replace("\r\n", "\n", StringComparison.Ordinal);
        var errors = stderr.GetAwaiter().GetResult();
        // Volume factors are also covered through mixed samples; the native dump
        // makes a floating-point mismatch distinguishable from a scheduling error.
        var lines = actual.Split('\n');
        actual = string.Join('\n', lines.Skip(256));
        if (process.ExitCode != 0 || actual != output || errors.Length > 0)
        {
            var expectedLines = output.Split('\n');
            var actualLines = actual.Split('\n');
            var first = Enumerable.Range(0, Math.Min(expectedLines.Length, actualLines.Length))
                .FirstOrDefault(i => expectedLines[i] != actualLines[i], -1);
            throw new InvalidOperationException($"Audio mixer parity differs at {first}: {(first < 0 ? "" : actualLines[first] + " / " + expectedLines[first])}; {errors}");
        }
    }

    private static string Execute()
    {
        var output = new StringBuilder();
        int[] rates = [44100, 48000, 11025, 139, 22050];
        int[] sizes = [0, 1, 127, 3, 1024, 13];
        for (var mode = 0; mode < 256; mode++)
        {
            var music = new Synthesizer();
            var mixer = new AudioMixer(rates[mode % 5], music)
            {
                MusicDisabled = (mode & 1) != 0,
                MusicStopped = (mode & 2) != 0
            };
            mixer.SetVolume((byte)mode, (byte)((mode * 73) % 256));
            var channels = new short[8][];
            for (byte channel = 0; channel < 8; channel++)
            {
                channels[channel] = Enumerable.Range(0, 257)
                    .Select(i => unchecked((short)((i * 1777 + channel * 8191 + mode * 31) & 65535))).ToArray();
                mixer.Play(channels[channel].AsMemory(0, (mode + channel * 37) % 258), channel, (byte)((mode + channel) % 8));
            }
            mixer.SamplesDisabled = (mode & 4) != 0;
            output.Append($"mode:{mode},{rates[mode % 5]}\n");
            for (var frame = 0; frame < sizes.Length; frame++)
            {
                if (frame == 2)
                {
                    mixer.AudioDisabled = (mode & 8) != 0;
                    mixer.SetVolume((byte)((mode + 11) % 256), (byte)((mode + 23) % 256));
                    mixer.Play(channels[3].AsMemory(0, 17), 3, 7);
                    mixer.AudioDisabled = false;
                }
                var samples = new short[sizes[frame]];
                mixer.Mix(samples);
                output.Append($"frame:{frame},{music.Generated},{music.Updates},{mixer.SamplesUntilUpdate},{mixer.SamplesUntilUpdateFraction}");
                for (byte channel = 0; channel < 8; channel++) output.Append($",{mixer.RemainingSamples(channel)}");
                output.Append($"\npcm:{string.Join(',', samples)}\n");
            }
        }
        return output.ToString();
    }

    private static void TestFailuresAndOwnership()
    {
        var mixer = new AudioMixer(44100, new Synthesizer());
        short[] samples = [short.MaxValue, short.MinValue, 1];
        mixer.Play(samples, 0, 7);
        var result = new short[1];
        mixer.Mix(result);
        Check(result[0] == short.MaxValue && mixer.RemainingSamples(0) == 2, "Channel volume/consumption differs.");
        mixer.Play(new short[] { -7 }, 0, 7);
        mixer.Mix(result);
        Check(result[0] == -7 && mixer.RemainingSamples(0) == 0, "Channel replacement differs.");
        for (byte channel = 0; channel < 8; channel++) mixer.Play(samples, channel, 7);
        mixer.Mix(new short[2]);
        mixer.ClearChannels();
        Check(Enumerable.Range(0, 8).All(channel => mixer.RemainingSamples((byte)channel) == 0), "Channel cleanup retains pending audio.");
        Throws<ArgumentOutOfRangeException>(() => new AudioMixer(0, new Synthesizer()));
        Throws<ArgumentOutOfRangeException>(() => mixer.Play(samples, 8, 0));
        Throws<ArgumentOutOfRangeException>(() => mixer.Play(samples, 0, 8));
        Throws<ArgumentOutOfRangeException>(() => mixer.RemainingSamples(8));
        var broken = new AudioMixer(44100, new BrokenSynthesizer()) { MusicStopped = false };
        Throws<IOException>(() => broken.Mix(result));
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

    private sealed class Synthesizer : IMusicSynthesizer
    {
        public uint Generated { get; private set; }
        public uint Updates { get; private set; }
        public void Update() => Updates++;
        public void Render(Span<short> samples)
        {
            for (var i = 0; i < samples.Length; i++) samples[i] = unchecked((short)((Generated++ * 7919 + Updates * 97) & 65535));
        }
    }

    private sealed class BrokenSynthesizer : IMusicSynthesizer
    {
        public void Update() => throw new IOException("music failure");
        public void Render(Span<short> samples) => throw new IOException("synthesis failure");
    }
}
