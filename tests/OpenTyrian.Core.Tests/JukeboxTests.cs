using System.Diagnostics;
using System.Text;
using OpenTyrian.Core;

namespace OpenTyrian.Core.Tests;

internal static class JukeboxTests
{
    public static void Run(string? executable = null)
    {
        for (var scene = 0; scene < 32; scene++)
        {
            var trace = new StringBuilder();
            MersenneTwister.Seed(44123);
            var audio = new Audio(trace, scene);
            var platform = new Platform(trace, audio, scene);
            var input = new InputState(platform, new VideoLayout());
            var presentation = new Presentation(trace);
            var jukebox = new Jukebox(input, new(input), audio, presentation, platform.PushJoysticksAsKeyboard);
            var volume = (byte)(scene % 4 == 0 ? 5 : scene % 4 == 1 ? 9 : scene % 4 == 2 ? 191 : 255);
            jukebox.Run(volume, 123, Titles(41, "Song"), Titles(38, "FX"));
            trace.AppendLine($"rng:{MersenneTwister.NextUInt32()}");
            Check(platform.Frame == ((scene & 28) != 0 ? 16 : 37), "Jukebox did not complete exactly fifteen fade-out frames.");
            Check(trace.ToString().Contains($"volume:{volume},123", StringComparison.Ordinal), "Jukebox did not restore volume.");
            if (executable != null)
            {
                var expected = RunNative(executable, scene.ToString()).Split('\n');
                var actual = trace.ToString().Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
                for (var i = 0; i < Math.Max(actual.Length, expected.Length); i++)
                    Check(actual.ElementAtOrDefault(i) == expected.ElementAtOrDefault(i),
                        $"Jukebox scene {scene}, line {i}: native '{expected.ElementAtOrDefault(i)}', managed '{actual.ElementAtOrDefault(i)}'.");
            }
        }
        if (executable != null)
            Check(RunNative(executable, "titles") ==
                string.Concat(JukeboxTitles.Music.Select(title => $"music:{title}\n")) +
                string.Concat(JukeboxTitles.Sounds.Select(title => $"sound:{title}\n")), "Native audio title tables changed.");
        Check(MusicMaster.DefaultSongBuy == 2 && MusicMaster.SongLevelEnd == 9 &&
            MusicMaster.SongGameOver == 10 && MusicMaster.SongMapView == 19 &&
            MusicMaster.SongEndGame1 == 7 && MusicMaster.SongZanac == 31 &&
            MusicMaster.SongTitle == 29 && MusicMaster.Titles.Count == 41 &&
            ReferenceEquals(MusicMaster.Titles, JukeboxTitles.Music) &&
            SoundMaster.SoundEffectCount == 29 && SoundMaster.VoiceCount == 9 &&
            SoundMaster.SoundCount == 38 && SoundMaster.Titles.Count == 38 &&
            ReferenceEquals(SoundMaster.Titles, JukeboxTitles.Sounds) &&
            SoundMaster.WindowTextSamples.SequenceEqual(new byte[] { 35, 31, 32, 30, 35, 36, 38, 35, 32 }) &&
            (byte)SoundId.ClearedPlatform == 30 && (byte)SoundId.Danger == 35 &&
            (byte)SoundId.Accelerate == 38,
            "Native music master constants or shared title table differ.");
        Integration();
        AudioIntegration();
    }

    private static string[] Titles(int count, string prefix) => Enumerable.Range(0, count).Select(i => $"{prefix} {i}").ToArray();

    private static void Integration()
    {
        var trace = new StringBuilder();
        var audio = new Audio(trace, 8);
        var platform = new Platform(trace, audio, 8);
        var input = new InputState(platform, new VideoLayout());
        var screen = new IndexedSurface(320, 200, 320, TextWriter.Null);
        var palette = new PaletteSystem(color => ((uint)color.Red << 16) | ((uint)color.Green << 8) | color.Blue);
        var stars = new JukeboxStarfield();
        var shown = 0;
        var presentation = new JukeboxPresentation(screen, palette, new(new MainShapeTables()), stars, input, () => shown++);
        MersenneTwister.Seed(44123);
        new Jukebox(input, new(input), audio, presentation, platform.PushJoysticksAsKeyboard)
            .Run(191, 123, Titles(41, "Song"), Titles(38, "FX"));
        Check(shown == 16 && palette.Active.Span.ToArray().All(c => c == new PaletteColor(0, 0, 0)) &&
            screen.Pixels.Span.ContainsAnyExcept((byte)0), "Jukebox presentation did not compose stars, fade-out, and frame presentation.");
        var failing = new JukeboxPresentation(screen, palette, new(new MainShapeTables()), stars, input,
            () => throw new IOException("presentation failed"));
        Throws<IOException>(() => new Jukebox(input, new(input), audio, failing, platform.PushJoysticksAsKeyboard)
            .Run(191, 123, Titles(41, "Song"), Titles(38, "FX")));
        Check(trace.ToString().EndsWith("volume:191,123\r\n", StringComparison.Ordinal) ||
            trace.ToString().EndsWith("volume:191,123\n", StringComparison.Ordinal), "Failed jukebox left attenuated volume.");
        Throws<ArgumentException>(() => new Jukebox(input, new(input), audio, presentation, platform.PushJoysticksAsKeyboard)
            .Run(191, 123, [], []));
    }

    private static (JukeboxAudio Audio, MusicController Controller, Player Player) CreateAudio()
    {
        using var file = new MemoryStream();
        BinaryFile.WriteUInt16(file, 41);
        for (var i = 0; i < 41; i++) BinaryFile.WriteUInt32(file, 166);
        file.Position = 0;
        var player = new Player();
        var controller = new MusicController(MusicArchive.Load(file), 44100, player);
        controller.PlaySong(1);
        var bank = new SoundBank();
        var audio = new JukeboxAudio(controller, () => (player.Playing, player.Looped),
            sample => bank.PlaySample(sample, controller.Mixer.Play));
        return (audio, controller, player);
    }

    private static void AudioIntegration()
    {
        var (audio, controller, player) = CreateAudio();
        Check(audio.Playing && !audio.Looped && audio.Song == 1, "Jukebox audio state did not use the music controller.");
        audio.Play(2); Check(controller.SongPlaying == 2 && player.Loads == 2, "Jukebox play did not load through the archive.");
        audio.Stop(); Check(controller.Mixer.MusicStopped, "Jukebox stop did not reach the mixer.");
        audio.Restart(); Check(!controller.Mixer.MusicStopped && player.Rewinds == 1, "Jukebox restart did not rewind.");
        audio.SetVolume(191, 123);
        var output = new short[512];
        controller.Mixer.Mix(output);
        Check(player.Rendered == 512 && output.Any(value => value != 0), "Jukebox audio did not integrate with PCM mixing.");
        audio.Sample(38);
        Throws<InvalidDataException>(() => audio.Sample(0));
        controller.Mixer.AudioDisabled = true;
        audio.Play(3); Check(audio.Disabled && audio.Song == 2, "Jukebox disabled audio changed selection.");
        player.Looped = true; Check(audio.Looped, "Jukebox loop status was stale.");
    }

    internal static void RunSdl(SdlVideoDevice video)
    {
        SdlInputPlatform? platform = null;
        var pushes = 0;
        var auxiliary = new Auxiliary(() =>
        {
            if (pushes++ == 0) platform!.PostEvent(new(InputEventKind.KeyDown, 41));
        });
        using (platform = new SdlInputPlatform(video, auxiliary, () => throw new InvalidOperationException("Unexpected jukebox quit.")))
        {
            var input = new InputState(platform, video.Layout); input.FlushEvents();
            var (audio, controller, player) = CreateAudio();
            var presents = 0;
            var presentation = new JukeboxPresentation(video.Primary, video.Palette, new(new MainShapeTables()),
                new(), input, () => { video.Present(); presents++; controller.Mixer.Mix(new short[512]); });
            new Jukebox(input, new(input), audio, presentation, platform.PushJoysticksAsKeyboard).Run(191, 123);
            Check(presents == 16 && pushes == 16 && player.Rendered == 16 * 512 &&
                video.Palette.Active.Span.ToArray().All(c => c == new PaletteColor(0, 0, 0)),
                "Real SDL jukebox did not finish its palette, mixer, renderer and event flow.");
            input.FlushEvents();
        }
    }

    private sealed class Player : IMusicPlayer
    {
        public bool Playing, Looped;
        public int Loads, Rewinds, Rendered;
        public void Load(ReadOnlySpan<byte> song) { Loads++; Playing = true; Looped = false; }
        public void Rewind() { Rewinds++; Playing = true; Looped = false; }
        public void Fade(byte speed) { }
        public void Update() { }
        public void Render(Span<short> samples) { Rendered += samples.Length; samples.Fill(1000); }
    }
    private sealed class Auxiliary(Action push) : IInputAuxiliaryServices
    {
        public bool JoystickDown => false;
        public bool IsNetworkGame => false;
        public void PushJoysticksAsKeyboard() => push();
        public void PollJoysticks() { }
        public void CheckNetwork() { }
    }

    private sealed class Audio(StringBuilder trace, int scene) : IJukeboxAudio
    {
        public bool Disabled => scene % 7 == 0;
        public bool IsPlaying = scene % 3 != 0;
        public bool IsLooped = scene % 3 == 1;
        public uint CurrentSong = scene % 2 != 0 ? 40u : 0;
        public bool Playing => IsPlaying;
        public bool Looped => IsLooped;
        public uint Song => CurrentSong;
        public void SetVolume(byte music, byte effects) => trace.AppendLine($"volume:{music},{effects}");
        public void Play(uint song)
        {
            trace.AppendLine($"play:{song}");
            if (!Disabled) { CurrentSong = song; IsPlaying = true; IsLooped = false; }
        }
        public void Stop() => trace.AppendLine("stop");
        public void Restart() => trace.AppendLine("restart");
        public void Sample(byte number) => trace.AppendLine($"sample:{number}");
    }
    private sealed class Presentation(StringBuilder trace) : IJukeboxPresentation
    {
        public void Initialize() { trace.AppendLine("init:0,255"); trace.AppendLine("stars-init"); MersenneTwister.NextUInt32(); }
        public void Stars() { trace.AppendLine("delay:1"); trace.AppendLine("clear:0"); trace.AppendLine("stars"); MersenneTwister.NextUInt32(); }
        public void DrawLine(int y, ReadOnlyMemory<byte> text, sbyte brightness) =>
            trace.AppendLine($"text:160,{y},2,1,1,{brightness}:{Encoding.Latin1.GetString(text.Span)}");
        public void FadeStep(int steps) => trace.AppendLine($"step:{steps},0,255");
        public void FadeBlack() => trace.AppendLine("black:0,0,0,0,255");
        public void Present() => trace.AppendLine("show");
    }
    private sealed class Platform(StringBuilder trace, Audio audio, int scene) : IInputPlatform
    {
        private readonly Queue<InputEvent> _events = new();
        private bool _logged;
        private bool _clear;
        private uint _ticks;
        public int Frame;
        public bool JoystickDown => false;
        public bool IsNetworkGame => false;
        public uint Ticks => _ticks;
        public bool PollEvent(out InputEvent input)
        {
            if (!_logged)
            {
                trace.AppendLine($"events:{(_clear ? 1 : 0)}");
                if (!_clear)
                {
                    trace.AppendLine($"mouse:{((scene & 16) != 0 && Frame == 0 ? 1 : 0)}");
                    Frame++;
                }
                _logged = true;
            }
            return _events.TryDequeue(out input);
        }
        public void Delay(uint milliseconds)
        {
            _ticks += milliseconds;
            trace.AppendLine("wait");
            _logged = false;
            _clear = false;
        }
        public void PushJoysticksAsKeyboard()
        {
            trace.AppendLine("push");
            if ((scene & 3) == 2 && Frame % 4 == 1) audio.IsLooped = true;
            if ((scene & 3) == 3 && Frame % 5 == 2) audio.IsPlaying = false;
            _logged = false;
            _clear = true;
            int[] keys = [56, 54, 51, 55, 51, 44, 44, 80, 82, 79, 81, 40, 88, 22, 21, 9, 17, 17, 9, 0, 0, 41];
            var key = (scene & 4) != 0 ? Frame == 0 ? 20 : 0 : (scene & 8) != 0 ? Frame == 0 ? 41 : 0 : Frame < keys.Length ? keys[Frame] : 0;
            if (key != 0) _events.Enqueue(new(InputEventKind.KeyDown, key));
            if ((scene & 16) != 0 && Frame == 0) _events.Enqueue(new(InputEventKind.MouseButtonDown, Button: 1));
            if ((scene & 16) != 0 && Frame == 1) _events.Enqueue(new(InputEventKind.MouseButtonUp, Button: 1));
            if (Frame > 200) throw new InvalidOperationException("Jukebox script did not terminate.");
        }
        public void SetCursorVisible(bool visible) { }
        public void SetRelativeMouseMode(bool enabled) { }
        public void EnableSystemMouseScaling() { }
        public void ToggleFullscreen() { }
        public void WindowResized() { }
        public void Quit() => throw new InvalidOperationException("Unexpected jukebox quit.");
        public void PollJoysticks() { }
        public void CheckNetwork() { }
    }
    private static string RunNative(string executable, string scene)
    {
        var info = new ProcessStartInfo(Path.GetFullPath(executable)) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        info.ArgumentList.Add(scene);
        using var process = Process.Start(info) ?? throw new InvalidOperationException("Cannot start jukebox oracle.");
        var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(30000)) { process.Kill(entireProcessTree: true); throw new TimeoutException("Jukebox oracle timed out."); }
        Check(process.ExitCode == 0, $"Jukebox oracle failed: {error.GetAwaiter().GetResult()}");
        return output.GetAwaiter().GetResult().Replace("\r\n", "\n", StringComparison.Ordinal);
    }
    private static void Throws<T>(Action action) where T : Exception
    {
        try { action(); } catch (T) { return; }
        throw new InvalidOperationException($"Expected {typeof(T).Name}.");
    }
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
