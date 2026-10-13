/*
 * OpenTyrian: A modern cross-platform port of Tyrian
 * Copyright (C) 2007-2013 The OpenTyrian Development Team
 * Distributed under the GNU General Public License, version 2 or later.
 * See COPYING for details.
 */

using System.Globalization;
using System.Text;

namespace OpenTyrian.Core;

public sealed class EpisodeCompletionState
{
    public byte SecretHint { get; set; }
    public bool TwoPlayerMode { get; set; }
    public bool ConstantPlay { get; set; }
    public bool NetworkGame { get; set; }
    public bool EscapePressed { get; set; }
    public bool JumpBackToEpisode1 { get; set; }
    public bool SuperTyrian { get; set; }
    public bool JumpSection { get; set; }
    public bool TrentWin { get; set; }
    public sbyte InitialDifficulty { get; set; }
    public byte SuperArcadeMode { get; set; }
    public byte MainLevel { get; set; }
    public ushort FrameCountMax { get; set; }
    public byte WarningLines { get; set; }
    public ulong TotalScore { get; set; }
    public byte[][] WarningText { get; } = Enumerable.Range(0, 10).Select(_ => Array.Empty<byte>()).ToArray();
    public ulong[] Cash { get; } = new ulong[2];
}

public sealed class EpisodeSuperArcadeData(
    IReadOnlyList<byte[]> superShips,
    IReadOnlyList<byte[]> specialNames,
    ReadOnlyMemory<byte> engageTitle,
    ReadOnlyMemory<byte> playerName,
    IReadOnlyList<ushort> shipGraphics,
    IReadOnlyList<byte> nextShip,
    IReadOnlyList<byte> shipLookup)
{
    public IReadOnlyList<byte[]> SuperShips { get; } = superShips;
    public IReadOnlyList<byte[]> SpecialNames { get; } = specialNames;
    public ReadOnlyMemory<byte> EngageTitle { get; } = engageTitle;
    public ReadOnlyMemory<byte> PlayerName { get; } = playerName;
    public IReadOnlyList<ushort> ShipGraphics { get; } = shipGraphics;
    public IReadOnlyList<byte> NextShip { get; } = nextShip;
    public IReadOnlyList<byte> ShipLookup { get; } = shipLookup;
}

public interface IEpisodeCompletionHost
{
    void WipeKey();
    void DisplayText();
    void FadeBlack(int steps);
    void NextEpisode();
    void LoadPicture(int number);
    void Clear();
    void DrawCentered(int y, ReadOnlyMemory<byte> text, int font);
    void DrawText(int x, int y, ReadOnlyMemory<byte> text, int font);
    void DrawShip(int x, int y, ushort graphic);
    void Present();
    void FadePalette(int steps, int firstColor, int lastColor);
    void WaitInput(bool clearEvents, bool allowKeyboard, bool allowMouse);
    void ReadTextSync();
}

public sealed class EpisodeCompletion
{
    private readonly EpisodeCompletionState _state;
    private readonly ReadOnlyMemory<byte> _singlePlayerScoreLabel;
    private readonly IReadOnlyList<ReadOnlyMemory<byte>> _playerCashLabels;
    private readonly EpisodeSuperArcadeData _superArcade;
    private readonly IEpisodeCompletionHost _host;

    public EpisodeCompletion(EpisodeCompletionState state, ReadOnlyMemory<byte> singlePlayerScoreLabel,
        IReadOnlyList<ReadOnlyMemory<byte>> playerCashLabels, EpisodeSuperArcadeData superArcade,
        IEpisodeCompletionHost host)
    {
        _state = state ?? throw new ArgumentNullException(nameof(state));
        _singlePlayerScoreLabel = singlePlayerScoreLabel;
        _playerCashLabels = playerCashLabels ?? throw new ArgumentNullException(nameof(playerCashLabels));
        _superArcade = superArcade ?? throw new ArgumentNullException(nameof(superArcade));
        _host = host ?? throw new ArgumentNullException(nameof(host));
    }

    public bool Show(Stream episode)
    {
        ArgumentNullException.ThrowIfNull(episode);
        if (!episode.CanRead)
            throw new ArgumentException("Episode completion input must be readable.", nameof(episode));
        if (_playerCashLabels.Count < 2)
            throw new ArgumentException("Episode completion requires both player cash labels.", nameof(_playerCashLabels));

        _state.EscapePressed = false;
        var hintSection = _state.SecretHint + (MersenneTwister.NextUInt32() % 3) * 3;
        if (_state.TwoPlayerMode)
        {
            for (var player = 0; player < 2; player++)
                _state.WarningText[player] = FormatLine(_playerCashLabels[player].Span, _state.Cash[player]);
            _state.WarningText[2] = [];
            _state.WarningLines = 3;
        }
        else
        {
            _state.WarningText[0] = FormatLine(_singlePlayerScoreLabel.Span, _state.TotalScore);
            _state.WarningText[1] = [];
            _state.WarningLines = 2;
        }

        var textBuffer = new byte[256];
        for (var section = 0; section < hintSection - 1; section++)
            ReadThroughTerminator(episode, textBuffer);

        while (true)
        {
            Array.Clear(textBuffer);
            EncryptedText.ReadPascalString(episode, textBuffer);
            var length = NullTerminatedLength(textBuffer);
            if (textBuffer[0] == (byte)'#')
            {
                if (_state.WarningLines >= _state.WarningText.Length)
                    throw new InvalidDataException("Episode completion terminator exceeds the native ten-line buffer.");
                _state.WarningText[_state.WarningLines] = textBuffer.AsSpan(0, length).ToArray();
                _state.WarningLines++;
                break;
            }
            if (_state.WarningLines >= _state.WarningText.Length)
                throw new InvalidDataException("Episode completion exceeds the native ten-line buffer.");
            _state.WarningText[_state.WarningLines++] = textBuffer.AsSpan(0, length).ToArray();
        }
        _state.WarningLines--;

        _host.WipeKey();
        _state.FrameCountMax = 4;
        if (!_state.ConstantPlay)
            _host.DisplayText();
        _host.FadeBlack(15);
        _host.NextEpisode();

        if (!_state.JumpBackToEpisode1 || _state.TwoPlayerMode)
            return false;

        _host.LoadPicture(1);
        _host.Clear();
        if (_state.SuperTyrian)
            _state.SuperArcadeMode = _state.InitialDifficulty == 4 ? (byte)8 : (byte)1;

        if (_state.SuperArcadeMode < 9)
        {
            var unlock = TableValue(_superArcade.NextShip, _state.SuperArcadeMode, "next-ship");
            if (unlock == 9)
            {
                _host.DrawCentered(100, Join(_superArcade.EngageTitle.Span, _superArcade.PlayerName.Span), 0);
                _host.DrawText(80, 180, Join("Or play... "u8, TableValue(_superArcade.SpecialNames, 7, "special-name").Span), 1);
            }
            else
            {
                _host.DrawCentered(30, TableValue(_superArcade.SuperShips, 0, "super-ship"), 0);
                _host.DrawCentered(100, TableValue(_superArcade.SuperShips, unlock, "super-ship"), 1);
            }

            if (unlock < 7)
            {
                var shipIndex = TableValue(_superArcade.ShipLookup, unlock - 1, "ship-lookup");
                _host.DrawShip(148, 70, TableValue(_superArcade.ShipGraphics, shipIndex, "ship-graphic"));
            }
            else if (unlock == 7)
            {
                _state.TrentWin = true;
            }

            var codeText = Join(Join("Type "u8, TableValue(_superArcade.SpecialNames, unlock - 1, "special-name").Span),
                " at Title"u8);
            _host.DrawCentered(160, codeText, 1);
            _host.Present();
            _host.FadePalette(50, 0, 255);
            if (!_state.ConstantPlay)
                _host.WaitInput(true, true, true);
        }

        _state.JumpSection = true;
        if (_state.NetworkGame)
            _host.ReadTextSync();
        if (!_state.SuperTyrian)
            return false;

        _host.FadeBlack(10);
        _state.MainLevel = 0;
        return true;
    }

    private static void ReadThroughTerminator(Stream episode, Span<byte> buffer)
    {
        while (true)
        {
            buffer.Clear();
            EncryptedText.ReadPascalString(episode, buffer);
            if (buffer[0] == (byte)'#')
                return;
        }
    }

    private static int NullTerminatedLength(ReadOnlySpan<byte> value)
    {
        var length = value.IndexOf((byte)0);
        return length < 0 ? value.Length : length;
    }

    private static byte[] FormatLine(ReadOnlySpan<byte> label, ulong value)
    {
        var number = Encoding.ASCII.GetBytes(value.ToString(CultureInfo.InvariantCulture));
        return Join(label, number);
    }

    private static byte[] Join(ReadOnlySpan<byte> first, ReadOnlySpan<byte> second)
    {
        var addSpace = first.Length > 0 && second.Length > 0 &&
            first[^1] != (byte)' ' && second[0] != (byte)' ';
        var result = new byte[checked(first.Length + second.Length + (addSpace ? 1 : 0))];
        first.CopyTo(result);
        var offset = first.Length;
        if (addSpace)
            result[offset++] = (byte)' ';
        second.CopyTo(result.AsSpan(offset));
        return result;
    }

    private static ReadOnlyMemory<byte> TableValue(IReadOnlyList<byte[]> values, int index, string table)
    {
        if ((uint)index >= values.Count)
            throw new InvalidDataException($"Episode super-arcade {table} index {index} is outside the loaded table of {values.Count} entries.");
        return values[index];
    }

    private static ushort TableValue(IReadOnlyList<ushort> values, int index, string table)
    {
        if ((uint)index >= values.Count)
            throw new InvalidDataException($"Episode super-arcade {table} index is outside the loaded table.");
        return values[index];
    }

    private static byte TableValue(IReadOnlyList<byte> values, int index, string table)
    {
        if ((uint)index >= values.Count)
            throw new InvalidDataException($"Episode super-arcade {table} index is outside the loaded table.");
        return values[index];
    }
}
