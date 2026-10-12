using System.Diagnostics;
using System.Text;
using OpenTyrian.Core;

namespace OpenTyrian.Core.Tests;

internal static class LevelEventDispatchTests
{
    public static void Run(string? executable = null)
    {
        var definitions = new ItemDefinitions();
        definitions.ParseBundle(Encoding.Latin1.GetBytes(LevelEventTests.Fixture()), "dispatch.tyrr");
        var items = ItemDatabase.FromDefinitions(definitions);
        var (expected, expectedErrors) = Execute(items);
        TestInvalidActions(items);
        if (executable is null) return;
        var path = Path.Combine(Path.GetTempPath(), $"opentyrian-dispatch-{Guid.NewGuid():N}.tyrr");
        try
        {
            File.WriteAllBytes(path, Encoding.Latin1.GetBytes(LevelEventTests.Fixture()));
            var start = new ProcessStartInfo(executable) { RedirectStandardOutput = true, RedirectStandardError = true };
            start.ArgumentList.Add(path);
            using var process = Process.Start(start) ?? throw new InvalidOperationException("Cannot start event dispatch oracle.");
            var output = process.StandardOutput.ReadToEndAsync();
            var errors = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(30000))
            {
                process.Kill(entireProcessTree: true);
                throw new TimeoutException("Event dispatch oracle timed out.");
            }
            var actual = EnemySystemTests.Normalize(output.GetAwaiter().GetResult().Replace("\r\n", "\n", StringComparison.Ordinal));
            var reference = EnemySystemTests.Normalize(expected);
            var first = Enumerable.Range(0, Math.Min(actual.Length, reference.Length)).FirstOrDefault(i => actual[i] != reference[i], -1);
            var actualErrors = errors.GetAwaiter().GetResult().Replace("\r\n", "\n", StringComparison.Ordinal);
            if (process.ExitCode != 0 || !actual.SequenceEqual(reference) || actualErrors != expectedErrors)
                throw new InvalidOperationException($"Event dispatch parity differs at line {first}: {(first < 0 ? "" : actual[first] + " / " + reference[first])}; {actualErrors}");
        }
        finally { File.Delete(path); }
    }

    private static (string Output, string Errors) Execute(ItemDatabase items)
    {
        using var warnings = new StringWriter();
        var output = new StringBuilder();
        MersenneTwister.Seed(12345);
        for (var type = 0; type <= 83; type++)
        {
            for (var mode = 0; mode < 4; mode++)
            {
                if (items.Enemies[0].Armor != 0 || items.Enemies[0].EGraphic[0] != 0)
                {
                    var fresh = new ItemDefinitions();
                    fresh.ParseBundle(Encoding.Latin1.GetBytes(LevelEventTests.Fixture()), "dispatch.tyrr");
                    items = ItemDatabase.FromDefinitions(fresh);
                }
                var enemies = new EnemySystem(items, warnings);
                new byte[] { 3, 7, 9, 12 }.CopyTo(enemies.SpriteIds, 0);
                for (var i = 0; i < 100; i++)
                {
                    var enemy = enemies.Enemies[i];
                    enemy.Availability = (byte)(i % 3); enemy.LinkNumber = (byte)(i % 5 + 1);
                    enemy.YSpeed = (sbyte)(mode == 1 ? 1 : 0); enemy.Value = 20;
                    enemy.Cycle = 1; enemy.Graphics[0] = 1;
                }
                enemies.GlobalFlags[0] = mode % 2 != 0;
                enemies.GalagaShotFrequency = (ushort)(mode == 3 ? 10 : 0); enemies.TwoPlayers = mode % 2 != 0;
                enemies.Difficulty = (sbyte)(mode + 1);
                var players = new[] { new PlayerState(0) { Lives = 11 }, new PlayerState(1) };
                var effects = new SceneEffects(new byte[8]);
                var backgrounds = new BackgroundRenderer();
                backgrounds.First.MapOffset = backgrounds.Second.MapOffset = 100;
                var hud = new IndexedSurface(320, 200, 320, warnings);
                var combat = new PlayerCombat(players, effects, hud) { MusicVolume = 100 };
                var shots = new PlayerShots(items.Shots, items.Weapons, players, effects.CreateProjectileEffects(hud,
                    new SpriteSheet(), new SpriteSheet(), new SpriteTable()), enemies.Enemies, effects.SoundQueue);
                Array.Fill(shots.MultiPosition, (byte)7); Array.Fill(shots.Repeat, (byte)8);
                var events = new LevelEvents(items, enemies, players, backgrounds, effects)
                {
                    Events = Enumerable.Range(0, 32).Select(i => new LevelEvent { Time = (ushort)(i * 10) }).ToArray(),
                    MapX = 1,
                    MapX3 = 2,
                    ReturnLocation = 10,
                    SecondWrap = 90,
                    SecondWrapTo = 80,
                    AllPlayersGone = mode == 3,
                    FirstGameOver = mode % 2 != 0,
                    EffectsVolume = 200,
                    OnePlayerAction = mode == 2,
                    SuperTyrian = mode == 3,
                    InitialDifficulty = (sbyte)(mode + 1)
                };
                events.Events[31].Time = 65535;
                var record = events.Current;
                record.Type = (byte)type; record.Data = (short)mode; record.Data2 = (short)(mode + 2);
                record.Data3 = (sbyte)mode; record.Data4 = 1; record.Data5 = 2; record.Data6 = (sbyte)mode;
                if (type == 5) { record.Data = 21; record.Data2 = 0; record.Data3 = 26; record.Data4 = 3; }
                if (type is 6 or 7 or 10 or 12 or 15 or 17 or 18 or 23 or 32 or 56) { record.Data = 1000; record.Data2 = 120; }
                if (type == 16) record.Data = (short)(mode == 3 ? 10 : mode + 1);
                if (type is 33 or 45) record.Data = 533;
                if (type is >= 49 and <= 52) { record.Data = 2; record.Data3 = 21; record.Data6 = 5; }
                if (type is 54 or 70 or 71) record.Data = 30;
                if (type == 61) { record.Data = record.Data2 = 1; }
                if (type == 64) record.Data = (short)(mode == 3 ? 6 : mode + 1);
                if (type == 75) { record.Data = 1; record.Data2 = 5; record.Data3 = 80; record.Data4 = 2; }
                if (type == 82) record.Data = 13;
                output.Append($"dispatch:{type}:{mode}\n");
                if (type == 5)
                {
                    using var oldSheet = new MemoryStream(new byte[] { 2, 0, 0x10, 0xAE, 0x0F });
                    enemies.SpriteSheets[1].Load(oldSheet, checked((int)oldSheet.Length));
                }
                events.Dispatch(new Services(enemies, output), shots, combat, warnings);
                if (type == 5)
                {
                    if (enemies.SpriteSheets[1].Size != 0) throw new InvalidOperationException("Event did not clear the unloaded shape bank.");
                    foreach (var index in new[] { 0, 2, 3 })
                    {
                        hud.Pixels.Span.Clear();
                        enemies.SpriteSheets[index].Blit(hud, 0, 0, 1);
                        if (hud.Pixels.Span[0] != 0xAE) throw new InvalidOperationException("Event shape loading did not integrate with rendering.");
                    }
                }
                output.Append($"record:{record.Time},{record.Type},{record.Data},{record.Data2},{record.Data3},{record.Data4},{record.Data5},{record.Data6}\n");
                Dump(events, enemies, items, players, effects, backgrounds, shots, combat, output);
            }
        }
        output.Append($"dispatchrng:{MersenneTwister.NextUInt32()}\n");
        return (output.ToString(), warnings.ToString());
    }

    private static int Flag(bool value) => value ? 1 : 0;

    private static void TestInvalidActions(ItemDatabase items)
    {
        using var warnings = new StringWriter();
        var enemies = new EnemySystem(items, warnings);
        var players = new[] { new PlayerState(0), new PlayerState(1) };
        var backgrounds = new BackgroundRenderer();
        var effects = new SceneEffects(new byte[8]);
        var surface = new IndexedSurface(320, 200, 320, warnings);
        var combat = new PlayerCombat(players, effects, surface);
        var shots = new PlayerShots(items.Shots, items.Weapons, players, effects.CreateProjectileEffects(surface,
            new SpriteSheet(), new SpriteSheet(), new SpriteTable()), enemies.Enemies, effects.SoundQueue);
        var record = new LevelEvent();
        var events = new LevelEvents(items, enemies, players, backgrounds, effects) { Events = [record] };
        var services = new Services(enemies, new StringBuilder());
        foreach (var (type, data) in new (byte, short)[] { (5, 35), (16, 0), (61, 11), (64, 10), (82, 32000) })
        {
            record.Type = type; record.Data = data;
            ThrowsInvalid(() => events.Dispatch(services, shots, combat, warnings));
            if (events.EventLocation != 1) throw new InvalidOperationException("Failed event advanced the dispatcher cursor.");
        }
        record.Type = 75; record.Data = record.Data2 = 2; record.Data3 = 80; record.Data4 = 0;
        enemies.Enemies[0].Availability = enemies.Enemies[1].Availability = 0;
        enemies.Enemies[0].LinkNumber = enemies.Enemies[1].LinkNumber = 2;
        enemies.Enemies[0].YSpeed = 0; enemies.Enemies[1].YSpeed = 1;
        ThrowsInvalid(() => events.Dispatch(services, shots, combat, warnings));
        enemies.Enemies[0].YSpeed = 1; record.Data4 = 1;
        ThrowsInvalid(() => events.Dispatch(services, shots, combat, warnings));
    }

    private static void ThrowsInvalid(Action action)
    {
        try { action(); }
        catch (InvalidDataException) { return; }
        throw new InvalidOperationException("Invalid event did not report a data error.");
    }

    private static void Dump(LevelEvents events, EnemySystem enemies, ItemDatabase items, PlayerState[] players,
        SceneEffects effects, BackgroundRenderer backgrounds, PlayerShots shots, PlayerCombat combat, StringBuilder output)
    {
        output.Append($"cursor:{events.Location},{events.ReturnLocation},{events.EventLocation}\n");
        output.Append($"state:{events.StarfieldSpeed},{Flag(events.StopBackgrounds)},{events.StopBackgroundNumber},{Flag(events.StarsActive)},{Flag(events.EnemiesActive)},{Flag(events.AllPlayersGone)},{Flag(events.ReallyEndLevel)},{Flag(events.ReadyToEndLevel)},{Flag(events.EndLevel)},{Flag(events.FirstGameOver)},{combat.LevelEnd},{Flag(events.MusicFade)},{Flag(events.FilterActive)},{events.DamageRate},{events.EnemyFrequency},{combat.TemporaryVolume},{events.SecondOver},{events.ThirdOver},{Flag(events.SecondNotTransparent)},{Flag(events.ForceEvents)},{enemies.Enemy254Jump},{Flag(enemies.ContinualDamage)},{Flag(events.ReturnActive)}\n");
        output.Append($"render:{Flag(events.ThirdAtFirstX)},{Flag(events.ThirdAtFirstXB)},{Flag(events.TopEnemyOver)},{Flag(events.SmallEnemyAdjust)},{Flag(events.SkyOverAll)},{Flag(backgrounds.FilterFade)},{Flag(backgrounds.FilterFadeStart)},{backgrounds.LevelFilter},{backgrounds.Brightness},{backgrounds.NewLevelFilter}\n");
        output.Append($"filterchange:{backgrounds.BrightnessChange}\n");
        output.Append($"timer:{Flag(combat.LevelTimer)},{events.TimerCountdown},{events.TimerJump},{Flag(events.RandomExplosions)}\n");
        output.Append($"scroll:{events.FirstDelay},{events.FirstDelayMax},{backgrounds.SecondDelay},{backgrounds.SecondDelayMax},{backgrounds.First.Move},{backgrounds.Second.Move},{backgrounds.Third.Move},{effects.ExplosionMove},{events.MapX},{events.MapX3}\n");
        output.Append($"maps:{backgrounds.First.MapOffset},{backgrounds.Second.MapOffset},{events.SecondWrap},{events.SecondWrapTo}\n");
        output.Append($"difficulty:{enemies.Difficulty},{events.InitialDifficulty}\n");
        output.Append($"player:{players[0].InvulnerableTicks},{players[1].InvulnerableTicks},{players[0].Items.Special}\n");
        output.Append($"boss:{enemies.BossBars[0].LinkNumber},{enemies.BossBars[1].LinkNumber},{enemies.GalagaShotFrequency}\n");
        output.Append($"sound:{string.Join(',', effects.SoundQueue)}\n");
        output.Append($"sheets:{string.Join(',', enemies.SpriteIds)}\n");
        output.Append($"smooth:{string.Join(',', Enumerable.Range(0, 9).Select(i => $"{Flag(backgrounds.Smoothies[i])},{backgrounds.SmoothieData[i]}"))}\n");
        output.Append($"links:{string.Join(',', events.NewLinks)}\n");
        output.Append($"shots:{string.Join(',', Enumerable.Range(0, 11).Select(i => $"{shots.MultiPosition[i]},{shots.Repeat[i]}"))}\n");
        output.Append($"zero:{items.Enemies[0].Armor},{items.Enemies[0].EGraphic[0]}\n");
        for (var i = 0; i < 100; i++)
        {
            output.Append($"slot:{i}\n");
            EnemySystemTests.Dump(enemies, items, enemies.Enemies[i], output);
        }
    }

    private sealed class Services(EnemySystem enemies, StringBuilder output) : ILevelEventServices
    {
        public void LoadEnemySheet(SpriteSheet sheet, char file)
        {
            output.Append($"load:{Array.IndexOf(enemies.SpriteSheets, sheet)}:{file}\n");
            using var data = new MemoryStream(new byte[] { 2, 0, 0x10, 0xAE, 0x0F });
            sheet.Load(data, checked((int)data.Length));
        }
        public void DrawTextWindow(int index) => output.Append($"text:{index}\n");
        public void PlaySong(int index) => output.Append($"song:{index}\n");
        public void SetVolume(ushort music, ushort effects) => output.Append($"volume:{music},{effects}\n");
    }
}
