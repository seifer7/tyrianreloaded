using System.Diagnostics;
using System.Text;
using OpenTyrian.Core;

namespace OpenTyrian.Core.Tests;

internal static class ArmorWarningTests
{
    public static void Run(string? executable = null)
    {
        var trace = new StringBuilder();
        var players = new[] { new PlayerState(0), new PlayerState(1) };
        var surface = new IndexedSurface(320, 200, 327, TextWriter.Null);
        var effects = new SceneEffects(new byte[8]); var combat = new PlayerCombat(players, effects, surface);
        var warning = new WarningBars(surface, new(new InputState(new TextGlowTests.Platform(), new())), () => { }, _ => { });
        for (var mode = 0; mode < 16384; mode++)
        {
            players[0].IsAlive = (mode & 1) != 0; players[1].IsAlive = (mode & 2) != 0;
            combat.TwoPlayers = (mode & 4) != 0; combat.Galaga = (mode & 8) != 0;
            players[0].Armor = (uint)((mode >> 6) % 8); players[1].Armor = (uint)((mode >> 9) % 8);
            warning.SoundDelay = unchecked((byte)(mode * 13)); warning.Color = unchecked((byte)(mode * 71));
            warning.ColorChange = (sbyte)(mode % 7 - 3); effects.SoundQueue[7] = 9;
            var host = new Host(trace, mode); var armor = new ArmorWarning(players, combat, warning, effects, host)
            { ShipDelay = mode % 5 == 0 ? (ushort)0 : (ushort)(mode % 503) };
            MersenneTwister.Seed((uint)(mode + 1)); trace.AppendLine($"case:{mode}");
            armor.Draw((mode & 16) != 0, (mode & 32) != 0 ? (byte)2 : (byte)1, unchecked((ushort)(mode * 97)));
            trace.AppendLine($"state:{armor.ShipDelay},{warning.SoundDelay},{warning.Color},{warning.ColorChange}," +
                $"{effects.SoundQueue[7]},{host.Drop},{host.Speed},{host.Armor}");
            trace.AppendLine($"rng:{MersenneTwister.NextUInt32()}");
        }
        TestHost();
        if (executable is null) return;
        using var process = Process.Start(new ProcessStartInfo(executable) { RedirectStandardOutput = true, RedirectStandardError = true })
            ?? throw new InvalidOperationException("Cannot start native armor warning reference.");
        var output = process.StandardOutput.ReadToEndAsync(); var errors = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(30000)) { process.Kill(entireProcessTree: true); throw new TimeoutException("Armor warning reference timed out."); }
        var expected = output.GetAwaiter().GetResult().Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var actual = trace.ToString().Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        if (process.ExitCode != 0 || !expected.SequenceEqual(actual))
        {
            var index = Enumerable.Range(0, Math.Min(expected.Length, actual.Length)).FirstOrDefault(i => expected[i] != actual[i], -1);
            throw new InvalidOperationException($"Armor warning differs at {index}: {expected.ElementAtOrDefault(index)} / " +
                $"{actual.ElementAtOrDefault(index)}; {errors.GetAwaiter().GetResult()}");
        }
    }

    private static void TestHost()
    {
        var definitions = new ItemDefinitions();
        definitions.ParseBundle(Encoding.Latin1.GetBytes(ItemDatabaseTests.Fixture(0) +
            "[enemy:560]\narmor=10\nshapebank=3\nymove=-4\n[enemy:561]\narmor=0\n[enemy:562]\narmor=0\n[enemy:563]\narmor=0\n"),
            "armor-warning.tyrr");
        var items = ItemDatabase.FromDefinitions(definitions);
        var surface = new IndexedSurface(320, 200, 327, TextWriter.Null);
        var expected = new IndexedSurface(320, 200, 327, TextWriter.Null);
        var players = new[] { new PlayerState(0) { IsAlive = true, Armor = 2 }, new PlayerState(1) };
        var effects = new SceneEffects(new byte[8]); var combat = new PlayerCombat(players, effects, surface);
        var enemies = new EnemySystem(items, TextWriter.Null); enemies.SpriteIds[0] = 3;
        var font = new FontRenderer(HighScoreScreenTests.CreateShapes(151));
        var host = new ArmorWarningHost(enemies, items, surface, font);
        var warning = new WarningBars(surface, new(new InputState(new TextGlowTests.Platform(), new())), () => { }, _ => { })
        { Color = 120, ColorChange = 1, SoundDelay = 0 };
        var armor = new ArmorWarning(players, combat, warning, effects, host);
        MersenneTwister.Seed(42); armor.Draw(false, 1, 65535);
        expected.FillRectangle(24, 181, 138, 183, 121); expected.FillRectangle(175, 181, 287, 183, 121);
        expected.FillRectangle(24, 0, 287, 3, 121); font.OutText(expected, 140, 178, "WARNING"u8, 7, 4);
        var ship = enemies.Enemies[50];
        if (!surface.Pixels.Span.SequenceEqual(expected.Pixels.Span) || armor.ShipDelay != 500 || warning.SoundDelay != 16 ||
            effects.SoundQueue[7] != 17 || ship.Armor != 4 || ship.YSpeed != -3 ||
            !new[] { items.Index(ItemType.Enemy, 561), items.Index(ItemType.Enemy, 562), items.Index(ItemType.Enemy, 563) }.Contains(ship.EnemyDie))
            throw new InvalidOperationException("Concrete armor warning spawn, drop, pixels, sound or fixed-width speed differs.");
        armor.Draw(false, 1, 0);
        if (armor.ShipDelay != 499 || warning.SoundDelay != 15) throw new InvalidOperationException("Armor warning delay did not advance.");
        foreach (var enemy in enemies.Enemies.Skip(50).Take(25)) enemy.Availability = 0;
        if (host.CreateShip() != 0) throw new InvalidOperationException("Full rescue-ship allocation succeeded.");
        try { host.ConfigureShip(51, 564, 0); throw new InvalidOperationException("Missing armor drop accepted."); }
        catch (InvalidDataException) { }
    }

    private sealed class Host(StringBuilder trace, int mode) : IArmorWarningHost
    {
        public ushort Drop { get; private set; } = 10;
        public sbyte Speed { get; private set; } = -4;
        public byte Armor { get; private set; } = 255;
        public short CreateShip() { trace.AppendLine("create:50,42,0"); return mode % 3 == 0 ? (short)0 : (short)51; }
        public void ConfigureShip(short ship, int dropId, ushort backgroundMove)
        {
            trace.AppendLine($"drop:7,{dropId}"); Drop = (ushort)(dropId - 500);
            Speed = unchecked((sbyte)(Speed - backgroundMove)); Armor = 4;
        }
        public void Fill(int left, int top, int right, int bottom, byte color) => trace.AppendLine($"fill:{left},{top},{right},{bottom},{color}");
        public void Label(byte brightness) => trace.AppendLine($"label:140,178,WARNING,7,{brightness}");
    }
}
