using System.Text;
using OpenTyrian.Core;

namespace OpenTyrian.Core.Tests;

internal static class GameRuntimeAssetsTests
{
    public static void Run()
    {
        var root = Path.Combine(Path.GetTempPath(), $"opentyrian-runtime-assets-{Guid.NewGuid():N}");
        var working = Path.Combine(root, "working");
        var assets = Path.Combine(root, "assets");
        Directory.CreateDirectory(working);
        Directory.CreateDirectory(assets);
        try
        {
            File.WriteAllBytes(Path.Combine(assets, "tyrian1.lvl"), [0, 0]);
            File.WriteAllBytes(Path.Combine(assets, "palette.dat"), new byte[23 * 256 * 3]);
            File.WriteAllBytes(Path.Combine(assets, "tyrian.shp"), Shapes());
            File.WriteAllBytes(Path.Combine(assets, "tyrian.hdt"), HelpArchive());
            File.WriteAllBytes(Path.Combine(assets, "music.mus"), MusicArchive());
            File.WriteAllBytes(Path.Combine(assets, "tyrian.snd"), SoundBankTests.BuildBank(29, false, 0));
            File.WriteAllBytes(Path.Combine(assets, "voices.snd"), SoundBankTests.BuildBank(9, true, 1));
            WriteItemBundles(working);
            using (var runtime = GameRuntimeAssets.Load(working, assets, root, false, TextWriter.Null))
            {
                Check(runtime.Episodes.Available[0] && runtime.Episodes.Levels.Count == 0 &&
                    runtime.Items.Shots[0].Id == "0" && runtime.Palettes.Length == 23 &&
                    runtime.Shapes.Tables.Length == 8 && runtime.Help.MenuText.Length == 7 &&
                    !runtime.EditedShips.Available && !runtime.ChristmasEnabled,
                    "Startup asset loader did not compose episode, item, palette, shape, help, and optional ship data.");
                Check(runtime.Shapes.Tables.All(table => table.Count == 0) &&
                    runtime.Shapes.Sheets.All(sheet => sheet.Size == 0),
                    "Synthetic startup shape archive did not load its empty native tables.");
            }

            File.WriteAllBytes(Path.Combine(assets, "tyrianc.shp"), Shapes());
            using (var diagnostics = new StringWriter())
            using (var runtime = GameRuntimeAssets.Load(working, assets, root, true, diagnostics))
            {
                Check(!runtime.ChristmasEnabled && diagnostics.ToString().Contains("warning: Christmas is missing.",
                    StringComparison.Ordinal), "Missing Christmas voice data did not disable Christmas mode with a warning.");
            }

            File.WriteAllBytes(Path.Combine(assets, "voicesc.snd"), SoundBankTests.BuildBank(9, true, 2));
            using (var runtime = GameRuntimeAssets.Load(working, assets, root, true, TextWriter.Null))
            {
                Check(runtime.ChristmasEnabled, "Complete Christmas assets did not enable Christmas mode.");
            }

            var seasonalStartup = new GameStartupState { Christmas = true };
            var seasonalOptions = GameStartupArguments.Parse(seasonalStartup, ["OpenTyrian", "--data", assets],
                TextWriter.Null, TextWriter.Null);
            Check(seasonalOptions.ExitCode is null, "Runtime resource test launch options did not parse.");
            using (var runtime = GameRuntimeResources.Load(working, seasonalStartup, root, TextWriter.Null, new IdentityConverter()))
            {
                Check(seasonalStartup.Christmas && runtime.Assets.ChristmasEnabled &&
                    runtime.Audio is { } audio && audio.Music.Count == 1 &&
                    audio.Sounds.Samples[32].AsSpan().SequenceEqual(new short[] { 59 }),
                    "Complete runtime composition did not select seasonal audio with seasonal assets.");
            }

            File.Delete(Path.Combine(assets, "tyrianc.shp"));
            using (var diagnostics = new StringWriter())
            using (var runtime = GameRuntimeResources.Load(working, seasonalStartup, root, diagnostics, new IdentityConverter()))
            {
                Check(!seasonalStartup.Christmas && !runtime.Assets.ChristmasEnabled &&
                    runtime.Audio is { } audio && audio.Sounds.Samples[32].AsSpan().SequenceEqual(new short[] { 58 }) &&
                    diagnostics.ToString().Contains("warning: Christmas is missing.", StringComparison.Ordinal),
                    "Runtime composition did not fall back to standard shapes and voices when a seasonal asset was missing.");
            }

            File.Delete(Path.Combine(assets, "music.mus"));
            var silentStartup = new GameStartupState();
            var silentOptions = GameStartupArguments.Parse(silentStartup, ["OpenTyrian", "--data", assets, "--no-sound"],
                TextWriter.Null, TextWriter.Null);
            Check(silentOptions.ExitCode is null && silentStartup.AudioDisabled,
                "Runtime resource test no-sound option did not parse.");
            using (var runtime = GameRuntimeResources.Load(working, silentStartup, root, TextWriter.Null, null))
                Check(runtime.Audio is null, "Audio-disabled startup unexpectedly loaded music and sound resources.");

            using (var runtime = GameRuntimeResources.LoadForStartup(working, silentStartup, root, TextWriter.Null))
                Check(runtime.AudioSession is null && runtime.Assets.Episodes.Available[0],
                    "No-sound startup did not load game assets without initializing SDL audio.");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    internal static byte[] Shapes()
    {
        using var stream = new MemoryStream();
        BinaryFile.WriteUInt16(stream, 12);
        var start = 2 + 12 * sizeof(int);
        for (var index = 0; index < 7; index++) BinaryFile.WriteInt32(stream, start + index * 2);
        for (var index = 0; index < 5; index++) BinaryFile.WriteInt32(stream, start + 14);
        for (var index = 0; index < 7; index++) BinaryFile.WriteUInt16(stream, 0);
        return stream.ToArray();
    }

    internal static byte[] HelpArchive()
    {
        int[] groupEntries =
        [
            39, 21, 68, 5, 11, 7, 9, 6, 34, 7, 9, 8, 6, 6, 5, 6, 7,
            5, 6, 3, 4, 4, 11, 6, 7, 5, 11, 9, 25, 17, 5, 52, 5
        ];
        using var stream = new MemoryStream();
        BinaryFile.WriteInt32(stream, 0);
        for (var group = 0; group < groupEntries.Length; group++)
        {
            stream.WriteByte(0);
            for (var entry = 0; entry < groupEntries[group]; entry++) stream.WriteByte(0);
            if (group != groupEntries.Length - 1) stream.WriteByte(0);
        }
        return stream.ToArray();
    }

    internal static byte[] MusicArchive()
    {
        using var stream = new MemoryStream();
        BinaryFile.WriteUInt16(stream, 1);
        BinaryFile.WriteUInt32(stream, 6);
        stream.Write([1, 2, 3]);
        return stream.ToArray();
    }

    internal static void WriteItemBundles(string working)
    {
        var fixture = ItemDatabaseTests.Fixture(0);
        var categories = new (string File, string Header)[]
        {
            ("data/shots/shots.tyrr", "shot"),
            ("data/items/ships.tyrr", "ship"),
            ("data/items/weapons.tyrr", "weapon"),
            ("data/items/generators.tyrr", "generator"),
            ("data/items/shields.tyrr", "shield"),
            ("data/items/sidekicks.tyrr", "sidekick"),
            ("data/items/specials.tyrr", "special"),
            ("data/enemy/enemies.tyrr", "enemy")
        };
        foreach (var (file, header) in categories)
        {
            var path = Path.Combine(working, file.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var records = System.Text.RegularExpressions.Regex.Matches(fixture,
                $@"(?m)^\[{header}:[^\]]+\]\n(?:[^\[]*)");
            File.WriteAllText(path, string.Concat(records.Select(match => match.Value)), Encoding.Latin1);
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class IdentityConverter : IAudioSampleConverter
    {
        public short[] Convert(ReadOnlySpan<sbyte> samples)
            => samples.ToArray().Select(sample => (short)sample).ToArray();
    }
}
