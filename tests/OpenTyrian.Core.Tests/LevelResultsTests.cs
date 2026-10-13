using System.Diagnostics;
using System.Text;
using OpenTyrian.Core;

namespace OpenTyrian.Core.Tests;

internal static class LevelResultsTests
{
    public static void Run(string? executable = null)
    {
        var fixture = ItemDatabaseTests.Fixture(0);
        foreach (var (category, ids) in new (string, string)[]
        {
            ("ship", "0 late 12 11 8 13 2 1"), ("weapon", "0 late 39 23 25 36 1 16 24 26 37 15"),
            ("shield", "0 late 2 4"), ("generator", "0 late 2"), ("sidekick", "0 late 24")
        })
        {
            var i = 0;
            foreach (var id in ids.Split(' ')) fixture += $"[{category}:{id}]\ncost={i++ * 37}\n";
        }
        var definitions = new ItemDefinitions(); definitions.ParseBundle(Encoding.Latin1.GetBytes(fixture), "results.tyrr");
        var items = ItemDatabase.FromDefinitions(definitions);
        var trace = new StringBuilder();
        for (var mode = 0; mode < 512; mode++)
        {
            var game = new LegacyGameState
            {
                TwoPlayerMode = (mode & 2) != 0,
                OnePlayerAction = (mode & 4) != 0,
                InitialDifficulty = (sbyte)(mode % 9 + 1),
                Difficulty = (sbyte)(mode % 10),
                Episode = (byte)(mode % 4 + 1),
                LevelName = "Level"u8.ToArray()
            };
            for (var i = 0; i < 2; i++)
            {
                var p = game.Players[i];
                p.Items.Weapons[0].Power = (byte)(mode % 11 + 1); p.Items.Weapons[1].Power = (byte)((mode + 1) % 11 + 1);
                p.Cash = (ulong)(mode * 179 + i * 127); p.IsAlive = (mode & (i == 0 ? 8 : 16)) == 0;
                p.Items.Weapons[0].Id = 1; p.Items.Weapons[1].Id = 2;
                p.Items.Sidekicks[0] = 1; p.Items.Sidekicks[1] = 2;
            }
            game.Players[0].Items.Special = (byte)(mode % 24);
            var effects = new SceneEffects(new byte[8]); var surface = new IndexedSurface(320, 200, 327, TextWriter.Null);
            var combat = new PlayerCombat(game.Players, effects, surface) { LevelTimer = (mode & 64) != 0 };
            var enemies = new EnemySystem(items, TextWriter.Null)
            { TotalEnemies = (ushort)(mode % 13), KilledEnemies = (ushort)(mode % 23), CubeCount = (ushort)(mode % 7) };
            var level = new LevelEvents(items, enemies, game.Players, new(), effects) { TimerCountdown = (ushort)(mode % 3) };
            var shots = new PlayerShots(items.Shots, items.Weapons, game.Players, new UnusedShots());
            var pickups = new PlayerPickups(game.Players, enemies, combat, shots, items, level, effects, [], []) { BonusLevel = (mode & 32) != 0 };
            var archive = new LegacySaveArchive { EditorLevel = 65530 };
            var glow = new TextGlow(new TraceGlow());
            var host = new TraceHost(trace, glow, mode);
            var results = new LevelResults(game, archive, new PlayerProgression(game, items), enemies, combat, level, pickups, glow,
                Enumerable.Range(0, 80).Select(i => Encoding.ASCII.GetBytes($"text{i}")).ToArray(), host)
            { ConstantPlay = (mode & 1) != 0 };
            trace.AppendLine($"results:{mode}"); results.Run();
            trace.AppendLine($"state:{game.Difficulty},{archive.EditorLevel},{enemies.CubeCount},{glow.FrameCount},{(int)glow.Font}," +
                $"{Encoding.Latin1.GetString(game.LastLevelName)},{host.ButtonCalls},{host.GlowCalls}");
            var p0 = game.Players[0].LastItems;
            trace.AppendLine($"items:{p0.Weapons[0].Id},{p0.Weapons[0].Power},{p0.Weapons[1].Id},{p0.Weapons[1].Power}," +
                $"{p0.Sidekicks[0]},{p0.Sidekicks[1]},{p0.Special},{p0.Ship},{p0.SidekickLevel},{p0.SidekickSeries},{p0.SuperArcadeMode},{p0.Generator},0,0,0");
            trace.AppendLine("unlock:" + string.Concat(archive.ItemAvailability.Concat(results.ChecksumScratch).Select(value => $"{value},")));
        }
        TestHost(items);
        if (executable is null) return;
        var path = Path.Combine(Path.GetTempPath(), $"opentyrian-results-{Guid.NewGuid():N}.tyrr");
        try
        {
            File.WriteAllBytes(path, Encoding.Latin1.GetBytes(fixture));
            var start = new ProcessStartInfo(executable) { RedirectStandardOutput = true, RedirectStandardError = true };
            start.ArgumentList.Add(path);
            using var process = Process.Start(start) ?? throw new InvalidOperationException("Cannot start level results reference.");
            var output = process.StandardOutput.ReadToEndAsync(); var errors = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(60000)) { process.Kill(entireProcessTree: true); throw new TimeoutException("Level results reference timed out."); }
            var expected = output.GetAwaiter().GetResult().Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
            var actual = trace.ToString().Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
            if (process.ExitCode != 0 || !expected.SequenceEqual(actual))
            {
                var index = Enumerable.Range(0, Math.Min(expected.Length, actual.Length)).FirstOrDefault(i => expected[i] != actual[i], -1);
                throw new InvalidOperationException($"Level results differ at {index}: {expected.ElementAtOrDefault(index)} / " +
                    $"{actual.ElementAtOrDefault(index)}; {errors.GetAwaiter().GetResult()}");
            }
        }
        finally { File.Delete(path); }
    }

    private static void TestHost(ItemDatabase items)
    {
        var game = new LegacyGameState { OnePlayerAction = true, InitialDifficulty = 2, Episode = 1, LevelName = "Level"u8.ToArray() };
        foreach (var p in game.Players) { p.Items.Weapons[0].Power = p.Items.Weapons[1].Power = 1; p.IsAlive = true; }
        var shapes = new MainShapeTables();
        using var data = new MemoryStream(); BinaryFile.WriteUInt16(data, 151);
        for (var i = 0; i < 151; i++)
        {
            BinaryFile.WriteBoolean(data, true); BinaryFile.WriteUInt16(data, 1); BinaryFile.WriteUInt16(data, 1);
            BinaryFile.WriteUInt16(data, 1); data.WriteByte(0xAF);
        }
        foreach (var table in shapes.Tables) { data.Position = 0; table.Load(data); }
        var primary = new IndexedSurface(320, 200, 327, TextWriter.Null);
        var cleared = new IndexedSurface(320, 200, 327, TextWriter.Null); cleared.Pixels.Span.Fill(0x75);
        var platform = new TextGlowTests.Platform(); var input = new InputState(platform, new VideoLayout());
        var timer = new DelayTimer(input); var font = new FontRenderer(shapes);
        var palette = new PaletteSystem(color => (uint)(color.Red << 16 | color.Green << 8 | color.Blue));
        var keeps = 0; var presents = 0; var samples = new List<byte>();
        var glow = new TextGlow(new TextGlowHost(primary, font, input, timer, () => keeps++, () => presents++));
        var host = new LevelResultsHost(primary, cleared, palette, glow,
            new GameHudPresentation(primary, font, new(), new(), shapes.Tables[5]), shapes.Tables[5], input, timer,
            sample => samples.Add(sample), _ => throw new InvalidOperationException("Unexpected result song."), () => presents++, () => keeps++);
        var effects = new SceneEffects(new byte[8]); var combat = new PlayerCombat(game.Players, effects, primary);
        var enemies = new EnemySystem(items, TextWriter.Null); var level = new LevelEvents(items, enemies, game.Players, new(), effects);
        var shots = new PlayerShots(items.Shots, items.Weapons, game.Players, new UnusedShots());
        var pickups = new PlayerPickups(game.Players, enemies, combat, shots, items, level, effects, [], []);
        var archive = new LegacySaveArchive();
        var results = new LevelResults(game, archive, new PlayerProgression(game, items), enemies, combat, level, pickups, glow,
            Enumerable.Range(0, 80).Select(i => Encoding.ASCII.GetBytes($"text{i}")).ToArray(), host)
        { ConstantPlay = true };
        platform.Events.Enqueue(new(InputEventKind.KeyDown, 40));
        results.Run();
        if (cleared.Pixels.Span.ContainsAnyExcept((byte)0) || primary.Pixels.Span.ContainsAnyExcept((byte)0) == false ||
            palette.Active.Span.ToArray().Any(color => color.Red != 0 || color.Green != 0 || color.Blue != 0) ||
            samples.Count != 1 || samples[0] != 34 || archive.ItemAvailability.Any(value => value != 0) ||
            keeps == 0 || presents <= keeps || glow.FrameCount != 0 || game.LastLevelName.AsSpan().SequenceEqual("Level"u8) == false)
            throw new InvalidOperationException("Concrete result glow/input/palette/timer/audio integration differs.");
        results.ConstantPlay = false; game.Players[0].Items.Weapons[0].Id = 255;
        try { results.Run(); throw new InvalidOperationException("Invalid unlock accepted."); }
        catch (InvalidDataException) { }
    }
    private sealed class TraceHost(StringBuilder trace, TextGlow glow, int mode) : ILevelResultsHost
    {
        public int ButtonCalls { get; private set; }
        public int GlowCalls { get; private set; }
        public void WipeKey() => trace.AppendLine("wipe");
        public void SetWhite() => trace.AppendLine("white:254,254");
        public void PlaySample(byte sample) => trace.AppendLine($"sample:{sample}");
        public void PlaySong(uint song) => trace.AppendLine($"song:{song}");
        public void Glow(int x, int y, ReadOnlyMemory<byte> text, CancellationToken cancellation)
        {
            GlowCalls++;
            trace.AppendLine($"glow:{x},{y},{Encoding.Latin1.GetString(text.Span)},{glow.FrameCount},{(int)glow.Font}");
            if ((mode & 128) != 0 && GlowCalls == 2 || (mode & 256) != 0 && y >= 150) glow.FrameCount = 0;
        }
        public void Cube(ushort x, ushort y) => trace.AppendLine($"cube:{x},{y}");
        public void CubeHue(ushort x, ushort y, sbyte brightness) => trace.AppendLine($"cubehue:{x},{y},{brightness}");
        public void Present() => trace.AppendLine("present");
        public void SetDelay(int frames) => trace.AppendLine($"delay:{frames}");
        public bool AnyButton() { ButtonCalls++; var result = ButtonCalls % 7 == 0; trace.AppendLine($"button:{(result ? 1 : 0)}"); return result; }
        public void KeepAlive() => trace.AppendLine("keep");
        public void Wait() => trace.AppendLine("wait");
        public void WaitJoystickRelease(CancellationToken cancellation) => trace.AppendLine("release:0,0,1");
        public void FadeBlack(int steps) => trace.AppendLine($"fade:{steps}");
        public void Clear() => trace.AppendLine("clear");
    }
    private sealed class TraceGlow : ITextGlowHost
    {
        public void Draw(int x, int y, ReadOnlyMemory<byte> text, byte hue, int brightness, Font font) => throw new InvalidOperationException("Unexpected trace glow.");
        public void SetDelay(int frames) => throw new InvalidOperationException("Unexpected trace delay.");
        public bool AnyButton() => throw new InvalidOperationException("Unexpected trace input.");
        public void KeepAlive() => throw new InvalidOperationException("Unexpected trace network.");
        public void Present() => throw new InvalidOperationException("Unexpected trace presentation.");
        public void Wait() => throw new InvalidOperationException("Unexpected trace wait.");
    }
    private sealed class UnusedShots : IPlayerShotEffects
    {
        public void Draw(int sheet, int x, int y, int frame, bool darken) => throw new InvalidOperationException("Unexpected result shot.");
        public (ushort Width, ushort Height) DrawSpecial(int x, int y, int frame) => throw new InvalidOperationException("Unexpected result shot.");
        public void Explosion(int x, int y, byte type) => throw new InvalidOperationException("Unexpected result explosion.");
        public void SuperPixels(int x, int y, ushort count, byte width, byte color) => throw new InvalidOperationException("Unexpected result particle.");
    }
}
