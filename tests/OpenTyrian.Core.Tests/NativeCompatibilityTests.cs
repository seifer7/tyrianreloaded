using System.Diagnostics;
using System.Globalization;
using System.Text;
using OpenTyrian.Core;

namespace OpenTyrian.Core.Tests;

internal static class NativeCompatibilityTests
{
    public static void Run(string executable)
    {
        foreach (var number in new[] { "0", "00", "1", "-1", "+1", "010", "08", "0x10", "-0x10", "0Xf", " 42", "\t-42", "42 ", "abc", "", "-", "0x", "2147483647", "-2147483648" })
        {
            var native = Execute(executable, ["config-number", number]);
            var config = new Configuration();
            var section = config.AddSection("test");
            section.SetOption("value", number);
            var signedOk = section.TryGetInt32("value", out var signedValue);
            var unsignedOk = section.TryGetUInt32("value", out var unsignedValue);
            Equal(native.Output, $"{(signedOk ? 1 : 0)}|{signedValue}|{(unsignedOk ? 1 : 0)}|{unsignedValue}\n");
        }

        foreach (var fixture in ConfigurationTests.Fixtures)
        {
            var native = Execute(executable, ["config"], fixture);
            using var input = new StringReader(fixture);
            var config = Configuration.Parse(input);
            using var output = new StringWriter();
            config.Write(output);
            Equal(native.Output, output.ToString());
            Equal("", native.Errors);
        }

        foreach (uint seed in new uint[] { 0, 1, 5489, uint.MaxValue })
        {
            var native = Execute(executable, ["rng", seed.ToString(CultureInfo.InvariantCulture)]);
            using var expected = new StringReader(native.Output);
            MersenneTwister.Seed(seed);
            for (var i = 0; i < 1250; i++)
            {
                var integer = MersenneTwister.NextUInt32();
                var inclusive = BitConverter.SingleToUInt32Bits(MersenneTwister.NextFloatInclusive());
                var exclusive = BitConverter.SingleToUInt32Bits(MersenneTwister.NextFloatExclusive());
                Equal($"{integer}|{inclusive}|{exclusive}", expected.ReadLine());
            }

            Equal(null, expected.ReadLine());
            Equal("", native.Errors);
        }

        string[][] cases =
        [
            [],
            ["first", "-hs", "second", "-tassets", "-"],
            ["operand", "-t", "short", "--dat=long", "--data", "-directory", "--data="],
            ["first", "--", "-s", "second"],
            ["--help=ignored"],
            ["-z"],
            ["-t"],
            ["--data"],
            ["--unknown"],
            ["--net-player-n"],
            ["--net-player-n", "name"],
            ["--net-player-name=name"],
            ["--=value"],
            ["-htvalue"],
            ["operand", "-hs", "--", "last"]
        ];
        OptionDefinition[] definitions =
        [
            new('h', 'h', "help", false),
            new('s', 's', "no-sound", false),
            new('t', 't', "data", true),
            new(256, '\0', "net-player-name", true),
            new(257, '\0', "net-player-number", true)
        ];
        foreach (var arguments in cases)
        {
            var native = Execute(executable, ["parser", .. arguments]);
            string[] argv = ["game", .. arguments];
            using var errors = new StringWriter();
            using var output = new StringWriter();
            var parser = new ArgumentParser(argv, definitions, errors);
            while (true)
            {
                var option = parser.Next();
                output.Write($"{option.Value}|{option.Argument ?? "<null>"}|{option.ArgumentIndex}\n");
                if (option.Value <= 0)
                {
                    break;
                }
            }

            foreach (var argument in argv)
            {
                output.Write($"argv:{argument}\n");
            }

            Equal(output.ToString(), native.Output);
            Equal(errors.ToString(), native.Errors);
        }
    }

    private static (string Output, string Errors) Execute(string executable, string[] arguments, string? input = null)
    {
        var start = new ProcessStartInfo(executable)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            StandardInputEncoding = Encoding.Latin1,
            UseShellExecute = false
        };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start) ?? throw new InvalidOperationException("Native reference did not start.");
        var output = process.StandardOutput.ReadToEndAsync();
        var errors = process.StandardError.ReadToEndAsync();
        if (input is not null)
        {
            process.StandardInput.Write(input);
        }

        process.StandardInput.Close();
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"Native reference failed: {errors.GetAwaiter().GetResult()}");
        }

        return (Normalize(output.GetAwaiter().GetResult()), Normalize(errors.GetAwaiter().GetResult()));
    }

    private static string Normalize(string value) => value.Replace("\r\n", "\n", StringComparison.Ordinal);

    private static void Equal(string? expected, string? actual)
    {
        if (!string.Equals(expected, actual, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Native comparison mismatch: expected '{expected}', got '{actual}'.");
        }
    }
}
