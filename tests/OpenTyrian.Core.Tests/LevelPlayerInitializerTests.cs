using System.Diagnostics;
using System.Text;
using OpenTyrian.Core;

namespace OpenTyrian.Core.Tests;

internal static class LevelPlayerInitializerTests
{
    public static void Run(string? executable = null)
    {
        var trace = new StringBuilder();
        for (var mode = 0; mode < 512; mode++)
        {
            var game = new LegacyGameState
            {
                TwoPlayerMode = (mode & 1) != 0,
                Episode = (byte)(mode % 6),
                Difficulty = (sbyte)(mode % 15 - 3),
                OldDifficulty = (sbyte)(mode * 31),
                LevelName = "Sample level"u8.ToArray()
            };
            for (var p = 0; p < 2; p++)
            {
                game.Players[p].X = mode * 17 + p * 53 - 500;
                game.Players[p].Y = mode * 29 + p * 71 - 700;
                game.Players[p].IsAlive = (mode >> p & 1) != 0;
                game.Players[p].LastXShotMove = -777; game.Players[p].LastYShotMove = 888;
                Array.Fill(game.Players[p].OldX, -111); Array.Fill(game.Players[p].OldY, 222);
            }
            var host = new TraceHost(trace);
            var initializer = new LevelPlayerInitializer(game, host);
            var sheet = new SpriteSheet();
            if ((mode & 2) != 0) sheet.Load(new MemoryStream([1]), 1);
            trace.AppendLine($"case:{mode}"); initializer.Initialize((byte)(mode % 4), sheet);
            trace.AppendLine($"state:{game.Difficulty},{game.OldDifficulty},{game.Episode},{(game.TwoPlayerMode ? 1 : 0)}");
            foreach (var player in game.Players)
            {
                trace.AppendLine($"player:{(player.IsAlive ? 1 : 0)},{player.X},{player.Y},{player.LastXShotMove},{player.LastYShotMove},0");
                for (var i = 0; i < player.OldX.Length; i++) trace.AppendLine($"history:{player.OldX[i]},{player.OldY[i]}");
            }
        }
        TestFailure();
        TestConcreteHost();
        if (executable is null) return;
        using var process = Process.Start(new ProcessStartInfo(executable) { RedirectStandardOutput = true, RedirectStandardError = true })
            ?? throw new InvalidOperationException("Cannot start native level player initialization reference.");
        var output = process.StandardOutput.ReadToEndAsync(); var errors = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(30000)) { process.Kill(entireProcessTree: true); throw new TimeoutException("Level player initialization reference timed out."); }
        var expected = output.GetAwaiter().GetResult().Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var actual = trace.ToString().Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        if (process.ExitCode != 0 || !expected.SequenceEqual(actual))
        {
            var index = Enumerable.Range(0, Math.Min(expected.Length, actual.Length)).FirstOrDefault(i => expected[i] != actual[i], -1);
            throw new InvalidOperationException($"Level player initialization differs at {index}: {expected.ElementAtOrDefault(index)} / " +
                $"{actual.ElementAtOrDefault(index)}; {errors.GetAwaiter().GetResult()}");
        }
    }

    private static void TestFailure()
    {
        var game = new LegacyGameState { Episode = 4, Difficulty = 2, LevelName = "Start"u8.ToArray() };
        game.Players[0].X = 20;
        var trace = new StringBuilder(); var host = new TraceHost(trace) { ThrowAt = "gamma" };
        try { new LevelPlayerInitializer(game, host).Initialize(2, new()); throw new InvalidOperationException("Gamma failure hidden."); }
        catch (IOException error) when (error.Message == "gamma") { }
        if (game.Difficulty != 1 || game.OldDifficulty != 2 || game.Players.Any(player => !player.IsAlive) ||
            game.Players[0].X != 100 || trace.ToString().Contains("fade:", StringComparison.Ordinal))
            throw new InvalidOperationException("Native sequential initialization mutation/failure ordering differs.");
        trace.Clear(); host.ThrowAt = "fade";
        try { new LevelPlayerInitializer(game, host).Initialize(2, new()); throw new InvalidOperationException("Fade failure hidden."); }
        catch (IOException error) when (error.Message == "fade") { }
        if (trace.ToString().Split('\n').TakeWhile(line => line != "present").Count() == 0 ||
            trace.ToString().Contains("shapes:", StringComparison.Ordinal))
            throw new InvalidOperationException("Palette failure concealed or explosion sheet loaded after fade failure.");
    }

    private static void TestConcreteHost()
    {
        var surface = new IndexedSurface(320, 200, 327, TextWriter.Null);
        var colors = Enumerable.Range(0, 256).Select(i => new PaletteColor((byte)i, (byte)(255 - i), (byte)(i / 2))).ToArray();
        var paletteTrace = new List<string>();
        var palette = new PaletteSystem(color => (uint)(color.Red << 16 | color.Green << 8 | color.Blue));
        var font = new FontRenderer(HighScoreScreenTests.CreateShapes(151));
        var events = new List<string>(); var shapes = new SpriteSheet();
        var host = new LevelPlayerInitializationHost(colors, palette, surface, font,
            (number, keep) => { events.Add($"picture:{number},{keep}"); surface.Pixels.Span.Clear(); },
            () => events.Add("options"), () => events.Add("present"), suffix => events.Add($"shapes:{suffix}"),
            () => events.Add("delay"), () => events.Add("fade-present"), () => events.Add("wait"));
        var game = new LegacyGameState { TwoPlayerMode = true, Difficulty = 5, Episode = 2, LevelName = "Level"u8.ToArray() };
        new LevelPlayerInitializer(game, host).Initialize(3, shapes);
        var expectedSurface = new IndexedSurface(320, 200, 327, TextWriter.Null);
        font.OutText(expectedSurface, 268, 76, "Level"u8, 12, 4);
        if (!events.SequenceEqual(["picture:6,False", "options", "present",
                .. Enumerable.Repeat(new[] { "delay", "fade-present", "wait" }, 50).SelectMany(steps => steps), "shapes:6"]) ||
            palette.Active.Span[0].Red != PaletteSystem.GammaChannel(0, 1.3f) ||
            !surface.Pixels.Span.SequenceEqual(expectedSurface.Pixels.Span))
            throw new InvalidOperationException($"Real palette fade, draw order or conditional explosion load differs ({events.Count} events, pixel mismatch {surface.Pixels.Span.SequenceEqual(expectedSurface.Pixels.Span)}).");
        events.Clear(); shapes.Load(new MemoryStream([1]), 1);
        var onePlayer = new LegacyGameState { Difficulty = 1, Episode = 4, LevelName = "Level"u8.ToArray() };
        new LevelPlayerInitializer(onePlayer, host).Initialize(0, shapes);
        if (events.Contains("shapes:6") || onePlayer.Difficulty != 1 || onePlayer.OldDifficulty != 1 ||
            onePlayer.Players[1].X != 190 || onePlayer.Players[0].OldX[0] != 81 || onePlayer.Players[1].OldY[19] != 162)
            throw new InvalidOperationException("Existing explosion sheet, difficulty clamp or player history differs.");
    }

    private sealed class TraceHost(StringBuilder trace) : ILevelPlayerInitializationHost
    {
        public string? ThrowAt { get; set; }
        private void Add(string text) { trace.AppendLine(text); if (ThrowAt == text.Split(':')[0]) throw new IOException(ThrowAt); }
        public void LoadPicture(int number, bool preservePalette) => Add($"picture:{number},{(preservePalette ? 1 : 0)},0");
        public void DrawOptions() => Add("options");
        public void DrawLevelName(int x, int y, ReadOnlyMemory<byte> name, byte hue, int brightness)
            => Add($"text:{x},{y},{Encoding.Latin1.GetString(name.Span)},{hue},{brightness}");
        public void Present() => Add("present");
        public void CorrectGamma(byte correction) => Add($"gamma:{correction}");
        public void FadePalette(int steps, int first, int last) => Add($"fade:{steps},{first},{last}");
        public void LoadExplosionShapes(char suffix) => Add($"shapes:{suffix}");
    }
}
