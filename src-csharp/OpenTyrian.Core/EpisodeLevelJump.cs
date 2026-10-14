/*
 * OpenTyrian: A modern cross-platform port of Tyrian
 * Copyright (C) 2007-2013 The OpenTyrian Development Team
 * Distributed under the GNU General Public License, version 2 or later.
 * See COPYING for details.
 */

namespace OpenTyrian.Core;

public sealed class EpisodeLevelJumpState
{
    public byte MainLevel { get; set; }
    public bool JumpSection { get; set; }
    public bool TwoPlayerMode { get; set; }
    public bool OnePlayerAction { get; set; }
    public bool LevelTimer { get; set; }
    public ushort TimerCountdown { get; set; }
    public sbyte InitialDifficulty { get; set; }
    public byte PlayerShip { get; set; }
    public byte StalkerShip { get; set; }
    public Func<bool> AllPlayersAlive { get; set; } = static () => true;
}

public static class EpisodeLevelJump
{
    private const sbyte HardDifficulty = 3;

    public static void Handle(EpisodeLevelJumpState state, char command, ReadOnlySpan<byte> section)
    {
        ArgumentNullException.ThrowIfNull(state);
        var destination = ParseAtoi(section[(command == 'H' ? 4 : 3)..]);

        switch (command)
        {
            case 'J':
                Jump();
                break;
            case '2':
                if (state.TwoPlayerMode || state.OnePlayerAction)
                    Jump();
                break;
            case 'w':
                if (state.PlayerShip == state.StalkerShip)
                    Jump();
                break;
            case 't':
                if (state.LevelTimer && state.TimerCountdown == 0)
                    Jump();
                break;
            case 'l':
                if (!state.AllPlayersAlive())
                    Jump();
                break;
            case 'H':
                if (state.InitialDifficulty < HardDifficulty)
                    Jump();
                break;
        }

        void Jump()
        {
            state.MainLevel = unchecked((byte)destination);
            state.JumpSection = true;
        }
    }

    internal static int ParseAtoi(ReadOnlySpan<byte> text)
    {
        var index = 0;
        while (index < text.Length && text[index] is 9 or 10 or 11 or 12 or 13 or 32)
            index++;

        var negative = false;
        if (index < text.Length && text[index] is (byte)'+' or (byte)'-')
        {
            negative = text[index] == '-';
            index++;
        }

        long value = 0;
        var digits = 0;
        while (index < text.Length && text[index] is >= (byte)'0' and <= (byte)'9')
        {
            value = checked(value * 10 + text[index] - (byte)'0');
            digits++;
            index++;
        }

        if (digits == 0)
            return 0;
        return checked((int)(negative ? -value : value));
    }
}
