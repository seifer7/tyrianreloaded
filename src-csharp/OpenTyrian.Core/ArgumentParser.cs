/*
 * OpenTyrian: A modern cross-platform port of Tyrian
 * Copyright (C) 2007-2009 The OpenTyrian Development Team
 * Distributed under the GNU General Public License, version 2 or later.
 * See COPYING for details.
 */

namespace OpenTyrian.Core;

public readonly record struct OptionDefinition(int Value, char ShortName, string? LongName, bool HasArgument);

public readonly record struct ParsedOption(int Value, string? Argument, int ArgumentIndex);

public sealed class ArgumentParser
{
    public const int NotOption = 0;
    public const int InvalidOption = -1;
    public const int AmbiguousOption = -2;
    public const int MissingArgument = -3;

    private readonly string[] _arguments;
    private readonly IReadOnlyList<OptionDefinition> _options;
    private readonly TextWriter _errors;
    private int _argumentIndex = 1;
    private int _firstNonOption = 1;
    private int _shortOffset = 1;
    private bool _noMoreOptions;

    // argv includes the executable name at index zero and is permuted in place.
    public ArgumentParser(string[] arguments, IReadOnlyList<OptionDefinition> options, TextWriter errors)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(errors);
        if (arguments.Length == 0 || arguments.Any(argument => argument is null))
        {
            throw new ArgumentException("Arguments must include an executable name and cannot contain null.", nameof(arguments));
        }

        _arguments = arguments;
        _options = options;
        _errors = errors;
    }

    public ParsedOption Next()
    {
        var option = new ParsedOption(NotOption, null, _firstNonOption);
        while (_argumentIndex < _arguments.Length)
        {
            var argument = _arguments[_argumentIndex];
            if (!_noMoreOptions && argument.StartsWith('-') && argument.Length > 1)
            {
                var firstOption = _argumentIndex;
                option = option with { ArgumentIndex = firstOption };
                if (argument[1] == '-')
                {
                    if (argument.Length == 2)
                    {
                        _argumentIndex++;
                        _noMoreOptions = true;
                    }
                    else
                    {
                        option = ParseLong(option);
                    }
                }
                else
                {
                    option = ParseShort(option);
                }

                var resultIndex = Permute(firstOption, _argumentIndex);
                return option with { ArgumentIndex = resultIndex + (_noMoreOptions ? 1 : 0) };
            }

            _argumentIndex++;
        }

        return option;
    }

    private int Permute(int firstOption, int afterOption)
    {
        var nonOptions = firstOption - _firstNonOption;
        for (var i = firstOption; i < afterOption; i++)
        {
            for (var j = i; j > _firstNonOption; j--)
            {
                (_arguments[j], _arguments[j - 1]) = (_arguments[j - 1], _arguments[j]);
            }

            _firstNonOption++;
        }

        return firstOption - nonOptions;
    }

    private ParsedOption ParseShort(ParsedOption option)
    {
        var index = option.ArgumentIndex;
        var argument = _arguments[index];
        var character = argument[_shortOffset];
        option = option with { Value = InvalidOption };
        foreach (var definition in _options)
        {
            if (definition.ShortName == '\0' && definition.LongName is null)
            {
                break;
            }

            if (definition.ShortName == '\0' || definition.ShortName != character)
            {
                continue;
            }

            option = option with { Value = definition.Value };
            if (definition.HasArgument)
            {
                if (_shortOffset + 1 < argument.Length)
                {
                    option = option with { Argument = argument[(_shortOffset + 1)..] };
                    _shortOffset = argument.Length;
                }
                else if (index < _arguments.Length - 1)
                {
                    option = option with { Argument = _arguments[++index] };
                    _shortOffset = argument.Length;
                }
                else
                {
                    option = option with { Value = MissingArgument };
                }
            }

            break;
        }

        if (option.Value == InvalidOption)
        {
            _errors.Write($"{_arguments[0]}: invalid option -- '{character}'\n");
        }
        else if (option.Value == MissingArgument)
        {
            _errors.Write($"{_arguments[0]}: option requires an argument -- '{character}'\n");
        }

        if (++_shortOffset >= argument.Length)
        {
            index++;
            _shortOffset = 1;
        }

        _argumentIndex = index;
        return option;
    }

    private ParsedOption ParseLong(ParsedOption option)
    {
        var index = option.ArgumentIndex;
        var argument = _arguments[index][2..];
        var optionLength = FindCharacterOrEnd(argument, '=');
        var name = argument[..optionLength];
        var attached = optionLength < argument.Length;
        var last = index == _arguments.Length - 1;
        option = option with { Value = InvalidOption };
        foreach (var definition in _options)
        {
            if (definition.ShortName == '\0' && definition.LongName is null)
            {
                break;
            }

            if (definition.LongName is null || !definition.LongName.StartsWith(name, StringComparison.Ordinal))
            {
                continue;
            }

            if (option.Value != InvalidOption)
            {
                option = option with { Value = AmbiguousOption };
                break;
            }

            option = option with { Value = definition.Value };
            if (definition.HasArgument)
            {
                if (attached)
                {
                    option = option with { Argument = argument[(optionLength + 1)..] };
                }
                else if (!last)
                {
                    option = option with { Argument = _arguments[++index] };
                }
                else
                {
                    option = option with { Value = MissingArgument };
                }
            }

            if (optionLength == definition.LongName.Length)
            {
                break;
            }
        }

        var originalArgument = _arguments[option.ArgumentIndex];
        switch (option.Value)
        {
            case InvalidOption:
                _errors.Write($"{_arguments[0]}: unrecognized option '{originalArgument}'\n");
                break;
            case AmbiguousOption:
                _errors.Write($"{_arguments[0]}: option '{originalArgument}' is ambiguous\n");
                break;
            case MissingArgument:
                _errors.Write($"{_arguments[0]}: option '{originalArgument}' requires an argument\n");
                break;
        }

        _argumentIndex = index + 1;
        return option;
    }

    private static int FindCharacterOrEnd(string value, char character)
    {
        var index = value.IndexOf(character);
        return index < 0 ? value.Length : index;
    }
}
