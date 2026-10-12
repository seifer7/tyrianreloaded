using System.Diagnostics;
using System.Globalization;
using System.Text;
using OpenTyrian.Core;

namespace OpenTyrian.Core.Tests;

internal static class PlayerCombatTests
{
    public static void Run(string? executable = null)
    {
        using var warnings = new StringWriter();
        var surface = new IndexedSurface(320, 200, 320, warnings);
        var players = new[] { new PlayerState(0), new PlayerState(1) };
        var effects = new SceneEffects(new byte[8]);
        var combat = new PlayerCombat(players, effects, surface) { LevelTimer = true, MusicVolume = 180 };
        players[0].Armor = 5;
        players[0].IsAlive = true;
        players[0].X = 100; players[0].Y = 100;
        Check(combat.Damage(5, players[0]) == 5 && players[0].IsAlive && players[0].Armor == 0 &&
            effects.SoundQueue[7] == 19, "Exact armor damage incorrectly kills the player.");
        Check(combat.Damage(1, players[0]) == 1 && !players[0].IsAlive && players[0].ExplodingTicks == 60 &&
            !combat.LevelTimer && combat.LevelEnd == 40 && combat.TemporaryVolume == 180,
            "Player death state differs.");
        players[0].Shield = 5;
        combat.Damage(5, players[0]);
        Check(effects.Explosions.Count(explosion => explosion.Lifetime > 0) == 9 && effects.Explosions[0].FollowPlayer,
            "Shield-hit explosion integration differs.");
        try
        {
            PlayerCombat.DrawBar(surface, 270, 194, 255, 144);
            throw new InvalidOperationException("Infinite native bar loop accepted.");
        }
        catch (ArgumentOutOfRangeException) { }
        if (executable is null) return;
        foreach (var mode in Enumerable.Range(0, 8))
            foreach (var pitch in new[] { 320, 327 })
            {
                var expected = Execute(mode, pitch);
                var start = new ProcessStartInfo(executable) { RedirectStandardOutput = true, RedirectStandardError = true };
                start.ArgumentList.Add(mode.ToString(CultureInfo.InvariantCulture));
                start.ArgumentList.Add(pitch.ToString(CultureInfo.InvariantCulture));
                using var process = Process.Start(start) ?? throw new InvalidOperationException("Cannot start player combat oracle.");
                var output = process.StandardOutput.ReadToEndAsync();
                var errors = process.StandardError.ReadToEndAsync();
                if (!process.WaitForExit(30000))
                {
                    process.Kill(entireProcessTree: true);
                    throw new TimeoutException("Player combat oracle timed out.");
                }
                var actual = output.GetAwaiter().GetResult().Replace("\r\n", "\n", StringComparison.Ordinal);
                var difference = actual.Split('\n').Zip(expected.Split('\n')).FirstOrDefault(pair => pair.First != pair.Second);
                Check(process.ExitCode == 0 && actual == expected,
                    $"Player combat mode {mode}, pitch {pitch} differs: {difference.First} / {difference.Second} {errors.GetAwaiter().GetResult()}");
            }
    }

    private static string Execute(int mode, int pitch)
    {
        using var warnings = new StringWriter();
        var hud = new IndexedSurface(320, 200, pitch, warnings);
        var output = new StringBuilder();
        var definitions = new ItemDefinitions();
        definitions.ParseBundle(Encoding.Latin1.GetBytes(ItemDatabaseTests.Fixture(0) +
            "[weapon:hud1]\nopnum=2\n[weapon:hud2]\nopnum=7\n"), "fixture.tyrr");
        var items = ItemDatabase.FromDefinitions(definitions);
        var players = new[] { new PlayerState(0), new PlayerState(1) };
        players[0].Items.Weapons[1].Id = (byte)items.Index(ItemType.WeaponRear, "hud1");
        players[1].Items.Weapons[1].Id = (byte)items.Index(ItemType.WeaponRear, "hud2");
        players[0].X = 80; players[0].Y = 100; players[1].X = 140; players[1].Y = 60;
        var combat = new PlayerCombat(players, new SceneEffects(new byte[8]), hud);
        for (var shield = 0; shield < 9; shield++)
            for (var armor = 0; armor < 5; armor++)
                for (var amount = 0; amount < 12; amount++)
                {
                    var effects = new SceneEffects(new byte[8]);
                    combat = new PlayerCombat(players, effects, hud)
                    {
                        TwoPlayers = (mode & 1) != 0,
                        Galaga = (mode & 2) != 0,
                        Cheating = (mode & 4) != 0,
                        LevelTimer = true,
                        MusicVolume = 180,
                        TemporaryVolume = 42
                    };
                    for (var i = 0; i < hud.Pixels.Length; i++) hud.Pixels.Span[i] = unchecked((byte)(i * 17 + i / 7));
                    for (var i = 0; i < 2; i++)
                    {
                        players[i].Shield = (uint)(shield + i);
                        players[i].ShieldMax = (uint)(9 + i);
                        players[i].Armor = (uint)(armor + i);
                        players[i].IsAlive = true;
                        players[i].ExplodingTicks = 0;
                    }
                    var damage = combat.Damage((byte)amount, players[mode & 1]);
                    players[1].Armor = 50;
                    combat.DrawArmor();
                    var hit = players[mode & 1];
                    output.Append($"damage:{shield}:{armor}:{amount}={damage},{(combat.LevelTimer ? 1 : 0)},{combat.LevelEnd}," +
                        $"{combat.TemporaryVolume},{players[0].Shield},{players[0].Armor},{players[1].Shield},{players[1].Armor}," +
                        $"{(hit.IsAlive ? 1 : 0)},{hit.ExplodingTicks},{effects.SoundQueue[1]},{effects.SoundQueue[7]}\n");
                    for (var i = 0; i < 9; i++)
                    {
                        var e = effects.Explosions[i];
                        output.Append($"E:{i}={e.Lifetime},{e.X},{e.Y},{e.DeltaX},{e.DeltaY},{(e.FixedPosition ? 1 : 0)},{(e.FollowPlayer ? 1 : 0)},{e.Sprite}\n");
                    }
                    uint hash = 2166136261;
                    foreach (var pixel in hud.Pixels.Span) hash = unchecked((hash ^ pixel) * 16777619);
                    output.Append($"pixels:{hash}\n");
                }
        output.Append($"port:{combat.PortConfigurations(items.Weapons)}\n");
        Check(warnings.ToString().Length == 0, "Normal combat gauges emitted clipping warnings.");
        return output.ToString();
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
