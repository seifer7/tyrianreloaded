using System.Diagnostics;
using System.Text;
using OpenTyrian.Core;

namespace OpenTyrian.Core.Tests;

internal static class LevelStatusDisplayInitializerTests
{
    public static void Run(string? executable = null)
    {
        var trace = new StringBuilder();
        var surface = new IndexedSurface(320, 200, 320, TextWriter.Null);
        var fixture = new LevelPresentationTests.Fixture(surface, trace, 0, 3, 2);
        var text = new HelpTextData();
        text.MiscText[20] = [4, 7, 9, 0];
        var host = new TraceHost(trace);
        var timer = new LevelTimerDisplay(fixture.Keyboard, fixture.Game.Players[0], fixture.Backgrounds,
            fixture.Settings, fixture.Level, fixture.Combat, fixture.Pickups, fixture.Effects, new NoOpHost());
        var initializer = new LevelStatusDisplayInitializer(fixture.Game, fixture.Items,
            fixture.Combat, timer, fixture.Pickups, text, host);
        fixture.Combat.TwoPlayers = true;

        for (var mode = 0; mode < 256; mode++)
        {
            fixture.Game.CubeMax = (ushort)(mode * 31);
            fixture.Combat.ShieldWait = (byte)(mode * 7);
            fixture.Combat.ShieldT = (byte)(mode * 11);
            fixture.Pickups.DisplayTime = unchecked((byte)(mode * 37));
            timer.Flash = (byte)(mode * 41);
            timer.FlashChange = unchecked((sbyte)(mode * 43));
            for (var i = 0; i < fixture.Game.Players.Length; i++)
            {
                var player = fixture.Game.Players[i];
                player.Items.Shield = (byte)((mode * 7 + i * 11) % 3);
                player.Shield = (uint)(mode * 13 + i * 17);
                player.ShieldMax = (uint)(mode * 19 + i * 23);
                player.Armor = (uint)(mode * 13 + i * 19);
                player.Superbombs = (uint)(mode * 23 + i * 5);
            }

            trace.AppendLine($"case:{mode}");
            initializer.Initialize((byte)mode);
            trace.AppendLine($"state:{fixture.Combat.ShieldWait},{fixture.Combat.ShieldT},{fixture.Game.Players[0].Shield},{fixture.Game.Players[0].ShieldMax},{fixture.Game.Players[0].Armor},{fixture.Game.Players[0].Superbombs},{fixture.Game.Players[1].Shield},{fixture.Game.Players[1].ShieldMax},{fixture.Game.Players[1].Armor},{fixture.Game.Players[1].Superbombs},{fixture.Game.CubeMax},{timer.Flash},{timer.FlashChange},{fixture.Pickups.DisplayTime}");
        }

        if (executable is null) return;
        using var process = Process.Start(new ProcessStartInfo(executable)
        { RedirectStandardOutput = true, RedirectStandardError = true })
            ?? throw new InvalidOperationException("Cannot start level status display reference.");
        var output = process.StandardOutput.ReadToEndAsync();
        var errors = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(30000))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException("Level status display reference timed out.");
        }
        var expected = output.GetAwaiter().GetResult().Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var actual = trace.ToString().Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        if (process.ExitCode != 0 || !expected.SequenceEqual(actual))
        {
            var index = Enumerable.Range(0, Math.Min(expected.Length, actual.Length))
                .FirstOrDefault(i => expected[i] != actual[i], -1);
            throw new InvalidOperationException($"Level status initialization differs at {index}: " +
                $"{expected.ElementAtOrDefault(index)} / {actual.ElementAtOrDefault(index)}; {errors.GetAwaiter().GetResult()}");
        }
    }

    private sealed class TraceHost(StringBuilder trace) : ILevelStatusDisplayHost
    {
        public void DrawTextWindow(ReadOnlyMemory<byte> value)
        {
            if (!value.Span.SequenceEqual(new byte[] { 4, 7, 9, 0 }))
                throw new InvalidOperationException("Level startup text differs.");
            trace.AppendLine("text:20");
        }
        public void PlaySong(uint song) => trace.AppendLine($"song:{song}");
        public void DrawPortConfigButtons() => trace.AppendLine("ports");
    }

    private sealed class NoOpHost : ILevelTimerDisplayHost
    {
        public uint Ticks => 0;
        public void DebugText(int x, int y, string value, byte hue, int brightness) { }
        public void Notice(int brightness) { }
        public void TimerLabel(int brightness) { }
        public void TimerValue(string value) { }
    }
}
