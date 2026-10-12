using System.Diagnostics;
using System.Text;
using OpenTyrian.Core;

namespace OpenTyrian.Core.Tests;

internal static class LevelStateTests
{
    public static void Run(string? executable = null)
    {
        var definitions = new ItemDefinitions();
        definitions.ParseBundle(Encoding.Latin1.GetBytes(LevelEventTests.Fixture()), "level-state.tyrr");
        var items = ItemDatabase.FromDefinitions(definitions);
        using var warnings = new StringWriter();
        var players = new[] { new PlayerState(0), new PlayerState(1) };
        var surface = new IndexedSurface(320, 200, 320, warnings);
        var output = new StringBuilder();
        for (var mode = 0; mode < 1024; mode++)
        {
            var enemies = new EnemySystem(items, warnings) { EnemiesOnScreen = (ushort)((mode & 64) != 0 ? 1 : 0) };
            var background = new BackgroundRenderer();
            background.First.Move = (ushort)((mode & 2) != 0 ? 1 : 0);
            background.Second.Move = 5; background.Third.Move = 6;
            var effects = new SceneEffects(new byte[8]) { ExplosionMove = 7 };
            if ((mode & 4) != 0)
            {
                effects.Repeating[0].Delay = 1;
                effects.Repeating[0].Lifetime = 1;
                effects.UpdateExplosions((_, _, _, _) => { });
            }
            var combat = new PlayerCombat(players, effects, surface) { LevelEnd = 13, LevelTimer = true };
            var events = new LevelEvents(items, enemies, players, background, effects)
            {
                StopBackgroundNumber = (byte)((mode & 1) != 0 ? 9 : 1),
                AllPlayersGone = (mode & 16) != 0,
                EndLevel = (mode & 32) != 0,
                ReadyToEndLevel = (mode & 128) != 0,
                TimerCountdown = (ushort)((mode & 256) != 0 ? 10 : 0),
                StopBackgrounds = (mode & 512) != 0
            };
            events.UpdateEndState(combat, (mode & 8) != 0);
            output.Append($"end:{mode},{events.StopBackgroundNumber},{background.First.Move},{background.Second.Move},{background.Third.Move},{effects.ExplosionMove},{Flag(events.StopBackgrounds)},{Flag(events.EndLevel)},{Flag(events.ReadyToEndLevel)},{Flag(events.ReallyEndLevel)},{combat.LevelEnd},{Flag(combat.LevelTimer)}\n");
        }
        for (var mode = 0; mode < 76; mode++)
        {
            var effects = new SceneEffects(Enumerable.Range(0, 8).Select(i => (byte)((mode + i) % 39)).ToArray());
            var events = new LevelEvents(items, new EnemySystem(items, warnings), players, new BackgroundRenderer(), effects)
            {
                FirstGameOver = mode % 2 != 0
            };
            output.Append($"audio:{mode}\n");
            events.PlaySounds((sample, channel, volume) => output.Append($"sample:{sample},{channel},{volume}\n"));
            output.Append($"queue:{string.Join(',', effects.SoundQueue)}\n");
        }
        TestAudioFailures(items, players, warnings);
        if (executable is not null) CompareNative(executable, output.ToString());
    }

    private static int Flag(bool value) => value ? 1 : 0;

    private static void TestAudioFailures(ItemDatabase items, PlayerState[] players, TextWriter warnings)
    {
        var effects = new SceneEffects(new byte[8]);
        var events = new LevelEvents(items, new EnemySystem(items, warnings), players, new BackgroundRenderer(), effects)
        {
            FirstGameOver = true
        };
        effects.SoundQueue[3] = 38;
        var failed = false;
        try { events.PlaySounds((_, _, _) => throw new IOException("audio failure")); }
        catch (IOException)
        {
            failed = true;
            if (effects.SoundQueue[3] != 38) throw new InvalidOperationException("Failed playback discarded the pending sound.");
        }
        if (!failed) throw new InvalidOperationException("Playback failure was suppressed.");
        effects.SoundQueue[3] = 39;
        try { events.PlaySounds((_, _, _) => throw new InvalidOperationException("Invalid sample reached playback.")); }
        catch (InvalidDataException) { return; }
        throw new InvalidOperationException("Out-of-range sound ID was accepted.");
    }

    private static void CompareNative(string executable, string expected)
    {
        var start = new ProcessStartInfo(executable) { RedirectStandardOutput = true, RedirectStandardError = true };
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Cannot start level-state oracle.");
        var output = process.StandardOutput.ReadToEndAsync();
        var errors = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(30000))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException("Level-state oracle timed out.");
        }
        var actual = output.GetAwaiter().GetResult().Replace("\r\n", "\n", StringComparison.Ordinal);
        var error = errors.GetAwaiter().GetResult();
        if (process.ExitCode != 0 || actual != expected || error.Length > 0)
            throw new InvalidOperationException($"Native level completion/sound parity differs: {error}");
    }
}
