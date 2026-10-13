using System.Diagnostics;
using System.Text;
using OpenTyrian.Core;

namespace OpenTyrian.Core.Tests;

internal static class EpisodeWarningDisplayTests
{
    private static readonly string[] FrameCounts =
    [
        "0", "1", "9", "10", "27", " 15tail", "-1", "65537", "65539",
        "abc", "+", "  -10", "32767", "999",
    ];

    public static void Run(string? executable = null)
    {
        var trace = new StringBuilder();
        for (var mode = 0; mode < 256; mode++)
        {
            var platform = new InputPlatform();
            var input = new InputState(platform, new());
            input.EscapePressed = (mode & 2) != 0;
            input.NewKey = (mode & 4) != 0;
            var timer = new DelayTimer(input);
            var settings = new GameRuntimeSettings();
            settings.SetFrameCountMax((uint)(mode % 7 + 1));
            var warning = new WarningBars(new IndexedSurface(320, 200, 320, TextWriter.Null), timer, () => { }, _ => { })
            {
                Color = (byte)(mode * 3),
                ColorChange = unchecked((sbyte)(mode - 128)),
                SoundDelay = (byte)(mode * 5),
            };
            var glow = new TextGlow(new TraceGlowHost()) { WarningRed = (mode & 8) != 0 };
            var state = new EpisodeWarningState
            {
                DisplayWarning = (mode & 16) != 0,
                Lines = new ReadOnlyMemory<byte>[] { "stale-a"u8.ToArray(), "stale-b"u8.ToArray() }
                    .Take(mode % 3).ToArray(),
            };
            var warningEnabled = state.DisplayWarning;
            var display = new TracePresenter(trace);
            var flow = new EpisodeWarningDisplay(settings, warning, glow, input, state,
                () => trace.AppendLine("wipe"), frames => trace.AppendLine($"delay2:{frames}"),
                value => { warningEnabled = value; }, display);

            var section = Encoding.ASCII.GetBytes($"W?{((mode & 64) != 0 ? 'y' : 'n')}?{FrameCounts[mode % FrameCounts.Length]}");
            using var episode = MakeEpisode(mode);
            var constantPlay = (mode & 1) != 0;
            trace.AppendLine($"case:{mode},{(constantPlay ? 1 : 0)},{(input.EscapePressed ? 1 : 0)},{Encoding.ASCII.GetString(section)}");
            flow.Show(episode, section, constantPlay);
            trace.AppendLine($"state:{settings.FrameCountMax},{(glow.WarningRed ? 1 : 0)}," +
                $"{(state.DisplayWarning ? 1 : 0)},{warning.Color},{warning.ColorChange}," +
                $"{warning.SoundDelay},{(input.NewKey ? 1 : 0)},{episode.Position}," +
                $"{state.Lines.Length},{HexLines(state.Lines)}");
            AssertState(mode, settings, warning, glow, input, state, warningEnabled, episode);
        }

        if (executable is null) return;
        using var process = Process.Start(new ProcessStartInfo(executable)
        { RedirectStandardOutput = true, RedirectStandardError = true })
            ?? throw new InvalidOperationException("Cannot start episode-warning reference.");
        var output = process.StandardOutput.ReadToEndAsync();
        var errors = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(30000))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException("Episode-warning reference timed out.");
        }

        var expected = output.GetAwaiter().GetResult().Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var actual = trace.ToString().Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        if (process.ExitCode != 0 || !expected.SequenceEqual(actual))
        {
            var index = Enumerable.Range(0, Math.Min(expected.Length, actual.Length))
                .FirstOrDefault(i => expected[i] != actual[i], -1);
            throw new InvalidOperationException($"Episode-warning display differs at {index}: " +
                $"{expected.ElementAtOrDefault(index)} / {actual.ElementAtOrDefault(index)}; " +
                errors.GetAwaiter().GetResult());
        }
    }

    private static void AssertState(int mode, GameRuntimeSettings settings, WarningBars warning,
        TextGlow glow, InputState input, EpisodeWarningState state, bool warningEnabled, MemoryStream episode)
    {
        var skipped = (mode & 3) != 0;
        if (skipped)
        {
            if (warning.Color != (byte)(mode * 3) || warning.ColorChange != unchecked((sbyte)(mode - 128)) ||
                warning.SoundDelay != (byte)(mode * 5) || settings.FrameCountMax != (uint)(mode % 7 + 1) ||
                glow.WarningRed != ((mode & 8) != 0) || state.DisplayWarning != ((mode & 16) != 0) ||
                warningEnabled != state.DisplayWarning || input.NewKey != ((mode & 4) != 0) ||
                episode.Position != 0 || state.Lines.Length != mode % 3 ||
                HexLines(state.Lines) != string.Join(',', new[] { "7374616C652D61", "7374616C652D62" }.Take(mode % 3)))
                throw new InvalidOperationException($"Skipped episode warning changed state in scenario {mode}.");
            return;
        }

        var number = ParseExpected(FrameCounts[mode % FrameCounts.Length]);
        var narrowed = unchecked((ushort)number);
        var expectedLines = Enumerable.Range(0, mode % 3 + 1)
            .Select(index => Encoding.ASCII.GetBytes($"Warning {mode:D3}-{index}"))
            .ToArray();
        if (warning.Color != 229 || warning.ColorChange != 1 || warning.SoundDelay != 0 ||
            settings.FrameCountMax != narrowed % 10 || glow.WarningRed != (narrowed / 10 != 0) ||
            state.DisplayWarning != ((mode & 64) != 0) || warningEnabled != state.DisplayWarning ||
            input.NewKey || episode.Position != episode.Length ||
            state.Lines.Length != expectedLines.Length ||
            !state.Lines.Select(line => line.ToArray()).Select(Convert.ToHexString)
                .SequenceEqual(expectedLines.Select(Convert.ToHexString)))
            throw new InvalidOperationException($"Episode warning state differs in scenario {mode}.");
    }

    private static MemoryStream MakeEpisode(int mode)
    {
        var stream = new MemoryStream();
        for (var index = 0; index < mode % 3 + 1; index++)
            WriteEncryptedString(stream, Encoding.ASCII.GetBytes($"Warning {mode:D3}-{index}"));
        WriteEncryptedString(stream, "#"u8);
        stream.Position = 0;
        return stream;
    }

    private static void WriteEncryptedString(Stream stream, ReadOnlySpan<byte> plaintext)
    {
        stream.WriteByte(checked((byte)plaintext.Length));
        Span<byte> encrypted = stackalloc byte[255];
        ReadOnlySpan<byte> key = [204, 129, 63, 255, 71, 19, 25, 62, 1, 99];
        byte previous = 0;
        for (var index = 0; index < plaintext.Length; index++)
        {
            encrypted[index] = (byte)(plaintext[index] ^ key[index % key.Length] ^ previous);
            previous = encrypted[index];
        }
        stream.Write(encrypted[..plaintext.Length]);
    }

    private static string HexLines(IReadOnlyList<ReadOnlyMemory<byte>> lines) =>
        string.Join(',', lines.Select(line => Convert.ToHexString(line.Span)));

    private static int ParseExpected(string text)
    {
        var index = 0;
        while (index < text.Length && text[index] is '\t' or '\n' or '\v' or '\f' or '\r' or ' ') index++;
        var negative = false;
        if (index < text.Length && text[index] is '+' or '-')
        {
            negative = text[index] == '-';
            index++;
        }
        long value = 0;
        var digits = 0;
        while (index < text.Length && text[index] is >= '0' and <= '9')
        {
            value = value * 10 + text[index] - '0';
            digits++;
            index++;
        }
        return digits == 0 ? 0 : checked((int)(negative ? -value : value));
    }

    private sealed class TracePresenter(StringBuilder trace) : IWarningTextPresenter
    {
        public void Run(IReadOnlyList<ReadOnlyMemory<byte>> lines, CancellationToken cancellation = default)
        {
            cancellation.ThrowIfCancellationRequested();
            trace.AppendLine("display");
        }
    }

    private sealed class TraceGlowHost : ITextGlowHost
    {
        public void Draw(int x, int y, ReadOnlyMemory<byte> text, byte hue, int brightness, Font font) { }
        public void SetDelay(int frames) { }
        public bool AnyButton() => false;
        public void KeepAlive() { }
        public void Present() { }
        public void Wait() { }
    }

    private sealed class InputPlatform : IInputPlatform
    {
        public uint Ticks => 0;
        public bool PollEvent(out InputEvent input) { input = default; return false; }
        public void Delay(uint milliseconds) { }
        public bool JoystickDown => false;
        public bool IsNetworkGame => false;
        public void SetRelativeMouseMode(bool enabled) { }
        public void SetCursorVisible(bool visible) { }
        public void EnableSystemMouseScaling() { }
        public void ToggleFullscreen() { }
        public void WindowResized() { }
        public void PushJoysticksAsKeyboard() { }
        public void PollJoysticks() { }
        public void CheckNetwork() { }
        public void Quit() => throw new InvalidOperationException("Unexpected episode-warning quit.");
    }
}
