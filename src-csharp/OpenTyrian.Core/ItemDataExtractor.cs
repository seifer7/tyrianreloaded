/*
 * OpenTyrian: A modern cross-platform port of Tyrian
 * Copyright (C) 2007-2009 The OpenTyrian Development Team
 * Distributed under the GNU General Public License, version 2 or later.
 * See COPYING for details.
 */

using System.Globalization;
using System.Text;

namespace OpenTyrian.Core;

public static class ItemDataExtractor
{
    public static int Run(string[] arguments, TextWriter output, TextWriter errors, string workingDirectory,
        string program = "OpenTyrian.DataExtractor", string? installedDirectory = null)
    {
        string? customDirectory = null;
        for (var index = 0; index < arguments.Length; index++)
        {
            var argument = arguments[index];
            if (argument == "--help")
            {
                PrintUsage(output, program);
                return 0;
            }

            if (argument == "--data" && index + 1 < arguments.Length)
            {
                customDirectory = arguments[++index];
            }
            else if (argument.StartsWith("--data=", StringComparison.Ordinal))
            {
                customDirectory = argument[7..];
            }
            else
            {
                errors.WriteLine($"error: unknown option or missing argument: {argument}");
                PrintUsage(output, program);
                return 1;
            }

            if (customDirectory.Length == 0)
            {
                errors.WriteLine("error: the asset directory must not be empty.");
                return 1;
            }
        }

        var files = new DataFiles(workingDirectory, customDirectory, installedDirectory);
        var assetDirectory = customDirectory ?? files.Directory;
        output.WriteLine($"Extracting Tyrian 2.1 definitions from '{assetDirectory}' into './data'...");
        try
        {
            Extract(files, assetDirectory, workingDirectory, output);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            var message = exception.Message;
            errors.WriteLine(message.StartsWith("error:", StringComparison.Ordinal) ? message : $"error: {message}");
            return 1;
        }

        output.WriteLine("Extraction complete: eight .tyrr category text files. Existing category files were preserved.");
        return 0;
    }

    public static void PrintUsage(TextWriter output, string program)
    {
        output.WriteLine($"Usage: {program} [--data DIR]");
        output.WriteLine();
        output.WriteLine("Extract enemy, item and shot definitions from Tyrian 2.1 assets.");
        output.WriteLine("Output: eight .tyrr category text files in data subfolders under the current working directory.");
        output.WriteLine("Records use [type:id] headers followed by key=value fields.");
        output.WriteLine("Existing category files are preserved. Shapes and levels are not extracted.");
        output.WriteLine();
        output.WriteLine("  --data DIR   Original Tyrian asset directory (default: game data search)");
        output.WriteLine("  --help       Show this help");
    }

    public static void Extract(DataFiles files, string assetDirectory, string workingDirectory, TextWriter output)
    {
        foreach (var directory in new[] { "data", Path.Combine("data", "shots"), Path.Combine("data", "enemy"), Path.Combine("data", "items") })
        {
            var path = Path.Combine(workingDirectory, directory);
            if (OperatingSystem.IsWindows())
            {
                Directory.CreateDirectory(path);
            }
            else
            {
                Directory.CreateDirectory(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            }
        }

        using var stream = files.OpenRequired(assetDirectory, "tyrian.hdt");
        Span<int> offset = stackalloc int[1];
        BinaryFile.ReadInt32(stream, offset);
        if (offset[0] < 0 || offset[0] > stream.Length)
        {
            throw new InvalidDataException("error: cannot seek to item data in tyrian.hdt: invalid item-data offset.");
        }

        stream.Position = offset[0];
        ReadLegacyData(stream, workingDirectory, output);
    }

    public static void ReadLegacyData(Stream stream, string workingDirectory, TextWriter output)
    {
        Span<ushort> ignoredCounts = stackalloc ushort[7];
        BinaryFile.ReadUInt16(stream, ignoredCounts);
        var reader = new LegacyReader(stream);
        WriteCategory(reader, workingDirectory, output, "data/shots/shots.tyrr", 781, ReadShot);
        WriteCategory(reader, workingDirectory, output, "data/items/weapons.tyrr", 43, ReadWeapon);
        WriteCategory(reader, workingDirectory, output, "data/items/specials.tyrr", 47, ReadSpecial);
        WriteCategory(reader, workingDirectory, output, "data/items/generators.tyrr", 7, ReadGenerator);
        WriteCategory(reader, workingDirectory, output, "data/items/ships.tyrr", 14, ReadShip);
        WriteCategory(reader, workingDirectory, output, "data/items/sidekicks.tyrr", 31, ReadSidekick);
        WriteCategory(reader, workingDirectory, output, "data/items/shields.tyrr", 11, ReadShield);
        WriteCategory(reader, workingDirectory, output, "data/enemy/enemies.tyrr", 851, ReadEnemy);
    }

    private static void WriteCategory(LegacyReader reader, string directory, TextWriter output, string category,
        int count, Func<LegacyReader, int, string> readRecord)
    {
        var path = Path.Combine(directory, category.Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(path))
        {
            output.WriteLine($"Preserving existing category file '{category}'.");
            for (var index = 0; index < count; index++)
            {
                _ = readRecord(reader, index);
            }

            return;
        }

        var temporary = path + ".tmp";
        try
        {
            using var writer = new StreamWriter(temporary, append: false, Encoding.Latin1);
            for (var index = 0; index < count; index++)
            {
                var text = readRecord(reader, index);
                writer.Write(OperatingSystem.IsWindows() ? text.Replace("\n", "\r\n", StringComparison.Ordinal) : text);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            try
            {
                File.Delete(temporary);
            }
            catch (Exception cleanup) when (cleanup is IOException or UnauthorizedAccessException)
            {
                throw new IOException($"error: failed to write extracted data to '{category}': {exception.Message}\n" +
                    $"error: failed to remove incomplete output '{category}.tmp': {cleanup.Message}", exception);
            }

            throw new IOException($"error: failed to write extracted data to '{category}': {exception.Message}", exception);
        }

        // Never overwrite a category created by another process during extraction.
        try
        {
            File.Move(temporary, path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new IOException($"error: failed to finalize extracted category '{category}': {exception.Message}", exception);
        }
    }

    private static string ReadShot(LegacyReader reader, int id)
    {
        var record = new Record("shot", id);
        record.Number("drain", reader.U16());
        record.Number("shotrepeat", reader.U8());
        record.Number("multi", reader.U8());
        record.Number("weapani", reader.U16());
        foreach (var name in new[] { "max", "tx", "ty", "aim" })
        {
            record.Number(name, reader.U8());
        }

        var attack = reader.Values(8, reader.U8);
        var delay = reader.Values(8, reader.U8);
        var sx = reader.Values(8, reader.S8);
        var sy = reader.Values(8, reader.S8);
        var bx = reader.Values(8, reader.S8);
        var by = reader.Values(8, reader.S8);
        var graphics = reader.Values(8, reader.U16);
        var chain = new int[8];
        var ice = new int[8];
        var infinite = new int[8];
        for (var index = 0; index < 8; index++)
        {
            var value = attack[index];
            if (value > 99 && value < 250)
            {
                chain[index] = value - 100;
                attack[index] = 1;
            }
            else if (value == 99)
            {
                attack[index] = 0;
                ice[index] = 40;
            }
            else if (value >= 250)
            {
                attack[index] = value - 250;
                infinite[index] = 1;
            }
        }

        record.Numbers("attack", attack);
        record.Numbers("chain", chain);
        record.Numbers("iceticks", ice);
        record.Numbers("infinite", infinite);
        record.Numbers("del", delay);
        record.Numbers("sx", sx);
        record.Numbers("sy", sy);
        record.Numbers("bx", bx);
        record.Numbers("by", by);
        record.Numbers("sg", graphics);
        record.Number("acceleration", reader.S8());
        record.Number("accelerationx", reader.S8());
        foreach (var name in new[] { "circlesize", "sound", "trail", "shipblastfilter" })
        {
            record.Number(name, reader.U8());
        }

        return record.Finish();
    }

    private static string ReadWeapon(LegacyReader reader, int id)
    {
        var record = new Record("weapon", id);
        record.Text("name", reader.Name());
        record.Number("opnum", reader.U8());
        record.Numbers("op0", reader.Values(11, reader.U16));
        record.Numbers("op1", reader.Values(11, reader.U16));
        record.Number("cost", reader.U16());
        record.Number("itemgraphic", reader.U16());
        record.Number("poweruse", reader.U16());
        record.Number("position", WeaponPositions[id]);
        record.Number("bigshipgraphic", WeaponGraphics[id]);
        record.Number("bigshipgraphfrntx", WeaponFrontOffsets[id].X);
        record.Number("bigshipgraphfrnty", WeaponFrontOffsets[id].Y);
        record.Number("bigshipgraphrearx", WeaponRearOffsets[id].X);
        record.Number("bigshipgraphreary", WeaponRearOffsets[id].Y);
        record.Number("linkgunweapon", id > 0 && id <= LinkGuns.Length ? LinkGuns[id - 1] : 0);
        var charge = id > 0 && id <= ChargeGuns.Length ? ChargeGuns[id - 1] : 0;
        // Native output has six entries even though the game stores five.
        record.Numbers("chargegunweapons", Enumerable.Range(0, 6).Select(index => charge > 0 ? charge + index : 0));
        record.Number("probability", WeaponProbabilities[id]);
        return record.Finish();
    }

    private static string ReadSpecial(LegacyReader reader, int id)
    {
        var record = new Record("special", id);
        record.Text("name", reader.Name());
        record.Number("itemgraphic", reader.U16());
        record.Number("pwr", reader.U8());
        record.Number("stype", reader.U8());
        record.Number("wpn", reader.U16());
        return record.Finish();
    }

    private static string ReadGenerator(LegacyReader reader, int id)
    {
        var record = new Record("generator", id);
        record.Text("name", reader.Name());
        record.Number("itemgraphic", reader.U16());
        record.Number("power", reader.U8());
        record.Number("speed", reader.S8());
        record.Number("cost", reader.U16());
        record.Number("bigshipgraphic", GeneratorGraphics[id]);
        record.Number("bigshipgraphx", GeneratorOffsets[id].X);
        record.Number("bigshipgraphy", GeneratorOffsets[id].Y);
        record.Number("probability", GeneratorProbabilities[id]);
        return record.Finish();
    }

    private static string ReadShip(LegacyReader reader, int id)
    {
        var record = new Record("ship", id);
        record.Text("name", reader.Name());
        record.Number("shipgraphic", reader.U16());
        record.Number("itemgraphic", reader.U16());
        record.Number("ani", reader.U8());
        record.Number("spd", reader.S8());
        record.Number("dmg", reader.U8());
        record.Number("cost", reader.U16());
        record.Number("bigshipgraphic", reader.U8());
        record.Number("bigshipgraphx", ShipOffsets[id].X);
        record.Number("bigshipgraphy", ShipOffsets[id].Y);
        record.Number("probability", ShipProbabilities[id]);
        return record.Finish();
    }

    private static string ReadSidekick(LegacyReader reader, int id)
    {
        var record = new Record("sidekick", id);
        record.Text("name", reader.Name());
        var power = reader.U8();
        var graphic = reader.U16();
        var cost = reader.U16();
        var tracking = reader.U8();
        var option = reader.U8();
        var speed = reader.S8();
        var animation = reader.U8();
        var graphics = reader.Values(20, reader.U16);
        record.Number("itemgraphic", graphic);
        record.Number("pwr", power);
        record.Number("cost", cost);
        record.Number("tr", tracking);
        record.Number("option", option);
        record.Number("opspd", speed);
        record.Number("ani", animation);
        record.Numbers("gr0", graphics);
        for (var level = 1; level < 5; level++)
        {
            var increment = level;
            record.Numbers($"gr{level}", graphics.Select((value, index) => power >= increment && animation > index ? value + increment : 0));
        }

        record.Number("wport", reader.U8());
        record.Number("wpnum", reader.U16());
        record.Number("ammo", reader.U8());
        record.Number("stop", reader.U8() == 0 ? 0 : 1);
        record.Number("icongr", reader.U8());
        record.Number("position", SidekickPositions[id]);
        record.Number("probability", SidekickProbabilities[id]);
        return record.Finish();
    }

    private static string ReadShield(LegacyReader reader, int id)
    {
        var record = new Record("shield", id);
        record.Text("name", reader.Name());
        record.Number("tpwr", reader.U8());
        record.Number("mpwr", reader.U8());
        record.Number("itemgraphic", reader.U16());
        record.Number("cost", reader.U16());
        record.Number("probability", ShieldProbabilities[id]);
        return record.Finish();
    }

    private static string ReadEnemy(LegacyReader reader, int id)
    {
        var record = new Record("enemy", id);
        record.Number("ani", reader.U8());
        var turrets = reader.Values(3, reader.U8);
        record.Numbers("tur", turrets);
        record.Numbers("freq", reader.Values(3, reader.U8));
        foreach (var name in new[] { "xmove", "ymove", "xaccel", "yaccel", "xcaccel", "ycaccel" })
        {
            record.Number(name, reader.S8());
        }

        record.Number("startx", reader.S16());
        record.Number("starty", reader.S16());
        record.Number("startxc", reader.S8());
        record.Number("startyc", reader.S8());
        record.Number("armor", reader.U8());
        record.Number("esize", reader.U8());
        record.Numbers("egraphic", reader.Values(20, reader.U16));
        var explosion = reader.U8();
        record.Number("explosiontype", explosion >> 1);
        record.Number("animate", reader.U8());
        record.Number("shapebank", reader.U8());
        record.Number("xrev", reader.S8());
        record.Number("yrev", reader.S8());
        record.Number("dgr", reader.U16());
        record.Number("dlevel", reader.S8());
        record.Number("dani", reader.S8());
        record.Number("elaunchfreq", reader.U8());
        var launch = reader.U16();
        record.Number("elaunchtype", launch % 1000);
        record.Number("elaunchtypepos", launch % 1000);
        record.Number("elaunchspecial", launch / 1000);
        record.Number("value", reader.S16());
        record.Number("eenemydie", reader.U16());
        record.Numbers("eshotwait", turrets.Select(value => value == 252 ? 1 : value > 0 ? 20 : 255));
        record.Number("enemyground", explosion & 1);
        return record.Finish();
    }

    private sealed class LegacyReader(Stream stream)
    {
        public int U8()
        {
            Span<byte> value = stackalloc byte[1];
            BinaryFile.ReadExactly(stream, value);
            return value[0];
        }

        public int S8() => unchecked((sbyte)U8());

        public int U16()
        {
            Span<ushort> value = stackalloc ushort[1];
            BinaryFile.ReadUInt16(stream, value);
            return value[0];
        }

        public int S16() => unchecked((short)U16());
        public int[] Values(int count, Func<int> read) => Enumerable.Range(0, count).Select(_ => read()).ToArray();

        public string Name()
        {
            var length = Math.Min(U8(), 30);
            Span<byte> bytes = stackalloc byte[30];
            BinaryFile.ReadExactly(stream, bytes);
            var zero = bytes[..length].IndexOf((byte)0);
            return Encoding.Latin1.GetString(bytes[..(zero < 0 ? length : zero)]);
        }
    }

    private sealed class Record
    {
        private readonly StringBuilder _text = new();

        public Record(string type, int id)
        {
            _text.Append('[').Append(type).Append(':').Append(id.ToString(CultureInfo.InvariantCulture)).Append("]\n");
            Number("id", id);
        }

        public void Text(string name, string value) => _text.Append(name).Append('=').Append(value).Append('\n');
        public void Number(string name, int value) => Text(name, value.ToString(CultureInfo.InvariantCulture));
        public void Numbers(string name, IEnumerable<int> values) => Text(name, string.Join(" ", values.Select(value => value.ToString(CultureInfo.InvariantCulture))));
        public string Finish() => _text.Append('\n').ToString();
    }

    private static readonly int[] WeaponGraphics = [-1, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 21, 5, 13, -1, 14, 15, 0, 14, 9, 8, 2, 15, 0, 13, 0, 8, 8, 11, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 2, 1];
    private static readonly int[] WeaponProbabilities = [0, 30, 56, 28, 4, 7, 1, 23, 14, 2, 54, 58, 37, 26, 11, 33, 0, 30, 19, 47, 33, 7, 16, 1, 1, 1, 1, 9, 5, 12, 14, 1, 1, 1, 1, 1, 1, 1, 1, 1, 7, 2, 2];
    private static readonly int[] WeaponPositions = [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 0, 1, 1, 0, 0, 1, 0, 0, 1, 1, 0, 1, 0, 1, 0, 1, 1, 1, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0];
    private static readonly (int X, int Y)[] WeaponFrontOffsets =
    [
        (0, 0), (61, 48), (52, 41), (54, 36), (61, 53), (66, 41), (51, 35), (53, 39), (66, 53), (0, 0), (0, 0),
        (0, 0), (0, 0), (51, 35), (61, 53), (0, 0), (0, 0), (59, 38), (0, 0), (61, 48), (59, 38),
        (0, 0), (0, 0), (54, 36), (0, 0), (61, 48), (0, 0), (61, 48), (0, 0), (0, 0), (0, 0),
        (52, 41), (59, 38), (59, 38), (59, 38), (59, 38), (59, 38), (59, 38), (59, 38), (59, 38), (59, 38), (54, 36), (52, 41)
    ];
    private static readonly (int X, int Y)[] WeaponRearOffsets =
    [
        (0, 0), (0, 0), (0, 0), (0, 0), (0, 0), (0, 0), (0, 0), (0, 0), (0, 0), (41, 92), (27, 92),
        (49, 113), (43, 102), (0, 0), (51, 97), (39, 96), (0, 0), (0, 0), (41, 76), (0, 0), (0, 0),
        (27, 92), (41, 92), (0, 0), (41, 76), (0, 0), (39, 96), (0, 0), (41, 92), (41, 92), (43, 102),
        (41, 92), (41, 92), (41, 92), (41, 92), (41, 92), (41, 92), (41, 92), (41, 92), (41, 92), (41, 92), (0, 0), (0, 0)
    ];
    private static readonly int[] LinkGuns = [0, 0, 0, 0, 0, 0, 0, 0, 444, 445, 446, 447, 0, 448, 449, 0, 0, 0, 0, 0, 450, 451, 0, 506, 0, 564, 445, 446, 447, 448, 449, 445, 446, 447, 448, 449, 450, 451];
    private static readonly int[] ChargeGuns = [0, 0, 0, 0, 0, 0, 0, 0, 476, 458, 464, 482, 0, 488, 470, 0, 0, 0, 0, 0, 494, 500, 0, 528, 0, 558, 458, 458, 458, 458, 458, 458, 458, 458, 458, 458, 458, 458];
    private static readonly int[] GeneratorGraphics = [0, 16, 16, 17, 18, 19, 20];
    private static readonly int[] GeneratorProbabilities = [0, 61, 84, 86, 49, 25, 5];
    private static readonly (int X, int Y)[] GeneratorOffsets = [(0, 0), (62, 84), (62, 84), (64, 85), (67, 86), (66, 84), (63, 97)];
    private static readonly int[] ShipProbabilities = [0, 67, 5, 46, 47, 32, 16, 7, 1, 40, 1, 1, 1, 2];
    private static readonly (int X, int Y)[] ShipOffsets =
        [(0, 0), (35, 33), (35, 33), (31, 36), (31, 36), (31, 35), (31, 35), (31, 35), (31, 35), (35, 33), (31, 36), (35, 33), (31, 35), (31, 35)];
    private static readonly int[] SidekickPositions = [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 2, 0, 2, 0, 2, 2, 2, 2, 2, 0, 0];
    private static readonly int[] SidekickProbabilities = [0, 23, 40, 18, 9, 2, 21, 21, 30, 19, 21, 4, 4, 11, 12, 4, 17, 9, 5, 13, 7, 10, 2, 9, 1, 4, 2, 4, 2, 6, 2];
    private static readonly int[] ShieldProbabilities = [0, 16, 40, 44, 68, 61, 35, 14, 26, 5, 1];
}
