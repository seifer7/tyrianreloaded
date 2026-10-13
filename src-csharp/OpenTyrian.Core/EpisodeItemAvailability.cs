/*
 * OpenTyrian: A modern cross-platform port of Tyrian
 * Copyright (C) 2007-2013 The OpenTyrian Development Team
 * Distributed under the GNU General Public License, version 2 or later.
 * See COPYING for details.
 */

namespace OpenTyrian.Core;

public sealed class EpisodeItemAvailabilityState
{
    public byte[,] Items { get; } = new byte[9, 10];
    public byte[] Counts { get; } = new byte[9];
}

public static class EpisodeItemAvailability
{
    private static readonly ItemType[] StoreTypes =
    [
        ItemType.Ship, ItemType.WeaponFront, ItemType.WeaponRear, ItemType.Generator,
        ItemType.Count, ItemType.SidekickLeft, ItemType.SidekickRight, ItemType.Count,
        ItemType.Shield,
    ];

    public static void Read(Stream episode, int shopContentSetting, ItemDatabase items,
        EpisodeItemAvailabilityState state, Action showItemScreen)
    {
        ArgumentNullException.ThrowIfNull(episode);
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(showItemScreen);
        if (!episode.CanRead)
            throw new ArgumentException("Episode item-availability input must be readable.", nameof(episode));

        Array.Clear(state.Items);
        Array.Clear(state.Counts);
        var encryptedLine = new byte[256];
        for (var row = 0; row < StoreTypes.Length; row++)
        {
            Array.Clear(encryptedLine);
            EncryptedText.ReadPascalString(episode, encryptedLine);
            var end = Array.IndexOf(encryptedLine, (byte)0);
            var length = end < 0 ? encryptedLine.Length : end;
            var remaining = length > 8 ? encryptedLine.AsMemory(8, length - 8) : ReadOnlyMemory<byte>.Empty;
            var reader = new TextTokenReader(remaining);
            var type = StoreTypes[row];
            var count = 0;

            if (shopContentSetting < 2)
            {
                while (count < 10 && reader.TryReadInt32(out var numericId))
                {
                    if (!items.TryFind(type, numericId, out var index))
                    {
                        throw new InvalidDataException(
                            $"Episode shop row {row} references missing item ID {numericId} for {type}.");
                    }
                    state.Items[row, count++] = unchecked((byte)index);
                }
            }

            if (shopContentSetting > 0)
            {
                var available = new byte[10];
                for (var slot = 0; slot < count; slot++)
                    available[slot] = state.Items[row, slot];
                while (count < 5 && items.AddRandomShopItem(type, available, count))
                {
                    state.Items[row, count] = available[count];
                    count++;
                }
            }

            state.Counts[row] = unchecked((byte)count);
        }

        showItemScreen();
    }
}
