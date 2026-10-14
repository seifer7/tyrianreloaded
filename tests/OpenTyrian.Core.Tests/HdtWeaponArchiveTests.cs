using System.Buffers.Binary;
using System.Xml.Linq;
using OpenTyrian.Core;

namespace OpenTyrian.Core.Tests;

internal static class HdtWeaponArchiveTests
{
    public static void Run()
    {
        const int offset = 32;
        var original = CreateArchive(offset);
        using var hdt = new MemoryStream(original, writable: false);
        using var xml = new StringWriter();
        HdtWeaponArchive.ToXml(hdt, xml);
        var text = xml.ToString();
        var document = XDocument.Parse(text);
        var weapons = document.Root!.Elements("weapon").ToArray();
        Check(weapons.Length == HdtWeaponArchive.WeaponCount &&
            (string?)weapons[0].Attribute("index") == "0000" &&
            (string?)weapons[^1].Attribute("index") == "030C",
            "HDT XML did not preserve the native weapon count and indexed record order.");
        Check(text.StartsWith($"<?xml version=\"1.0\" ?>{Environment.NewLine}<TyrianHDT>{Environment.NewLine}", StringComparison.Ordinal) &&
            text.Contains("<acceleration value=\"-128\"/>", StringComparison.Ordinal) &&
            text.Contains("<entry attack=\"255\" bx=\"-128\" by=\"127\" del=\"254\" sg=\"65535\" sx=\"-127\" sy=\"126\"/>",
                StringComparison.Ordinal),
            "HDT XML formatting, signed fields, or pattern field ordering differs from the native utility.");

        using var output = new MemoryStream((byte[])original.Clone(), writable: true);
        HdtWeaponArchive.ToHdt(new StringReader(text), output);
        var roundTripped = output.ToArray();
        var mismatch = roundTripped.AsSpan().SequenceEqual(original)
            ? -1
            : Enumerable.Range(0, original.Length).First(index => roundTripped[index] != original[index]);
        if (mismatch != -1)
            throw new InvalidOperationException(
                $"HDT XML round trip changed byte {mismatch} ({original[mismatch]} to {roundTripped[mismatch]}) or damaged the original header/trailing data.");

        var oneWeaponXml = "<TyrianHDT>" + weapons[0] + "</TyrianHDT>";
        var shortArchive = CreateArchive(offset);
        using var partial = new MemoryStream(shortArchive, writable: true);
        HdtWeaponArchive.ToHdt(new StringReader(oneWeaponXml), partial);
        Check(partial.ToArray().AsSpan(offset + 14 + HdtWeaponArchive.RecordSize)
            .SequenceEqual(shortArchive.AsSpan(offset + 14 + HdtWeaponArchive.RecordSize)),
            "HDT conversion truncated or overwrote bytes after the XML weapon records.");

        using var malformed = new MemoryStream((byte[])original.Clone(), writable: true);
        malformed.SetLength(offset + 14 + HdtWeaponArchive.RecordSize - 1);
        try
        {
            HdtWeaponArchive.ToXml(malformed, TextWriter.Null);
            throw new InvalidOperationException("Truncated HDT weapon data was accepted.");
        }
        catch (EndOfStreamException) { }

        using var invalidValueArchive = new MemoryStream((byte[])original.Clone(), writable: true);
        var drain = weapons[0].Element("drain")!.Attribute("value")!.Value;
        var invalidXml = text.Replace($"<drain value=\"{drain}\"/>", "<drain value=\"65536\"/>", StringComparison.Ordinal);
        try
        {
            HdtWeaponArchive.ToHdt(new StringReader(invalidXml), invalidValueArchive);
            throw new InvalidOperationException("Out-of-range HDT weapon value was accepted.");
        }
        catch (InvalidDataException) { }

        using var usageOutput = new StringWriter();
        using var usageError = new StringWriter();
        Check(HdtWeaponArchive.Run([], usageOutput, usageError) == 1 &&
            usageError.ToString().Contains("toxml", StringComparison.Ordinal),
            "HDT utility did not report its command usage.");

        var directory = Path.Combine(Path.GetTempPath(), $"opentyrian-hdt-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var inputPath = Path.Combine(directory, "input.hdt");
            var xmlPath = Path.Combine(directory, "weapons.xml");
            File.WriteAllBytes(inputPath, original);
            using var cliOutput = new StringWriter();
            using var cliError = new StringWriter();
            Check(HdtWeaponArchive.Run(["toxml", inputPath, xmlPath], cliOutput, cliError) == 0 &&
                File.Exists(xmlPath),
                "HDT utility did not export XML through its file-based CLI.");
            Check(HdtWeaponArchive.Run(["tohdt", xmlPath, inputPath], cliOutput, cliError) == 0 &&
                File.ReadAllBytes(inputPath).SequenceEqual(original),
                "HDT utility did not round-trip the archive through its file-based CLI.");
            Check(HdtWeaponArchive.Run(["toxml", Path.Combine(directory, "missing.hdt"), xmlPath],
                cliOutput, cliError) == 1 && cliError.ToString().Contains("error:", StringComparison.Ordinal),
                "HDT utility did not report a missing input file.");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static byte[] CreateArchive(int offset)
    {
        var suffix = new byte[] { 0xA5, 0x5A, 0xC3 };
        var result = new byte[offset + 14 + HdtWeaponArchive.WeaponCount * HdtWeaponArchive.RecordSize + suffix.Length];
        BinaryPrimitives.WriteInt32LittleEndian(result, offset);
        for (var index = 4; index < offset + 14; index++)
            result[index] = unchecked((byte)(index * 19 + 7));
        for (var weapon = 0; weapon < HdtWeaponArchive.WeaponCount; weapon++)
        {
            var record = result.AsSpan(offset + 14 + weapon * HdtWeaponArchive.RecordSize, HdtWeaponArchive.RecordSize);
            for (var index = 0; index < record.Length; index++)
                record[index] = unchecked((byte)(weapon * 31 + index * 17 + 3));
        }

        suffix.CopyTo(result, result.Length - suffix.Length);
        var firstRecord = result.AsSpan(offset + 14, HdtWeaponArchive.RecordSize);
        firstRecord[74] = 0x80;
        firstRecord[75] = 0x7F;
        firstRecord[10] = 0xFF;
        firstRecord[11] = 0xFE;
        firstRecord[12] = 0x80;
        firstRecord[13] = 0x7F;
        firstRecord[14] = 0xFE;
        firstRecord[15] = 0xFF;
        BinaryPrimitives.WriteUInt16LittleEndian(firstRecord[16..], ushort.MaxValue);
        firstRecord[18] = 0xFE;
        firstRecord[26] = 0x81;
        firstRecord[34] = 0x7E;
        firstRecord[42] = 0x80;
        firstRecord[50] = 0x7F;
        BinaryPrimitives.WriteUInt16LittleEndian(firstRecord[58..], ushort.MaxValue);
        return result;
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
