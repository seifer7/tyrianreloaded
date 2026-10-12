using System.Diagnostics;
using System.Text;
using OpenTyrian.Core;

namespace OpenTyrian.Core.Tests;

internal static class LegacySaveTests
{
    public static void Run(string? executable = null)
    {
        var trace = new StringBuilder();
        for (var mode = 0; mode < 32; mode++)
        {
            var bytes = new byte[LegacySaveCipher.PayloadSize];
            for (var i = 0; i < bytes.Length; i++)
                bytes[i] = unchecked((byte)(mode == 0 ? 0 : mode == 1 ? 255 : i * (mode * 17 + 1) + i / (mode + 1)));
            var encrypted = LegacySaveCipher.Encrypt(bytes);
            trace.AppendLine($"cipher:{mode}:{Convert.ToHexString(encrypted)}");
            var decrypted = LegacySaveCipher.Decrypt(encrypted, TextWriter.Null);
            Check(bytes.AsSpan().SequenceEqual(decrypted), "Legacy cipher did not roundtrip.");
            trace.AppendLine($"plain:{mode}:{Convert.ToHexString(decrypted)}");
        }
        for (var mode = 0; mode < 256; mode++)
        {
            var bytes = Enumerable.Range(0, 12).Select(i => unchecked((byte)(mode * 17 + i * 31))).ToArray();
            var items = new PlayerItems();
            items.Weapons[0].Power = 3; items.Weapons[1].Power = 11;
            var episode = items.LoadLegacyItems(bytes);
            trace.AppendLine($"items:{mode}:{Convert.ToHexString(items.ToLegacyItems(episode))}");
            trace.AppendLine($"power:{items.Weapons[0].Power},{items.Weapons[1].Power}");
            items.LoadLegacyItems(bytes);
            trace.AppendLine($"null-episode:{mode}:{Convert.ToHexString(items.ToLegacyItems(99))}");
        }
        for (var mode = 0; mode < 16; mode++)
        {
            var archive = new LegacySaveArchive { EditorLevel = unchecked((ushort)(mode * 1703 + 800)) };
            for (var z = 0; z < 22; z++)
            {
                var slot = archive.Slots[z];
                slot.Encode = (ushort)(mode * 1001 + z); slot.Level = (ushort)(mode * 100 + z);
                for (var i = 0; i < 12; i++)
                {
                    slot.Items[i] = unchecked((byte)(mode + z + i));
                    slot.LastItems[i] = unchecked((byte)(mode * 17 + z + i * 5));
                }
                slot.Score = mode * 1234567 + z; slot.Score2 = -mode * 7654321 - z;
                slot.LevelName = Enumerable.Repeat((byte)('A' + z % 26), (mode + z) % 10).ToArray();
                for (var i = 0; i < 14; i++) slot.Name[i] = unchecked((byte)(mode * 23 + z + i));
                slot.Cubes = (byte)(mode + z); slot.Power[0] = (byte)z; slot.Power[1] = (byte)mode;
                slot.Episode = (byte)(z % 4 + 1); slot.Difficulty = (byte)mode; slot.SecretHint = (byte)z;
                slot.Input1 = 1; slot.Input2 = 2; slot.GameHasRepeated = z % 2 != 0; slot.InitialDifficulty = 3;
                slot.HighScore1 = 1000 * z; slot.HighScore2 = -2000 * z;
                slot.HighScoreName = Enumerable.Repeat((byte)('Z' - z % 26), (mode * 3 + z) % 30).ToArray();
                slot.HighScoreDifficulty = (byte)(mode + z);
            }
            for (var i = 0; i < 100; i++) archive.ItemAvailability[i] = unchecked((byte)(mode * 17 + i));
            using var stream = new MemoryStream();
            archive.Write(stream);
            Check(stream.Length == 2502, "Legacy archive size changed.");
            trace.AppendLine($"archive:{mode}:{Convert.ToHexString(stream.ToArray())}");
            stream.Position = 0;
            var read = LegacySaveArchive.Read(stream, TextWriter.Null);
            Check(read.EditorLevel == (ushort)((archive.EditorLevel << 8) | (archive.EditorLevel >> 8)),
                "Native editor-level byte-order quirk was not preserved.");
            Check(archive.ItemAvailability.AsSpan(0, 98).SequenceEqual(read.ItemAvailability.AsSpan(0, 98)),
                "Legacy availability tail was not preserved.");
            trace.AppendLine($"editor:{mode},{read.EditorLevel}");
            for (var z = 0; z < 22; z++)
            {
                var s = read.Slots[z];
                trace.AppendLine($"slot:{mode},{z},{s.Encode},{s.Level},{s.Score},{s.Score2},{Encoding.ASCII.GetString(s.LevelName)},{Encoding.ASCII.GetString(s.HighScoreName)},{(s.GameHasRepeated ? 1 : 0)}");
                Check(s.Items.AsSpan().SequenceEqual(archive.Slots[z].Items) && s.LastItems.AsSpan().SequenceEqual(archive.Slots[z].LastItems) &&
                    s.Name.AsSpan().SequenceEqual(archive.Slots[z].Name) && s.Power.AsSpan().SequenceEqual(archive.Slots[z].Power),
                    "Legacy raw fields changed during load.");
            }
        }
        string[] keys = ["TYRIANRELOADED", "ABCD", "x"];
        for (var mode = 0; mode < 12; mode++)
        {
            var bytes = new byte[33];
            for (var i = 0; i < 32; i++) bytes[i] = unchecked((byte)(mode * 17 + i + 1));
            LegacySaveCipher.XorCString(bytes, Encoding.ASCII.GetBytes(keys[mode % 3]));
            trace.AppendLine($"xor:{mode}:{Convert.ToHexString(bytes)}");
        }
        GameOperations(trace);
        if (executable != null)
        {
            var native = RunNative(executable);
            var actual = trace.ToString().Replace("\r\n", "\n", StringComparison.Ordinal);
            Check(native.Output == actual && native.Code == 0, "Native legacy save byte/field traces differ.");
            foreach (var index in new[] { 0, 2497, 2498, 2499, 2500, 2501 })
            {
                var bytes = Enumerable.Range(0, 2498).Select(i => unchecked((byte)(i * 37 + i / 3))).ToArray();
                var file = LegacySaveCipher.Encrypt(bytes);
                file[index] ^= 1;
                using var diagnostics = new StringWriter();
                var failed = RunNative(executable, index.ToString(System.Globalization.CultureInfo.InvariantCulture));
                if (index == 0)
                {
                    LegacySaveCipher.Decrypt(file, diagnostics);
                    Check(failed.Code == 0 && failed.Output.Length == 0 && failed.Error.Length == 0 && diagnostics.ToString().Length == 0,
                        "Native legacy checksum-collision behavior changed.");
                }
                else
                {
                    Throws<InvalidDataException>(() => LegacySaveCipher.Decrypt(file, diagnostics));
                    Check(failed.Code == 255 && failed.Output == diagnostics.ToString().Replace("\r\n", "\n", StringComparison.Ordinal) &&
                        failed.Error == "Error reading save file!\n", "Legacy corrupted-save diagnostics differ.");
                }
            }
        }
        Failures();
        FilePersistence();
    }

    private static void FilePersistence()
    {
        var path = Path.GetTempFileName();
        try
        {
            var archive = new LegacySaveArchive();
            var game = new LegacyGameState
            {
                TwoPlayerMode = true,
                Episode = 3,
                InitialEpisode = 1,
                SaveLevel = 33,
                LastLevelName = "Completed"u8.ToArray(),
                Difficulty = 4,
                InitialDifficulty = 2,
                LastCubeMax = 7
            };
            game.Players[0].Items.Ship = 2; game.Players[1].Items.Ship = 4;
            game.Players[0].Items.Weapons[0].Power = 3; game.Players[1].Items.Weapons[1].Power = 5;
            game.Players[0].Cash = 12345; game.Players[1].Cash = 67890;
            LegacySaveGame.Save(archive, game, 12, "Twin flight"u8, saved =>
            {
                using var output = File.Create(path);
                saved.Write(output);
                output.Flush(flushToDisk: true);
            });
            LegacySaveArchive restored;
            using (var input = File.OpenRead(path)) restored = LegacySaveArchive.Read(input, TextWriter.Null);
            var loaded = new LegacyGameState();
            LegacySaveGame.Load(restored, loaded, 12, episode => episode);
            Check(loaded.TwoPlayerMode && loaded.Episode == 3 && loaded.MainLevel == 33 &&
                loaded.Players[0].Cash == 12345 && loaded.Players[1].Cash == 67890 &&
                loaded.Players[0].Items.Ship == 2 && loaded.Players[1].Items.Ship == 4 &&
                loaded.Players[0].Items.Weapons[0].Power == 3 && loaded.Players[1].Items.Weapons[1].Power == 5,
                "Legacy save state did not persist through a real file and restore.");
        }
        finally { File.Delete(path); }
    }

    private static void GameOperations(StringBuilder trace)
    {
        for (var mode = 0; mode < 8; mode++)
            for (byte slotNumber = 1; slotNumber <= 22; slotNumber++)
            {
                trace.AppendLine($"game:{mode},{slotNumber}");
                var archive = new LegacySaveArchive { EditorLevel = 800 };
                var slot = archive.Slots[slotNumber - 1];
                Array.Fill(slot.Name, (byte)'Z');
                var game = new LegacyGameState
                {
                    InitialDifficulty = -3,
                    Difficulty = -2,
                    GameHasRepeated = mode % 2 != 0,
                    SaveLevel = (byte)(200 + mode),
                    Episode = (byte)(mode % 4 + 1),
                    InitialEpisode = 3,
                    LastCubeMax = 300,
                    SecretHint = 7,
                    TwoPlayerMode = (mode & 1) != 0,
                    SuperTyrian = (mode & 2) != 0,
                    OnePlayerAction = (mode & 4) != 0,
                    SuperArcadeMode = mode == 3 ? (byte)7 : mode == 7 ? (byte)9 : (byte)0,
                    ExtraGame = true,
                    GalagaMode = true,
                    LastLevelName = Encoding.ASCII.GetBytes(mode % 2 != 0 ? "Completed" : "LevelA")
                };
                game.InputDevices[0] = 1; game.InputDevices[1] = 2;
                for (var p = 0; p < 2; p++)
                {
                    var items = Enumerable.Range(0, 12).Select(i => (byte)(mode * 13 + p * 19 + i)).ToArray();
                    game.Players[p].Items.LoadLegacyItems(items);
                    game.Players[p].LastItems.LoadLegacyItems(items);
                    game.Players[p].Items.Weapons[0].Power = (byte)((mode + p) % 12);
                    game.Players[p].Items.Weapons[1].Power = (byte)((mode + p + 3) % 12);
                    game.Players[p].LastItems.Weapons[0].Power = 9;
                    game.Players[p].LastItems.Weapons[1].Power = 10;
                    game.Players[p].Cash = 0xF0000000u + (uint)(mode * 123 + p);
                }
                LegacySaveGame.Save(archive, game, slotNumber, Encoding.ASCII.GetBytes(mode % 2 != 0 ? "ABCDEFGHIJKLMN" : "Hi"),
                    saved =>
                    {
                        using var output = new MemoryStream();
                        saved.Write(output);
                        trace.AppendLine($"game-save:{Convert.ToHexString(output.ToArray())}");
                    });
                slot.Level += 256;
                LegacySaveGame.Load(archive, game, slotNumber, episode => { trace.AppendLine($"game-episode:{episode}"); return episode; });
                trace.AppendLine($"game-state:{Bit(game.SuperTyrian)},{Bit(game.OnePlayerAction)},{game.SuperArcadeMode},{Bit(game.TwoPlayerMode)},{Bit(game.ExtraGame)},{Bit(game.GalagaMode)},{game.InitialDifficulty},{game.Difficulty},{Bit(game.GameHasRepeated)},{game.InitialEpisode},{game.MainLevel},{game.SaveLevel},{game.CubeMax},{game.LastCubeMax},{game.SecretHint},{game.InputDevices[0]},{game.InputDevices[1]},{game.Episode},{game.Players[1].Items.SidekickLevel},{Encoding.ASCII.GetString(game.LastLevelName)},{Encoding.ASCII.GetString(game.LevelName)}");
                for (var p = 0; p < 2; p++)
                {
                    var player = game.Players[p];
                    trace.AppendLine($"game-player:{p},{player.Cash},{player.Items.Weapons[0].Power},{player.Items.Weapons[1].Power}:{Convert.ToHexString(player.Items.ToLegacyItems(0))}");
                    trace.AppendLine($"game-last:{p},{player.LastItems.Weapons[0].Power},{player.LastItems.Weapons[1].Power}:{Convert.ToHexString(player.LastItems.ToLegacyItems(0))}");
                }
            }
    }

    private static int Bit(bool value) => value ? 1 : 0;

    private static void Failures()
    {
        Throws<ArgumentException>(() => LegacySaveCipher.Encrypt([]));
        Throws<InvalidDataException>(() => LegacySaveCipher.Decrypt(new byte[2501], TextWriter.Null));
        Throws<ArgumentException>(() => LegacySaveCipher.XorCString(new byte[1], []));
        Throws<ArgumentException>(() => new PlayerItems().LoadLegacyItems([]));
        using var truncated = new MemoryStream(new byte[2501]);
        Throws<IOException>(() => LegacySaveArchive.Read(truncated, TextWriter.Null));
        var archive = new LegacySaveArchive();
        archive.Slots[0].LevelName = new byte[10];
        Array.Fill(archive.Slots[0].LevelName, (byte)'A');
        using var stream = new MemoryStream();
        Throws<InvalidDataException>(() => archive.Write(stream));
        var payload = new byte[2498];
        payload[24] = 10;
        using var malformed = new MemoryStream(LegacySaveCipher.Encrypt(payload));
        Throws<InvalidDataException>(() => LegacySaveArchive.Read(malformed, TextWriter.Null));
        var game = new LegacyGameState();
        Throws<ArgumentOutOfRangeException>(() => LegacySaveGame.Load(archive, game, 0, i => i));
        Throws<ArgumentOutOfRangeException>(() => LegacySaveGame.Save(archive, game, 23, [], _ => { }));
        Throws<ArgumentException>(() => LegacySaveGame.Save(archive, game, 1, "name-is-too-long"u8, _ => { }));
        Throws<IOException>(() => LegacySaveGame.Save(new(), game, 1, [], _ => throw new IOException("fixture")));
    }

    private static (string Output, string Error, int Code) RunNative(string executable, string? argument = null)
    {
        var start = new ProcessStartInfo(Path.GetFullPath(executable))
        { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        if (argument != null) start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Cannot start native save oracle.");
        var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(30000))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException("Native save oracle timed out.");
        }
        return (stdout.GetAwaiter().GetResult().Replace("\r\n", "\n", StringComparison.Ordinal),
            stderr.GetAwaiter().GetResult().Replace("\r\n", "\n", StringComparison.Ordinal), process.ExitCode);
    }
    private static void Throws<T>(Action action) where T : Exception
    {
        try { action(); } catch (T) { return; }
        throw new InvalidOperationException($"Expected {typeof(T).Name}.");
    }
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
