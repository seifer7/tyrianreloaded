/*
 * OpenTyrian: A modern cross-platform port of Tyrian
 * Copyright (C) 2007-2009 The OpenTyrian Development Team
 * Distributed under the GNU General Public License, version 2 or later.
 * See COPYING for details.
 */

namespace OpenTyrian.Core;

public sealed class LevelArchive
{
    public ushort Count { get; private set; }
    public int[] Positions { get; } = new int[43];

    public void Analyze(Stream stream)
    {
        Span<ushort> count = stackalloc ushort[1];
        BinaryFile.ReadUInt16(stream, count);
        if (count[0] > 42)
        {
            throw new InvalidDataException("Level archive exceeds the native 42-entry limit.");
        }

        Count = count[0];
        BinaryFile.ReadInt32(stream, Positions.AsSpan(0, Count));
        Positions[Count] = checked((int)stream.Length);
    }
}

public sealed class EpisodeState(DataFiles files)
{
    public byte Number { get; private set; }
    public string LevelFile { get; private set; } = "";
    public string CubeFile { get; private set; } = "";
    public string EpisodeFile { get; private set; } = "";
    public bool[] Available { get; } = new bool[5];
    public bool JumpBackToFirst { get; private set; }
    public bool GameHasRepeated { get; set; }
    public LevelArchive Levels { get; } = new();

    public void Initialize(byte number)
    {
        if (number == Number)
        {
            return;
        }

        Number = number;
        LevelFile = $"tyrian{number}.lvl";
        CubeFile = $"cubetxt{number}.dat";
        EpisodeFile = $"levels{number}.dat";
        using var stream = files.OpenRequired(files.Directory, LevelFile);
        Levels.Analyze(stream);
    }

    public void Scan()
    {
        for (var index = 0; index < Available.Length; index++)
        {
            Available[index] = files.Exists(files.Directory, $"tyrian{index + 1}.lvl");
        }
    }

    public uint FindNext()
    {
        uint next = Number;
        JumpBackToFirst = false;
        // Native callers initialize the episode before searching. Reject the
        // uninitialized no-assets case instead of reproducing an infinite loop.
        if (Number == 0 && !Available.Any(value => value))
        {
            throw new InvalidOperationException("Cannot select an episode before initialization when none are available.");
        }

        do
        {
            next++;
            if (next > 5)
            {
                next = 1;
                JumpBackToFirst = true;
                GameHasRepeated = true;
            }
        } while (!Available[next - 1] && next != Number);

        return next;
    }
}
