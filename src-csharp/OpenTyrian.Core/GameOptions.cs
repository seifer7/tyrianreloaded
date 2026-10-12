/*
 * OpenTyrian: A modern cross-platform port of Tyrian
 * Copyright (C) 2007-2009 The OpenTyrian Development Team
 * Distributed under the GNU General Public License, version 2 or later.
 * See COPYING for details.
 */

namespace OpenTyrian.Core;

public sealed class GameOptions
{
    public bool AudioDisabled { get; private set; }
    public bool IgnoreJoystick { get; private set; }
    public bool Christmas { get; private set; }
    public bool ConstantPlay { get; private set; }
    public bool ConstantDie { get; private set; }
    public bool RecordDemo { get; private set; }
    public bool RichMode { get; private set; }
    public bool NetworkGame { get; private set; }
    public string? DataDirectory { get; private set; }
    public string? OpponentHost { get; private set; }
    public string PlayerName { get; private set; } = "";
    public ushort PlayerPort { get; private set; } = 1333;
    public ushort OpponentPort { get; private set; } = 1333;
    public uint PlayerNumber { get; private set; }
    public int NetworkDelay { get; private set; } = 2;

    private static readonly OptionDefinition[] Definitions =
    [
        new('h', 'h', "help", false), new('s', 's', "no-sound", false),
        new('j', 'j', "no-joystick", false), new('x', 'x', "no-xmas", false),
        new('t', 't', "data", true), new('n', 'n', "net", true),
        new(256, '\0', "net-player-name", true), new(257, '\0', "net-player-number", true),
        new('p', 'p', "net-port", true), new('d', 'd', "net-delay", true),
        new('X', 'X', "xmas", false), new('c', 'c', "constant", false),
        new('k', 'k', "death", false), new('r', 'r', "record", false),
        new('l', 'l', "loot", false)
    ];

    public static GameOptionResult Parse(string[] arguments, TextWriter output, TextWriter errors, bool christmas = false)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(errors);
        var args = (string[])arguments.Clone();
        var parser = new ArgumentParser(args, Definitions, errors);
        var options = new GameOptions { Christmas = christmas };
        while (true)
        {
            var option = parser.Next();
            if (option.Value == ArgumentParser.NotOption)
            {
                for (var i = option.ArgumentIndex; i < args.Length; i++)
                {
                    args[i] = string.Concat(args[i].Select(c => c is >= 'a' and <= 'z' ? (char)(c - 32) : c));
                    switch (args[i])
                    {
                        case "LOOT": options.RichMode = true; break;
                        case "RECORD": options.RecordDemo = true; break;
                        case "NOJOY": options.IgnoreJoystick = true; break;
                        case "CONSTANT": options.ConstantPlay = true; break;
                        case "DEATH": options.ConstantDie = true; break;
                        case "NOSOUND": options.AudioDisabled = true; break;
                        case "NOXMAS": options.Christmas = false; break;
                        case "YESXMAS": options.Christmas = true; break;
                    }
                }
                return new(options, args, null);
            }
            switch (option.Value)
            {
                case ArgumentParser.InvalidOption:
                case ArgumentParser.AmbiguousOption:
                case ArgumentParser.MissingArgument:
                    errors.Write($"Try `{args[0]} --help' for more information.\n");
                    return new(options, args, 1);
                case 'h':
                    output.Write(Help(args[0]));
                    return new(options, args, 0);
                case 's': options.AudioDisabled = true; break;
                case 'j': options.IgnoreJoystick = true; break;
                case 'x': options.Christmas = false; break;
                case 't': options.DataDirectory = option.Argument; break;
                case 'n':
                    options.NetworkGame = true;
                    var host = option.Argument!;
                    var colon = host.IndexOf(':');
                    if (colon >= 0)
                    {
                        if (!TryIntegerPrefix(host.AsSpan(colon + 1), out var port) || port is <= 0 or >= 49152)
                            return Invalid("network port number");
                        options.OpponentPort = (ushort)port;
                        options.OpponentHost = host[..colon];
                    }
                    else options.OpponentHost = host;
                    break;
                case 256: options.PlayerName = option.Argument!; break;
                case 257:
                    if (!TryIntegerPrefix(option.Argument, out var player) || player is < 1 or > 2)
                        return Invalid("network player number");
                    options.PlayerNumber = (uint)player;
                    break;
                case 'p':
                    if (!TryIntegerPrefix(option.Argument, out var localPort) || localPort is <= 0 or >= 49152)
                        return Invalid("network port number");
                    options.PlayerPort = (ushort)localPort;
                    break;
                case 'd':
                    if (!TryIntegerPrefix(option.Argument, out var delay) || delay == int.MaxValue)
                        return Invalid("network delay value");
                    options.NetworkDelay = delay + 1;
                    break;
                case 'X': options.Christmas = true; break;
                case 'c': options.ConstantPlay = true; break;
                case 'k': options.ConstantDie = true; break;
                case 'r': options.RecordDemo = true; break;
                case 'l': options.RichMode = true; break;
                default: throw new InvalidOperationException("Unhandled game option definition.");
            }
        }

        GameOptionResult Invalid(string description)
        {
            errors.Write($"{args[0]}: error: invalid {description}\n");
            return new(options, args, 1);
        }
    }

    private static bool TryIntegerPrefix(ReadOnlySpan<char> text, out int value)
    {
        value = 0;
        var position = 0;
        while (position < text.Length && text[position] is ' ' or '\t' or '\n' or '\r' or '\v' or '\f') position++;
        var negative = position < text.Length && text[position] == '-';
        if (position < text.Length && text[position] is '+' or '-') position++;
        var start = position;
        long number = 0;
        while (position < text.Length && text[position] is >= '0' and <= '9')
        {
            number = number * 10 + text[position++] - '0';
            if (number > (negative ? 2147483648L : int.MaxValue)) return false;
        }
        value = (int)(negative ? -number : number);
        return position > start;
    }

    private static string Help(string executable) =>
        $"Usage: {executable} [OPTION...]\n\n" +
        "Options:\n" +
        "  -h, --help                   Show help about options\n\n" +
        "  -s, --no-sound               Disable audio\n" +
        "  -j, --no-joystick            Disable joystick/gamepad input\n" +
        "  -x, --no-xmas                Disable Christmas mode\n\n" +
        "  -t, --data=DIR               Set Tyrian data directory\n\n" +
        "  -n, --net=HOST[:PORT]        Start a networked game\n" +
        "  --net-player-name=NAME       Sets local player name in a networked game\n" +
        "  --net-player-number=NUMBER   Sets local player number in a networked game\n" +
        "                               (1 or 2)\n" +
        "  -p, --net-port=PORT          Local port to bind (default is 1333)\n" +
        "  -d, --net-delay=FRAMES       Set lag-compensation delay (default is 1)\n";
}

public sealed record GameOptionResult(GameOptions Options, string[] Arguments, int? ExitCode);
