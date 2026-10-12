using System.Diagnostics;
using System.Text;
using OpenTyrian.Core;

namespace OpenTyrian.Core.Tests;

internal static class DemoSessionTests
{
    public static void Run(string? executable = null)
    {
        var fixture = LevelEventTests.Fixture() +
            "[ship:200]\n[weapon:201]\n[weapon:202]\n[generator:203]\n[shield:204]\n" +
            "[sidekick:205]\n[sidekick:206]\n[special:207]\n";
        var definitions = new ItemDefinitions(); definitions.ParseBundle(Encoding.Latin1.GetBytes(fixture), "demo.tyrr");
        var items = ItemDatabase.FromDefinitions(definitions);
        var root = Path.Combine(Path.GetTempPath(), $"opentyrian-demo-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var fixturePath = Path.Combine(root, "items.tyrr");
            File.WriteAllBytes(fixturePath, Encoding.Latin1.GetBytes(fixture));
            foreach (var scenario in new[] { "load", "wrap", "missing", "record" })
            {
                var managed = Path.Combine(root, "managed-" + scenario);
                var native = Path.Combine(root, "native-" + scenario);
                Prepare(managed, scenario); Prepare(native, scenario);
                var trace = Execute(managed, scenario, items);
                if (executable is null) continue;
                var start = new ProcessStartInfo(executable) { RedirectStandardOutput = true, RedirectStandardError = true };
                start.ArgumentList.Add(fixturePath); start.ArgumentList.Add(native); start.ArgumentList.Add(scenario);
                using var process = Process.Start(start) ?? throw new InvalidOperationException("Cannot start demo oracle.");
                var output = process.StandardOutput.ReadToEndAsync(); var errors = process.StandardError.ReadToEndAsync();
                if (!process.WaitForExit(30000)) { process.Kill(entireProcessTree: true); throw new TimeoutException("Demo oracle timed out."); }
                var expected = output.GetAwaiter().GetResult().Replace("\r\n", "\n", StringComparison.Ordinal);
                if (process.ExitCode != 0 || trace != expected)
                {
                    var a = expected.Split('\n'); var b = trace.Split('\n');
                    var index = Enumerable.Range(0, Math.Min(a.Length, b.Length)).FirstOrDefault(i => a[i] != b[i], -1);
                    throw new InvalidOperationException($"Demo {scenario} differs at {index}: {a.ElementAtOrDefault(index)} / " +
                        $"{b.ElementAtOrDefault(index)}; {errors.GetAwaiter().GetResult()}");
                }
            }
            TestFailures(Path.Combine(root, "failures"), items);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    private static void Prepare(string directory, string scenario)
    {
        Directory.CreateDirectory(directory);
        foreach (var number in new[] { 1, 4, 5 }) File.WriteAllBytes(Path.Combine(directory, $"tyrian{number}.lvl"), [0, 0]);
        if (scenario is "load" or "wrap")
        {
            File.WriteAllBytes(Path.Combine(directory, "demo.1"), Header(1, [0x12, 0x34]));
            File.WriteAllBytes(Path.Combine(directory, "demo.2"), [5, 1, 2, 3, 17]);
            File.WriteAllBytes(Path.Combine(directory, "demo.3"), Header(4, [0xAB]));
            if (scenario == "wrap") File.WriteAllBytes(Path.Combine(directory, "demo.0"), [5, 0, 0, 0, 3, 0, 1]);
        }
        if (scenario == "record")
        {
            File.WriteAllText(Path.Combine(directory, "demorec.0"), "occupied");
            File.WriteAllBytes(Path.Combine(directory, "demorec.1"), []);
        }
    }

    private static byte[] Header(byte episode, byte[] wait)
        => [episode, 65, 66, 0, 67, 68, 69, 70, 71, 72, 73, 19,
            201, 202, 7, 205, 206, 203, 102, 3, 4, 204, 207, 200, 5, 9,
            31, 32, 33, 17, .. wait];

    private static string Execute(string directory, string scenario, ItemDatabase items)
    {
        using var trace = new StringWriter(); using var errors = new StringWriter();
        var files = new DataFiles(directory, directory);
        var episodes = new EpisodeState(files);
        var players = new[] { new PlayerState(0), new PlayerState(1) };
        var effects = new SceneEffects(new byte[8]);
        var combat = new PlayerCombat(players, effects, new(320, 200, 320, TextWriter.Null));
        var enemies = new EnemySystem(items, TextWriter.Null);
        var level = new LevelEvents(items, enemies, players, new(), effects);
        var shots = new PlayerShots(items.Shots, items.Weapons, players, new ShotEffects());
        var movement = new PlayerMovement(players, combat, level, shots, items);
        var input = new PlayerInput(new(new Platform(), new()), new([], () => 0, () => { }, _ => { }), [],
            [], movement, combat, level, new());
        using var demo = new DemoSession(files, items, players[0], input, enemies,
            number => { episodes.Initialize(number); trace.WriteLine($"init:{number}"); }, trace, errors);
        if (scenario == "wrap") demo.Number = 255;
        if (scenario == "record")
        {
            for (var mode = 0; mode < 64; mode++)
            {
                var name = Enumerable.Range(0, 10).Select(i => (byte)(mode * 13 + i * 7 + 65)).ToArray();
                if ((mode & 1) != 0 && mode % 11 < 10) name[mode % 11] = 0;
                demo.LevelName = Encoding.Latin1.GetString(name);
                demo.Episode = (byte)(mode % 5 + 1); demo.InitialEpisode = (byte)(mode % 7 + 1);
                demo.LevelFileNumber = (byte)(mode * 3); demo.Song = (byte)(mode * 7);
                var p = players[0].Items;
                p.Weapons[0].Id = Value(0); p.Weapons[1].Id = Value(2);
                p.SuperArcadeMode = Value(4); p.Sidekicks[0] = Value(5); p.Sidekicks[1] = Value(6);
                p.Generator = Value(7); p.SidekickLevel = Value(8); p.SidekickSeries = Value(9);
                p.Shield = Value(10); p.Special = Value(11); p.Ship = Value(12);
                p.Weapons[0].Power = (byte)(mode % 11 + 1); p.Weapons[1].Power = (byte)(mode % 9 + 1);
                input.DemoKeys = 255; input.DemoWait = 65535;
                var filename = demo.RecordLevel(directory);
                demo.Dispose();
                var bytes = File.ReadAllBytes(Path.Combine(directory, filename));
                if (bytes.Length != 30) throw new InvalidOperationException("Demo header must be exactly 30 bytes.");
                trace.WriteLine($"record:{filename},{input.DemoKeys},{input.DemoWait}:{Convert.ToHexString(bytes)}");
                byte Value(int offset) => unchecked((byte)(mode * 7 + offset * 3));
            }
            if (File.ReadAllText(Path.Combine(directory, "demorec.0")) != "occupied" ||
                new FileInfo(Path.Combine(directory, "demorec.1")).Length != 0)
                throw new InvalidOperationException("Demo recording overwrote an occupied slot.");
        }
        else
        {
            for (var mode = 0; mode < 5; mode++)
            {
                input.PlayDemo = true; demo.MainLevel = 17; enemies.Difficulty = 9; demo.BonusLevel = true;
                var result = demo.LoadNext();
                trace.WriteLine($"load:{Bit(result)},{demo.Number},{Bit(input.PlayDemo)},{demo.MainLevel},{Bit(demo.BonusLevel)}," +
                    $"{enemies.Difficulty},{episodes.Number},{demo.InitialEpisode},{demo.LevelFileNumber},{demo.Song}," +
                    $"{input.DemoWait},{demo.Stream?.Position ?? -1}:{Convert.ToHexString(Encoding.Latin1.GetBytes(demo.LevelName))}");
                var p = players[0].Items;
                trace.WriteLine("items:" + string.Join(',', p.ToLegacyItems(demo.InitialEpisode)) +
                    $",{p.Weapons[0].Power},{p.Weapons[1].Power}");
            }
            if (scenario == "missing" && !errors.ToString().Contains("warning: failed to open", StringComparison.Ordinal))
                throw new InvalidOperationException("Missing demo must report its open failure.");
        }
        return trace.ToString().Replace("\r\n", "\n", StringComparison.Ordinal);
    }

    private static void TestFailures(string directory, ItemDatabase items)
    {
        Prepare(directory, "missing");
        var files = new DataFiles(directory, directory);
        var players = new[] { new PlayerState(0), new PlayerState(1) };
        var effects = new SceneEffects(new byte[8]);
        var combat = new PlayerCombat(players, effects, new(320, 200, 320, TextWriter.Null));
        var enemies = new EnemySystem(items, TextWriter.Null);
        var level = new LevelEvents(items, enemies, players, new(), effects);
        var shots = new PlayerShots(items.Shots, items.Weapons, players, new ShotEffects());
        var input = new PlayerInput(new(new Platform(), new()), new([], () => 0, () => { }, _ => { }), [], [],
            new(players, combat, level, shots, items), combat, level, new());
        using var demo = new DemoSession(files, items, players[0], input, enemies, _ => { }, TextWriter.Null, TextWriter.Null);
        var header = Header(1, []);
        for (var length = 0; length < 30; length++)
        {
            demo.Number = 0; File.WriteAllBytes(Path.Combine(directory, "demo.1"), header[..length]);
            Throws<EndOfStreamException>(() => demo.LoadNext()); demo.Dispose();
        }
        header[12] = 255; File.WriteAllBytes(Path.Combine(directory, "demo.1"), header);
        demo.Number = 0; Throws<InvalidDataException>(() => demo.LoadNext()); demo.Dispose();
        for (var i = 0; i <= 255; i++) File.WriteAllBytes(Path.Combine(directory, $"demorec.{i}"), []);
        Throws<IOException>(() => demo.RecordLevel(directory));
    }

    private static void Throws<T>(Action action) where T : Exception
    {
        try { action(); } catch (T) { return; }
        throw new InvalidOperationException($"Expected {typeof(T).Name}.");
    }
    private static int Bit(bool value) => value ? 1 : 0;
    private sealed class Platform : IInputPlatform
    {
        public bool PollEvent(out InputEvent input) => throw new InvalidOperationException("Unexpected demo-header input.");
        public uint Ticks => 0;
        public bool JoystickDown => false;
        public bool IsNetworkGame => false;
        public void Delay(uint milliseconds) => throw new InvalidOperationException("Unexpected demo-header wait.");
        public void SetCursorVisible(bool visible) { }
        public void SetRelativeMouseMode(bool enabled) { }
        public void EnableSystemMouseScaling() { }
        public void ToggleFullscreen() => throw new InvalidOperationException();
        public void WindowResized() => throw new InvalidOperationException();
        public void Quit() => throw new InvalidOperationException();
        public void PushJoysticksAsKeyboard() => throw new InvalidOperationException();
        public void PollJoysticks() => throw new InvalidOperationException();
        public void CheckNetwork() => throw new InvalidOperationException();
    }
    private sealed class ShotEffects : IPlayerShotEffects
    {
        public void Draw(int sheet, int x, int y, int frame, bool darken) => throw new InvalidOperationException();
        public (ushort Width, ushort Height) DrawSpecial(int x, int y, int frame) => throw new InvalidOperationException();
        public void Explosion(int x, int y, byte type) => throw new InvalidOperationException();
        public void SuperPixels(int x, int y, ushort count, byte width, byte color) => throw new InvalidOperationException();
    }
}
