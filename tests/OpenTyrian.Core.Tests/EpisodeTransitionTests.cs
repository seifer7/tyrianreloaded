using System.Diagnostics;
using System.Text;
using OpenTyrian.Core;

namespace OpenTyrian.Core.Tests;

internal static class EpisodeTransitionTests
{
    public static void Run(string? executable = null)
    {
        var fixture = ItemDatabaseTests.Fixture(0);
        var definitions = new ItemDefinitions(); definitions.ParseBundle(Encoding.Latin1.GetBytes(fixture), "transition.tyrr");
        var items = ItemDatabase.FromDefinitions(definitions);
        var trace = new StringBuilder();
        var root = Path.Combine(Path.GetTempPath(), $"opentyrian-transition-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            for (var i = 1; i <= 5; i++)
            {
                using var level = File.Create(Path.Combine(root, $"tyrian{i}.lvl"));
                BinaryFile.WriteUInt16(level, 1); BinaryFile.WriteInt32(level, 6);
            }
            var files = new DataFiles(root, root);
            var names = Enumerable.Range(0, 6).Select(i => Encoding.ASCII.GetBytes(i == 0 ? "" : $"Episode{i}")).ToArray();
            for (var mode = 0; mode < 1024; mode++)
            {
                var game = new LegacyGameState
                {
                    Episode = (byte)(mode % 5 + 1),
                    InitialEpisode = (byte)((mode & 1) != 0 ? mode % 5 + 1 : 1),
                    GameHasRepeated = (mode & 2) != 0,
                    MainLevel = 9,
                    SaveLevel = 10
                };
                foreach (var p in game.Players) { p.Items.Weapons[0].Power = 5; p.Items.Weapons[1].Power = 7; }
                var episodes = new EpisodeState(files); episodes.Initialize(game.Episode);
                for (var i = 0; i < 5; i++) episodes.Available[i] = (mode >> (i + 4) & 1) != 0;
                var host = new TraceHost(trace, mode);
                var transition = new EpisodeTransition(game, episodes, items, names, "Continue"u8.ToArray(), host)
                { NetworkGame = (mode & 4) != 0, ConstantPlay = (mode & 8) != 0 };
                var old = game.Episode;
                MersenneTwister.Seed((uint)(mode + 1));
                trace.AppendLine($"transition:{mode}");
                // The native initialization call appears between reward/credits and the song.
                host.BeforeSong = () => { if (episodes.Number != old) trace.AppendLine($"initialize:{episodes.Number}"); };
                transition.Run();
                trace.AppendLine($"state:{game.Episode},{Bit(game.GameHasRepeated)},{Bit(episodes.JumpBackToFirst)}," +
                    $"{Bit(game.GameLoaded)},{game.MainLevel},{game.SaveLevel},{Encoding.ASCII.GetString(game.LastLevelName)},{host.Buttons}");
                foreach (var p in game.Players) { Dump(p.Items); Dump(p.LastItems); }
                trace.AppendLine("palette:71,71"); trace.AppendLine($"rng:{MersenneTwister.NextUInt32()}");
                void Dump(PlayerItems p) => trace.AppendLine($"items:{p.Weapons[0].Id},{p.Weapons[0].Power},{p.Weapons[1].Id},{p.Weapons[1].Power}," +
                    $"{p.Sidekicks[0]},{p.Sidekicks[1]},{p.Special},{p.Ship},{p.SidekickLevel},{p.SidekickSeries},{p.SuperArcadeMode},{p.Generator},0,0,0");
            }
            TestHost();
            if (executable is null) return;
            var path = Path.Combine(root, "fixture.tyrr"); File.WriteAllBytes(path, Encoding.Latin1.GetBytes(fixture));
            var start = new ProcessStartInfo(executable) { RedirectStandardOutput = true, RedirectStandardError = true }; start.ArgumentList.Add(path);
            using var process = Process.Start(start) ?? throw new InvalidOperationException("Cannot start episode transition reference.");
            var output = process.StandardOutput.ReadToEndAsync(); var errors = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(30000)) { process.Kill(entireProcessTree: true); throw new TimeoutException("Episode transition reference timed out."); }
            var expected = output.GetAwaiter().GetResult().Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
            var actual = trace.ToString().Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
            if (process.ExitCode != 0 || !expected.SequenceEqual(actual))
            {
                var index = Enumerable.Range(0, Math.Min(expected.Length, actual.Length)).FirstOrDefault(i => expected[i] != actual[i], -1);
                throw new InvalidOperationException($"Episode transition differs at {index}: {expected.ElementAtOrDefault(index)} / " +
                    $"{actual.ElementAtOrDefault(index)}; {errors.GetAwaiter().GetResult()}");
            }
        }
        finally
        {
            for (var i = 1; i <= 5; i++) File.Delete(Path.Combine(root, $"tyrian{i}.lvl"));
            File.Delete(Path.Combine(root, "fixture.tyrr")); Directory.Delete(root);
        }
    }

    private static void TestHost()
    {
        var shapes = new MainShapeTables();
        using var data = new MemoryStream(); BinaryFile.WriteUInt16(data, 151);
        for (var i = 0; i < 151; i++)
        {
            BinaryFile.WriteBoolean(data, true); BinaryFile.WriteUInt16(data, 1); BinaryFile.WriteUInt16(data, 1);
            BinaryFile.WriteUInt16(data, 1); data.WriteByte(0xAF);
        }
        foreach (var table in shapes.Tables) { data.Position = 0; table.Load(data); }
        var surface = new IndexedSurface(320, 200, 327, TextWriter.Null);
        var expected = new IndexedSurface(320, 200, 327, TextWriter.Null);
        var font = new FontRenderer(shapes); var platform = new TextGlowTests.Platform();
        var input = new InputState(platform, new VideoLayout()); var timer = new DelayTimer(input);
        var palette = new PaletteSystem(color => (uint)(color.Red << 16 | color.Green << 8 | color.Blue));
        var colors = Enumerable.Range(0, 256).Select(i => new PaletteColor((byte)i, (byte)i, (byte)i)).ToArray();
        var presents = 0; var keeps = 0; var checks = 0; var credits = 0; var songs = new List<uint>();
        var host = new EpisodeTransitionHost(surface, font, palette, colors, input, timer,
            () => checks++, () => credits++, song => songs.Add(song), () => presents++, () => keeps++);
        surface.Pixels.Span.Fill(0x75); host.Clear();
        host.SelectPalette(); host.Text(130, "Episode"u8.ToArray());
        font.DrawString(expected, font.Center("Episode"u8, Font.Normal), 130, "Episode"u8, Font.Normal);
        if (!surface.Pixels.Span.SequenceEqual(expected.Pixels.Span)) throw new InvalidOperationException("Episode title concrete font pixels differ.");
        host.Present(); host.FadeIn(15);
        if (!palette.Active.Span.SequenceEqual(colors)) throw new InvalidOperationException("Episode palette fade-in differs.");
        host.CheckHighScores(); host.Credits(); host.PlaySong(26); host.WipeKey(); host.KeepAlive(); host.Delay(16);
        platform.Events.Enqueue(new(InputEventKind.KeyDown, 40));
        if (!host.AnyButton()) throw new InvalidOperationException("Episode transition did not read input.");
        host.FadeBlack(15);
        if (palette.Active.Span.ToArray().Any(color => color.Red != 0 || color.Green != 0 || color.Blue != 0) ||
            checks != 1 || credits != 1 || songs.Count != 1 || songs[0] != 26 || presents != 31 || keeps != 1 || platform.Ticks == 0)
            throw new InvalidOperationException("Episode concrete host service integration differs.");
    }

    private static int Bit(bool value) => value ? 1 : 0;
    private sealed class TraceHost(StringBuilder trace, int mode) : IEpisodeTransitionHost
    {
        public Action BeforeSong { get; set; } = () => { };
        public int Buttons { get; private set; }
        public void CheckHighScores() => trace.AppendLine("highscores");
        public void Credits() => trace.AppendLine("credits");
        public void PlaySong(uint song) { BeforeSong(); trace.AppendLine($"song:{song}"); }
        public void Clear() => trace.AppendLine("clear");
        public void SelectPalette() { }
        public void Text(int y, ReadOnlyMemory<byte> text)
        {
            var value = Encoding.Latin1.GetString(text.Span);
            trace.AppendLine($"center:{value},1"); trace.AppendLine($"title:100,{y},{value},1");
        }
        public void Present() => trace.AppendLine("present");
        public void FadeIn(int steps) => trace.AppendLine($"fadein:{steps},0,255");
        public void WipeKey() => trace.AppendLine("wipe");
        public void KeepAlive() => trace.AppendLine("keep");
        public void Delay(uint milliseconds) => trace.AppendLine($"delay:{milliseconds}");
        public bool AnyButton() { Buttons++; var result = Buttons == mode % 4 + 1; trace.AppendLine($"button:{Bit(result)}"); return result; }
        public void FadeBlack(int steps) => trace.AppendLine($"fade:{steps}");
    }
}
