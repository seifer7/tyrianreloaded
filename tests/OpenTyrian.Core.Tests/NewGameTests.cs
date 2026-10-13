using System.Buffers.Binary;
using System.Diagnostics;
using System.Text;
using OpenTyrian.Core;

namespace OpenTyrian.Core.Tests;

internal static class NewGameTests
{
    public static void Run(string? executable = null)
    {
        var fixture = ItemDatabaseTests.Fixture(0);
        for (var i = 1; i <= 12; i++) fixture += $"[ship:{i}]\nshipgraphic={(i == 1 ? 1 : i * 3)}\n";
        foreach (var category in new[] { "weapon", "special" })
            for (var i = 1; i <= 40; i++) fixture += $"[{category}:{i}]\nname={category}{i}\n";
        var definitions = new ItemDefinitions(); definitions.ParseBundle(Encoding.Latin1.GetBytes(fixture), "new-game.tyrr");
        var items = ItemDatabase.FromDefinitions(definitions);
        var text = new HelpTextData();
        for (var i = 0; i < text.SuperShips.Length; i++) text.SuperShips[i] = Encoding.ASCII.GetBytes($"Super{i}");
        text.MiscTextB[4] = "0123456789"u8.ToArray(); text.PlanetNames[0] = "ABCDEFGHIJKLMNO"u8.ToArray();
        var trace = new StringBuilder();
        for (var mode = 0; mode < 2048; mode++)
        {
            var game = Reset(mode, items); var newGame = Create(mode, game);
            trace.AppendLine($"normal:{mode}"); var result = newGame.Run(); trace.AppendLine($"result:{Bit(result)}"); Dump(game, newGame);
        }
        for (var mode = 0; mode < 256; mode++)
            for (var ship = 0; ship < 7; ship++)
            {
                var game = Reset(mode, items); var newGame = Create(mode, game);
                trace.AppendLine($"arcade:{mode},{ship}"); var result = newGame.RunSuperArcade(ship); trace.AppendLine($"result:{Bit(result)}"); Dump(game, newGame);
            }
        for (var mode = 0; mode < 256; mode++)
        {
            var game = Reset(mode, items); var newGame = Create(mode, game);
            trace.AppendLine($"super:{mode}"); newGame.RunSuperTyrian(); Dump(game, newGame);
        }
        TestHost(items, text);
        if (executable is null) return;
        var path = Path.Combine(Path.GetTempPath(), $"opentyrian-new-game-{Guid.NewGuid():N}.tyrr");
        File.WriteAllBytes(path, Encoding.Latin1.GetBytes(fixture));
        try
        {
            var start = new ProcessStartInfo(executable) { RedirectStandardOutput = true, RedirectStandardError = true };
            start.ArgumentList.Add(path);
            using var process = Process.Start(start) ?? throw new InvalidOperationException("Cannot start new-game oracle.");
            var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(30000)) { process.Kill(entireProcessTree: true); throw new TimeoutException("New-game oracle timed out."); }
            var expected = output.GetAwaiter().GetResult().Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
            var actual = trace.ToString().Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
            if (process.ExitCode != 0 || !expected.SequenceEqual(actual))
            {
                var index = Enumerable.Range(0, Math.Min(expected.Length, actual.Length)).FirstOrDefault(i => expected[i] != actual[i], -1);
                throw new InvalidOperationException($"New-game flow differs at {index}: {expected.ElementAtOrDefault(index)} / " +
                    $"{actual.ElementAtOrDefault(index)}; {error.GetAwaiter().GetResult()}");
            }
        }
        finally { File.Delete(path); }

        NewGame Create(int mode, LegacyGameState game)
        {
            var input = new InputState(new TextGlowTests.Platform(), new()); input.KeysActive[71] = (mode & 128) != 0 ? (byte)1 : (byte)0;
            return new(game, items, text, input, new TraceHost(trace, mode, game)) { RichMode = (mode & 64) != 0, ConstantDie = true };
        }
        void Dump(LegacyGameState game, NewGame flow)
        {
            trace.AppendLine($"state:{Bit(game.GameLoaded)},{Bit(game.OnePlayerAction)},{Bit(game.TwoPlayerMode)},{Bit(game.SuperTyrian)}," +
                $"{game.SuperArcadeMode},{Bit(flow.ConstantDie)},{game.Difficulty},{game.InitialDifficulty},{game.Episode},{game.InputDevices[0]},{game.InputDevices[1]}");
            foreach (var player in game.Players)
                trace.AppendLine($"player:{player.Cash},{player.Items.Ship},{player.Items.Weapons[0].Id},{player.Items.Special}," +
                    $"{player.Items.Sidekicks[0]},{player.Items.Sidekicks[1]}");
        }
    }

    private static LegacyGameState Reset(int mode, ItemDatabase items)
    {
        var game = new LegacyGameState
        {
            GameLoaded = (mode & 8) != 0,
            OnePlayerAction = (mode & 16) != 0,
            TwoPlayerMode = (mode & 32) != 0,
            SuperTyrian = (mode & 128) != 0,
            SuperArcadeMode = 5,
            Difficulty = unchecked((sbyte)(mode / 8 % 256)),
            InitialDifficulty = -3,
            Episode = (byte)(mode % 4 + 1)
        };
        game.InputDevices[0] = 7; game.InputDevices[1] = 8;
        for (var i = 0; i < 2; i++)
        {
            game.Players[i].Cash = (ulong)(999 + i);
            game.Players[i].Items.Ship = checked((byte)items.KnownIndex(KnownItem.ShipUspTalon));
            game.Players[i].Items.Weapons[0].Id = checked((byte)items.KnownIndex(KnownItem.WeaponFrontPulseCannon));
            game.Players[i].Items.Special = 3; game.Players[i].Items.Sidekicks[0] = 4; game.Players[i].Items.Sidekicks[1] = 5;
        }
        return game;
    }

    private static void TestHost(ItemDatabase items, HelpTextData text)
    {
        var game = Reset(0, items); game.Difficulty = 2;
        var selection = new SelectionMenuState(); var platform = new TextGlowTests.Platform();
        var input = new InputState(platform, new()); var timer = new DelayTimer(input);
        var menuHost = new MenuHost(input, platform); var menus = new SelectionMenus(input, menuHost, selection);
        var shapes = new MainShapeTables(); using var glyphs = new MemoryStream(); BinaryFile.WriteUInt16(glyphs, 127);
        for (var i = 0; i < 127; i++)
        {
            BinaryFile.WriteBoolean(glyphs, true); BinaryFile.WriteUInt16(glyphs, 1); BinaryFile.WriteUInt16(glyphs, 1);
            BinaryFile.WriteUInt16(glyphs, 2); glyphs.WriteByte(0xae); glyphs.WriteByte(254);
        }
        foreach (var table in shapes.Tables) { glyphs.Position = 0; table.Load(glyphs); }
        var font = new FontRenderer(shapes); var ships = new SpriteSheet(); var bytes = new byte[663];
        for (var i = 0; i < 330; i++) BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(i * 2), 660);
        bytes[660] = 0x10; bytes[661] = 0xae; bytes[662] = 0x0f;
        using var sprites = new MemoryStream(bytes); ships.Load(sprites, bytes.Length);
        var primary = new IndexedSurface(320, 200, 327, TextWriter.Null); var expected = new IndexedSurface(320, 200, 327, TextWriter.Null);
        var palette = new PaletteSystem(c => (uint)(c.Red << 16 | c.Green << 8 | c.Blue));
        var colors = Enumerable.Range(0, 256).Select(i => new PaletteColor((byte)i, (byte)i, (byte)i)).ToArray();
        var loads = 0; var presents = 0; var songs = new List<uint>(); var samples = new List<byte>();
        var host = new NewGameHost(game, menus, selection, text, [true, true, true, true], primary, font, ships, palette, colors, input,
            timer, () => loads++, () => presents++, songs.Add, samples.Add, episode => episode);
        if (!host.SelectGameplay() || game.OnePlayerAction || game.TwoPlayerMode || !host.SelectEpisode() ||
            game.Episode != 1 || game.InitialEpisode != 1 || !host.SelectDifficulty() || game.Difficulty != 2 || menuHost.Inits != 1)
            throw new InvalidOperationException("New-game concrete selection/state ownership differs.");
        primary.Pixels.Span.Fill(0x75); host.Clear();
        host.Title(30, "SUPER"u8.ToArray(), Font.Large);
        font.DrawString(expected, font.Center("SUPER"u8, Font.Large), 30, "SUPER"u8, Font.Large);
        host.Ship(9); ships.Blit2x2(expected, 148, 70, 9);
        host.Text(10, "Ready"u8.ToArray()); font.OutText(expected, 10, 10, "Ready"u8, 15, 4);
        if (!primary.Pixels.Span.SequenceEqual(expected.Pixels.Span)) throw new InvalidOperationException("New-game concrete titles/ships/text differ.");
        host.LoadPicture(); host.Present(); host.PlaySong(16); host.Sample(35); host.FadeIn(50);
        if (!palette.Active.Span.SequenceEqual(colors) || loads != 1 || presents != 51 ||
            !songs.SequenceEqual([16u]) || !samples.SequenceEqual(new byte[] { 35 }))
            throw new InvalidOperationException("New-game palette/audio/picture services differ.");
        host.FadeBlack(); if (presents != 61) throw new InvalidOperationException("New-game exit fade differs.");
        platform.Events.Enqueue(new(InputEventKind.KeyUp, 40)); host.WaitNoInput(default);
        platform.Events.Enqueue(new(InputEventKind.KeyDown, 40)); host.WaitInput(default);
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        try { new NewGame(game, items, text, input, host).RunSuperTyrian(cancellation.Token); throw new InvalidOperationException("New game ignored cancellation."); }
        catch (OperationCanceledException) { }
        menuHost.Cancel = true; game.OnePlayerAction = true; game.TwoPlayerMode = true; game.Episode = 3; game.InitialEpisode = 2; game.Difficulty = 7;
        if (host.SelectGameplay() || !game.OnePlayerAction || !game.TwoPlayerMode || host.SelectEpisode() ||
            game.Episode != 3 || game.InitialEpisode != 2 || host.SelectDifficulty() || game.Difficulty != 7)
            throw new InvalidOperationException("Canceled concrete selections overwrote existing game mode state.");
    }

    private static int Bit(bool value) => value ? 1 : 0;
    private sealed class TraceHost(StringBuilder trace, int mode, LegacyGameState game) : INewGameHost
    {
        public bool SelectGameplay() { trace.AppendLine("gameplay"); return (mode & 1) != 0; }
        public bool SelectEpisode() { trace.AppendLine("episode"); return (mode & 2) != 0; }
        public bool SelectDifficulty() { trace.AppendLine("difficulty"); return (mode & 4) != 0; }
        public void LoadPicture() => trace.AppendLine("picture:1");
        public void Clear() => trace.AppendLine("clear");
        public void Title(int y, ReadOnlyMemory<byte> text, Font font) => trace.AppendLine($"title:{y},{Encoding.ASCII.GetString(text.Span)},{(int)font}");
        public void Ship(ushort graphic) => trace.AppendLine($"ship:{graphic}");
        public void Text(int y, ReadOnlyMemory<byte> text) => trace.AppendLine($"text:{y},{Encoding.ASCII.GetString(text.Span)}");
        public void Present() => trace.AppendLine("present");
        public void FadeIn(int steps) => trace.AppendLine($"fadein:{steps}");
        public void FadeBlack() => trace.AppendLine("fade:10");
        public void WaitNoInput(CancellationToken cancellation) => trace.AppendLine("noinput");
        public void WaitInput(CancellationToken cancellation) => trace.AppendLine("input");
        public void PlaySong(uint song) => trace.AppendLine($"song:{song}");
        public void Sample(byte number) => trace.AppendLine($"sample:{number}");
        public byte InitializeEpisode(byte episode) { trace.AppendLine($"init:{episode}"); game.Episode = episode; return episode; }
    }

    private sealed class MenuHost(InputState input, TextGlowTests.Platform platform) : ISelectionMenuHost
    {
        public bool Cancel;
        public int Inits;
        public void EnsureCursorSprites() { }
        public void LoadBackground() { }
        public void RestoreBackground() { }
        public int TextWidth(ReadOnlyMemory<byte> text) => 20;
        public void DrawText(bool background, int x, int y, ReadOnlyMemory<byte> text, Font font, FontAlignment alignment, sbyte brightness) { }
        public void FadeIn() { }
        public void PresentWithCursor() { }
        public void PlaySound(byte sample) { }
        public void FadeBlack(int steps) { }
        public void PushJoysticksAsKeyboard() => platform.Events.Enqueue(new(InputEventKind.KeyDown, Cancel ? 41 : 40));
        public void KeepAlive() { }
        public ushort Modifiers => input.LastModifiers;
        public int InitializeEpisode(int episode) { Inits++; return episode; }
    }
}
