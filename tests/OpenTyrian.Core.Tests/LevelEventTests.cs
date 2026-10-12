using System.Diagnostics;
using System.Text;
using OpenTyrian.Core;

namespace OpenTyrian.Core.Tests;

internal static class LevelEventTests
{
    public static void Run(string? executable = null)
    {
        var definitions = new ItemDefinitions();
        definitions.ParseBundle(Encoding.Latin1.GetBytes(Fixture()), "events.tyrr");
        var items = ItemDatabase.FromDefinitions(definitions);
        var expected = Execute(items);
        using var warnings = new StringWriter();
        var enemy = Create(items, warnings);
        var players = new[] { new PlayerState(0), new PlayerState(1) };
        var events = new LevelEvents(items, enemy, players, new BackgroundRenderer(), new SceneEffects(new byte[8]));
        try
        {
            events.Jump(100);
            throw new InvalidOperationException("Jump past the event array accepted.");
        }
        catch (InvalidDataException) { }
        events.Events = [new() { Type = 23, Data = 1000, Data2 = 120, Data5 = -5 }];
        events.MapX = 1;
        events.HandleEnemyEvent();
        Check(enemy.Enemies[50].Y == 175 && enemy.Enemies[50].Availability == 0, "Enemy event group did not integrate event spawning.");
        if (executable is null) return;
        var path = Path.Combine(Path.GetTempPath(), $"opentyrian-events-{Guid.NewGuid():N}.tyrr");
        try
        {
            File.WriteAllBytes(path, Encoding.Latin1.GetBytes(Fixture()));
            var start = new ProcessStartInfo(executable) { RedirectStandardOutput = true, RedirectStandardError = true };
            start.ArgumentList.Add(path);
            using var process = Process.Start(start) ?? throw new InvalidOperationException("Cannot start level event oracle.");
            var output = process.StandardOutput.ReadToEndAsync();
            var errors = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(30000))
            {
                process.Kill(entireProcessTree: true);
                throw new TimeoutException("Level event oracle timed out.");
            }
            var actual = EnemySystemTests.Normalize(output.GetAwaiter().GetResult().Replace("\r\n", "\n", StringComparison.Ordinal));
            var reference = EnemySystemTests.Normalize(expected);
            var first = Enumerable.Range(0, Math.Min(actual.Length, reference.Length)).FirstOrDefault(i => actual[i] != reference[i], -1);
            Check(process.ExitCode == 0 && actual.SequenceEqual(reference) && errors.GetAwaiter().GetResult().Length == 0,
                $"Level enemy event parity differs at line {first}: {(first < 0 ? "" : actual[first] + " / " + reference[first])}; {errors.Result}");
        }
        finally { File.Delete(path); }
    }

    internal static string Fixture()
    {
        var output = new StringBuilder(EnemySystemTests.Fixture());
        output.Append("[enemy:0]\narmor=0\negraphic=0\n");
        for (var id = 1000; id <= 1003; id++)
            output.Append($"[enemy:{id}]\nshapebank=3\narmor=10\nvalue=20\nstartx=40\nstarty=50\nstartxc=3\nstartyc=-2\n" +
                $"esize={id % 2}\nymove=2\nani=1\negraphic=1\n");
        foreach (var id in new[] { 512, 513, 827, 828, 829, 830, 831, 832, 833, 834 })
            output.Append($"[enemy:{id}]\nshapebank=3\narmor=0\nvalue=1\n");
        return output.ToString();
    }

    private static EnemySystem Create(ItemDatabase items, TextWriter warnings)
    {
        var enemy = new EnemySystem(items, warnings);
        new byte[] { 3, 7, 9, 12 }.CopyTo(enemy.SpriteIds, 0);
        return enemy;
    }

    private static string Execute(ItemDatabase items)
    {
        using var warnings = new StringWriter();
        var output = new StringBuilder();
        var players = new[] { new PlayerState(0), new PlayerState(1) };
        MersenneTwister.Seed(12345);
        for (var mode = 0; mode < 64; mode++)
        {
            var enemy = Create(items, warnings);
            var record = new LevelEvent
            {
                Data = 1000,
                Data2 = (short)(mode % 3 == 0 ? -99 : mode % 3 == 1 ? 120 : 32767),
                Data3 = (sbyte)(mode % 2 != 0 ? 127 : -128),
                Data4 = 20,
                Data5 = (sbyte)(mode % 2 != 0 ? 5 : -5),
                Data6 = -2
            };
            var events = new LevelEvents(items, enemy, players, new BackgroundRenderer(), new SceneEffects(new byte[8]))
            {
                Events = [record],
                MapX = (ushort)(mode % 3),
                MapX3 = (ushort)(mode % 4),
                SmallEnemyAdjust = mode % 2 != 0,
                ThirdAtFirstX = mode % 3 == 0,
                ThirdAtFirstXB = mode % 3 == 1
            };
            if (mode == 63) foreach (var slot in enemy.Enemies) slot.Availability = 0;
            var created = events.CreateEnemy((byte)(mode % 4), (ushort)(mode % 4 * 25), (short)(mode % 5 == 0 ? 21 : 0));
            output.Append($"spawn:{mode}={created}\n");
            if (created > 0) EnemySystemTests.Dump(enemy, items, enemy.Enemies[created - 1], output);
        }
        {
            var enemy = Create(items, warnings);
            var events = new LevelEvents(items, enemy, players, new BackgroundRenderer(), new SceneEffects(new byte[8]))
            {
                Events = new ushort[] { 0, 10, 20, 30, 65535 }.Select(time => new LevelEvent { Time = time }).ToArray()
            };
            ushort[] targets = [0, 5, 10, 21, 65534, 65535];
            for (var i = 0; i < targets.Length; i++)
            {
                events.Location = 65535; events.ReturnLocation = 20; events.Jump(targets[i]);
                output.Append($"jumpcase:{i}={events.Location},{events.ReturnLocation},{events.EventLocation}\n");
            }
            for (var i = 0; i < 100; i++)
            {
                enemy.Enemies[i].LinkNumber = (byte)(i % 4); enemy.Enemies[i].Value = 20000;
                enemy.Enemies[i].Availability = (byte)(i % 3);
            }
            for (var mode = 0; mode < 8; mode++)
            {
                enemy.GalagaMode = mode % 2 != 0;
                var result = events.SearchEnemy((byte)(mode % 5), out var found);
                output.Append($"search:{mode}={(result ? 1 : 0)},{(result ? found : 200)}\n");
                for (var i = 0; i < 100; i++)
                {
                    output.Append($"slot:{i}\n"); EnemySystemTests.Dump(enemy, items, enemy.Enemies[i], output);
                }
            }
        }
        LevelEvents? last = null;
        for (var type = 19; type <= 33; type++)
        {
            for (var mode = 0; mode < 20; mode++)
            {
                var enemy = Create(items, warnings);
                var backgrounds = new BackgroundRenderer { SecondDelay = 3, SecondDelayMax = 3 };
                backgrounds.First.Move = backgrounds.Second.Move = backgrounds.Third.Move = 2;
                var effects = new SceneEffects(new byte[8]) { ExplosionMove = 2 };
                enemy.GalagaMode = mode % 2 != 0; enemy.Difficulty = (sbyte)(mode % 11);
                enemy.TwoPlayers = mode % 4 == 0; enemy.SuperArcadeMode = (byte)(mode % 4 == 3 ? 1 : 0);
                players[0].Lives = (byte)(mode % 12);
                var events = new LevelEvents(items, enemy, players, backgrounds, effects)
                {
                    Events = [new()],
                    MapX = 1,
                    MapX3 = 2,
                    TopEnemyOver = true,
                    ThirdOver = 2,
                    FirstDelay = 3,
                    FirstDelayMax = 3,
                    OnePlayerAction = mode % 4 == 1,
                    SuperTyrian = mode % 4 == 2
                };
                for (var i = 0; i < 10; i++) events.NewLinks[i] = (byte)(i + 1);
                for (var i = 0; i < 100; i++)
                {
                    var e = enemy.Enemies[i]; e.Availability = (byte)(i % 3); e.LinkNumber = (byte)(i % 5);
                    e.XSpeed = 3; e.YSpeed = 4; e.XFixedAcceleration = 2; e.YFixedAcceleration = 3;
                    e.XAccelerationWait = 4; e.YAccelerationWait = 5; e.XAccelerationWaitMax = 4; e.YAccelerationWaitMax = 5;
                    e.XAccelerationAdd = e.YAccelerationAdd = 1; e.FixedMoveY = 2;
                    e.AnimationLength = 3; e.AnimationMin = 1; e.Cycle = 1; e.AnimationMax = 2;
                    e.AnimationActive = e.AnimationWhenFire = 2; e.Armor = 10;
                    e.XReverse = 5; e.YReverse = -5; e.LaunchType = (ushort)(i % 2);
                }
                var record = events.Current;
                record.Type = (byte)type; record.Data = (short)(mode % 4 == 0 ? -99 : mode % 4 == 1 ? -128 : mode % 4 == 2 ? 300 : 10);
                record.Data2 = (short)(mode % 3 == 0 ? -99 : mode % 3 == 1 ? 0 : 257);
                sbyte[] groups = [0, 1, 2, 3, 99, 80, 89, 90, -1, 5]; record.Data3 = groups[mode % 10];
                record.Data4 = (byte)(mode % 3 == 0 ? 0 : mode % 3 == 1 ? 3 : 99);
                record.Data5 = (sbyte)(mode % 3 == 0 ? -1 : mode % 3 == 1 ? 0 : 5);
                record.Data6 = (sbyte)(mode % 3 == 0 ? -99 : mode % 3 == 1 ? 0 : 5);
                if (type == 25) record.Data = 20;
                if (type is 23 or 32) { record.Data = 1000; record.Data2 = 120; record.Data3 = 1; record.Data5 = -5; }
                if (type == 33)
                {
                    record.Data = (short)(mode % 4 == 0 ? 512 : mode % 4 == 1 ? 513 : mode % 4 == 2 ? 533 : 534);
                    enemy.TwoPlayers = ((mode / 4) & 1) != 0; events.OnePlayerAction = ((mode / 4) & 2) != 0;
                    events.SuperTyrian = ((mode / 4) & 4) != 0; enemy.SuperArcadeMode = (byte)(mode % 5 == 0 ? 1 : 0);
                }
                output.Append($"control:{type}:{mode}\n");
                events.HandleEnemyEvent();
                output.Append($"event:{record.Time},{record.Type},{record.Data},{record.Data2},{record.Data3},{record.Data4},{record.Data5},{record.Data6}\n");
                output.Append($"environment:{(events.SmallEnemyAdjust ? 1 : 0)},{events.ThirdOver},{(events.TopEnemyOver ? 1 : 0)},{events.FirstDelay},{events.FirstDelayMax},{backgrounds.SecondDelay},{backgrounds.SecondDelayMax},{backgrounds.First.Move},{backgrounds.Second.Move},{backgrounds.Third.Move},{effects.ExplosionMove}\n");
                for (var i = 0; i < 100; i++)
                {
                    output.Append($"slot:{i}\n"); EnemySystemTests.Dump(enemy, items, enemy.Enemies[i], output);
                }
                last = events;
            }
        }
        for (var life = 0; life < 12; life++)
        {
            for (var i = 0; i < 8; i++)
            {
                players[0].Lives = (byte)life;
                var events = last ?? throw new InvalidOperationException();
                events.Current.Data = 533; events.ApplyRandomSpecialDrop();
                output.Append($"drop:{life}:{i}={events.Current.Data}\n");
            }
        }
        output.Append($"eventrng:{MersenneTwister.NextUInt32()}\n");
        Check(warnings.ToString().Length == 0, $"Unexpected level event warnings: {warnings}");
        return output.ToString();
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
