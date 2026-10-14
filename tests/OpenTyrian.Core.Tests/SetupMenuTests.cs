using System.Diagnostics;
using System.Text;
using OpenTyrian.Core;

namespace OpenTyrian.Core.Tests;

internal static class SetupMenuTests
{
    private static readonly int[] Counts = [0, 5, 4, 3, 2];
    private static readonly SetupItemId[] Pickers = [SetupItemId.Display, SetupItemId.Scaler, SetupItemId.ScalingMode, SetupItemId.ShopContents];

    public static void Run(string? executable = null)
    {
        var trace = new StringBuilder();
        for (var mode = 0; mode < 16; mode++)
        {
            var (menu, _, _, _, _) = Create(mode, trace);
            trace.AppendLine($"catalog:{mode}");
            foreach (var item in Pickers)
            {
                var count = menu.PickerCount(item); trace.AppendLine($"count:{(int)item},{count}");
                for (var i = 0; i < count; i++) trace.AppendLine($"item:{(int)item},{i},{menu.PickerItem(item, i)}");
            }
        }
        for (var mode = 0; mode < 256; mode++)
        {
            var (menu, _, _, _, _) = Create(mode, trace);
            var id = mode % 4 + 1; var selected = mode / 4 % Counts[id]; var state = State(mode, id, selected);
            var item = Item(id, selected);
            if ((mode & 32) != 0 && Pickers.Contains(item))
            { state.Picker = item; state.PickerSelected = mode / 64 % menu.PickerCount(item); }
            trace.AppendLine($"draw:{mode}"); var y = menu.Draw(state); trace.AppendLine($"drawstate:{y},{Counts[id]}");
        }
        int[] scans = [82, 81, 80, 79, 44, 40, 88, 41, 0];
        int[] xs = [44, 45, 179, 180, 274, 275, 400]; int[] dy = [-1, 0, 12, 13];
        for (var mode = 0; mode < 8192; mode++)
        {
            var (menu, input, platform, _, _) = Create(mode, trace);
            var id = mode % 4 + 1; var state = State(mode, id, mode / 4 % Counts[id]);
            var x = xs[mode / 216 % 7]; var y = 37 + 21 * (mode / 1512 % Counts[id]) + dy[mode / 16 % 4];
            SetInput(input, platform, (mode & 1) != 0, (mode & 2) != 0, scans[mode / 8 % 9], mode / 72 % 3 + 1,
                x, y, (mode & 16) != 0 ? x : 400, (mode & 32) != 0 ? y : 0);
            trace.AppendLine($"menuinput:{mode}"); DumpInput(trace, menu.CollectMenuInput(state, (mode & 4) != 0));
        }
        for (var mode = 0; mode < 8192; mode++)
        {
            var (menu, input, platform, _, _) = Create(mode, trace);
            var item = Pickers[mode % 4]; var yPicker = mode / 4 % 3 * 20 - 5;
            var x = xs[mode / 216 % 7]; var y = yPicker + 15 * (mode / 1512 % menu.PickerCount(item)) + dy[mode / 16 % 4];
            SetInput(input, platform, (mode & 1) != 0, (mode & 2) != 0, scans[mode / 8 % 9], mode / 72 % 3 + 1,
                x, y, (mode & 16) != 0 ? x : 400, (mode & 32) != 0 ? y : 0);
            trace.AppendLine($"pickerinput:{mode}"); DumpInput(trace, menu.CollectPickerInput(item, yPicker, (mode & 4) != 0));
        }
        for (var mode = 0; mode < 1024; mode++)
        {
            var (menu, _, _, host, config) = Create(mode, trace);
            var item = Pickers[mode % 4]; var count = menu.PickerCount(item);
            var state = State(mode, item == SetupItemId.ShopContents ? 4 : 2, 0);
            state.Picker = item; state.PickerSelected = mode / 4 % count; host.ScalerValue = 2; host.FailScaler = true;
            trace.AppendLine($"pickeraction:{mode}");
            menu.ApplyPicker(state, item, new((SetupAction)(mode / 16 % 5), (mode & 128) != 0 ? mode / 32 % count : -1));
            Dump(trace, state, host, config);
        }
        for (var mode = 0; mode < 156; mode++)
        {
            var (menu, _, _, host, config) = Create(mode, trace); var state = State(mode, mode / 13 % 4 + 1, 0);
            trace.AppendLine($"activate:{mode}"); menu.Activate(state, (SetupItemId)(mode % 13)); Dump(trace, state, host, config);
        }
        int[] values = [-300, -256, -255, -9, -1, 0, 1, 8, 255, 256, 300];
        for (var mode = 0; mode < 2816; mode++)
        {
            var (menu, _, _, host, config) = Create(mode, trace); var id = mode % 4 + 1; var state = State(mode, id, mode / 4 % Counts[id]);
            trace.AppendLine($"menuaction:{mode}");
            menu.ApplyMenu(state, new((SetupAction)(mode / 16 % 8), (mode & 128) != 0 ? mode / 32 % Counts[id] : -1, values[mode / 256 % 11]));
            Dump(trace, state, host, config);
        }
        for (var mode = 0; mode < 8; mode++)
        {
            var (menu, input, platform, host, _) = Create(mode, trace);
            if (mode == 4) SetInput(input, platform, false, false, 0, 0, -1, 10, 0, 0);
            trace.AppendLine($"wait:{mode}"); var moved = menu.WaitInput(host.FullscreenDisplay);
            trace.AppendLine($"waitstate:{Bit(moved)},{input.MouseX},{input.MouseY},{platform.Waits},{host.FullscreenDisplay}");
        }
        for (var mode = 0; mode < 64; mode++)
        {
            var (menu, _, platform, host, config) = Create(mode, trace); platform.Running = true;
            trace.AppendLine($"run:{mode}"); menu.Run();
            trace.AppendLine($"runstate:{platform.Steps},{host.FullscreenDisplay},{host.Scaler},{(int)host.ScalingMode},{host.ShopContents}," +
                $"{Bit(host.MusicDisabled)},{Bit(host.SamplesDisabled)},{config.MusicVolume},{config.EffectsVolume}");
        }
        if (executable != null) Compare(executable, trace);
        TestFailures();
    }

    private static void Compare(string executable, StringBuilder trace)
    {
        using var process = Process.Start(new ProcessStartInfo(executable) { RedirectStandardOutput = true, RedirectStandardError = true })
            ?? throw new InvalidOperationException("Cannot start setup-menu oracle.");
        var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(30000)) { process.Kill(entireProcessTree: true); throw new TimeoutException("Setup-menu oracle timed out."); }
        var expected = output.GetAwaiter().GetResult().Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var actual = trace.ToString().Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        if (process.ExitCode == 0 && expected.SequenceEqual(actual)) return;
        var index = Enumerable.Range(0, Math.Min(expected.Length, actual.Length)).FirstOrDefault(i => expected[i] != actual[i], -1);
        throw new InvalidOperationException($"Setup-menu trace differs at {index}: {expected.ElementAtOrDefault(index)} / " +
            $"{actual.ElementAtOrDefault(index)}; {error.GetAwaiter().GetResult()}");
    }

    private static (SetupMenu Menu, InputState Input, Platform Platform, Host Host, DosConfiguration Config) Create(int mode, StringBuilder trace)
    {
        var config = new DosConfiguration { MusicVolume = (ushort)(mode * 37 % 256), EffectsVolume = (ushort)(mode * 53 % 256) };
        var platform = new Platform(mode, trace); var input = new InputState(platform, new());
        SetInput(input, platform, false, false, 0, 0, 10, 10, 0, 0);
        var host = new Host(mode, trace, platform); platform.Host = host;
        return (new(input, config, host), input, platform, host, config);
    }

    private static SetupMenuState State(int mode, int id, int selected)
    {
        var state = new SetupMenuState { Menu = (SetupMenuId)id, Restart = (mode & 1) != 0 };
        for (var i = 2; i < 5; i++) state.Parents[i] = SetupMenuId.Setup;
        state.Selected[id] = selected;
        return state;
    }
    private static SetupItemId Item(int id, int i) => id switch
    {
        1 => new[] { SetupItemId.Graphics, SetupItemId.Sound, SetupItemId.Jukebox, SetupItemId.Gameplay, SetupItemId.Done }[i],
        2 => new[] { SetupItemId.Display, SetupItemId.Scaler, SetupItemId.ScalingMode, SetupItemId.Done }[i],
        3 => new[] { SetupItemId.MusicVolume, SetupItemId.SoundVolume, SetupItemId.Done }[i],
        _ => i == 0 ? SetupItemId.ShopContents : SetupItemId.Done
    };
    private static void Dump(StringBuilder trace, SetupMenuState state, Host host, DosConfiguration config)
    {
        trace.Append($"state:{(int)state.Menu},{(int)state.Picker},{state.PickerSelected},{Bit(state.Restart)}");
        for (var i = 0; i < 5; i++) trace.Append($",{(int)state.Parents[i]},{state.Selected[i]}");
        trace.AppendLine($",{host.FullscreenDisplay},{host.Scaler},{(int)host.ScalingMode},{host.ShopContents}," +
            $"{Bit(host.MusicDisabled)},{Bit(host.SamplesDisabled)},{config.MusicVolume},{config.EffectsVolume}");
    }
    private static void DumpInput(StringBuilder trace, SetupMenuInput action) => trace.AppendLine($"input:{(int)action.Action},{action.HoveredIndex},{action.Value}");
    private static int Bit(bool value) => value ? 1 : 0;

    private static void SetInput(InputState input, Platform platform, bool key, bool mouse, int scan, int button,
        int x, int y, int clickX, int clickY)
    {
        input.ServiceEvents(true);
        platform.Events.Enqueue(new(InputEventKind.MouseMotion, X: x < 0 ? x - 1 : x, Y: y < 0 ? y - 1 : y)); input.ServiceEvents(false);
        if (key) { platform.Events.Enqueue(new(InputEventKind.KeyDown, scan)); input.ServiceEvents(false); }
        if (mouse)
        {
            platform.Events.Enqueue(new(InputEventKind.MouseButtonDown, Button: (byte)button,
                X: clickX < 0 ? clickX - 1 : clickX, Y: clickY < 0 ? clickY - 1 : clickY)); input.ServiceEvents(false);
        }
    }

    private static void TestFailures()
    {
        var (menu, _, _, host, _) = Create(5, new()); var state = new SetupMenuState { Picker = SetupItemId.Scaler, PickerSelected = 1 };
        host.ScalerValue = 2; host.FailScaler = true;
        menu.ApplyPicker(state, SetupItemId.Scaler, new(SetupAction.Activate));
        if (host.Scaler != 2 || state.Picker != SetupItemId.None) throw new InvalidOperationException("Failed scaler selection did not restore the previous scaler.");
        state.Picker = SetupItemId.Scaler; state.PickerSelected = 0; host.ScalerValue = 1;
        try { menu.ApplyPicker(state, SetupItemId.Scaler, new(SetupAction.Activate)); throw new InvalidOperationException("Both scaler failures were silently ignored."); }
        catch (InvalidOperationException exception) when (exception.Message.Contains("Unable to initialize", StringComparison.Ordinal)) { }
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        try { menu.Run(cancellation.Token); throw new InvalidOperationException("Setup menu ignored cancellation."); }
        catch (OperationCanceledException) { }
        try { menu.WaitInput(host.FullscreenDisplay, cancellation.Token); throw new InvalidOperationException("Setup input wait ignored cancellation."); }
        catch (OperationCanceledException) { }
    }

    internal static void RunSdl(SdlVideoDevice video)
    {
        var previousScaler = video.ScalerName; var previousMode = video.Layout.Mode; var previousDisplay = video.FullscreenDisplay;
        var directory = Path.Combine(Path.GetTempPath(), $"opentyrian-setup-{Guid.NewGuid():N}");
        try
        {
            video.ReinitializeFullscreen(-1); video.SetScalerByName("None"); video.Layout.Mode = ScalingMode.Integer;
            int[] scans =
            [
                81, 81, 81, 81, 40,
                40, 40, 81, 40, 40, 82, 40,
                81, 40, 81, 81, 40, 81, 40, 82, 40, 41,
                81, 40, 40, 40, 79, 81, 40, 79, 41,
                81, 40,
                81, 40, 40, 81, 81, 40, 41, 81, 40, 41
            ];
            var pushes = 0; var jukeboxFrames = 0; var inJukebox = false; var jukeboxCalls = 0; var setups = 0;
            SdlInputPlatform? platform = null;
            using var ownedPlatform = platform = new SdlInputPlatform(video, new Auxiliary(() =>
            {
                if (inJukebox) { if (jukeboxFrames++ == 0) platform!.PostEvent(new(InputEventKind.KeyDown, 41)); return; }
                if (pushes >= scans.Length) throw new InvalidOperationException("SDL setup/title path requested unexpected input.");
                platform!.PostEvent(new(InputEventKind.KeyDown, scans[pushes++]));
            }), () => throw new InvalidOperationException("Unexpected SDL setup quit."));
            var input = new InputState(platform, video.Layout); input.FlushEvents();
            var dos = new DosConfiguration { MusicVolume = 128, EffectsVolume = 128 };
            var config = new GameConfiguration(new SdlKeyboardNames()) { FullscreenDisplay = -1 };
            config.Scaler.SetByName("None"); config.Layout.Mode = ScalingMode.Integer;
            var player = new Player(); using var musicData = new MemoryStream();
            BinaryFile.WriteUInt16(musicData, 41);
            for (var i = 0; i < 41; i++) BinaryFile.WriteUInt32(musicData, 166);
            musicData.Position = 0;
            var music = new MusicController(MusicArchive.Load(musicData), 44100, player);
            var timer = new DelayTimer(input); var shapes = HighScoreScreenTests.CreateShapes(151); var font = new FontRenderer(shapes);
            var sprites = new SpriteSheet(); var spriteBytes = HighScoreScreenTests.SpriteBytes();
            var cursor = new MouseCursor(input, video.Primary, sprites) { HasMouse = false };
            var background = new IndexedSurface(320, 200, video.Primary.Pitch, TextWriter.Null);
            var colors = Enumerable.Range(0, 256).Select(i => new PaletteColor((byte)i, (byte)i, (byte)i)).ToArray();
            var samples = new List<byte>(); var pictures = 0; var saves = 0;
            void Cursor() { if (sprites.Size != 0) return; using var stream = new MemoryStream(spriteBytes); sprites.Load(stream, spriteBytes.Length); }
            void Background() { background.Pixels.Span.Fill(0x75); pictures++; }
            var jukebox = new Jukebox(input, timer, new JukeboxAudio(music, () => (true, false), samples.Add),
                new JukeboxPresentation(video.Primary, video.Palette, font, new(), input, video.Present),
                platform.PushJoysticksAsKeyboard);
            var host = new SetupMenuHost(video, background, font, sprites, cursor, colors, timer, music.Mixer, config,
                Cursor, Background, platform.PushJoysticksAsKeyboard, samples.Add, music.Restart,
                () => { config.SaveFile(directory); saves++; },
                () => { jukeboxCalls++; inJukebox = true; try { jukebox.Run((byte)dos.MusicVolume, (byte)dos.EffectsVolume); } finally { inJukebox = false; } },
                () => throw new InvalidOperationException("Unlisted Destruct item activated."));
            TestRendering(video, host, background, font);
            var screen = new SetupMenu(input, dos, host);
            var text = new HelpTextData(); for (var i = 0; i < text.MenuText.Length; i++) text.MenuText[i] = Encoding.ASCII.GetBytes($"Menu{i}");
            using var cancellation = new CancellationTokenSource(15000);
            bool Unexpected() => throw new InvalidOperationException("Unexpected SDL title child action.");
            void UnexpectedVoid() => throw new InvalidOperationException("Unexpected SDL title child action.");
            var title = new TitleScreen(text, input, new TitleScreenHost(video.Primary, background, font, shapes.Tables[7],
                sprites, cursor, video.Palette, colors, timer, "Version"u8.ToArray(), Cursor,
                () => { video.Primary.Pixels.Span.Fill(0x75); pictures++; }, music.PlaySong, video.Present,
                platform.PushJoysticksAsKeyboard, samples.Add, Unexpected, Unexpected, UnexpectedVoid, UnexpectedVoid,
                () => { setups++; screen.Run(cancellation.Token); }, UnexpectedVoid, UnexpectedVoid, _ => Unexpected()));
            if (title.Run(cancellation.Token) || pushes != scans.Length || setups != 1 || jukeboxCalls != 1 || jukeboxFrames != 16 ||
                pictures != 5 || saves != 1 || player.Rewinds != 1 || dos.MusicVolume != 136 || dos.EffectsVolume != 136 ||
                music.Mixer.MusicDisabled || !music.Mixer.SamplesDisabled || video.FullscreenDisplay != -1 ||
                video.ScalerName != "Scale2x" || video.Layout.Mode != ScalingMode.Center || config.Scaler.SelectedIndex != 2 ||
                config.Layout.Mode != ScalingMode.Center || config.FullscreenDisplay != -1 || config.ShopContent != 2 ||
                video.Palette.Active.Span.ToArray().Any(color => color != default))
                throw new InvalidOperationException($"SDL setup/settings/jukebox/title path differs: pushes={pushes}, pictures={pictures}, " +
                    $"jukebox={jukeboxCalls}/{jukeboxFrames}, save={saves}, rewinds={player.Rewinds}, volume={dos.MusicVolume}/{dos.EffectsVolume}, " +
                    $"display={video.FullscreenDisplay}, scaler={video.ScalerName}, mode={video.Layout.Mode}, shop={config.ShopContent}.");
            var persisted = new GameConfiguration(new SdlKeyboardNames());
            using var diagnostics = new StringWriter();
            if (!persisted.LoadFile(directory, diagnostics) || persisted.ShopContent != 2 || persisted.FullscreenDisplay != -1 ||
                persisted.Scaler.SelectedIndex != 2 || persisted.Layout.Mode != ScalingMode.Center || diagnostics.ToString().Length != 0)
                throw new InvalidOperationException("SDL setup settings were not persisted through the configuration store.");
            input.FlushEvents();
        }
        finally
        {
            var path = Path.Combine(directory, "opentyrian.cfg");
            if (File.Exists(path)) File.Delete(path);
            if (Directory.Exists(directory)) Directory.Delete(directory);
            video.ReinitializeFullscreen(previousDisplay); video.SetScalerByName(previousScaler); video.Layout.Mode = previousMode;
        }
    }

    private static void TestRendering(SdlVideoDevice video, SetupMenuHost host, IndexedSurface background, FontRenderer font)
    {
        host.EnsureCursor(); host.LoadBackground(); host.RestoreBackground();
        var expected = new IndexedSurface(320, 200, video.Primary.Pitch, TextWriter.Null);
        background.Pixels.Span.CopyTo(expected.Pixels.Span);
        host.Label(160, 4, "Graphics", Font.Large, FontAlignment.Centered, -3);
        font.DrawShadow(expected, 160, 4, "Graphics"u8, Font.Large, FontAlignment.Centered, 15, -3, false, 2);
        host.Label(180, 37, "Window", Font.Normal, FontAlignment.Left, -1);
        font.DrawShadow(expected, 180, 37, "Window"u8, Font.Normal, FontAlignment.Left, 15, -1, false, 2);
        host.Gauge(58, 174, 16); expected.ShadowedGauge(180, 58, 1, 174, 16, 2, 10);
        host.Rectangle(178, 56, 276, 69, 242); expected.Rectangle(178, 56, 276, 69, 242);
        host.Fill(178, 77, 99, 47, 224); expected.FillRectangleSize(178, 77, 99, 47, 224);
        host.Description("Change the graphics settings.");
        font.ShadeText(expected, 45, 190, "Change the graphics settings."u8, 15, 4, TextShade.Partial);
        if (!video.Primary.Pixels.Span.SequenceEqual(expected.Pixels.Span) ||
            background.Pixels.Span[(192 * background.Pitch)..].ContainsAnyExcept((byte)0) || host.DisplayCount < 1)
            throw new InvalidOperationException("Concrete setup graphics/gauge/picker/description rendering differs.");
    }

    private sealed class Player : IMusicPlayer
    {
        public int Rewinds { get; private set; }
        public void Load(ReadOnlySpan<byte> song) { }
        public void Rewind() => Rewinds++;
        public void Fade(byte speed) { }
        public void Update() { }
        public void Render(Span<short> samples) => samples.Clear();
    }

    private sealed class Auxiliary(Action push) : IInputAuxiliaryServices
    {
        public bool JoystickDown => false;
        public bool IsNetworkGame => false;
        public void PushJoysticksAsKeyboard() => push();
        public void PollJoysticks() { }
        public void CheckNetwork() { }
    }

    private sealed class Host(int mode, StringBuilder trace, Platform platform) : ISetupMenuHost
    {
        public int FullscreenValue { get; set; } = mode % 3 - 1;
        public int ScalerValue { get; set; } = mode % 9;
        public bool FailScaler { get; set; }
        public int FullscreenDisplay => FullscreenValue;
        public int DisplayCount => mode % 4 + 1;
        public int Scaler => ScalerValue;
        public ScalingMode ScalingMode { get; set; } = (ScalingMode)(mode % 4);
        public int ShopContents { get; set; } = mode % 7 - 2;
        public bool MusicDisabled { get; set; } = (mode & 1) != 0;
        public bool SamplesDisabled { get; set; } = (mode & 2) != 0;
        public void EnsureCursor() { if ((mode & 1) == 0) trace.AppendLine("cursor:1"); }
        public void LoadBackground() { trace.AppendLine("picture:2"); trace.AppendLine("fill:0,192,320,8,0"); }
        public void RestoreBackground() { }
        public void Label(int x, int y, string text, Font font, FontAlignment alignment, sbyte brightness) =>
            trace.AppendLine($"label:{x},{y},{text},{(int)font},{(int)alignment},{brightness}");
        public void Gauge(int y, byte color, int amount) => trace.AppendLine($"gauge:180,{y},1,{color},{amount},2,10");
        public void Rectangle(int x1, int y1, int x2, int y2, byte color) => trace.AppendLine($"rectangle:{x1},{y1},{x2},{y2},{color}");
        public void Fill(int x, int y, int width, int height, byte color) => trace.AppendLine($"fill:{x},{y},{width},{height},{color}");
        public void Description(string text) => trace.AppendLine($"description:45,190,{text},15,4,0");
        public void FadeIn() => trace.AppendLine("fadein:10,0,255,0");
        public void FadeBlack() => trace.AppendLine("fade:10");
        public void PresentCursor() { trace.AppendLine("mouse"); trace.AppendLine("present"); trace.AppendLine("replace"); }
        public void PushJoysticks() => platform.PushJoysticksAsKeyboard();
        public void Sample(byte number) => trace.AppendLine($"sample:{number},{Bit(MusicDisabled)},{Bit(SamplesDisabled)}");
        public void Volume(ushort music, ushort effects) => trace.AppendLine($"volume:{music},{effects}");
        public void RestartSong() => trace.AppendLine("restart-song");
        public void ReinitializeFullscreen(int display) { trace.AppendLine($"fullscreen:{display}"); FullscreenValue = display; }
        public bool InitializeScaler(int scaler)
        {
            trace.AppendLine($"scaler:{scaler}"); if (FailScaler && mode % 5 == 0 && scaler != 2) return false; ScalerValue = scaler; return true;
        }
        public void SaveConfiguration() => trace.AppendLine("save");
        public void Jukebox() => trace.AppendLine("jukebox");
        public void Destruct() => trace.AppendLine("destruct");
    }

    private sealed class Platform(int mode, StringBuilder trace) : IInputPlatform
    {
        public Queue<InputEvent> Events { get; } = new();
        public Host? Host { get; set; }
        public bool Running { get; set; }
        public int Steps { get; private set; }
        public int Waits { get; private set; }
        private int _mouseY = 10;
        public uint Ticks { get; private set; }
        public bool JoystickDown => false;
        public bool IsNetworkGame => false;
        public bool PollEvent(out InputEvent input) => Events.TryDequeue(out input);
        public void Delay(uint milliseconds) { trace.AppendLine($"delay:{milliseconds}"); Ticks += milliseconds; }
        public void PushJoysticksAsKeyboard()
        {
            trace.AppendLine("joystick");
            if (!Running)
            {
                if (++Waits < mode % 4 + 1) return;
                if (mode == 0) Events.Enqueue(new(InputEventKind.KeyDown, 40));
                else if (mode == 1) Events.Enqueue(new(InputEventKind.MouseButtonDown, Button: 1));
                else if (mode == 2) Events.Enqueue(new(InputEventKind.MouseMotion, X: 11, Y: 10));
                else if (mode == 3) (Host ?? throw new InvalidOperationException("Missing setup host.")).FullscreenValue++;
                else if (mode >= 5) Events.Enqueue(new(InputEventKind.MouseMotion, X: 10, Y: ++_mouseY));
                return;
            }
            int[][] scripts =
            [
                [41], [82, 40], [40, 40, 81, 40, 41, 41], [81, 40, 40, 79, 81, 40, 80, 81, 40, 41],
                [81, 81, 40, 41], [81, 81, 81, 40, 40, 81, 81, 40, 81, 40, 41],
                [40, 81, 40, 81, 40, 41, 41], [40, 81, 81, 40, 81, 40, 41, 41]
            ];
            var script = scripts[mode % 8];
            Events.Enqueue(new(InputEventKind.KeyDown, Steps < script.Length ? script[Steps] : 41)); Steps++;
        }
        public void SetRelativeMouseMode(bool enabled) { }
        public void SetCursorVisible(bool visible) { }
        public void EnableSystemMouseScaling() { }
        public void ToggleFullscreen() { }
        public void WindowResized() { }
        public void PollJoysticks() { }
        public void CheckNetwork() { }
        public void Quit() => throw new InvalidOperationException("Unexpected setup-menu quit.");
    }
}
