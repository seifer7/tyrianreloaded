using System.Diagnostics;
using System.Text;
using OpenTyrian.Core;

namespace OpenTyrian.Core.Tests;

internal static class GameSettingsTests
{
    public static void Run(string? executable = null)
    {
        var trace = new StringBuilder();
        for (var gamma = 0; gamma < 256; gamma++)
        {
            var colors = Enumerable.Range(0, 256).Select(i => new PaletteColor((byte)i, (byte)(255 - i), unchecked((byte)(i * 17)))).ToArray();
            PaletteSystem.CorrectGamma(colors, (byte)gamma);
            trace.Append($"gamma:{gamma}");
            foreach (var color in colors) trace.Append($",{color.Red},{color.Green},{color.Blue}");
            trace.AppendLine();
        }
        var platform = new NoInput();
        var input = new InputState(platform, new VideoLayout());
        var mapped = 0;
        var palette = new PaletteSystem(color => { mapped++; return (uint)(color.Red << 16 | color.Green << 8 | color.Blue); });
        var controller = new GameGamma(input, palette);
        for (var mode = 0; mode < 1024; mode++)
        {
            var colors = Enumerable.Range(0, 256).Select(i => new PaletteColor(unchecked((byte)(i + mode)),
                unchecked((byte)(i * 3 + mode)), unchecked((byte)(i * 7 + mode)))).ToArray();
            colors.CopyTo(controller.Colors, 0);
            controller.Correction = (byte)(mode % 256);
            input.KeysActive[68] = (byte)((mode & 1) != 0 ? 2 : 0); input.NewKey = (mode & 2) != 0;
            trace.AppendLine($"check:{mode}");
            var before = mapped;
            var result = controller.Check(colors);
            if (mapped - before != (result ? 256 : 0)) throw new InvalidOperationException("Gamma palette publication count differs.");
            if (result)
            {
                if (!palette.Active.Span.SequenceEqual(controller.Colors)) throw new InvalidOperationException("Gamma did not publish corrected colors.");
                trace.AppendLine($"set:0,255,{Hash(palette.Active.Span)}");
                if (controller.Check(colors)) throw new InvalidOperationException("Held F11 was not consumed.");
            }
            if (!colors.SequenceEqual(Enumerable.Range(0, 256).Select(i => new PaletteColor(unchecked((byte)(i + mode)),
                unchecked((byte)(i * 3 + mode)), unchecked((byte)(i * 7 + mode))))))
                throw new InvalidOperationException("Gamma modified the uncorrected picture palette.");
            trace.AppendLine($"keys:{Bit(result)},{controller.Correction},{input.KeysActive[68]},{Bit(input.NewKey)}");
            trace.AppendLine($"colors:{Hash(controller.Colors)}");
        }
        int[] scores = [int.MinValue, int.MaxValue, 0, -1, 50, 50, 20, -40, 3];
        for (var mode = 0; mode < 512; mode++)
        {
            var archive = new LegacySaveArchive();
            for (var i = 0; i < 22; i++)
            {
                var slot = archive.Slots[i];
                slot.HighScore1 = scores[(mode + i * 3 + i / 3) % 9]; slot.HighScore2 = mode + i;
                slot.HighScoreName = Encoding.ASCII.GetBytes($"name{mode}_{i}");
                slot.HighScoreDifficulty = unchecked((byte)(mode + i)); slot.Level = (ushort)i;
            }
            trace.AppendLine($"sort:{mode}");
            if ((mode & 1) != 0) archive.SortHighScores(); else archive.SortHighScoreGroup(mode % 20);
            foreach (var slot in archive.Slots)
                trace.AppendLine($"slot:{slot.HighScore1},{slot.HighScore2},{Encoding.ASCII.GetString(slot.HighScoreName)}," +
                    $"{slot.HighScoreDifficulty},{slot.Level}");
        }
        Throws<ArgumentOutOfRangeException>(() => PaletteSystem.GammaChannel(1, float.NaN));
        Throws<ArgumentOutOfRangeException>(() => PaletteSystem.GammaChannel(1, -1));
        Throws<ArgumentException>(() => PaletteSystem.CorrectGamma(new PaletteColor[255], 1));
        input.KeysActive[68] = 1;
        Throws<ArgumentException>(() => controller.Check([]));
        if (input.KeysActive[68] != 1) throw new InvalidOperationException("Invalid gamma input consumed the key.");
        Throws<ArgumentOutOfRangeException>(() => new LegacySaveArchive().SortHighScoreGroup(-1));
        Throws<ArgumentOutOfRangeException>(() => new LegacySaveArchive().SortHighScoreGroup(20));
        if (PaletteSystem.GammaChannel(255, float.MaxValue) != 255) throw new InvalidOperationException("Gamma overflow failed to clamp.");
        if (executable is null) return;
        using var process = Process.Start(new ProcessStartInfo(executable) { RedirectStandardOutput = true, RedirectStandardError = true })
            ?? throw new InvalidOperationException("Cannot start game settings reference.");
        var output = process.StandardOutput.ReadToEndAsync(); var errors = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(30000)) { process.Kill(entireProcessTree: true); throw new TimeoutException("Settings reference timed out."); }
        var expected = output.GetAwaiter().GetResult().Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var actual = trace.ToString().Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        if (process.ExitCode != 0 || !expected.SequenceEqual(actual))
        {
            var index = Enumerable.Range(0, Math.Min(expected.Length, actual.Length)).FirstOrDefault(i => expected[i] != actual[i], -1);
            throw new InvalidOperationException($"Settings differ at {index}: {expected.ElementAtOrDefault(index)} / " +
                $"{actual.ElementAtOrDefault(index)}; {errors.GetAwaiter().GetResult()}");
        }
    }

    private static uint Hash(ReadOnlySpan<PaletteColor> colors)
    {
        var hash = 2166136261u;
        foreach (var color in colors)
        {
            hash = unchecked((hash ^ color.Red) * 16777619);
            hash = unchecked((hash ^ color.Green) * 16777619);
            hash = unchecked((hash ^ color.Blue) * 16777619);
        }
        return hash;
    }
    private static int Bit(bool value) => value ? 1 : 0;
    private static void Throws<T>(Action action) where T : Exception
    {
        try { action(); } catch (T) { return; }
        throw new InvalidOperationException($"Expected {typeof(T).Name}.");
    }
    private sealed class NoInput : IInputPlatform
    {
        public bool PollEvent(out InputEvent input) { input = default; return false; }
        public uint Ticks => 0;
        public bool JoystickDown => false;
        public bool IsNetworkGame => false;
        public void Delay(uint milliseconds) => throw new InvalidOperationException("Settings must not wait.");
        public void SetCursorVisible(bool visible) => throw new InvalidOperationException("Settings must not change cursor.");
        public void SetRelativeMouseMode(bool enabled) => throw new InvalidOperationException("Settings must not change mouse.");
        public void EnableSystemMouseScaling() => throw new InvalidOperationException("Settings must not change mouse scaling.");
        public void ToggleFullscreen() => throw new InvalidOperationException("Settings must not toggle fullscreen.");
        public void WindowResized() => throw new InvalidOperationException("Settings must not resize.");
        public void Quit() => throw new InvalidOperationException("Settings must not quit.");
        public void PushJoysticksAsKeyboard() => throw new InvalidOperationException("Settings must not poll joysticks.");
        public void PollJoysticks() => throw new InvalidOperationException("Settings must not poll joysticks.");
        public void CheckNetwork() => throw new InvalidOperationException("Settings must not poll network.");
    }
}
