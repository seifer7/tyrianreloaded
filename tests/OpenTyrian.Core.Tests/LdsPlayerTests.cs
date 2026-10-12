using System.Diagnostics;
using System.Text;
using OpenTyrian.Core;

namespace OpenTyrian.Core.Tests;

internal static class LdsPlayerTests
{
    public static void Run(string? executable = null)
    {
        var path = Path.Combine(Path.GetTempPath(), $"opentyrian-lds-{Guid.NewGuid():N}.lds");
        try
        {
            for (var scenario = 0; scenario < 24; scenario++)
            {
                var fixture = Fixture(scenario);
                var output = Execute(fixture, scenario);
                if (executable is not null)
                {
                    File.WriteAllBytes(path, fixture);
                    CompareNative(executable, path, scenario, output);
                }
            }
            TestFailuresAndIntegration();
        }
        finally { File.Delete(path); }
    }

    private static byte[] Fixture(int mode)
    {
        using var output = new MemoryStream();
        output.WriteByte((byte)(mode % 3)); BinaryFile.WriteUInt16(output, (ushort)(0x4300 + mode));
        output.WriteByte((byte)(mode % 3)); output.WriteByte(32);
        for (var i = 0; i < 9; i++) output.WriteByte((byte)((i + mode) % 4));
        output.WriteByte((byte)(mode * 7));
        BinaryFile.WriteUInt16(output, 64);
        for (var i = 0; i < 64; i++)
        {
            var patch = new byte[46];
            for (var j = 0; j < patch.Length; j++) patch[j] = unchecked((byte)(i * 31 + j * 19 + mode * 7));
            patch[10] = (byte)(i % 16);
            patch[11] = (byte)(i % 10); patch[12] = (byte)(i % 4);
            patch[13] = (byte)(i % 3 == 0 ? 0 : i % 3 == 1 ? 16 : 240);
            patch[14] = (byte)(i % 4 == 0 ? 0 : i % 4 == 1 ? 127 : i % 4 == 2 ? 128 : 255);
            patch[15] = (byte)(i % 4 == 0 ? 0 : i * 7); patch[16] = (byte)(i % 7);
            patch[19] = (byte)(i % 4 == 0 ? 0 : i);
            patch[20] = (byte)((i % 3 << 4) | (i % 5 == 4 ? 12 : i % 5));
            for (var j = 0; j < 12; j++) patch[21 + j] = (byte)(j % 5 == 0 ? 255 : j % 5 == 1 ? 128 : j);
            output.Write(patch);
        }
        BinaryFile.WriteUInt16(output, 4);
        for (var position = 0; position < 4; position++)
            for (var channel = 0; channel < 9; channel++)
            {
                BinaryFile.WriteUInt16(output, (ushort)((position * 9 + channel) * 32 * 2));
                byte[] transposes = [0, 1, 127, 64, 128, 129, 255, 192, 63];
                output.WriteByte(transposes[(channel + mode) % 9]);
            }
        BinaryFile.WriteUInt16(output, 0);
        for (var position = 0; position < 4; position++)
            for (var channel = 0; channel < 9; channel++)
                for (var row = 0; row < 32; row++)
                {
                    var note = (ushort)(((80 + (row + channel) % 40) << 8) | ((mode + channel + row) % 64));
                    var command = row switch
                    {
                        1 => 0xFD30,
                        3 => 0xF734,
                        4 => 0xF605,
                        6 => 0xF5F0,
                        8 => 0xF4C0,
                        9 => 0xF302,
                        10 => 0xF2FF,
                        11 => 0xFF20,
                        12 => 0xF800,
                        14 => 0xFB00,
                        15 => 0xFE00 | (mode % 3),
                        16 => 0xF101,
                        17 => 0xF001,
                        18 => 0xA101,
                        19 => 0x8100,
                        21 => 0x8002,
                        26 => mode % 4 == 0 ? (position == 3 ? 0xFC00 : 0xFA00) : 0xF900 | ((position + 1) % 4),
                        _ => note
                    };
                    // Break and stop commands affect the whole order; one channel
                    // issues them while the other eight still process their rows.
                    if (row == 26 && channel != 8) command = note;
                    BinaryFile.WriteUInt16(output, (ushort)command);
                }
        output.WriteByte(0x99);
        return output.ToArray();
    }

    private static string Execute(byte[] fixture, int scenario)
    {
        var output = new StringBuilder();
        var player = new LdsPlayer(new Opl(output));
        player.Load(fixture);
        State();
        for (var frame = 0; frame < 384; frame++)
        {
            output.Append($"frame:{frame}\n");
            if (frame == 128) player.Fade((byte)(scenario % 2 != 0 ? 129 : 7));
            if (frame == 256) player.Rewind();
            if (frame == 300)
            {
                player.SetRegister(0x40, 0x7F); player.SetRegister(0x40, 0x7F);
                player.SetRegisterMasked(0x40, 0xC0, 0x12);
            }
            var active = player.Advance();
            output.Append($"advance:{(active ? 1 : 0)}\n"); State();
        }
        output.Append("notes\n"); player.Rewind();
        for (var i = 0; i < 64; i++)
        {
            output.Append($"note:{i}\n");
            player.PlaySound(i, i % 9, 512 + i * 13);
            player.Update(); State();
        }
        player.Free();
        Check(!player.Playing && !player.Advance(), "Freed LDS player retained active playback.");
        return output.ToString();
        void State() => output.Append($"state:{(player.Playing ? 1 : 0)},{(player.SongLooped ? 1 : 0)},{player.Row},{player.PositionIndex},{player.Tempo},{player.TempoNow},{player.AllVolume},{player.FadeSpeed}\n");
    }

    private static void TestFailuresAndIntegration()
    {
        var output = new StringBuilder();
        var opl = new Opl(output);
        var player = new LdsPlayer(opl);
        var fixture = Fixture(1);
        foreach (var length in new[] { 0, 1, 16, 17, 50, 2962, 2970 })
            Throws<EndOfStreamException>(() => player.Load(fixture.AsSpan(0, length)));
        var malformed = fixture.ToArray(); malformed[0] = 3;
        Throws<InvalidDataException>(() => player.Load(malformed));
        player.Load(fixture);
        Throws<InvalidDataException>(() => player.PlaySound(64, 0, 512));
        Throws<InvalidDataException>(() => player.PlaySound(0, 9, 512));
        Throws<InvalidDataException>(() => player.PlaySound(0, 0, -1000));
        Throws<InvalidDataException>(() => player.SetRegister(255, 1));
        Throws<InvalidDataException>(() => player.SetRegisterMasked(255, 0, 1));
        var badPattern = fixture.ToArray();
        badPattern[17 + 64 * 46 + 2] = badPattern[17 + 64 * 46 + 3] = 255;
        player.Load(badPattern);
        for (var i = 0; i < 3; i++) player.Update();
        Throws<InvalidDataException>(() => player.Update());
        player.Load(fixture);
        var mixer = new AudioMixer(44100, player) { MusicStopped = false };
        var samples = new short[44100];
        mixer.Mix(samples);
        Check(opl.Rendered == 44100 && samples.Any(value => value != 0) && output.ToString().Contains("write:", StringComparison.Ordinal),
            "Managed LDS updates did not drive OPL registers and mixer rendering.");
        using var archiveBytes = new MemoryStream();
        BinaryFile.WriteUInt16(archiveBytes, 2);
        BinaryFile.WriteUInt32(archiveBytes, 10); BinaryFile.WriteUInt32(archiveBytes, 10);
        archiveBytes.Write(fixture); archiveBytes.Position = 0;
        var controller = new MusicController(MusicArchive.Load(archiveBytes), 44100, player);
        controller.PlaySong(1); controller.Mixer.Mix(new short[2048]);
        Check(controller.SongPlaying == 1 && !controller.Mixer.MusicStopped && opl.Rendered == 46148,
            "Music archive/control/mixer integration did not reach the LDS sequencer.");
        controller.Stop(); controller.Mixer.Mix(new short[16]);
        Check(opl.Rendered == 46148, "Stopped music still invoked OPL synthesis.");
        var invalidArpeggio = fixture.ToArray();
        invalidArpeggio[17 + 20] = 13;
        invalidArpeggio.AsSpan(17 + 21, 12).Fill(1);
        for (var i = 0; i < 36; i++)
        {
            var start = 3073 + i * 64;
            invalidArpeggio[start] = 255; invalidArpeggio[start + 1] = 128;
        }
        player.Load(invalidArpeggio);
        for (var i = 0; i < 3; i++) player.Update();
        player.PlaySound(0, 0, 512);
        Throws<InvalidDataException>(() =>
        {
            for (var i = 0; i < 13; i++) player.Update();
        });
    }

    private static void CompareNative(string executable, string path, int scenario, string expected)
    {
        var start = new ProcessStartInfo(executable) { RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add(path); start.ArgumentList.Add(scenario.ToString(System.Globalization.CultureInfo.InvariantCulture));
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Cannot start LDS oracle.");
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(30000))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException("LDS oracle timed out.");
        }
        var actual = stdout.GetAwaiter().GetResult().Replace("\r\n", "\n", StringComparison.Ordinal);
        var error = stderr.GetAwaiter().GetResult();
        if (process.ExitCode == 0 && actual == expected && error.Length == 0) return;
        var actualLines = actual.Split('\n'); var expectedLines = expected.Split('\n');
        var first = Enumerable.Range(0, Math.Min(actualLines.Length, expectedLines.Length))
            .FirstOrDefault(i => actualLines[i] != expectedLines[i], -1);
        throw new InvalidOperationException($"Native LDS parity differs in scenario {scenario} at {first}: {(first < 0 ? "" : actualLines[first] + " / " + expectedLines[first])}; {error}");
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

    private sealed class Opl(StringBuilder output) : IOplSynthesizer
    {
        private uint _signature;
        public int Rendered { get; private set; }
        public void Reset() { _signature = 1; output.Append("reset\n"); }
        public void Write(byte register, byte value)
        {
            _signature = unchecked(_signature * 31 + register * 256u + value);
            output.Append($"write:{register},{value}\n");
        }
        public void Render(Span<short> samples)
        {
            for (var i = 0; i < samples.Length; i++) samples[i] = unchecked((short)(_signature + (uint)Rendered++));
        }
    }
}
