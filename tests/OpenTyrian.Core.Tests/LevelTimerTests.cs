using System.Diagnostics;
using System.Globalization;
using System.Text;
using OpenTyrian.Core;

namespace OpenTyrian.Core.Tests;

internal static class LevelTimerTests
{
    public static void Run(string? executable = null)
    {
        var trace = new StringBuilder();
        var surface = new IndexedSurface(320, 200, 327, TextWriter.Null);
        for (var mode = 0; mode < 4096; mode++)
        {
            var fixture = new LevelPresentationTests.Fixture(surface, new(), (byte)(mode % 6), 3);
            fixture.Keyboard.Debug = (mode & 1) != 0; fixture.Combat.LevelTimer = (mode & 2) != 0;
            fixture.Pickups.DisplayTime = (ushort)(mode % 7);
            fixture.Level.TimerCountdown = unchecked((ushort)(mode % 4 == 0 ? 0 : mode % 4 == 1 ? 1 : mode % 4 == 2 ? mode % 701 : mode * 97));
            fixture.Level.TimerJump = unchecked((ushort)(mode * 31)); fixture.Level.Location = unchecked((ushort)(mode * 41));
            fixture.Level.MapX = unchecked((ushort)(mode * 97)); fixture.Level.Events = [new() { Time = ushort.MaxValue }];
            var player = fixture.Game.Players[0];
            player.X = mode % 401 - 50; player.Y = mode % 251 - 30;
            player.XVelocity = mode % 41 - 20; player.YVelocity = mode % 43 - 21;
            fixture.Effects.SoundQueue[6] = 8; fixture.Effects.SoundQueue[7] = 9;
            for (var i = 0; i < 9; i++) fixture.Backgrounds.Smoothies[i] = (mode >> i & 1) != 0;
            var host = new TraceHost(trace, fixture.Level);
            var display = new LevelTimerDisplay(fixture.Keyboard, player, fixture.Backgrounds, fixture.Settings,
                fixture.Level, fixture.Combat, fixture.Pickups, fixture.Effects, host)
            { Flash = unchecked((byte)(mode * 71)), FlashChange = (sbyte)(mode % 9 - 4) };
            for (var frame = 0; frame < 4; frame++)
            {
                host.Ticks = (mode % 17 == 0 ? 2147483648u + (uint)(mode * 97) : mode % 11 == 0 ? 0 : (uint)(mode * 97)) + (uint)(frame * (mode % 61));
                host.Jump = fixture.Combat.LevelTimer && fixture.Level.TimerCountdown == 1;
                trace.AppendLine($"case:{mode},{frame}"); display.Draw(mode % 203);
                trace.AppendLine($"state:{fixture.Pickups.DisplayTime},{display.Flash},{display.FlashChange},{fixture.Settings.FrameCountMax}," +
                    $"{fixture.Level.TimerCountdown},{fixture.Effects.SoundQueue[6]},{fixture.Effects.SoundQueue[7]},{fixture.Level.Location}," +
                    $"{fixture.Keyboard.LastDebugTime},{fixture.Keyboard.DebugHistoryCount},{((double)fixture.Keyboard.DebugHistory).ToString("F0", CultureInfo.InvariantCulture)}");
            }
        }
        TestHost(surface);
        TestFailure(surface);
        TestDebug(surface);
        if (executable is null) return;
        using var process = Process.Start(new ProcessStartInfo(executable) { RedirectStandardOutput = true, RedirectStandardError = true })
            ?? throw new InvalidOperationException("Cannot start native level timer reference.");
        var output = process.StandardOutput.ReadToEndAsync(); var errors = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(30000)) { process.Kill(entireProcessTree: true); throw new TimeoutException("Level timer reference timed out."); }
        var expected = output.GetAwaiter().GetResult().Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var actual = trace.ToString().Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        if (process.ExitCode != 0 || !expected.SequenceEqual(actual))
        {
            var index = Enumerable.Range(0, Math.Min(expected.Length, actual.Length)).FirstOrDefault(i => expected[i] != actual[i], -1);
            throw new InvalidOperationException($"Level timer differs at {index}: {expected.ElementAtOrDefault(index)} / " +
                $"{actual.ElementAtOrDefault(index)}; {errors.GetAwaiter().GetResult()}");
        }
    }

    internal static void RunSdl(SdlVideoDevice video)
    {
        TestHost(video.Primary);
        video.Present();
    }

    private static void TestHost(IndexedSurface surface)
    {
        surface.Pixels.Span.Clear();
        var expected = new IndexedSurface(surface.Width, surface.Height, surface.Pitch, TextWriter.Null);
        var fixture = new LevelPresentationTests.Fixture(surface, new(), 5, 3);
        fixture.Pickups.DisplayTime = 2; fixture.Combat.LevelTimer = true;
        fixture.Level.TimerCountdown = 301;
        var text = new HelpTextData(); text.MiscText[59] = "Bonus level"u8.ToArray(); text.MiscText[66] = "Time remaining"u8.ToArray();
        var font = new FontRenderer(HighScoreScreenTests.CreateShapes(151));
        var display = new LevelTimerDisplay(fixture.Keyboard, fixture.Game.Players[0], fixture.Backgrounds, fixture.Settings,
            fixture.Level, fixture.Combat, fixture.Pickups, fixture.Effects, new LevelTimerDisplayHost(surface, font, text, () => 97))
        { Flash = 3, FlashChange = 1 };
        display.Draw(9);
        font.OutTextAndDarken(expected, 90, 10, text.MiscText[59], 15, unchecked((uint)-5), Font.Large);
        font.ShadeText(expected, 140, 6, text.MiscText[66], 7, 0, TextShade.Full);
        font.DrawString(expected, 100, 2, "3.0"u8, Font.Normal);
        if (!surface.Pixels.Span.SequenceEqual(expected.Pixels.Span) || display.DebugTime != 97 ||
            fixture.Pickups.DisplayTime != 1 || display.Flash != 4 || fixture.Settings.FrameCountMax != 3 ||
            fixture.Effects.SoundQueue[6] != 24 || fixture.Effects.SoundQueue[7] != 17)
            throw new InvalidOperationException("Concrete timer/notice font, countdown, sound or Pentium pacing differs.");
        fixture.Level.TimerCountdown = 1; fixture.Level.TimerJump = 200; fixture.Level.Location = 17;
        fixture.Level.Events = [new() { Time = 10 }, new() { Time = 200 }, new() { Time = 500 }];
        display.Draw(9);
        if (fixture.Level.Location != 200 || fixture.Level.ReturnLocation != 18 || fixture.Level.EventLocation != 1 ||
            fixture.Level.TimerCountdown != 0 || fixture.Settings.FrameCountMax != 2)
            throw new InvalidOperationException("Actual timer expiration did not jump or advance alternating pacing.");
        var input = new InputState(new TextGlowTests.Platform(), new());
        var timer = new DelayTimer(input);
        var present = new LevelPresentation(surface, surface, fixture.Game.Players[0], fixture.Settings,
            fixture.Keyboard, fixture.Setup, timer, () => { });
        present.Show();
        if (timer.GetDelayTicks() != 28) throw new InvalidOperationException("Pentium frame count did not reach level presentation PIT timing.");
    }

    private static void TestFailure(IndexedSurface surface)
    {
        var fixture = new LevelPresentationTests.Fixture(surface, new(), 5, 3);
        fixture.Combat.LevelTimer = true; fixture.Level.TimerCountdown = 1; fixture.Level.TimerJump = 20;
        fixture.Level.Events = [];
        var trace = new StringBuilder();
        var host = new TraceHost(trace, fixture.Level);
        var display = new LevelTimerDisplay(fixture.Keyboard, fixture.Game.Players[0], fixture.Backgrounds, fixture.Settings,
            fixture.Level, fixture.Combat, fixture.Pickups, fixture.Effects, host);
        try { display.Draw(0); throw new InvalidOperationException("Missing expiration target hidden."); }
        catch (InvalidDataException error) when (error.Message.Contains("Level jump", StringComparison.Ordinal)) { }
        if (trace.Length != 0 || fixture.Level.TimerCountdown != 0) throw new InvalidOperationException("Failed timer jump drew success-shaped timer.");
        fixture.Keyboard.Debug = true;
        try { fixture.Keyboard.RecordDebugFrame(100, _ => throw new IOException("debug failed")); throw new InvalidOperationException("Debug failure hidden."); }
        catch (IOException error) when (error.Message == "debug failed") { }
        if (fixture.Keyboard.LastDebugTime != 0 || fixture.Keyboard.DebugHistoryCount != 1 || fixture.Keyboard.DebugHistory != 100)
            throw new InvalidOperationException("Failed debug drawing advanced last-frame time.");
    }

    private static void TestDebug(IndexedSurface surface)
    {
        var fixture = new LevelPresentationTests.Fixture(surface, new(), 2, 3);
        fixture.Keyboard.Debug = true;
        fixture.Level.MapX = 2; fixture.Level.Location = 42; fixture.Game.Players[0].X = 100;
        var trace = new StringBuilder();
        var display = new LevelTimerDisplay(fixture.Keyboard, fixture.Game.Players[0], fixture.Backgrounds, fixture.Settings,
            fixture.Level, fixture.Combat, fixture.Pickups, fixture.Effects, new TraceHost(trace, fixture.Level) { Ticks = 0 });
        var culture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            display.Draw(9);
            if (!trace.ToString().Contains("X:112 Y:42     inf FPS", StringComparison.Ordinal))
                throw new InvalidOperationException("Zero elapsed debug FPS or native left-justified location differs.");
            trace.Clear();
            var font = new FontRenderer(HighScoreScreenTests.CreateShapes(151));
            var expected = new IndexedSurface(surface.Width, surface.Height, surface.Pitch, TextWriter.Null);
            var host = new LevelTimerDisplayHost(surface, font, new(), () => 0);
            surface.Pixels.Span.Clear();
            host.DebugText(30, 70, "SM = 000000000", 4, 0);
            font.OutText(expected, 30, 70, "SM = 000000000"u8, 4, 0);
            if (!surface.Pixels.Span.SequenceEqual(expected.Pixels.Span)) throw new InvalidOperationException("Concrete debug font pixels differ.");
        }
        finally { CultureInfo.CurrentCulture = culture; }
    }

    private sealed class TraceHost(StringBuilder trace, LevelEvents level) : ILevelTimerDisplayHost
    {
        public uint Ticks { get; set; }
        public bool Jump { get; set; }
        public void DebugText(int x, int y, string text, byte hue, int brightness) => trace.AppendLine($"debug:{x},{y},{text},{hue},{brightness}");
        public void Notice(int brightness) => trace.AppendLine($"notice:{unchecked((uint)brightness)}");
        public void TimerLabel(int brightness)
        {
            if (Jump) trace.AppendLine($"jump:{level.TimerJump}");
            trace.AppendLine($"label:{brightness}");
        }
        public void TimerValue(string value) => trace.AppendLine($"value:{value}");
    }
}
