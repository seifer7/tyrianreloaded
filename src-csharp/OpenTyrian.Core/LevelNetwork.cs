/*
 * OpenTyrian: A modern cross-platform port of Tyrian
 * Copyright (C) 2007-2013 The OpenTyrian Development Team
 * Distributed under the GNU General Public License, version 2 or later.
 * See COPYING for details.
 */

using System.Buffers.Binary;

namespace OpenTyrian.Core;

public interface ILevelNetworkServices
{
    void Pause();
    bool ShowInGameSetup(bool yourRequest);
    void DrawDesynchronization(int playerNumber);
}

public sealed class LevelNetworkServices(Action pause, Func<bool, bool> setup, IndexedSurface screen,
    FontRenderer font) : ILevelNetworkServices
{
    public void Pause() => pause();
    public bool ShowInGameSetup(bool yourRequest) => setup(yourRequest);
    public void DrawDesynchronization(int playerNumber)
    {
        if (playerNumber is < 1 or > 2) throw new ArgumentOutOfRangeException(nameof(playerNumber));
        var text = System.Text.Encoding.Latin1.GetBytes($"Player {playerNumber} is unsynchronized!");
        font.ShadeText(screen, 40, 110 + (playerNumber - 1) * 10, text, 9, 2, TextShade.Full);
    }
}

public static class InGameChat
{
    public static void Handle() { }
}

public sealed partial class LevelEvents
{
    public void UpdateNetworkState(bool isNetworkGame, NetworkSession session, PlayerCombat combat,
        ILevelNetworkServices services, ref ushort shipGraphic, CancellationToken cancellation = default)
    {
        if (!isNetworkGame) return;
        if (!ReallyEndLevel)
        {
            var requests = session.Requests;
            Write(14, (requests.Pause ? 1 : 0) | (requests.InGameMenu ? 2 : 0) |
                (requests.SkipLevel ? 4 : 0) | (requests.NortShip ? 8 : 0));
            Write(16, enemies.Difficulty);
            Write(18, players[0].X); Write(20, players[1].X);
            Write(22, players[0].Y); Write(24, players[1].Y); Write(26, Location);
            session.SendState();
            if (session.UpdateState(cancellation))
            {
                var received = session.IncomingState!.Data;
                var delayed = session.OutgoingStates[session.DelayFrames]!.Data;
                if (Read(received, 26) != Read(delayed, 26))
                    throw new InvalidDataException("Network level locations are unsynchronized.");
                var localRequests = Read(delayed, 14);
                var changed = Read(received, 14) ^ localRequests;
                if ((changed & 1) != 0) services.Pause();
                if ((changed & 2) != 0)
                {
                    requests.YourInGameMenu = (localRequests & 2) != 0;
                    bool halt;
                    try { halt = services.ShowInGameSetup(requests.YourInGameMenu); }
                    finally { requests.YourInGameMenu = false; }
                    if (halt) ReallyEndLevel = true;
                }
                if ((changed & 4) != 0)
                {
                    combat.LevelTimer = true; TimerCountdown = 0;
                    EndLevel = true; combat.LevelEnd = 40;
                }
                if ((changed & 8) != 0)
                {
                    players[0].Items.Ship = checked((byte)items.KnownIndex(KnownItem.ShipNortship));
                    players[0].Items.Special = checked((byte)items.KnownIndex(KnownItem.SpecialAstralZone));
                    players[0].Items.Weapons[0].Id = checked((byte)items.KnownIndex(KnownItem.WeaponFrontNortshipSuperPulse));
                    players[0].Items.Weapons[1].Id = checked((byte)items.KnownIndex(KnownItem.WeaponRearNortshipSpreader));
                    shipGraphic = 1;
                }
                for (var i = 0; i < 2; i++)
                {
                    // Preserve the native diagnostic's overlapping offsets (not X/Y pairs).
                    if (Read(received, 18 + i * 2) != Read(delayed, 18 + i * 2) ||
                        Read(received, 20 + i * 2) != Read(delayed, 20 + i * 2))
                        services.DrawDesynchronization(i + 1);
                }
            }
        }
        session.Requests.ClearSpecialRequests();

        void Write(int offset, int value) =>
            BinaryPrimitives.WriteUInt16BigEndian(session.OutgoingState[offset..], unchecked((ushort)value));
    }

    private static ushort Read(ReadOnlyMemory<byte> packet, int offset) =>
        BinaryPrimitives.ReadUInt16BigEndian(packet.Span[offset..]);
}
