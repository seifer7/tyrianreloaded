using System.Diagnostics;
using System.Text;
using OpenTyrian.Core;

namespace OpenTyrian.Core.Tests;

internal static class InGameSetupTests
{
    public static void Run(string? executable = null)
    {
        var text = new HelpTextData();
        Fill(text.InGameText, "Menu"); Fill(text.DetailLevels, "Detail"); Fill(text.GameSpeeds, "Speed"); Fill(text.MainMenuHelp, "Help");
        var trace = new StringBuilder();
        int[] scans = [82, 81, 80, 79, 44, 40, 88, 41, 26, 0];
        for (var mode = 0; mode < 2048; mode++)
        {
            var settings = new GameRuntimeSettings { ProcessorType = (byte)(mode % 6 + 1), GameSpeed = (byte)(mode % 5 + 1) };
            var config = new DosConfiguration { MusicVolume = (ushort)(mode % 256), EffectsVolume = (ushort)(255 - mode % 256) };
            var host = new TraceHost(trace, mode) { MusicDisabled = (mode & 1) != 0, SamplesDisabled = (mode & 2) != 0 };
            var setup = new InGameSetup(settings, config, text, host)
            { Selected = mode % 6, ConstantPlay = (mode & 4) != 0, NetworkGame = (mode & 8) != 0 };
            var restart = true;
            trace.AppendLine($"setup:{mode}"); setup.Draw(ref restart); setup.Draw(ref restart);
            host.Input = new((mode & 2) != 0, (mode & 1) != 0, scans[mode / 4 % 10], mode % 241 - 10,
                mode / 8 % 161 - 10, mode % 3 + 1, mode * 7 % 241 - 10, mode * 13 % 161 - 10);
            var action = new InGameSetupAction();
            setup.ReadMouse((mode & 4) != 0, action); setup.ReadKeyboard(action);
            trace.AppendLine($"read:{setup.Selected},{Bit(action.Activate)},{Bit(action.Decrease)},{Bit(action.Increase)}," +
                $"{Bit(action.Close)},{Bit(action.VolumeChanged)},{action.VolumeItem},{action.VolumeValue}");
            action = new()
            {
                Activate = (mode & 1) != 0,
                Decrease = (mode & 2) != 0,
                Increase = (mode & 4) != 0,
                Close = (mode & 8) != 0,
                VolumeChanged = (mode & 16) != 0,
                VolumeItem = mode % 3,
                VolumeValue = mode % 256
            };
            var done = false; var result = false;
            setup.Apply(action, ref done, ref result);
            trace.AppendLine($"state:{settings.ProcessorType},{settings.GameSpeed},{config.MusicVolume},{config.EffectsVolume}," +
                $"{Bit(host.MusicDisabled)},{Bit(host.SamplesDisabled)},{Bit(setup.HaltGame)},{Bit(setup.PlayerEndLevel)},{Bit(done)},{Bit(result)}");
            host.ResetWait();
            host.Input = host.Input with { MouseX = mode % 2 != 0 ? -1 : 10, MouseY = mode % 3 != 0 ? -1 : 20 };
            var moved = setup.Wait();
            trace.AppendLine($"moved:{Bit(moved)}");
        }
        for (var mode = 0; mode < 16; mode++)
        {
            var settings = new GameRuntimeSettings { ProcessorType = (byte)(mode % 6 + 1), GameSpeed = (byte)(mode % 5 + 1) };
            var config = new DosConfiguration { MusicVolume = 100, EffectsVolume = 200 };
            var host = new TraceHost(trace, mode) { Running = true, Input = new(false, false, 0, 10, 10, 0, 0, 0) };
            var setup = new InGameSetup(settings, config, text, host) { ConstantPlay = (mode & 4) != 0, NetworkGame = (mode & 8) != 0 };
            trace.AppendLine($"run:{mode}");
            var result = setup.Run(); trace.AppendLine($"result:{Bit(result)},{host.Loops},{Bit(setup.HaltGame)},{Bit(setup.PlayerEndLevel)}," +
                $"{Bit(host.MusicDisabled)},{Bit(host.SamplesDisabled)},{settings.ProcessorType},{settings.GameSpeed}");
        }
        TestMouseBoundaries(text);
        TestHost(text);
        if (executable is null) return;
        var start = new ProcessStartInfo(executable) { RedirectStandardOutput = true, RedirectStandardError = true };
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Cannot start in-game setup reference.");
        var output = process.StandardOutput.ReadToEndAsync(); var errors = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(30000)) { process.Kill(entireProcessTree: true); throw new TimeoutException("In-game setup reference timed out."); }
        var expected = output.GetAwaiter().GetResult().Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var actual = trace.ToString().Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        if (process.ExitCode != 0 || !expected.SequenceEqual(actual))
        {
            var index = Enumerable.Range(0, Math.Min(expected.Length, actual.Length)).FirstOrDefault(i => expected[i] != actual[i], -1);
            throw new InvalidOperationException($"In-game setup differs at {index}: {expected.ElementAtOrDefault(index)} / " +
                $"{actual.ElementAtOrDefault(index)}; {errors.GetAwaiter().GetResult()}");
        }
    }

    private static void TestMouseBoundaries(HelpTextData text)
    {
        var host = new TraceHost(new StringBuilder(), 0);
        var setup = new InGameSetup(new(), new(), text, host);
        for (var row = 0; row < 6; row++)
            foreach (var x in new[] { 9, 10, 119, 120, 202, 209, 210 })
                foreach (var offset in new[] { -1, 0, 12, 13 })
                {
                    var y = 20 + row * 20 + offset;
                    host.Input = new(false, true, 0, x, y, 1, x, y);
                    setup.Selected = 0;
                    var action = new InGameSetupAction(); setup.ReadMouse(true, action);
                    var inside = x is >= 10 and < 210 && offset is >= 0 and < 13;
                    if (inside && setup.Selected != row || action.Activate != (inside && x < 120) ||
                        action.VolumeChanged != (inside && x >= 120 && row < 2) ||
                        action.Increase != (inside && x >= 120 && row is 2 or 3))
                        throw new InvalidOperationException("Setup click bounds differ.");
                }
        host.Input = new(false, true, 0, 202, 20, 1, 202, 20);
        var slider = new InGameSetupAction(); setup.ReadMouse(false, slider);
        if (slider.VolumeValue != 255) throw new InvalidOperationException("Setup maximum slider does not reach 255.");
        host.Input = new(false, true, 0, 120, 20, 1, 120, 20);
        slider = new(); setup.ReadMouse(false, slider);
        if (slider.VolumeValue != 0) throw new InvalidOperationException("Setup minimum slider does not reach zero.");
    }

    private static void TestHost(HelpTextData text)
    {
        var shapes = new MainShapeTables(); using var data = new MemoryStream(); BinaryFile.WriteUInt16(data, 151);
        for (var i = 0; i < 151; i++)
        {
            BinaryFile.WriteBoolean(data, true); BinaryFile.WriteUInt16(data, 1); BinaryFile.WriteUInt16(data, 1);
            BinaryFile.WriteUInt16(data, 1); data.WriteByte(0xAE);
        }
        foreach (var table in shapes.Tables) { data.Position = 0; table.Load(data); }
        var primary = new IndexedSurface(320, 200, 327, TextWriter.Null);
        var saved = new IndexedSurface(320, 200, 327, TextWriter.Null);
        var expected = new IndexedSurface(320, 200, 327, TextWriter.Null);
        primary.Pixels.Span.Fill(0x75); expected.Pixels.Span.Fill(0x75);
        var font = new FontRenderer(shapes);
        var platform = new TextGlowTests.Platform(); var input = new InputState(platform, new VideoLayout());
        var cursorSheet = new SpriteSheet(); var cursor = new MouseCursor(input, primary, cursorSheet) { HasMouse = false, Cursor = 4 };
        var settings = new GameRuntimeSettings { ProcessorType = 2, GameSpeed = 4 };
        var config = new DosConfiguration { MusicVolume = 100, EffectsVolume = 200 };
        var timer = new DelayTimer(input); var mixer = new AudioMixer(44100, new Silence());
        var loads = 0; var presents = 0; var pushes = 0; var keeps = 0; var halts = 0; var samples = new List<byte>();
        var host = new InGameSetupHost(primary, saved, font, input, cursor, cursorSheet, mixer, settings, config,
            timer, () => loads++, () => presents++, () => { pushes++; platform.Events.Enqueue(new(InputEventKind.KeyDown, 41)); },
            () => keeps++, samples.Add, () => halts++);
        var setup = new InGameSetup(settings, config, text, host);
        host.Shade(3, 13, 217, 137); expected.ShadeRectangle(3, 13, 217, 137);
        host.Capture(); primary.Pixels.Span.Clear(); host.Restore(); host.ResetCursor();
        host.Label(10, 20, "Menu"u8.ToArray(), -2); font.DrawShadow(expected, 10, 20, "Menu"u8, Font.Normal, FontAlignment.Left, 15, -2, false, 2);
        host.Gauge(120, 20, 16, 8); expected.ShadowedGauge(120, 20, 1, 16, 8, 3, 13);
        host.Help("Help"u8.ToArray()); font.OutTextAdjust(expected, 10, 147, "Help"u8, 14, 6, Font.Small, true);
        if (!primary.Pixels.Span.SequenceEqual(expected.Pixels.Span) || cursor.Cursor != 0)
            throw new InvalidOperationException("Setup concrete text/gauge/surface rendering differs.");
        setup.Selected = 2; setup.Adjust(1);
        if (settings.ProcessorType != 3 || config.ProcessorType != 3 || config.GameSpeed != 4 || timer.GetDelayTicks() == 0)
            throw new InvalidOperationException("Setup concrete processor/timer binding differs.");
        host.MusicDisabled = host.SamplesDisabled = true;
        if (!mixer.MusicDisabled || !mixer.SamplesDisabled) throw new InvalidOperationException("Setup concrete audio flags differ.");
        host.EnsureCursor(); host.Volume(100, 200); host.Halt(); setup.Wait();
        if (loads != 1 || presents != 1 || pushes != 1 || keeps != 1 || halts != 1 || !samples.SequenceEqual(new byte[] { 28 }))
            throw new InvalidOperationException("Setup concrete input/audio/host ordering differs.");
    }

    private static void Fill(byte[][] values, string prefix)
    {
        for (var i = 0; i < values.Length; i++) values[i] = Encoding.ASCII.GetBytes($"{prefix}{i}");
    }
    private static int Bit(bool value) => value ? 1 : 0;
    private sealed class Silence : IMusicSynthesizer
    {
        public void Update() { }
        public void Render(Span<short> samples) => samples.Clear();
    }
    private sealed class TraceHost(StringBuilder trace, int mode) : IInGameSetupHost
    {
        private int _waitFrame;
        public bool Running { get; init; }
        public int Loops { get; private set; }
        public SetupInput Input { get; set; }
        public void ResetWait() { Loops = _waitFrame = 0; }
        public void EnsureCursor() { if ((mode & 1) == 0) trace.AppendLine("cursor"); }
        public void Shade(int left, int top, int right, int bottom) => trace.AppendLine($"shade:{left},{top},{right},{bottom}");
        public void Capture() { }
        public void Restore() { }
        public void ResetCursor() { }
        public void Label(int x, int y, ReadOnlyMemory<byte> text, int brightness)
            => trace.AppendLine($"label:{x},{y},{Encoding.ASCII.GetString(text.Span)},{brightness}");
        public void Gauge(int x, int y, int color, int amount) => trace.AppendLine($"gauge:{x},{y},{color},{amount}");
        public void Help(ReadOnlyMemory<byte> text) => trace.AppendLine($"help:{Encoding.ASCII.GetString(text.Span)}");
        public void ServiceEvents(bool clear)
        {
            trace.AppendLine($"events:{Bit(clear)}");
            if (clear) { Input = Input with { NewKey = false, NewMouse = false }; _waitFrame = 0; Loops++; }
            else if (++_waitFrame == mode % 4 + 1)
                Input = Input with { NewKey = true, Scancode = Running ? RunScan() : 40 };
        }
        private int RunScan()
        {
            int[][] scans = [[81, 81, 81, 41], [81, 81, 81, 81, 40], [81, 81, 81, 81, 81, 40],
                [40, 81, 40, 81, 26, 79, 81, 80, 41]];
            return scans[mode % 4][Loops - 1];
        }
        public void MouseStart() => trace.AppendLine("mouse");
        public void Present() => trace.AppendLine("present");
        public void MouseReplace() => trace.AppendLine("replace");
        public void Delay(uint milliseconds) => trace.AppendLine($"delay:{milliseconds}");
        public void PushJoysticks() => trace.AppendLine("joystick");
        public void KeepAlive() => trace.AppendLine("keep");
        public bool MusicDisabled { get; set; }
        public bool SamplesDisabled { get; set; }
        public void Sample(byte number) => trace.AppendLine($"sample:{number}");
        public void Volume(ushort music, ushort effects) => trace.AppendLine($"volume:{music},{effects}");
        public void InitializeProcessor() => trace.AppendLine("processor");
        public void SetSpeed() => trace.AppendLine("speed");
        public void Halt() => trace.AppendLine("halt");
    }
}
