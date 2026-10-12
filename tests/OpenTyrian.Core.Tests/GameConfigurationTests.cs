using System.Diagnostics;
using System.Text;
using OpenTyrian.Core;

namespace OpenTyrian.Core.Tests;

internal static class GameConfigurationTests
{
    private static readonly string[] SettingNames =
        ["up", "down", "left", "right", "fire", "change fire", "left sidekick", "right sidekick"];
    private static readonly string[] Names =
        ["Up", "Down", "Left", "Right", "Space", "Return", "Left Ctrl", "Left Alt", "A", "Keypad Enter", "invalid", "", "a"];
    private static readonly string[] ScalingModes = ["Center", "Integer", "Fit 8:5", "Fit 4:3"];

    public static void Run(string? executable = null)
    {
        IKeyboardNames names = executable == null ? new FakeKeyboard() : new SdlKeyboardNames();
        var trace = new StringBuilder();
        for (var mode = 0; mode < 32; mode++)
        {
            var config = new GameConfiguration(names);
            config.Layout.Mode = ScalingMode.Fit4By3;
            using var input = new StringReader(Fixture(mode));
            config.Load(input);
            trace.Append($"load:{mode},1,{config.FullscreenDisplay},{VideoScaler.Scalers[config.Scaler.SelectedIndex].Name},{ScalingModes[(int)config.Layout.Mode]},{config.ShopContent}");
            foreach (var scancode in config.Keys) trace.Append($",{scancode}");
            trace.AppendLine();
            if (mode % 2 != 0) config.Keys[4] = 0;
            trace.AppendLine($"write:{mode}");
            using var output = new StringWriter();
            config.Save(output);
            trace.Append(output);
            if (mode == 0)
                Check(config.FullscreenDisplay == -1 && config.Scaler.SelectedIndex == 2 && config.ShopContent == 0 &&
                    config.Layout.Mode == ScalingMode.Fit4By3, "Config defaults/unknown-value preservation changed.");
        }
        if (executable != null)
        {
            using var process = Process.Start(new ProcessStartInfo(Path.GetFullPath(executable))
            { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true })
                ?? throw new InvalidOperationException("Cannot start native game-config oracle.");
            var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(30000))
            {
                process.Kill(entireProcessTree: true);
                throw new TimeoutException("Native game-config oracle timed out.");
            }
            var expected = stdout.GetAwaiter().GetResult().Replace("\r\n", "\n", StringComparison.Ordinal);
            var actual = trace.ToString().Replace("\r\n", "\n", StringComparison.Ordinal);
            Check(process.ExitCode == 0 && expected == actual, $"Native game-config state/text differs: {stderr.GetAwaiter().GetResult()}");
        }
        Files(names);
    }

    private static string Fixture(int mode)
    {
        var text = new StringBuilder("section \"video\"\n");
        if (mode % 4 != 0) text.AppendLine($"\titem \"fullscreen\" \"{mode - 3}\"");
        text.AppendLine($"\titem \"scaler\" \"{(mode % 5 != 0 ? VideoScaler.Scalers[mode % 9].Name : "invalid")}\"");
        text.AppendLine($"\titem \"scaling_mode\" \"{(mode % 3 != 0 ? ScalingModes[mode % 4] : "invalid")}\"");
        text.AppendLine("section \"keyboard\"");
        for (var i = 0; i < 8; i++) text.AppendLine($"\titem \"{SettingNames[i]}\" \"{Names[(mode + i) % 13]}\"");
        text.AppendLine($"section \"gameplay\"\n\titem \"shop_content\" \"{(mode % 2 != 0 ? "0x15" : "invalid")}\"");
        text.AppendLine("section \"joystick\" \"Synthetic pad\"\n\titem \"threshold\" \"3\"");
        return text.ToString();
    }

    private static void Files(IKeyboardNames names)
    {
        var directory = Path.Combine(Path.GetTempPath(), $"opentyrian-config-{Guid.NewGuid():N}");
        var path = Path.Combine(directory, "opentyrian.cfg");
        try
        {
            var config = new GameConfiguration(names);
            config.Layout.Mode = ScalingMode.Center;
            config.Keys[0] = 10;
            using var diagnostics = new StringWriter();
            Check(!config.LoadFile(directory, diagnostics) && config.Keys[0] == 82 &&
                config.Layout.Mode == ScalingMode.Center && diagnostics.ToString().Length > 0,
                "Missing config did not reset defaults and report its absence.");
            config.Load(new StringReader(Fixture(7)));
            config.SaveFile(directory);
            var restored = new GameConfiguration(names);
            Check(restored.LoadFile(directory, diagnostics) &&
                config.Keys.SequenceEqual(restored.Keys) && config.ShopContent == restored.ShopContent &&
                config.FullscreenDisplay == restored.FullscreenDisplay && config.Layout.Mode == restored.Layout.Mode &&
                config.Scaler.SelectedIndex == restored.Scaler.SelectedIndex &&
                restored.Text.FindSection("joystick", "Synthetic pad")?.GetOption("threshold")?.Value == "3",
                "Game config did not persist settings and unrelated joystick sections.");
            using (var locked = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                Throws<IOException>(() => restored.LoadFile(directory, diagnostics));
                Throws<IOException>(() => restored.SaveFile(directory));
            }
            Throws<IOException>(() => restored.SaveFile(path));
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
            if (Directory.Exists(directory)) Directory.Delete(directory);
        }
    }

    private static void Throws<T>(Action action) where T : Exception
    {
        try { action(); } catch (T) { return; }
        throw new InvalidOperationException($"Expected {typeof(T).Name}.");
    }

    private sealed class FakeKeyboard : IKeyboardNames
    {
        private static readonly int[] Codes = [82, 81, 80, 79, 44, 40, 224, 226, 4, 88, 0, 0, 4];
        public int FromName(string name) => Array.IndexOf(Names, name) is var index && index >= 0 ? Codes[index] : 0;
        public string Name(int scancode)
        {
            if (scancode == 0) return "";
            var index = Array.IndexOf(Codes, scancode);
            return index >= 0 ? Names[index] : "";
        }
    }
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
