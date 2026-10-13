using System.Buffers.Binary;
using System.Diagnostics;
using System.Text;
using OpenTyrian.Core;

namespace OpenTyrian.Core.Tests;

internal static class ShipInfoTests
{
    public static void Run(string? executable = null)
    {
        var trace = new StringBuilder();
        var normal = Sheet(0xAE); var extra = Sheet(0x7B);
        var definitions = new ItemDefinitions();
        var fixture = Encoding.Latin1.GetBytes(ItemDatabaseTests.Fixture(0));
        var players = new PlayerState[] { new(0), new(1) }; var graphics = new ushort[2];
        for (var mode = 0; mode < 1024; mode++)
        {
            definitions = new ItemDefinitions(); definitions.ParseBundle(fixture, "ships.tyrr"); definitions.ReserveZeroSlots();
            foreach (var type in new[] { ItemType.Ship, ItemType.Generator })
            {
                var records = definitions.Records(type).Take(3).ToArray();
                for (var i = 0; i < 3; i++)
                {
                    var text = type == ItemType.Ship
                        ? $"[ship:{records[i].Id}]\nshipgraphic={100 + i * 10}\ndmg={(byte)(mode + i)}\nani={(mode + i) % 3}\n"
                        : $"[generator:{records[i].Id}]\npower={(byte)(mode + i * 17)}\n";
                    definitions.ParseBundle(Encoding.ASCII.GetBytes(text), "ship-fields.tyrr");
                }
            }
            var items = ItemDatabase.FromDefinitions(definitions);
            var custom = new byte[154];
            for (var i = 0; i < 10; i++) { custom[i * 15] = (byte)((mode + i) % 15 + 1); custom[i * 15 + 7] = (byte)(mode + i * 13); }
            for (var p = 0; p < 2; p++)
            {
                players[p].Items.Ship = (byte)((mode & 1 << p) != 0 ? 91 + (mode / 4 + p) % 10 : (mode / 4 + p) % 3);
                players[p].Items.Generator = (byte)((mode + p) % 3);
            }
            var info = new PlayerShipInfo(players, items, normal, extra, custom, graphics);
            info.Initialize();
            trace.AppendLine($"info:{mode},{info.PowerAdd},{graphics[0]},{graphics[1]},{SheetId(info.Sheets[0])},{SheetId(info.Sheets[1])}");
            foreach (var player in players)
                trace.AppendLine($"player:{player.Armor},{player.InitialArmor},{player.ShotHitAreaX},{player.ShotHitAreaY}");
            for (ushort s = 1; s <= 10; s++)
            {
                var selected = s % 2 == 1 ? normal : extra;
                var graphic = info.ExtraGraphic(s, ref selected);
                trace.AppendLine($"extra:{s},{graphic},{SheetId(selected)}");
            }
            if (mode == 1023) TestDraw(info, normal, extra);
            int SheetId(SpriteSheet sheet) => ReferenceEquals(sheet, extra) ? 1 : 0;
        }
        var invalidItems = ItemDatabase.FromDefinitions(definitions);
        var invalid = new PlayerShipInfo(players, invalidItems, normal, extra, new byte[154], graphics);
        var pointer = normal;
        Throws<InvalidDataException>(() => invalid.ExtraGraphic(1, ref pointer));
        Throws<InvalidDataException>(() => invalid.ExtraGraphic(0, ref pointer));
        Throws<InvalidDataException>(() => invalid.ExtraGraphic(100, ref pointer));
        if (executable is null) return;
        var start = new ProcessStartInfo(executable) { RedirectStandardOutput = true, RedirectStandardError = true };
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Cannot start ship initialization reference.");
        var output = process.StandardOutput.ReadToEndAsync(); var errors = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(30000)) { process.Kill(entireProcessTree: true); throw new TimeoutException("Ship initialization reference timed out."); }
        var expected = output.GetAwaiter().GetResult().Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var actual = trace.ToString().Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        if (process.ExitCode != 0 || !expected.SequenceEqual(actual))
        {
            var index = Enumerable.Range(0, Math.Min(expected.Length, actual.Length)).FirstOrDefault(i => expected[i] != actual[i], -1);
            throw new InvalidOperationException($"Ship initialization differs at {index}: {expected.ElementAtOrDefault(index)} / " +
                $"{actual.ElementAtOrDefault(index)}; {errors.GetAwaiter().GetResult()}");
        }
    }

    private static SpriteSheet Sheet(byte color)
    {
        var bytes = new byte[663];
        for (var i = 0; i < 330; i++) BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(i * 2), 660);
        bytes[660] = 0x10; bytes[661] = color; bytes[662] = 0x0F;
        var sheet = new SpriteSheet(); using var stream = new MemoryStream(bytes); sheet.Load(stream, bytes.Length);
        return sheet;
    }

    private static void TestDraw(PlayerShipInfo info, SpriteSheet normal, SpriteSheet extra)
    {
        var surface = new IndexedSurface(320, 200, 327, TextWriter.Null);
        var expected = new IndexedSurface(320, 200, 327, TextWriter.Null);
        foreach (var large in new[] { false, true })
            foreach (var effect in new[] { SpriteEffect.Copy, SpriteEffect.Darken })
                for (var p = 0; p < 2; p++)
                {
                    surface.Pixels.Span.Fill(0x75); expected.Pixels.Span.Fill(0x75);
                    info.Sheets[p] = p == 0 ? normal : extra;
                    info.Draw(p, surface, 20, 30, 100, large, effect);
                    if (large) info.Sheets[p].Blit2x2(expected, 20, 30, 100, effect);
                    else info.Sheets[p].Blit(expected, 20, 30, 100, effect);
                    if (!surface.Pixels.Span.SequenceEqual(expected.Pixels.Span))
                        throw new InvalidOperationException("Ship initialization sheet/render routing differs.");
                }
    }

    private static void Throws<T>(Action action) where T : Exception
    {
        try { action(); } catch (T) { return; }
        throw new InvalidOperationException($"Expected {typeof(T).Name}.");
    }
}
