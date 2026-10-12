using OpenTyrian.Core;

namespace OpenTyrian.Core.Tests;

internal static class ArgumentParserTests
{
    private static readonly OptionDefinition[] Definitions =
    [
        new('h', 'h', "help", false),
        new('s', 's', "no-sound", false),
        new('t', 't', "data", true),
        new(256, '\0', "net-player-name", true),
        new(257, '\0', "net-player-number", true)
    ];

    public static void Run()
    {
        ShortClustersAndPermutation();
        ArgumentsAndAbbreviations();
        EndOfOptions();
        Errors();
        IndependentParsers();
    }

    private static void ShortClustersAndPermutation()
    {
        string[] arguments = ["game", "first", "-hs", "second", "-tassets", "-"];
        using var errors = new StringWriter();
        var parser = new ArgumentParser(arguments, Definitions, errors);
        Equal(new ParsedOption('h', null, 1), parser.Next());
        Equal(new ParsedOption('s', null, 1), parser.Next());
        Equal(new ParsedOption('t', "assets", 2), parser.Next());
        Equal(new ParsedOption(0, null, 3), parser.Next());
        Equal("game|-hs|-tassets|first|second|-", string.Join('|', arguments));
        Equal("", errors.ToString());
    }

    private static void ArgumentsAndAbbreviations()
    {
        string[] arguments = ["game", "operand", "-t", "short", "--dat=long", "--data", "-directory", "--data="];
        using var errors = new StringWriter();
        var parser = new ArgumentParser(arguments, Definitions, errors);
        Equal(new ParsedOption('t', "short", 1), parser.Next());
        Equal(new ParsedOption('t', "long", 3), parser.Next());
        Equal(new ParsedOption('t', "-directory", 4), parser.Next());
        Equal(new ParsedOption('t', "", 6), parser.Next());
        Equal(new ParsedOption(0, null, 7), parser.Next());
        Equal("game|-t|short|--dat=long|--data|-directory|--data=|operand", string.Join('|', arguments));
        Equal("", errors.ToString());

        parser = new ArgumentParser(["game", "--help=ignored"], Definitions, errors);
        Equal(new ParsedOption('h', null, 1), parser.Next());
    }

    private static void EndOfOptions()
    {
        string[] arguments = ["game", "first", "--", "-s", "second"];
        using var errors = new StringWriter();
        var parser = new ArgumentParser(arguments, Definitions, errors);
        Equal(new ParsedOption(0, null, 2), parser.Next());
        Equal("game|--|first|-s|second", string.Join('|', arguments));
        Equal(new ParsedOption(0, null, 2), parser.Next());
    }

    private static void Errors()
    {
        CheckError("-z", ArgumentParser.InvalidOption, "game: invalid option -- 'z'\n");
        CheckError("-t", ArgumentParser.MissingArgument, "game: option requires an argument -- 't'\n");
        CheckError("--unknown", ArgumentParser.InvalidOption, "game: unrecognized option '--unknown'\n");
        CheckError("--data", ArgumentParser.MissingArgument, "game: option '--data' requires an argument\n");
        CheckError("--net-player-n", ArgumentParser.AmbiguousOption, "game: option '--net-player-n' is ambiguous\n");
        using var errors = new StringWriter();
        var parser = new ArgumentParser(["game", "--net-player-name=name"], Definitions, errors);
        Equal(new ParsedOption(256, "name", 1), parser.Next());
    }

    private static void IndependentParsers()
    {
        using var errors = new StringWriter();
        var first = new ArgumentParser(["game", "-hs"], Definitions, errors);
        var second = new ArgumentParser(["other", "-t", "value"], Definitions, errors);
        Equal('h', (char)first.Next().Value);
        Equal(new ParsedOption('t', "value", 1), second.Next());
        Equal('s', (char)first.Next().Value);
    }

    private static void CheckError(string argument, int value, string message)
    {
        using var errors = new StringWriter();
        var parser = new ArgumentParser(["game", argument], Definitions, errors);
        Equal(value, parser.Next().Value);
        Equal(message, errors.ToString());
    }

    private static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException($"Expected {expected}, got {actual}.");
        }
    }
}
