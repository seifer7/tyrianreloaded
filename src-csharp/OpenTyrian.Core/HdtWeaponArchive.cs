/*
 * OpenTyrian: A modern cross-platform port of Tyrian
 * Copyright (C) 2007-2013 The OpenTyrian Development Team
 * Distributed under the GNU General Public License, version 2 or later.
 * See COPYING for details.
 */

using System.Buffers.Binary;
using System.Globalization;
using System.Xml;
using System.Xml.Linq;

namespace OpenTyrian.Core;

public static class HdtWeaponArchive
{
    public const int WeaponCount = 781;
    public const int RecordSize = 0x50;
    private const int HeaderSize = 14;

    private static readonly string[] ScalarFields =
    [
        "acceleration", "accelerationx", "aim", "circleSize", "drain", "max", "multi",
        "shipBlastFilter", "shotRepeat", "sound", "trail", "tx", "ty", "weapAni"
    ];

    private static readonly string[] XmlFields =
    [
        "acceleration", "accelerationx", "aim", "circleSize", "drain", "max", "multi",
        "patterns", "shipBlastFilter", "shotRepeat", "sound", "trail", "tx", "ty", "weapAni"
    ];

    private static readonly string[] PatternFields = ["attack", "bx", "by", "del", "sg", "sx", "sy"];

    public static void ToXml(Stream hdt, TextWriter output)
    {
        ArgumentNullException.ThrowIfNull(hdt);
        ArgumentNullException.ThrowIfNull(output);
        var dataOffset = ReadDataOffset(hdt);
        hdt.Position = checked(dataOffset + HeaderSize);

        var newLine = output.NewLine;
        output.Write($"<?xml version=\"1.0\" ?>{newLine}<TyrianHDT>{newLine}");
        for (var index = 0; index < WeaponCount; index++)
        {
            var weapon = ReadWeapon(hdt);
            output.Write($"\t<weapon index=\"{index:X4}\">{newLine}");
            foreach (var field in XmlFields)
            {
                if (field != "patterns")
                {
                    output.Write($"\t\t<{field} value=\"{weapon.Scalars[field].ToString(CultureInfo.InvariantCulture)}\"/>{newLine}");
                    continue;
                }

                output.Write($"\t\t<patterns>{newLine}");
                foreach (var pattern in weapon.Patterns)
                {
                    output.Write("\t\t\t<entry");
                    foreach (var patternField in PatternFields)
                        output.Write($" {patternField}=\"{pattern[patternField].ToString(CultureInfo.InvariantCulture)}\"");
                    output.Write($"/>{newLine}");
                }
                output.Write($"\t\t</patterns>{newLine}");
            }
            output.Write($"\t</weapon>{newLine}");
        }
        output.Write($"</TyrianHDT>{newLine}");
    }

    public static void ToHdt(TextReader xml, Stream hdt)
    {
        ArgumentNullException.ThrowIfNull(xml);
        ArgumentNullException.ThrowIfNull(hdt);
        if (!hdt.CanWrite)
            throw new ArgumentException("HDT output must be writable.", nameof(hdt));
        var document = XDocument.Load(xml, LoadOptions.None);
        if (document.Root is null)
            throw new InvalidDataException("HDT weapon XML has no root element.");

        var dataOffset = ReadDataOffset(hdt);
        hdt.Position = checked(dataOffset + HeaderSize);
        foreach (var element in document.Root.Elements())
            WriteWeapon(hdt, ParseWeapon(element));
    }

    public static int Run(string[] arguments, TextWriter output, TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);
        if (arguments.Length != 3 || arguments[0] is not ("toxml" or "tohdt"))
        {
            error.WriteLine("Usage: OpenTyrian.HdtWeaponTool toxml path/to/tyrian.hdt output.xml");
            error.WriteLine("       OpenTyrian.HdtWeaponTool tohdt input.xml path/to/tyrian.hdt");
            return 1;
        }

        try
        {
            if (arguments[0] == "toxml")
            {
                using var hdt = File.OpenRead(arguments[1]);
                using var writer = new StreamWriter(arguments[2], false, new System.Text.UTF8Encoding(false));
                ToXml(hdt, writer);
            }
            else
            {
                using var reader = new StreamReader(arguments[1]);
                using var hdt = new FileStream(arguments[2], FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                ToHdt(reader, hdt);
            }
            return 0;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
            XmlException or FormatException or OverflowException or InvalidDataException or ArgumentException)
        {
            error.WriteLine($"error: {exception.Message}");
            return 1;
        }
    }

    private static long ReadDataOffset(Stream hdt)
    {
        if (!hdt.CanRead || !hdt.CanSeek)
            throw new ArgumentException("HDT input must be readable and seekable.", nameof(hdt));
        hdt.Position = 0;
        Span<byte> offsetBytes = stackalloc byte[sizeof(int)];
        hdt.ReadExactly(offsetBytes);
        var offset = BinaryPrimitives.ReadInt32LittleEndian(offsetBytes);
        if (offset < 0)
            throw new InvalidDataException("HDT data offset cannot be negative.");
        var position = checked((long)offset + HeaderSize);
        if (position > hdt.Length)
            throw new EndOfStreamException("HDT weapon header extends beyond the input file.");
        return offset;
    }

    private static WeaponRecord ReadWeapon(Stream hdt)
    {
        var bytes = new byte[RecordSize];
        hdt.ReadExactly(bytes);
        var cursor = 0;
        var weapon = new WeaponRecord();
        weapon.Scalars["drain"] = ReadUInt16();
        weapon.Scalars["shotRepeat"] = ReadByte();
        weapon.Scalars["multi"] = ReadByte();
        weapon.Scalars["weapAni"] = ReadUInt16();
        weapon.Scalars["max"] = ReadByte();
        weapon.Scalars["tx"] = ReadByte();
        weapon.Scalars["ty"] = ReadByte();
        weapon.Scalars["aim"] = ReadByte();
        ReadPatterns("attack", signed: false);
        ReadPatterns("del", signed: false);
        ReadPatterns("sx", signed: true);
        ReadPatterns("sy", signed: true);
        ReadPatterns("bx", signed: true);
        ReadPatterns("by", signed: true);
        ReadPatterns("sg", word: true);
        weapon.Scalars["acceleration"] = ReadSignedByte();
        weapon.Scalars["accelerationx"] = ReadSignedByte();
        weapon.Scalars["circleSize"] = ReadByte();
        weapon.Scalars["sound"] = ReadByte();
        weapon.Scalars["trail"] = ReadByte();
        weapon.Scalars["shipBlastFilter"] = ReadByte();
        return weapon;

        byte ReadByte() => bytes[cursor++];
        short ReadSignedByte() => unchecked((sbyte)ReadByte());
        ushort ReadUInt16()
        {
            var value = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(cursor));
            cursor += sizeof(ushort);
            return value;
        }
        void ReadPatterns(string field, bool signed = false, bool word = false)
        {
            for (var index = 0; index < 8; index++)
                weapon.Patterns[index][field] = word ? ReadUInt16() : signed ? ReadSignedByte() : ReadByte();
        }
    }

    private static WeaponRecord ParseWeapon(XElement element)
    {
        var weapon = new WeaponRecord();
        foreach (var field in ScalarFields)
        {
            var value = element.Element(field)?.Attribute("value")?.Value
                ?? throw new InvalidDataException($"Weapon field '{field}' is missing.");
            weapon.Scalars[field] = ParseInteger(value, field);
        }

        var patterns = element.Element("patterns")
            ?? throw new InvalidDataException("Weapon patterns are missing.");
        var entries = patterns.Elements("entry").ToArray();
        if (entries.Length != 8)
            throw new InvalidDataException("Each weapon must contain exactly eight pattern entries.");
        for (var index = 0; index < entries.Length; index++)
            foreach (var field in PatternFields)
            {
                var value = entries[index].Attribute(field)?.Value
                    ?? throw new InvalidDataException($"Pattern {index} is missing '{field}'.");
                weapon.Patterns[index][field] = ParseInteger(value, field);
            }
        return weapon;
    }

    private static int ParseInteger(string value, string field)
        => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number)
            ? number : throw new InvalidDataException($"Weapon value '{field}' is not an integer.");

    private static void WriteWeapon(Stream hdt, WeaponRecord weapon)
    {
        var bytes = new byte[RecordSize];
        var cursor = 0;
        WriteUInt16(weapon.Scalars["drain"], "drain");
        WriteByte(weapon.Scalars["shotRepeat"], "shotRepeat");
        WriteByte(weapon.Scalars["multi"], "multi");
        WriteUInt16(weapon.Scalars["weapAni"], "weapAni");
        WriteByte(weapon.Scalars["max"], "max");
        WriteByte(weapon.Scalars["tx"], "tx");
        WriteByte(weapon.Scalars["ty"], "ty");
        WriteByte(weapon.Scalars["aim"], "aim");
        WritePatterns("attack", signed: false);
        WritePatterns("del", signed: false);
        WritePatterns("sx", signed: true);
        WritePatterns("sy", signed: true);
        WritePatterns("bx", signed: true);
        WritePatterns("by", signed: true);
        WritePatterns("sg", word: true);
        WriteByte(weapon.Scalars["acceleration"], "acceleration", signed: true);
        WriteByte(weapon.Scalars["accelerationx"], "accelerationx", signed: true);
        WriteByte(weapon.Scalars["circleSize"], "circleSize");
        WriteByte(weapon.Scalars["sound"], "sound");
        WriteByte(weapon.Scalars["trail"], "trail");
        WriteByte(weapon.Scalars["shipBlastFilter"], "shipBlastFilter");
        if (cursor != RecordSize)
            throw new InvalidOperationException("HDT weapon record layout does not match its native size.");
        hdt.Write(bytes);

        void WriteByte(int value, string field, bool signed = false)
        {
            var minimum = signed ? (int)sbyte.MinValue : byte.MinValue;
            var maximum = signed ? (int)sbyte.MaxValue : byte.MaxValue;
            if (value < minimum || value > maximum)
                throw new InvalidDataException($"Weapon value '{field}' is outside its native byte range.");
            bytes[cursor++] = unchecked((byte)value);
        }
        void WriteUInt16(int value, string field)
        {
            if (value < ushort.MinValue || value > ushort.MaxValue)
                throw new InvalidDataException($"Weapon value '{field}' is outside its native word range.");
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(cursor), (ushort)value);
            cursor += sizeof(ushort);
        }
        void WritePatterns(string field, bool signed = false, bool word = false)
        {
            for (var index = 0; index < 8; index++)
            {
                var value = weapon.Patterns[index][field];
                if (word) WriteUInt16(value, field);
                else WriteByte(value, field, signed);
            }
        }
    }

    private sealed class WeaponRecord
    {
        public Dictionary<string, int> Scalars { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, int>[] Patterns { get; } =
            Enumerable.Range(0, 8).Select(_ => new Dictionary<string, int>(StringComparer.Ordinal)).ToArray();
    }
}
