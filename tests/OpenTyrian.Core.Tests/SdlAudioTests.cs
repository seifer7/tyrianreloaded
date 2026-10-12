using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace OpenTyrian.Core.Tests;

internal static class SdlAudioTests
{
    internal static void Run(string native)
    {
        var expected = new StringBuilder();
        foreach (var rate in new[] { 8000, 11025, 22050, 44100, 48000, 96000 })
        {
            var converter = new SdlAudioSampleConverter(rate);
            foreach (var size in new[] { 0, 1, 2, 7, 31, 256, 1024, 65535 })
            {
                for (var pattern = 0; pattern < 4; pattern++)
                {
                    var samples = new sbyte[size];
                    for (var i = 0; i < size; i++)
                        samples[i] = unchecked((sbyte)(pattern == 0 ? i * 73 + 128
                            : pattern == 1 ? 0 : pattern == 2 ? 128 : i % 2 != 0 ? 127 : 128));
                    var output = converter.Convert(samples);
                    var hash = 2166136261u;
                    foreach (var value in MemoryMarshal.AsBytes(output.AsSpan())) hash = unchecked((hash ^ value) * 16777619u);
                    expected.AppendLine($"convert:{rate},{size},{pattern},{output.Length},{hash}");
                }
            }
        }
        using var process = Process.Start(new ProcessStartInfo(Path.GetFullPath(native))
        { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true })
            ?? throw new InvalidOperationException("Could not start SDL audio oracle.");
        var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(30000)) { process.Kill(entireProcessTree: true); throw new TimeoutException("SDL audio oracle timed out."); }
        var actual = stdout.GetAwaiter().GetResult().Replace("\r\n", "\n", StringComparison.Ordinal);
        if (process.ExitCode != 0) throw new InvalidOperationException($"SDL audio oracle failed ({process.ExitCode}): {stderr.GetAwaiter().GetResult()}");
        var managed = expected.ToString().Replace("\r\n", "\n", StringComparison.Ordinal);
        if (actual != managed)
        {
            var a = actual.Split('\n'); var b = managed.Split('\n');
            var line = Enumerable.Range(0, Math.Min(a.Length, b.Length)).FirstOrDefault(i => a[i] != b[i], -1);
            throw new InvalidOperationException(line < 0 ? "SDL audio trace length differs." : $"SDL conversion differs at {line + 1}: native={a[line]}, managed={b[line]}.");
        }
        Throws<ArgumentOutOfRangeException>(() => new SdlAudioSampleConverter(0));
        Throws<ArgumentOutOfRangeException>(() => new SdlAudioSampleConverter(44100).Convert(new sbyte[65536]));
        using var effects = Bank(29, false); using var voices = Bank(9, true);
        var bank = new SoundBank();
        bank.Load(effects, voices, new SdlAudioSampleConverter(44100));
        for (var i = 0; i < 38; i++)
        {
            var length = (i < 29 ? i : i - 29) * 17;
            var raw = Enumerable.Range(0, length).Select(n => unchecked((sbyte)(n * 13))).ToArray();
            if (!bank.Samples[i].AsSpan().SequenceEqual(new SdlAudioSampleConverter(44100).Convert(raw)))
                throw new InvalidOperationException("Sound bank did not use real SDL conversion.");
        }
        var mixer = new AudioMixer(44100, new SilentMusic()) { MusicDisabled = true };
        bank.Play(5, 0, 4, mixer.Play);
        mixer.Mix(new short[4096]);
        if (mixer.RemainingSamples(0) != 0) throw new InvalidOperationException("Converted bank did not reach the PCM mixer.");
        TestDevice();
    }

    private static void TestDevice()
    {
        if (Environment.GetEnvironmentVariable("SDL_AUDIODRIVER") != "dummy")
            throw new InvalidOperationException("SDL audio device checks require SDL_AUDIODRIVER=dummy to avoid physical audio.");
        using (var device = new SdlAudioDevice(rate => new AudioMixer(rate, new SilentMusic())))
        {
            if (device.SampleRate != 44100 || device.BufferSamples != 1024)
                throw new InvalidOperationException("SDL dummy device does not match native 44.1 kHz/1024 mono request.");
            device.Mixer.Play(new short[4096], 0, 4);
            GC.Collect(); GC.WaitForPendingFinalizers();
            device.Start();
            WaitFor(() => device.CompletedCallbacks >= 4, device.ThrowIfFaulted);
            device.Pause();
            if (device.Mixer.RemainingSamples(0) != 0) throw new InvalidOperationException("Real SDL callback did not consume PCM channel samples.");
            var callbacks = device.CompletedCallbacks;
            Thread.Sleep(50);
            if (callbacks != device.CompletedCallbacks) throw new InvalidOperationException("Paused SDL device kept mixing.");
            device.Start(); WaitFor(() => device.CompletedCallbacks > callbacks, device.ThrowIfFaulted);
            device.Dispose(); device.Dispose();
            Throws<ObjectDisposedException>(device.Start);
        }
        Throws<IOException>(() => new SdlAudioDevice(_ => throw new IOException("Synthetic mixer factory failure.")));
        using (var recovered = new SdlAudioDevice(rate => new AudioMixer(rate, new SilentMusic())))
        {
            recovered.Start(); WaitFor(() => recovered.CompletedCallbacks > 0, recovered.ThrowIfFaulted);
        }
        var failed = new SdlAudioDevice(rate => new AudioMixer(rate, new FailingMusic()) { MusicStopped = false });
        failed.Start();
        WaitFor(() =>
        {
            try { failed.ThrowIfFaulted(); return false; } catch (IOException) { return true; }
        }, () => { });
        Throws<IOException>(failed.Pause);
        Throws<IOException>(failed.Start);
        Throws<IOException>(failed.Dispose);
        failed.Dispose();
        using var afterFailure = new SdlAudioDevice(rate => new AudioMixer(rate, new SilentMusic()));
        afterFailure.Start(); WaitFor(() => afterFailure.CompletedCallbacks > 0, afterFailure.ThrowIfFaulted);
    }

    private static void WaitFor(Func<bool> ready, Action checkFault)
    {
        var timer = Stopwatch.StartNew();
        while (!ready())
        {
            checkFault();
            if (timer.ElapsedMilliseconds > 5000) throw new TimeoutException("SDL audio callbacks did not progress.");
            Thread.Sleep(5);
        }
    }

    private static MemoryStream Bank(ushort count, bool voices)
    {
        var stream = new MemoryStream();
        BinaryFile.WriteUInt16(stream, count);
        var offset = 2 + count * 4;
        for (var i = 0; i < count; i++)
        {
            BinaryFile.WriteUInt32(stream, (uint)offset);
            offset += i * 17 + (voices ? 100 : 0);
        }
        for (var i = 0; i < count; i++)
        {
            for (var n = 0; n < i * 17; n++) stream.WriteByte(unchecked((byte)(n * 13)));
            if (voices) stream.Write(new byte[100]);
        }
        stream.Position = 0;
        return stream;
    }

    private sealed class SilentMusic : IMusicSynthesizer
    {
        public void Update() => throw new InvalidOperationException("Disabled music updated.");
        public void Render(Span<short> samples) => throw new InvalidOperationException("Disabled music rendered.");
    }

    private sealed class FailingMusic : IMusicSynthesizer
    {
        public void Update() => throw new IOException("Synthetic audio callback failure.");
        public void Render(Span<short> samples) => throw new IOException("Synthetic audio callback failure.");
    }

    private static void Throws<T>(Action action) where T : Exception
    {
        try { action(); } catch (T) { return; }
        throw new InvalidOperationException($"Expected {typeof(T).Name}.");
    }
}
