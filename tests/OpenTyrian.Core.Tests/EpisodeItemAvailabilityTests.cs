using System.Diagnostics;
using System.Text;
using OpenTyrian.Core;

namespace OpenTyrian.Core.Tests;

internal static class EpisodeItemAvailabilityTests
{
    private static readonly string[][] ItemIds =
    [
        ["12", "11", "8", "13", "2", "1"],
        ["39", "23", "25", "36", "1", "16"],
        ["24", "26", "37", "15"],
        ["2"],
        [],
        ["24"],
        ["24"],
        [],
        ["2", "4"],
    ];

    public static void Run(string? executable = null)
    {
        var trace = new StringBuilder();
        for (var mode = 0; mode < 15; mode++)
        {
            var shopSetting = mode - 1;
            var definitions = new ItemDefinitions();
            definitions.ParseBundle(Encoding.Latin1.GetBytes(ItemDatabaseTests.Fixture(0)), "episode-shop.tyrr");
            var database = ItemDatabase.FromDefinitions(definitions);
            var state = new EpisodeItemAvailabilityState();
            var screenCalls = 0;
            using var episode = MakeEpisode(mode);
            trace.AppendLine($"case:{mode},{shopSetting}");
            EpisodeItemAvailability.Read(episode, shopSetting, database, state, () => screenCalls++);
            trace.AppendLine($"state:{episode.Position},{screenCalls},{Dump(state)}");
            AssertState(mode, shopSetting, episode, state, screenCalls, database);
        }

        TestRandomOffers();

        if (executable is null) return;
        using var process = Process.Start(new ProcessStartInfo(executable)
        { RedirectStandardOutput = true, RedirectStandardError = true })
            ?? throw new InvalidOperationException("Cannot start episode item-availability reference.");
        var output = process.StandardOutput.ReadToEndAsync();
        var errors = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(30000))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException("Episode item-availability reference timed out.");
        }

        var expected = output.GetAwaiter().GetResult().Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var actual = trace.ToString().Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        if (process.ExitCode != 0 || !expected.SequenceEqual(actual))
        {
            var index = Enumerable.Range(0, Math.Min(expected.Length, actual.Length))
                .FirstOrDefault(i => expected[i] != actual[i], -1);
            throw new InvalidOperationException($"Episode item availability differs at {index}: " +
                $"{expected.ElementAtOrDefault(index)} / {actual.ElementAtOrDefault(index)}; " +
                errors.GetAwaiter().GetResult());
        }
    }

    private static MemoryStream MakeEpisode(int mode)
    {
        var stream = new MemoryStream();
        for (var row = 0; row < ItemIds.Length; row++)
        {
            var values = ItemIds[row];
            if (values.Length > 0 && (mode & 1) != 0)
                values = values.Concat(values.Take(5)).ToArray();
            WriteEncryptedString(stream, Encoding.ASCII.GetBytes("abcdefgh" + string.Join(' ', values)));
        }
        stream.Position = 0;
        return stream;
    }

    private static void WriteEncryptedString(Stream stream, ReadOnlySpan<byte> plaintext)
    {
        stream.WriteByte(checked((byte)plaintext.Length));
        Span<byte> encrypted = stackalloc byte[255];
        ReadOnlySpan<byte> key = [204, 129, 63, 255, 71, 19, 25, 62, 1, 99];
        byte previous = 0;
        for (var index = 0; index < plaintext.Length; index++)
        {
            encrypted[index] = (byte)(plaintext[index] ^ key[index % key.Length] ^ previous);
            previous = encrypted[index];
        }
        stream.Write(encrypted[..plaintext.Length]);
    }

    private static string Dump(EpisodeItemAvailabilityState state)
    {
        var rows = new string[9];
        for (var row = 0; row < rows.Length; row++)
        {
            var items = new byte[10];
            for (var column = 0; column < items.Length; column++)
                items[column] = state.Items[row, column];
            rows[row] = $"{state.Counts[row]}:{string.Join(',', items)}";
        }
        return string.Join(';', rows);
    }

    private static void AssertState(int mode, int shopSetting, MemoryStream episode,
        EpisodeItemAvailabilityState state, int screenCalls, ItemDatabase database)
    {
        if (episode.Position != episode.Length || screenCalls != 1)
            throw new InvalidOperationException($"Episode item reader stream/UI state differs in scenario {mode}.");

        for (var row = 0; row < ItemIds.Length; row++)
        {
            var ids = shopSetting < 2 ? ItemIds[row] : [];
            if (ids.Length > 0 && (mode & 1) != 0)
                ids = ids.Concat(ids.Take(5)).ToArray();
            ids = ids.Take(10).ToArray();
            var expected = ids.Select(id => unchecked((byte)database.Index(RowType(row), int.Parse(id,
                System.Globalization.CultureInfo.InvariantCulture)))).ToArray();
            if (state.Counts[row] != expected.Length)
                throw new InvalidOperationException($"Episode item count differs in scenario {mode}, row {row}.");
            for (var column = 0; column < expected.Length; column++)
                if (state.Items[row, column] != expected[column])
                    throw new InvalidOperationException($"Episode item index differs in scenario {mode}, row {row}.");
            for (var column = expected.Length; column < 10; column++)
                if (state.Items[row, column] != 0)
                    throw new InvalidOperationException($"Episode item clear differs in scenario {mode}, row {row}.");
        }
    }

    private static void TestRandomOffers()
    {
        var fixture = ItemDatabaseTests.Fixture(0);
        if (!fixture.Contains("probability=0\n", StringComparison.Ordinal))
            throw new InvalidOperationException("Item fixture no longer has zero-probability shop entries.");
        fixture = fixture.Replace("probability=0\n", "probability=100\n", StringComparison.Ordinal);
        var definitions = new ItemDefinitions();
        definitions.ParseBundle(Encoding.Latin1.GetBytes(fixture), "episode-shop-random.tyrr");
        var expectedDatabase = ItemDatabase.FromDefinitions(definitions);
        var expectedItems = new byte[9, 10];
        var expectedCounts = new byte[9];
        MersenneTwister.Seed(1234);
        for (var row = 0; row < 9; row++)
        {
            var available = new byte[10];
            while (expectedCounts[row] < 5 &&
                expectedDatabase.AddRandomShopItem(RowType(row), available, expectedCounts[row]))
            {
                expectedItems[row, expectedCounts[row]] = available[expectedCounts[row]];
                expectedCounts[row]++;
            }
        }

        var actualDefinitions = new ItemDefinitions();
        actualDefinitions.ParseBundle(Encoding.Latin1.GetBytes(fixture), "episode-shop-random.tyrr");
        var actualDatabase = ItemDatabase.FromDefinitions(actualDefinitions);
        MersenneTwister.Seed(1234);
        using var episode = MakeEmptyEpisode();
        var actual = new EpisodeItemAvailabilityState();
        var screenCalls = 0;
        EpisodeItemAvailability.Read(episode, 1, actualDatabase, actual, () => screenCalls++);
        if (screenCalls != 1 || episode.Position != episode.Length)
            throw new InvalidOperationException("Episode item random-offer stream/UI state differs.");
        for (var row = 0; row < 9; row++)
        {
            if (actual.Counts[row] != expectedCounts[row])
                throw new InvalidOperationException($"Episode random-offer count differs in row {row}.");
            for (var slot = 0; slot < 10; slot++)
                if (actual.Items[row, slot] != expectedItems[row, slot])
                    throw new InvalidOperationException($"Episode random-offer item differs in row {row}.");
        }
    }

    private static MemoryStream MakeEmptyEpisode()
    {
        var stream = new MemoryStream();
        for (var row = 0; row < 9; row++)
            WriteEncryptedString(stream, "abcdefgh"u8);
        stream.Position = 0;
        return stream;
    }

    private static ItemType RowType(int row) => row switch
    {
        0 => ItemType.Ship,
        1 => ItemType.WeaponFront,
        2 => ItemType.WeaponRear,
        3 => ItemType.Generator,
        4 or 7 => ItemType.Count,
        5 => ItemType.SidekickLeft,
        6 => ItemType.SidekickRight,
        8 => ItemType.Shield,
        _ => throw new ArgumentOutOfRangeException(nameof(row)),
    };
}
