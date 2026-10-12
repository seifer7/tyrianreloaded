using System.Diagnostics;
using System.Text;

namespace OpenTyrian.Core.Tests;

internal static class SdlInputTests
{
    internal static void Run(string native)
    {
        if (Environment.GetEnvironmentVariable("SDL_VIDEODRIVER") != "dummy")
            throw new InvalidOperationException("SDL input checks require SDL_VIDEODRIVER=dummy.");
        using var video = new SdlVideoDevice(TextWriter.Null, -1);
        var auxiliary = new Auxiliary();
        var quit = false;
        using var platform = new SdlInputPlatform(video, auxiliary, () => quit = true);
        var state = new InputState(platform, video.Layout);
        state.FlushEvents();
        var output = new StringBuilder();
        for (var i = 0; i < 64; i++)
        {
            state.FlushEvents();
            InputEvent[] inputs =
            [
                new(i % 3 == 0 ? InputEventKind.FocusLost : i % 3 == 1 ? InputEventKind.FocusGained : InputEventKind.WindowResized),
                new(InputEventKind.KeyDown, i * 7, unchecked((ushort)(i * 913))), new(InputEventKind.KeyUp, i * 7),
                new(InputEventKind.MouseMotion, X: i * 17 - 400, Y: i * 3 - 100, RelativeX: i - 32, RelativeY: 64 - i),
                new(InputEventKind.MouseButtonDown, Button: (byte)(i % 5 + 1), X: -i, Y: i * 71),
                new(InputEventKind.MouseButtonUp, Button: (byte)(i % 5 + 1), X: -i, Y: i * 71),
                new(InputEventKind.TextInput, Text: Encoding.UTF8.GetBytes("bytes-\u00e9-123")),
                new(InputEventKind.TextEditing), new(InputEventKind.Unknown), new(InputEventKind.Quit)
            ];
            foreach (var input in inputs) platform.PostEvent(input);
            foreach (var expected in inputs)
            {
                Check(platform.PollEvent(out var actual), "SDL dropped a posted input event.");
                Check(actual.Kind == expected.Kind && actual.Scancode == expected.Scancode && actual.Modifiers == expected.Modifiers &&
                    actual.Button == expected.Button && actual.X == expected.X && actual.Y == expected.Y &&
                    actual.RelativeX == expected.RelativeX && actual.RelativeY == expected.RelativeY, "SDL event union ABI differs.");
                output.AppendLine(Describe(actual));
            }
        }
        CompareNative(native, output.ToString());
        state.Initialize();
        state.FlushEvents();
        platform.PostEvent(new(InputEventKind.KeyDown, 10, 0x42));
        platform.PostEvent(new(InputEventKind.TextInput, Text: "real SDL"u8.ToArray()));
        platform.PostEvent(new(InputEventKind.KeyUp, 10));
        state.ServiceEvents(true);
        Check(state.NewKey && state.KeyDown && state.LastModifiers == 0x42, "SDL queue did not reach managed input state.");
        state.ServiceEvents(false);
        Check(!state.KeyDown && state.NewText && Encoding.UTF8.GetString(state.LastText.Span[..8]) == "real SDL", "SDL text/release ordering changed.");
        state.FlushEvents();
        platform.PostEvent(new(InputEventKind.KeyDown, 40, 0x300));
        state.ServiceEvents(false);
        Check(video.FullscreenDisplay != -1, "Alt+Enter did not reach real SDL video fullscreen.");
        video.ToggleFullscreen(); video.SetWindowSize(1, 1); state.FlushEvents();
        platform.PostEvent(new(InputEventKind.WindowResized)); state.ServiceEvents(false);
        Check(video.WindowSize == (320, 200), "SDL resize did not enforce native minimum dimensions.");
        platform.PostEvent(new(InputEventKind.Quit)); state.ServiceEvents(false);
        Check(quit, "SDL quit did not reach host shutdown callback.");
        platform.PushJoysticksAsKeyboard(); platform.PollJoysticks(); platform.CheckNetwork();
        Check(auxiliary.Pushes == 1 && auxiliary.Polls == 1 && auxiliary.Checks == 1 && platform.JoystickDown && platform.IsNetworkGame,
            "Input auxiliary services were silently replaced.");
        var ticks = platform.Ticks; platform.Delay(15);
        Check(unchecked(platform.Ticks - ticks) >= 10, "SDL tick/delay integration failed.");
        Throws<ArgumentOutOfRangeException>(() => platform.PostEvent(new(InputEventKind.KeyDown, 512)));
        Throws<ArgumentOutOfRangeException>(() => platform.PostEvent(new(InputEventKind.TextInput, Text: new byte[32])));
        platform.Dispose(); platform.Dispose();
        Throws<ObjectDisposedException>(() => platform.PollEvent(out _));
        SdlJoystickTests.Run(video);
        SelectionMenuTests.RunSdl(video);
        AnimationPlayerTests.RunSdl(video);
        JukeboxTests.RunSdl(video);
        NetworkSessionTests.RunShutdownSdl(video);
        GamePauseTests.RunSdl(video);
    }

    private static string Describe(InputEvent input) => input.Kind switch
    {
        InputEventKind.FocusLost => "window:13",
        InputEventKind.FocusGained => "window:12",
        InputEventKind.WindowResized => "window:5",
        InputEventKind.KeyDown or InputEventKind.KeyUp => $"key:{(input.Kind == InputEventKind.KeyDown ? 768 : 769)},{input.Scancode},{input.Modifiers}",
        InputEventKind.MouseMotion => $"motion:{input.X},{input.Y},{input.RelativeX},{input.RelativeY}",
        InputEventKind.MouseButtonDown or InputEventKind.MouseButtonUp => $"button:{(input.Kind == InputEventKind.MouseButtonDown ? 1025 : 1026)},{input.Button},{input.X},{input.Y}",
        InputEventKind.TextInput => $"text:{Encoding.UTF8.GetString(input.Text.Span[..input.Text.Span.IndexOf((byte)0)])}",
        InputEventKind.TextEditing => "edit",
        InputEventKind.Quit => "quit",
        _ => "unknown"
    };

    private sealed class Auxiliary : IInputAuxiliaryServices
    {
        internal int Pushes, Polls, Checks;
        public bool JoystickDown => true;
        public bool IsNetworkGame => true;
        public void PushJoysticksAsKeyboard() => Pushes++;
        public void PollJoysticks() => Polls++;
        public void CheckNetwork() => Checks++;
    }
    private static void CompareNative(string executable, string expected)
    {
        using var process = Process.Start(new ProcessStartInfo(Path.GetFullPath(executable))
        { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true })
            ?? throw new InvalidOperationException("Could not start native SDL event oracle.");
        var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(30000)) { process.Kill(entireProcessTree: true); throw new TimeoutException("Native SDL input oracle timed out."); }
        Check(process.ExitCode == 0, $"SDL event oracle failed ({process.ExitCode}): {stderr.GetAwaiter().GetResult()}");
        Check(stdout.GetAwaiter().GetResult().Replace("\r\n", "\n", StringComparison.Ordinal) ==
            expected.Replace("\r\n", "\n", StringComparison.Ordinal), "Native/managed SDL event queue traces differ.");
    }
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
    private static void Throws<T>(Action action) where T : Exception
    {
        try { action(); } catch (T) { return; }
        throw new InvalidOperationException($"Expected {typeof(T).Name}.");
    }
}
