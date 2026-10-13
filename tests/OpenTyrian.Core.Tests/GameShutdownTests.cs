using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using OpenTyrian.Core;

namespace OpenTyrian.Core.Tests;

internal static class GameShutdownTests
{
    public static void Run(string? executable = null)
    {
        var trace = new StringBuilder();
        for (var mode = 0; mode < 512; mode++)
        {
            trace.AppendLine($"case:{mode}");
            try { new GameShutdown(new TraceHost(trace)).Halt(unchecked((byte)mode), (mode & 256) != 0); }
            catch (GameTerminationException exception)
            {
                if (exception.ExitCode != ((mode & 255) == 5 ? 0 : mode & 255))
                    throw new InvalidOperationException("Game halt changed the native exit code.");
            }
        }
        TestFailures();
        if (executable is null) return;
        using var process = Process.Start(new ProcessStartInfo(executable)
        { RedirectStandardOutput = true, RedirectStandardError = true })
            ?? throw new InvalidOperationException("Cannot start game shutdown reference.");
        var output = process.StandardOutput.ReadToEndAsync(); var errors = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(30000)) { process.Kill(entireProcessTree: true); throw new TimeoutException("Game shutdown reference timed out."); }
        var expected = output.GetAwaiter().GetResult().Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var actual = trace.ToString().Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        if (process.ExitCode != 0 || !expected.SequenceEqual(actual))
        {
            var index = Enumerable.Range(0, Math.Min(expected.Length, actual.Length)).FirstOrDefault(i => expected[i] != actual[i], -1);
            throw new InvalidOperationException($"Game shutdown differs at {index}: {expected.ElementAtOrDefault(index)} / " +
                $"{actual.ElementAtOrDefault(index)}; {errors.GetAwaiter().GetResult()}");
        }
    }

    private static void TestFailures()
    {
        foreach (var operation in new[] { "audio", "video", "joysticks", "mainshapes", "sheet:0", "sheet:1", "sheet:2", "sounds", "save", "sdlquit" })
        {
            var trace = new StringBuilder(); var host = new TraceHost(trace, operation);
            try { new GameShutdown(host).Halt(5); throw new InvalidOperationException("Shutdown failure was swallowed."); }
            catch (IOException exception)
            {
                if (exception.Message != operation || !trace.ToString().Replace("\r\n", "\n", StringComparison.Ordinal).Contains("sdlquit\n", StringComparison.Ordinal) ||
                    trace.ToString().Contains("exit:", StringComparison.Ordinal))
                    throw new InvalidOperationException("Shutdown failure changed or bypassed SDL cleanup.");
            }
            if (operation is "audio" or "video" or "joysticks" &&
                !trace.ToString().Replace("\r\n", "\n", StringComparison.Ordinal).Contains("sample:37\n", StringComparison.Ordinal))
                throw new InvalidOperationException("Platform shutdown failure bypassed owned-resource release.");
        }
    }

    internal static void RunSdl()
    {
        foreach (var code in new[] { 5, 9, 255, -1 })
        {
            var start = new ProcessStartInfo("dotnet") { RedirectStandardOutput = true, RedirectStandardError = true };
            start.ArgumentList.Add(typeof(GameShutdownTests).Assembly.Location);
            start.ArgumentList.Add("--sdl-shutdown-child"); start.ArgumentList.Add(code.ToString(System.Globalization.CultureInfo.InvariantCulture));
            using var process = Process.Start(start) ?? throw new InvalidOperationException("Cannot start isolated SDL shutdown check.");
            var output = process.StandardOutput.ReadToEndAsync(); var errors = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(30000)) { process.Kill(entireProcessTree: true); throw new TimeoutException("SDL shutdown check timed out."); }
            if (process.ExitCode != 0)
                throw new InvalidOperationException($"SDL shutdown failed for {code}: {output.GetAwaiter().GetResult()} {errors.GetAwaiter().GetResult()}");
        }
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate uint Initialized(uint flags);

    internal static void RunSdlChild(int requested)
    {
        var directory = Path.Combine(Path.GetTempPath(), $"opentyrian-shutdown-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var failedSave = requested < 0; var code = failedSave ? (byte)5 : checked((byte)requested);
        var blocked = Path.Combine(directory, "blocked");
        try
        {
            using var video = new SdlVideoDevice(TextWriter.Null, -1);
            using var audio = new SdlAudioDevice(rate => new(rate, new SilentMusic()));
            var music = new LdsPlayer(new SilentOpl());
            music.Rewind();
            var settings = new GameSettingsStore(new(new SdlKeyboardNames()), TextWriter.Null);
            MersenneTwister.Seed(789); settings.Load(directory, (_, _) => { });
            settings.Text.ShopContent = 2; settings.Runtime.ProcessorType = 4; settings.Runtime.GameSpeed = 3;
            using var joysticks = new SdlJoysticks(settings.Text.Text, TextWriter.Null, TextWriter.Null,
                _ => throw new InvalidOperationException("Unexpected shutdown joystick input."), () => false, () => { });
            using var input = new SdlInputPlatform(video, joysticks, () => throw new InvalidOperationException("Unexpected shutdown input."));
            var shapes = HighScoreScreenTests.CreateShapes(151); var sheets = Enumerable.Range(0, 3).Select(_ => new SpriteSheet()).ToArray();
            var bytes = HighScoreScreenTests.SpriteBytes();
            foreach (var sheet in shapes.Sheets.Concat(sheets)) { using var data = new MemoryStream(bytes); sheet.Load(data, bytes.Length); }
            var sounds = new SoundBank();
            using var effects = new MemoryStream(SoundBankTests.BuildBank(29, false, 1));
            using var voices = new MemoryStream(SoundBankTests.BuildBank(9, true, 1));
            sounds.Load(effects, voices, new Converter());
            if (sounds.Samples.All(sample => sample.Length == 0)) throw new InvalidOperationException("Shutdown sounds were never populated.");
            using var output = new StringWriter();
            if (failedSave) File.WriteAllText(blocked, "not a directory");
            var host = new SdlGameShutdownHost(audio, video, joysticks, input, shapes, sheets[0], sheets[1], sheets[2],
                sounds, settings, failedSave ? blocked : directory, output, music);
            var shutdown = new GameShutdown(host);
            try
            {
                if (requested == 5)
                {
                    var state = new InputState(input, video.Layout); state.FlushEvents();
                    var timer = new DelayTimer(state); var font = new FontRenderer(shapes);
                    var warning = new WarningBars(video.Primary, timer, video.Present, _ => { });
                    var characterHost = new CharacterGlowHost(video.Primary, font, shapes.Tables[2], state, timer,
                        warning, () => { }, video.Present);
                    var glow = new TextGlow(new TextGlowHost(video.Primary, font, state, timer, () => { }, video.Present));
                    var textHost = new WarningTextHost(new(glow, characterHost), characterHost, state, timer, font,
                        warning, () => true, value => shutdown.Halt(checked((byte)value), true), () => { });
                    new WarningTextDisplay(glow, new(), textHost).Run(["A"u8.ToArray()]);
                }
                else shutdown.Halt(code, true);
                throw new InvalidOperationException("Concrete shutdown returned.");
            }
            catch (GameTerminationException exception) when (!failedSave && exception.ExitCode == (code == 5 ? 0 : code)) { }
            catch (IOException) when (failedSave) { }
            if (shapes.Tables.Any(table => table.Count != 0) || shapes.Sheets.Concat(sheets).Any(sheet => sheet.Size != 0) ||
                sounds.Samples.Any(sample => sample.Length != 0) || music.Playing)
                throw new InvalidOperationException("Concrete shutdown retained owned sample/shape storage.");
            Throws<ObjectDisposedException>(() => video.Present());
            Throws<ObjectDisposedException>(() => audio.Start());
            Throws<ObjectDisposedException>(() => joysticks.PollJoysticks());
            Throws<ObjectDisposedException>(() => input.PollEvent(out _));
            var library = NativeLibrary.Load(Environment.GetEnvironmentVariable("SDL2_LIBRARY_PATH") ??
                (OperatingSystem.IsWindows() ? "SDL2.dll" : OperatingSystem.IsMacOS() ? "libSDL2.dylib" : "libSDL2-2.0.so.0"));
            try
            {
                var initialized = Marshal.GetDelegateForFunctionPointer<Initialized>(NativeLibrary.GetExport(library, "SDL_WasInit"));
                if (initialized(0) != 0) throw new InvalidOperationException("Concrete shutdown left SDL subsystems initialized.");
            }
            finally { NativeLibrary.Free(library); }
            var saved = code != 9 && !failedSave;
            foreach (var filename in new[] { "tyrian.cfg", "tyrian.sav", "opentyrian.cfg" })
                if (File.Exists(Path.Combine(directory, filename)) != saved) throw new InvalidOperationException("Shutdown save/skip behavior differs.");
            if (saved)
            {
                var restored = new GameSettingsStore(new(new SdlKeyboardNames()), TextWriter.Null);
                restored.Load(directory, (_, _) => { });
                if (restored.Text.ShopContent != 2 || restored.Runtime.ProcessorType != 4 || restored.Runtime.GameSpeed != 3 ||
                    !output.ToString().Contains("Sleep well, Trent", StringComparison.Ordinal))
                    throw new InvalidOperationException("Concrete shutdown configuration or farewell did not persist.");
            }
        }
        finally
        {
            foreach (var filename in new[] { "tyrian.cfg", "tyrian.sav", "opentyrian.cfg", "blocked" })
            {
                var path = Path.Combine(directory, filename); if (File.Exists(path)) File.Delete(path);
            }
            Directory.Delete(directory);
        }
    }

    private sealed class Converter : IAudioSampleConverter
    {
        public short[] Convert(ReadOnlySpan<sbyte> samples) => samples.ToArray().Select(sample => (short)(sample * 256)).ToArray();
    }
    private sealed class SilentMusic : IMusicSynthesizer
    {
        public void Update() { }
        public void Render(Span<short> samples) => samples.Clear();
    }
    private sealed class SilentOpl : IOplSynthesizer
    {
        public void Reset() { }
        public void Write(byte register, byte value) { }
        public void Render(Span<short> samples) => samples.Clear();
    }
    private static void Throws<T>(Action action) where T : Exception
    {
        try { action(); } catch (T) { return; }
        throw new InvalidOperationException($"Expected {typeof(T).Name}.");
    }
    private sealed class TraceHost(StringBuilder trace, string? failure = null) : IGameShutdownHost
    {
        public TextWriter Output { get; } = new StringWriter(trace);
        private void Operation(string name)
        {
            trace.AppendLine(name);
            if (name == failure) throw new IOException(name);
        }
        public void DeinitializeAudio() => Operation("audio");
        public void DeinitializeVideo() => Operation("video");
        public void DeinitializeJoysticks() => Operation("joysticks");
        public void ClearMainShapes() => Operation("mainshapes");
        public void ClearShopShapes() => Operation("sheet:0");
        public void ClearExplosionShapes() => Operation("sheet:1");
        public void ClearDestructShapes() => Operation("sheet:2");
        public void ClearSounds()
        {
            if (failure == "sounds") throw new IOException("sounds");
            for (var i = 0; i < 38; i++) trace.AppendLine($"sample:{i}");
        }
        public void SaveConfiguration() => Operation("save");
        public void QuitSdl() => Operation("sdlquit");
        public void Exit(int code) { trace.AppendLine($"exit:{code}"); throw new GameTerminationException(code); }
    }
}
