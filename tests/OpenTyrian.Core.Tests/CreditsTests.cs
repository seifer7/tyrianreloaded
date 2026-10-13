using System.Buffers.Binary;
using System.Diagnostics;
using System.Text;
using OpenTyrian.Core;

namespace OpenTyrian.Core.Tests;

internal static class CreditsTests
{
    public static void Run(string? executable = null)
    {
        var definitions = new ItemDefinitions();
        definitions.ParseBundle(Encoding.Latin1.GetBytes(ItemDatabaseTests.Fixture(0)), "credits.tyrr");
        // Credits selects the first eleven loaded ships, not known-item IDs.
        definitions.ReserveZeroSlots();
        foreach (var (record, index) in definitions.Records(ItemType.Ship).Select((record, index) => (record, index)).ToArray())
            definitions.ParseBundle(Encoding.ASCII.GetBytes($"[ship:{record.Id}]\nshipgraphic={100 + index * 10}\n"), "credits-ships.tyrr");
        for (var i = definitions.Records(ItemType.Ship).Count; i < 12; i++)
            definitions.ParseBundle(Encoding.ASCII.GetBytes($"[ship:credits{i}]\nshipgraphic={100 + i * 10}\n"), "credits-ships.tyrr");
        var items = ItemDatabase.FromDefinitions(definitions);
        var trace = new StringBuilder();
        int[] limits = [1, 200, 1001, 7381, 7900, 8000];
        for (var mode = 0; mode < limits.Length; mode++)
        {
            var host = new TraceHost(trace, mode % 4 + 1, limits[mode]);
            MersenneTwister.Seed((uint)(mode + 1));
            trace.AppendLine($"credits:{mode}");
            new Credits(items, "Episode"u8.ToArray(), host).Run();
            trace.AppendLine($"rng:{MersenneTwister.NextUInt32()}");
            if (!host.Freed || host.Frames != limits[mode]) throw new InvalidOperationException("Credits lifetime/exit differs.");
        }
        var cancelled = new TraceHost(new StringBuilder(), 1, 1);
        Throws<OperationCanceledException>(() => new Credits(items, ReadOnlyMemory<byte>.Empty, cancelled).Run(new CancellationToken(true)));
        if (!cancelled.Freed) throw new InvalidOperationException("Cancelled credits leaked pictures.");
        var truncated = new TraceHost(new StringBuilder(), 1, 1) { Truncated = true };
        Throws<EndOfStreamException>(() => new Credits(items, ReadOnlyMemory<byte>.Empty, truncated).Run());
        if (!truncated.Freed) throw new InvalidOperationException("Malformed credits leaked pictures.");
        TestHost();
        if (executable is null) return;
        var start = new ProcessStartInfo(executable) { RedirectStandardOutput = true, RedirectStandardError = true };
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Cannot start credits reference.");
        var output = process.StandardOutput.ReadToEndAsync(); var errors = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(30000)) { process.Kill(entireProcessTree: true); throw new TimeoutException("Credits reference timed out."); }
        var expected = output.GetAwaiter().GetResult().Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var actual = trace.ToString().Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        if (process.ExitCode != 0 || !expected.SequenceEqual(actual))
        {
            var index = Enumerable.Range(0, Math.Min(expected.Length, actual.Length)).FirstOrDefault(i => expected[i] != actual[i], -1);
            throw new InvalidOperationException($"Credits differ at {index}: {expected.ElementAtOrDefault(index)} / " +
                $"{actual.ElementAtOrDefault(index)}; {errors.GetAwaiter().GetResult()}");
        }
    }

    private static byte[] TextData()
    {
        using var stream = new MemoryStream();
        byte[] key = [204, 129, 63, 255, 71, 19, 25, 62, 1, 99];
        for (var i = 0; i < Credits.LineCount; i++)
        {
            var bytes = Encoding.ASCII.GetBytes(i % 17 == 0 ? "." : i % 13 == 0 ? "" : $"{(char)(65 + i % 16)}Line{i}");
            stream.WriteByte((byte)bytes.Length);
            byte previous = 0;
            for (var n = 0; n < bytes.Length; n++) { previous = (byte)(bytes[n] ^ key[n % key.Length] ^ previous); stream.WriteByte(previous); }
        }
        return stream.ToArray();
    }

    private static void TestHost()
    {
        var root = Path.Combine(Path.GetTempPath(), $"opentyrian-credits-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            File.WriteAllBytes(Path.Combine(root, "tyrian1.lvl"), []);
            File.WriteAllBytes(Path.Combine(root, "tyrian.cdt"), TextData());
            using var data = new MemoryStream(); BinaryFile.WriteUInt16(data, 151);
            for (var i = 0; i < 151; i++)
            {
                BinaryFile.WriteBoolean(data, true); BinaryFile.WriteUInt16(data, 1); BinaryFile.WriteUInt16(data, 1);
                BinaryFile.WriteUInt16(data, 1); data.WriteByte(0xAE);
            }
            File.WriteAllBytes(Path.Combine(root, "estsc.shp"), data.ToArray());
            var shapes = new MainShapeTables();
            foreach (var table in shapes.Tables) { data.Position = 0; table.Load(data); }
            var font = new FontRenderer(shapes); var pictures = new SpriteTable();
            var sheet = new SpriteSheet(); var bytes = new byte[663];
            for (var i = 0; i < 330; i++) BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(i * 2), 660);
            bytes[660] = 0x10; bytes[661] = 0xAE; bytes[662] = 0x0F;
            using var encoded = new MemoryStream(bytes); sheet.Load(encoded, bytes.Length);
            var surface = new IndexedSurface(320, 200, 327, TextWriter.Null);
            var expected = new IndexedSurface(320, 200, 327, TextWriter.Null);
            var platform = new TextGlowTests.Platform(); var input = new InputState(platform, new VideoLayout());
            var timer = new DelayTimer(input);
            var palette = new PaletteSystem(c => (uint)(c.Red << 16 | c.Green << 8 | c.Blue));
            var colors = Enumerable.Range(0, 256).Select(i => new PaletteColor((byte)i, (byte)i, (byte)i)).ToArray();
            var presents = 0; var keeps = 0; var fades = 0; var songs = new List<uint>();
            var host = new CreditsHost(new DataFiles(root, root), surface, pictures, sheet, font, palette, colors,
                input, timer, songs.Add, () => fades++, () => presents++, () => keeps++);
            host.LoadPictures(); host.Clear();
            host.Picture(0, 280, 90, -4); pictures.Blit(expected, 280, 90, 0, SpriteEffect.Hue, 0, -4);
            host.Ship(15, 160, 100); sheet.Blit2x2(expected, 15, 160, 100);
            host.Text(12, 20, "Credits"u8.ToArray(), 3, -2);
            font.OutTextAdjust(expected, 12, 20, "Credits"u8, 3, -2, Font.Normal, false);
            host.Fill(0, 0, 319, 10); expected.FillRectangle(0, 0, 319, 10, 0);
            if (!surface.Pixels.Span.SequenceEqual(expected.Pixels.Span) || host.PictureSize(0) != (1, 1) ||
                host.TextWidth("Credits"u8.ToArray()) != font.TextWidth("Credits"u8, Font.Normal))
                throw new InvalidOperationException("Credits concrete font/ship/picture pixels differ.");
            using (var text = host.OpenText())
            {
                var buffer = new byte[66]; EncryptedText.ReadPascalString(text, buffer);
                if (buffer[0] != '.' || buffer[1] != 0) throw new InvalidOperationException("Credits text loading differs.");
            }
            host.SetPictureDelay(1000); host.SetDelay(1); host.Wait();
            if (host.PictureDelay == 0) throw new InvalidOperationException("Credits picture timer expired early.");
            host.PlaySong(8); host.FadeSong(); host.Present(); host.KeepAlive(); host.FadeIn(2);
            if (!palette.Active.Span.SequenceEqual(colors)) throw new InvalidOperationException("Credits palette differs.");
            platform.Events.Enqueue(new(InputEventKind.KeyDown, 40));
            if (!host.AnyButton()) throw new InvalidOperationException("Credits input was not polled.");
            host.FadeBlack(10); host.FreePictures();
            if (pictures.Count != 0 || presents != 13 || keeps != 1 || fades != 1 || !songs.SequenceEqual([8u]) ||
                palette.Active.Span.ToArray().Any(c => c.Red != 0 || c.Green != 0 || c.Blue != 0))
                throw new InvalidOperationException("Credits concrete host lifecycle differs.");
        }
        finally
        {
            foreach (var name in new[] { "tyrian1.lvl", "tyrian.cdt", "estsc.shp" }) File.Delete(Path.Combine(root, name));
            Directory.Delete(root);
        }
    }

    private static void Throws<T>(Action action) where T : Exception
    {
        try { action(); } catch (T) { return; }
        throw new InvalidOperationException($"Expected {typeof(T).Name}.");
    }

    private sealed class TraceHost(StringBuilder trace, int count, int limit) : ICreditsHost
    {
        private int _ticks, _target;
        public int Frames { get; private set; }
        public bool Freed { get; private set; }
        public bool Truncated { get; init; }
        public int PictureCount => count;
        public void LoadPictures() => trace.AppendLine("load:estsc.shp");
        public (ushort Width, ushort Height) PictureSize(int index) => ((ushort)(10 + index * 3), (ushort)(20 + index * 5));
        public Stream OpenText() { trace.AppendLine("textfile"); return new MemoryStream(Truncated ? [] : TextData()); }
        public void SetDelay(int ticks) => trace.AppendLine($"delay:{ticks}");
        public void SetPictureDelay(int ticks) { _target = _ticks + ticks; trace.AppendLine($"delay2:{ticks}"); }
        public uint PictureDelay => (uint)Math.Max(0, _target - _ticks);
        public void PlaySong(uint song) => trace.AppendLine($"song:{song}");
        public void FadeSong() => trace.AppendLine("fadesong");
        public void Clear() => trace.AppendLine("clear");
        public void Present() => trace.AppendLine("present");
        public void FadeIn(int steps) => trace.AppendLine($"fadein:{steps}");
        public void Picture(int index, int x, int y, sbyte brightness) => trace.AppendLine($"picture:{index},{x},{y},{brightness}");
        public void Ship(int x, int y, uint graphic) => trace.AppendLine($"ship:{x},{y},{graphic}");
        public int TextWidth(ReadOnlyMemory<byte> text) { trace.AppendLine($"width:{Encoding.ASCII.GetString(text.Span)}"); return text.Length * 2; }
        public void Text(int x, int y, ReadOnlyMemory<byte> text, byte color, int brightness)
            => trace.AppendLine($"text:{x},{y},{Encoding.ASCII.GetString(text.Span)},{color},{brightness}");
        public void Fill(int left, int top, int right, int bottom) => trace.AppendLine($"fill:{left},{top},{right},{bottom}");
        public void KeepAlive() => trace.AppendLine("keep");
        public void Wait() { _ticks++; trace.AppendLine("wait"); }
        public bool AnyButton() { var done = ++Frames == limit; trace.AppendLine($"button:{(done ? 1 : 0)}"); return done; }
        public void FadeBlack(int steps) => trace.AppendLine($"fade:{steps}");
        public void FreePictures() { Freed = true; trace.AppendLine("free"); }
    }
}
