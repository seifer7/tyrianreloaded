using System.Buffers.Binary;
using System.Diagnostics;
using System.Text;
using OpenTyrian.Core;

namespace OpenTyrian.Core.Tests;

internal static class HighScoreTests
{
    public static void Run(string? executable = null)
    {
        var fixture = ItemDatabaseTests.Fixture(0);
        var definitions = new ItemDefinitions(); definitions.ParseBundle(Encoding.Latin1.GetBytes(fixture), "scores.tyrr");
        var items = ItemDatabase.FromDefinitions(definitions);
        var text = Enumerable.Range(0, 68).Select(i => Encoding.ASCII.GetBytes($"Text{i}")).ToArray();
        var episodes = Enumerable.Range(0, 6).Select(i => Encoding.ASCII.GetBytes($"Episode{i}")).ToArray();
        var trace = new StringBuilder();
        ulong[] cashCases = [0, 4000, 4001, uint.MaxValue, int.MaxValue, 2147483648, 3000, 10000];
        for (var mode = 0; mode < 512; mode++)
        {
            var game = new LegacyGameState
            {
                TwoPlayerMode = (mode & 8) != 0,
                InitialEpisode = (byte)(mode % 3 + 1),
                Episode = (byte)(mode % 3 + 1),
                Difficulty = (sbyte)(mode % 11)
            };
            for (var p = 0; p < 2; p++)
            {
                game.Players[p].Items.Weapons[0].Power = game.Players[p].Items.Weapons[1].Power = 1;
                game.Players[p].Cash = mode < 128 ? (ulong)(1000 + (mode * 997 + p * 1171) % 10000) : cashCases[(mode / 16 + p) % 8];
            }
            var archive = new LegacySaveArchive();
            for (var i = 0; i < archive.Slots.Length; i++)
            {
                var slot = archive.Slots[i];
                slot.HighScore1 = (mode & 64) != 0 ? 100000 + i : i % 3 * 2000;
                slot.HighScore2 = 123 + i; slot.HighScoreName = Encoding.ASCII.GetBytes($"Old{i}");
                slot.HighScoreDifficulty = (byte)(i + 10); slot.Level = (ushort)i;
            }
            trace.AppendLine($"scores:{mode}");
            new HighScores(game, archive, new PlayerProgression(game, items), text, episodes, new TraceHost(trace, mode)).Run();
            foreach (var slot in archive.Slots)
                trace.AppendLine($"slot:{slot.HighScore1},{slot.HighScore2},{Encoding.ASCII.GetString(slot.HighScoreName)}," +
                    $"{slot.HighScoreDifficulty},{slot.Level}");
        }
        TestHost();
        TestEditing(items, text, episodes);
        TestSaveNamePopup();
        if (executable is null) return;
        var path = Path.Combine(Path.GetTempPath(), $"opentyrian-scores-{Guid.NewGuid():N}.tyrr");
        File.WriteAllBytes(path, Encoding.Latin1.GetBytes(fixture));
        try
        {
            var start = new ProcessStartInfo(executable) { RedirectStandardOutput = true, RedirectStandardError = true };
            start.ArgumentList.Add(path);
            using var process = Process.Start(start) ?? throw new InvalidOperationException("Cannot start high-score reference.");
            var output = process.StandardOutput.ReadToEndAsync(); var errors = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(30000)) { process.Kill(entireProcessTree: true); throw new TimeoutException("High-score reference timed out."); }
            var expected = output.GetAwaiter().GetResult().Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
            var actual = trace.ToString().Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
            if (process.ExitCode != 0 || !expected.SequenceEqual(actual))
            {
                var index = Enumerable.Range(0, Math.Min(expected.Length, actual.Length)).FirstOrDefault(i => expected[i] != actual[i], -1);
                throw new InvalidOperationException($"High scores differ at {index}: {expected.ElementAtOrDefault(index)} / " +
                    $"{actual.ElementAtOrDefault(index)}; {errors.GetAwaiter().GetResult()}");
            }
        }
        finally { File.Delete(path); }
    }

    private static void TestSaveNamePopup()
    {
        var text = Enumerable.Range(0, 11).Select(i => Encoding.ASCII.GetBytes($"Text{i}")).ToArray();
        var host = new TraceSaveNameHost(
        [
            new(false, false, true, 0, 0, 0, 0, "ab "u8.ToArray()),
            new(true, false, false, 42, 0, 0, 0, ReadOnlyMemory<byte>.Empty),
            new(true, false, false, 40, 0, 0, 0, ReadOnlyMemory<byte>.Empty)
        ]);
        SavedGameNamePopup.Run(true, "LEVEL"u8.ToArray(), text, host);
        if (host.Saved != "AB" || host.Loaded is not null ||
            !host.Calls.Contains("wait-no-input") || !host.Calls.Contains("shade:65,55,255,155") ||
            !host.Calls.Contains("text:60,55,Text0,11,4,True") ||
            !host.Calls.Contains("adjusted:Text9") || !host.Calls.Contains("adjusted:Text10"))
            throw new InvalidOperationException("Save-name popup changed text editing, trimming, layout or save dispatch.");

        host = new TraceSaveNameHost(
        [
            new(false, false, true, 0, 0, 0, 0, "load me"u8.ToArray()),
            new(true, false, false, 88, 0, 0, 0, ReadOnlyMemory<byte>.Empty),
            new(true, false, false, 40, 0, 0, 0, ReadOnlyMemory<byte>.Empty)
        ]);
        SavedGameNamePopup.Run(false, "LEVEL"u8.ToArray(), text, host);
        if (host.Loaded != "LOAD ME" || host.Saved is not null)
            throw new InvalidOperationException("Save-name popup changed load dispatch or text capitalization.");

        host = new TraceSaveNameHost(
        [
            new(false, false, true, 0, 0, 0, 0, "cancel"u8.ToArray()),
            new(true, false, false, 41, 0, 0, 0, ReadOnlyMemory<byte>.Empty)
        ]);
        SavedGameNamePopup.Run(true, "LEVEL"u8.ToArray(), text, host);
        if (host.Saved is not null || host.Loaded is not null)
            throw new InvalidOperationException("Save-name popup did not suppress save dispatch after Escape.");
    }

    private sealed class TraceSaveNameHost(IEnumerable<HighScoreInput> inputs) : ISavedGameNamePopupHost
    {
        private readonly Queue<HighScoreInput> _inputs = new(inputs);
        public List<string> Calls { get; } = [];
        public HighScoreInput Input { get; private set; }
        public bool Playing => true;
        public string? Saved { get; private set; }
        public string? Loaded { get; private set; }
        public void EnsureMouseSprite() => Calls.Add("cursor");
        public void WaitForNoInput(CancellationToken cancellation) => Calls.Add("wait-no-input");
        public void Shade(int left, int top, int right, int bottom) => Calls.Add($"shade:{left},{top},{right},{bottom}");
        public void ServiceEvents(bool clearNew)
        {
            if (clearNew) Input = default;
            if (_inputs.Count > 0) Input = _inputs.Dequeue();
        }
        public void DrawMessageBox() => Calls.Add("message-box");
        public void DrawShadedText(int x, int y, ReadOnlyMemory<byte> text, byte color, int brightness, bool darken)
            => Calls.Add($"text:{x},{y},{Encoding.ASCII.GetString(text.Span)},{color},{brightness},{darken}");
        public void DrawName(int x, int y, ReadOnlyMemory<byte> text) => Calls.Add($"name:{x},{y},{Encoding.ASCII.GetString(text.Span)}");
        public int TextWidth(ReadOnlyMemory<byte> text, Font font) => text.Length * 5;
        public void DrawAdjustedText(int x, int y, ReadOnlyMemory<byte> text, byte color, int adjustment, Font font, bool centered)
            => Calls.Add($"adjusted:{Encoding.ASCII.GetString(text.Span)}");
        public void Fill(int left, int top, int right, int bottom, byte color) { }
        public void SetDelay(int ticks) { }
        public void MouseStart() { }
        public void Present() { }
        public void MouseReplace() { }
        public void PushJoysticks() { }
        public void ServiceWait() { }
        public void ReadMousePosition() { }
        public void PlaySong(uint song) { }
        public void SaveGame(string name) => Saved = name;
        public void LoadGame(string name) => Loaded = name;
    }

    private static void TestEditing(ItemDatabase items, byte[][] text, byte[][] episodes)
    {
        for (var kind = 0; kind < 8; kind++)
        {
            var game = new LegacyGameState { TwoPlayerMode = true, InitialEpisode = 1, Episode = 1, Difficulty = 7 };
            game.Players[0].Cash = 10000; game.Players[1].Cash = 0;
            var archive = new LegacySaveArchive();
            foreach (var slot in archive.Slots) { slot.HighScore1 = 100; slot.HighScoreName = "OLD"u8.ToArray(); slot.HighScoreDifficulty = 3; }
            var scores = new HighScores(game, archive, new PlayerProgression(game, items), text, episodes, new TraceHost(new StringBuilder(), kind));
            scores.Run();
            var saved = archive.Slots[3];
            var cancel = kind is 1 or 6;
            if (saved.HighScore1 != (cancel ? 100 : 10000) || saved.HighScoreDifficulty != (cancel ? 3 : 7))
                throw new InvalidOperationException("High-score save/cancel semantics differ.");
            if (!cancel)
            {
                var prefix = kind switch { 2 or 3 => "AB C", 4 => "ABCDEFGHIJKLMNOPQRSTUVWXYZ12", 7 => "Z", _ => "" };
                if (saved.HighScoreName.Length != 29 || Encoding.ASCII.GetString(saved.HighScoreName) != prefix.PadRight(29))
                    throw new InvalidOperationException("High-score name filtering, backspace or length differs.");
            }
        }
    }

    private static void TestHost()
    {
        var shapes = new MainShapeTables(); using var data = new MemoryStream(); BinaryFile.WriteUInt16(data, 151);
        for (var i = 0; i < 151; i++)
        {
            BinaryFile.WriteBoolean(data, true); BinaryFile.WriteUInt16(data, 1); BinaryFile.WriteUInt16(data, 1);
            BinaryFile.WriteUInt16(data, 1); data.WriteByte(0xAE);
        }
        foreach (var table in shapes.Tables) { data.Position = 0; table.Load(data); }
        var font = new FontRenderer(shapes); var sheet = new SpriteSheet();
        var bytes = new byte[663];
        for (var i = 0; i < 330; i++) BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(i * 2), 660);
        bytes[660] = 0x10; bytes[661] = 0xAE; bytes[662] = 0x0F;
        var loads = 0;
        void LoadCursor() { using var stream = new MemoryStream(bytes); sheet.Load(stream, bytes.Length); loads++; }
        var surface = new IndexedSurface(320, 200, 327, TextWriter.Null);
        var expected = new IndexedSurface(320, 200, 327, TextWriter.Null);
        var platform = new TextGlowTests.Platform(); var input = new InputState(platform, new VideoLayout());
        var timer = new DelayTimer(input); var cursor = new MouseCursor(input, surface, sheet);
        var palette = new PaletteSystem(c => (uint)(c.Red << 16 | c.Green << 8 | c.Blue));
        var colors = new PaletteColor[256];
        var initial = Enumerable.Range(0, 256).Select(i => new PaletteColor((byte)i, (byte)i, (byte)i)).ToArray();
        var glow = new TextGlow(new TextGlowHost(surface, font, input, timer, () => { }, () => { }));
        var pictures = 0; var presents = 0; var pushes = 0; var songs = new List<uint>();
        var host = new HighScoreHost(surface, sheet, shapes.Tables[5], font, palette, colors, initial, input,
            cursor, timer, glow, LoadCursor, () => pictures++, songs.Add, () => false, () => presents++, () => pushes++)
        { LegacyMouseX = 200, LegacyMouseY = 130 };
        host.EnsureCursor(); host.EnsureCursor(); host.Clear();
        host.MessageBox(); shapes.Tables[5].Blit(expected, 50, 50, 35);
        host.Title(100, 3, "TITLE"u8.ToArray(), Font.Large); font.DrawString(expected, 100, 3, "TITLE"u8, Font.Large);
        host.ShadedText(60, 55, "SCORE"u8.ToArray(), 11, 4); font.ShadeText(expected, 60, 55, "SCORE"u8, 11, 4, TextShade.Full);
        host.Name(65, 89, "NAME"u8.ToArray()); font.OutText(expected, 65, 89, "NAME"u8, 8, 3);
        host.Shade(70, 90, 74, 95); expected.ShadeRectangle(70, 90, 74, 95);
        host.Fill(69, 89, 73, 94, 138); expected.FillRectangle(69, 89, 73, 94, 138);
        if (!surface.Pixels.Span.SequenceEqual(expected.Pixels.Span) || loads != 1 ||
            host.Width("NAME"u8.ToArray()) != font.TextWidth("NAME"u8, Font.Small) ||
            host.Center("NAME"u8.ToArray(), Font.Normal) != font.Center("NAME"u8, Font.Normal))
            throw new InvalidOperationException("High-score concrete rendering differs.");
        host.WaitNoMouse(default); host.SelectPalette(); host.FadeIn(15);
        if (!palette.Active.Span.SequenceEqual(initial)) throw new InvalidOperationException("High-score palette differs.");
        host.PlaySong(33); host.LoadPicture(); host.Present(); host.SetDelay(1); host.PushJoysticks(); host.ServiceWait();
        platform.Events.Enqueue(new(InputEventKind.MouseButtonDown, Button: 1));
        host.MouseStart(); host.MouseReplace();
        platform.Events.Enqueue(new(InputEventKind.MouseButtonUp, Button: 1)); host.ServiceEvents(false);
        if (host.Input.MouseButton != 1 || host.Input.MouseX != 200 || host.Input.MouseY != 130)
            throw new InvalidOperationException("High-score legacy mouse snapshot differs.");
        platform.Events.Enqueue(new(InputEventKind.KeyDown, 40)); host.ServiceEvents(true); host.WaitInput(default);
        host.GlowFrames = 0; host.Glow(10, 180, "OK"u8.ToArray(), default);
        if (glow.Font != Font.Small || glow.Brightness != 6 || host.Playing) throw new InvalidOperationException("High-score glow/music state differs.");
        host.FadeBlack(15);
        if (pictures != 1 || pushes != 1 || presents != 31 || !songs.SequenceEqual([33u]))
            throw new InvalidOperationException("High-score host routing differs.");
    }

    private sealed class TraceHost(StringBuilder trace, int mode) : IHighScoreHost
    {
        private int _edit, _blink;
        private HighScoreInput _input;
        public void EnsureCursor() { if ((mode & 1) == 0) trace.AppendLine("cursor:1"); }
        public void WaitNoMouse(CancellationToken cancellation) { _edit = _blink = 0; trace.AppendLine("nomouse"); }
        public void Clear() => trace.AppendLine("clear");
        public void Present() => trace.AppendLine("present");
        public void SelectPalette() { }
        public void PlaySong(uint song) => trace.AppendLine($"song:{song}");
        public bool Playing => (mode & 64) != 0;
        public void Shade(int left, int top, int right, int bottom) => trace.AppendLine($"shade:{left},{top},{right},{bottom}");
        public void Fill(int left, int top, int right, int bottom, byte color) => trace.AppendLine($"fill:{left},{top},{right},{bottom},{color}");
        public void ServiceEvents(bool clearNew)
        {
            if (clearNew) { _input = default; _blink = 0; _edit++; }
            trace.AppendLine("events");
        }
        public HighScoreInput Input => _input;
        public int Center(ReadOnlyMemory<byte> text, Font font) { trace.AppendLine($"center:{Encoding.ASCII.GetString(text.Span)},{(int)font}"); return 100; }
        public int Width(ReadOnlyMemory<byte> text) { trace.AppendLine($"width:{Encoding.ASCII.GetString(text.Span)}"); return text.Length * 2; }
        public void Title(int x, int y, ReadOnlyMemory<byte> text, Font font) => trace.AppendLine($"title:{x},{y},{Encoding.ASCII.GetString(text.Span)},{(int)font}");
        public void MessageBox() => trace.AppendLine("P:50,50,35");
        public void ShadedText(int x, int y, ReadOnlyMemory<byte> text, byte color, int brightness)
            => trace.AppendLine($"shaded:{x},{y},{Encoding.ASCII.GetString(text.Span)},{color},{brightness}");
        public void Name(int x, int y, ReadOnlyMemory<byte> text) => trace.AppendLine($"name:{x},{y},{Encoding.ASCII.GetString(text.Span)}");
        public void SetDelay(int ticks) => trace.AppendLine($"delay:{ticks}");
        public void MouseStart() => trace.AppendLine("mouse");
        public void MouseReplace() => trace.AppendLine("replace");
        public void PushJoysticks() => trace.AppendLine("joystick");
        public void ServiceWait()
        {
            trace.AppendLine("wait");
            if (++_blink < ((mode & 32) != 0 ? 16 : 1)) return;
            var kind = mode % 8;
            _input = kind switch
            {
                0 => Key(40),
                1 => Key(41),
                2 or 3 => _edit == 1 ? Text("ab @cd"u8.ToArray()) : Key(_edit == 2 ? kind == 2 ? 42 : 76 : 88),
                4 => _edit == 1 ? Text("abcdefghijklmnopqrstuvwxyz12345"u8.ToArray()) : Key(40),
                5 => new(false, true, false, 0, 1, 100, 130, default),
                6 => new(false, true, false, 0, 1, 200, 130, default),
                _ => _edit == 1 ? Text([0x80, (byte)'z']) : Key(40)
            };
            static HighScoreInput Key(int scan) => new(true, false, false, scan, 0, 0, 0, default);
            static HighScoreInput Text(byte[] text) => new(false, false, true, 0, 0, 0, 0, text);
        }
        public void FadeIn(int steps) => trace.AppendLine($"fadein:{steps}");
        public void FadeBlack(int steps) => trace.AppendLine($"fade:{steps}");
        public void LoadPicture() => trace.AppendLine("picture:2");
        public uint GlowFrames { get; set; }
        public void Glow(int x, int y, ReadOnlyMemory<byte> text, CancellationToken cancellation)
        {
            trace.AppendLine($"glow:{x},{y},{Encoding.ASCII.GetString(text.Span)},{GlowFrames},2,10");
            if ((mode & 16) != 0) GlowFrames = 0;
        }
        public void WaitInput(CancellationToken cancellation) => trace.AppendLine("input");
    }
}
