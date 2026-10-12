using System.Diagnostics;
using System.Buffers.Binary;
using System.Text;
using OpenTyrian.Core;

namespace OpenTyrian.Core.Tests;

internal static class PlayerSpecialWeaponsTests
{
    public static void Run(string? executable = null)
    {
        var fixture = LevelEventTests.Fixture() +
            "[shot:147]\nmulti=1\nmax=1\ndel=5\nsg=7\nsy=2\nsx=3\nshotrepeat=4\n";
        var costs = new[] { 0, 3, 98, 99, 105, 255 };
        for (var i = 0; i < 114; i++)
            fixture += $"[special:case{i}]\nstype={i / 6}\npwr={costs[i % 6]}\nwpn=147\n";
        foreach (var id in new[] { 7, 8, 9, 10, 11, 12, 13, 37, 6, 15, 40, 16, 14, 41 })
            fixture += $"[special:{id}]\nstype=1\nwpn=147\n";
        fixture += string.Concat(Enumerable.Range(0, 740).Select(i =>
            $"[shot:raw{i}]\nmulti=1\nmax=1\ndel=5\nsg=9\nsy=2\nsx=3\nshotrepeat=4\n"));
        var definitions = new ItemDefinitions(); definitions.ParseBundle(Encoding.Latin1.GetBytes(fixture), "special.tyrr");
        var items = ItemDatabase.FromDefinitions(definitions);
        var rearId = items.Weapons[items.KnownIndex(KnownItem.WeaponRearNortshipSpreader)].Id;
        var frontId = items.Weapons[items.KnownIndex(KnownItem.WeaponFrontNortshipSuperPulse)].Id;
        var sidekickId = items.Sidekicks[items.KnownIndex(KnownItem.SidekickCompanionShipQuicksilver)].Id;
        var shotIds = string.Join(' ', Enumerable.Repeat("147", 11));
        fixture += $"[weapon:{rearId}]\nop0={shotIds}\nop1={shotIds}\nopnum=2\nchargegunweapons=147 147 147 147 147\n" +
            $"[weapon:{frontId}]\nop0={shotIds}\nop1={shotIds}\n" +
            $"[sidekick:{sidekickId}]\nwport={frontId}\nwpnum=147\n";
        foreach (var option in items.Sidekicks)
            fixture += $"[sidekick:{option.Id}]\nammo={items.Index(ItemType.SidekickLeft, option.Id) % 2 * 25}\n" +
                $"tr={items.Index(ItemType.SidekickLeft, option.Id) % 5}\noption=1\nicongr=1\n";
        var sidekickShotId = items.Shots[items.KnownIndex(KnownItem.SidekickCompanionShipQuicksilver)].Id;
        for (var i = 102; i < 114; i++) fixture += $"[special:case{i}]\nwpn={sidekickShotId}\n";
        fixture += string.Concat(items.Shots.Skip(items.Index(ItemType.Shot, "147")).Take(4).Select(shot =>
            $"[shot:{shot.Id}]\nmulti=1\nmax=1\ndel=5\nsg=7\nsy=2\nsx=3\nshotrepeat=4\n"));
        definitions = new(); definitions.ParseBundle(Encoding.Latin1.GetBytes(fixture), "special.tyrr");
        items = ItemDatabase.FromDefinitions(definitions);
        TestPresentation(items);
        TestFrameFailures(items);
        var trace = new StringBuilder();
        for (var mode = 0; mode < 20480; mode++)
        {
            MersenneTwister.Seed((uint)(mode + 31));
            var game = new IndexedSurface(320, 200, 327, TextWriter.Null);
            var hud = new IndexedSurface(320, 200, 327, TextWriter.Null);
            game.Pixels.Span.Fill(166); hud.Pixels.Span.Fill(69);
            var players = new[] { new PlayerState(0), new PlayerState(1) };
            var effects = new SceneEffects(new byte[8]);
            var combat = new PlayerCombat(players, effects, hud) { TwoPlayers = (mode & 1) != 0, Galaga = (mode & 2) != 0 };
            var enemies = new EnemySystem(items, TextWriter.Null) { Difficulty = (sbyte)(mode % 11) };
            var backgrounds = new BackgroundRenderer
            { LevelFilter = (sbyte)((mode & 2048) != 0 ? 7 : -99), Brightness = (sbyte)((mode & 4096) != 0 ? -99 : mode % 19 - 9) };
            var level = new LevelEvents(items, enemies, players, backgrounds, effects) { FilterActive = (mode & 512) != 0 };
            var shots = new PlayerShots(items.Shots, items.Weapons, players, new ShotEffects(), enemies.Enemies.Take(100).ToArray(), effects.SoundQueue)
            { Power = 100 };
            Array.Fill(shots.MultiPosition, (byte)3);
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
            var motion = new PlayerMovement(players, combat, level, shots, items) { MoveAllowed = (mode & 8) != 0 };
            level.EndLevel = (mode & 16) != 0;
            var special = new PlayerSpecialWeapons(items, shots, weapons, combat, enemies, effects, level, backgrounds)
            {
                ArcadeMode = (byte)(mode % 10),
                PowerParameter = (byte)(mode % 256),
                NextWait = 17,
                Wait = (byte)((mode & 4) != 0 ? 2 : 0),
                ZinglonDuration = (byte)((mode & 8) != 0 ? mode % 51 : 0),
                AstralDuration = (byte)(mode % 3),
                FlareDuration = (ushort)((mode & 16) != 0 ? mode % 4 : 0),
                FlareStart = (mode & 32) != 0,
                LinkToPlayer = (mode & 64) != 0,
                Spray = (mode & 128) != 0,
                FireButtonHeld = (mode & 256) != 0,
                Filter = (sbyte)((mode & 1024) != 0 ? 7 : -99),
                Frequency = (sbyte)(mode % 11 - 2),
                ColorChange = -1,
                Weapon = checked((ushort)items.Index(ItemType.Shot, "147"))
            };
            for (var i = 0; i < 2; i++)
            {
                var player = players[i];
                player.X = 100 + i * 40; player.Y = 80 + i * 10; player.Armor = (uint)(mode % 30);
                player.Shield = (uint)(mode % 31); player.ShieldMax = 31;
                player.Items.Weapons[0].Power = (byte)(mode % 11 + 1);
                player.Items.Sidekicks[0] = (mode & 64) != 0 ? checked((byte)items.KnownIndex(KnownItem.SidekickCompanionShipQuicksilver)) : (byte)0;
                player.Items.SidekickLevel = (byte)(mode % 8 + 98);
                player.Items.Weapons[1].Power = (byte)(mode % 11 + 1); player.WeaponMode = (uint)(mode % 2 + 1);
                player.Items.Weapons[0].Id = checked((byte)items.KnownIndex(KnownItem.WeaponFrontNortshipSuperPulse));
                player.Items.Weapons[1].Id = checked((byte)items.KnownIndex(KnownItem.WeaponRearNortshipSpreader));
                player.IsAlive = (mode & 32) == 0; player.Superbombs = (uint)(mode % 3);
                player.LastXShotMove = 97 + i * 30; player.LastYShotMove = 78 + i * 10;
                if (mode >= 8192)
                    for (var j = 0; j < 2; j++)
                    {
                        player.Items.Sidekicks[j] = checked((byte)items.KnownIndex(KnownItem.SidekickCompanionShipQuicksilver));
                        var option = player.Sidekicks[j];
                        option.X = 90 + j * 20; option.Y = 80; option.Style = (uint)(mode % 5);
                        option.AmmoMax = 25; option.Ammo = mode % 26;
                        option.AmmoRefillTicks = (uint)(mode % 3); option.AmmoRefillTicksMax = 7; option.Charge = (uint)(mode % 4);
                    }
            }
            for (var i = 0; i < 100; i++)
            {
                var enemy = enemies.Enemies[i];
                enemy.Availability = (byte)(i % 3); enemy.ScoreItem = i % 2 != 0; enemy.Value = (short)(i % 4);
                enemy.X = (short)(100 + i % 5 - 2); enemy.Y = (short)(80 + i % 7 - 3);
                enemy.XSpeed = (sbyte)(mode % 256 - 128); enemy.YSpeed = (sbyte)(127 - mode % 256);
            }
            for (var i = 0; i < 60; i++)
            {
                var shot = enemies.EnemyShots[i];
                shot.Available = i % 2 != 0; shot.X = (short)(100 + i % 5 - 2); shot.Y = (short)(80 + i % 7 - 3);
                shot.XVelocity = (short)(mode % 65536 - 32768); shot.YVelocity = (short)(32767 - mode % 65536);
            }
            var index = checked((byte)items.Index(ItemType.Special, $"case{mode % 114}"));
            players[0].Items.Special = (mode & 2048) != 0 ? index : (byte)0;
            var number = (byte)(mode % 2 + 1); weapons.ExecutedSpecial[number - 1] = (mode & 1024) != 0 ? index : (byte)0;
            shots.Repeat[8] = (byte)((mode & 512) != 0 ? 1 : 0); shots.Repeat[10] = 2; shots.Availability[80] = 9;
            bool[] buttons = [(mode & 128) != 0, (mode & 256) != 0, (mode & 512) != 0, false];
            trace.AppendLine($"specialcase:{mode}");
            Action drawOptions = () => weapons.DrawOptions(hud, (x, y, frame) => trace.AppendLine($"P:{x},{y},{frame}"));
            if (mode >= 12288)
            {
                foreach (var owner in players)
                {
                    owner.Lives = 3; owner.InitialArmor = 57; owner.Items.Ship = 0;
                    owner.WeaponMode = (uint)(mode % 4 + 1);
                    owner.LastXExplosionFollow = 90; owner.LastYExplosionFollow = 75;
                }
                if (combat.TwoPlayers) players[0].WeaponMode = (uint)(mode % 2 + 1);
                level.OnePlayerAction = (mode & 64) != 0; combat.LevelEnd = 17; motion.LevelEndWarp = 3;
                combat.Linked = (mode & 1024) != 0; motion.SecondShipGraphic = 3;
                effects.FollowX = 5; effects.FollowY = 6;
                var input = new InputState(new FramePlatform(), new());
                input.KeysActive[(mode & 128) != 0 ? 1 : 0] = 1;
                input.KeysActive[(mode & 64) != 0 ? 3 : 2] = 1;
                input.KeysActive[4] = (byte)((mode >> 8) & 1); input.KeysActive[5] = (byte)((mode >> 11) & 1);
                input.KeysActive[6] = (byte)((mode >> 9) & 1); input.KeysActive[7] = (byte)((mode >> 10) & 1);
                var collector = new PlayerInput(input, new([], () => 0, () => { }, _ => { }), [],
                    Enumerable.Range(0, 8).ToArray(), motion, combat, level, new())
                { HasMouse = false, NetworkGame = mode < 16384 && (mode & 2048) != 0, LocalPlayer = (byte)((mode & 256) != 0 ? 2 : 1) };
                collector.PlayDemo = mode < 16384 && !collector.NetworkGame && (mode & 128) != 0;
                collector.DemoWait = 1;
                collector.DemoKeys = (byte)(((mode & 128) != 0 ? 2 : 1) | ((mode & 64) != 0 ? 8 : 4) |
                    ((mode >> 8) & 1) * 16 | ((mode >> 11) & 1) * 32 |
                    ((mode >> 9) & 1) * 64 | ((mode >> 10) & 1) * 128);
                using var replay = new MemoryStream();
                NetworkSession? network = null;
                if (collector.NetworkGame)
                {
                    var transport = new FrameTransport(); network = new(transport, input, mode % 3 + 1, TextWriter.Null);
                    if ((mode & 64) == 0)
                    {
                        for (var delay = 0; delay < network.DelayFrames; delay++)
                        {
                            network.PrepareState(); FillFramePacket(network.OutgoingState, mode);
                            network.SendState();
                        }
                        var incoming = new byte[28];
                        BinaryPrimitives.WriteUInt16BigEndian(incoming, (ushort)NetworkPacketType.State);
                        FillFramePacket(incoming, mode);
                        transport.Incoming.Enqueue(new(incoming));
                        network.UpdateState();
                    }
                }
                var frame = new PlayerFrame(players, combat, level, motion, collector, weapons, special, shots, items, effects, enemies)
                {
                    Episode = (byte)((mode & 128) != 0 ? 4 : 1),
                    SuperWild = (mode & 256) != 0,
                    Background2 = (mode & 512) != 0,
                    MapX2Offset = mode % 43,
                    Network = network,
                    DemoPlayback = replay
                };
                buttons.CopyTo(frame.Buttons, 0);
                Array.Fill(frame.OldX, (ushort)98); Array.Fill(frame.OldY, (ushort)77);
                ushort x = 98, y = 77;
                var presentation = new PlayerFramePresentation
                {
                    Game = game,
                    Hud = hud,
                    DrawShip = (sx, sy, graphic, large, effect) => trace.AppendLine(large
                        ? $"ship:{sx},{sy},{graphic},1,{(int)effect}"
                        : $"D:9:{sx}:{sy}:{graphic}:0"),
                    DrawTrail = (sx, sy, graphic) => trace.AppendLine($"ship:{sx},{sy},{graphic},1,0"),
                    DrawSidekick = (sx, sy, graphic, large) => trace.AppendLine(large
                        ? $"ship:{sx},{sy},{graphic},1,0" : $"D:9:{sx}:{sy}:{graphic}:0"),
                    DrawModeButtons = () => weapons.DrawModeButtons((sx, sy, graphic) => trace.AppendLine($"P:{sx},{sy},{graphic}")),
                    DrawIndicator = (sx, sy, graphic) => trace.AppendLine($"D:9:{sx}:{sy}:{graphic}:0"),
                    DrawCharge = (sx, sy, graphic) => trace.AppendLine($"D:10:{sx}:{sy}:{graphic}:0"),
                    DrawOptions = drawOptions,
                    DrawDemoText = () => trace.AppendLine("insertcoin")
                };
                if (mode >= 16384)
                {
                    frame.Devices[0] = frame.Devices[1] = 1;
                    frame.Graphics[0] = (ushort)(mode % 3); frame.Graphics[1] = (ushort)((mode + 1) % 3);
                    level.ThirdAtFirstX = (mode & 64) != 0;
                    motion.LevelEndWarp = (mode & 128) != 0 ? (sbyte)127 : (sbyte)-128;
                    frame.UpdatePlayers([presentation, presentation], backgrounds);
                    frame.UpdatePlayers([presentation, presentation], backgrounds);
                    trace.AppendLine($"parallax:{frame.OldMapXOffset},{frame.MapXOffset},{frame.MapX2Offset},{frame.MapX3Offset}," +
                        $"{backgrounds.First.X},{backgrounds.Second.X},{backgrounds.First.HorizontalTileOffset}," +
                        $"{backgrounds.Second.HorizontalTileOffset},{backgrounds.Third.HorizontalTileOffset}," +
                        $"{backgrounds.Third.X},{frame.OldX[0]},{frame.OldY[0]},{frame.OldX[1]},{motion.LevelEndWarp}");
                    trace.AppendLine($"secondold:{frame.OldY[1]}");
                    foreach (var owner in players)
                        trace.AppendLine($"paired:{owner.X},{owner.Y},{owner.XVelocity},{owner.YVelocity},{Bit(owner.IsAlive)}," +
                            $"{owner.DeltaXShotMove},{owner.DeltaYShotMove}");
                    x = frame.OldX[number - 1]; y = frame.OldY[number - 1];
                }
                else frame.Update(number, 1, (ushort)(mode % 3), ref x, ref y, presentation);
                foreach (var explosion in effects.Explosions.Where(explosion => explosion.Lifetime != 0))
                    trace.AppendLine($"E:{explosion.X}:{explosion.Y}:0");
                if (collector.NetworkGame && collector.LocalPlayer == number)
                    trace.AppendLine("framepacket:" + Convert.ToHexString(network!.OutgoingState));
                trace.AppendLine($"framedifficulty:{enemies.Difficulty}");
                trace.AppendLine($"framedirection:{BitConverter.SingleToUInt32Bits(motion.LinkGunDirection)}");
                trace.AppendLine($"frame:{x},{y},{Bit(motion.MoveAllowed)},{Bit(combat.Linked)},{Bit(level.ReallyEndLevel)}," +
                    $"{combat.LevelEnd},{Bit(level.EndLevel)},{players[0].Lives},{players[1].Lives}," +
                    $"{players[0].PurpleBallsNeeded},{players[1].PurpleBallsNeeded},{Bit(players[number - 1].IsAlive)},{effects.FollowX},{effects.FollowY}");
                var p = players[number - 1];
                trace.AppendLine($"movement:{p.X},{p.Y},{p.XVelocity},{p.YVelocity},{p.XFrictionTicks},{p.YFrictionTicks}," +
                    $"{p.LastXExplosionFollow},{p.LastYExplosionFollow}");
                trace.AppendLine("history:" + string.Concat(Enumerable.Range(0, 20).Select(i => $"{p.OldX[i]},{p.OldY[i]},")));
                for (var i = 0; i < 21; i++)
                    trace.AppendLine($"combo:{weapons.SpecialCodeProgress[0][i]},{weapons.SpecialCodeProgress[1][i]}");
            }
            else if (mode < 4096) special.Complete(number, index, 98, 77, drawOptions);
            else if (mode < 8192) special.Update(number, 98, 77, buttons, game,
                (x, y, frame) => trace.AppendLine($"D:9:{x}:{y}:{frame}:0"), drawOptions);
            else
            {
                weapons.UpdateAttacks(players[number - 1], number, (ushort)(mode % 3), 98, 77, buttons,
                    special.ArcadeMode, (sbyte)(mode % 11), level.EndLevel, motion, special, game, hud,
                    () => weapons.DrawModeButtons((x, y, frame) => trace.AppendLine($"P:{x},{y},{frame}")),
                    (x, y, frame) => trace.AppendLine($"D:9:{x}:{y}:{frame}:0"),
                    (x, y, frame) => trace.AppendLine($"D:10:{x}:{y}:{frame}:0"), drawOptions);
                foreach (var explosion in effects.Explosions.Where(explosion => explosion.Lifetime != 0))
                    trace.AppendLine($"E:{explosion.X}:{explosion.Y}:0");
            }
            trace.AppendLine($"special:{special.NextWait},{special.Wait},{special.ZinglonDuration},{special.AstralDuration},{special.FlareDuration}," +
                $"{Bit(special.FlareStart)},{Bit(special.LinkToPlayer)},{Bit(special.Spray)},{Bit(special.FireButtonHeld)},{Bit(level.FilterActive)}," +
                $"{special.Filter},{special.Frequency},{special.ColorChange},{backgrounds.LevelFilter},{special.Weapon},{special.PowerParameter}," +
                $"{backgrounds.Brightness},{weapons.ExecutedSpecial[0]},{weapons.ExecutedSpecial[1]}");
            foreach (var player in players)
                trace.AppendLine($"owner:{player.Armor},{player.Shield},{player.InvulnerableTicks},{player.Items.Sidekicks[0]},{player.Items.Sidekicks[1]}");
            foreach (var player in players)
                foreach (var option in player.Sidekicks)
                    trace.AppendLine($"initialized:{option.Ammo},{option.AmmoMax},{option.AmmoRefillTicks},{option.AmmoRefillTicksMax}," +
                        $"{option.Style},{option.AnimationFrame},{Bit(option.AnimationEnabled)},{option.Charge},{option.ChargeTicks}");
            if (mode >= 8192)
            {
                var player = players[number - 1];
                trace.AppendLine($"attack:{player.DeltaXShotMove},{player.DeltaYShotMove},{player.WeaponMode},{player.Superbombs}," +
                    $"{weapons.ChargeLevel},{weapons.ChargeWait},{weapons.ChargeGraphic},{weapons.ChargeGraphicWait}," +
                    $"{Bit(weapons.ModeChangeRequested)},{Bit(weapons.ModeChangeReady)},{BitConverter.SingleToUInt32Bits(motion.SatelliteRotation)}");
                foreach (var option in player.Sidekicks)
                    trace.AppendLine($"optionstate:{option.X},{option.Y},{option.Ammo},{option.AmmoRefillTicks}," +
                        $"{option.Charge},{option.ChargeTicks},{Bit(option.AnimationEnabled)}");
            }
            trace.AppendLine($"shotstate:{shots.Power}" + string.Concat(Enumerable.Range(0, 11).Select(i => $",{shots.Repeat[i]},{shots.MultiPosition[i]}")));
            trace.AppendLine("sounds:" + string.Concat(effects.SoundQueue.Select(value => $"{value},")));
            trace.AppendLine($"pixels:{Hash(game)},{Hash(hud)}");
            foreach (var enemy in enemies.Enemies.Take(100)) trace.AppendLine($"attract:{enemy.XSpeed},{enemy.YSpeed}");
            foreach (var shot in enemies.EnemyShots) trace.AppendLine($"repel:{shot.XVelocity},{shot.YVelocity}");
            for (var i = 0; i < PlayerShots.Capacity; i++)
            {
                var shot = shots.Shots[i];
                trace.AppendLine($"projectile:{shots.Availability[i]},{shot.X},{shot.Y},{shot.XM},{shot.YM},{shot.Graphic},{shot.PlayerNumber}");
            }
            trace.AppendLine($"rng:{MersenneTwister.NextUInt32()}");
        }
        if (executable == null) return;
        var path = Path.Combine(Path.GetTempPath(), $"opentyrian-special-{Guid.NewGuid():N}.tyrr");
        try
        {
            File.WriteAllBytes(path, Encoding.Latin1.GetBytes(fixture));
            var start = new ProcessStartInfo(executable) { RedirectStandardOutput = true, RedirectStandardError = true };
            start.ArgumentList.Add(path);
            using var process = Process.Start(start) ?? throw new InvalidOperationException("Cannot start special weapons oracle.");
            var output = process.StandardOutput.ReadToEndAsync(); var errors = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(120000)) { process.Kill(entireProcessTree: true); throw new TimeoutException("Special weapons oracle timed out."); }
            var actual = trace.ToString().Replace("\r\n", "\n", StringComparison.Ordinal);
            var expected = output.GetAwaiter().GetResult().Replace("\r\n", "\n", StringComparison.Ordinal);
            if (process.ExitCode != 0 || actual != expected)
            {
                var a = expected.Split('\n'); var b = actual.Split('\n');
                var index = Enumerable.Range(0, Math.Min(a.Length, b.Length)).FirstOrDefault(i => a[i] != b[i], -1);
                var context = index < 0 ? "length" : b.Take(index).LastOrDefault(line => line.StartsWith("specialcase:", StringComparison.Ordinal));
                throw new InvalidOperationException($"Special weapons differ at {index} ({context}): {a.ElementAtOrDefault(index)} / {b.ElementAtOrDefault(index)}; {errors.GetAwaiter().GetResult()}");
            }
        }
        finally { File.Delete(path); }
    }

    private static void TestFrameFailures(ItemDatabase items)
    {
        var players = new[] { new PlayerState(0), new PlayerState(1) };
        var game = new IndexedSurface(320, 200, 320, TextWriter.Null);
        var effects = new SceneEffects(new byte[8]);
        var combat = new PlayerCombat(players, effects, game);
        var enemies = new EnemySystem(items, TextWriter.Null);
        var backgrounds = new BackgroundRenderer();
        var level = new LevelEvents(items, enemies, players, backgrounds, effects);
        var shots = new PlayerShots(items.Shots, items.Weapons, players, new ShotEffects());
        var weapons = new PlayerWeapons(shots, items, combat);
        var movement = new PlayerMovement(players, combat, level, shots, items);
        var input = new InputState(new FramePlatform(), new());
        var collector = new PlayerInput(input, new([], () => 0, () => { }, _ => { }), [],
            Enumerable.Range(0, 8).ToArray(), movement, combat, level, new())
        { HasMouse = false };
        var special = new PlayerSpecialWeapons(items, shots, weapons, combat, enemies, effects, level, backgrounds);
        var frame = new PlayerFrame(players, combat, level, movement, collector, weapons, special, shots, items, effects, enemies);
        var presentation = new PlayerFramePresentation
        {
            Game = game,
            Hud = game,
            DrawShip = (_, _, _, _, _) => throw new InvalidOperationException("Unexpected rendering."),
            DrawSidekick = (_, _, _, _) => throw new InvalidOperationException("Unexpected sidekick."),
            DrawTrail = (_, _, _) => throw new InvalidOperationException("Unexpected trail."),
            DrawModeButtons = () => throw new InvalidOperationException("Unexpected mode change."),
            DrawIndicator = (_, _, _) => throw new InvalidOperationException("Unexpected indicator."),
            DrawCharge = (_, _, _) => throw new InvalidOperationException("Unexpected charge."),
            DrawOptions = () => throw new InvalidOperationException("Unexpected options."),
            DrawDemoText = () => throw new InvalidOperationException("Unexpected demo text.")
        };
        ushort x = 100, y = 80;
        Throws<ArgumentOutOfRangeException>(() => frame.Update(0, 1, 2, ref x, ref y, presentation));
        Throws<ArgumentException>(() => frame.UpdatePlayers([], backgrounds));
        collector.NetworkGame = true;
        Throws<InvalidOperationException>(() => frame.Update(1, 1, 2, ref x, ref y, presentation));
        frame.Network = new(new FrameTransport(), input, 1, TextWriter.Null);
        collector.LocalPlayer = 0;
        Throws<InvalidDataException>(() => frame.Update(1, 1, 2, ref x, ref y, presentation));
        collector.LocalPlayer = 1;
        frame.Network.PrepareState(); frame.Network.SendState();
        players[1].IsAlive = true;
        Throws<InvalidOperationException>(() => frame.Update(2, 1, 2, ref x, ref y, presentation));
    }

    private static void Throws<T>(Action action) where T : Exception
    {
        try { action(); } catch (T) { return; }
        throw new InvalidOperationException($"Expected {typeof(T).Name}.");
    }

    private static void FillFramePacket(Span<byte> packet, int mode)
    {
        BinaryPrimitives.WriteInt16BigEndian(packet[4..], 2);
        BinaryPrimitives.WriteInt16BigEndian(packet[6..], -3);
        BinaryPrimitives.WriteInt16BigEndian(packet[8..], 1);
        BinaryPrimitives.WriteInt16BigEndian(packet[10..], -1);
        BinaryPrimitives.WriteUInt16BigEndian(packet[12..], (ushort)(mode % 16));
        BinaryPrimitives.WriteUInt16BigEndian(packet[16..], (ushort)(mode % 11));
    }

    private sealed class FrameTransport : INetworkTransport
    {
        public Queue<NetworkPacket> Incoming { get; } = new();
        public void Send(ReadOnlySpan<byte> packet) { }
        public NetworkPacket? Receive() => Incoming.TryDequeue(out var packet) ? packet : null;
    }

    private sealed class FramePlatform : IInputPlatform
    {
        public bool PollEvent(out InputEvent input) { input = default; return false; }
        public uint Ticks => 0;
        public bool JoystickDown => false;
        public bool IsNetworkGame => false;
        public void Delay(uint milliseconds) => throw new InvalidOperationException("Unexpected frame wait.");
        public void SetCursorVisible(bool visible) { }
        public void SetRelativeMouseMode(bool enabled) { }
        public void EnableSystemMouseScaling() { }
        public void ToggleFullscreen() => throw new InvalidOperationException("Unexpected fullscreen toggle.");
        public void WindowResized() => throw new InvalidOperationException("Unexpected window resize.");
        public void Quit() => throw new InvalidOperationException("Unexpected quit.");
        public void PushJoysticksAsKeyboard() => throw new InvalidOperationException("Unexpected menu input.");
        public void PollJoysticks() => throw new InvalidOperationException("Unexpected joystick polling.");
        public void CheckNetwork() => throw new InvalidOperationException("Unexpected network service.");
    }

    private static void TestPresentation(ItemDatabase items)
    {
        var players = new[] { new PlayerState(0), new PlayerState(1) };
        var game = new IndexedSurface(320, 200, 327, TextWriter.Null);
        var hud = new IndexedSurface(320, 200, 327, TextWriter.Null);
        var effects = new SceneEffects(new byte[8]);
        var combat = new PlayerCombat(players, effects, hud);
        var enemies = new EnemySystem(items, TextWriter.Null); var backgrounds = new BackgroundRenderer();
        var level = new LevelEvents(items, enemies, players, backgrounds, effects);
        var shots = new PlayerShots(items.Shots, items.Weapons, players, new ShotEffects());
        var weapons = new PlayerWeapons(shots, items, combat);
        var special = new PlayerSpecialWeapons(items, shots, weapons, combat, enemies, effects, level, backgrounds);
        var data = new byte[203];
        for (var i = 0; i < 100; i++) BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(i * 2), 200);
        data[200] = 0x10; data[201] = 0xBE; data[202] = 0x0F;
        var sheet = new SpriteSheet(); using var sheetStream = new MemoryStream(data); sheet.Load(sheetStream, data.Length);
        using var iconStream = new MemoryStream(); BinaryFile.WriteUInt16(iconStream, 20);
        for (var i = 0; i < 20; i++)
        {
            BinaryFile.WriteBoolean(iconStream, true); BinaryFile.WriteUInt16(iconStream, 1);
            BinaryFile.WriteUInt16(iconStream, 1); BinaryFile.WriteUInt16(iconStream, 1);
            iconStream.WriteByte((byte)(0xA0 + i));
        }
        iconStream.Position = 0; var icons = new SpriteTable(); icons.Load(iconStream);
        players[0].Items.Special = checked((byte)items.Index(ItemType.Special, "case0"));
        bool[] buttons = [false, false, false, false];
        special.Update(1, 0, 0, buttons, game, hud, sheet, icons);
        if (game.Pixels.Span[4 * 327 + 47] != 0xBE)
            throw new InvalidOperationException("Special readiness sprite adapter did not draw.");
        weapons.DrawOptions(hud, icons);
        if (hud.Pixels.Span[64 * 327 + 284] != 0xA0 || players[0].Sidekicks[0].ChargeTicks != 20)
            throw new InvalidOperationException("Sidekick initialization/icon adapter differs.");
        players[0].WeaponMode = 1; weapons.DrawModeButtons(hud, icons);
        if (hud.Pixels.Span[44 * 327 + 285] != 0xB2 || hud.Pixels.Span[44 * 327 + 302] != 0xB3)
            throw new InvalidOperationException("Rear pattern HUD icon adapter differs.");
        combat.TwoPlayers = true; players[1].Items.SidekickLevel = 102; weapons.DrawOptions(hud, icons);
        if (hud.Pixels.Span[108 * 327 + 284] != 0xA0 || hud.Pixels.Span[133 * 327 + 268] != 204)
            throw new InvalidOperationException("Two-player sidekick icon/level adapter differs.");
    }

    private static uint Hash(IndexedSurface surface)
    {
        uint hash = 2166136261;
        foreach (var pixel in surface.Pixels.Span) hash = unchecked((hash ^ pixel) * 16777619);
        return hash;
    }
    private static int Bit(bool value) => value ? 1 : 0;
    private sealed class ShotEffects : IPlayerShotEffects
    {
        public void Draw(int sheet, int x, int y, int frame, bool darken) => throw new InvalidOperationException("Unexpected projectile rendering.");
        public (ushort Width, ushort Height) DrawSpecial(int x, int y, int frame) => throw new InvalidOperationException("Unexpected projectile rendering.");
        public void Explosion(int x, int y, byte type) => throw new InvalidOperationException("Unexpected projectile movement.");
        public void SuperPixels(int x, int y, ushort count, byte width, byte color) => throw new InvalidOperationException("Unexpected projectile movement.");
    }
}
