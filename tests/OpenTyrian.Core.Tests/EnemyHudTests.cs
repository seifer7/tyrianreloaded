using System.Diagnostics;
using System.Text;
using OpenTyrian.Core;

namespace OpenTyrian.Core.Tests;

internal static class EnemyHudTests
{
    public static void Run(string? executable = null)
    {
        var definitions = new ItemDefinitions();
        definitions.ParseBundle(Encoding.Latin1.GetBytes(LevelEventTests.Fixture()), "boss.tyrr");
        var items = ItemDatabase.FromDefinitions(definitions);
        var expected = Execute(items);
        TestRendering(items);
        if (executable is null) return;
        var start = new ProcessStartInfo(executable) { RedirectStandardOutput = true, RedirectStandardError = true };
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Cannot start boss-bar oracle.");
        var output = process.StandardOutput.ReadToEndAsync();
        var errors = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(30000))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException("Boss-bar oracle timed out.");
        }
        var actual = output.GetAwaiter().GetResult().Replace("\r\n", "\n", StringComparison.Ordinal);
        var error = errors.GetAwaiter().GetResult();
        Check(process.ExitCode == 0 && actual == expected && error.Length == 0, $"Native boss-bar parity differs: {error}");
    }

    private static string Execute(ItemDatabase items)
    {
        using var warnings = new StringWriter();
        var output = new StringBuilder();
        for (var mode = 0; mode < 512; mode++)
        {
            var enemies = new EnemySystem(items, warnings);
            var first = enemies.BossBars[0]; var second = enemies.BossBars[1];
            first.LinkNumber = (byte)((mode & 1) != 0 ? 2 : 0);
            second.LinkNumber = (byte)((mode & 2) != 0 ? 3 : 0);
            first.Armor = 42; second.Armor = 43;
            first.Color = (byte)(mode % 256); second.Color = (byte)((mode + 127) % 256);
            for (var i = 0; i < 12; i++)
            {
                var enemy = enemies.Enemies[i];
                enemy.Availability = (byte)((mode & 8) != 0 ? 1 : i % 3);
                enemy.LinkNumber = (byte)(i % 2 + 2);
                enemy.Armor = (byte)((mode & 16) != 0 ? 255 : (mode & 32) != 0 ? 0 : (mode + i * 37) % 255 + 1);
            }
            output.Append($"bars:{mode}\n");
            enemies.DrawBossBars((x1, y1, x2, y2, color) => output.Append($"fill:{x1},{y1},{x2},{y2},{color}\n"), (mode & 4) != 0);
            output.Append($"state:{first.LinkNumber},{first.Armor},{first.Color},{second.LinkNumber},{second.Armor},{second.Color}\n");
        }
        return output.ToString();
    }

    private static void TestRendering(ItemDatabase items)
    {
        using var warnings = new StringWriter();
        var enemies = new EnemySystem(items, warnings);
        enemies.Enemies[0].Availability = 0;
        enemies.Enemies[0].LinkNumber = 2;
        enemies.Enemies[0].Armor = 100;
        enemies.BossBars[1].LinkNumber = 2;
        enemies.BossBars[1].Color = 6;
        var surface = new IndexedSurface(320, 200, 323, warnings);
        enemies.DrawBossBars(surface, true);
        Check(enemies.BossBars[0].LinkNumber == 2 && enemies.BossBars[0].Armor == 100 &&
            enemies.BossBars[0].Color == 5 && enemies.BossBars[1].LinkNumber == 0, "Single boss-bar migration or flash decay differs.");
        Check(surface.Pixels.Span[7 * 323 + 225] == 116 && surface.Pixels.Span[7 * 323 + 240] == 125 &&
            surface.Pixels.Span[8 * 323 + 240] == 124 && surface.Pixels.Span[12 * 323 + 260] == 123 &&
            surface.Pixels.Span[7 * 323 + 155] == 0 && warnings.ToString().Length == 0,
            "Boss bar shading, pitch, or timer placement differs.");
        var players = new[] { new PlayerState(0), new PlayerState(1) };
        var effects = new SceneEffects(new byte[8]);
        var events = new LevelEvents(items, enemies, players, new BackgroundRenderer(), effects)
        {
            Events = [new LevelEvent { Type = 79, Data = 2 }]
        };
        var combat = new PlayerCombat(players, effects, surface);
        var shots = new PlayerShots(items.Shots, items.Weapons, players, effects.CreateProjectileEffects(surface,
            new SpriteSheet(), new SpriteSheet(), new SpriteTable()), enemies.Enemies, effects.SoundQueue);
        events.HandleMiscEvent(shots, combat);
        enemies.DrawBossBars(surface, false);
        Check(surface.Pixels.Span[7 * 323 + 130] == 116, "Scripted boss links did not reach HUD rendering.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
