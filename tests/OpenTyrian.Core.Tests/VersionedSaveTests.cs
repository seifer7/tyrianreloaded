using System.Buffers.Binary;
using System.Diagnostics;
using System.Text;
using OpenTyrian.Core;

namespace OpenTyrian.Core.Tests;

internal static class VersionedSaveTests
{
    public static void Run(string? executable = null)
    {
        var items = Database();
        var trace = new StringBuilder();
        byte[]? valid = null;
        var verified = 0;
        for (var mode = 0; mode < 64; mode++)
        {
            trace.AppendLine($"case:{mode}");
            using var file = new MemoryStream();
            try { VersionedSaveGame.Write(file, Fixture(mode), items); }
            catch (InvalidDataException exception) when (exception.Message.Contains("NUL-truncated native cipher", StringComparison.Ordinal))
            {
                trace.AppendLine("rejected:NUL");
                Check(file.Length == 0, "Invalid native cipher partially overwrote a save.");
                continue;
            }
            verified++;
            valid ??= file.ToArray();
            trace.AppendLine($"saved:{Convert.ToHexString(file.ToArray())}");
            var game = Fixture(mode + 32);
            file.Position = 0;
            VersionedSaveGame.Load(file, game, items, episode => { trace.AppendLine($"game-episode:{episode}"); return episode; });
            Dump(trace, game);
        }
        Check(verified == 57 && valid != null, "Defined roundtrip/native cipher rejection counts changed.");
        if (executable != null)
        {
            var actual = RunNative(executable);
            var expected = trace.ToString().Replace("\r\n", "\n", StringComparison.Ordinal);
            var mismatch = 0;
            while (mismatch < Math.Min(expected.Length, actual.Length) && expected[mismatch] == actual[mismatch]) mismatch++;
            Check(actual == expected, $"Versioned save trace differs at {mismatch}: native '{actual.Substring(mismatch, Math.Min(100, actual.Length - mismatch))}', managed '{expected.Substring(mismatch, Math.Min(100, expected.Length - mismatch))}'.");
        }
        FailureChecks(valid ?? throw new InvalidOperationException("Missing save fixture."), items);
        Persistence(items);
    }

    private static void FailureChecks(byte[] valid, ItemDatabase items)
    {
        var unchanged = Fixture(9);
        var before = new StringBuilder(); Dump(before, unchanged);
        foreach (var mutation in new Action<byte[]>[]
        {
            bytes => bytes[0] = 0,
            bytes => { BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(4), 2); BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(6), 2); },
            bytes => bytes[^1] ^= 1,
            bytes => bytes[157] = 0,
            bytes => bytes[156] = 2
        })
        {
            var bytes = valid.ToArray(); mutation(bytes);
            Throws<InvalidDataException>(() => VersionedSaveGame.Load(new MemoryStream(bytes), unchanged, items,
                _ => throw new InvalidOperationException("Invalid save initialized an episode.")));
            var after = new StringBuilder(); Dump(after, unchanged);
            Check(before.ToString() == after.ToString(), "Invalid save changed game state.");
        }
        for (var size = 0; size <= 157; size++)
        {
            var bytes = valid[..size];
            if (size < 157) Throws<EndOfStreamException>(() => VersionedSaveGame.Load(new MemoryStream(bytes), unchanged, items, n => n));
            else Throws<InvalidDataException>(() => VersionedSaveGame.Load(new MemoryStream(bytes), unchanged, items, n => n));
        }
        var future = valid.ToArray(); BinaryPrimitives.WriteUInt16LittleEndian(future.AsSpan(4), 3);
        VersionedSaveGame.Load(new MemoryStream(future), unchanged, items, n => n);
        var excessive = new byte[157 + 1024]; valid.AsSpan(0, 157).CopyTo(excessive);
        Array.Fill(excessive, (byte)1, 157, 1024);
        Throws<InvalidDataException>(() => VersionedSaveGame.Load(new MemoryStream(excessive), unchanged, items, n => n));
        var missing = valid.ToArray();
        var payload = missing.AsSpan(157); LegacySaveCipher.XorCString(payload, "TYRIANRELOADED"u8);
        var text = Encoding.Latin1.GetString(payload);
        var fields = text.Split('*'); fields[0] = "unknown";
        using var absentItem = Encoded(fields);
        Throws<InvalidDataException>(() => VersionedSaveGame.Load(absentItem, unchanged, items, n => n), "Required save item ID");
        fields[0] = text.Split('*')[0];
        fields[2] = "-1"; fields[4] = "  -7x"; fields[10] = "-1";
        using var signed = Encoded(fields);
        VersionedSaveGame.Load(signed, unchanged, items, n => n);
        Check(unchanged.Players[0].Items.Weapons[0].Power == 255 &&
            unchanged.Players[0].Items.Weapons[1].Power == 249 &&
            unchanged.Players[0].Cash == (OperatingSystem.IsWindows() || IntPtr.Size == 4 ? uint.MaxValue : ulong.MaxValue),
            "Native atoi prefix, signed power truncation, or platform unsigned cash conversion changed.");
        fields[10] = "2147483648";
        using var overflow = Encoded(fields);
        Throws<InvalidDataException>(() => VersionedSaveGame.Load(overflow, unchanged, items, n => n), "signed-int range");
        var repeated = valid.ToArray(); repeated[15] = 2;
        BinaryPrimitives.WriteUInt16LittleEndian(repeated.AsSpan(13), ushort.MaxValue);
        VersionedSaveGame.Load(new MemoryStream(repeated), unchanged, items, n => n);
        Check(!unchanged.GameHasRepeated && unchanged.CubeMax == ushort.MaxValue, "Exact-one repeated flag or sixteen-bit cube count changed.");
        var bad = Fixture(0); bad.LastLevelName = new byte[12];
        Throws<InvalidDataException>(() => VersionedSaveGame.Write(new MemoryStream(), bad, items));
        var collision = Database(cipherCollision: true);
        bad = Fixture(0); bad.Players[0].Items.Ship = checked((byte)collision.Index(ItemType.Ship, "T"));
        Throws<InvalidDataException>(() => VersionedSaveGame.Write(new MemoryStream(), bad, collision));

        MemoryStream Encoded(string[] values)
        {
            byte[] encoded;
            for (; ; )
            {
                encoded = Encoding.Latin1.GetBytes(string.Join('*', values));
                LegacySaveCipher.XorCString(encoded, "TYRIANRELOADED"u8);
                if (!encoded.Contains((byte)0)) break;
                values[11] += "r";
                Check(values[11].Length <= 2, "Could not construct a defined native cipher fixture.");
            }
            var result = new MemoryStream();
            result.Write(valid, 0, 157); result.Write(encoded); result.Position = 0;
            return result;
        }
    }

    private static void Persistence(ItemDatabase items)
    {
        var root = Path.Combine(Path.GetTempPath(), $"opentyrian-versioned-{Guid.NewGuid():N}");
        var directory = Path.Combine(root, "saves");
        Directory.CreateDirectory(directory);
        var catalog = new SavedGameCatalog(root, TextWriter.Null);
        var path = catalog.NativePath("fixture.tyrsav");
        try
        {
            VersionedSaveGame.SaveFile(root, "fixture", Fixture(0), items);
            Check(catalog.ReadMetadataFile("fixture.tyrsav")?.GameMode == 0, "Versioned save metadata was not integrated.");
            var restored = Fixture(32);
            using var diagnostics = new StringWriter();
            Check(VersionedSaveGame.LoadFile(root, "fixture", restored, items, n => n, diagnostics) && restored.GameLoaded,
                "Versioned save file did not load.");
            Check(!VersionedSaveGame.LoadFile(root, "missing", restored, items, n => n, diagnostics) &&
                diagnostics.ToString().Contains("save file not found", StringComparison.Ordinal), "Missing file was not diagnosed.");
            var existing = File.ReadAllBytes(path);
            var invalid = Fixture(0); invalid.LastLevelName = new byte[12];
            Throws<InvalidDataException>(() => VersionedSaveGame.SaveFile(root, "fixture", invalid, items));
            Check(File.ReadAllBytes(path).SequenceEqual(existing), "Invalid save erased existing data.");
            using var locked = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            Throws<IOException>(() => VersionedSaveGame.LoadFile(root, "fixture", restored, items, n => n, diagnostics));
            Throws<IOException>(() => VersionedSaveGame.SaveFile(root, "fixture", Fixture(0), items));
        }
        finally { if (File.Exists(path)) File.Delete(path); Directory.Delete(directory); Directory.Delete(root); }
    }

    private static ItemDatabase Database(bool cipherCollision = false)
    {
        var fixture = new StringBuilder();
        foreach (var type in new[] { "shot", "ship", "weapon", "generator", "shield", "sidekick", "special" })
        {
            if (cipherCollision && type == "ship") fixture.AppendLine("[ship:T]");
            for (var index = 0; index < 256; index++) fixture.AppendLine($"[{type}:{index}]");
        }
        foreach (var index in new[] { 0, 533, 534, 560 }) fixture.AppendLine($"[enemy:{index}]");
        var definitions = new ItemDefinitions();
        definitions.ParseBundle(Encoding.ASCII.GetBytes(fixture.ToString()), "save-fixture");
        return ItemDatabase.FromDefinitions(definitions);
    }

    private static LegacyGameState Fixture(int mode)
    {
        var game = new LegacyGameState
        {
            Difficulty = unchecked((sbyte)(mode * 17 - 128)),
            InitialDifficulty = unchecked((sbyte)(mode * 11 - 64)),
            SaveLevel = unchecked((byte)(mode * 31 + 2)),
            MainLevel = 42,
            LastCubeMax = unchecked((ushort)(mode * 5001 + 255)),
            CubeMax = 777,
            InitialEpisode = unchecked((byte)(mode * 3 + 1)),
            Episode = (byte)(mode % 5 + 1),
            GameHasRepeated = (mode & 1) != 0,
            SuperTyrian = mode % 8 == 4,
            OnePlayerAction = mode % 8 is 3 or 6,
            SuperArcadeMode = (byte)(mode % 8 == 2 ? 7 : mode % 8 == 5 ? 255 : 0),
            TwoPlayerMode = (mode & 1) != 0,
            ExtraGame = true,
            GalagaMode = true,
            LevelName = "Old"u8.ToArray(),
            LastLevelName = Encoding.ASCII.GetBytes(mode % 3 == 0 ? "Completed" : $"LEVEL{mode:00}")
        };
        for (var p = 0; p < 2; p++)
        {
            game.Players[p].Items.LoadLegacyItems(Enumerable.Range(0, 12).Select(i => unchecked((byte)(mode * 17 + p * 53 + i * 29))).ToArray());
            game.Players[p].LastItems.LoadLegacyItems(Enumerable.Range(0, 12).Select(i => unchecked((byte)(mode * 13 + p * 67 + i * 31))).ToArray());
            game.Players[p].Items.Weapons[0].Power = unchecked((byte)(mode * 19 + p * 7));
            game.Players[p].Items.Weapons[1].Power = unchecked((byte)(mode * 23 + p * 11));
            game.Players[p].LastItems.Weapons[0].Power = unchecked((byte)(mode + 3));
            game.Players[p].LastItems.Weapons[1].Power = unchecked((byte)(mode + 9));
            game.Players[p].Cash = (ulong)(mode * 10001 + p * 321 + 17);
        }
        return game;
    }

    private static void Dump(StringBuilder trace, LegacyGameState game)
    {
        trace.AppendLine($"state:{game.Difficulty},{game.InitialDifficulty},{game.MainLevel},{game.SaveLevel},{game.CubeMax},{game.LastCubeMax},{Bit(game.GameHasRepeated)},{game.InitialEpisode},{game.Episode},{Bit(game.SuperTyrian)},{Bit(game.OnePlayerAction)},{game.SuperArcadeMode},{Bit(game.TwoPlayerMode)},{Bit(game.ExtraGame)},{Bit(game.GalagaMode)},{Bit(game.GameLoaded)},{Bit(game.GameJustLoaded)}");
        trace.AppendLine($"name:{Convert.ToHexString(game.LevelName)}"); trace.AppendLine($"last:{Convert.ToHexString(game.LastLevelName)}");
        for (var p = 0; p < 2; p++)
        {
            var player = game.Players[p];
            trace.AppendLine($"items:{p}:{Convert.ToHexString(player.Items.ToLegacyItems(0))}");
            trace.AppendLine($"powers:{player.Items.Weapons[0].Power},{player.Items.Weapons[1].Power}");
            trace.AppendLine($"previous:{Convert.ToHexString(player.LastItems.ToLegacyItems(0))}");
            trace.AppendLine($"previous-powers:{player.LastItems.Weapons[0].Power},{player.LastItems.Weapons[1].Power}");
            trace.AppendLine($"cash:{player.Cash}");
        }
    }
    private static string RunNative(string executable)
    {
        using var process = Process.Start(new ProcessStartInfo(Path.GetFullPath(executable))
        { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true })
            ?? throw new InvalidOperationException("Cannot start versioned save oracle.");
        var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(30000)) { process.Kill(entireProcessTree: true); throw new TimeoutException("Versioned save oracle timed out."); }
        Check(process.ExitCode == 0, $"Versioned save oracle failed: {error.GetAwaiter().GetResult()}");
        return output.GetAwaiter().GetResult().Replace("\r\n", "\n", StringComparison.Ordinal);
    }
    private static int Bit(bool value) => value ? 1 : 0;
    private static void Throws<T>(Action action, string? diagnostic = null) where T : Exception
    {
        try { action(); }
        catch (T exception)
        {
            Check(diagnostic == null || exception.Message.Contains(diagnostic, StringComparison.Ordinal),
                $"Expected {typeof(T).Name} diagnostic '{diagnostic}', got '{exception.Message}'.");
            return;
        }
        throw new InvalidOperationException($"Expected {typeof(T).Name}.");
    }
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
