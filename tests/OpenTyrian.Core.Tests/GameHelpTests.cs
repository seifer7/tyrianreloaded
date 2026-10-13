using System.Diagnostics;
using System.Text;
using OpenTyrian.Core;

namespace OpenTyrian.Core.Tests;

internal static class GameHelpTests
{
    public static void Run(string? executable = null)
    {
        var trace = new StringBuilder();
        for (var mode = 0; mode < 32; mode++)
        {
            var host = new TraceHost(trace, mode);
            var window = new GameTextWindow(() => { }, _ => { }) { EraseTicks = 100 };
            trace.AppendLine($"help:{mode}");
            new GameHelp(host, window).Run((mode & 1) != 0, "Continue"u8.ToArray());
            trace.AppendLine($"state:{window.EraseTicks},12,3,{Bit(host.NewKey)},{Bit(host.NewMouse)},1");
        }
        TestHost();
        if (executable is null) return;
        using var process = Process.Start(new ProcessStartInfo(executable) { RedirectStandardOutput = true, RedirectStandardError = true })
            ?? throw new InvalidOperationException("Cannot start game help reference.");
        var output = process.StandardOutput.ReadToEndAsync(); var errors = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(30000)) { process.Kill(entireProcessTree: true); throw new TimeoutException("Game help reference timed out."); }
        var expected = output.GetAwaiter().GetResult().Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var actual = trace.ToString().Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        if (process.ExitCode != 0 || !expected.SequenceEqual(actual))
        {
            var index = Enumerable.Range(0, Math.Min(expected.Length, actual.Length)).FirstOrDefault(i => expected[i] != actual[i], -1);
            throw new InvalidOperationException($"Game help differs at {index}: {expected.ElementAtOrDefault(index)} / " +
                $"{actual.ElementAtOrDefault(index)}; {errors.GetAwaiter().GetResult()}");
        }
    }

    private static void TestHost()
    {
        var shapes = new MainShapeTables();
        using var data = new MemoryStream(); BinaryFile.WriteUInt16(data, 151);
        for (var i = 0; i < 151; i++)
        {
            BinaryFile.WriteBoolean(data, true); BinaryFile.WriteUInt16(data, 1); BinaryFile.WriteUInt16(data, 1);
            BinaryFile.WriteUInt16(data, 1); data.WriteByte(0xAF);
        }
        foreach (var table in shapes.Tables) { data.Position = 0; table.Load(data); }
        var surface = new IndexedSurface(320, 200, 327, TextWriter.Null);
        var expected = new IndexedSurface(320, 200, 327, TextWriter.Null);
        foreach (var canvas in new[] { surface, expected }) canvas.Pixels.Span.Fill(0x75);
        var platform = new TextGlowTests.Platform(); var input = new InputState(platform, new VideoLayout());
        var cursor = new MouseCursor(input, surface, new SpriteSheet()) { HasMouse = false };
        var font = new FontRenderer(shapes); var help = new HelpTextRenderer(font); var text = new HelpTextData();
        for (var i = 0; i < text.Help.Length; i++) text.Help[i] = "Two words"u8.ToArray();
        var presents = 0; var keeps = 0; var pushes = 0;
        var host = new GameHelpHost(surface, font, shapes.Tables[5], help, text, input, cursor, () => presents++,
            () => { pushes++; platform.Events.Enqueue(new(InputEventKind.KeyDown, 40)); }, () => keeps++);
        host.Shade(1, 1, 262, 182); expected.ShadeRectangle(1, 1, 262, 182);
        host.Fill(9, 9, 254, 174, 0); expected.FillRectangle(9, 9, 254, 174, 0);
        host.Option(15, 5, 40); shapes.Tables[5].Blit(expected, 15, 5, 40);
        host.Box(40, 10, 31, 45, 5, 3);
        var renderer = new HelpTextRenderer(font) { Color = 5, Brightness = 3 };
        renderer.DrawHelp(expected, 40, 10, text, 31, 45);
        host.Text(20, 190, "Continue"u8.ToArray()); font.OutText(expected, 20, 190, "Continue"u8, 0, 4);
        if (!surface.Pixels.Span.SequenceEqual(expected.Pixels.Span) || help.Color != 12 || help.Brightness != 3 ||
            host.TextWidth("Continue"u8.ToArray()) != font.TextWidth("Continue"u8, Font.Small))
            throw new InvalidOperationException("Game help concrete font/shape/box presentation differs.");
        var window = new GameTextWindow(surface, font, shapes.Tables[5]) { EraseTicks = 100 };
        new GameHelp(host, window).Run(false, "Continue"u8.ToArray());
        if (presents != 1 || keeps != 1 || pushes != 1 || platform.Ticks != 16 || !input.NewKey || window.EraseTicks != 1)
            throw new InvalidOperationException("Game help input/presentation/network integration differs.");
        using var cancel = new CancellationTokenSource(); cancel.Cancel();
        try { new GameHelp(host, window).Run(true, "Continue"u8.ToArray(), cancel.Token); throw new InvalidOperationException("Help did not cancel."); }
        catch (OperationCanceledException) { }
    }

    private static int Bit(bool value) => value ? 1 : 0;
    internal sealed class TraceHost(StringBuilder trace, int mode) : IGameHelpHost
    {
        private int _frame;
        public bool NewKey { get; private set; } = true;
        public bool NewMouse { get; private set; } = true;
        public void ClearKeyboard() => trace.AppendLine("clear");
        public void WipeKey() => trace.AppendLine("wipe");
        public void Shade(int l, int t, int r, int b) => trace.AppendLine($"shade:{l},{t},{r},{b}");
        public void Fill(int l, int t, int r, int b, byte color) => trace.AppendLine($"fill:{l},{t},{r},{b},{color}");
        public void Option(int x, int y, int sprite) => trace.AppendLine($"option:{x},{y},{sprite}");
        public void Box(int x, int y, uint number, uint width, byte hue, byte brightness) => trace.AppendLine($"box:{x},{y},{number},{width},{hue},{brightness}");
        public int TextWidth(ReadOnlyMemory<byte> text) { trace.AppendLine($"width:{Encoding.Latin1.GetString(text.Span)}"); return 39; }
        public void Text(int x, int y, ReadOnlyMemory<byte> text) => trace.AppendLine($"text:{x},{y},{Encoding.Latin1.GetString(text.Span)}");
        public void ServiceEvents(bool clear)
        {
            trace.AppendLine($"events:{Bit(clear)}");
            if (clear) { NewKey = NewMouse = false; _frame++; }
            else { NewKey = (mode & 2) == 0 && _frame == mode % 5 + 1; NewMouse = (mode & 2) != 0 && _frame == mode % 5 + 1; }
        }
        public void MouseStart() => trace.AppendLine("mouse");
        public void Present() => trace.AppendLine("present");
        public void MouseReplace() => trace.AppendLine("replace");
        public void Delay(uint milliseconds) => trace.AppendLine($"delay:{milliseconds}");
        public void PushJoysticks() => trace.AppendLine("joysticks");
        public void KeepAlive() => trace.AppendLine("keep");
    }
}
