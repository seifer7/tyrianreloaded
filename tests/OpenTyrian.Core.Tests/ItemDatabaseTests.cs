using System.Collections;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using OpenTyrian.Core;

namespace OpenTyrian.Core.Tests;

internal static class ItemDatabaseTests
{
    private static readonly (ItemType Type, Type Model, string Ids)[] Types =
    [
        (ItemType.Shot, typeof(ShotItem), "late 0 147 148"),
        (ItemType.Ship, typeof(ShipItem), "late 0 12 11 8 13 2 1"),
        (ItemType.WeaponFront, typeof(WeaponItem), "late 0 39 23 25 36 1 16 24 26 37 15"),
        (ItemType.Generator, typeof(GeneratorItem), "late 0 2"),
        (ItemType.Shield, typeof(ShieldItem), "late 0 2 4"),
        (ItemType.SidekickLeft, typeof(SidekickItem), "late 0 24"),
        (ItemType.Special, typeof(SpecialItem), "late 0 13"),
        (ItemType.Enemy, typeof(EnemyItem), "late 0 560 533 534")
    ];

    public static void Run(string? executable = null)
    {
        var fixture = Fixture(0);
        var database = Parse(fixture);
        Check(database.Shots[0].Id == "0" && database.Shots[1].Id == "late", "Zero-slot ID order differs.");
        Check(database.Index(ItemType.WeaponRear, "late") == database.Index(ItemType.WeaponFront, "late") &&
            database.Index(ItemType.SidekickRight, "24") == database.Index(ItemType.SidekickLeft, "24") &&
            database.Index(ItemType.Ship, -99) == -1, "Runtime lookup and aliases differ.");
        Check(database.Shots[1].Chain.All(index => index is 0 or 1) && database.Weapons[1].Op[0][0] == 1 &&
            database.Sidekicks[1].Wpnum == 1 && database.Enemies[1].ELaunchType == 1, "Forward references differ.");
        Check(database.Sidekicks[1].Stop && !database.Enemies[1].EnemyGround, "Native boolean assignment differs.");
        Check(database.KnownIndices.Count == Enum.GetValues<KnownItem>().Length &&
            database.KnownIndices[(int)KnownItem.Shot147] == database.Index(ItemType.Shot, 147), "Required item indices differ.");
        Check(!ItemDatabase.Contains([0, 1], 257), "Byte availability incorrectly truncates searched index.");
        Span<byte> available = stackalloc byte[10];
        Check(!database.AddRandomShopItem(ItemType.Special, available, 0), "Unsupported shop type accepted.");
        Check(database.TryFind(ItemType.Shot, 147, out var shotIndex) && shotIndex == database.Index(ItemType.Shot, 147),
            "Integer lookup differs.");
        TestFileLoading(fixture);
        foreach (var invalid in new[]
        {
            "[shot:late]\nchain=missing", "[weapon:late]\nop0=missing",
            "[sidekick:late]\nwpnum=missing", "[enemy:late]\nelaunchtype=missing",
            "[shot:late]\nchain=a\vb", "[special:late]\nwpn=0 late"
        })
        {
            try
            {
                Parse(fixture + invalid + "\n");
                throw new InvalidOperationException("Missing required item reference accepted.");
            }
            catch (InvalidDataException exception)
            {
                Check(exception.Message.Contains("required extracted item ID", StringComparison.Ordinal),
                    "Required-reference diagnostic differs.");
            }
        }

        if (executable is not null)
        {
            foreach (var variant in Enumerable.Range(0, 6))
                CompareNative(executable, Fixture(variant));
            CompareNative(executable, fixture + string.Concat(Enumerable.Range(0, 260)
                .Select(index => $"[ship:wide{index}]\nprobability=100\n")));
            foreach (var invalid in new[]
            {
                "[shot:late]\nchain=missing", "[weapon:late]\nop0=missing", "[sidekick:late]\nwpnum=missing",
                "[enemy:late]\nelaunchtype=missing", "[shot:late]\nchain=a\vb", "[special:late]\nwpn=0 late"
            })
                CompareNative(executable, fixture + invalid + "\n");
            CompareNative(executable, fixture.Replace("[shot:0]\nid=0", "[shot:zero]\nid=zero", StringComparison.Ordinal));
            CompareNative(executable, Regex.Replace(fixture, @"(?m)^\[generator:[^\]]+\]\n(?:[^\[]*)", ""));
            CompareNative(executable, fixture.Replace("[generator:2]\nid=2", "[generator:absent]\nid=absent", StringComparison.Ordinal));
        }
    }

    private static void TestFileLoading(string fixture)
    {
        var root = Path.Combine(Path.GetTempPath(), $"opentyrian-database-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var paths = new List<string>();
        try
        {
            var headers = Types.Select(entry => entry.Type switch
            {
                ItemType.WeaponFront => "weapon",
                ItemType.SidekickLeft => "sidekick",
                _ => entry.Type.ToString().ToLowerInvariant()
            }).ToArray();
            for (var i = 0; i < ItemDefinitions.BaseFiles.Count; i++)
            {
                var path = Path.Combine(root, ItemDefinitions.BaseFiles[i].Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(path) ?? throw new InvalidOperationException());
                var contents = string.Concat(Regex.Matches(fixture, $@"(?m)^\[{headers[i]}:[^\]]+\]\n(?:[^\[]*)")
                    .Select(match => match.Value));
                File.WriteAllText(path, contents, Encoding.Latin1);
                paths.Add(path);
            }

            var mod = Path.Combine(root, "data", "shots", "override.tyrr");
            File.WriteAllText(mod, "[shot:late]\ndrain=42\n", Encoding.Latin1);
            paths.Add(mod);
            var loaded = ItemDatabase.Load(root);
            Check(loaded.Shots[1].Drain == 42 && loaded.Shots[1].Attack.All(value => value == 0) &&
                loaded.Weapons[1].Op[0][0] == 1, "Integrated file/mod loading or whole-record replacement differs.");
        }
        finally
        {
            foreach (var path in paths) File.Delete(path);
            foreach (var directory in new[] { "items", "enemy", "shots" }) Directory.Delete(Path.Combine(root, "data", directory));
            Directory.Delete(Path.Combine(root, "data"));
            Directory.Delete(root);
        }
    }

    private static ItemDatabase Parse(string fixture, string path = "fixture.tyrr")
    {
        var definitions = new ItemDefinitions();
        definitions.ParseBundle(Encoding.Latin1.GetBytes(fixture), path);
        return ItemDatabase.FromDefinitions(definitions);
    }

    internal static string Fixture(int variant)
    {
        var values = new[] { int.MinValue, int.MaxValue, -129, 128, 256, 65536 };
        var text = new StringBuilder();
        foreach (var (type, model, ids) in Types)
        {
            var name = type switch { ItemType.WeaponFront => "weapon", ItemType.SidekickLeft => "sidekick", _ => type.ToString().ToLowerInvariant() };
            foreach (var id in ids.Split(' '))
            {
                text.Append($"[{name}:{id}]\nid={id}\n");
                var sample = Activator.CreateInstance(model) ?? throw new InvalidOperationException();
                foreach (var property in model.GetProperties())
                {
                    var key = property.Name.ToLowerInvariant();
                    if (key is "id" or "chain" or "linkgunweapon" or "chargegunweapons" or "wport" or "elaunchtype" or "eenemydie")
                        continue;
                    if (key == "name") { text.Append("name=Byte\x80\xFF\n"); continue; }
                    var fieldValue = property.GetValue(sample);
                    var reference = key.EndsWith("id", StringComparison.Ordinal) || key.EndsWith("ids", StringComparison.Ordinal) ||
                        key is "wpnum" or "wpn";
                    var field = key switch
                    {
                        "chainids" => "chain",
                        "chargegunweaponids" => "chargegunweapons",
                        "linkgunweaponid" => "linkgunweapon",
                        "wportid" => "wport",
                        "elaunchtypeid" => "elaunchtype",
                        "eenemydieid" => "eenemydie",
                        _ => key
                    };
                    if (fieldValue is Array array)
                    {
                        if (key is "op" or "gr")
                        {
                            for (var row = 0; row < array.Length; row++)
                            {
                                var inner = (Array)(array.GetValue(row) ?? throw new InvalidOperationException());
                                AppendArray(field + row, inner.Length, key == "op");
                            }
                        }
                        else AppendArray(field, array.Length, reference);
                    }
                    else
                    {
                        var scalar = reference ? "late" : values[variant].ToString(CultureInfo.InvariantCulture);
                        if (key == "probability") scalar = (variant % 3 == 0 ? 0 : variant % 3 == 1 ? 55 : 100).ToString(CultureInfo.InvariantCulture);
                        if (key == "position") scalar = ((variant + int.Parse(id == "late" ? "1" : id, CultureInfo.InvariantCulture)) % 3).ToString(CultureInfo.InvariantCulture);
                        text.Append($"{field}={scalar}\n");
                    }

                    void AppendArray(string arrayKey, int capacity, bool isReference)
                    {
                        var tokens = Enumerable.Range(0, capacity + 2)
                            .Select(index => isReference ? index % 2 == 0 ? "0" : "late" :
                                values[(index + variant) % values.Length].ToString(CultureInfo.InvariantCulture));
                        text.Append($"{arrayKey}={string.Join(' ', tokens)}\n");
                        text.Append($"{arrayKey}={(isReference ? "late 0" : "3 -3")}\n");
                    }
                }
            }
        }

        return text.ToString();
    }

    private static List<string> Dump(ItemDatabase database)
    {
        var lines = new List<string>();
        foreach (var (type, _, _) in Types)
        {
            var items = database.Items(type);
            for (var i = 0; i < items.Count; i++)
            {
                foreach (var property in items[i].GetType().GetProperties())
                {
                    lines.Add($"{(int)type}:{i}:{property.Name.ToLowerInvariant()}={Format(property.GetValue(items[i]))}");
                }
            }
        }

        for (var i = 0; i < database.KnownIndices.Count; i++)
            lines.Add($"known:{i}={database.KnownIndices[i]}");
        MersenneTwister.Seed(1234);
        for (var type = 0; type < (int)ItemType.Count; type++)
        {
            var available = new byte[10];
            for (var turn = 0; turn < 20; turn++)
            {
                var result = database.AddRandomShopItem((ItemType)type, available, turn % 10);
                lines.Add($"shop:{type}:{turn}={(result ? 1 : 0)},{string.Join(',', available)}");
            }
        }

        return lines;
    }

    private static string Format(object? value) => value switch
    {
        string text => Convert.ToHexString(Encoding.Latin1.GetBytes(text)),
        bool flag => flag ? "1" : "0",
        Array array when array.GetType().GetElementType()?.IsArray == true =>
            string.Join(',', array.Cast<Array>().Select(Format)),
        IEnumerable sequence => string.Join(',', sequence.Cast<object>().Select(Format)),
        IFormattable number => number.ToString(null, CultureInfo.InvariantCulture),
        _ => throw new InvalidOperationException("Unexpected item model property type.")
    };

    private static void CompareNative(string executable, string fixture)
    {
        var path = Path.Combine(Path.GetTempPath(), $"opentyrian-typed-items-{Guid.NewGuid():N}.tyrr");
        try
        {
            File.WriteAllBytes(path, Encoding.Latin1.GetBytes(fixture));
            var output = new List<string>();
            var errors = "";
            var code = 0;
            try { output = Dump(Parse(fixture, path)); }
            catch (InvalidDataException exception) { errors = exception.Message + "\n"; code = 1; }
            var start = new ProcessStartInfo(executable)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.Latin1,
                StandardErrorEncoding = Encoding.Latin1
            };
            start.ArgumentList.Add("--typed");
            start.ArgumentList.Add(path);
            using var process = Process.Start(start) ?? throw new InvalidOperationException("Cannot start typed item oracle.");
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(30000))
            {
                process.Kill(entireProcessTree: true);
                throw new TimeoutException("Typed item oracle timed out.");
            }

            var nativeLines = stdout.GetAwaiter().GetResult().Replace("\r\n", "\n", StringComparison.Ordinal)
                .Split('\n', StringSplitOptions.RemoveEmptyEntries).Order(StringComparer.Ordinal).ToArray();
            var managedLines = output.Order(StringComparer.Ordinal).ToArray();
            var nativeErrors = stderr.GetAwaiter().GetResult().Replace("\r\n", "\n", StringComparison.Ordinal);
            Check(code == process.ExitCode && errors == nativeErrors, $"Native typed item diagnostics differ: {errors} / {nativeErrors}");
            var differences = nativeLines.Except(managedLines).Concat(managedLines.Except(nativeLines)).Take(8);
            Check(nativeLines.SequenceEqual(managedLines), $"Native typed item values differ: {string.Join(" / ", differences)}");
        }
        finally { File.Delete(path); }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
