/*
 * OpenTyrian: A modern cross-platform port of Tyrian
 * Copyright (C) 2007-2009 The OpenTyrian Development Team
 * Distributed under the GNU General Public License, version 2 or later.
 * See COPYING for details.
 */

using System.Globalization;
using System.Text;

namespace OpenTyrian.Core;

public enum ItemType { Shot, Ship, WeaponFront, WeaponRear, Generator, Shield, SidekickLeft, SidekickRight, Special, Enemy, Count }

public readonly record struct ItemField(string Key, string Value, int Line);

public sealed class ItemRecord(string id, string path, int line)
{
    public string Id { get; } = id;
    public string Path { get; } = path;
    public int Line { get; } = line;
    public List<ItemField> Fields { get; } = [];
}

public sealed class ItemDefinitions
{
    private enum FieldKind { Integer, Reference, Name }
    private readonly List<ItemRecord>[] _records = Enumerable.Range(0, (int)ItemType.Count).Select(_ => new List<ItemRecord>()).ToArray();
    private readonly Dictionary<string, int>[] _indices = Enumerable.Range(0, (int)ItemType.Count)
        .Select(_ => new Dictionary<string, int>(StringComparer.Ordinal)).ToArray();

    public static IReadOnlyList<string> BaseFiles { get; } = Array.AsReadOnly(new[]
    {
        "data/shots/shots.tyrr", "data/items/ships.tyrr", "data/items/weapons.tyrr",
        "data/items/generators.tyrr", "data/items/shields.tyrr", "data/items/sidekicks.tyrr",
        "data/items/specials.tyrr", "data/enemy/enemies.tyrr"
    });

    public IReadOnlyList<ItemRecord> Records(ItemType type) => _records[(int)Normalize(type)].AsReadOnly();

    public bool TryFind(ItemType type, string id, out int index) => _indices[(int)Normalize(type)].TryGetValue(id, out index);

    public void LoadBundle(string path, string? diagnosticPath = null)
    {
        diagnosticPath ??= path;
        byte[] data;
        try
        {
            data = File.ReadAllBytes(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw Error(diagnosticPath, 0, $"cannot read extracted data file: {exception.Message}");
        }

        ParseBundle(data, diagnosticPath);
    }

    public void LoadAll(string workingDirectory)
    {
        foreach (var path in BaseFiles)
        {
            LoadBundle(Resolve(workingDirectory, path), path);
        }

        var paths = new List<string>();
        foreach (var directory in new[] { "data/items", "data/enemy", "data/shots" })
        {
            try
            {
                foreach (var path in Directory.EnumerateFiles(Resolve(workingDirectory, directory)))
                {
                    var relative = directory + "/" + System.IO.Path.GetFileName(path);
                    if (AsciiEquals(System.IO.Path.GetExtension(path), ".tyrr") && !BaseFiles.Any(baseFile => AsciiEquals(baseFile, relative)))
                    {
                        if (Encoding.Latin1.GetByteCount(relative) >= 4096)
                        {
                            throw Error(directory, 0, "definition path is too long");
                        }

                        paths.Add(relative);
                    }
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                throw Error(directory, 0, $"cannot enumerate .tyrr files: {exception.Message}");
            }
        }

        paths.Sort(ComparePaths);
        foreach (var path in paths)
        {
            LoadBundle(Resolve(workingDirectory, path), path);
        }
    }

    public void ParseBundle(ReadOnlySpan<byte> data, string path)
    {
        if (data.Contains((byte)0))
        {
            throw Error(path, 0, "file contains a NUL byte");
        }

        if (data.StartsWith(new byte[] { 0xEF, 0xBB, 0xBF }))
        {
            data = data[3..];
        }

        var text = Encoding.Latin1.GetString(data);
        var lineNumber = 0;
        ItemRecord? record = null;
        var type = ItemType.Count;
        var cursor = 0;
        while (cursor < text.Length)
        {
            var end = text.IndexOf('\n', cursor);
            if (end < 0)
            {
                end = text.Length;
            }

            var line = Trim(text[cursor..end]);
            cursor = end + 1;
            lineNumber++;
            if (line.Length == 0 || line[0] is '#' or ';')
            {
                continue;
            }

            if (line[0] == '[')
            {
                var colon = line.IndexOf(':');
                if (colon < 0 || line[^1] != ']')
                {
                    throw Error(path, lineNumber, "expected [type:id] header");
                }

                var id = Trim(line[(colon + 1)..^1]);
                if (!ValidId(id))
                {
                    throw Error(path, lineNumber, $"invalid object ID '{id}'");
                }

                type = TypeFromName(Trim(line[1..colon]), path, lineNumber);
                record = Replace(type, id, path, lineNumber);
                continue;
            }

            if (record is null)
            {
                throw Error(path, lineNumber, "field appears before an object header");
            }

            var equals = line.IndexOf('=');
            if (equals < 0)
            {
                throw Error(path, lineNumber, "expected key=value");
            }

            var key = Trim(line[..equals]);
            var value = Trim(line[(equals + 1)..]);
            if (key.Length >= 30 || value.Length >= 255)
            {
                throw Error(path, lineNumber, "field name or value is too long");
            }

            ValidateField(type, key, value, path, lineNumber);
            if (key == "id" && value != record.Id)
            {
                throw Error(path, lineNumber, $"id field does not match header ID '{record.Id}'");
            }

            record.Fields.Add(new ItemField(key, value, lineNumber));
        }
    }

    public void ReserveZeroSlots()
    {
        for (var type = 0; type < _records.Length; type++)
        {
            var records = _records[type];
            if (records.Count == 0)
            {
                continue;
            }

            if (!_indices[type].TryGetValue("0", out var zero))
            {
                throw Error("data", 0, $"missing required object ID '0' for type {type}");
            }

            if (zero != 0)
            {
                var record = records[zero];
                records.RemoveAt(zero);
                records.Insert(0, record);
                for (var index = 0; index < records.Count; index++)
                {
                    _indices[type][records[index].Id] = index;
                }
            }
        }
    }

    public static bool IsSpace(char character) => character == ' ' || character is >= '\t' and <= '\r';

    public static string Trim(string text)
    {
        var start = 0;
        var end = text.Length;
        while (start < end && IsSpace(text[start]))
        {
            start++;
        }

        while (end > start && IsSpace(text[end - 1]))
        {
            end--;
        }

        return text[start..end];
    }

    public static bool ValidId(ReadOnlySpan<char> id)
    {
        if (id.Length is 0 or > 10)
        {
            return false;
        }

        foreach (var character in id)
        {
            if (character is ':' or '[' or ']' or '=' or '#' or ';' || IsSpace(character))
            {
                return false;
            }
        }

        return true;
    }

    public static int ComparePaths(string left, string right)
    {
        var length = Math.Min(left.Length, right.Length);
        for (var index = 0; index < length; index++)
        {
            var comparison = Fold(left[index]).CompareTo(Fold(right[index]));
            if (comparison != 0)
            {
                return comparison;
            }
        }

        var lengthComparison = left.Length.CompareTo(right.Length);
        return lengthComparison == 0 ? StringComparer.Ordinal.Compare(left, right) : lengthComparison;
    }

    private static char Fold(char character) => character is >= 'A' and <= 'Z' ? (char)(character + ('a' - 'A')) : character;

    private static bool AsciiEquals(string left, string right)
        => left.Length == right.Length && left.Zip(right).All(pair => Fold(pair.First) == Fold(pair.Second));

    private ItemRecord Replace(ItemType type, string id, string path, int line)
    {
        var records = _records[(int)type];
        var indices = _indices[(int)type];
        var record = new ItemRecord(id, path, line);
        if (indices.TryGetValue(id, out var index))
        {
            records[index] = record;
        }
        else
        {
            indices.Add(id, records.Count);
            records.Add(record);
        }

        return record;
    }

    private static ItemType Normalize(ItemType type)
    {
        if (type < ItemType.Shot || type >= ItemType.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(type));
        }

        return type switch { ItemType.WeaponRear => ItemType.WeaponFront, ItemType.SidekickRight => ItemType.SidekickLeft, _ => type };
    }

    private static ItemType TypeFromName(string name, string path, int line) => name switch
    {
        "shot" => ItemType.Shot,
        "ship" => ItemType.Ship,
        "weapon" => ItemType.WeaponFront,
        "generator" => ItemType.Generator,
        "shield" => ItemType.Shield,
        "sidekick" => ItemType.SidekickLeft,
        "special" => ItemType.Special,
        "enemy" => ItemType.Enemy,
        _ => throw Error(path, line, $"unknown object type '{name}'")
    };

    private static void ValidateField(ItemType type, string key, string value, string path, int line)
    {
        if (!Schemas[(int)type].TryGetValue(key, out var kind))
        {
            throw Error(path, line, $"unknown field '{key}'");
        }

        if (kind == FieldKind.Name)
        {
            if (value.Length > 30)
            {
                throw Error(path, line, "name exceeds 30 characters");
            }

            return;
        }

        if (value.Length == 0)
        {
            throw Error(path, line, $"field '{key}' requires a value");
        }

        foreach (var token in Tokens(value))
        {
            if (kind == FieldKind.Reference)
            {
                if (token.Length > 10)
                {
                    throw Error(path, line, "reference ID exceeds 10 characters");
                }

                if (!ValidId(token))
                {
                    throw Error(path, line, $"invalid reference ID '{token}'");
                }
            }
            else if (!int.TryParse(token, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out _))
            {
                throw Error(path, line, $"invalid integer in field '{key}'");
            }
        }
    }

    public static IEnumerable<string> Tokens(string value) => value.Split([' ', '\t', '\n', '\v', '\f', '\r'], StringSplitOptions.RemoveEmptyEntries);

    internal static InvalidDataException Error(string path, int line, string message)
        => new($"error: {path}:{line}: {message}\nRun the Data Extractor executable first to build the required extracted data files.\n" +
            "If using mods, check the referenced definitions in your .tyrr files.");

    private static string Resolve(string directory, string path) => System.IO.Path.Combine(directory, path.Replace('/', System.IO.Path.DirectorySeparatorChar));

    private static Dictionary<string, FieldKind> Schema(string integers, string references, bool name = false)
    {
        var schema = new Dictionary<string, FieldKind>(StringComparer.Ordinal);
        foreach (var key in integers.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            schema.Add(key, FieldKind.Integer);
        }

        foreach (var key in ("id " + references).Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            schema.Add(key, FieldKind.Reference);
        }

        if (name)
        {
            schema.Add("name", FieldKind.Name);
        }

        return schema;
    }

    private static readonly Dictionary<string, FieldKind>[] Schemas =
    [
        Schema("drain shotrepeat multi weapani max tx ty aim attack iceticks infinite del sx sy bx by sg acceleration accelerationx circlesize sound trail shipblastfilter", "chain"),
        Schema("shipgraphic itemgraphic ani spd dmg cost bigshipgraphic bigshipgraphx bigshipgraphy probability", "", true),
        Schema("opnum cost itemgraphic poweruse position bigshipgraphic bigshipgraphfrntx bigshipgraphfrnty bigshipgraphrearx bigshipgraphreary probability", "op0 op1 linkgunweapon chargegunweapons", true),
        [],
        Schema("itemgraphic power speed cost bigshipgraphic bigshipgraphx bigshipgraphy probability", "", true),
        Schema("itemgraphic tpwr mpwr cost probability", "", true),
        Schema("itemgraphic pwr cost tr option opspd ani gr0 gr1 gr2 gr3 gr4 ammo stop icongr position probability", "wport wpnum", true),
        [],
        Schema("itemgraphic pwr stype", "wpn", true),
        Schema("ani tur freq xmove ymove xaccel yaccel xcaccel ycaccel startx starty startxc startyc armor esize egraphic explosiontype animate shapebank xrev yrev dgr dlevel dani elaunchfreq elaunchtypepos elaunchspecial value eshotwait enemyground", "elaunchtype eenemydie")
    ];
}
