/*
 * OpenTyrian: A modern cross-platform port of Tyrian
 * Copyright (C) 2007-2009 The OpenTyrian Development Team
 * Distributed under the GNU General Public License, version 2 or later.
 * See COPYING for details.
 */

namespace OpenTyrian.Core;

public sealed class HelpTextData
{
    public int EpisodeOneDataOffset { get; private set; }
    public byte[][] Help { get; } = Create(39, 231);
    public byte[][] PlanetNames { get; } = Create(21, 16);
    public byte[][] MiscText { get; } = Create(68, 42);
    public byte[][] MiscTextB { get; } = Create(5, 11);
    public byte[][] KeyNames { get; } = Create(8, 18);
    public byte[][] MenuText { get; } = Create(7, 21);
    public byte[][] Outputs { get; } = Create(9, 31);
    public byte[][] TopicNames { get; } = Create(6, 21);
    public byte[][] MainMenuHelp { get; } = Create(34, 66);
    public byte[][] InGameText { get; } = Create(6, 21);
    public byte[][] DetailLevels { get; } = Create(6, 13);
    public byte[][] GameSpeeds { get; } = Create(5, 13);
    public byte[][] EpisodeNames { get; } = Create(6, 31);
    public byte[][] DifficultyNames { get; } = Create(7, 21);
    public byte[][] GameplayNames { get; } = Create(5, 26);
    public byte[][] InputDevices { get; } = Create(3, 13);
    public byte[][] NetworkText { get; } = Create(4, 22);
    public byte[][] HighScoreDifficultyNames { get; } = Create(11, 21);
    public byte[][] JoystickButtonNames { get; } = Create(5, 21);
    public byte[][] SuperShips { get; } = Create(11, 26);
    public byte[][] SpecialNames { get; } = Create(9, 10);
    public byte[][] DestructHelp { get; } = Create(25, 22);
    public byte[][] WeaponNames { get; } = Create(17, 17);
    public byte[][] DestructModeNames { get; } = Create(5, 13);
    public byte[][][] ShipInfo { get; } = Enumerable.Range(0, 13).Select(_ => Create(2, 256)).ToArray();
    public byte[][][] MenuEntries { get; } = Enumerable.Range(0, 15).Select(_ => Create(11, 18)).ToArray();

    public static ReadOnlySpan<byte> MenuHelp =>
    [
        1, 34, 2, 3, 4, 5, 0, 0, 0, 0, 0,
        6, 7, 8, 9, 10, 11, 11, 12, 0, 0, 0,
        13, 14, 15, 15, 16, 17, 12, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        4, 30, 30, 3, 5, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        16, 17, 15, 15, 12, 0, 0, 0, 0, 0, 0,
        31, 31, 31, 31, 32, 12, 0, 0, 0, 0, 0,
        4, 34, 3, 5, 0, 0, 0, 0, 0, 0, 0
    ];

    public void Load(Stream stream)
    {
        Span<int> offset = stackalloc int[1];
        BinaryFile.ReadInt32(stream, offset);
        EpisodeOneDataOffset = offset[0];
        byte[][][] groups =
        [
            Help, PlanetNames, MiscText, MiscTextB, MenuEntries[6], MenuText,
            Outputs, TopicNames, MainMenuHelp, MenuEntries[1][..7], MenuEntries[2][..9],
            MenuEntries[3][..8], InGameText, DetailLevels, GameSpeeds, EpisodeNames,
            DifficultyNames, GameplayNames, MenuEntries[10][..6], InputDevices,
            NetworkText, MenuEntries[11][..4], HighScoreDifficultyNames,
            MenuEntries[12][..6], MenuEntries[13][..7], JoystickButtonNames,
            SuperShips, SpecialNames, DestructHelp, WeaponNames, DestructModeNames,
            ShipInfo.SelectMany(pair => pair).ToArray(), MenuEntries[14][..5]
        ];
        for (var group = 0; group < groups.Length; group++)
        {
            EncryptedText.SkipPascalString(stream);
            foreach (var entry in groups[group])
            {
                EncryptedText.ReadPascalString(stream, entry);
            }

            if (group != groups.Length - 1)
            {
                EncryptedText.SkipPascalString(stream);
            }
        }
    }

    private static byte[][] Create(int count, int size) => Enumerable.Range(0, count).Select(_ => new byte[size]).ToArray();
}
