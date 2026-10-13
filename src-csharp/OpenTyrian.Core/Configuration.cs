/*
 * Derived from config_file.c/config_file.h by Carl Reinke, 2015.
 * Distributed under the GNU General Public License, version 2 or later.
 * See COPYING for details.
 */

using System.Globalization;
using System.Text;

namespace OpenTyrian.Core;

public enum ConfigBoolStyle
{
    Numeric,
    NoYes,
    OffOn,
    FalseTrue
}

public sealed class ConfigOption
{
    private readonly List<string> _values = [];
    public string Key { get; }
    public bool IsList { get; private set; }
    public IReadOnlyList<string> Values => _values.Select(CString).ToArray();
    public int Count => _values.Count;
    public string? Value => IsList || _values.Count == 0 ? null : CString(_values[0]);

    private static string CString(string text)
    {
        var end = text.IndexOf('\0');
        return end < 0 ? text : text[..end];
    }

    internal ConfigOption(string key, string? value)
    {
        Key = key;
        SetValue(value);
    }

    public ConfigOption SetValue(string? value)
    {
        _values.Clear();
        IsList = value is null;
        if (value is not null)
        {
            _values.Add(value);
        }

        return this;
    }

    public ConfigOption AddValue(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        _values.Add(value);
        IsList = true;
        return this;
    }

    public ConfigOption? RemoveValue(int index)
    {
        if ((uint)index >= (uint)_values.Count)
        {
            return null;
        }

        _values.RemoveAt(index);
        if (_values.Count == 0)
        {
            IsList = true;
        }

        return this;
    }
}

public sealed class ConfigSection
{
    private static readonly string[][] BoolValues = [["0", "1"], ["no", "yes"], ["off", "on"], ["false", "true"]];
    private readonly List<ConfigOption> _options = [];
    public string Type { get; }
    public string? Name { get; }
    public IReadOnlyList<ConfigOption> Options => _options.AsReadOnly();

    internal ConfigSection(string type, string? name)
    {
        Type = type;
        Name = name;
    }

    public ConfigOption? GetOption(string key) => _options.FirstOrDefault(option => option.Key == key);

    public ConfigOption SetOption(string key, string? value)
    {
        ArgumentNullException.ThrowIfNull(key);
        var option = GetOption(key);
        if (option is not null)
        {
            return option.SetValue(value);
        }

        return AppendOption(key, value);
    }

    public ConfigOption GetOrSetOption(string key, string? value)
    {
        ArgumentNullException.ThrowIfNull(key);
        return GetOption(key) ?? AppendOption(key, value);
    }

    private ConfigOption AppendOption(string key, string? value)
    {
        var option = new ConfigOption(key, value);
        _options.Add(option);
        return option;
    }

    public bool TryGetString(string key, out string? value)
    {
        value = GetOption(key)?.Value;
        return value is not null;
    }

    public string? GetOrSetString(string key, string? value)
    {
        if (TryGetString(key, out var existing))
        {
            return existing;
        }

        SetOption(key, value);
        return value;
    }

    public void SetBoolean(string key, bool value, ConfigBoolStyle style)
    {
        if ((uint)style >= BoolValues.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(style));
        }

        SetOption(key, BoolValues[(int)style][value ? 1 : 0]);
    }

    public bool TryGetBoolean(string key, out bool value)
    {
        value = false;
        if (!TryGetString(key, out var text))
        {
            return false;
        }

        foreach (var pair in BoolValues)
        {
            if (text == pair[0] || text == pair[1])
            {
                value = text == pair[1];
                return true;
            }
        }

        return false;
    }

    public bool GetOrSetBoolean(string key, bool value, ConfigBoolStyle style)
    {
        if (TryGetBoolean(key, out var existing))
        {
            return existing;
        }

        SetBoolean(key, value, style);
        return value;
    }

    public void SetInt32(string key, int value) => SetOption(key, value.ToString(CultureInfo.InvariantCulture));
    public void SetUInt32(string key, uint value) => SetOption(key, value.ToString(CultureInfo.InvariantCulture));

    public bool TryGetInt32(string key, out int value)
    {
        var success = TryGetInteger(key, detectBase: true, out var bits);
        value = unchecked((int)bits);
        return success;
    }

    public bool TryGetUInt32(string key, out uint value) => TryGetInteger(key, detectBase: false, out value);

    public int GetOrSetInt32(string key, int value)
    {
        if (TryGetInt32(key, out var existing))
        {
            return existing;
        }

        SetInt32(key, value);
        return value;
    }

    public uint GetOrSetUInt32(string key, uint value)
    {
        if (TryGetUInt32(key, out var existing))
        {
            return existing;
        }

        SetUInt32(key, value);
        return value;
    }

    private bool TryGetInteger(string key, bool detectBase, out uint value)
    {
        value = 0;
        if (!TryGetString(key, out var text) || text is null)
        {
            return false;
        }

        var index = 0;
        while (index < text.Length && text[index] is ' ' or '\t' or '\n' or '\r' or '\v' or '\f')
        {
            index++;
        }

        var negative = index < text.Length && text[index] == '-';
        if (index < text.Length && text[index] is '-' or '+')
        {
            index++;
        }

        var radix = 10;
        if (detectBase && index < text.Length && text[index] == '0')
        {
            radix = 8;
            if (index + 1 < text.Length && text[index + 1] is 'x' or 'X')
            {
                index += 2;
                radix = 16;
            }
        }

        var start = index;
        ulong number = 0;
        for (; index < text.Length; index++)
        {
            var character = text[index];
            var digit = character is >= '0' and <= '9' ? character - '0'
                : character is >= 'a' and <= 'f' ? character - 'a' + 10
                : character is >= 'A' and <= 'F' ? character - 'A' + 10 : -1;
            if (digit < 0 || digit >= radix)
            {
                return false;
            }

            number = unchecked(number * (uint)radix + (uint)digit);
        }

        if (index == start)
        {
            return false;
        }

        value = unchecked((uint)(negative ? 0ul - number : number));
        return true;
    }
}

public sealed class Configuration
{
    private readonly List<ConfigSection> _sections = [];
    public IReadOnlyList<ConfigSection> Sections => _sections.AsReadOnly();
    public void Clear() => _sections.Clear();

    public static void ThrowOutOfMemory(TextWriter errors)
    {
        ArgumentNullException.ThrowIfNull(errors);
        errors.WriteLine("out of memory");
        throw new OutOfMemoryException("out of memory");
    }

    public ConfigSection AddSection(string type, string? name = null)
    {
        ArgumentNullException.ThrowIfNull(type);
        var section = new ConfigSection(type, name);
        _sections.Add(section);
        return section;
    }

    public IEnumerable<ConfigSection> FindSections(string type) => _sections.Where(section => section.Type == type);

    public ConfigSection? FindSection(string type, string? name = null)
    {
        return _sections.FirstOrDefault(section => section.Type == type && section.Name == name);
    }

    public ConfigSection FindOrAddSection(string type, string? name = null)
    {
        return FindSection(type, name) ?? AddSection(type, name);
    }

    public static Configuration Parse(TextReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);
        var config = new Configuration();
        ConfigSection? section = null;
        ConfigOption? option = null;
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            var index = 0;
            var directive = ReadDirective(line, ref index);
            if (directive is null || !TryReadField(line, ref index, out var first))
            {
                continue;
            }

            if (directive == "section")
            {
                var hasName = TryReadField(line, ref index, out var name);
                section = config.AddSection(first, hasName ? name : null);
                option = null;
            }
            else if (section is not null && TryReadField(line, ref index, out var value))
            {
                if (directive == "item")
                {
                    option = section.SetOption(first, value);
                }
                else
                {
                    if (option is null || option.Key != first)
                    {
                        option = section.GetOrSetOption(first, null);
                    }

                    option.AddValue(value);
                }
            }
        }

        return config;
    }

    private static bool IsWhitespace(char character) => character is ' ' or '\t';
    private static bool IsEnd(char character) => character is '\0' or '\r' or '\n';
    private static char CharacterAt(string line, int index) => index < line.Length ? line[index] : '\0';

    private static string? ReadDirective(string line, ref int index)
    {
        while (IsWhitespace(CharacterAt(line, index)))
        {
            index++;
        }

        foreach (var directive in new[] { "section", "item", "list" })
        {
            if (line.AsSpan(index).StartsWith(directive, StringComparison.Ordinal))
            {
                var after = CharacterAt(line, index + directive.Length);
                if (IsWhitespace(after) || IsEnd(after))
                {
                    index += directive.Length;
                    return directive;
                }
            }
        }

        return null;
    }

    private static bool TryReadField(string line, ref int index, out string field)
    {
        field = "";
        var position = index;
        while (IsWhitespace(CharacterAt(line, position)))
        {
            position++;
        }

        var quote = CharacterAt(line, position);
        var output = new StringBuilder();
        if (quote is '\'' or '"')
        {
            while (true)
            {
                var character = CharacterAt(line, ++position);
                if (character == quote)
                {
                    position++;
                    break;
                }

                if (character == '\\')
                {
                    character = CharacterAt(line, ++position);
                    if (character == quote)
                    {
                        output.Append(quote);
                    }
                    else if (character == 'x')
                    {
                        var high = HexDigit(CharacterAt(line, ++position));
                        var low = HexDigit(CharacterAt(line, ++position));
                        if (high < 0 || low < 0)
                        {
                            return false;
                        }

                        output.Append((char)((high << 4) | low));
                    }
                    else
                    {
                        var decoded = character switch { 't' => '\t', 'n' => '\n', 'r' => '\r', '\\' => '\\', _ => '\0' };
                        if (decoded == '\0')
                        {
                            return false;
                        }

                        output.Append(decoded);
                    }
                }
                else if (character is >= ' ' and <= '~')
                {
                    output.Append(character);
                }
                else
                {
                    return false;
                }
            }
        }
        else
        {
            while (true)
            {
                var character = CharacterAt(line, position);
                if (IsWhitespace(character) || IsEnd(character))
                {
                    break;
                }

                if (character is <= ' ' or > '~' or '#' or '\'' or '"')
                {
                    return false;
                }

                output.Append(character);
                position++;
            }

            if (output.Length == 0)
            {
                return false;
            }
        }

        var next = CharacterAt(line, position);
        if (!IsWhitespace(next) && !IsEnd(next))
        {
            return false;
        }

        index = position;
        field = output.ToString();
        return true;
    }

    private static int HexDigit(char character) => character switch
    {
        >= '0' and <= '9' => character - '0',
        >= 'a' and <= 'f' => character - 'a' + 10,
        >= 'A' and <= 'F' => character - 'A' + 10,
        _ => -1
    };

    public void Write(TextWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        foreach (var section in _sections)
        {
            writer.Write("section ");
            WriteField(writer, section.Type);
            if (section.Name is not null)
            {
                writer.Write(' ');
                WriteField(writer, section.Name);
            }

            writer.Write('\n');
            foreach (var option in section.Options)
            {
                foreach (var value in option.Values)
                {
                    writer.Write(option.IsList ? "\tlist " : "\titem ");
                    WriteField(writer, option.Key);
                    writer.Write(' ');
                    WriteField(writer, value);
                    writer.Write('\n');
                }
            }

            writer.Write('\n');
        }
    }

    private static void WriteField(TextWriter writer, string field)
    {
        writer.Write('\'');
        foreach (var character in field.TakeWhile(character => character != '\0'))
        {
            switch (character)
            {
                case '\t': writer.Write("\\t"); break;
                case '\n': writer.Write("\\n"); break;
                case '\r': writer.Write("\\r"); break;
                case '\'':
                case '\\': writer.Write('\\'); writer.Write(character); break;
                default:
                    if (character is >= ' ' and <= '~')
                    {
                        writer.Write(character);
                    }
                    else if (character <= byte.MaxValue)
                    {
                        writer.Write($"\\x{(byte)character:x2}");
                    }
                    else
                    {
                        throw new InvalidDataException("Native configuration fields must contain single-byte characters.");
                    }

                    break;
            }
        }

        writer.Write('\'');
    }
}
