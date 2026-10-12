using System.Text;
using System.Diagnostics;
using OpenTyrian.Core;

namespace OpenTyrian.Core.Tests;

internal static class ItemDefinitionTests
{
    public static void Run(string? executable = null)
    {
        var definitions = new ItemDefinitions();
        definitions.ParseBundle(Encoding.Latin1.GetBytes("""
            # comment
            [shot:later]
            id=later
            drain=-2147483648 +2147483647
            chain=0 later
            [shot:0]
            drain=2
            [weapon:0]
            name=
            op0=0 later
            [sidekick:0]
            wport=0
            """), "base.tyrr");
        definitions.ParseBundle([0xEF, 0xBB, 0xBF, .. "[shot:later]\r\ndrain=7\r\n"u8], "mod.tyrr");
        Check(definitions.Records(ItemType.Shot).Count == 2 && definitions.Records(ItemType.Shot)[0].Fields.Count == 1 &&
            definitions.Records(ItemType.Shot)[0].Fields[0].Value == "7", "Whole-record replacement or insertion order differs.");
        definitions.ReserveZeroSlots();
        Check(definitions.Records(ItemType.Shot)[0].Id == "0" && definitions.TryFind(ItemType.Shot, "later", out var index) && index == 1,
            "Zero-slot relocation or ID lookup differs.");
        Check(definitions.TryFind(ItemType.WeaponRear, "0", out index) && index == 0 &&
            definitions.TryFind(ItemType.SidekickRight, "0", out index) && index == 0, "Item-type aliases differ.");
        Check(ItemDefinitions.ValidId("a-_\x80") && !ItemDefinitions.ValidId("a b") && !ItemDefinitions.ValidId("a:b") &&
            !ItemDefinitions.ValidId("12345678901") && ItemDefinitions.Trim("\v\t abc \r\f") == "abc", "Identifier or ASCII whitespace differs.");
        foreach (var (input, line, message) in new[]
        {
            ("id=0", 1, "field appears before an object header"),
            ("[unknown:0]", 1, "unknown object type"),
            ("[shot:]", 1, "invalid object ID"),
            ("[shot:0", 1, "expected [type:id] header"),
            ("[shot:0]\nwrong=2", 2, "unknown field"),
            ("[shot:0]\ndrain=2147483648", 2, "invalid integer"),
            ("[shot:0]\ndrain=0x10", 2, "invalid integer"),
            ("[shot:0]\ndrain=1x", 2, "invalid integer"),
            ("[shot:0]\ndrain=", 2, "requires a value"),
            ("[shot:0]\nchain=has#hash", 2, "invalid reference ID"),
            ("[shot:0]\nchain=12345678901", 2, "reference ID exceeds"),
            ("[shot:0]\nid=1", 2, "id field does not match"),
            ("[shot:0]\ndrain", 2, "expected key=value"),
            ("[ship:0]\nname=" + new string('a', 31), 2, "name exceeds"),
            ("[shot:0]\0", 0, "file contains a NUL byte")
        })
        {
            try
            {
                new ItemDefinitions().ParseBundle(Encoding.Latin1.GetBytes(input), "fixture.tyrr");
                throw new InvalidOperationException("Invalid item bundle accepted.");
            }

            catch (InvalidDataException exception)
            {
                Check(exception.Message.Contains($"fixture.tyrr:{line}:", StringComparison.Ordinal) &&
                    exception.Message.Contains(message, StringComparison.Ordinal), "Item error location or diagnostic differs.");
            }
        }

        TestModLoading();
        if (executable is not null)
        {
            CompareNative(executable);
        }
    }

    private static void CompareNative(string executable)
    {
        var path = Path.Combine(Path.GetTempPath(), $"opentyrian-items-{Guid.NewGuid():N}.tyrr");
        try
        {
            foreach (var fixture in new[]
            {
                    "[shot:later]\r\nid=later\nchain=0 later\n[shot:0]\ndrain=-2147483648 +2147483647\n[shot:later]\nattack=1 2\t3",
                    "[ship:0]\nname=Byte\x80\xFF\n[weapon:0]\nop0=0 later\n[sidekick:0]\ng r=1",
                    "[shot:0]\nid=1", "[shot:0]\ndrain=1x", "[shot:0]\ndrain=-2147483649",
                    "[shot:0]\nchain=a#b", "[shot:0]\ndrain=", "[unknown:0]",
                    "[shot:0]\ndrain=00000000000000000000000000001",
                    "[ship:0]\nname=\n[enemy:e]\nvalue=-20\nelaunchtype=e",
                    "[shot:12345678901]", "[shot:0]\0", "[shot:0]\nchain=a b c",
                    "\xEF\xBB\xBF[shot:0]\n# comment\nid=0\n"
                })
            {
                File.WriteAllBytes(path, Encoding.Latin1.GetBytes(fixture));
                var managed = new ItemDefinitions();
                var output = new StringBuilder();
                string errors = "";
                var code = 0;
                try
                {
                    managed.LoadBundle(path);
                    foreach (var type in new[] { ItemType.Shot, ItemType.Ship, ItemType.WeaponFront, ItemType.Generator,
                            ItemType.Shield, ItemType.SidekickLeft, ItemType.Special, ItemType.Enemy })
                    {
                        foreach (var record in managed.Records(type))
                        {
                            output.Append($"{(int)type}:{record.Id}:{record.Line}\n");
                            foreach (var field in record.Fields)
                            {
                                output.Append($"{field.Line}:{field.Key}={field.Value}\n");
                            }
                        }
                    }
                }
                catch (InvalidDataException exception)
                {
                    errors = exception.Message + "\n";
                    code = 1;
                }

                var start = new ProcessStartInfo(executable)
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = Encoding.Latin1,
                    StandardErrorEncoding = Encoding.Latin1
                };
                start.ArgumentList.Add(path);
                using var process = Process.Start(start) ?? throw new InvalidOperationException("Cannot start item parser oracle.");
                var nativeOutput = process.StandardOutput.ReadToEndAsync();
                var nativeErrors = process.StandardError.ReadToEndAsync();
                if (!process.WaitForExit(30000))
                {
                    process.Kill(entireProcessTree: true);
                    throw new TimeoutException("Item parser oracle timed out.");
                }

                Check(process.ExitCode == code && Normalize(nativeOutput.GetAwaiter().GetResult()) == output.ToString() &&
                    Normalize(nativeErrors.GetAwaiter().GetResult()) == errors, $"Native item parser output differs for {fixture.Replace("\n", " / ", StringComparison.Ordinal)}.");
            }
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string Normalize(string value) => value.Replace("\r\n", "\n", StringComparison.Ordinal);

    private static void TestModLoading()
    {
        var root = Path.Combine(Path.GetTempPath(), $"opentyrian-items-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var paths = new List<string>();
        try
        {
            var types = new[] { "shot", "ship", "weapon", "generator", "shield", "sidekick", "special", "enemy" };
            for (var file = 0; file < ItemDefinitions.BaseFiles.Count; file++)
            {
                var path = Path.Combine(root, ItemDefinitions.BaseFiles[file].Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(path) ?? throw new InvalidOperationException());
                File.WriteAllText(path, $"[{types[file]}:0]\nid=0\n", Encoding.Latin1);
                paths.Add(path);
            }

            foreach (var (name, value) in new[] { ("z.tyrr", 8), ("a.TYRR", 3) })
            {
                var path = Path.Combine(root, "data", "shots", name);
                File.WriteAllText(path, $"[shot:0]\ndrain={value}\n", Encoding.Latin1);
                paths.Add(path);
            }

            var loaded = new ItemDefinitions();
            loaded.LoadAll(root);
            loaded.ReserveZeroSlots();
            Check(loaded.Records(ItemType.Shot)[0].Fields[0].Value == "8", "Additional bundles not applied in sorted path order.");
            Check(ItemDefinitions.ComparePaths("A.tyrr", "a.tyrr") < 0 && ItemDefinitions.ComparePaths("z", "AA") > 0, "Native path sort tie-break differs.");
        }
        finally
        {
            foreach (var path in paths)
            {
                File.Delete(path);
            }

            foreach (var directory in new[] { "items", "enemy", "shots" })
            {
                Directory.Delete(Path.Combine(root, "data", directory));
            }

            Directory.Delete(Path.Combine(root, "data"));
            Directory.Delete(root);
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
