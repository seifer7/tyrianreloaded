using System.Diagnostics;
using System.Buffers.Binary;
using System.Text;
using OpenTyrian.Core;

namespace OpenTyrian.Core.Tests;

internal static class PlayerMovementTests
{
    public static void Run(string? executable = null)
    {
        var fixture = LevelEventTests.Fixture() +
            "[shot:147]\nmulti=1\nmax=1\ndel=5\nsg=7\nsy=2\nsx=3\nshotrepeat=4\n" +
            "[shot:148]\nmulti=1\nmax=1\ndel=5\nsg=8\nsy=2\nsx=3\nshotrepeat=4\n";
        // Known item IDs are resolved by the database, not by enum ordinals.
        var definitions = new ItemDefinitions();
        definitions.ParseBundle(Encoding.Latin1.GetBytes(fixture), "motion.tyrr");
        var items = ItemDatabase.FromDefinitions(definitions);
        var rear = items.Weapons[items.KnownIndex(KnownItem.WeaponRearNortshipSpreader)];
        var front = items.Weapons[items.KnownIndex(KnownItem.WeaponFrontNortshipSuperPulse)];
        var sidekickId = items.Sidekicks[items.KnownIndex(KnownItem.SidekickCompanionShipQuicksilver)].Id;
        var shotIds = string.Join(' ', Enumerable.Repeat("147", 11));
        fixture += $"[weapon:{rear.Id}]\nlinkgunweapon=147\nop0={shotIds}\nop1={shotIds}\n" +
            $"[weapon:{front.Id}]\nop0={shotIds}\nop1={shotIds}\n" +
            $"[weapon:{rear.Id}]\nopnum=2\n" +
            $"[weapon:{rear.Id}]\nchargegunweapons=147 148 147 148 147\n" +
            $"[sidekick:{sidekickId}]\nwport={front.Id}\nwpnum=147\noption=2\nani=3\npwr=4\n" +
            "gr0=3 4 5\ngr1=6 7 8\ngr2=9 10 11\ngr3=12 13 14\ngr4=15 16 17\n";
        fixture += string.Concat(items.Shots.Skip(items.KnownIndex(KnownItem.Shot147)).Take(4).Select(shot =>
            $"[shot:{shot.Id}]\nmulti=1\nmax=1\ndel=5\nsg=7\nsy=2\nsx=3\nshotrepeat=4\n"));
        fixture += string.Concat(new[] { 7, 8, 9, 10, 11, 12, 13, 37, 6, 15, 40, 16, 14, 41 }.Select(id => $"[special:{id}]\n"));
        fixture += string.Concat(Enumerable.Range(0, 680).Select(i =>
            $"[shot:raw{i}]\nmulti=1\nmax=1\ndel=5\nsg=9\nsy=2\nsx=3\nshotrepeat=4\n"));
        definitions = new(); definitions.ParseBundle(Encoding.Latin1.GetBytes(fixture), "motion.tyrr");
        items = ItemDatabase.FromDefinitions(definitions);
        TestInvalidInput(items);
        TestShipPixels(items);
        var trace = new StringBuilder();
        for (var mode = 0; mode < 4096; mode++)
        {
            var players = new[] { new PlayerState(0), new PlayerState(1) };
            for (var i = 0; i < 2; i++)
            {
                var player = players[i];
                player.IsAlive = (mode & 1024) == 0; player.X = 80 + i * (mode % 9); player.Y = 60 + i * (mode % 11);
                player.XVelocity = mode % 13 - 6; player.YVelocity = mode % 17 - 8;
                player.XFrictionTicks = (uint)(mode % 3); player.YFrictionTicks = (uint)(mode % 4);
                player.Items.Weapons[1].Id = checked((byte)items.KnownIndex(KnownItem.WeaponRearNortshipSpreader));
                player.Items.Weapons[0].Id = (mode & 64) != 0 ? (byte)0 :
                    checked((byte)items.KnownIndex(KnownItem.WeaponFrontNortshipSuperPulse));
                player.Items.Weapons[0].Power = player.Items.Weapons[1].Power = (byte)(mode % 11 + 1);
                player.WeaponMode = (uint)(mode % 2 + 1); player.Superbombs = (uint)(mode % 3);
                for (var j = 0; j < 20; j++) { player.OldX[j] = j * 3; player.OldY[j] = j * 2; }
                for (var j = 0; j < 2; j++)
                {
                    player.Sidekicks[j].Style = (uint)(mode / (j != 0 ? 5 : 1) % 5);
                    player.Sidekicks[j].X = 100 + j * 15; player.Sidekicks[j].Y = 20 + j * 25;
                    player.Items.Sidekicks[j] = checked((byte)items.KnownIndex(KnownItem.SidekickCompanionShipQuicksilver));
                    player.Sidekicks[j].AmmoMax = (mode & 64) != 0 ? 25 : 0; player.Sidekicks[j].Ammo = mode % 26;
                    player.Sidekicks[j].AmmoRefillTicks = (uint)(mode % 3); player.Sidekicks[j].AmmoRefillTicksMax = 7;
                    player.Sidekicks[j].Charge = (uint)(mode % 4); player.Sidekicks[j].ChargeTicks = (uint)(mode % 3);
                    player.Sidekicks[j].AnimationFrame = (uint)(mode % 3); player.Sidekicks[j].AnimationEnabled = (mode & 128) != 0;
                }
            }
            var effects = new SceneEffects(new byte[8]);
            var combat = new PlayerCombat(players, effects, new(320, 200, 320, TextWriter.Null))
            {
                TwoPlayers = (mode & 1) != 0,
                Linked = (mode & 2) != 0,
                Galaga = (mode & 4) != 0,
                LevelEnd = (byte)(mode % 43)
            };
            var level = new LevelEvents(items, new(items, TextWriter.Null), players, new(), effects) { EndLevel = (mode & 16) != 0 };
            var shots = new PlayerShots(items.Shots, items.Weapons, players, new ShotEffects()) { Power = 100 };
            var motion = new PlayerMovement(players, combat, level, shots, items)
            {
                MoveAllowed = (mode & 8) != 0,
                SecondShipGraphic = (ushort)((mode & 32) != 0 ? 3 : 0),
                LinkGunDirection = mode % 101 * 0.1f - 1,
                LevelEndWarp = (sbyte)(mode % 7 - 2),
                SatelliteRotation = mode % 43 * 0.2f,
                AttachmentLinked = (mode & 64) != 0,
                AttachmentReturn = (mode & 128) != 0,
                AttachmentMove = (short)(mode % 31 - 20)
            };
            var number = (byte)(mode % 2 + 1); var current = players[number - 1];
            var oldX = (ushort)(current.X + ((mode & 32) != 0 ? 3 : 0));
            var oldY = (ushort)(current.Y + ((mode & 64) != 0 ? 4 : 0));
            var graphic = (ushort)(mode % 3);
            var input = new PlayerMovementState
            {
                AccelerationX = (short)(mode % 15 - 7),
                AccelerationY = (short)(mode % 19 - 9),
                AnalogLinkGun = (mode & 2048) != 0,
                LinkGunAngle = mode % 79 * 0.1f - 1
            };
            var buttons = Enumerable.Range(0, 4).Select(i => ((mode >> (8 + i)) & 1) != 0).ToArray();
            shots.Repeat[1] = (byte)((mode & 128) != 0 ? 2 : 0);
            shots.Repeat[6] = shots.Repeat[7] = (byte)((mode & 512) != 0 ? 2 : 0);
            shots.Repeat[2] = shots.Repeat[3] = (byte)((mode & 256) != 0 ? 2 : 0);
            shots.Repeat[5] = (byte)((mode & 1024) != 0 ? 2 : 0);
            shots.MultiPosition[8] = shots.MultiPosition[10] = 3;
            var arcadeMode = (byte)(mode % 10);
            var specialId = arcadeMode is >= 1 and <= 7 ? arcadeMode + 6 : 7;
            players[0].Items.Special = (mode & 32) != 0 ? checked((byte)items.Index(ItemType.Special, specialId)) : (byte)0;
            trace.AppendLine($"case:{mode}");
            motion.UpdateLinking(current, number, oldX, oldY, input, buttons,
                (n, x, y, ox, oy) => trace.AppendLine($"codes:{n},{x},{y},{ox},{oy}"));
            motion.DrawLevelEnd(current, graphic, (x, y, g) => trace.AppendLine($"trail:{x},{y},{g}"));
            motion.UpdateMovement(current, number, graphic, oldX, oldY, input, buttons);
            motion.UpdateSidekickPositions(current, buttons);
            foreach (var explosion in effects.Explosions.Where(e => e.Lifetime != 0))
                trace.AppendLine($"E:{explosion.X}:{explosion.Y}:0");
            var weapons = new PlayerWeapons(shots, items, combat)
            {
                ChargeLevel = (byte)(mode % 6),
                ChargeWait = (byte)(mode % 7),
                ChargeMaximum = 5,
                ChargeGraphic = (byte)(mode % 4),
                ChargeGraphicWait = (byte)(mode % 5),
                ModeChangeRequested = (mode & 256) != 0,
                ModeChangeReady = (mode & 128) != 0
            };
            var hud = new IndexedSurface(320, 200, 320, TextWriter.Null); hud.Pixels.Span.Fill(69);
            weapons.ChangeMode(current, buttons, arcadeMode,
                () => weapons.DrawModeButtons((x, y, frame) => trace.AppendLine($"P:{x},{y},{frame}")));
            weapons.FireMain(current, number, oldX, oldY, buttons);
            weapons.FireSuperbomb(current, number, oldX, oldY, buttons);
            weapons.UpdateCharge(current, number, graphic, oldX, oldY, buttons, (sbyte)(mode % 11), hud,
                (x, y, frame) => trace.AppendLine($"D:10:{x}:{y}:{frame}:0"));
            weapons.FireSidekicks(current, number, oldX, oldY, buttons, hud);
            weapons.DrawSidekicks(current, number, level.EndLevel, (x, y, frame, large) =>
                trace.AppendLine(large ? $"trail:{x},{y},{frame}" : $"D:9:{x}:{y}:{frame}:0"));
            trace.AppendLine($"state:{Bit(combat.Linked)},{Bit(level.ReallyEndLevel)},{BitConverter.SingleToUInt32Bits(motion.LinkGunDirection)}," +
                $"{BitConverter.SingleToUInt32Bits(motion.SatelliteRotation)},{Bit(motion.AttachmentLinked)},{Bit(motion.AttachmentReturn)},{motion.AttachmentMove}");
            trace.AppendLine($"player:{current.X},{current.Y},{current.XVelocity},{current.YVelocity},{current.XFrictionTicks},{current.YFrictionTicks}");
            foreach (var sidekick in current.Sidekicks) trace.AppendLine($"sidekick:{sidekick.X},{sidekick.Y}");
            trace.AppendLine("history:" + string.Concat(current.OldX.Zip(current.OldY, (x, y) => $"{x},{y},")));
            trace.AppendLine($"shots:{shots.MultiPosition[4]},{shots.Repeat[1]},{effects.SoundQueue[2]},{effects.SoundQueue[3]},{shots.Power}," +
                $"{shots.Repeat[0]},{shots.MultiPosition[0]},{shots.MultiPosition[1]},{players[0].Superbombs},{players[1].Superbombs}," +
                $"{shots.Repeat[6]},{shots.Repeat[7]}");
            trace.AppendLine($"charge:{weapons.ChargeLevel},{weapons.ChargeWait},{weapons.ChargeGraphic},{weapons.ChargeGraphicWait}," +
                $"{shots.Repeat[2]},{shots.Repeat[3]},{shots.Repeat[5]}");
            trace.AppendLine($"mode:{current.WeaponMode},{players[0].Items.Special},{Bit(weapons.ModeChangeRequested)}," +
                $"{Bit(weapons.ModeChangeReady)},{shots.MultiPosition[8]},{shots.MultiPosition[10]}");
            foreach (var sidekick in current.Sidekicks)
                trace.AppendLine($"ammo:{sidekick.Ammo},{sidekick.AmmoRefillTicks},{sidekick.Charge},{sidekick.ChargeTicks}," +
                    $"{Bit(sidekick.AnimationEnabled)},{sidekick.AnimationFrame}");
            uint hash = 2166136261;
            foreach (var pixel in hud.Pixels.Span) hash = unchecked((hash ^ pixel) * 16777619);
            trace.AppendLine($"hud:{hash}");
            for (var i = 0; i < 10; i++)
            {
                var shot = shots.Shots[i];
                trace.AppendLine($"shot:{shots.Availability[i]},{shot.X},{shot.Y},{shot.XM},{shot.YM},{shot.Graphic}");
            }
        }
        AppendShipTraces(items, trace);
        AppendSpecialTraces(items, trace);
        if (executable == null) return;
        var path = Path.Combine(Path.GetTempPath(), $"opentyrian-motion-{Guid.NewGuid():N}.tyrr");
        try
        {
            File.WriteAllBytes(path, Encoding.Latin1.GetBytes(fixture));
            var start = new ProcessStartInfo(executable) { RedirectStandardOutput = true, RedirectStandardError = true };
            start.ArgumentList.Add(path);
            using var process = Process.Start(start) ?? throw new InvalidOperationException("Cannot start player movement oracle.");
            var output = process.StandardOutput.ReadToEndAsync(); var errors = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(60000)) { process.Kill(entireProcessTree: true); throw new TimeoutException("Player movement oracle timed out."); }
            var actual = trace.ToString().Replace("\r\n", "\n", StringComparison.Ordinal);
            var expected = output.GetAwaiter().GetResult().Replace("\r\n", "\n", StringComparison.Ordinal);
            if (process.ExitCode != 0 || actual != expected)
            {
                var a = expected.Split('\n'); var b = actual.Split('\n');
                var index = Enumerable.Range(0, Math.Min(a.Length, b.Length)).FirstOrDefault(i => a[i] != b[i], -1);
                throw new InvalidOperationException($"Player movement differs at {index}: {a.ElementAtOrDefault(index)} / {b.ElementAtOrDefault(index)}; {errors.GetAwaiter().GetResult()}");
            }
        }
        finally { File.Delete(path); }
    }
    private static void AppendSpecialTraces(ItemDatabase items, StringBuilder trace)
    {
        int[][] sequences =
        [
            [2,1,2,5], [4,3,2,5], [3,4,6], [2,5], [6,2,6], [6,7,5,8,6,7,5],
            [7,8], [1,7,6], [8,6,7,1], [3,6,8,5], [1,2,7,8], [3,4,3,6],
            [6,7,5,8], [1,6], [9,5], [1,7], [1,8], [9,7], [9,8], [4,2,3,5],
            [3,1,2,8], [2,4,5], [3,4,2,8], [1,4,6], [1,3,6], [1,4,3,4,7]
        ];
        for (var mode = 0; mode < 1768; mode++)
        {
            var players = new[] { new PlayerState(0), new PlayerState(1) };
            var effects = new SceneEffects(new byte[8]);
            var combat = new PlayerCombat(players, effects, new(320, 200, 320, TextWriter.Null));
            var shots = new PlayerShots(items.Shots, items.Weapons, players, new ShotEffects());
            var weapons = new PlayerWeapons(shots, items, combat);
            var level = new LevelEvents(items, new(items, TextWriter.Null), players, new(), effects);
            var motion = new PlayerMovement(players, combat, level, shots, items);
            var ship = mode / 26 % 17;
            if (ship == 14) ship = 13;
            var number = (byte)(mode / 442 % 2 + 1);
            var player = players[number - 1]; player.Items.Ship = (byte)ship;
            var sequence = sequences[mode % 26];
            for (var step = 0; step < 24; step++)
            {
                var key = sequence[step % sequence.Length]; var variant = step / sequence.Length;
                if (variant % 3 == 1 && step % 2 == 0) key = 9;
                if (variant % 3 == 2 && step % 3 == 0) key = 10;
                if (variant % 3 == 2 && step % 3 == 1) key = 11;
                bool[] buttons = [key is >= 5 and <= 8, false, false, false];
                var x = 100; var y = 100; var direction = (key - 1) % 4 + 1;
                if (key <= 8)
                {
                    if (direction == 1) y--; if (direction == 2) y++;
                    if (direction == 3) x--; if (direction == 4) x++;
                }
                if (key == 10) { x++; y++; }
                if (key == 11) buttons[0] = true;
                player.X = x; player.Y = y;
                motion.UpdateLinking(player, number, 100, 100, new(), buttons, weapons, mode / 884 != 0);
                trace.AppendLine($"code:{mode},{step},{weapons.ExecutedSpecial[number - 1]}:" +
                    string.Concat(weapons.SpecialCodeProgress[number - 1].Select(value => $"{value},")));
            }
        }
    }

    private static void TestShipPixels(ItemDatabase items)
    {
        var data = new byte[500 + 3];
        for (var i = 0; i < 250; i++) BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(i * 2), 500);
        data[500] = 0x10; data[501] = 0xBE; data[502] = 0x0F;
        var sheet = new SpriteSheet();
        using var stream = new MemoryStream(data); sheet.Load(stream, data.Length);
        for (var graphic = 0; graphic < 3; graphic++)
        {
            foreach (var invulnerable in new[] { false, true })
            {
                var surface = new IndexedSurface(320, 200, 327, TextWriter.Null);
                surface.Pixels.Span.Fill(0xA6);
                var players = new[] { new PlayerState(0), new PlayerState(1) };
                var effects = new SceneEffects(new byte[8]);
                var combat = new PlayerCombat(players, effects, surface);
                var level = new LevelEvents(items, new(items, TextWriter.Null), players, new(), effects);
                var shots = new PlayerShots(items.Shots, items.Weapons, players, new ShotEffects());
                var motion = new PlayerMovement(players, combat, level, shots, items);
                var player = players[0]; player.X = 100; player.Y = 60; player.InvulnerableTicks = invulnerable ? 2u : 0u;
                motion.DrawShip(player, 1, (ushort)graphic, 100, 60, false, true, true, 30, 10, effects, surface, sheet);
                var x = graphic < 2 ? 83 : 95;
                var expected = invulnerable ? 0xBA : 0xBE;
                if (surface.Pixels.Span[53 * 327 + x] != expected ||
                    surface.Pixels.Span[63 * 327 + x] != 0xA3 ||
                    player.InvulnerableTicks != (invulnerable ? 1u : 0u))
                    throw new InvalidOperationException("Ship sprite adapter copy/blend/shadow pixels differ.");
                for (var y = 0; y < 200; y++)
                    if (surface.Pixels.Span.Slice(y * 327 + 320, 7).IndexOfAnyExcept((byte)0xA6) >= 0)
                        throw new InvalidOperationException("Ship rendering changed surface padding.");
            }
        }
    }

    private static void AppendShipTraces(ItemDatabase items, StringBuilder trace)
    {
        for (var mode = 0; mode < 2048; mode++)
        {
            MersenneTwister.Seed((uint)(mode + 19));
            var players = new[] { new PlayerState(0), new PlayerState(1) };
            var effects = new SceneEffects(new byte[8]) { FollowX = -12, FollowY = -13 };
            var combat = new PlayerCombat(players, effects, new(320, 200, 320, TextWriter.Null));
            var level = new LevelEvents(items, new(items, TextWriter.Null), players, new(), effects) { EndLevel = (mode & 1) != 0 };
            var shots = new PlayerShots(items.Shots, items.Weapons, players, new ShotEffects()) { Power = 100 };
            var motion = new PlayerMovement(players, combat, level, shots, items) { ConstantLastX = (sbyte)(mode % 255 - 128) };
            var number = (byte)((mode & 16) != 0 ? 2 : 1);
            var player = players[number - 1];
            player.X = mode % 301; player.Y = mode % 201 - 20; player.XVelocity = mode % 13 - 6;
            player.LastXExplosionFollow = mode % 310; player.LastYExplosionFollow = mode % 181;
            player.InvulnerableTicks = (mode & 32) != 0 ? 3u : 0u;
            player.Sidekicks[0].Style = (uint)(mode % 5); player.Sidekicks[1].Style = (uint)(mode / 5 % 5);
            player.Sidekicks[0].X = 1; player.Sidekicks[0].Y = 2; player.Sidekicks[1].X = 3; player.Sidekicks[1].Y = 4;
            shots.Repeat[9] = (byte)((mode & 64) != 0 ? 2 : 0);
            trace.AppendLine($"shipcase:{mode}");
            motion.DrawShip(player, number, (ushort)(mode % 3), (ushort)(mode % 290), (ushort)(mode % 190),
                (mode & 2) != 0, (mode & 4) != 0, (mode & 8) != 0, mode % 37 - 18, 10,
                effects, (x, y, frame, large, effect) => trace.AppendLine(large
                    ? $"ship:{x},{y},{frame},1,{(int)effect}" : $"D:9:{x}:{y}:{frame}:0"));
            trace.AppendLine($"shipstate:{player.X},{player.Y},{motion.ConstantLastX},{player.InvulnerableTicks}," +
                $"{effects.FollowX},{effects.FollowY},{player.LastXExplosionFollow},{player.LastYExplosionFollow}," +
                $"{shots.Repeat[9]},{shots.MultiPosition[9]},{MersenneTwister.NextUInt32()}");
            trace.AppendLine($"shipoptions:{player.Sidekicks[0].X},{player.Sidekicks[0].Y},{player.Sidekicks[1].X},{player.Sidekicks[1].Y}");
            for (var i = 0; i < 2; i++)
            {
                var shot = shots.Shots[i];
                trace.AppendLine($"spark:{shots.Availability[i]},{shot.X},{shot.Y},{shot.XM},{shot.YM},{shot.Graphic}");
            }
        }
    }

    private static void TestInvalidInput(ItemDatabase items)
    {
        var players = new[] { new PlayerState(0), new PlayerState(1) };
        var hud = new IndexedSurface(320, 200, 320, TextWriter.Null);
        var combat = new PlayerCombat(players, new(new byte[8]), hud);
        var shots = new PlayerShots(items.Shots, items.Weapons, players, new ShotEffects());
        var weapons = new PlayerWeapons(shots, items, combat);
        var player = players[1];
        bool[] buttons = [true, true, true, true];
        Throws<ArgumentException>(() => weapons.ChangeMode(player, [true], 0, () => { }));
        players[0].Items.Ship = 14;
        Throws<InvalidDataException>(() => weapons.RecognizeSpecial(1, 100, 99, 100, 100, buttons, false));
        players[0].Items.Ship = 1; weapons.SpecialCodeProgress[0][0] = 7;
        Throws<InvalidDataException>(() => weapons.RecognizeSpecial(1, 100, 99, 100, 100, buttons, false));
        Throws<ArgumentException>(() => weapons.FireMain(player, 2, 0, 0, [true]));
        Throws<ArgumentOutOfRangeException>(() => weapons.DrawSidekicks(player, 0, false, (_, _, _, _) => { }));
        player.Items.Weapons[1].Id = checked((byte)items.KnownIndex(KnownItem.WeaponRearNortshipSpreader));
        player.Items.Weapons[1].Power = 0; player.WeaponMode = 1;
        Throws<InvalidDataException>(() => weapons.FireMain(player, 2, 0, 0, buttons));
        player.Items.Weapons[1].Power = 1; player.WeaponMode = 3;
        Throws<InvalidDataException>(() => weapons.FireMain(player, 2, 0, 0, buttons));
        weapons.ChargeLevel = 6;
        Throws<InvalidDataException>(() => weapons.UpdateCharge(player, 2, 0, 0, 0, buttons, 0, hud, (_, _, _) => { }));
        weapons.ChargeLevel = 5; weapons.ChargeMaximum = 6;
        Throws<InvalidDataException>(() => weapons.UpdateCharge(player, 2, 0, 0, 0, buttons, 0, hud, (_, _, _) => { }));
        player.Items.Sidekicks[0] = checked((byte)items.KnownIndex(KnownItem.SidekickCompanionShipQuicksilver));
        player.Sidekicks[0].Ammo = -1; player.Sidekicks[0].AmmoMax = 25;
        player.Sidekicks[0].AmmoRefillTicks = 0;
        Throws<InvalidDataException>(() => weapons.FireSidekicks(player, 2, 0, 0, buttons, hud));
        player.Sidekicks[0].Charge = 5;
        Throws<InvalidDataException>(() => weapons.DrawSidekicks(player, 2, false, (_, _, _, _) => { }));
        player.Sidekicks[0].Charge = 0; player.Sidekicks[0].AnimationFrame = 20;
        Throws<InvalidDataException>(() => weapons.DrawSidekicks(player, 2, false, (_, _, _, _) => { }));
    }
    private static void Throws<T>(Action action) where T : Exception
    {
        try { action(); }
        catch (T) { return; }
        throw new InvalidOperationException($"Expected {typeof(T).Name}.");
    }
    private static int Bit(bool value) => value ? 1 : 0;
    private sealed class ShotEffects : IPlayerShotEffects
    {
        public void Draw(int sheet, int x, int y, int frame, bool darken) { }
        public (ushort Width, ushort Height) DrawSpecial(int x, int y, int frame) => throw new InvalidOperationException("Unexpected special-shot rendering.");
        public void Explosion(int x, int y, byte type) => throw new InvalidOperationException("Unexpected shot movement.");
        public void SuperPixels(int x, int y, ushort count, byte width, byte color) => throw new InvalidOperationException("Unexpected shot movement.");
    }
}
