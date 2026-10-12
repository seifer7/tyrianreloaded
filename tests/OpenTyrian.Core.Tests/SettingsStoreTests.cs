using System.Diagnostics;
using System.Text;
using OpenTyrian.Core;

namespace OpenTyrian.Core.Tests;

internal static class SettingsStoreTests
{
    public static void Run(string? executable = null)
    {
        var trace = new StringBuilder();
        for (var mode = 0; mode < 8; mode++)
        {
            trace.AppendLine($"case:{mode}");
            var directory = Path.Combine(Path.GetTempPath(), $"opentyrian-settings-{Guid.NewGuid():N}");
            Directory.CreateDirectory(directory);
            var cfg = Path.Combine(directory, "tyrian.cfg");
            var sav = Path.Combine(directory, "tyrian.sav");
            var text = Path.Combine(directory, "opentyrian.cfg");
            try
            {
                if (mode % 3 != 0)
                {
                    var bytes = Enumerable.Range(0, mode % 3 == 1 ? 28 : 27).Select(i => unchecked((byte)(mode * 13 + i * 31))).ToArray();
                    bytes[0] = (byte)(mode % 2);
                    bytes[1] = bytes[5] = (byte)mode;
                    if (mode == 4) { bytes[14] = 23; bytes[15] = 0; bytes[16] = 255; bytes[17] = 0; }
                    File.WriteAllBytes(cfg, bytes);
                }
                File.WriteAllText(text, "", new UTF8Encoding(false));
                if ((mode & 4) != 0)
                {
                    var initial = new LegacySaveArchive { EditorLevel = (ushort)(500 + mode) };
                    for (var z = 0; z < 22; z++) { initial.Slots[z].Level = (ushort)(mode + z); initial.Slots[z].HighScore1 = 100 + z; }
                    for (var i = 0; i < 100; i++) initial.ItemAvailability[i] = unchecked((byte)(i * mode));
                    using var output = File.Create(sav);
                    initial.Write(output);
                }
                using var diagnostics = new StringWriter();
                var store = new GameSettingsStore(new(new Keyboard()), diagnostics);
                MersenneTwister.Seed(789);
                store.Load(directory, (music, effects) => trace.AppendLine($"volume:{music},{effects}"));
                var runtime = store.Runtime;
                trace.AppendLine($"settings:{Bit(runtime.Wild)},{Bit(runtime.SuperWild)},{Bit(runtime.SmoothScroll)},{Bit(runtime.ExplosionTransparent)},{Bit(runtime.FiltrationAvailable)},{Bit(runtime.DisplayScore)},{runtime.FastPlay},{Bit(store.Dos.Background2)},{store.Saves.EditorLevel}");
                Check(diagnostics.ToString().Contains("Invalid or missing TYRIAN.CFG!", StringComparison.Ordinal) == (mode % 3 != 1),
                    "Legacy config validity/default diagnostic changed.");
                store.Save(directory);
                var dos = File.ReadAllBytes(cfg);
                Check(dos.Length == 28, "Legacy configuration byte count changed.");
                trace.AppendLine($"dos:{Convert.ToHexString(dos)}");
                trace.AppendLine($"saved:{Convert.ToHexString(File.ReadAllBytes(sav))}");
                var restored = new GameSettingsStore(new(new Keyboard()), diagnostics);
                restored.Load(directory, (music, effects) =>
                    Check(music == store.Dos.MusicVolume && effects == store.Dos.EffectsVolume, "Stored volumes did not reload."));
                Check(restored.Saves.Slots[0].HighScore1 == store.Saves.Slots[0].HighScore1 &&
                    restored.Runtime.ProcessorType == store.Runtime.ProcessorType, "Configuration composition did not persist state.");
                using var locked = new FileStream(sav, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                Throws<IOException>(() => store.Save(directory));
                Throws<IOException>(() => store.Load(directory, (_, _) => { }));
            }
            finally
            {
                foreach (var path in new[] { cfg, sav, text }) if (File.Exists(path)) File.Delete(path);
                Directory.Delete(directory);
            }
        }
        if (executable != null)
        {
            var result = RunNative(executable);
            var filtered = string.Join('\n', result.Output.Split('\n').Where(line =>
                line.StartsWith("case:", StringComparison.Ordinal) || line.StartsWith("volume:", StringComparison.Ordinal) ||
                line.StartsWith("settings:", StringComparison.Ordinal) || line.StartsWith("dos:", StringComparison.Ordinal) ||
                line.StartsWith("saved:", StringComparison.Ordinal))) + "\n";
            var expected = trace.ToString().Replace("\r\n", "\n", StringComparison.Ordinal);
            var mismatch = 0;
            while (mismatch < Math.Min(filtered.Length, expected.Length) && filtered[mismatch] == expected[mismatch]) mismatch++;
            Check(result.Code == 0 && filtered == expected,
                $"Complete native configuration composition differs at {mismatch}: native '{filtered.Substring(mismatch, Math.Min(120, filtered.Length - mismatch))}', managed '{expected.Substring(mismatch, Math.Min(120, expected.Length - mismatch))}'; {result.Error}");
            var directory = RunNative(executable, "directory");
            Check(directory.Code == 0 && directory.Output.TrimEnd('\n') == GameSettingsStore.GetUserDirectory(),
                "Platform user-directory precedence differs from native.");
        }
        var user = GameSettingsStore.GetUserDirectory();
        Check(GameSettingsStore.GetUserDirectory() == user, "User configuration directory was not cached.");
    }

    private static (string Output, string Error, int Code) RunNative(string executable, string? argument = null)
    {
        var start = new ProcessStartInfo(Path.GetFullPath(executable))
        { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        if (argument != null) start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Cannot start configuration composition oracle.");
        var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(30000))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException("Native settings oracle timed out.");
        }
        return (stdout.GetAwaiter().GetResult().Replace("\r\n", "\n", StringComparison.Ordinal),
            stderr.GetAwaiter().GetResult().Replace("\r\n", "\n", StringComparison.Ordinal), process.ExitCode);
    }
    private sealed class Keyboard : IKeyboardNames
    {
        public int FromName(string name) => 0;
        public string Name(int scancode) => "";
    }
    private static int Bit(bool value) => value ? 1 : 0;
    private static void Throws<T>(Action action) where T : Exception
    {
        try { action(); } catch (T) { return; }
        throw new InvalidOperationException($"Expected {typeof(T).Name}.");
    }
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
