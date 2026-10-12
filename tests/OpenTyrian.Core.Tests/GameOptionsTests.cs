using System.Diagnostics;
using System.Text;

namespace OpenTyrian.Core.Tests;

internal static class GameOptionsTests
{
    internal static void Run(string? native = null)
    {
        string[][] cases =
        [
            [], ["--help"], ["-h"], ["--bogus"], ["--net"], ["--net-p"], ["-z"],
            ["-sjxckrlX"], ["--no-sound", "--no-joystick", "--no-xmas", "--constant", "--death", "--record", "--loot"],
            ["--data=C:\\Tyrian Data", "--net=host:49151", "--net-player-name=Test pilot", "--net-player-number=2", "--net-port=1", "--net-delay=0"],
            ["--net=one:1234", "--net=two", "--net-player-name", "", "-t", "."],
            ["loot", "record", "nojoy", "constant", "death", "nosound", "noxmas", "yesxmas"],
            ["yesxmas", "--no-xmas", "noxmas", "--xmas"],
            ["yesxmas", "--no-xmas"], ["--", "--help", "loot"], ["-"], ["unknown", "foo"],
            ["LOOT", "-p1234", "YESXMAS", "--net-delay", "-3frames"],
            ["--net=:2"], ["--net=host:1suffix"], ["--net=host: \t+49151end"],
            ["--net=host:0"], ["--net=host:49152"], ["--net=host:"], ["--net=host:-1"], ["--net=host:bad"],
            ["--net-port=0"], ["--net-port=49152"], ["--net-port=-1"], ["--net-port=0x123"],
            ["--net-player-number=1suffix"], ["--net-player-number=  +2end"], ["--net-player-number=3"],
            ["--net-player-number=0"], ["--net-player-number=bad"],
            ["--net-delay=-1"], ["--net-delay=-2147483648"], ["--net-delay=2147483646"],
            ["--net-delay=  +7suffix"], ["--net-delay=0x4"], ["--net-delay=bad"], ["--net-delay="],
            ["--data=a", "--data=b", "--net-port=45", "--net-port=46"], ["--xma"], ["--no-j"], ["--data"],
            ["--help", "--bogus"], ["--bogus", "--help"], ["-tfoo", "-nbar:77", "-d2", "-p33"],
            ["--net-player-name", "MiXeD", "yesxmas", "--", "-j", "noxmas"]
        ];
        foreach (var args in cases)
        {
            using var output = new StringWriter(); using var errors = new StringWriter();
            var supplied = new[] { "OpenTyrian" }.Concat(args).ToArray();
            var parsed = GameOptions.Parse(supplied, output, errors);
            Check(supplied.Skip(1).SequenceEqual(args), "Game options mutated caller-owned arguments.");
            if (parsed.ExitCode == null) output.Write(State(parsed));
            if (native != null) CompareNative(native, args, parsed.ExitCode ?? 0, output.ToString(), errors.ToString());
        }
        using var discarded = new StringWriter();
        var legacy = GameOptions.Parse(["OpenTyrian", "--no-xmas", "yesxmas"], discarded, discarded).Options;
        Check(legacy.Christmas, "Legacy parameters did not override modern options.");
        var options = GameOptions.Parse(["OpenTyrian", "--data=assets", "--net=host:45", "--net-player-number=2"], discarded, discarded).Options;
        Check(options.DataDirectory == "assets" && options.OpponentHost == "host" && options.OpponentPort == 45 && options.PlayerNumber == 2,
            "Game launch state was not retained.");
        Check(GameOptions.Parse(["OpenTyrian"], discarded, discarded, christmas: true).Options.Christmas, "Initial Christmas detection state was lost.");
        foreach (var argument in new[] { "--net-delay=2147483647", "--net-port=9999999999999999999999", "--net-player-number=-999999999999999" })
            Check(GameOptions.Parse(["OpenTyrian", argument], discarded, discarded).ExitCode == 1, "Undefined native numeric overflow was accepted.");
        var files = new DataFiles(Environment.CurrentDirectory, options.DataDirectory);
        Check(!files.Exists(options.DataDirectory!, "absent-game-option-fixture.lvl"), "Options/data resolver integration did not retain the path.");
    }

    private static string State(GameOptionResult result)
    {
        var o = result.Options;
        var output = new StringBuilder();
        output.AppendLine($"flags:{Bit(o.AudioDisabled)},{Bit(o.IgnoreJoystick)},{Bit(o.Christmas)},{Bit(o.ConstantPlay)},{Bit(o.ConstantDie)},{Bit(o.RecordDemo)},{Bit(o.RichMode)},{Bit(o.NetworkGame)}");
        output.AppendLine($"net:{o.PlayerPort},{o.OpponentPort},{o.PlayerNumber},{o.NetworkDelay}");
        output.AppendLine($"data:{o.DataDirectory ?? "<null>"}");
        output.AppendLine($"host:{o.OpponentHost ?? "<null>"}");
        output.AppendLine($"name:{o.PlayerName}");
        foreach (var argument in result.Arguments) output.AppendLine($"arg:{argument}");
        return output.ToString();
    }

    private static void CompareNative(string executable, string[] arguments, int exitCode, string stdout, string stderr)
    {
        var start = new ProcessStartInfo(Path.GetFullPath(executable))
        { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start native game options reference.");
        var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(30000)) { process.Kill(entireProcessTree: true); throw new TimeoutException("Native game option reference timed out."); }
        Check(process.ExitCode == exitCode, $"Native game option exit differs for {string.Join(" ", arguments)}.");
        Check(Normalize(output.GetAwaiter().GetResult()) == Normalize(stdout), $"Native option output/state differs for {string.Join(" ", arguments)}.\nNative: {output.Result}\nManaged: {stdout}");
        Check(Normalize(error.GetAwaiter().GetResult()) == Normalize(stderr), $"Native option diagnostics differ for {string.Join(" ", arguments)}.\nNative: {error.Result}\nManaged: {stderr}");
    }
    private static string Normalize(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal);
    private static int Bit(bool value) => value ? 1 : 0;
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
