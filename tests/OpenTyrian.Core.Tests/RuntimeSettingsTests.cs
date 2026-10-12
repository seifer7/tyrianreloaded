using System.Diagnostics;
using System.Text;
using OpenTyrian.Core;

namespace OpenTyrian.Core.Tests;

internal static class RuntimeSettingsTests
{
    public static void Run(string? executable = null)
    {
        var output = new StringBuilder();
        for (byte cpu = 0; cpu < 9; cpu++)
            for (byte speed = 0; speed < 8; speed++)
                for (byte prior = 0; prior < 6; prior++)
                {
                    var settings = new GameRuntimeSettings { ProcessorType = cpu, GameSpeed = speed, FastPlay = prior };
                    settings.InitializeProcessor();
                    output.AppendLine($"settings:{cpu},{speed},{prior},{Bit(settings.Wild)},{Bit(settings.SuperWild)},{Bit(settings.SmoothScroll)},{Bit(settings.ExplosionTransparent)},{Bit(settings.FiltrationAvailable)},{Bit(settings.Background2)},{Bit(settings.DisplayScore)},{settings.FastPlay}");
                    var timer = new DelayTimer(new Platform());
                    settings.SetGameSpeed(timer);
                    output.AppendLine($"timing:{Bit(settings.SmoothScroll)},{Bit(settings.PentiumMode)},{settings.FrameCountMax},{timer.GetDelayTicks() + 12345}");
                }
        if (executable != null)
        {
            using var process = Process.Start(new ProcessStartInfo(Path.GetFullPath(executable))
            { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true })
                ?? throw new InvalidOperationException("Cannot start native runtime-settings oracle.");
            var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(30000))
            {
                process.Kill(entireProcessTree: true);
                throw new TimeoutException("Native runtime-settings oracle timed out.");
            }
            if (process.ExitCode != 0 || stdout.GetAwaiter().GetResult().Replace("\r\n", "\n", StringComparison.Ordinal) !=
                output.ToString().Replace("\r\n", "\n", StringComparison.Ordinal))
                throw new InvalidOperationException($"Native runtime-settings/PIT traces differ: {stderr.GetAwaiter().GetResult()}");
        }
        var invalid = new GameRuntimeSettings { FastPlay = 6 };
        try { invalid.SetGameSpeed(new(new Platform())); throw new InvalidOperationException("Undefined fast-play mode was accepted."); }
        catch (InvalidDataException) { }
    }
    private static int Bit(bool value) => value ? 1 : 0;
    private sealed class Platform : IDelayPlatform
    {
        public uint Ticks => 12345;
        public bool NewKey { get; set; }
        public bool MouseDown => false;
        public bool JoystickDown => false;
        public void Delay(uint milliseconds) => throw new InvalidOperationException();
        public void ServiceEvents(bool clearNew) => throw new InvalidOperationException();
        public void PollJoysticks() => throw new InvalidOperationException();
    }
}
