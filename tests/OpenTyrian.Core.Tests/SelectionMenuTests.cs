using System.Diagnostics;
using System.Text;
using System.Runtime.InteropServices;
using OpenTyrian.Core;

namespace OpenTyrian.Core.Tests;

internal static class SelectionMenuTests
{
    private readonly record struct Frame(int Key = 0, ushort Mod = 0, int X = -999, int Y = 0,
        int ClickX = 0, int ClickY = 0, byte Button = 0, int Held = 0);

    public static void Run(string? executable = null)
    {
        var actual = Execute();
        Check(actual.Contains("result:1,1,1,7,9,8,", StringComparison.Ordinal), "Lord difficulty was not unlocked.");
        Check(actual.Contains("black:15", StringComparison.Ordinal), "Menu cancellation did not fade out.");
        if (executable != null)
        {
            var startInfo = new ProcessStartInfo(executable)
            { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
            startInfo.ArgumentList.Add(Path.GetTempPath());
            using var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("Cannot start native menu oracle.");
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(30000))
            {
                process.Kill(entireProcessTree: true);
                throw new TimeoutException("Native menu oracle timed out.");
            }
            var expected = stdout.GetAwaiter().GetResult().Replace("\r\n", "\n", StringComparison.Ordinal);
            Check(process.ExitCode == 0, $"Native menu oracle failed: {stderr.GetAwaiter().GetResult()}");
            var expectedLines = expected.Split('\n');
            var actualLines = actual.Split('\n');
            for (var i = 0; i < Math.Max(expectedLines.Length, actualLines.Length); i++)
                if (i >= expectedLines.Length || i >= actualLines.Length || expectedLines[i] != actualLines[i])
                    throw new InvalidOperationException($"Menu trace mismatch at {i + 1}: native '{expectedLines.ElementAtOrDefault(i)}', managed '{actualLines.ElementAtOrDefault(i)}'.");
        }
        Presentation();
        KeyboardConfigurationParity();
        MainMenuActionParity();
        ItemIconDrawingParity();
        ShipIllustrationDrawingParity();
        ShipSpecificationsDrawingParity();
        WeaponSimulationDrawingParity();
        WeaponViewFrameParity();
        ItemScreenLoopParity();
    }

    private static void ItemIconDrawingParity()
    {
        var definitions = new ItemDefinitions();
        definitions.ParseBundle(Encoding.Latin1.GetBytes(ItemDatabaseTests.Fixture(0) +
            "[ship:icons]\nshipgraphic=41\n[weapon:icons]\nitemgraphic=42\n" +
            "[generator:icons]\nitemgraphic=44\n[shield:icons]\nitemgraphic=45\n" +
            "[sidekick:icons]\nitemgraphic=46\n"), "icons.tyrr");
        var items = ItemDatabase.FromDefinitions(definitions);
        var shipIcon = checked((ushort)items.Index(ItemType.Ship, "icons"));
        var weaponIcon = checked((ushort)items.Index(ItemType.WeaponFront, "icons"));
        var generatorIcon = checked((ushort)items.Index(ItemType.Generator, "icons"));
        var shieldIcon = checked((ushort)items.Index(ItemType.Shield, "icons"));
        var sidekickIcon = checked((ushort)items.Index(ItemType.SidekickLeft, "icons"));
        var normalShips = new SpriteSheet();
        var extraShips = new SpriteSheet();
        var customShipData = new byte[15];
        customShipData[0] = 8;
        var shipInfo = new PlayerShipInfo([new PlayerState(0), new PlayerState(1)], items,
            normalShips, extraShips, customShipData, new ushort[2]);
        var shopSprites = new SpriteSheet();
        var calls = new List<(SpriteSheet Sheet, int X, int Y, int Graphic)>();
        void Draw(SpriteSheet sheet, int x, int y, int graphic) => calls.Add((sheet, x, y, graphic));

        ItemIconDrawing.Draw(0, 1, 5, 6, items, shopSprites, normalShips, shipInfo, Draw);
        ItemIconDrawing.Draw(1, shipIcon, 5, 6, items, shopSprites, normalShips, shipInfo, Draw);
        ItemIconDrawing.Draw(1, 91, 7, 8, items, shopSprites, normalShips, shipInfo, Draw);
        foreach (var (type, item) in new (byte Type, ushort Item)[]
            { (2, weaponIcon), (3, weaponIcon), (4, shieldIcon), (5, generatorIcon), (6, sidekickIcon), (7, sidekickIcon) })
            ItemIconDrawing.Draw(type, item, 9, 10, items, shopSprites, normalShips, shipInfo, Draw);
        ItemIconDrawing.Draw(9, 1, 0, 0, items, shopSprites, normalShips, shipInfo, Draw);

        Check(calls.Count == 8 &&
            calls[0] == (normalShips, 5, 6, 41) &&
            calls[1] == (extraShips, 7, 8, 5) &&
            calls.Skip(2).All(call => call.Sheet == shopSprites) &&
            calls.Skip(2).Select(call => call.Graphic).SequenceEqual(new[] { 42, 42, 45, 44, 46, 46 }) &&
            calls.Skip(2).All(call => call.X == 9 && call.Y == 10),
            "Item drawing changed item-type lookup, custom-ship routing, coordinates, or empty-item handling.");

        var rejectedItem = false;
        try { ItemIconDrawing.Draw(1, 250, 0, 0, items, shopSprites, normalShips, shipInfo, Draw); }
        catch (InvalidDataException) { rejectedItem = true; }
        Check(rejectedItem, "Item drawing accepted an out-of-range ship id.");
    }

    private static void ShipIllustrationDrawingParity()
    {
        var definitions = new ItemDefinitions();
        definitions.ParseBundle(Encoding.Latin1.GetBytes(ItemDatabaseTests.Fixture(0) +
            "[ship:illus]\nbigshipgraphic=8\nbigshipgraphx=2\nbigshipgraphy=3\n" +
            "[generator:illus]\nbigshipgraphic=14\nbigshipgraphx=10\nbigshipgraphy=11\n" +
            "[weapon:illus]\nbigshipgraphic=17\nbigshipgraphfrntx=20\nbigshipgraphfrnty=21\n" +
            "bigshipgraphrearx=22\nbigshipgraphreary=23\n" +
            "[sidekick:illus]\nitemgraphic=46\n[shield:illus]\nmpwr=8\n"), "illustration.tyrr");
        var items = ItemDatabase.FromDefinitions(definitions);
        var player = new PlayerState(0);
        player.Items.Ship = checked((byte)items.Index(ItemType.Ship, "illus"));
        player.Items.Generator = checked((byte)items.Index(ItemType.Generator, "illus"));
        player.Items.Weapons[0].Id = checked((byte)items.Index(ItemType.WeaponFront, "illus"));
        player.Items.Weapons[1].Id = player.Items.Weapons[0].Id;
        player.Items.Sidekicks[0] = checked((byte)items.Index(ItemType.SidekickLeft, "illus"));
        player.Items.Sidekicks[1] = player.Items.Sidekicks[0];
        player.Items.Shield = checked((byte)items.Index(ItemType.Shield, "illus"));

        var host = new TraceShipIllustrationHost();
        ShipIllustrationDrawing.Draw(player, items, host);
        Check(host.Calls.SequenceEqual(new[]
        {
            "sprite:2,3,5,7", "sprite:10,11,6,14", "sprite:20,21,6,17",
            "sprite:22,23,6,17", $"item:6,{player.Items.Sidekicks[0]},3,84",
            $"item:7,{player.Items.Sidekicks[1]},129,84", "hue:28,23,5,26,15,254"
        }), "Ship illustration changed component ordering, catalog lookups, positions, or shield brightness.");

        var invalidPlayer = new PlayerState(1);
        var rejectedShip = false;
        try { ShipIllustrationDrawing.Draw(invalidPlayer, items, host); }
        catch (InvalidDataException) { rejectedShip = true; }
        Check(rejectedShip, "Ship illustration accepted a missing player ship.");
    }

    private sealed class TraceShipIllustrationHost : IShipIllustrationHost
    {
        public List<string> Calls { get; } = [];
        public void DrawSprite(int x, int y, int shapeTable, int sprite)
            => Calls.Add($"sprite:{x},{y},{shapeTable},{sprite}");
        public void DrawHueSprite(int x, int y, int shapeTable, int sprite, byte hue, byte brightness)
            => Calls.Add($"hue:{x},{y},{shapeTable},{sprite},{hue},{brightness}");
        public void DrawItemIcon(byte itemType, byte itemNumber, int x, int y)
            => Calls.Add($"item:{itemType},{itemNumber},{x},{y}");
    }

    private static void ShipSpecificationsDrawingParity()
    {
        var fixture = ItemDatabaseTests.Fixture(0)
            .Replace("bigshipgraphic=-2147483648", "bigshipgraphic=3", StringComparison.Ordinal)
            .Replace("bigshipgraphx=-2147483648", "bigshipgraphx=40", StringComparison.Ordinal)
            .Replace("bigshipgraphy=-2147483648", "bigshipgraphy=30", StringComparison.Ordinal);
        var definitions = new ItemDefinitions();
        definitions.ParseBundle(Encoding.Latin1.GetBytes(fixture), "ship-specs.tyrr");
        var items = ItemDatabase.FromDefinitions(definitions);

        using var encoded = new MemoryStream();
        BinaryFile.WriteUInt16(encoded, 3);
        BinaryFile.WriteBoolean(encoded, false);
        BinaryFile.WriteBoolean(encoded, false);
        BinaryFile.WriteBoolean(encoded, true);
        BinaryFile.WriteUInt16(encoded, 1);
        BinaryFile.WriteUInt16(encoded, 1);
        BinaryFile.WriteUInt16(encoded, 1);
        encoded.WriteByte(0xCF);
        encoded.Position = 0;
        var shapes = new SpriteTable();
        shapes.Load(encoded);

        var screen = new IndexedSurface(320, 200, 327, TextWriter.Null);
        var temporary = new IndexedSurface(320, 200, 327, TextWriter.Null);
        screen.Pixels.Span.Fill(0x55);
        temporary.Pixels.Span.Fill(0x77);
        var host = new TraceShipSpecsHost();
        ShipSpecificationsDrawing.Draw(screen, temporary, items.Ships, 0, 1,
            new ReadOnlyMemory<byte>[] { "description one"u8.ToArray(), "description two"u8.ToArray() },
            "Done"u8.ToArray(), shapes, host);

        Check(host.Calls.SequenceEqual(new[]
        {
            "lines:1", "lines:0", "height:9", "text:10,2,Byte\u0080\u00FF,12,3",
            "help:100,20,description one,40", "help:100,100,description two,40",
            "height:7", "text:88,190,Done,12,2"
        }) && host.VerticalHeight == 7,
            "Ship specification drawing changed decoration/text order or vertical-height state.");
        Check(screen.Pixels.Span[30 * screen.Pitch + 10] == 0xCF &&
            screen.Pixels.Span[30 * screen.Pitch + 11] == 0 &&
            screen.Pixels.Span[0] == 37 && temporary.Pixels.Span[30 * screen.Pitch + 10] == 0xCF,
            "Ship specification greenification or screen initialization differs.");
    }

    private sealed class TraceShipSpecsHost : IShipSpecificationsDrawingHost
    {
        public List<string> Calls { get; } = [];
        private int _verticalHeight;
        public int VerticalHeight
        {
            get => _verticalHeight;
            set { _verticalHeight = value; Calls.Add($"height:{value}"); }
        }
        public void DrawLines(IndexedSurface screen, bool dark) => Calls.Add($"lines:{(dark ? 1 : 0)}");
        public void DrawHelpBox(IndexedSurface screen, int x, int y, ReadOnlyMemory<byte> text, int width)
            => Calls.Add($"help:{x},{y},{Encoding.Latin1.GetString(text.Span)},{width}");
        public void DrawText(IndexedSurface screen, int x, int y, ReadOnlyMemory<byte> text, byte hue, int brightness)
            => Calls.Add($"text:{x},{y},{Encoding.Latin1.GetString(text.Span)},{hue},{brightness}");
        public int Center(ReadOnlyMemory<byte> text, Font font) => 88;
    }

    private static void WeaponSimulationDrawingParity()
    {
        var definitions = new ItemDefinitions();
        definitions.ParseBundle(Encoding.Latin1.GetBytes(ItemDatabaseTests.Fixture(0)), "weapon-sim.tyrr");
        var items = ItemDatabase.FromDefinitions(definitions);
        var weaponId = checked((byte)items.Index(ItemType.WeaponFront, "late"));
        var player = new PlayerState(0) { X = 72, Y = 110 };
        player.Items.Weapons[0] = new PlayerWeapon { Id = weaponId, Power = 2 };
        var availability = new EpisodeItemAvailabilityState();
        availability.Items[1, 0] = weaponId;
        availability.Counts[1] = 1;
        var state = new WeaponSimulationState
        {
            UpgradeSelection = 3,
            SubmenuSelection = 2,
            SubmenuChoiceCount = 3,
            LeftPowerAvailable = true,
            RightPowerAvailable = true,
            RightPowerAffordable = false,
            DowngradeCost = 12,
            UpgradeCost = 34
        };
        var host = new TraceWeaponSimulationHost();
        WeaponSimulationDrawing.Update(state, player, availability, items, host);
        var expectedShotId = items.Weapons[weaponId].Op[0][1];
        Check(host.Calls.SequenceEqual(new[]
        {
            "view", "text:26,137,12,1,4", "text:108,137,34,7,4", "sprite:119,149,14",
            "fill:45,151,49,151,251", "pixel:45,151,252", "fill:45,152,49,164,250",
            "fill:45,165,49,165,249", "fill:51,151,55,151,251", "pixel:51,151,252",
            "fill:51,152,55,164,250", "fill:51,165,55,165,249", "text:58,137,POWER: 2,15,4",
            $"text:58,127,Shot: {items.Shots[expectedShotId].Id},15,4", "ship:67,103"
        }), "Weapon simulation changed the power controls, power meter, shot label, or draw order.");

        state.UpgradeSelection = 2;
        state.LeftPowerAvailable = true;
        state.RightPowerAvailable = true;
        host = new TraceWeaponSimulationHost();
        WeaponSimulationDrawing.Update(state, player, availability, items, host);
        Check(host.Calls.SequenceEqual(new[] { "view", "sprite:20,146,17", "ship:67,103" }) &&
            !state.LeftPowerAvailable && !state.RightPowerAvailable,
            "Weapon simulation did not hide and reset power controls outside weapon rows.");
    }

    private sealed class TraceWeaponSimulationHost : IWeaponSimulationHost
    {
        public List<string> Calls { get; } = [];
        public void UpdateWeaponViewFrame() => Calls.Add("view");
        public void DrawText(int x, int y, string text, byte hue, int brightness)
            => Calls.Add($"text:{x},{y},{text},{hue},{brightness}");
        public void DrawOptionSprite(int x, int y, int sprite) => Calls.Add($"sprite:{x},{y},{sprite}");
        public void FillRectangle(int left, int top, int right, int bottom, byte color)
            => Calls.Add($"fill:{left},{top},{right},{bottom},{color}");
        public void DrawPixel(int x, int y, byte color) => Calls.Add($"pixel:{x},{y},{color}");
        public void DrawShip(int x, int y) => Calls.Add($"ship:{x},{y}");
    }

    private static void WeaponViewFrameParity()
    {
        var definitions = new ItemDefinitions();
        definitions.ParseBundle(Encoding.Latin1.GetBytes(ItemDatabaseTests.Fixture(0) +
            "[shot:late]\nmax=1\nmulti=1\nsg=1\ndel=5\n" +
            "[sidekick:24]\ntr=2\noption=1\nani=2\npwr=1\nwport=late\nwpnum=late\ngr0=2 3\n"),
            "weapon-view.tyrr");
        var items = ItemDatabase.FromDefinitions(definitions);
        var player = new PlayerState(0) { X = 72, Y = 110 };
        player.Items.Weapons[0].Id = 1;
        player.Items.Weapons[0].Power = 1;
        player.Items.Sidekicks[(int)SidekickPort.Right] =
            checked((byte)items.Index(ItemType.SidekickRight, "24"));
        player.Sidekicks[(int)SidekickPort.Right].AnimationEnabled = true;
        player.Sidekicks[(int)SidekickPort.Right].ChargeTicks = 1;
        var players = new[] { player, new PlayerState(1) };
        var effects = new IdleShotEffects();
        var shots = new PlayerShots(items.Shots, items.Weapons, players, effects)
        {
            Power = 500
        };
        shots.Repeat[0] = 0;
        shots.Repeat[1] = 1;
        shots.Repeat[2] = 1;
        shots.Repeat[3] = 0;
        var screen = new IndexedSurface(320, 200, 320, TextWriter.Null);
        var hud = new LevelHudState();
        var host = new TraceWeaponViewFrameHost();

        WeaponViewFrame.Update(screen, player, items, shots, hud, 30, host);

        Check(host.Calls.SequenceEqual(new[] { "starfield:1", "sidekick:9,72,90,3,False", "upgrade-interface" }) &&
            shots.Repeat.Take(4).SequenceEqual(new byte[] { 0, 0, 0, 0 }) &&
            shots.Power == 530 && hud.LastPower == 94 &&
            player.Sidekicks[(int)SidekickPort.Right].X == 72 &&
            player.Sidekicks[(int)SidekickPort.Right].Y == 90 &&
            player.Sidekicks[(int)SidekickPort.Right].AnimationFrame == 1 &&
            player.Sidekicks[(int)SidekickPort.Right].Charge == 1 &&
            player.Sidekicks[(int)SidekickPort.Right].ChargeTicks == 20 &&
            shots.Availability[0] == 4 && shots.Availability[1] == 4 && effects.DrawCount == 2 &&
            shots.Shots[0].PlayerNumber == 1 && shots.Shots[0].X == 72 && shots.Shots[0].Y == 110 &&
            shots.Shots[1].PlayerNumber == 1 && shots.Shots[1].X == 72 && shots.Shots[1].Y == 90 &&
            screen.Pixels.Span[93 * screen.Pitch + 145] != 0,
            $"Weapon-view frame mismatch: calls={string.Join('|', host.Calls)}, repeat={string.Join(',', shots.Repeat.Take(4))}, power={shots.Power}/{hud.LastPower}, sidekick={player.Sidekicks[1].X},{player.Sidekicks[1].Y},{player.Sidekicks[1].AnimationFrame},{player.Sidekicks[1].Charge},{player.Sidekicks[1].ChargeTicks}, availability={shots.Availability[0]},{shots.Availability[1]}, shots={shots.Shots[0].PlayerNumber}:{shots.Shots[0].X},{shots.Shots[0].Y};{shots.Shots[1].PlayerNumber}:{shots.Shots[1].X},{shots.Shots[1].Y}, draws={effects.DrawCount}");
    }

    private sealed class TraceWeaponViewFrameHost : IWeaponViewFrameHost
    {
        public List<string> Calls { get; } = [];
        public void UpdateStarfield(IndexedSurface screen, int moveSpeed) => Calls.Add($"starfield:{moveSpeed}");
        public void DrawSidekick(int sheet, int x, int y, int graphic, bool large)
            => Calls.Add($"sidekick:{sheet},{x},{y},{graphic},{large}");
        public void DrawUpgradeInterface() => Calls.Add("upgrade-interface");
    }

    private sealed class IdleShotEffects : IPlayerShotEffects
    {
        public int DrawCount { get; private set; }
        public void Draw(int sheet, int x, int y, int frame, bool darken) => DrawCount++;
        public (ushort Width, ushort Height) DrawSpecial(int x, int y, int frame) => (0, 0);
        public void Explosion(int x, int y, byte type) { }
        public void SuperPixels(int x, int y, ushort count, byte width, byte color) { }
    }

    private static void ItemScreenLoopParity()
    {
        var host = new TraceItemScreenLoopHost([false, true]);
        ItemScreenLoop.Run(host);
        Check(host.Calls.SequenceEqual(new[]
        {
            "initialize", "prepare", "draw", "events:1", "update", "wait", "update", "wait",
            "keyboard:0", "input"
        }), "Item-screen loop changed the non-network frame, animation, input, or exit sequence.");

        host = new TraceItemScreenLoopHost([], constantPlay: true, networkGame: true);
        ItemScreenLoop.Run(host);
        Check(host.Calls.SequenceEqual(new[]
        {
            "initialize", "prepare", "draw", "events:1", "start", "keyboard:0", "input",
            "prepare-packet", "send-packet", "events:0", "present", "network-update",
            "network-check", "delay:16", "events:0", "present", "network-update",
            "reset-network", "events:0", "present", "network-check", "delay:16", "fade:10"
        }), "Item-screen loop changed constant-play, network-wait, synchronization, or fade ordering.");
    }

    private sealed class TraceItemScreenLoopHost(
        IEnumerable<bool> inputResults, bool constantPlay = false, bool networkGame = false) : IItemScreenLoopHost
    {
        private readonly Queue<bool> _inputResults = new(inputResults);
        private int _networkUpdates;
        private int _synchronizationChecks;
        private bool _keyboardUsed;
        public List<string> Calls { get; } = [];
        public bool ConstantPlay { get; } = constantPlay;
        public bool NetworkGame { get; } = networkGame;
        public bool GameLoaded { get; private set; }
        public bool JumpSection { get; private set; }
        public bool KeyboardUsed
        {
            get => _keyboardUsed;
            set { _keyboardUsed = value; Calls.Add($"keyboard:{(value ? 1 : 0)}"); }
        }
        public bool WaitingPacketReceived => _networkUpdates > 0;
        public bool NetworkSynchronized => ++_synchronizationChecks > 1;

        public void Initialize() => Calls.Add("initialize");
        public void PrepareFrame(int[] temporaryWeaponPower)
        {
            Check(temporaryWeaponPower.Length == 7, "Item-screen weapon-power scratch array changed size.");
            Calls.Add("prepare");
        }
        public void Draw() => Calls.Add("draw");
        public void ServiceEvents(bool clearNew) => Calls.Add($"events:{(clearNew ? 1 : 0)}");
        public void StartCurrentLevel() { Calls.Add("start"); GameLoaded = true; }
        public void UpdateFrame() => Calls.Add("update");
        public bool WaitForInput() { Calls.Add("wait"); return _inputResults.Dequeue(); }
        public void HandleInput(int[] temporaryWeaponPower)
        { Calls.Add("input"); JumpSection = true; }
        public void Present() => Calls.Add("present");
        public void PrepareWaitingPacket() => Calls.Add("prepare-packet");
        public void SendNetworkPacket() => Calls.Add("send-packet");
        public void NetworkUpdate() { Calls.Add("network-update"); _networkUpdates++; }
        public void NetworkCheck() => Calls.Add("network-check");
        public void Delay(int milliseconds) => Calls.Add($"delay:{milliseconds}");
        public void ResetNetworkState() => Calls.Add("reset-network");
        public void FadeBlack(int steps) => Calls.Add($"fade:{steps}");
    }

    private static void DataCubeTextLoading(StringBuilder trace)
    {
        var archive = new MemoryStream();
        WriteCubeString(archive, "*   12");
        WriteCubeString(archive, "Flight log");
        WriteCubeString(archive, "Chapter 4");
        WriteCubeString(archive, "alpha beta gamma delta epsilon");
        WriteCubeString(archive, string.Empty);
        WriteCubeString(archive, string.Empty);
        WriteCubeString(archive, "paragraph word");
        WriteCubeString(archive, "*   13");
        WriteCubeString(archive, "Flight log 2");
        WriteCubeString(archive, "Chapter 5");
        WriteCubeString(archive, "second record");
        WriteCubeString(archive, "*");
        archive.Position = 0;

        var openCalls = 0;
        var cubes = DataCubeTextLoader.LoadCubes([1, 2], () =>
        {
            openCalls++;
            return new MemoryStream(archive.ToArray(), writable: false);
        }, text => text.Length * 6);
        var cube = cubes[0];
        Check(openCalls == 2 && cube.FaceSprite == 11 && cube.Title == "Flight log" &&
            cube.Header == "Chapter 4" && cubes[1].FaceSprite == 12 &&
            cubes[1].Title == "Flight log 2" && cubes[1].Header == "Chapter 5",
            "Data-cube marker or metadata parsing changed.");
        Check(cube.LastLine == 7 && cube.Lines[0] == " alpha beta gamma delta" &&
            cube.Lines[1] == "epsilon" && cube.Lines[6] == " paragraph word",
            "Data-cube paragraph spacing or tiny-font word wrapping changed.");
        Check(cube.Lines.Length == 90 && cube.Lines[89].Length == 0,
            "Data-cube text storage did not preserve the native line capacity.");
        for (var index = 0; index < cubes.Count; index++)
        {
            var loaded = cubes[index];
            trace.AppendLine($"cube-loaded:{index},{loaded.FaceSprite},{loaded.LastLine}," +
                $"{Convert.ToHexString(Encoding.Latin1.GetBytes(loaded.Title))}," +
                $"{Convert.ToHexString(Encoding.Latin1.GetBytes(loaded.Header))}");
            for (var line = 0; line < loaded.LastLine; line++)
                trace.AppendLine($"cube-line:{index},{line},{Convert.ToHexString(Encoding.Latin1.GetBytes(loaded.Lines[line]))}");
        }

        var rejectedIndex = false;
        try
        {
            DataCubeTextLoader.LoadCube(() => new MemoryStream(), 0, text => text.Length);
        }
        catch (ArgumentOutOfRangeException)
        {
            rejectedIndex = true;
        }
        Check(rejectedIndex, "Data-cube loading accepted a zero native record index.");

        static void WriteCubeString(Stream stream, string value)
        {
            var plain = Encoding.Latin1.GetBytes(value);
            if (plain.Length > byte.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(value));
            var cipher = new byte[plain.Length];
            ReadOnlySpan<byte> key = [204, 129, 63, 255, 71, 19, 25, 62, 1, 99];
            for (var i = 0; i < plain.Length; i++)
                cipher[i] = (byte)(plain[i] ^ key[i % key.Length] ^ (i == 0 ? 0 : cipher[i - 1]));
            stream.WriteByte((byte)plain.Length);
            stream.Write(cipher);
        }
    }

    private static void KeyboardConfigurationParity()
    {
        int[] defaults = [82, 81, 80, 79, 44, 40, 224, 226];
        var settings = Enumerable.Range(0, 8).Select(i => i + 10).ToArray();
        byte currentMenu = 0;
        ushort reboundSelection = 0;
        KeyboardConfigurationSelection.Handle(10, settings, defaults, false,
            menu => currentMenu = menu, selection => reboundSelection = selection);
        Check(settings.SequenceEqual(defaults) && currentMenu == 0 && reboundSelection == 0,
            "Keyboard defaults selection did not copy every binding.");
        KeyboardConfigurationSelection.Handle(11, settings, defaults, true,
            menu => currentMenu = menu, selection => reboundSelection = selection);
        Check(currentMenu == 11 && reboundSelection == 0, "Network keyboard configuration did not return to limited options.");
        KeyboardConfigurationSelection.Handle(11, settings, defaults, false,
            menu => currentMenu = menu, selection => reboundSelection = selection);
        Check(currentMenu == 2, "Local keyboard configuration did not return to options.");
        KeyboardConfigurationSelection.Handle(6, settings, defaults, false,
            menu => currentMenu = menu, selection => reboundSelection = selection);
        Check(reboundSelection == 6, "Keyboard configuration did not rebind the selected control.");

        settings = [10, 11, 12, 13, 14, 15, 16, 17];
        var platform = new QuitPlatform();
        var input = new InputState(platform, new VideoLayout { Output = new(0, 0, 320, 200) });
        var host = new KeyboardHost(input, platform, [new(InputEventKind.KeyDown, 11)]);
        KeyboardConfigurationSelection.Rebind(4, settings, host);
        Check(settings.SequenceEqual([10, 12, 11, 13, 14, 15, 16, 17]) && host.Clicks == 1,
            "Keyboard rebinding did not swap a duplicate binding before assignment.");
        Check(host.KeyNameDrawn == "12" && host.LastRectangle == (230, 60, 300, 69, 249),
            "Keyboard rebinding changed its prompt location or initial highlight frame.");

        settings = [41, 11, 12, 13, 14, 15, 16, 17];
        platform = new QuitPlatform();
        input = new InputState(platform, new VideoLayout { Output = new(0, 0, 320, 200) });
        host = new KeyboardHost(input, platform, [new(InputEventKind.KeyDown, 41)]);
        KeyboardConfigurationSelection.Rebind(4, settings, host);
        Check(settings.SequenceEqual([12, 11, 12, 13, 14, 15, 16, 17]) && host.Clicks == 0,
            "Forbidden Escape binding was assigned or failed to release its prior binding.");

        settings = [10, 11, 12, 13, 14, 15, 16, 17];
        platform = new QuitPlatform();
        input = new InputState(platform, new VideoLayout { Output = new(0, 0, 320, 200) });
        host = new KeyboardHost(input, platform, [new(InputEventKind.MouseButtonDown, Button: 1)]);
        KeyboardConfigurationSelection.Rebind(4, settings, host);
        Check(settings.SequenceEqual([10, 11, 12, 13, 14, 15, 16, 17]) && host.Clicks == 0,
            "Mouse input incorrectly assigned a keyboard binding.");

        var rejectedSelection = false;
        try { KeyboardConfigurationSelection.Rebind(1, settings, host); }
        catch (ArgumentOutOfRangeException) { rejectedSelection = true; }
        Check(rejectedSelection, "Keyboard rebinding accepted a selection outside its control rows.");
    }

    private static void MainMenuActionParity()
    {
        var state = new MainMenuActionState { CurrentMenu = 9, ChoiceCount = 4 };
        var specs = 0;
        var nextLevel = 0;
        var quit = 0;
        MainMenuActionHandlers.SelectFullGame(2, state, () => specs++, () => nextLevel++, () => quit++);
        Check(state.CurrentMenu == 7 && specs == 0 && nextLevel == 0 && quit == 0,
            "Full-game data-cube selection changed the wrong state.");
        MainMenuActionHandlers.SelectFullGame(3, state, () => specs++, () => nextLevel++, () => quit++);
        MainMenuActionHandlers.SelectFullGame(6, state, () => specs++, () => nextLevel++, () => quit++);
        MainMenuActionHandlers.SelectFullGame(7, state, () => specs++, () => nextLevel++, () => quit++);
        Check(specs == 1 && nextLevel == 1 && quit == 1,
            "Full-game selection failed to dispatch ship, next-level, or quit actions.");
        foreach (var selection in new byte[] { 4, 5 })
        {
            MainMenuActionHandlers.SelectFullGame(selection, state, () => specs++, () => nextLevel++, () => quit++);
            Check(state.CurrentMenu == (selection == 4 ? 1 : 2), "Full-game navigation selected the wrong submenu.");
        }

        var sounds = new List<byte>();
        var starts = 0;
        state.InputDevices[0] = 1; state.InputDevices[1] = 2;
        MainMenuActionHandlers.SelectTwoPlayer(3, state, 0, () => starts++, sounds.Add, () => quit++);
        Check(state.InputDevices.SequenceEqual(new byte[] { 2, 1 }) && sounds.SequenceEqual(new byte[] { 28 }),
            "Two-player device cycling without joysticks failed to retain distinct players.");
        state.InputDevices[0] = 3; state.InputDevices[1] = 1;
        MainMenuActionHandlers.SelectTwoPlayer(3, state, 1, () => starts++, sounds.Add, () => quit++);
        Check(state.InputDevices[0] == 2 && state.InputDevices[1] == 1,
            "Two-player device cycling did not wrap past the connected-device range.");
        MainMenuActionHandlers.SelectTwoPlayer(2, state, 0, () => starts++, sounds.Add, () => quit++);
        MainMenuActionHandlers.SelectTwoPlayer(5, state, 0, () => starts++, sounds.Add, () => quit++);
        Check(starts == 1 && state.CurrentMenu == 2 && sounds.Count == 2,
            "Two-player start/options handling changed sound or action ordering.");
        MainMenuActionHandlers.SelectTwoPlayer(6, state, 0, () => starts++, sounds.Add, () => quit++);
        Check(quit == 2, "Two-player quit selection did not dispatch quit.");
        MainMenuActionHandlers.ChangeTwoPlayerInputDevice(4, -1, 0, state.InputDevices, sounds.Add);
        Check(state.InputDevices.SequenceEqual(new byte[] { 1, 2 }) && sounds.Count == 3,
            "Backward no-joystick cycling did not preserve distinct input devices.");
        var soundCount = sounds.Count;
        MainMenuActionHandlers.ChangeTwoPlayerInputDevice(2, 1, 0, state.InputDevices, sounds.Add);
        Check(sounds.Count == soundCount, "An invalid player row played a cursor sound.");

        var menuDestinations = new List<byte>();
        MainMenuActionHandlers.SelectOnePlayer(2, state, false, () => starts++,
            menuDestinations.Add, () => quit++);
        MainMenuActionHandlers.SelectOnePlayer(3, state, true, () => starts++,
            menuDestinations.Add, () => quit++);
        MainMenuActionHandlers.SelectOnePlayer(4, state, false, () => starts++,
            menuDestinations.Add, () => quit++);
        Check(starts == 2 && menuDestinations.SequenceEqual(new byte[] { 11 }) && quit == 3,
            "One-player actions did not start, return to the network menu, and quit.");

        foreach (var (selection, expected) in new (byte, byte)[] { (2, 12), (3, 5), (6, 10) })
        {
            MainMenuActionHandlers.SelectLimitedOptions(selection, state);
            Check(state.CurrentMenu == expected, "Limited-options selection chose the wrong destination.");
        }
        MainMenuActionHandlers.SelectSuperTyrian(2, state, () => starts++, () => specs++, () => quit++);
        MainMenuActionHandlers.SelectSuperTyrian(3, state, () => starts++, () => specs++, () => quit++);
        MainMenuActionHandlers.SelectSuperTyrian(4, state, () => starts++, () => specs++, () => quit++);
        MainMenuActionHandlers.SelectSuperTyrian(5, state, () => starts++, () => specs++, () => quit++);
        Check(starts == 3 && specs == 2 && state.CurrentMenu == 2 && quit == 4,
            "SuperTyrian selections did not preserve their start/spec/options/quit actions.");

        foreach (var selection in new byte[] { 2, 3 })
        {
            state.QuickSave = true;
            state.SaveGamePageIndex = 8;
            MainMenuActionHandlers.SelectOptions(selection, state);
            Check(state.CurrentMenu == 6 && state.PerformSave == (selection == 3) &&
                !state.QuickSave && state.SaveGamePageIndex == 0,
                "Options save/load selection failed to reset save-page state.");
        }
        foreach (var (selection, expected) in new (byte, byte)[] { (6, 12), (7, 5), (8, 0) })
        {
            MainMenuActionHandlers.SelectOptions(selection, state);
            Check(state.CurrentMenu == expected, "Options selection chose the wrong submenu.");
        }

        state.CurrentMenu = 3; state.NewPalette = 0; state.ChoiceCount = 4;
        state.MapSections = [7, 11];
        MainMenuActionHandlers.SelectNextLevel(3, state);
        Check(state.MainLevel == 11 && state.JumpSection, "Next-level selection did not jump to the selected section.");
        state.JumpSection = false;
        MainMenuActionHandlers.SelectNextLevel(4, state);
        Check(state.CurrentMenu == 0 && state.NewPalette == 1 && !state.JumpSection,
            "Next-level Done selection did not return to the full-game menu.");

        var game = new LegacyGameState();
        var player = game.Players[0];
        player.Cash = 100;
        player.Items.Ship = 4;
        player.Items.Weapons[0].Id = 5;
        player.Items.Weapons[0].Power = 3;
        player.Items.Sidekicks[1] = 6;
        var calls = new List<string>();
        state.CurrentMenu = 1;
        MainMenuActionHandlers.SelectUpgrade(3, state, game,
            type => { calls.Add($"cash:{type}"); return 75; },
            type => { calls.Add($"generate:{type}"); return new UpgradeSubmenuState(6, 4, []); },
            () => calls.Add("weapon-view"));
        Check(calls.SequenceEqual(new[] { "generate:3", "weapon-view", "cash:3" }) &&
            state.CurrentMenu == 4 && state.LastDirection == 1 && state.LastUpgradeSelection == 4 &&
            player.Cash == 125 && player.LastItems.Ship == 4 && player.LastItems.Weapons[0].Power == 3,
            "Upgrade entry did not preserve native snapshot, setup order, selection, or cash conversion.");
        player.Items.Ship = 9; player.Items.Weapons[0].Power = 1; player.Items.Sidekicks[1] = 2;
        Check(player.LastItems.Ship == 4 && player.LastItems.Weapons[0].Power == 3 &&
            player.LastItems.Sidekicks[1] == 6,
            "Upgrade rollback snapshot aliased the live player's nested item arrays.");
        calls.Clear();
        MainMenuActionHandlers.SelectUpgrade(9, state, game,
            _ => throw new InvalidOperationException("Done selection should not inspect cash."),
            _ => throw new InvalidOperationException("Done selection should not build a submenu."),
            () => throw new InvalidOperationException("Done selection should not initialize the weapon view."));
        Check(state.CurrentMenu == 0, "Upgrade Done selection did not return to the full-game menu.");

        var completionSounds = new List<byte>();
        MainMenuActionHandlers.SelectUpgradeSubmenu(3, 6, state, player, () => 0,
            completionSounds.Add);
        Check(state.LastUpgradeSelection == 6 && player.Cash == 125 && completionSounds.Count == 0,
            "Selecting an upgrade item did not move selection to Done without committing.");
        MainMenuActionHandlers.SelectUpgradeSubmenu(6, 6, state, player, () => 90,
            completionSounds.Add);
        Check(state.CurrentMenu == 1 && player.Cash == 90 && completionSounds.SequenceEqual(new byte[] { 9 }),
            "Selecting upgrade Done did not commit cash, play the item sound, and return to upgrades.");

        state.CurrentMenu = 7; state.NewPalette = 0;
        state.FirstDataCubePage = false; state.DataCubeScroll = 9;
        state.DataCubeScrollDelta = -2; state.CurrentDataCube = 5;
        MainMenuActionHandlers.SelectDataCube(3, 6, 4, state);
        Check(state.CurrentMenu == 8 && state.FirstDataCubePage && state.DataCubeScroll == 0 &&
            state.DataCubeScrollDelta == 0 && state.CurrentDataCube == 1,
            "Data-cube selection did not initialize its subpage and scroll state.");
        state.CurrentMenu = 7; state.NewPalette = 0;
        MainMenuActionHandlers.SelectDataCube(6, 6, 4, state);
        Check(state.CurrentMenu == 0 && state.NewPalette == 1,
            "Data-cube Done selection did not return to the full-game menu.");
        state.CurrentMenu = 7; state.NewPalette = 0;
        MainMenuActionHandlers.SelectDataCube(3, 6, 0, state);
        Check(state.CurrentMenu == 0 && state.NewPalette == 1,
            "An empty data-cube menu did not return to the full-game menu.");

        var saveHost = new SaveMenuHost(["slot-a", "slot-b"]);
        state.QuickSave = false; state.OldMenu = 10; state.OldPalette = 18;
        state.CurrentMenu = 6; state.NewPalette = 0;
        MainMenuActionHandlers.SelectLoadSave(13, state, saveHost);
        Check(state.CurrentMenu == 2 && state.NewPalette == 0 && saveHost.Builds == 0,
            "Ordinary load/save Done selection did not return to options without listing saves.");
        state.QuickSave = true;
        MainMenuActionHandlers.SelectLoadSave(13, state, saveHost);
        Check(state.CurrentMenu == 10 && state.NewPalette == 18,
            "Quick-save Done selection did not restore the previous menu and palette.");

        state.QuickSave = false; state.PerformSave = true; state.SaveGamePageIndex = 0;
        MainMenuActionHandlers.SelectLoadSave(2, state, saveHost);
        Check(saveHost.PopupRequests.SequenceEqual(new[] { true }) && saveHost.Builds == 0,
            "Save-name popup did not bypass save-list construction.");
        state.TwoPlayerMode = true;
        MainMenuActionHandlers.SelectLoadSave(3, state, saveHost);
        Check(saveHost.LastBuildPlayers == 2 && saveHost.Saved.SequenceEqual(new[] { "slot-a" }) &&
            saveHost.Disposals == 1,
            "Save selection did not apply page indexing, player count, and list disposal.");
        state.PerformSave = false;
        MainMenuActionHandlers.SelectLoadSave(2, state, saveHost);
        Check(saveHost.Loaded.SequenceEqual(new[] { "slot-a" }) && saveHost.Disposals == 2,
            "Load selection did not select the first save and dispose its list.");
        state.QuickSave = true; state.OldMenu = 9; state.OldPalette = 17;
        MainMenuActionHandlers.SelectLoadSave(9, state, saveHost);
        Check(state.CurrentMenu == 9 && state.NewPalette == 17 && saveHost.Disposals == 3,
            "Quick-save out-of-range selection failed to restore prior state or release its list.");
    }

    private static void TwoPlayerInputDeviceParity(StringBuilder trace)
    {
        int[][] cases =
        [
            [3, -1, 0, 1, 2], [3, 1, 0, 1, 2], [4, -1, 0, 2, 1], [4, 1, 0, 2, 1],
            [3, -1, 1, 2, 1], [3, 1, 1, 2, 1], [4, -1, 1, 1, 3], [4, 1, 1, 1, 3],
            [3, -1, 3, 4, 1], [3, 1, 3, 4, 1], [4, -1, 3, 2, 5], [4, 1, 3, 2, 5]
        ];
        foreach (var test in cases)
        {
            var inputDevices = new byte[] { (byte)test[3], (byte)test[4] };
            trace.AppendLine($"input-device-case:{test[0]},{test[1]},{test[2]},{test[3]},{test[4]}");
            MainMenuActionHandlers.ChangeTwoPlayerInputDevice((byte)test[0], test[1], test[2],
                inputDevices, sample => trace.AppendLine($"sound:{sample}"));
            trace.AppendLine($"input-device-state:{inputDevices[0]},{inputDevices[1]}");
        }
    }

    private static void ItemScreenVolumeMouseParity(StringBuilder trace)
    {
        int[][] cases =
        [
            [220, 76, 0, 1, 1],
            [221, 70, 1, 1, 1],
            [224, 82, 0, 0, 1],
            [305, 82, 0, 1, 0],
            [309, 82, 0, 1, 1],
            [1000, 70, 0, 0, 0],
            [221, 86, 1, 0, 1],
            [309, 98, 1, 1, 0],
            [309, 85, 1, 1, 1]
        ];
        foreach (var test in cases)
        {
            var (x, y, selected, musicDisabled, samplesDisabled) =
                (test[0], test[1], test[2], test[3] != 0, test[4] != 0);
            var state = new ItemScreenVolumeState
            {
                OptionsSelection = (byte)selected,
                MusicVolume = 100,
                EffectsVolume = 80,
                MusicDisabled = musicDisabled,
                SamplesDisabled = samplesDisabled
            };
            trace.AppendLine($"volume-mouse-case:{x},{y},{selected}," +
                $"{Bit(musicDisabled)},{Bit(samplesDisabled)}");
            ItemScreenVolumeAdjustment.Adjust(x, y, state, new ItemScreenVolumeHost(trace));
            trace.AppendLine($"volume-mouse-state:{state.OptionsSelection},{state.MusicVolume}," +
                $"{state.EffectsVolume},{Bit(state.MusicDisabled)},{Bit(state.SamplesDisabled)}");
        }
    }

    private sealed class ItemScreenVolumeHost(StringBuilder trace) : IItemScreenVolumeHost
    {
        public void RestartSong() => trace.AppendLine("restart-song");
        public void SetVolume(ushort music, ushort effects)
            => trace.AppendLine($"set-volume:{music},{effects}");
        public void PlaySound(byte sample) => trace.AppendLine($"sound:{sample}");
    }

    private static void ItemScreenInputDispatchParity(StringBuilder trace)
    {
        int[][] cases = [[0, 0], [0, 1], [1, 0], [-1, 1], [-1, 0]];
        foreach (var test in cases)
        {
            var mouseButton = test[0];
            var newKey = test[1] != 0;
            trace.AppendLine($"input-dispatch-case:{mouseButton},{Bit(newKey)}");
            ItemScreenInputHandlers.DispatchInput(mouseButton, newKey,
                () => trace.AppendLine("dispatch:mouse"),
                () => trace.AppendLine("dispatch:key"));
        }
    }

    private static void ItemScreenOptionSelectionParity(StringBuilder trace)
    {
        int[][] cases =
        [
            [2, 2, 7, 3, 3, 100, 83, 1],
            [2, 3, 7, 3, 3, 100, 83, 1],
            [4, 2, 4, 3, 4, 100, 83, 1],
            [4, 2, 4, 3, 3, 100, 83, 6],
            [4, 2, 4, 4, 3, 100, 83, 1],
            [4, 3, 4, 4, 3, 100, 83, 1],
            [4, 2, 4, 4, 3, 100, 83, 6],
            [4, 2, 4, 3, 5, 100, 83, 1]
        ];
        foreach (var test in cases)
        {
            var (menu, currentSelection, choiceCount, upgradeSelection, selection, cash, cashLeft, multiplier) =
                (test[0], test[1], test[2], test[3], test[4], test[5], test[6], test[7]);
            var game = new LegacyGameState();
            var player = game.Players[0];
            player.Cash = (uint)cash;
            player.WeaponMode = 7;
            player.Items.Weapons[0].Power = 4;
            player.Items.Weapons[1].Power = 5;
            var state = new ItemScreenSelectionState
            {
                CurrentMenu = (byte)menu,
                CurrentSelection = (byte)currentSelection,
                ChoiceCount = (byte)choiceCount,
                UpgradeSelection = (byte)upgradeSelection
            };
            var mainMenuState = new MainMenuActionState { CurrentMenu = (byte)menu };
            var dispatchHost = new MenuFunctionHost(trace, game);
            var host = new ItemScreenSelectionHost(trace, (ulong)cashLeft, multiplier,
                mainMenuState, dispatchHost);
            int[] temporaryPower = [9, 10, 11, 12, 13, 14, 15];
            byte[] availableItems = [10, 20, 30, 40];
            trace.AppendLine($"selection-case:{menu},{currentSelection},{choiceCount}," +
                $"{upgradeSelection},{selection},{cash},{cashLeft},{multiplier}");
            ItemScreenMenuSelection.SelectOption(selection, availableItems, temporaryPower,
                player, state, host);
            trace.Append("selection-state:");
            trace.Append($"{state.CurrentMenu},{state.CurrentSelection},{player.Cash},{player.WeaponMode},");
            trace.Append($"{player.Items.Weapons[0].Power},{player.Items.Weapons[1].Power},");
            trace.AppendJoin(',', temporaryPower);
            trace.AppendLine();
        }
    }

    private sealed class ItemScreenSelectionHost(StringBuilder trace, ulong cashLeft, int multiplier,
        MainMenuActionState menuState, IMainMenuFunctionHost dispatchHost) : IItemScreenSelectionHost
    {
        public void PlaySound(byte sample) => trace.AppendLine($"sound:{sample}");
        public ulong GetUpgradeCost(byte upgradeSelection, byte itemId)
        {
            _ = upgradeSelection;
            return (ulong)itemId * (uint)multiplier;
        }
        public ulong CalculateCashLeft() => cashLeft;
        public void DispatchMenuFunction(byte menu, byte selection)
            => MainMenuFunction.Dispatch(menu, selection, selection, menuState, dispatchHost);
    }

    private static void ItemScreenWeaponPowerMouseParity(StringBuilder trace)
    {
        int[][] cases =
        [
            [4, 23, 149, 3, 2, 1, 0, 0],
            [4, 36, 168, 3, 2, 0, 0, 0],
            [4, 119, 149, 4, 5, 0, 1, 1],
            [4, 131, 168, 4, 5, 0, 1, 0],
            [4, 37, 149, 3, 2, 1, 1, 1],
            [2, 23, 149, 3, 2, 1, 1, 1]
        ];
        foreach (var test in cases)
        {
            var (menu, x, y, upgrade, submenu, left, right, affordable) =
                (test[0], test[1], test[2], test[3], test[4], test[5] != 0, test[6] != 0, test[7] != 0);
            var state = new ItemScreenHorizontalState
            {
                UpgradeSelection = (byte)upgrade,
                LeftPowerAvailable = left,
                RightPowerAvailable = right,
                RightPowerAffordable = affordable
            };
            var player = new PlayerState(0);
            player.Items.Weapons[upgrade - 3].Power = 7;
            int[] temporaryPower = [10, 11, 12, 13, 14, 15, 16];
            trace.AppendLine($"weapon-mouse-case:{menu},{x},{y},{upgrade},{submenu}," +
                $"{Bit(left)},{Bit(right)},{Bit(affordable)}");
            ItemScreenInputHandlers.AdjustWeaponPowerOnMouse((byte)menu, x, y, (byte)submenu,
                state, temporaryPower, player, sample => trace.AppendLine($"sound:{sample}"),
                () => trace.AppendLine("wait-noinput"));
            trace.Append("weapon-mouse-state:");
            trace.AppendJoin(',', temporaryPower);
            trace.AppendLine($",{player.Items.Weapons[upgrade - 3].Power}");
        }
    }

    private static void ItemScreenWaitParity(StringBuilder trace)
    {
        int[][] cases =
        [
            [2,1,0,0,0,0,0,0,0,0,0,0,0,0,12,14,0,31,42],
            [2,0,1,0,0,0,0,0,0,0,0,0,0,0,12,14,0,51,62],
            [2,1,0,0,1,0,0,0,0,0,0,0,0,0,12,14,0,71,82],
            [2,1,0,0,0,1,1,0,0,0,0,0,0,0,12,14,0,91,102],
            [7,1,0,0,1,1,0,0,0,0,0,0,0,0,12,14,0,111,122],
            [6,1,0,0,1,1,1,0,0,0,0,0,0,0,12,14,0,131,142],
            [8,0,1,1,0,0,0,0,0,0,0,0,0,0,12,14,0,151,162],
            [8,0,1,2,0,0,0,0,0,0,0,0,0,0,120,20,0,171,182],
            [8,0,0,0,0,0,0,0,1,1,1,0,0,0,120,20,0,191,202],
            [8,1,0,0,0,0,0,1,1,1,1,1,1,2,132,20,0,211,222],
            [8,0,0,0,0,0,0,0,0,0,0,1,0,2,12,14,1,231,242],
            [8,0,0,3,0,0,0,0,0,0,0,0,0,0,12,14,0,251,262],
            [8,0,0,0,0,0,0,0,0,0,0,0,0,0,0,14,-1,271,282],
            [8,0,0,0,0,0,0,0,0,0,0,0,0,0,12,9,1,291,302]
        ];
        foreach (var test in cases)
        {
            var state = new ItemScreenWaitState
            {
                CurrentPalette = 6,
                NewKey = test[1] != 0,
                MouseButton = test[2],
                MouseCursor = (ItemScreenMouseCursor)test[3],
                AltSavePressed = test[4] != 0,
                AltLoadPressed = test[5] != 0,
                PageUpPressed = test[7] != 0,
                PageDownPressed = test[8] != 0,
                UpPressed = test[9] != 0,
                DownPressed = test[10] != 0,
                YLocation = test[14],
                CubeLastLine = test[15],
                YChange = test[16]
            };
            var menu = state.Menu;
            menu.CurrentMenu = (byte)test[0];
            menu.QuickSave = false;
            menu.SaveGamePageIndex = 7;
            menu.PerformSave = false;
            menu.OldMenu = 9;
            menu.NewPalette = 9;
            menu.OldPalette = 17;
            var joystickDirections = Enumerable.Range(0, test[13])
                .Select(index => new ItemScreenJoystickDirection(
                    index == 0 && test[11] != 0, index == 1 && test[12] != 0)).ToArray();
            var host = new ItemScreenWaitHost(trace, test[2], test[17], test[18]);
            trace.AppendLine($"wait-case:{string.Join(",", test)}");
            var result = ItemScreenInputWait.WaitForInput(state, host, joystickDirections);
            trace.AppendLine($"wait-state:{Bit(result)},{state.YChange},{menu.CurrentMenu}," +
                $"{Bit(menu.QuickSave)},{menu.OldMenu},{menu.SaveGamePageIndex},{Bit(menu.PerformSave)}," +
                $"{menu.NewPalette},{menu.OldPalette},{state.MouseButton},{state.MouseX},{state.MouseY}," +
                $"{Bit(state.InputDetected)}");
        }
    }

    private sealed class ItemScreenWaitHost(StringBuilder trace, int button, int mouseX, int mouseY)
        : IItemScreenWaitHost
    {
        public void WaitDelay() => trace.AppendLine("wait-delay");
        public void PushJoysticksAsKeyboard() => trace.AppendLine("joystick");
        public void ServiceEvents(ItemScreenWaitState state) { _ = state; }
        public (int Button, int X, int Y) GetMousePosition() => (button, mouseX, mouseY);
    }

    private static void ItemScreenKeyParity(StringBuilder trace)
    {
        int[][] cases =
        [
            [4,3,46,3,2,1,20,77],[4,4,45,1,2,1,20,77],[4,5,46,3,2,1,20,77],
            [2,3,46,3,2,1,20,77],[4,4,56,1,2,3,20,77],[4,3,56,1,2,1,20,77],
            [4,3,56,1,2,1,20,77],[2,2,44,1,2,1,20,77],[2,2,40,1,2,1,20,77],
            [2,2,88,1,2,1,20,77],[2,2,41,1,2,1,20,77],[2,2,58,1,2,1,20,77],
            [2,2,82,1,2,1,20,77],[2,2,81,1,2,1,20,77],[8,2,74,1,2,1,20,77],
            [8,2,77,1,2,1,20,77],[2,2,74,1,2,1,20,77],[2,2,77,1,2,1,20,77],
            [2,2,80,1,2,1,20,77],[2,2,79,1,2,1,20,77],[2,2,42,1,2,1,20,77]
        ];
        var definitions = new ItemDefinitions();
        definitions.ParseBundle(Encoding.Latin1.GetBytes(ItemDatabaseTests.Fixture(0) +
            "[weapon:0]\nopnum=3\n[weapon:39]\nopnum=2\n"), "item-screen-keys.tyrr");
        var weapons = ItemDatabase.FromDefinitions(definitions).Weapons;
        var temporaryPower = new int[7];
        var host = new ItemScreenKeyHost(trace);
        foreach (var test in cases)
        {
            var player = new PlayerState(0) { WeaponMode = (uint)test[5] };
            player.Items.Weapons[0] = new PlayerWeapon { Id = 1, Power = (byte)test[3] };
            player.Items.Weapons[1] = new PlayerWeapon { Id = 2, Power = (byte)test[4] };
            foreach (var weapon in weapons)
                Array.Clear(weapon.Op[0]);
            weapons[1].Op[0][2] = 300;
            var state = new ItemScreenKeyState
            {
                CurrentMenu = (byte)test[0],
                UpgradeSelection = (byte)test[1],
                CubeLastLine = test[6],
                CubeScrollLocation = test[7]
            };
            trace.AppendLine($"key-case:{string.Join(",", test)}");
            ItemScreenKeyHandler.Handle(test[2], state, player, weapons, temporaryPower, host);
            trace.AppendLine($"key-state:{player.WeaponMode},{weapons[1].Op[0][2]}," +
                $"{weapons[2].Op[0][1]},{state.CubeScrollLocation},{weapons[0].Op[0][0]}");
        }
    }

    private sealed class ItemScreenKeyHost(StringBuilder trace) : IItemScreenKeyHost
    {
        public void ActivateSelection(int[] temporaryPower) { _ = temporaryPower; trace.AppendLine("key-activate"); }
        public void CancelSelection() => trace.AppendLine("key-cancel");
        public void ShowHelp() => trace.AppendLine("key-help");
        public void MoveSelection(int direction, int[] temporaryPower)
        {
            _ = temporaryPower;
            trace.AppendLine($"key-move:{direction}");
        }
        public void HandleHorizontalKey(int direction, int[] temporaryPower)
        {
            _ = temporaryPower;
            trace.AppendLine($"key-horizontal:{direction}");
        }
    }

    private static void ItemScreenMouseInputParity(StringBuilder trace)
    {
        int[][] cases =
        [
            [7,0,4,1,100,100],[7,2,4,1,180,38],[8,2,4,1,270,165],
            [2,2,7,1,225,76],[2,2,7,1,171,21],[8,2,4,1,200,100],
            [7,0,4,1,200,60],[11,2,7,1,225,70],[2,2,7,1,308,76]
        ];
        foreach (var test in cases)
        {
            var state = new ItemScreenMouseInputState
            {
                CurrentMenu = (byte)test[0],
                CubeCount = test[1],
                ChoiceCount = (byte)test[2],
                MouseButton = test[3],
                MouseX = test[4],
                MouseY = test[5],
                NewPalette = 9,
                LastDirection = 99
            };
            trace.AppendLine($"mouse-input-case:{string.Join(",", test)}");
            ItemScreenMouseInput.Handle(state, new int[7],
                new ItemScreenMouseInputHost(trace, test[3], test[4], test[5]));
            trace.AppendLine($"mouse-input-state:{state.CurrentMenu},{state.LastDirection}," +
                $"{state.MouseButton},{state.MouseX},{state.MouseY},{state.NewPalette}");
        }
    }

    private sealed class ItemScreenMouseInputHost(StringBuilder trace, int button, int mouseX, int mouseY)
        : IItemScreenMouseInputHost
    {
        public (int Button, int X, int Y) GetMousePosition() => (button, mouseX, mouseY);
        public void PlaySound(byte sample) => trace.AppendLine($"sound:{sample}");
        public void WipeKey() => trace.AppendLine("wipe-key");
        public void AdjustVolume(int x, int y) => trace.AppendLine($"mouse-volume:{x},{y}");
        public void SelectMenuOption(byte menu, int selection, int[] temporaryPower)
        {
            _ = temporaryPower;
            trace.AppendLine($"mouse-select:{menu},{selection}");
        }
        public void WaitNoInput(bool keyboard, bool mouse, bool joystick)
            => trace.AppendLine($"wait-noinput:{Bit(keyboard)},{Bit(mouse)},{Bit(joystick)}");
        public void AdjustWeaponPower(byte menu, int x, int y, int[] temporaryPower)
        {
            _ = temporaryPower;
            trace.AppendLine($"mouse-power:{menu},{x},{y}");
        }
    }

    private static void ItemScreenSelectionMovementParity(StringBuilder trace)
    {
        int[][] cases =
        [
            [2,2,4,-1,0,2,5,6,5,0,0,0],
            [2,4,4,1,0,2,5,6,5,0,0,0],
            [8,2,4,-1,0,2,5,6,5,0,0,0],
            [4,2,5,1,3,3,5,6,5,0,0,0],
            [4,4,5,1,4,2,5,6,5,0,0,0],
            [12,4,7,1,0,2,5,6,5,1,0,0],
            [12,6,7,-1,0,2,5,6,5,1,0,0],
            [12,3,7,1,0,2,5,6,5,1,0,0],
            [12,4,7,-1,0,2,5,6,5,1,0,0],
            [12,4,7,1,0,2,5,6,5,0,0,0],
            [12,3,7,1,0,2,5,6,5,1,0,1],
            [2,2,2,-1,0,2,5,6,5,0,0,0]
        ];
        foreach (var test in cases)
        {
            var state = new ItemScreenMoveSelectionState
            {
                CurrentMenu = (byte)test[0],
                CurrentSelection = (byte)(test[0] == 4 ? test[5] : test[1]),
                ChoiceCount = (byte)test[2],
                UpgradeSelection = (byte)test[4],
                JoystickCount = test[9],
                JoystickConfig = test[10],
                JoystickAnalog = [test[11] != 0]
            };
            var player = new PlayerState(0)
            {
                WeaponMode = (uint)test[8]
            };
            player.Items.Weapons[0].Power = (byte)test[6];
            player.Items.Weapons[1].Power = (byte)test[7];
            int[] temporaryPower = [10, 11, 12, 13, 14, 15, 16];
            trace.AppendLine($"move-case:{string.Join(",", test)}");
            ItemScreenSelectionMovement.Move(test[3], state, player, temporaryPower,
                sample => trace.AppendLine($"sound:{sample}"));
            trace.AppendLine($"move-state:{Bit(state.KeyboardUsed)},{state.LastDirection}," +
                $"{state.CurrentSelection},{player.Items.Weapons[0].Power}," +
                $"{player.Items.Weapons[1].Power},{player.WeaponMode}");
        }
    }

    private static void ItemScreenMouseSelectionParity(StringBuilder trace)
    {
        int[] mouseYs = [-100, 0, 20, 37, 38, 39, 54, 70, 200, 1000];
        byte[] choiceCounts = [0, 4, 255];
        for (byte menu = 0; menu < 14; menu++)
        {
            if (menu == 8)
                continue;
            foreach (var y in mouseYs)
                foreach (var count in choiceCounts)
                {
                    trace.AppendLine($"mouse-selection-case:{menu},{y},{count}");
                    trace.AppendLine($"mouse-selection:{ItemScreenInputHandlers.GetMouseSelection(menu, y, count)}");
                }
        }
    }

    private static void QuickSaveOpeningParity(StringBuilder trace)
    {
        int[][] cases = [[8, 1, 17], [7, 0, 9], [6, 1, 3], [2, 0, 5]];
        foreach (var test in cases)
        {
            var (menu, save, palette) = (test[0], test[1] != 0, test[2]);
            var state = new MainMenuActionState
            {
                CurrentMenu = (byte)menu,
                QuickSave = false,
                SaveGamePageIndex = 7,
                PerformSave = !save
            };
            trace.AppendLine($"quick-save-case:{menu},{Bit(save)},{palette}");
            ItemScreenInputHandlers.OpenQuickSave((byte)menu, save, (byte)palette, state);
            trace.AppendLine($"quick-save-state:{Bit(state.QuickSave)},{state.OldMenu},{state.CurrentMenu}," +
                $"{state.SaveGamePageIndex},{Bit(state.PerformSave)},{state.NewPalette},{state.OldPalette}");
        }
    }

    private static void ItemScreenCancellationParity(StringBuilder trace)
    {
        int[][] cases =
        [
            [6, 1, 9, 17, 1, 3, 2],
            [4, 2, 8, 17, 0, 6, 3],
            [8, 8, 9, 17, 0, 4, 4],
            [5, 3, 9, 17, 0, 7, 2],
            [7, 1, 9, 17, 0, 2, 5],
            [12, 3, 9, 17, 0, 5, 4]
        ];
        foreach (var test in cases)
        {
            var (menu, escapeDestination, oldMenu, oldPalette, quickSave, selected, lastSelection) =
                (test[0], test[1], test[2], test[3], test[4] != 0, test[5], test[6]);
            var state = new MainMenuActionState
            {
                CurrentMenu = (byte)menu,
                NewPalette = 17,
                QuickSave = quickSave,
                OldMenu = (byte)oldMenu,
                OldPalette = (byte)oldPalette,
                LastUpgradeSelection = (byte)lastSelection
            };
            var player = new PlayerState(0) { Cash = 123 };
            player.LastItems.Ship = 10;
            player.LastItems.Weapons[0] = new PlayerWeapon { Id = 5, Power = 3 };
            player.Items.Ship = 30;
            player.Items.Weapons[0] = new PlayerWeapon { Id = 9, Power = 8 };
            byte submenuSelection = (byte)selected;
            var keyboardUsed = false;
            var quitRequests = 0;
            trace.AppendLine($"cancel-case:{menu},{escapeDestination},{oldMenu},{oldPalette}," +
                $"{Bit(quickSave)},{selected},{lastSelection}");
            ItemScreenInputHandlers.CancelSelection((byte)menu, (byte)escapeDestination, state,
                player, ref submenuSelection, ref keyboardUsed, () => 83,
                () => trace.AppendLine("sound:16"), () => { quitRequests++; trace.AppendLine("quit-request"); });
            trace.AppendLine($"cancel-state:{Bit(keyboardUsed)},{state.CurrentMenu},{state.NewPalette}," +
                $"{player.Cash},{player.Items.Ship},{player.Items.Weapons[0].Id}," +
                $"{player.Items.Weapons[0].Power},{submenuSelection}");
            Check(quitRequests == 0, "A non-root item-screen cancellation requested a game quit.");
        }

        var quitState = new MainMenuActionState { CurrentMenu = 0, NewPalette = 9 };
        var quitPlayer = new PlayerState(0);
        byte selection = 2;
        var wasKeyboardUsed = false;
        var quitCount = 0;
        ItemScreenInputHandlers.CancelSelection(0, 0, quitState, quitPlayer, ref selection,
            ref wasKeyboardUsed, () => 0, () => { }, () => quitCount++);
        Check(wasKeyboardUsed && quitCount == 1 && quitState.CurrentMenu == 0 && quitState.NewPalette == 9,
            "Root-menu cancellation did not request quit while preserving menu state.");
    }

    private static void ItemScreenHelpParity(StringBuilder trace)
    {
        int[][] cases = [[3, 0], [4, 0], [8, 0], [2, 1]];
        foreach (var test in cases)
        {
            var menu = (byte)test[0];
            var networkGame = test[1] != 0;
            var state = new ItemScreenHelpState { NewPalette = 9, CurrentPalette = 6 };
            var host = new ItemScreenHelpTraceHost(trace);
            trace.AppendLine($"help-case:{menu},{Bit(networkGame)}");
            ItemScreenHelp.Show(menu, networkGame, 5, state, host);
            trace.AppendLine($"help-state:{state.NewPalette},{state.CurrentPalette},{Bit(state.BackFromHelp)}," +
                $"{Hash(host.Primary)},{Hash(host.Colors)},{Hash(host.Background)}");
        }
    }

    private sealed class ItemScreenHelpTraceHost(StringBuilder trace) : IItemScreenHelpHost
    {
        internal byte[] Primary { get; } = Enumerable.Range(0, 64000)
            .Select(i => unchecked((byte)(i * 29 + 7))).ToArray();
        internal byte[] Background { get; } = Enumerable.Range(0, 64000)
            .Select(i => unchecked((byte)(i * 31 + 13))).ToArray();
        internal byte[] Colors { get; } = new byte[1024];

        public void FadeBlack(int steps) => trace.AppendLine($"black:{steps}");
        public void RunHelpSystem(byte topic) => trace.AppendLine($"help-system:{topic}");
        public void PlaySong(byte song) => trace.AppendLine($"song:{song}");
        public void LoadPicture(byte picture, bool loadPalette)
        {
            trace.AppendLine("picture");
            Primary.AsSpan().Fill(0xA8);
        }
        public void CopyPrimaryToBackground()
        {
            Primary.CopyTo(Background, 0);
        }
        public void ApplyPalette(byte palette)
        {
            for (var i = 0; i < Colors.Length; i++)
                Colors[i] = unchecked((byte)((palette - 1) * 13 + i * 7));
        }
        public void Present() => trace.AppendLine($"show:{Hash(Primary)}");
    }

    private static void ItemScreenActivationParity(StringBuilder trace)
    {
        int[][] cases =
        [
            [0, 4, 3, 2, 2],
            [4, 5, 3, 3, 3],
            [4, 6, 4, 4, 4],
            [4, 8, 5, 5, 2],
            [5, 7, 4, 6, 1],
            [13, 3, 3, 2, 3]
        ];
        foreach (var test in cases)
        {
            var (menu, selection, upgrade, itemCount, initialPower) =
                (test[0], test[1], test[2], test[3], test[4]);
            var game = new LegacyGameState();
            var weaponSlot = upgrade is 3 or 4 ? upgrade - 3 : 0;
            game.Players[0].Items.Weapons[weaponSlot].Power = (byte)(initialPower + 10);
            game.Players[0].Items.Ship = (byte)(20 + menu);
            var state = new MainMenuActionState
            {
                CurrentMenu = (byte)menu,
                MenuColor = 77,
                MenuColorDirection = 9
            };
            var temporaryPower = Enumerable.Range(0, 7).Select(i => initialPower + i).ToArray();
            var keyboardUsed = false;
            var host = new MenuFunctionHost(trace, game);
            trace.AppendLine($"activate-case:{menu},{selection},{upgrade},{itemCount},{initialPower}");
            ItemScreenInputHandlers.ActivateSelection((byte)menu, (byte)selection, (byte)upgrade,
                itemCount, temporaryPower, game.Players[0], ref keyboardUsed, state, host);
            trace.AppendLine($"activate-state:{Bit(keyboardUsed)},{string.Join(',', temporaryPower)}," +
                $"{game.Players[0].Items.Weapons[weaponSlot].Power},{state.CurrentMenu}," +
                $"{state.MenuColor},{state.MenuColorDirection},{host.SnapshotShip}");
        }
    }

    private static void HorizontalInputParity(StringBuilder trace)
    {
        int[][] cases =
        [
            [2, 4, 3, 1, 1, 1, 0, 1, 1],
            [2, 4, 3, -1, 0, 1, 1, 0, 0],
            [2, 5, 3, -1, 0, 0, 0, 0, 1],
            [11, 5, 3, 1, 0, 0, 0, 0, 1],
            [4, 2, 3, -1, 1, 0, 0, 0, 0],
            [4, 2, 3, 1, 0, 1, 1, 0, 0],
            [4, 2, 4, 1, 0, 1, 0, 0, 0],
            [12, 3, 3, -1, 0, 0, 0, 0, 0],
            [9, 4, 3, 1, 0, 0, 0, 0, 0],
            [0, 4, 3, 1, 0, 0, 0, 0, 0]
        ];
        foreach (var test in cases)
        {
            var (menu, selection, upgrade, direction, left, right, affordable, musicDisabled, samplesDisabled) =
                (test[0], test[1], test[2], test[3], test[4] != 0, test[5] != 0,
        test[6] != 0, test[7] != 0, test[8] != 0);
            var state = new ItemScreenHorizontalState
            {
                UpgradeSelection = (byte)upgrade,
                JoystickSelection = (byte)selection,
                LeftPowerAvailable = left,
                RightPowerAvailable = right,
                RightPowerAffordable = affordable,
                MusicDisabled = musicDisabled,
                SamplesDisabled = samplesDisabled
            };
            var actionState = new MainMenuActionState();
            actionState.InputDevices[0] = 1;
            actionState.InputDevices[1] = 2;
            var player = new PlayerState(0);
            player.Items.Weapons[upgrade - 3].Power = 7;
            var temporaryPower = Enumerable.Range(0, 7).Select(i => i + 10).ToArray();
            var volume = new HorizontalVolume();
            var host = new HorizontalInputHost(trace, actionState, player, temporaryPower,
                state, volume);
            trace.AppendLine($"horizontal-case:{menu},{selection},{upgrade},{direction}," +
                $"{Bit(left)},{Bit(right)},{Bit(affordable)},{Bit(musicDisabled)},{Bit(samplesDisabled)}");
            ItemScreenInputHandlers.HandleHorizontal((byte)menu, (byte)selection, direction, state, host);
            trace.AppendLine($"horizontal-state:{Bit(state.MusicDisabled)},{Bit(state.SamplesDisabled)}," +
                $"{actionState.InputDevices[0]},{actionState.InputDevices[1]},{volume.Music},{volume.Effects}," +
                $"{string.Join(',', temporaryPower)},{player.Items.Weapons[upgrade - 3].Power}");
        }
    }

    private sealed class HorizontalVolume
    {
        internal ushort Music = 100;
        internal ushort Effects = 80;
    }

    private sealed class HorizontalInputHost(StringBuilder trace, MainMenuActionState actionState,
        PlayerState player, int[] temporaryPower, ItemScreenHorizontalState state,
        HorizontalVolume volume) : IItemScreenHorizontalHost
    {
        public void AdjustJoystick(byte selection, int direction)
            => trace.AppendLine($"adjust-joystick:{selection},{direction}");
        public void ChangePlayerDevice(int direction)
            => MainMenuActionHandlers.ChangeTwoPlayerInputDevice(state.JoystickSelection, direction, 0,
                actionState.InputDevices, sample => PlaySound(sample));
        public void PlaySound(byte sample) => trace.AppendLine($"sound:{sample}");
        public void ChangeVolume(int musicDelta, int effectsDelta)
        {
            trace.AppendLine($"volume:{musicDelta},{effectsDelta}");
            volume.Music = unchecked((ushort)(volume.Music + musicDelta));
            volume.Effects = unchecked((ushort)(volume.Effects + effectsDelta));
        }
        public void RestartSong() => trace.AppendLine("restart-song");
        public void ChangeWeaponPower(int direction, bool canChange)
        {
            var weapon = state.UpgradeSelection;
            MainMenuActionHandlers.ChangeUpgradeWeaponPower(weapon,
                2, direction, canChange, temporaryPower, player, PlaySound);
        }
    }

    private static void MainMenuFunctionParity(StringBuilder trace)
    {
        var game = new LegacyGameState();
        var state = new MainMenuActionState();
        for (byte menu = 0; menu <= 14; menu++)
        {
            var selection = (byte)(90 + menu);
            var currentSelection = (ushort)(40 + menu);
            state.CurrentMenu = menu;
            state.MenuColor = 77;
            state.MenuColorDirection = 9;
            game.Players[0].Items.Ship = (byte)(20 + menu);
            var host = new MenuFunctionHost(trace, game);
            trace.AppendLine($"menu-dispatch-case:{menu},{selection},{currentSelection}");
            MainMenuFunction.Dispatch(menu, selection, currentSelection, state, host);
            trace.AppendLine($"menu-dispatch-state:{state.CurrentMenu},{state.MenuColor}," +
                $"{state.MenuColorDirection},{host.SnapshotShip}");
        }
    }

    private sealed class MenuFunctionHost(StringBuilder trace, LegacyGameState game) : IMainMenuFunctionHost
    {
        internal byte SnapshotShip;
        public void PlaySound(byte sample) => trace.AppendLine($"sound:{sample}");
        public void HandleFullGame(byte selection) => trace.AppendLine($"dispatch:full:{selection}");
        public void HandleUpgradeMenu(byte selection) => trace.AppendLine($"dispatch:upgrade:{selection}");
        public void HandleOptions(byte selection) => trace.AppendLine($"dispatch:options:{selection}");
        public void HandleNextLevel(ushort selection) => trace.AppendLine($"dispatch:next:{selection}");
        public void HandleUpgradeSubmenu() => trace.AppendLine("dispatch:upgrade-submenu");
        public void HandleKeyboardConfiguration(ushort selection) => trace.AppendLine($"dispatch:keyboard:{selection}");
        public void HandleLoadSave(ushort selection) => trace.AppendLine($"dispatch:load-save:{selection}");
        public void HandleDataCubes(ushort selection) => trace.AppendLine($"dispatch:data-cube:{selection}");
        public void HandleTwoPlayer() => trace.AppendLine("dispatch:two-player");
        public void HandleOnePlayer() => trace.AppendLine("dispatch:one-player");
        public void HandleLimitedOptions(byte selection) => trace.AppendLine($"dispatch:limited:{selection}");
        public void HandleJoystickConfiguration(byte selection) => trace.AppendLine($"dispatch:joystick:{selection}");
        public void HandleSuperTyrian() => trace.AppendLine("dispatch:super-tyrian");
        public void SnapshotPlayerItems() => SnapshotShip = game.Players[0].Items.Ship;
    }

    private static void UpgradeWeaponPowerParity(StringBuilder trace)
    {
        for (byte weaponSelection = 3; weaponSelection <= 4; weaponSelection++)
            foreach (byte submenuSelection in new byte[] { 2, 5, 8 })
                for (var canChange = 0; canChange <= 1; canChange++)
                    for (var direction = -1; direction <= 1; direction += 2)
                    {
                        var choice = submenuSelection - 2;
                        var temporaryPower = Enumerable.Range(0, 7).Select(i => i + 10).ToArray();
                        if (weaponSelection == 4 && submenuSelection == 8 && canChange != 0 && direction > 0)
                            temporaryPower[choice] = 255;
                        var player = new PlayerState(0);
                        player.Items.Weapons[weaponSelection - 3].Power = 7;
                        trace.AppendLine($"weapon-power-case:{weaponSelection},{submenuSelection},{direction},{canChange}");
                        MainMenuActionHandlers.ChangeUpgradeWeaponPower(weaponSelection, submenuSelection,
                            direction, canChange != 0, temporaryPower, player,
                            sample => trace.AppendLine($"sound:{sample}"));
                        trace.Append("weapon-power-state:");
                        trace.AppendJoin(',', temporaryPower);
                        trace.AppendLine($",{player.Items.Weapons[weaponSelection - 3].Power}");
                    }
    }

    private sealed class SaveMenuHost(string[] names) : ISaveMenuHost
    {
        internal int Builds;
        internal int Disposals;
        internal byte LastBuildPlayers;
        internal List<bool> PopupRequests { get; } = [];
        internal List<string> Saved { get; } = [];
        internal List<string> Loaded { get; } = [];

        public ISaveMenuList BuildSaveGameList(byte players)
        {
            Builds++;
            LastBuildPlayers = players;
            return new SaveList(names, () => Disposals++);
        }
        public void ShowSaveNamePopup(bool saving) => PopupRequests.Add(saving);
        public void SaveGame(string name) => Saved.Add(name);
        public void LoadGame(string name) => Loaded.Add(name);
    }

    private sealed class SaveList(string[] names, Action dispose) : ISaveMenuList
    {
        public int Count => names.Length;
        public string this[int index] => names[index];
        public IEnumerator<string> GetEnumerator() => ((IEnumerable<string>)names).GetEnumerator();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => names.GetEnumerator();
        public void Dispose() => dispose();
    }

    private sealed class KeyboardHost(InputState input, QuitPlatform platform, InputEvent[] scriptedEvents)
        : IKeyboardRebindingHost
    {
        private int _scriptIndex;
        public InputState Input => input;
        public IKeyboardNames KeyboardNames { get; } = new KeyboardNames();
        public string? KeyNameDrawn { get; private set; }
        public (int Left, int Top, int Right, int Bottom, int Color) LastRectangle { get; private set; }
        public int Clicks { get; private set; }

        public void DrawKeyName(int x, int y, string name, int foreground, int brightness)
        {
            Check((x, y, foreground, brightness) == (236, 62, 15, 6), "Keyboard name draw parameters changed.");
            KeyNameDrawn = name;
        }
        public void Present() { }
        public void SetDelay(uint ticks)
        {
            Check(ticks == 1, "Keyboard rebinding changed its animation delay.");
            if (_scriptIndex < scriptedEvents.Length)
                platform.Enqueue([scriptedEvents[_scriptIndex++]]);
        }
        public void DrawSelectionRectangle(int left, int top, int right, int bottom, int color)
            => LastRectangle = (left, top, right, bottom, color);
        public void WaitDelay() { }
        public void PlayClick() => Clicks++;
        public void WipeKey() { }
    }

    private sealed class KeyboardNames : IKeyboardNames
    {
        public int FromName(string name) => int.Parse(name, System.Globalization.CultureInfo.InvariantCulture);
        public string Name(int scancode) => scancode.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void SetModifiers(int modifiers);

    internal static void RunSdl(SdlVideoDevice video)
    {
        SdlInputPlatform? platform = null;
        var keys = new Queue<int>();
        var auxiliary = new MenuAuxiliary(() =>
        {
            Check(keys.Count > 0, "SDL menu input script exhausted.");
            platform!.PostEvent(new(InputEventKind.KeyDown, keys.Dequeue()));
        });
        using (platform = new SdlInputPlatform(video, auxiliary, () => throw new InvalidOperationException("Unexpected menu quit.")))
        {
            var path = Environment.GetEnvironmentVariable("SDL2_LIBRARY_PATH") ??
                (OperatingSystem.IsWindows() ? "SDL2.dll" : OperatingSystem.IsMacOS() ? "libSDL2.dylib" : "libSDL2-2.0.so.0");
            var library = NativeLibrary.Load(path);
            var set = Marshal.GetDelegateForFunctionPointer<SetModifiers>(NativeLibrary.GetExport(library, "SDL_SetModState"));
            var previous = platform.Modifiers;
            try
            {
                set(1);
                Check(platform.Modifiers == 1, "Menus cannot read live SDL modifier state.");
                set(0);
                var input = new InputState(platform, video.Layout);
                input.FlushEvents();
                var timer = new DelayTimer(input);
                var cursor = new MouseCursor(input, video.Primary, new SpriteSheet());
                var colors = Enumerable.Range(0, 256).Select(i => new PaletteColor((byte)i, (byte)(255 - i), (byte)(i / 2))).ToArray();
                var sounds = new List<byte>();
                var presents = 0;
                void Present() { video.Present(); presents++; }
                void BeginDelay() => timer.SetDelay(1);
                var host = new SelectionMenuPresentation(video.Primary, video.Secondary, new FontRenderer(new MainShapeTables()), cursor,
                    () => { }, () => video.Secondary.Pixels.Span.Fill(0xA8),
                    () => video.Palette.FadePalette(colors, 10, 0, 255, BeginDelay, Present, timer.Wait),
                    Present, sounds.Add,
                    steps => video.Palette.FadeBlack(steps, BeginDelay, Present, timer.Wait),
                    platform.PushJoysticksAsKeyboard, platform.CheckNetwork, () => platform.Modifiers, episode => episode);
                var state = new SelectionMenuState();
                var menus = new SelectionMenus(input, host, state);
                var names = new ReadOnlyMemory<byte>[7];
                keys.Enqueue(81); keys.Enqueue(40);
                Check(menus.SelectGameplay(names) && state.OnePlayerAction && !state.TwoPlayerMode,
                    "Real SDL input did not select arcade gameplay.");
                keys.Enqueue(81); keys.Enqueue(81); keys.Enqueue(40);
                Check(menus.SelectEpisode(names, [true, false, true, false]) && state.Episode == 3 && state.InitialEpisode == 3,
                    "Real SDL input did not commit the selected episode.");
                keys.Enqueue(41);
                state.Difficulty = 6;
                Check(!menus.SelectDifficulty(names) && state.Difficulty == 6, "Real SDL difficulty cancellation changed state.");
                Check(sounds.SequenceEqual(new byte[] { 28, 8, 28, 28, 8, 16 }) && auxiliary.KeepAlives == 4,
                    "SDL menu sound or keep-alive ordering changed.");
                Check(presents >= 70 && video.Palette.Active.Span.ToArray().All(c => c == new PaletteColor(0, 0, 0)),
                    "Menu fades did not reach real SDL presentation or finish black.");
            }
            finally { set(previous); NativeLibrary.Free(library); }
        }
    }

    private sealed class MenuAuxiliary(Action push) : IInputAuxiliaryServices
    {
        internal int KeepAlives;
        public bool JoystickDown => false;
        public bool IsNetworkGame => true;
        public void PushJoysticksAsKeyboard() => push();
        public void PollJoysticks() => throw new InvalidOperationException();
        public void CheckNetwork() => KeepAlives++;
    }

    private static void ItemScreenInitializationParity(StringBuilder trace)
    {
        var state = new ItemScreenInitializationState
        {
            CurrentPalette = 6, NewPalette = 9, Color = 44, GameLoaded = true,
            CurrentItemType = 9, Cursor = 7, CurrentItem = 99, CurrentMenu = 11,
            KeyboardUsed = true, FirstMenu9 = true, BackFromHelp = true
        };
        Array.Fill(state.MenuChoices, (byte)0xA5);
        Array.Fill(state.CurrentSelections, (byte)0xA5);
        var availability = new EpisodeItemAvailabilityState();
        availability.Counts[0] = 1; availability.Items[0, 0] = 5;
        availability.Counts[1] = 1; availability.Items[1, 0] = 3;
        availability.Counts[2] = 5;
        for (var slot = 0; slot < 5; slot++) availability.Items[2, slot] = (byte)(slot + 1);
        availability.Counts[3] = 1; availability.Items[3, 0] = 4;
        availability.Counts[8] = 1; availability.Items[8, 0] = 0;
        var lastItems = new PlayerItems
        {
            Ship = 5, Shield = 0, Generator = 4
        };
        lastItems.Weapons[0].Id = 9;
        lastItems.Weapons[1].Id = 9;
        lastItems.Sidekicks[0] = 7;
        lastItems.Sidekicks[1] = 2;

        var primary = Enumerable.Range(0, 64000).Select(i => unchecked((byte)(i * 29 + 7))).ToArray();
        var gamePixels = Enumerable.Repeat((byte)0x33, 64000).ToArray();
        var backgroundPixels = Enumerable.Repeat((byte)0xCC, 64000).ToArray();
        var colors = Enumerable.Range(0, 1024).Select(i => unchecked((byte)(i * 3 + 1))).ToArray();
        using var warnings = new StringWriter();
        var gameScreen = new IndexedSurface(320, 200, 320, gamePixels, warnings);
        var backgroundScreen = new IndexedSurface(320, 200, 320, backgroundPixels, warnings);
        var host = new ItemScreenInitializationHost(trace, primary);
        byte[] menuChoicesDefault = [7, 9, 8, 0, 0, 11, 13, 0, 0, 6, 4, 6, 7, 5];

        trace.AppendLine("item-init-missing-sheet-case");
        ItemScreenInitializer.Initialize(state, availability, lastItems, menuChoicesDefault, colors,
            gameScreen, backgroundScreen, host);
        Check(ReferenceEquals(state.ActiveScreen, gameScreen), "Item-screen initialization did not select the game screen.");
        TraceItemScreenInitialization(trace, state, availability, backgroundScreen);

        trace.AppendLine("item-init-cached-sheet-case");
        ItemScreenInitializer.Initialize(state, availability, lastItems, menuChoicesDefault, colors,
            gameScreen, backgroundScreen, host);
        TraceItemScreenInitialization(trace, state, availability, backgroundScreen);
        Check(warnings.ToString().Length == 0, "Item-screen initialization emitted surface warnings.");
        ItemScreenFramePreparationParity(trace);
        ItemScreenStatusDrawingParity(trace);
        ItemScreenDataCubeDrawingParity(trace);
        ItemScreenFooterDrawingParity(trace);
        ItemScreenLoadSaveDrawingParity(trace);
        ItemScreenKeyboardConfigurationDrawingParity(trace);
        ItemScreenJoystickConfigurationDrawingParity(trace);
    }

    private static void ItemScreenFramePreparationParity(StringBuilder trace)
    {
        int[][] cases =
        [
            [0, 1, 1, 1, 1, 0, 2, 1],
            [8, 0, 0, 0, 0, 0, 2, 0],
            [8, 0, 0, 0, 0, 1, 2, 0],
            [1, 0, 0, 0, 0, 0, 3, 0],
            [3, 0, 0, 0, 0, 0, 2, 1]
        ];
        using var warnings = new StringWriter();
        foreach (var (test, c) in cases.Select((test, index) => (test, index)))
        {
            var state = new ItemScreenFramePreparationState
            {
                CurrentMenu = (byte)test[0],
                TwoPlayerMode = test[1] != 0,
                NetworkGame = test[2] != 0,
                OnePlayerAction = test[3] != 0,
                SuperTyrian = test[4] != 0,
                FirstMenu9 = test[5] != 0,
                PaletteChanged = true,
                LeftPowerAvailable = true,
                RightPowerAvailable = true,
                KeyboardUsed = test[7] != 0,
                PlanetAnimation = 99,
                PlanetAnimationWait = 99,
                CurrentDotNumber = 99,
                CurrentDotWait = 99
            };
            Array.Fill(state.CurrentSelections, (byte)2);
            state.CurrentSelections[1] = (byte)test[6];
            var availability = new EpisodeItemAvailabilityState();
            availability.Counts[1] = 3;
            availability.Items[1, 0] = 4;
            availability.Items[1, 1] = 8;
            availability.Items[1, 2] = 9;
            var player = new PlayerState(0);
            player.Items.Weapons[0].Id = 8;
            player.Items.Weapons[0].Power = 4;
            player.Items.Weapons[1].Id = 9;
            player.Items.Weapons[1].Power = 5;
            int[] temporaryPower = [77, 77, 77, 77, 77, 77, 77];
            var screen = new IndexedSurface(320, 200, 320,
                Enumerable.Repeat((byte)0x35, 64000).ToArray(), warnings);
            var background = new IndexedSurface(320, 200, 320,
                Enumerable.Repeat((byte)0xC7, 64000).ToArray(), warnings);
            var host = new ItemScreenFramePreparationHost(trace);

            trace.AppendLine($"item-prepare-case:{c}");
            ItemScreenFramePreparation.Prepare(state, temporaryPower, availability, player,
                screen, background, host);
            trace.AppendLine($"item-prepare-state:{state.CurrentMenu},{Bit(state.PaletteChanged)}," +
                $"{Bit(state.LeftPowerAvailable)},{Bit(state.RightPowerAvailable)}," +
                $"{Bit(state.KeyboardUsed)},{state.PlanetAnimation},{state.CurrentDotNumber}," +
                $"{state.CurrentDotWait},{state.PlanetAnimationWait},{temporaryPower[0]}," +
                $"{temporaryPower[1]},{temporaryPower[2]},{temporaryPower[3]},{temporaryPower[4]}");
            var powerBytes = temporaryPower.Select(value => unchecked((byte)value)).ToArray();
            trace.AppendLine($"item-prepare-hash:{Hash(screen.Pixels.Span)}," +
                $"{Hash(background.Pixels.Span)},{Hash(powerBytes)}");
        }
        Check(warnings.ToString().Length == 0, "Item-screen frame preparation emitted surface warnings.");
        ItemScreenDrawingParity(trace);
    }

    private static void ItemScreenDrawingParity(StringBuilder trace)
    {
        int[][] cases = [[0, 0], [0, 2], [4, 3], [3, 1], [9, 1], [11, 1], [12, 1], [13, 1], [8, 1]];
        foreach (var test in cases)
        {
            trace.AppendLine($"item-draw-case:{test[0]},{test[1]}");
            ItemScreenDrawing.Draw((byte)test[0], test[1], new ItemScreenDrawingHost(trace));
        }
    }

    private sealed class ItemScreenDrawingHost(StringBuilder trace) : IItemScreenDrawingHost
    {
        public void DrawHeader() => trace.AppendLine("item-header");
        public void DrawChoices() => trace.AppendLine("item-choices");
        public void DrawDataCubeIcon(bool dark, int x, int y, int spriteSheet, int spriteId)
            => trace.AppendLine(dark
                ? $"item-cube-dark:{x},{y},{spriteSheet},{spriteId},0"
                : $"item-cube:{x},{y},{spriteSheet},{spriteId}");
        public void DrawLoadSave() => trace.AppendLine("item-load-save");
        public void DrawKeyboardConfiguration() => trace.AppendLine("item-keyboard");
        public void DrawJoystickConfiguration() => trace.AppendLine("item-joystick");
        public void DrawUpgradeMenu() => trace.AppendLine("item-upgrade");
        public void DrawStatus() => trace.AppendLine("item-status");
        public void DrawDataCubes() => trace.AppendLine("item-cubes");
        public void DrawFooter() => trace.AppendLine("item-footer");
    }

    private sealed class ItemScreenFramePreparationHost(StringBuilder trace)
        : IItemScreenFramePreparationHost
    {
        public void GetShipInfo() => trace.AppendLine("ship-info");
        public void UpdateNavigationScreen() => trace.AppendLine("update-navigation");
    }

    private static void ItemScreenStatusDrawingParity(StringBuilder trace)
    {
        int[][] cases =
        [
            [0, 0, 0, 0, 2, 0, 0, 144, 96, 1],
            [1, 1, 0, 0, 2, 0, 0, 144, 96, 2],
            [2, 0, 0, 0, 2, 1, 0, 120, 84, 0],
            [3, 0, 0, 0, 2, 0, 0, 144, 96, 0],
            [4, 0, 0, 0, 2, 0, 0, 144, 96, 2],
            [4, 0, 0, 0, 3, 0, 0, 144, 96, 1],
            [4, 0, 0, 0, 5, 0, 0, 144, 96, 0],
            [5, 0, 0, 0, 2, 0, 0, 144, 96, 0],
            [6, 1, 0, 0, 2, 0, 0, 144, 96, 0],
            [7, 0, 0, 0, 2, 0, 0, 144, 96, 0],
            [9, 1, 0, 0, 2, 0, 0, 144, 96, 0],
            [10, 0, 0, 0, 2, 0, 0, 144, 96, 0],
            [11, 0, 0, 0, 2, 0, 1, 101, 77, 0],
            [12, 0, 0, 0, 2, 0, 0, 144, 96, 0],
            [13, 0, 0, 1, 2, 0, 0, 144, 96, 0],
            [0, 0, 2, 0, 2, 0, 0, 144, 96, 0]
        ];
        var players = new[] { new PlayerState(0), new PlayerState(1) };
        players[0].Cash = 12345;
        players[1].Cash = 67890;
        players[0].Armor = 73;
        players[0].Items.Weapons[0].Id = 8;
        players[0].Items.Special = 3;
        foreach (var (test, index) in cases.Select((test, index) => (test, index)))
        {
            players[0].Items.Shield = (byte)test[9];
            var state = new ItemScreenStatusState
            {
                CurrentMenu = (byte)test[0],
                TwoPlayerMode = test[1] != 0,
                SuperArcadeMode = test[2],
                SuperTyrian = test[3] != 0,
                SuperArcadeNone = 0,
                SuperArcadeShipOffset = 3,
                MusicDisabled = test[5] != 0,
                SamplesDisabled = test[6] != 0,
                MusicVolume = (ushort)test[7],
                EffectsVolume = (ushort)test[8],
                HelpBoxColor = 6,
                HelpBoxBrightness = 7,
                PlayerCashLabels = ["P1", "P2"],
                SuperShipLabels = Enumerable.Range(0, 16).Select(i => $"Ship{i}").ToArray(),
                FrontWeaponNames = Enumerable.Range(0, 256).Select(i => $"Weapon{i}").ToArray(),
                SpecialNames = Enumerable.Range(0, 256).Select(i => $"Special{i}").ToArray(),
                ShieldMaximumPower = Enumerable.Repeat(17, 256).ToArray()
            };
            state.CurrentSelections[1] = (byte)test[4];
            trace.AppendLine($"item-status-case:{index}");
            ItemScreenStatusDrawing.Draw(state, players, new ItemScreenStatusDrawingHost(trace));
            trace.AppendLine($"item-status-state:{state.HelpBoxColor},{state.HelpBoxBrightness}");
        }
    }

    private sealed class ItemScreenStatusDrawingHost(StringBuilder trace) : IItemScreenStatusHost
    {
        public void DrawTextShade(int x, int y, string text, int font, int shade, int darken)
            => trace.AppendLine($"status-text:{x},{y},{text},{font},{shade},{darken}");
        public void DrawBar(int x, int y, int color, int height, int value, int shade, int darken)
            => trace.AppendLine($"status-bar:{x},{y},{color},{height},{value},{shade},{darken}");
        public void DrawHelpBox(int x, int y, string text, int width, int color, int brightness)
            => trace.AppendLine($"status-help:{x},{y},{text},{width},{color},{brightness}");
        public void DrawShipIllustration() => trace.AppendLine("status-ship");
    }

    private static void ItemScreenDataCubeDrawingParity(StringBuilder trace)
    {
        int[][] cases =
        [
            [7, 0, 2, 0, 0],
            [7, 3, 2, 0, 0],
            [7, 3, 3, 0, 0],
            [7, 3, 4, 0, 0],
            [8, 3, 3, 1, 0],
            [8, 3, 2, 0, 1],
            [8, 3, 3, 0, 0],
            [0, 3, 2, 0, 0]
        ];
        ItemScreenDataCube[] cubes =
        [
            new("Cube0", "Header0", 4),
            new("Cube1", "Header1", -1),
            new("Cube2", "Header2", 7),
            new("Cube3", "Header3", 2)
        ];
        var palettes = Enumerable.Range(0, 20)
            .Select(palette => Enumerable.Range(0, 1024)
                .Select(index => unchecked((byte)(palette * 13 + index * 7))).ToArray())
            .ToArray();
        foreach (var (test, index) in cases.Select((test, index) => (test, index)))
        {
            var state = new ItemScreenDataCubeState
            {
                CurrentMenu = (byte)test[0],
                FirstMenu9 = test[3] != 0,
                BackFromHelp = test[4] != 0,
                CubeCount = test[1],
                Cubes = cubes,
                NoDataCubesText = "No cubes",
                DoneText = "Done",
                Colors = Enumerable.Range(0, 1024)
                    .Select(i => unchecked((byte)(i * 3 + 1))).ToArray(),
                Palettes = palettes,
                FacePalettes = Enumerable.Range(0, 256).Select(i => (byte)(i % 20)).ToArray(),
                PaletteChanged = false,
                NewPalette = 11,
                TemporaryWord = 91,
                TemporaryWord2 = 92,
                TemporaryByte = 93,
                HelpBoxColor = 6,
                HelpBoxBrightness = 7,
                HelpBoxShadeType = 8
            };
            Array.Fill(state.MenuChoices, (byte)0xA5);
            state.CurrentSelections[ItemScreenDataCubeState.DataCubeMenu] = (byte)test[2];
            trace.AppendLine($"item-cubes-case:{index}");
            ItemScreenDataCubeDrawing.Draw(state, new ItemScreenDataCubeDrawingHost(trace));
            trace.AppendLine($"item-cubes-state:{state.CurrentMenu},{Bit(state.FirstMenu9)}," +
                $"{Bit(state.BackFromHelp)},{state.MenuChoices[ItemScreenDataCubeState.DataCubeMenu]}," +
                $"{state.CurrentSelections[ItemScreenDataCubeState.DataCubeMenu]}," +
                $"{Bit(state.PaletteChanged)},{state.NewPalette},{state.TemporaryWord}," +
                $"{state.TemporaryWord2},{state.TemporaryByte},{state.HelpBoxColor}," +
                $"{state.HelpBoxBrightness},{state.HelpBoxShadeType},{Hash(state.Colors)}");
        }
    }

    private sealed class ItemScreenDataCubeDrawingHost(StringBuilder trace) : IItemScreenDataCubeHost
    {
        public void FillRectangle(int x1, int y1, int x2, int y2, int color)
            => trace.AppendLine($"cube-fill:{x1},{y1},{x2},{y2},{color}");
        public void DrawPortraitBackground(int x, int y, int spriteSheet, int spriteId)
            => trace.AppendLine($"cube-blit:{x},{y},{spriteSheet},{spriteId}");
        public void DrawHelpBox(int x, int y, string text, int width, int color, int brightness, int shadeType)
            => trace.AppendLine($"cube-help:{x},{y},{text},{width},{color},{brightness},{shadeType}");
        public void DrawCube(int x, int y, int filter, int brightness)
            => trace.AppendLine($"cube-draw:{x},{y},{filter},{brightness}");
        public void DrawTextShade(int x, int y, string text, int color, int brightness, int shadeType)
            => trace.AppendLine($"cube-text:{x},{y},{text},{color},{brightness},{shadeType}");
        public (int Width, int Height) GetFaceSpriteSize(int spriteSheet, int spriteId)
            => (16 + spriteId % 5 * 3, 10 + spriteId % 7 * 2);
        public void DrawFaceSprite(int x, int y, int spriteSheet, int spriteId)
            => trace.AppendLine($"cube-blit:{x},{y},{spriteSheet},{spriteId}");
    }

    private static void ItemScreenFooterDrawingParity(StringBuilder trace)
    {
        int[][] cases =
        [
            [9, 0, 4, 4, 0, 2, 5],
            [9, 1, 3, 1, 0, 4, 6],
            [9, 2, 4, 3, 2, 3, 5],
            [7, 0, 1, 2, 0, 2, 4],
            [8, 1, 3, 4, 4, 4, 5],
            [0, 0, 1, 2, 0, 2, 4]
        ];
        ItemScreenDataCube[] cubes =
        [
            new("Cube0", "Header0", 4),
            new("Cube1", "Header1", -1),
            new("Cube2", "Header2", 7),
            new("Cube3", "Header3", 2)
        ];
        var palettes = Enumerable.Range(0, 20)
            .Select(palette => Enumerable.Range(0, 1024)
                .Select(index => unchecked((byte)(palette * 13 + index * 7))).ToArray())
            .ToArray();
        foreach (var (test, index) in cases.Select((test, index) => (test, index)))
        {
            var state = new ItemScreenFooterState
            {
                CurrentMenu = (byte)test[0],
                InputDeviceNames = ["Keyboard", "Mouse", "Joystick"],
                JoystickCount = test[1],
                Flash = true,
                CurrentPalette = 5,
                NewPalette = test[4],
                Colors = Enumerable.Range(0, 1024)
                    .Select(i => unchecked((byte)(i * 3 + 1))).ToArray(),
                Palettes = palettes,
                Cubes = cubes
            };
            state.InputDevices[0] = (byte)test[2];
            state.InputDevices[1] = (byte)test[3];
            Array.Fill(state.ShotMultiPositions, (byte)0xA5);
            state.MenuChoices[ItemScreenDataCubeState.DataCubeMenu] = (byte)test[6];
            state.CurrentSelections[ItemScreenDataCubeState.DataCubeMenu] = (byte)test[5];
            trace.AppendLine($"item-footer-case:{index}");
            ItemScreenFooterDrawing.Draw(state, new ItemScreenFooterDrawingHost(trace));
            trace.AppendLine($"item-footer-state:{state.CurrentPalette},{state.NewPalette}," +
                $"{Bit(state.Flash)},{state.InputDevices[0]},{state.InputDevices[1]}," +
                $"{Hash(state.ShotMultiPositions)},{Hash(state.Colors)}");
        }
    }

    private sealed class ItemScreenFooterDrawingHost(StringBuilder trace) : IItemScreenFooterHost
    {
        public void DrawInputDevice(int x, int y, string text, int font)
            => trace.AppendLine($"footer-device:{x},{y},{text},{font}");
        public void DrawScore() => trace.AppendLine("footer-score");
        public void DrawMainMenuHelpText() => trace.AppendLine("footer-help");
        public void SetPalette(ReadOnlyMemory<byte> palette, int first, int last)
            => trace.AppendLine($"footer-palette:{first},{last},{Hash(palette.Span[..(last + 1)])}");
        public int TextWidth(string text, int font)
        {
            trace.AppendLine($"footer-width:{text},{font}");
            return 40;
        }
        public void DrawTextShade(int x, int y, string text, int color, int brightness, int shadeType)
            => trace.AppendLine($"footer-text:{x},{y},{text},{color},{brightness},{shadeType}");
    }

    private static void ItemScreenLoadSaveDrawingParity(StringBuilder trace)
    {
        int[][] cases =
        [
            [6, 0, 1, 0, 2, 3],
            [6, 0, 1, 1, 3, 15],
            [6, 0, 0, 0, 13, 2],
            [6, 1, 0, 1, 12, 13],
            [6, 0, 0, 0, 4, 2],
            [5, 1, 1, 0, 2, 1]
        ];
        var saves = Enumerable.Range(0, 32)
            .Select(i => new SavedGameMetadata($"Save{i}", Encoding.Latin1.GetBytes($"Level{i}"),
                0, false, (byte)(i + 1), 1, 1)).ToArray();
        foreach (var (test, index) in cases.Select((test, index) => (test, index)))
        {
            var state = new ItemScreenLoadSaveState
            {
                CurrentMenu = (byte)test[0],
                TwoPlayerMode = test[1] != 0,
                PerformSave = test[2] != 0,
                SaveGamePageIndex = (byte)test[3],
                ReturnLabel = Encoding.Latin1.GetBytes("Return"),
                EpisodePrefix = Encoding.Latin1.GetBytes("EP "),
                TemporaryColor = 88
            };
            state.CurrentSelections[6] = (byte)test[4];
            var host = new ItemScreenLoadSaveDrawingHost(trace, saves, test[5]);
            trace.AppendLine($"item-load-save-case:{index}");
            ItemScreenLoadSaveDrawing.Draw(state, host);
            trace.AppendLine($"item-load-save-state:{state.TemporaryColor}");
        }
    }

    private sealed class ItemScreenLoadSaveDrawingHost(StringBuilder trace,
        IReadOnlyList<SavedGameMetadata> saves, int count) : IItemScreenLoadSaveHost
    {
        public IReadOnlyList<SavedGameMetadata> BuildSaveGameList(byte players)
        {
            trace.AppendLine($"save-list:{players},{count}");
            return saves.Take(count).ToArray();
        }
        public void DrawTextShade(int x, int y, ReadOnlyMemory<byte> text, int color,
            int brightness, int shadeType)
            => trace.AppendLine($"load-save-text:{x},{y},{Encoding.Latin1.GetString(text.Span)}," +
                $"{color},{brightness},{shadeType}");
        public void DrawMenuHeader() => trace.AppendLine("load-save-header");
    }

    private static void ItemScreenKeyboardConfigurationDrawingParity(StringBuilder trace)
    {
        int[][] cases = [[5, 2, 0], [5, 9, 0], [5, 10, 0], [5, 11, 0], [4, 7, 0]];
        foreach (var (test, index) in cases.Select((test, index) => (test, index)))
        {
            var state = new ItemScreenKeyboardConfigurationState
            {
                CurrentMenu = (byte)test[0],
                TemporaryColor = 88,
                MenuLabels = Enumerable.Range(0, 16)
                    .Select(i => (ReadOnlyMemory<byte>)Encoding.Latin1.GetBytes($"Key{i}")).ToArray(),
                KeyNames = Enumerable.Range(0, 8).Select(i => $"Scan{i}").ToArray()
            };
            state.CurrentSelections[state.CurrentMenu] = (byte)test[1];
            Array.Fill(state.MenuChoices, (byte)0xA5);
            trace.AppendLine($"item-keyboard-case:{index}");
            ItemScreenKeyboardConfigurationDrawing.Draw(state,
                new ItemScreenKeyboardConfigurationDrawingHost(trace));
            trace.AppendLine($"item-keyboard-state:{state.MenuChoices[5]},{state.TemporaryColor}");
        }
    }

    private sealed class ItemScreenKeyboardConfigurationDrawingHost(StringBuilder trace)
        : IItemScreenKeyboardConfigurationHost
    {
        public void DrawTextShade(int x, int y, ReadOnlyMemory<byte> text, int color,
            int brightness, int shadeType)
            => trace.AppendLine($"keyboard-text:{x},{y},{Encoding.Latin1.GetString(text.Span)}," +
                $"{color},{brightness},{shadeType}");
        public void DrawKeyName(int x, int y, string name, int color, int brightness, int shadeType)
            => trace.AppendLine($"keyboard-text:{x},{y},{name},{color},{brightness},{shadeType}");
    }

    private static void ItemScreenJoystickConfigurationDrawingParity(StringBuilder trace)
    {
        int[][] cases = [[12, 0, 0, 4], [12, 1, 0, 2], [12, 1, 0, 4], [12, 2, 1, 9], [11, 0, 0, 6]];
        var firstJoystick = new JoystickState(null) { Sensitivity = 7, Threshold = 3 };
        firstJoystick.Assignments[0, 0] = new(JoystickAssignmentKind.Axis, 0, false, true);
        firstJoystick.Assignments[0, 1] = new(JoystickAssignmentKind.Button, 2);
        firstJoystick.Assignments[1, 0] = new(JoystickAssignmentKind.Hat, 1, true, false);
        for (var action = 2; action < 10; action++)
            firstJoystick.Assignments[action, 0] = new(JoystickAssignmentKind.Button, action);
        var secondJoystick = new JoystickState(null)
        {
            Analog = true,
            Sensitivity = 10,
            Threshold = 8
        };
        secondJoystick.Assignments[0, 0] = new(JoystickAssignmentKind.Axis, 1, true, false);
        JoystickState[] joysticks = [firstJoystick, secondJoystick];
        foreach (var (test, index) in cases.Select((test, index) => (test, index)))
        {
            var state = new ItemScreenJoystickConfigurationState
            {
                CurrentMenu = (byte)test[0],
                MenuLabels = Enumerable.Range(0, 16)
                    .Select(i => (ReadOnlyMemory<byte>)Encoding.Latin1.GetBytes($"JoystickLabel{i}")).ToArray(),
                JoystickCount = test[1],
                JoystickConfig = test[2],
                Joysticks = joysticks
            };
            Array.Fill(state.MenuChoices, (byte)0xA5);
            state.CurrentSelections[state.CurrentMenu] = (byte)test[3];
            trace.AppendLine($"item-joystick-case:{index}");
            ItemScreenJoystickConfigurationDrawing.Draw(state,
                new ItemScreenJoystickConfigurationDrawingHost(trace));
            trace.AppendLine($"item-joystick-state:{state.MenuChoices[12]}");
        }
    }

    private sealed class ItemScreenJoystickConfigurationDrawingHost(StringBuilder trace)
        : IItemScreenJoystickConfigurationHost
    {
        public void DrawTextShade(int x, int y, ReadOnlyMemory<byte> text, int color,
            int brightness, int shadeType)
            => trace.AppendLine($"joystick-text:{x},{y},{Encoding.Latin1.GetString(text.Span)}," +
                $"{color},{brightness},{shadeType}");
    }

    private static void ItemScreenUpgradeMenuDrawingParity(StringBuilder trace)
    {
        int[][] cases =
        [
            [2, 2, 4, 1, 200, 1, 4, 92, 0, 0, 92, 1, 0],
            [2, 2, 5, 1, 5, 2, 4, 3, 4, 0, 4, 0, 0],
            [3, 2, 5, 1, 500, 10, 7, 2, 4, 0, 2, 1, 0],
            [4, 5, 5, 1, 500, 10, 5, 5, 3, 0, 3, 1, 1],
            [5, 3, 5, 1, 500, 1, 5, 1, 2, 0, 2, 0, 0],
            [6, 2, 4, 1, 500, 1, 5, 3, 0, 0, 3, 0, 0],
            [7, 3, 5, 1, 500, 1, 5, 1, 2, 0, 2, 0, 0],
            [8, 2, 5, 1, 500, 1, 5, 3, 4, 0, 4, 0, 0]
        ];
        foreach (var (test, index) in cases.Select((test, index) => (test, index)))
        {
            var choices = Enumerable.Range(0, 9)
                .Select(_ => (IReadOnlyList<UpgradeMenuItem>)Array.Empty<UpgradeMenuItem>()).ToArray();
            choices[test[0]] =
            [
                new((byte)test[7], UpgradeName(test[0], test[7]), (ulong)(test[7] * test[5]),
                    (uint)(test[7] + 100), (byte)(test[7] % 4)),
                new((byte)test[8], UpgradeName(test[0], test[8]), (ulong)(test[8] * test[5]),
                    (uint)(test[8] + 100), (byte)(test[8] % 4)),
                new((byte)test[9], UpgradeName(test[0], test[9]), (ulong)(test[9] * test[5]),
                    (uint)(test[9] + 100), (byte)(test[9] % 4)),
                new(0, "None", 0)
            ];
            var playerItems = new PlayerItems
            {
                Ship = 22,
                Shield = 25,
                Generator = 26
            };
            playerItems.Weapons[0] = new PlayerWeapon { Id = 23, Power = 1 };
            playerItems.Weapons[1] = new PlayerWeapon { Id = 24, Power = 1 };
            playerItems.Weapons[test[0] == 3 ? 0 : 1].Power = (byte)test[6];
            playerItems.Sidekicks[0] = 27;
            playerItems.Sidekicks[1] = 28;
            var previousItems = playerItems.Copy();
            previousItems.SetMenuItemId(test[0] - 2, (byte)test[10]);
            playerItems.SetMenuItemId(test[0] - 2, (byte)test[11]);
            var state = new ItemScreenUpgradeMenuState
            {
                CurrentMenu = ItemScreenUpgradeMenuState.UpgradeSubmenu,
                UpgradeSelection = (byte)test[0],
                SelectedChoice = (byte)test[1],
                ChoiceCount = (byte)test[2],
                LastDirection = test[3],
                PlayerCash = (ulong)test[4],
                PlayerItems = playerItems,
                PreviousItems = previousItems,
                ChoicesByUpgrade = choices,
                Titles = Enumerable.Range(0, 8).Select(i => $"Title{i + 1}").ToArray(),
                DoneLabel = "Done",
                RightPowerAffordable = test[12] != 0
            };
            trace.AppendLine($"item-upgrade-case:{index}");
            var host = new ItemScreenUpgradeMenuDrawingHost(trace, test[5], 200);
            ItemScreenUpgradeMenuDrawing.Draw(state, host);
            var side = test[0] == 8 ? 1 : 0;
            var sidekick = state.Sidekicks[side];
            trace.AppendLine($"item-upgrade-state:{state.SelectedChoice},{playerItems.Ship}," +
                $"{playerItems.Weapons[0].Id},{playerItems.Weapons[0].Power}," +
                $"{playerItems.Weapons[1].Id},{playerItems.Weapons[1].Power}," +
                $"{playerItems.Shield},{playerItems.Generator},{playerItems.Sidekicks[0]}," +
                $"{playerItems.Sidekicks[1]},{Bit(state.LeftPower)},{Bit(state.RightPower)}," +
                $"{Bit(state.RightPowerAffordable)},{sidekick.ChargeTicks},{sidekick.Charge}," +
                $"{sidekick.Style},{Bit(sidekick.AnimationEnabled)},{sidekick.AnimationFrame}," +
                $"{host.UpgradeCost},{host.DowngradeCost}");
        }
    }

    private static string UpgradeName(int upgrade, int item) => upgrade switch
    {
        2 => $"Ship{item}",
        3 or 4 => $"Weapon{item}",
        5 => $"Shield{item}",
        6 => $"Generator{item}",
        7 or 8 => $"Sidekick{item}",
        _ => throw new ArgumentOutOfRangeException(nameof(upgrade))
    };

    private sealed class ItemScreenUpgradeMenuDrawingHost(StringBuilder trace,
        int costMultiplier, long cashLeft) : IItemScreenUpgradeMenuHost
    {
        public ushort UpgradeCost { get; private set; }
        public ushort DowngradeCost { get; private set; }

        public ulong GetUpgradeCost(byte upgradeSelection, byte itemId)
        {
            if (upgradeSelection is 3 or 4)
            {
                UpgradeCost = unchecked((ushort)(itemId * costMultiplier * 2));
                DowngradeCost = unchecked((ushort)(itemId * costMultiplier));
            }
            return unchecked((ulong)(itemId * costMultiplier));
        }

        public long CalculateCashLeft(byte upgradeSelection)
        {
            _ = upgradeSelection;
            return cashLeft;
        }

        public void DrawString(int x, int y, string text, int font)
            => trace.AppendLine($"upgrade-string:{x},{y},{text},{font}");

        public int FontCenter(string text, int font)
        {
            _ = font;
            return 160 - text.Length * 6 / 2;
        }

        public void DrawShipInfo() => trace.AppendLine("upgrade-ship-info");

        public void FillRectangle(int x1, int y1, int x2, int y2, int color)
            => trace.AppendLine($"upgrade-fill:{x1},{y1},{x2},{y2},{color}");

        public void DrawOwnedMarker(int x, int y, int spriteSheet, int spriteId)
        {
            _ = spriteSheet;
            trace.AppendLine($"upgrade-marker:{x},{y},{spriteId}");
        }

        public void DrawTextShade(int x, int y, string text, int font, int shade, int darken)
            => trace.AppendLine($"upgrade-text:{x},{y},{text},{font},{shade},{darken}");

        public void DrawItem(int itemType, byte itemId, int x, int y)
            => trace.AppendLine($"upgrade-item:{itemType},{itemId},{x},{y}");
    }

    private static void ItemScreenFrameUpdateParity(StringBuilder trace)
    {
        int[][] cases =
        [
            [8, 0, 2, 1, 16, 20, 5, 7, 180, 120, 1, 0, 0, 3, 4, 1, 0, 5],
            [8, 0, 3, 0, 12, 12, 2, -6, 164, 47, 1, 0, 0, 7, 7, 0, 0, 5],
            [3, 0, 2, 0, 20, 16, 0, 0, 0, 0, 0, 1, 2, 4, 4, 7, 1, 4],
            [4, 3, 2, 0, 20, 16, 0, 0, 0, 0, 1, 1, 2, 6, 0, 0, 0, 4],
            [7, 0, 3, 0, 20, 16, 0, 0, 0, 0, 0, 0, 0, 6, 6, -1, 0, 5],
            [2, 0, 2, 1, 10, 16, 0, 0, 0, 0, 0, 0, 0, -2, -1, 0, 0, 5]
        ];
        foreach (var (test, index) in cases.Select((test, index) => (test, index)))
        {
            var state = new ItemScreenFrameUpdateState
            {
                CurrentMenu = (byte)test[0],
                UpgradeSelection = (byte)test[1],
                DataCubeSelection = (byte)test[2],
                CurrentCube = test[3],
                MouseX = test[8],
                MouseY = test[9],
                BackFromHelp = test[10] != 0,
                PaletteChanged = test[11] != 0,
                NewPalette = test[12],
                CurrentPalette = test[13],
                Color = test[14],
                ColorDirection = test[15],
                ExtraGame = test[16] != 0,
                ReadPercentLabel = "Read",
                DoneLabel = "Done",
                ExtraGameLabel = "Extra Game",
                Colors = Enumerable.Range(0, 1024).Select(i => (byte)(i * 3 + 1)).ToArray(),
                Palettes = Enumerable.Range(0, 20).Select(p =>
                    Enumerable.Range(0, 1024).Select(i => (byte)(p * 13 + i * 7)).ToArray()).ToArray(),
                Cubes = Enumerable.Range(0, 2).Select(c => new ItemScreenReadingCube(
                    c == 0 ? test[4] : test[5],
                    Enumerable.Range(1, 20).Select(line =>
                        (ReadOnlyMemory<byte>)Encoding.ASCII.GetBytes($"Cube{c}Line{line}")).ToArray())).ToArray()
            };
            state.ScrollLocation = test[6];
            state.ScrollChange = test[7];
            state.MenuChoices[ItemScreenFrameUpdateState.DataCubeMenu] = (byte)test[17];
            trace.AppendLine($"item-update-case:{index}");
            ItemScreenFrameUpdate.Update(state, new ItemScreenFrameUpdateHost(trace));
            trace.AppendLine($"item-update-state:{state.Color},{state.ColorDirection}," +
                $"{state.MouseCursor},{state.ScrollLocation},{state.ScrollChange}," +
                $"{Bit(state.BackFromHelp)},{state.CurrentPalette},{state.NewPalette}," +
                $"{Bit(state.PaletteChanged)}");
        }
    }

    private sealed class ItemScreenFrameUpdateHost(StringBuilder trace) : IItemScreenFrameUpdateHost
    {
        public void KeepAlive() => trace.AppendLine("update-keep");
        public void FillRectangle(int x1, int y1, int x2, int y2, int color)
            => trace.AppendLine($"update-fill:{x1},{y1},{x2},{y2},{color}");
        public void DrawReadingLine(int x, int y, ReadOnlyMemory<byte> text, int color,
            int brightness, int font)
            => trace.AppendLine($"update-text:{x},{y},{Encoding.Latin1.GetString(text.Span)}," +
                $"{color},{brightness},{font}");
        public void DrawPercentage(int x, int y, string text, int color, int brightness, int font)
            => trace.AppendLine($"update-text:{x},{y},{text},{color},{brightness},{font}");
        public void DrawString(int x, int y, string text, int font)
            => trace.AppendLine($"update-string:{x},{y},{text},{font}");
        public void DrawNavigationScreen() => trace.AppendLine("update-navigation");
        public void DrawMainMenuHelpText() => trace.AppendLine("update-help");
        public void DrawMenuHeader() => trace.AppendLine("update-header");
        public void DrawMenuChoices() => trace.AppendLine("update-choices");
        public void DrawCubeFlash(int x, int y, int spriteSheet, int spriteId, int color, int brightness)
            => trace.AppendLine($"update-cube-flash:{x},{y},{spriteSheet},{spriteId},{color},{brightness}");
        public void UpdateWeaponSimulation() => trace.AppendLine("update-weapon-sim");
        public void DrawScore() => trace.AppendLine("update-score");
        public void ServiceEvents(bool clear) => trace.AppendLine($"update-events:{Bit(clear)}");
        public void SetPalette(ReadOnlyMemory<byte> palette, int first, int last)
        {
            _ = palette;
            trace.AppendLine($"update-palette:{first},{last}");
        }
        public void MouseStart() => trace.AppendLine("update-mouse-start");
        public void Present() => trace.AppendLine("update-present");
        public void FadePalette(ReadOnlyMemory<byte> colors, int steps, int first, int last)
        {
            _ = colors;
            trace.AppendLine($"update-fade:{steps},{first},{last}");
        }
        public void MouseReplace() => trace.AppendLine("update-mouse-replace");
        public void SetDelay(int ticks) => trace.AppendLine($"update-delay:{ticks}");
    }

    private static void TraceItemScreenInitialization(StringBuilder trace,
        ItemScreenInitializationState state, EpisodeItemAvailabilityState availability,
        IndexedSurface backgroundScreen)
    {
        trace.AppendLine($"item-init-state:{state.CurrentPalette},{state.NewPalette},{state.Color}," +
            $"{Bit(state.GameLoaded)},{state.CurrentItemType},{state.Cursor},{state.CurrentItem}," +
            $"{state.CurrentMenu},{Bit(state.KeyboardUsed)},{Bit(state.FirstMenu9)}," +
            $"{Bit(state.BackFromHelp)},{Bit(state.ShopSpriteSheetLoaded)}," +
            $"{Hash(state.MenuChoices)},{Hash(state.CurrentSelections)},{Hash(backgroundScreen.Pixels.Span)}");
        for (var row = 0; row < availability.Counts.Length; row++)
        {
            var slots = Enumerable.Range(0, 10).Select(slot => availability.Items[row, slot]);
            trace.AppendLine($"item-init-row:{row},{availability.Counts[row]},{string.Join(",", slots)}");
        }
    }

    private sealed class ItemScreenInitializationHost(StringBuilder trace, byte[] primary)
        : IItemScreenInitializationHost
    {
        public void LoadShopSpriteSheet(char code)
        {
            trace.AppendLine("cursor-load");
            _ = code;
        }

        public void LoadCubes() => trace.AppendLine("load-cubes");
        public void PlaySong(int song) => trace.AppendLine($"song:{song}");

        public void LoadPicture(IndexedSurface screen, int picture, bool loadPalette)
        {
            trace.AppendLine("picture");
            screen.Pixels.Span.Fill(0xA8);
            _ = picture;
            _ = loadPalette;
        }

        public void Present() => trace.AppendLine($"show:{Hash(primary)}");
        public void SetPalette(ReadOnlyMemory<byte> colors, int first, int last)
            => trace.AppendLine($"set-palette:{first},{last}");
    }

    private static string Execute()
    {
        var trace = new StringBuilder();
        ItemScreenInitializationParity(trace);
        QuitConfirmationParity(trace);
        MenuQuitParity(trace);
        UpgradeSubmenuParity(trace);
        ItemScreenUpgradeMenuDrawingParity(trace);
        ItemScreenFrameUpdateParity(trace);
        DataCubeTextLoading(trace);
        for (var kind = 0; kind < 3; kind++)
            for (var mode = 0; mode < 40; mode++)
            {
                trace.AppendLine($"case:{kind},{mode}");
                var platform = new Platform(trace, Script(kind, mode));
                var input = new InputState(platform, new VideoLayout { Output = new(0, 0, 320, 200) });
                platform.Input = input;
                var host = new Host(trace, platform, input, mode % 2 != 0);
                var state = new SelectionMenuState { OnePlayerAction = true, TwoPlayerMode = true, Episode = 7, InitialEpisode = 9, Difficulty = 10 };
                var menus = new SelectionMenus(input, host, state);
                var prefix = kind == 0 ? 'G' : kind == 1 ? 'E' : 'D';
                var names = Enumerable.Range(0, 7).Select(i => (ReadOnlyMemory<byte>)Encoding.ASCII.GetBytes($"{prefix}{i}")).ToArray();
                var result = kind switch
                {
                    0 => menus.SelectGameplay(names),
                    1 => menus.SelectEpisode(names, Enumerable.Range(0, 4).Select(i => mode % 4 == 0 || i % 2 == 0).ToArray()),
                    _ => menus.SelectDifficulty(names)
                };
                trace.AppendLine($"result:{Bit(result)},{Bit(state.OnePlayerAction)},{Bit(state.TwoPlayerMode)},{state.Episode},{state.InitialEpisode},{state.Difficulty},{platform.FrameIndex}");
            }
        foreach (byte menu in new byte[] { 0, 3, 9, 10 })
            foreach (byte choice in new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 255 })
                foreach (byte selected in new byte[] { 0, 1, 7, 255 })
                    trace.AppendLine($"choice-y:{menu},{choice},{selected},{MainMenuChoiceLayout.GetChoiceY(menu, choice, selected)}");
        foreach (byte menu in new byte[] { 0, 1, 3, 9, 10 })
            foreach (byte count in new byte[] { 1, 2, 4, 7 })
                foreach (byte selected in new byte[] { 0, 2, 4, 7 })
                {
                    trace.AppendLine($"menu-draw-case:{menu},{count},{selected}");
                    var labels = Enumerable.Range(0, count).Select(i =>
                        (ReadOnlyMemory<byte>)Encoding.ASCII.GetBytes($"m{menu}c{i + 1}\0tail")).ToArray();
                    MainMenuChoiceLayout.DrawChoices(menu, count, selected, count, labels,
                        (x, y, text, font) => trace.AppendLine(
                            $"menu-draw:{x},{y},{Encoding.ASCII.GetString(text.Span).Split('\0')[0]},{(font == Font.Small ? 1 : -1)}"));
                }
        var menuLabels = Enumerable.Range(0, 15).Select(row =>
            (IReadOnlyList<ReadOnlyMemory<byte>>)Enumerable.Range(0, 16)
                .Select(column => (ReadOnlyMemory<byte>)Encoding.ASCII.GetBytes($"r{row}c{column}\0tail")).ToArray()).ToArray();
        var cubeHeaders = Enumerable.Range(0, 16)
            .Select(i => (ReadOnlyMemory<byte>)Encoding.ASCII.GetBytes($"cube{i}\0tail")).ToArray();
        foreach (byte menu in Enumerable.Range(0, 14).Select(i => (byte)i))
            for (var performSave = 0; performSave <= 2; performSave++)
                for (byte cubeSelection = 2; cubeSelection <= 7; cubeSelection++)
                {
                    trace.AppendLine($"menu-header-case:{menu},{performSave},{cubeSelection}");
                    MainMenuChoiceLayout.DrawHeader(menu, cubeSelection, performSave, cubeHeaders, menuLabels,
                        text => text.Length * 6,
                        (x, y, text, font) => trace.AppendLine(
                            $"menu-draw:{x},{y},{Encoding.ASCII.GetString(text.Span)},{(font == Font.Large ? 0 : -1)}"));
                }
        var menuHelp = Enumerable.Range(0, 14).Select(menu =>
            (IReadOnlyList<byte>)Enumerable.Range(0, 11).Select(column => (byte)((menu + column) % 34 + 1)).ToArray()).ToArray();
        var helpTexts = Enumerable.Range(0, 34)
            .Select(index => (ReadOnlyMemory<byte>)Encoding.ASCII.GetBytes($"help{index:D2}")).ToArray();
        byte[] helpCounts = [2, 5, 10, 17];
        byte[] helpSelections = [2, 3, 5, 10, 12, 17];
        for (byte menu = 0; menu < 14; menu++)
            foreach (var choiceCount in helpCounts)
                foreach (var selectedChoice in helpSelections)
                {
                    if (menu != 12 && selectedChoice > 12) continue;
                    for (var power = 0; power < 4; power++)
                        for (var cubes = 0; cubes <= 1; cubes++)
                        {
                            var leftPower = (power & 1) != 0;
                            var rightPower = (power & 2) != 0;
                            trace.AppendLine($"help-case:{menu},{selectedChoice},{choiceCount},{power},{cubes}");
                            MainMenuHelpLayout.Draw(menu, selectedChoice, choiceCount, leftPower, rightPower,
                                cubes, menuHelp, helpTexts,
                                (x, y, text, _, _, _) => trace.AppendLine(
                                    $"help-draw:{x},{y},{Encoding.ASCII.GetString(text.Span)}"));
                        }
                }
        byte[] scoreMenus = [0, 4, 7, 13];
        int[] cashValues = [int.MinValue, -100, 0, 100, int.MaxValue];
        foreach (var menu in scoreMenus)
            foreach (var cashLeft in cashValues)
            {
                trace.AppendLine($"score-case:{menu},{cashLeft}");
                MainMenuScore.Draw(menu, cashLeft,
                    (x, y, text, _, _, _) => trace.AppendLine(
                        $"score-draw:{x},{y},{Encoding.ASCII.GetString(text.Span)}"));
            }
        using (var warnings = new StringWriter())
        {
            var screenPixels = Enumerable.Range(0, 320 * 200)
                .Select(i => unchecked((byte)(i * 29 + 7))).ToArray();
            var screen = new IndexedSurface(320, 200, 320, screenPixels, warnings);
            var players = new[] { new PlayerState(0), new PlayerState(1) };
            var player = players[0];
            player.X = 19; player.Y = 23; player.DeltaXShotMove = 31; player.DeltaYShotMove = -7;
            player.LastXExplosionFollow = 88; player.LastYExplosionFollow = 91;
            player.Sidekicks[0].X = 1; player.Sidekicks[0].Y = 2;
            player.Sidekicks[1].X = 3; player.Sidekicks[1].Y = 4;
            var shots = new PlayerShots([], [], players, new NoOpShotEffects());
            Array.Fill(shots.Availability, (byte)0xA5);
            Array.Fill(shots.Repeat, (byte)0xA5);
            Array.Fill(shots.MultiPosition, (byte)0xA5);
            shots.Power = 42;
            var hud = new LevelHudState { LastPower = 23 };
            var starfield = new Starfield();
            MersenneTwister.Seed(5489);
            trace.AppendLine("weapon-view-case");
            trace.AppendLine("weapon-fill:8,8,144,177,0");
            WeaponViewInitializer.Initialize(screen, player, shots, hud, starfield);
            trace.AppendLine("starfield-init");
            trace.AppendLine($"weapon-view:{player.Sidekicks[0].X},{player.Sidekicks[0].Y}," +
                $"{player.Sidekicks[1].X},{player.Sidekicks[1].Y},{player.X},{player.Y}," +
                $"{player.DeltaXShotMove},{player.DeltaYShotMove},{player.LastXExplosionFollow}," +
                $"{player.LastYExplosionFollow},{shots.Power},{hud.LastPower}," +
                $"{shots.Availability[0]},{Hash(screen.Pixels.Span)}");
            trace.AppendLine($"weapon-arrays:{shots.Availability[^1]},{shots.Repeat[0]}," +
                $"{shots.Repeat[^1]},{shots.MultiPosition[^1]}");
            Check(starfield.Stars.Length == 100 &&
                starfield.Stars.ToArray().All(star => star.Speed is >= 2 and <= 4 && star.Color is >= 0x90 and <= 0x9F),
                "Weapon preview did not initialize the starfield.");
            Check(warnings.ToString().Length == 0, "Weapon preview clear emitted clipping warnings.");
        }
        var scaleBreaks = new[] { 1, 4, 20, 81, 0 };
        for (var c = 0; c < scaleBreaks.Length; c++)
        {
            var destinationPixels = Enumerable.Range(0, 320 * 200)
                .Select(i => unchecked((byte)(i * 19 + c * 3 + 5))).ToArray();
            var sourcePixels = Enumerable.Range(0, 320 * 200)
                .Select(i => unchecked((byte)(i * 31 + c * 7 + 13))).ToArray();
            var destination = new IndexedSurface(320, 200, 320, destinationPixels, TextWriter.Null);
            var source = new IndexedSurface(320, 200, 320, sourcePixels, TextWriter.Null);
            var buttonCalls = 0;
            trace.AppendLine($"scale-picture-case:{c},{scaleBreaks[c]}");
            ShipSpecificationsPresentation.ScaleInPicture(destination, source,
                () => buttonCalls++ >= scaleBreaks[c],
                target => trace.AppendLine($"show:{Hash(target.Pixels.Span)}"),
                ticks => trace.AppendLine($"delay:{ticks}"));
        }
        var shipSpecsBreaks = new[] { 1, 6, 81, 0 };
        for (var c = 0; c < shipSpecsBreaks.Length; c++)
        {
            var displayPixels = Enumerable.Range(0, 320 * 200)
                .Select(i => unchecked((byte)(i * 17 + c * 3 + 1))).ToArray();
            var temporaryPixels = Enumerable.Range(0, 320 * 200)
                .Select(i => unchecked((byte)(i * 13 + c * 9 + 2))).ToArray();
            var gamePixels = Enumerable.Range(0, 320 * 200)
                .Select(i => unchecked((byte)(i * 7 + c * 5 + 3))).ToArray();
            var display = new IndexedSurface(320, 200, 320, displayPixels, TextWriter.Null);
            var temporary = new IndexedSurface(320, 200, 320, temporaryPixels, TextWriter.Null);
            var gameScreen = new IndexedSurface(320, 200, 320, gamePixels, TextWriter.Null);
            var buttonCalls = 0;
            trace.AppendLine($"ship-specs-case:{c},{shipSpecsBreaks[c]}");
            ShipSpecificationsPresentation.Run(display, gameScreen, temporary,
                () => trace.AppendLine("wait-noinput"),
                (screen, temp) =>
                {
                    trace.AppendLine("draw-specs");
                    for (var i = 0; i < screen.Pixels.Length; i++)
                        screen.Pixels.Span[i] = unchecked((byte)(i * 11 + 17));
                    temp.Pixels.Span.Fill(0x5A);
                },
                (screen, picture, palette) =>
                {
                    Check(picture == 1 && !palette, "Ship-spec background reload parameters changed.");
                    trace.AppendLine("picture");
                    screen.Pixels.Span.Fill(0xA8);
                },
                sample => trace.AppendLine($"sound:{sample}"),
                () => buttonCalls++ >= shipSpecsBreaks[c],
                screen => trace.AppendLine($"show:{Hash(screen.Pixels.Span)}"),
                ticks => trace.AppendLine($"delay:{ticks}"),
                () => trace.AppendLine("wait-input"));
        }
        byte[][] nextRoutes = [[21], [2, 12, 14, 18, 20]];
        byte[] nextOrigins = [13, 21];
        var planetNames = Enumerable.Range(1, 21)
            .Select(index => (ReadOnlyMemory<byte>)Encoding.ASCII.GetBytes($"planet{index}")).ToArray();
        for (var c = 0; c < nextRoutes.Length; c++)
        {
            trace.AppendLine($"next-menu-case:{c},{nextOrigins[c]},{nextRoutes[c].Length}");
            var next = NextLevelMenuSetup.Initialize(nextRoutes[c], nextOrigins[c], planetNames, "Done"u8.ToArray());
            trace.AppendLine($"next-menu:{next.CurrentMenu},{next.NewPalette},{(int)next.NavigationX}," +
                $"{(int)next.NavigationY},{(int)next.TargetX},{(int)next.TargetY}," +
                $"{next.ChoiceCount},{next.SelectedChoice}");
            for (var i = 0; i < next.ChoiceCount; i++)
                trace.AppendLine($"next-label:{i},{Encoding.ASCII.GetString(next.Labels[i].Span)}");
            trace.AppendLine($"next-dots:{string.Join(",", next.Dots.Counts)}");
            var mapSections = Enumerable.Range(0, nextRoutes[c].Length)
                .Select(i => (byte)(30 + c * 10 + i)).ToArray();
            next.StartCurrentLevel(mapSections);
            trace.AppendLine($"start-current:{next.MainLevel},{Bit(next.JumpSection)}");
            next.ReturnToOptions(false);
            trace.AppendLine($"return-options:0,{next.CurrentMenu}");
            next.ReturnToOptions(true);
            trace.AppendLine($"return-options:1,{next.CurrentMenu}");
        }
        TwoPlayerInputDeviceParity(trace);
        MainMenuFunctionParity(trace);
        UpgradeWeaponPowerParity(trace);
        HorizontalInputParity(trace);
        ItemScreenActivationParity(trace);
        ItemScreenCancellationParity(trace);
        ItemScreenHelpParity(trace);
        QuickSaveOpeningParity(trace);
        ItemScreenWaitParity(trace);
        ItemScreenKeyParity(trace);
        ItemScreenSelectionMovementParity(trace);
        ItemScreenMouseInputParity(trace);
        ItemScreenMouseSelectionParity(trace);
        ItemScreenOptionSelectionParity(trace);
        ItemScreenWeaponPowerMouseParity(trace);
        ItemScreenVolumeMouseParity(trace);
        ItemScreenInputDispatchParity(trace);
        byte[] navRoute = [1, 13, 17, 21];
        var navDots = NavigationMapDots.Compute(navRoute, 13);
        float[] initialNavX = [0.5f, 111.25f, -100.75f, 239.0f];
        float[] initialNavY = [-1.5f, 79.75f, 99.25f, -200.0f];
        byte[] navSelections = [2, 3, 4, 5];
        for (var c = 0; c < navSelections.Length; c++)
        {
            var pixels = Enumerable.Range(0, 320 * 200)
                .Select(i => unchecked((byte)(i * 23 + c * 5 + 9))).ToArray();
            var screen = new IndexedSurface(320, 200, 320, pixels, TextWriter.Null);
            var state = new NavigationMapScreenState
            {
                X = initialNavX[c], Y = initialNavY[c],
                TargetX = 40.5f + c * 13.25f, TargetY = 150.75f - c * 11.5f,
                AnimationFrame = (byte)(c * 4), AnimationWait = (byte)(c % 2 != 0 ? 2 : 0),
                CurrentDotNumber = (byte)c, CurrentDotWait = (byte)(c % 2 != 0 ? 0 : 3)
            };
            var selected = navSelections[c];
            trace.AppendLine($"nav-update-case:{c},{selected},{state.AnimationFrame},{state.AnimationWait}");
            NavigationMapScreen.Update(state, navRoute, 13, 6, selected, navDots,
                (x1, y1, x2, y2, color) =>
                {
                    trace.AppendLine($"fill:{x1},{y1},{x2},{y2},{color}");
                    screen.FillRectangle(x1, y1, x2, y2, unchecked((byte)color));
                },
                (x1, y1, x2, y2, color) => trace.AppendLine($"rectangle:{x1},{y1},{x2},{y2},{color}"),
                (x, y, color) => trace.AppendLine($"pixel:{x},{y},{color}"),
                sprite => ((ushort)(16 + sprite % 5 * 3), (ushort)(10 + sprite % 7 * 2)),
                (x, y, sprite, black) => trace.AppendLine($"planet-dark:{x},{y},{sprite},{Bit(black)}"),
                (x, y, sprite) => trace.AppendLine($"planet:{x},{y},{sprite}"),
                (x, y, sprite) => trace.AppendLine($"nav-dot:{x},{y},{sprite}"),
                (x, y, _, sprite) => trace.AppendLine($"nav-dot:{x},{y},{sprite}"));
            trace.AppendLine($"nav-update-state:{state.ViewX},{state.ViewY}," +
                $"{unchecked((int)(state.X * 1000f))},{unchecked((int)(state.Y * 1000f))}," +
                $"{unchecked((int)(state.TargetX * 1000f))},{unchecked((int)(state.TargetY * 1000f))}," +
                $"{state.AnimationFrame},{state.AnimationWait},{state.CurrentDotNumber}," +
                $"{state.CurrentDotWait},{navRoute.Length},13,{Hash(screen.Pixels.Span)}");
        }
        uint random = 0x7A31C5E9;
        for (var i = 0; i < 8192; i++)
        {
            random = unchecked(random * 1664525 + 1013904223);
            var start = unchecked((short)(random >> 16));
            var finish = unchecked((short)random);
            var dots = (byte)(random >> 8);
            var distance = (byte)random;
            trace.AppendLine($"part-way:{start},{finish},{dots},{distance},{MainMenuChoiceLayout.PartWay(start, finish, dots, distance)}");
        }
        foreach (var dark in new[] { false, true })
        {
            trace.AppendLine($"background-grid-case:{Bit(dark)}");
            NavigationMapGrid.DrawBackground(dark,
                (x1, y1, x2, y2, color) => trace.AppendLine($"rectangle:{x1},{y1},{x2},{y2},{color}"),
                (x, y, color) => trace.AppendLine($"pixel:{x},{y},{color}"));
        }
        foreach (short viewX in new short[] { short.MinValue, -101, -1, 0, 1, 101, short.MaxValue })
            foreach (short viewY in new short[] { short.MinValue, -101, -1, 0, 1, 101, short.MaxValue })
                foreach (var dark in new[] { false, true })
                {
                    trace.AppendLine($"viewport-grid-case:{viewX},{viewY},{Bit(dark)}");
                    NavigationMapGrid.DrawViewport(viewX, viewY, dark,
                        (x1, y1, x2, y2, color) => trace.AppendLine($"rectangle:{x1},{y1},{x2},{y2},{color}"),
                        (x, y, color) => trace.AppendLine($"pixel:{x},{y},{color}"));
                }
        short[] planetViews = [-100, -20, 0, 80, 160, 240, 400];
        byte[] planetFrames = [0, 4, 14];
        for (byte planet = 0; planet < 21; planet++)
            foreach (var viewX in planetViews)
                foreach (var viewY in planetViews)
                    foreach (var animationFrame in planetFrames)
                    {
                        trace.AppendLine($"planet-case:{planet},{viewX},{viewY},{animationFrame}");
                        NavigationMapPlanets.Draw(planet, viewX, viewY, animationFrame,
                            sprite => ((ushort)(16 + sprite % 5 * 3), (ushort)(10 + sprite % 7 * 2)),
                            (x, y, sprite, black) =>
                                trace.AppendLine($"planet-dark:{x},{y},{sprite},{Bit(black)}"),
                            (x, y, sprite) => trace.AppendLine($"planet:{x},{y},{sprite}"));
                    }
        var scaleCases = new[]
        {
            new[] { 9, 8, 13, 9, 9, 15, 1, 1, 9, 6 },
            new[] { 13, 10, 14, 12, 12, 17, 2, 2, 13, 9 },
            new[] { 7, 9, 11, 9, 10, 13, 0, 0, 8, 7 }
        };
        for (var c = 0; c < scaleCases.Length; c++)
        {
            var v = scaleCases[c];
            var sourcePixels = Enumerable.Range(0, v[2] * v[1])
                .Select(i => unchecked((byte)(i * 37 + c * 19 + 11))).ToArray();
            var destinationPixels = Enumerable.Range(0, v[5] * v[4])
                .Select(i => unchecked((byte)(i * 53 + c * 7 + 0xA5))).ToArray();
            var source = new IndexedSurface(v[0], v[1], v[2], sourcePixels, TextWriter.Null);
            var destination = new IndexedSurface(v[3], v[4], v[5], destinationPixels, TextWriter.Null);
            SelectionBitmapScaler.Scale(destination, source, v[6], v[7], v[8], v[9]);
            trace.AppendLine($"scale:{c},{Hash(destination.Pixels.Span)}");
        }
        for (byte origin = 1; origin <= 21; origin++)
            for (var mode = 0; mode < 16; mode++)
            {
                byte[] route = Enumerable.Range(0, 5).Select(i => (byte)((i * 7 + mode * 3) % 21 + 1)).ToArray();
                var dots = NavigationMapDots.Compute(route, origin);
                trace.Append($"nav-dots:{origin},{mode}");
                for (var i = 0; i < route.Length; i++)
                {
                    trace.Append($":{dots.Counts[i]}");
                    for (var dot = 0; dot < dots.Counts[i]; dot++)
                        trace.Append($":{dots.X[i, dot]},{dots.Y[i, dot]}");
                }
                trace.AppendLine();
                var viewX = (short)(origin * 15 - 150);
                var viewY = (short)(mode * 17 - 120);
                foreach (byte selected in new byte[] { 1, 2, 4, 7 })
                    foreach (byte currentDot in new byte[] { 0, 1, 5, 10 })
                    {
                        trace.AppendLine($"nav-dot-case:{origin},{mode},{viewX},{viewY},{selected},{currentDot}");
                        NavigationMapDots.Draw(dots, viewX, viewY, selected, currentDot,
                            (x, y, sprite) => trace.AppendLine($"nav-dot:{x},{y},{sprite}"));
                    }
            }
        return trace.ToString().Replace("\r\n", "\n", StringComparison.Ordinal);
    }

    private static void MenuQuitParity(StringBuilder trace)
    {
        for (var mode = 0; mode < 8; mode++)
        {
            var confirm = (mode & 1) != 0;
            var haltNetwork = (mode & 2) != 0;
            var network = (mode & 4) != 0;
            var game = new LegacyGameState { MainLevel = (byte)(10 + mode) };
            trace.AppendLine($"quit-case:{Bit(confirm)},{Bit(haltNetwork)},{Bit(network)}");
            var platform = new QuitPlatform();
            var input = new InputState(platform, new VideoLayout { Output = new(0, 0, 320, 200) });
            var host = new QuitHost(trace, input, platform,
                [[new InputEvent(InputEventKind.KeyDown, confirm ? 40 : 41)]],
                hasMouse: false, isNetworkGame: network);
            MenuQuitRequest.Run(haltNetwork, network, game,
                () => new QuitConfirmationScreen(input, host).Run(
                    "Quit title"u8.ToArray(), "Quit message"u8.ToArray(),
                    "Yes"u8.ToArray(), "No"u8.ToArray()),
                () => trace.AppendLine("quit-halt:0"));
            trace.AppendLine($"quit-state:{Bit(game.GameLoaded)},{game.MainLevel}");
        }
    }

    private static void QuitConfirmationParity(StringBuilder trace)
    {
        InputEvent[][] scripts =
        [
            [new(InputEventKind.KeyDown, 40)],
            [new(InputEventKind.KeyDown, 43)], [new(InputEventKind.KeyDown, 40)],
            [new(InputEventKind.KeyDown, 41)],
            [new(InputEventKind.KeyDown, 82)], [new(InputEventKind.KeyDown, 40)],
            [new(InputEventKind.MouseButtonDown, Button: 1, X: 57, Y: 124)],
            [new(InputEventKind.MouseButtonDown, Button: 1, X: 152, Y: 124)],
            [new(InputEventKind.MouseButtonDown, Button: 1, X: 100, Y: 123)], [new(InputEventKind.KeyDown, 40)],
            [new(InputEventKind.KeyDown, 80)], [new(InputEventKind.KeyDown, 44)],
            [new(InputEventKind.MouseButtonDown, Button: 1, X: 142, Y: 130)], [new(InputEventKind.KeyDown, 40)]
        ];
        var frameStarts = new[] { 0, 1, 3, 4, 6, 7, 8, 10, 12, 0 };
        var frameCounts = new[] { 1, 2, 1, 2, 1, 1, 2, 2, 2, 1 };
        for (var test = 0; test < frameStarts.Length; test++)
        {
            var mode = test;
            var frames = Enumerable.Range(frameStarts[test], frameCounts[test])
                .Select(index => scripts[index]).ToArray();
            var platform = new QuitPlatform();
            var input = new InputState(platform, new VideoLayout { Output = new(0, 0, 320, 200) });
            var host = new QuitHost(trace, input, platform, frames, hasMouse: mode == 8, isNetworkGame: mode == 9);
            trace.AppendLine($"quit-confirm-case:{mode}");
            var result = new QuitConfirmationScreen(input, host).Run(
                "Quit title"u8.ToArray(), "Quit message"u8.ToArray(), "Yes"u8.ToArray(), "No"u8.ToArray());
            trace.AppendLine($"quit-confirm-result:{Bit(result)},{host.FramesConsumed}");
        }
    }

    private sealed class QuitPlatform : IInputPlatform
    {
        private readonly Queue<InputEvent> _events = new();
        public uint Ticks { get; private set; }
        public bool JoystickDown => false;
        public bool IsNetworkGame => false;
        public bool PollEvent(out InputEvent input) => _events.TryDequeue(out input);
        public void Enqueue(IEnumerable<InputEvent> events)
        {
            foreach (var input in events) _events.Enqueue(input);
        }
        public void Delay(uint milliseconds) => Ticks = unchecked(Ticks + milliseconds);
        public void SetCursorVisible(bool visible) { }
        public void SetRelativeMouseMode(bool enabled) { }
        public void EnableSystemMouseScaling() { }
        public void ToggleFullscreen() { }
        public void WindowResized() { }
        public void Quit() => throw new InvalidOperationException("Unexpected quit event.");
        public void PushJoysticksAsKeyboard() { }
        public void PollJoysticks() { }
        public void CheckNetwork() { }
    }

    private sealed class QuitHost(StringBuilder trace, InputState input, QuitPlatform platform,
        InputEvent[][] frames, bool hasMouse, bool isNetworkGame = false) : IQuitConfirmationHost
    {
        private int _frame;
        public int FramesConsumed => _frame;
        public bool HasMouse => hasMouse;
        public bool IsNetworkGame => isNetworkGame;
        public void ClearKeyboard() => trace.AppendLine("clear-keyboard");
        public void WipeKey() => trace.AppendLine("wipe-key");
        public void WaitNoInput(bool keyboard, bool mouse, bool joystick)
        {
            trace.AppendLine("wait-noinput");
            _ = keyboard; _ = mouse; _ = joystick;
        }
        public void ShadeDialog(int left, int top, int right, int bottom)
            => trace.AppendLine($"quit-shade:{left},{top},{right},{bottom}");
        public void ServiceEvents(bool clearNew) => input.ServiceEvents(clearNew);
        public void SetDelay(int ticks)
        {
            trace.AppendLine($"delay:{ticks}");
            if (_frame >= frames.Length) throw new InvalidOperationException("Quit dialog script exhausted.");
            platform.Enqueue(frames[_frame++]);
        }
        public void DrawMessageBox(int x, int y, int shape) => trace.AppendLine($"quit-box:{x},{y},{shape}");
        public void DrawTitle(int x, int y, ReadOnlyMemory<byte> text)
            => trace.AppendLine($"help-draw:{x},{y},{Encoding.Latin1.GetString(text.Span)}");
        public void DrawMessage(int x, int y, ReadOnlyMemory<byte> text, int width)
            => trace.AppendLine($"quit-help:{x},{y},{Encoding.Latin1.GetString(text.Span)},{width}");
        public int TextWidth(ReadOnlyMemory<byte> text) { _ = text; return 40; }
        public void DrawChoice(int x, int y, ReadOnlyMemory<byte> text, int brightness)
            => trace.AppendLine($"quit-choice:{x},{y},{Encoding.Latin1.GetString(text.Span)},{brightness}");
        public void MouseStart() { input.ServiceEvents(false); trace.AppendLine("cursor-start"); }
        public void Present() => trace.AppendLine($"show:{Hash(new byte[64000])}");
        public void MouseReplace() => trace.AppendLine("cursor-restore");
        public void WaitDelay() => trace.AppendLine("wait-delay");
        public void PushJoysticksAsKeyboard() => trace.AppendLine("joystick");
        public void PlaySound(byte sample) => trace.AppendLine($"sound:{sample}");
        public void SendQuitAndHaltNetwork()
        {
            trace.AppendLine("quit-network-prepare:32");
            trace.AppendLine("quit-network-send:4");
            trace.AppendLine("quit-network-halt:0,1");
        }
    }

    private static void UpgradeSubmenuParity(StringBuilder trace)
    {
        var definitions = new ItemDefinitions();
        definitions.ParseBundle(Encoding.Latin1.GetBytes(ItemDatabaseTests.Fixture(0)), "upgrade-menu.tyrr");
        var items = ItemDatabase.FromDefinitions(definitions);
        int[] rows = [0, 1, 2, 8, 3, 5, 6];
        for (byte itemNumber = 2; itemNumber <= 8; itemNumber++)
        {
            var row = rows[itemNumber - 2];
            var count = itemNumber % 2 == 0 ? 3 : 2;
            var availability = new EpisodeItemAvailabilityState();
            availability.Counts[row] = (byte)count;
            availability.Items[row, 0] = 1;
            availability.Items[row, 1] = 0;
            availability.Items[row, 2] = 1;
            var playerItems = new PlayerItems();
            playerItems.SetMenuItemId(itemNumber - 2, itemNumber % 2 == 0 ? (byte)1 : (byte)2);
            var labels = Enumerable.Range(0, 16)
                .Select(index => (ReadOnlyMemory<byte>)Encoding.ASCII.GetBytes($"stale{index}")).ToArray();
            var result = UpgradeSubmenuBuilder.Build(itemNumber, items, availability,
                playerItems, labels, "header"u8.ToArray(), "Done"u8.ToArray());
            for (var index = 0; index < result.Labels.Length; index++)
                trace.AppendLine($"upgrade-label:{index},{Convert.ToHexString(result.Labels[index].Span)}");
            trace.AppendLine($"upgrade-state:{count},{result.ChoiceCount},{result.SelectedChoice}");

            var player = new PlayerState(0) { Items = playerItems };
            var drawingState = ItemScreenUpgradeMenuState.Create(4, itemNumber,
                result.SelectedChoice, 1, player, playerItems.Copy(), items, availability,
                Enumerable.Range(0, 8).Select(index => $"Title{index}").ToArray(), "Done");
            Check(drawingState.ChoiceCount == count + 2 &&
                drawingState.ChoicesByUpgrade[itemNumber].Count == count + 1 &&
                drawingState.ChoicesByUpgrade[itemNumber][0].Id == availability.Items[row, 0] &&
                drawingState.ChoicesByUpgrade[itemNumber][count].Id == 0,
                $"Upgrade renderer state for menu {itemNumber} did not match native availability.");
        }
    }

    private static List<Frame> Script(int kind, int mode)
    {
        var frames = new List<Frame>();
        void Key(int key, ushort mod = 0, int held = 0) => frames.Add(new(Key: key, Mod: mod, Held: held));
        void Mouse(int mx, int my, int cx, int cy, byte button, int key = 0)
            => frames.Add(new(key, X: mx, Y: my, ClickX: cx, ClickY: cy, Button: button));
        void Down(int count) { for (var i = 0; i < count; i++) Key(81); }
        var x = kind == 1 ? 20 : 140;
        var y = kind == 1 ? 50 : 54;
        var dy = kind == 1 ? 30 : 24;
        switch (mode)
        {
            case 0: Key(40); break;
            case 1: Key(41); break;
            case 2: Key(82); Key(40); break;
            case 3: Key(81); Key(40); break;
            case 4: Key(88); break;
            case 5: Key(44); break;
            case 6: Mouse(0, 0, 0, 0, 3); break;
            case 7: Mouse(x, y, x, y, 1); break;
            case 8: Mouse(x, y + 2 * dy, x, y + 2 * dy, 1); break;
            case 9: Mouse(x + 40, y, x + 40, y, 1); break;
            case 10: Mouse(x, y + 13, x, y + 13, 1); break;
            case 11: Mouse(x, y + dy, 0, 0, 1); break;
            case 12: Mouse(x, y + 2 * dy, x, y + 2 * dy, 3, 40); break;
            case 13: Mouse(x, y + dy, x, y + dy, 1, 82); break;
            case 14: Key(0); Key(0); Key(40); break;
            case 15: Key(10, 1); Down(2); Key(40); break;
            case 16: Key(48, 1); Key(10, 1); Key(48, 1); Down(3); Key(40); break;
            case 17 or 18 or 19:
                Key(10, 1); Key(48, 1);
                if (mode == 18) Key(0, held: 15);
                else { Key(15); Key(18); if (mode == 19) Key(5); Key(21); Key(7); }
                Down(4); Key(40); break;
            case 20: Key(10); Key(48); Key(82); Key(40); break;
            case 21: Mouse(x, y + dy, x, y + dy, 0); Key(40); break;
            case 22: Mouse(-1, -1, -1, -1, 1); break;
            case 23: Mouse(x + 39, y + 12, x + 39, y + 12, 1); break;
            default:
                for (var i = 0; i < mode - 21; i++) Key(mode % 2 != 0 ? 82 : 81);
                Key(40); break;
        }
        Key(41);
        return frames;
    }

    private sealed class Platform(StringBuilder trace, List<Frame> frames) : IInputPlatform
    {
        private readonly Queue<InputEvent> _events = new();
        internal InputState? Input;
        internal int FrameIndex;
        internal ushort Modifiers;
        public bool PollEvent(out InputEvent input) => _events.TryDequeue(out input);
        public uint Ticks => 0;
        public bool JoystickDown => false;
        public bool IsNetworkGame => false;
        public void Delay(uint milliseconds)
        {
            Check(FrameIndex < frames.Count, "Managed menu script exhausted.");
            trace.AppendLine($"delay:{milliseconds}");
            var frame = frames[FrameIndex++];
            Modifiers = frame.Mod;
            var active = Input!.KeysActive;
            Array.Clear(active);
            int[] lord = [15, 18, 21, 7];
            for (var i = 0; i < 4; i++) if ((frame.Held & (1 << i)) != 0) active[lord[i]] = 1;
            if (frame.X != -999) _events.Enqueue(new(InputEventKind.MouseMotion, X: frame.X, Y: frame.Y));
            if (frame.Button != 0) _events.Enqueue(new(InputEventKind.MouseButtonDown, Button: frame.Button, X: frame.ClickX, Y: frame.ClickY));
            if (frame.Key != 0) _events.Enqueue(new(InputEventKind.KeyDown, frame.Key, frame.Mod));
        }
        public void SetCursorVisible(bool visible) { }
        public void PushJoysticksAsKeyboard() => trace.AppendLine("joystick");
        public void CheckNetwork() => trace.AppendLine("keep");
        public void SetRelativeMouseMode(bool enabled) => throw new InvalidOperationException();
        public void EnableSystemMouseScaling() => throw new InvalidOperationException();
        public void ToggleFullscreen() => throw new InvalidOperationException();
        public void WindowResized() => throw new InvalidOperationException();
        public void Quit() => throw new InvalidOperationException();
        public void PollJoysticks() => throw new InvalidOperationException();
    }

    private sealed class Host(StringBuilder trace, Platform platform, InputState input, bool loaded) : ISelectionMenuHost
    {
        private readonly byte[] _screen = new byte[64000], _background = new byte[64000];
        public void EnsureCursorSprites() { if (!loaded) trace.AppendLine("cursor-load"); }
        public void LoadBackground() { trace.AppendLine("picture"); Array.Fill(_background, (byte)0xA8); }
        public void RestoreBackground() => _background.CopyTo(_screen, 0);
        public int TextWidth(ReadOnlyMemory<byte> text) => 40;
        public void DrawText(bool background, int x, int y, ReadOnlyMemory<byte> text, Font font, FontAlignment alignment, sbyte brightness)
        {
            trace.AppendLine($"draw:{Bit(background)},{x},{y},{Encoding.ASCII.GetString(text.Span)},{(int)font},{(int)alignment},{brightness}");
            var left = x - (alignment == FontAlignment.Centered ? 20 : 0);
            if (left >= 0 && left < 320 && y >= 0 && y < 200)
                (background ? _background : _screen)[y * 320 + left] = unchecked((byte)(15 * 16 + brightness + 8));
        }
        public void FadeIn() => trace.AppendLine("fade-in");
        public void PresentWithCursor()
        {
            input.ServiceEvents(false);
            trace.AppendLine("cursor-start");
            uint hash = 2166136261;
            foreach (var pixel in _screen) hash = unchecked((hash ^ pixel) * 16777619);
            trace.AppendLine($"show:{hash}");
            trace.AppendLine("cursor-restore");
        }
        public void PlaySound(byte sample) => trace.AppendLine($"sound:{sample}");
        public void FadeBlack(int steps) => trace.AppendLine($"black:{steps}");
        public void PushJoysticksAsKeyboard() => platform.PushJoysticksAsKeyboard();
        public void KeepAlive() => platform.CheckNetwork();
        public ushort Modifiers => platform.Modifiers;
        public int InitializeEpisode(int episode) { trace.AppendLine($"episode:{episode}"); return episode; }
    }

    private static void Presentation()
    {
        using var warnings = new StringWriter();
        var screen = new IndexedSurface(320, 200, 320, warnings);
        var background = new IndexedSurface(320, 200, 320, warnings);
        var shapes = new MainShapeTables();
        using var encoded = new MemoryStream();
        BinaryFile.WriteUInt16(encoded, 1);
        BinaryFile.WriteBoolean(encoded, true);
        BinaryFile.WriteUInt16(encoded, 1);
        BinaryFile.WriteUInt16(encoded, 1);
        BinaryFile.WriteUInt16(encoded, 1);
        BinaryFile.WriteExactly(encoded, new byte[] { 0xA8 });
        foreach (var table in shapes.Tables.Take(2)) { encoded.Position = 0; table.Load(encoded); }
        var platform = new Platform(new(), []);
        var input = new InputState(platform, new VideoLayout());
        var cursor = new MouseCursor(input, screen, new SpriteSheet()) { Cursor = 4 };
        var presentCount = 0;
        var failPresent = false;
        var host = new SelectionMenuPresentation(screen, background, new FontRenderer(shapes), cursor,
            () => { }, () => background.Pixels.Span.Fill(0xAE), () => { },
            () => { presentCount++; screen.Pixel(0, 0, 33); if (failPresent) throw new IOException("fixture"); },
            _ => { }, _ => { }, () => { }, () => { }, () => 0, episode => episode);
        host.LoadBackground();
        host.DrawText(true, 160, 20, "A"u8.ToArray(), Font.Large, FontAlignment.Centered, -3);
        host.RestoreBackground();
        Check(screen.Pixels.Span[20 * 320 + 159] == 0xF5 && screen.Pixels.Span[22 * 320 + 161] == 0xA7,
            "Menu presentation did not use managed font shadow/alignment.");
        host.FadeIn();
        Check(cursor.Cursor == 0, "Menu did not select the normal pointer.");
        host.PresentWithCursor();
        Check(screen.Pixels.Span[0] == 0xAE && presentCount == 1, "Menu cursor restoration did not preserve its background.");
        failPresent = true;
        try { host.PresentWithCursor(); throw new InvalidOperationException("Missing presentation failure."); }
        catch (IOException) { Check(screen.Pixels.Span[0] == 0xAE, "Failed presentation left cursor pixels on screen."); }
        Check(warnings.ToString().Length == 0, "Menu presentation emitted clipping warnings.");
        var menus = new SelectionMenus(input, host, new());
        try { menus.SelectGameplay([]); throw new InvalidOperationException("Missing label validation."); }
        catch (ArgumentException) { }
        try { menus.SelectEpisode(new ReadOnlyMemory<byte>[5], []); throw new InvalidOperationException("Missing availability validation."); }
        catch (ArgumentException) { }
    }

    private static int Bit(bool value) => value ? 1 : 0;
    private sealed class NoOpShotEffects : IPlayerShotEffects
    {
        public void Draw(int sheet, int x, int y, int frame, bool darken) { }
        public (ushort Width, ushort Height) DrawSpecial(int x, int y, int frame) => (0, 0);
        public void Explosion(int x, int y, byte type) { }
        public void SuperPixels(int x, int y, ushort count, byte width, byte color) { }
    }

    private static uint Hash(ReadOnlySpan<byte> bytes)
    {
        var hash = 2166136261u;
        foreach (var value in bytes) hash = unchecked((hash ^ value) * 16777619u);
        return hash;
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
