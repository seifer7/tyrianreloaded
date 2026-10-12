using System.Buffers.Binary;
using System.Diagnostics;
using System.Text;
using OpenTyrian.Core;

namespace OpenTyrian.Core.Tests;

internal static class ExtractorTests
{
    private static readonly (string Category, string Type, int Count, int Size, bool Named)[] Layout =
    [
        ("data/shots/shots.tyrr", "shot", 781, 80, false),
        ("data/items/weapons.tyrr", "weapon", 43, 82, true),
        ("data/items/specials.tyrr", "special", 47, 37, true),
        ("data/items/generators.tyrr", "generator", 7, 37, true),
        ("data/items/ships.tyrr", "ship", 14, 41, true),
        ("data/items/sidekicks.tyrr", "sidekick", 31, 86, true),
        ("data/items/shields.tyrr", "shield", 11, 37, true),
        ("data/enemy/enemies.tyrr", "enemy", 851, 77, false)
    ];

    public static void Run(string? nativeExecutable = null)
    {
        var root = Path.Combine(Path.GetTempPath(), $"opentyrian-extractor-{Guid.NewGuid():N}");
        var assets = Path.Combine(root, "assets");
        var managed = Path.Combine(root, "managed");
        var native = Path.Combine(root, "native");
        Directory.CreateDirectory(assets);
        Directory.CreateDirectory(managed);
        Directory.CreateDirectory(native);
        try
        {
            var fixture = BuildFixture();
            File.WriteAllBytes(Path.Combine(assets, "tyrian.hdt"), fixture);
            File.WriteAllBytes(Path.Combine(assets, "tyrian1.lvl"), []);
            using var output = new StringWriter();
            using var errors = new StringWriter();
            Check(ItemDataExtractor.Run(["--data", assets], output, errors, managed) == 0 && errors.ToString() == "", "Extractor failed.");
            foreach (var (category, type, count, _, _) in Layout)
            {
                var path = Path.Combine(managed, category.Replace('/', Path.DirectorySeparatorChar));
                var text = Encoding.Latin1.GetString(File.ReadAllBytes(path));
                var records = text.Split(Environment.NewLine).Count(line => line.StartsWith($"[{type}:", StringComparison.Ordinal));
                Check(records == count && text.Contains($"[{type}:{count - 1}]", StringComparison.Ordinal), $"Extractor count differs: {type}.");
                Check(!File.Exists(path + ".tmp"), "Finalized output left a temporary file.");
            }

            var shots = ReadCategory(managed, Layout[0].Category);
            Check(shots.Contains("attack=98 0 1 1 1 0 1 5", StringComparison.Ordinal) &&
                shots.Contains("chain=0 0 0 1 149 0 0 0", StringComparison.Ordinal) &&
                shots.Contains("iceticks=0 40 0 0 0 0 0 0", StringComparison.Ordinal) &&
                shots.Contains("infinite=0 0 0 0 0 1 1 1", StringComparison.Ordinal), "Attack transformations differ.");
            var weapons = ReadCategory(managed, Layout[1].Category);
            Check(weapons.Contains("chargegunweapons=476 477 478 479 480 481", StringComparison.Ordinal) &&
                weapons.Contains("bigshipgraphic=-1", StringComparison.Ordinal), "Weapon native metadata differs.");
            Check(ReadCategory(managed, Layout[5].Category).Contains("stop=1", StringComparison.Ordinal), "Sidekick boolean differs.");
            var enemies = ReadCategory(managed, Layout[7].Category);
            Check(enemies.Contains("elaunchtype=234", StringComparison.Ordinal) &&
                enemies.Contains("elaunchtypepos=234", StringComparison.Ordinal) &&
                enemies.Contains("elaunchspecial=1", StringComparison.Ordinal) &&
                enemies.Contains("eshotwait=1 20 255", StringComparison.Ordinal), "Enemy derived fields differ.");

            if (nativeExecutable is not null)
            {
                var result = Execute(nativeExecutable, native, "--data", assets);
                Check(result.Code == 0, $"Native extractor failed: {result.Errors}");
                Check(result.Output == output.ToString(), "Native extractor success output differs.");
                foreach (var (category, _, _, _, _) in Layout)
                {
                    Check(File.ReadAllBytes(CategoryPath(native, category)).AsSpan().SequenceEqual(File.ReadAllBytes(CategoryPath(managed, category))),
                        $"Native extractor byte output differs: {category}.");
                }
            }

            var preserved = Encoding.Latin1.GetBytes("Existing mod definitions must survive.\n");
            File.WriteAllBytes(CategoryPath(managed, Layout[2].Category), preserved);
            output.GetStringBuilder().Clear();
            Check(ItemDataExtractor.Run([$"--data={assets}"], output, errors, managed) == 0, "Repeated extraction failed.");
            Check(File.ReadAllBytes(CategoryPath(managed, Layout[2].Category)).AsSpan().SequenceEqual(preserved), "Existing category was overwritten.");
            Check(output.ToString().Split(Environment.NewLine).Count(line => line.StartsWith("Preserving existing category", StringComparison.Ordinal)) == 8,
                "Category preservation messages differ.");
            TestCommandLine(root, nativeExecutable);
            TestDataFiles(root, assets);
            TestFailures(root, fixture);
        }
        finally
        {
            foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            {
                File.Delete(file);
            }

            foreach (var directory in Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories).OrderByDescending(path => path.Length))
            {
                Directory.Delete(directory);
            }

            Directory.Delete(root);
        }
    }

    private static byte[] BuildFixture()
    {
        using var stream = new MemoryStream();
        BinaryFile.WriteInt32(stream, 32);
        stream.Write(new byte[28 + 14]);
        var sequence = 0;
        foreach (var (_, type, count, size, named) in Layout)
        {
            for (var index = 0; index < count; index++)
            {
                var data = new byte[size];
                for (var position = 0; position < size; position++)
                {
                    data[position] = unchecked((byte)(sequence++ * 73 + 11));
                }

                if (named)
                {
                    data[0] = 30;
                    Array.Fill(data, (byte)' ', 1, 30);
                    Encoding.Latin1.GetBytes($"Name-{index}-\x80\xFF").CopyTo(data, 1);
                    if (index == 1)
                    {
                        data[0] = 255;
                        data[8] = 0;
                    }
                    if (index == 3)
                    {
                        data[5] = 13;
                        data[6] = 10;
                    }
                }

                if (type == "shot" && index == 0)
                {
                    new byte[] { 98, 99, 100, 101, 249, 250, 251, 255 }.CopyTo(data, 10);
                }

                if (type == "enemy" && index == 0)
                {
                    new byte[] { 252, 1, 0 }.CopyTo(data, 1);
                    BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(71), 1234);
                }

                if (type == "sidekick" && index == 0)
                {
                    data[84] = 2;
                }

                stream.Write(data);
            }
        }

        return stream.ToArray();
    }

    private static void TestCommandLine(string directory, string? nativeExecutable)
    {
        foreach (var arguments in new[] { new[] { "--help" }, new[] { "--bad" }, new[] { "--data" },
            new[] { "--data=" }, new[] { "--data", "" }, new[] { "--help", "--bad" } })
        {
            using var output = new StringWriter();
            using var errors = new StringWriter();
            var program = nativeExecutable ?? "extractor";
            var code = ItemDataExtractor.Run(arguments, output, errors, directory, program);
            Check(code == (arguments[0] == "--help" ? 0 : 1), "Extractor CLI exit status differs.");
            if (nativeExecutable is not null)
            {
                var result = Execute(nativeExecutable, directory, arguments);
                Check(result.Code == code && result.Output == output.ToString() && result.Errors == errors.ToString(), "Native extractor CLI differs.");
            }
        }
    }

    private static void TestDataFiles(string directory, string assets)
    {
        var files = new DataFiles(directory, assets);
        Check(files.Directory == assets && files.Exists(assets, "tyrian.hdt"), "Custom data directory selection differs.");
        using var warnings = new StringWriter();
        Check(files.OpenWarn(assets, "missing.file", warnings) is null && warnings.ToString().StartsWith("warning: failed to open 'missing.file':", StringComparison.Ordinal),
            "File warning differs.");
        var fallback = new DataFiles(directory, "not-found", assets);
        Check(fallback.Directory == assets, "Installed data directory fallback differs.");
        using (var writable = files.OpenWarn(assets, "saved.cfg", warnings, FileMode.Create, FileAccess.Write)
            ?? throw new InvalidOperationException("Warning file wrapper cannot create saves."))
        {
            writable.WriteByte(42);
        }

        using (var writable = files.OpenRequired(assets, "required.cfg", FileMode.Create, FileAccess.Write))
        {
            writable.WriteByte(43);
        }

        Check(File.ReadAllBytes(Path.Combine(assets, "saved.cfg"))[0] == 42 &&
            File.ReadAllBytes(Path.Combine(assets, "required.cfg"))[0] == 43, "File wrapper write modes differ.");
        File.Delete(Path.Combine(assets, "tyrian1.lvl"));
        Check(files.Directory == assets, "Data directory cache differs.");
    }

    private static void TestFailures(string root, byte[] fixture)
    {
        var broken = Path.Combine(root, "broken");
        Directory.CreateDirectory(broken);
        var outputDirectory = Path.Combine(root, "failure-output");
        Directory.CreateDirectory(outputDirectory);
        File.WriteAllBytes(Path.Combine(broken, "tyrian.hdt"), fixture[..100]);
        using var output = new StringWriter();
        using var errors = new StringWriter();
        Check(ItemDataExtractor.Run(["--data", broken], output, errors, outputDirectory) == 1 &&
            errors.ToString().Contains("error:", StringComparison.Ordinal) && !output.ToString().Contains("Extraction complete", StringComparison.Ordinal),
            "Truncated input reported success.");
        Check(!File.Exists(CategoryPath(outputDirectory, Layout[0].Category)) &&
            !File.Exists(CategoryPath(outputDirectory, Layout[0].Category) + ".tmp"), "Truncated input published an incomplete category.");
        File.WriteAllBytes(Path.Combine(broken, "tyrian.hdt"), new byte[] { 255, 255, 255, 255 });
        Check(ItemDataExtractor.Run(["--data", broken], output, errors, outputDirectory) == 1 &&
            errors.ToString().Contains("invalid item-data offset", StringComparison.Ordinal), "Invalid data offset was accepted.");
        Check(ItemDataExtractor.Run(["--data", Path.Combine(root, "missing-assets")], output, errors, outputDirectory) == 1 &&
            errors.ToString().Contains("required Tyrian 2.1 data files could not be found", StringComparison.Ordinal), "Missing assets lack a diagnostic.");
        var blocked = Path.Combine(root, "blocked-output");
        Directory.CreateDirectory(blocked);
        File.WriteAllText(Path.Combine(blocked, "data"), "not a directory");
        Check(ItemDataExtractor.Run(["--data", broken], output, errors, blocked) == 1, "Directory creation failure reported success.");
    }

    private static (int Code, string Output, string Errors) Execute(string executable, string directory, params string[] arguments)
    {
        var start = new ProcessStartInfo(executable) { WorkingDirectory = directory, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start native extractor.");
        var output = process.StandardOutput.ReadToEndAsync();
        var errors = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(30000))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException("Native extractor timed out.");
        }

        return (process.ExitCode, output.GetAwaiter().GetResult(), errors.GetAwaiter().GetResult());
    }

    private static string CategoryPath(string directory, string category) => Path.Combine(directory, category.Replace('/', Path.DirectorySeparatorChar));
    private static string ReadCategory(string directory, string category) => Encoding.Latin1.GetString(File.ReadAllBytes(CategoryPath(directory, category)));

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
