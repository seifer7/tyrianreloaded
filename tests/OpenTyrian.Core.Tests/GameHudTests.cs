using System.Buffers.Binary;
using System.Diagnostics;
using System.Text;
using OpenTyrian.Core;

namespace OpenTyrian.Core.Tests;

internal static class GameHudTests
{
    public static void Run(string? executable = null)
    {
        var definitions = new ItemDefinitions();
        definitions.ParseBundle(Encoding.Latin1.GetBytes(ItemDatabaseTests.Fixture(0) +
            "[special:late]\nitemgraphic=19\n[special:13]\nitemgraphic=327\n"), "hud.tyrr");
        var items = ItemDatabase.FromDefinitions(definitions);
        var players = new[] { new PlayerState(0), new PlayerState(1) };
        var surface = new IndexedSurface(320, 200, 327, TextWriter.Null);
        var effects = new SceneEffects(new byte[8]);
        var combat = new PlayerCombat(players, effects, surface);
        var enemies = new EnemySystem(items, TextWriter.Null);
        var level = new LevelEvents(items, enemies, players, new(), effects);
        var shots = new PlayerShots(items.Shots, items.Weapons, players, new UnusedEffects());
        var pickups = new PlayerPickups(players, enemies, combat, shots, items, level, effects,
            Enumerable.Range(0, 80).Select(i => $"text{i}").ToArray(), []);
        var hud = new GameHud(players, items, combat, level, pickups);
        var trace = new StringBuilder();
        var presentation = new TracePresentation(trace);
        for (var mode = 0; mode < 4096; mode++)
        {
            combat.TwoPlayers = (mode & 1) != 0; combat.Galaga = (mode & 2) != 0;
            level.OnePlayerAction = (mode & 4) != 0; pickups.NetworkGame = (mode & 8) != 0;
            combat.Cheating = (mode & 16) != 0; pickups.LocalPlayer = (byte)((mode & 32) != 0 ? 2 : 1);
            pickups.LocalName = (mode & 64) != 0 ? "local" : ""; pickups.OpponentName = (mode & 128) != 0 ? "opponent" : "";
            for (var i = 0; i < 2; i++)
            {
                players[i].Lives = (byte)((mode / 16 + i * 3) % 256);
                players[i].Cash = (mode & 256) != 0
                    ? (OperatingSystem.IsWindows() || IntPtr.Size == 4 ? uint.MaxValue : ulong.MaxValue) - (ulong)(mode + i)
                    : (ulong)(mode * 7919 + i);
                players[i].Superbombs = (uint)((mode / 32 + i) % 12);
            }
            players[0].Items.Special = (byte)(mode % 3);
            trace.AppendLine($"hud:{mode}");
            hud.Draw(presentation);
            GameHud.DrawCube(presentation, unchecked((ushort)(mode * 37)), unchecked((ushort)(mode * 19)),
                unchecked((byte)mode), unchecked((byte)(mode * 3)));
        }
        var managedTrace = trace.ToString();
        players[0].Items.Special = byte.MaxValue;
        Throws<InvalidDataException>(() => hud.Draw(presentation));
        Throws<InvalidOperationException>(() => new GameHud([players[0]], items, combat, level, pickups).Draw(presentation));
        Throws<ArgumentOutOfRangeException>(() => pickups.DefaultName(0));
        TestPresentation();
        if (executable is null) return;
        using var process = Process.Start(new ProcessStartInfo(executable) { RedirectStandardOutput = true, RedirectStandardError = true })
            ?? throw new InvalidOperationException("Cannot start HUD reference.");
        var output = process.StandardOutput.ReadToEndAsync(); var errors = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(30000)) { process.Kill(entireProcessTree: true); throw new TimeoutException("HUD reference timed out."); }
        var expected = output.GetAwaiter().GetResult().Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var actual = managedTrace.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        if (process.ExitCode != 0 || !expected.SequenceEqual(actual))
        {
            var index = Enumerable.Range(0, Math.Min(expected.Length, actual.Length)).FirstOrDefault(i => expected[i] != actual[i], -1);
            throw new InvalidOperationException($"HUD differs at {index}: {expected.ElementAtOrDefault(index)} / " +
                $"{actual.ElementAtOrDefault(index)}; {errors.GetAwaiter().GetResult()}");
        }
    }

    private static void TestPresentation()
    {
        var shapes = new MainShapeTables();
        using var encoded = new MemoryStream();
        BinaryFile.WriteUInt16(encoded, 151);
        for (var i = 0; i < 151; i++)
        {
            BinaryFile.WriteBoolean(encoded, true); BinaryFile.WriteUInt16(encoded, 1); BinaryFile.WriteUInt16(encoded, 1);
            BinaryFile.WriteUInt16(encoded, 1); encoded.WriteByte(0xAE);
        }
        foreach (var table in shapes.Tables) { encoded.Position = 0; table.Load(encoded); }
        var sheet = new SpriteSheet(); var bytes = new byte[660 + 3];
        for (var i = 0; i < 330; i++) BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(i * 2), 660);
        bytes[660] = 0x10; bytes[661] = 0xAE; bytes[662] = 0x0F;
        using var data = new MemoryStream(bytes); sheet.Load(data, bytes.Length);
        var font = new FontRenderer(shapes);
        var surface = new IndexedSurface(320, 200, 327, TextWriter.Null);
        var expected = new IndexedSurface(320, 200, 327, TextWriter.Null);
        var presentation = new GameHudPresentation(surface, font, sheet, sheet, shapes.Tables[5]);
        foreach (var canvas in new[] { surface, expected }) canvas.Pixels.Span.Fill(0x75);
        presentation.Cube(10, 20, 3, -2);
        shapes.Tables[5].Blit(expected, 14, 24, 25, SpriteEffect.Darken);
        shapes.Tables[5].Blit(expected, 13, 23, 25, SpriteEffect.Darken);
        shapes.Tables[5].Blit(expected, 10, 20, 25, SpriteEffect.Hue, 3, -2);
        presentation.Icon(9, 30, 40, 285, false); sheet.Blit(expected, 30, 40, 285);
        presentation.Icon(10, 70, 40, 19, true); sheet.Blit2x2(expected, 70, 40, 19);
        presentation.Text(100, 80, "Name\0tail", 2, 6, true);
        font.ShadeText(expected, 100, 80, Encoding.Latin1.GetBytes("Name\0tail"), 2, 6, TextShade.Full);
        presentation.Text(90, 170, "Cheaters always prosper.", 3, 4, false);
        font.OutText(expected, 90, 170, Encoding.Latin1.GetBytes("Cheaters always prosper."), 3, 4);
        if (!surface.Pixels.Span.SequenceEqual(expected.Pixels.Span) ||
            presentation.TextWidth("Name\0tail") != font.TextWidth("Name"u8, Font.Small))
            throw new InvalidOperationException("HUD concrete sprite/font routing differs.");
        Throws<ArgumentOutOfRangeException>(() => presentation.Icon(8, 0, 0, 1, false));
    }

    private static void Throws<T>(Action action) where T : Exception
    {
        try { action(); } catch (T) { return; }
        throw new InvalidOperationException($"Expected {typeof(T).Name}.");
    }

    private sealed class TracePresentation(StringBuilder trace) : IGameHudPresentation
    {
        public void Text(int x, int y, string text, byte hue, int brightness, bool shaded)
            => trace.AppendLine($"text:{x},{y},{text},{hue},{brightness},{(shaded ? 1 : 0)}");
        public int TextWidth(string text)
        {
            var width = Encoding.Latin1.GetBytes(text).Sum(c => c % 7 + 1);
            trace.AppendLine($"width:{text},{width}"); return width;
        }
        public void Icon(int sheet, int x, int y, int graphic, bool large)
            => trace.AppendLine($"icon:{sheet},{x},{y},{graphic},{(large ? 1 : 0)}");
        public void Cube(int x, int y, byte hue, sbyte brightness)
        {
            trace.AppendLine($"dark:{x + 4},{y + 4},25,0"); trace.AppendLine($"dark:{x + 3},{y + 3},25,0");
            trace.AppendLine($"hue:{x},{y},25,{hue},{brightness}");
        }
    }
    private sealed class UnusedEffects : IPlayerShotEffects
    {
        public void Draw(int sheet, int x, int y, int frame, bool darken) => throw new InvalidOperationException("HUD must not create shots.");
        public (ushort Width, ushort Height) DrawSpecial(int x, int y, int frame) => throw new InvalidOperationException("HUD must not create shots.");
        public void Explosion(int x, int y, byte type) => throw new InvalidOperationException("HUD must not create explosions.");
        public void SuperPixels(int x, int y, ushort count, byte width, byte color) => throw new InvalidOperationException("HUD must not create particles.");
    }
}
