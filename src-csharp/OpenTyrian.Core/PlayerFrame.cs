/*
 * OpenTyrian: A modern cross-platform port of Tyrian
 * Copyright (C) 2007-2013 The OpenTyrian Development Team
 * Distributed under the GNU General Public License, version 2 or later.
 * See COPYING for details.
 */

using System.Buffers.Binary;

namespace OpenTyrian.Core;

public sealed class PlayerFramePresentation
{
    public required IndexedSurface Game { get; init; }
    public required IndexedSurface Hud { get; init; }
    public required Action<int, int, int, bool, SpriteEffect> DrawShip { get; init; }
    public required Action<int, int, int, bool> DrawSidekick { get; init; }
    public required Action<int, int, int> DrawTrail { get; init; }
    public required Action DrawModeButtons { get; init; }
    public required Action<int, int, int> DrawIndicator { get; init; }
    public required Action<int, int, int> DrawCharge { get; init; }
    public required Action DrawOptions { get; init; }
    public required Action DrawDemoText { get; init; }
}

public sealed class PlayerFrame(IReadOnlyList<PlayerState> players, PlayerCombat combat, LevelEvents level,
    PlayerMovement movement, PlayerInput input, PlayerWeapons weapons, PlayerSpecialWeapons special,
    PlayerShots shots, ItemDatabase items, SceneEffects effects, EnemySystem enemies)
{
    public PlayerInput Input => input;
    public bool[] Buttons { get; } = new bool[4];
    public byte Episode { get; set; } = 1;
    public bool Background2 { get; set; }
    public bool SuperWild { get; set; }
    public int MapX2Offset { get; set; }
    public uint ShadowDistance { get; set; } = 10;
    public NetworkSession? Network { get; set; }
    public Stream? DemoPlayback { get; set; }
    public Stream? DemoRecording { get; set; }
    public byte[] Devices { get; } = new byte[2];
    public ushort[] Graphics { get; } = new ushort[2];
    public ushort[] OldX { get; } = new ushort[2];
    public ushort[] OldY { get; } = new ushort[2];
    public ushort MapXOffset { get; set; }
    public ushort OldMapXOffset { get; set; }
    public ushort MapX3Offset { get; set; }

    public void UpdatePlayers(IReadOnlyList<PlayerFramePresentation> presentation, BackgroundRenderer backgrounds)
    {
        if (presentation.Count != 2) throw new ArgumentException("Player frames require two presentations.", nameof(presentation));
        if (level.EndLevel && combat.LevelEnd > 0)
        {
            combat.LevelEnd--;
            movement.LevelEndWarp = unchecked((sbyte)(movement.LevelEndWarp + 1));
        }
        Array.Clear(weapons.ExecutedSpecial);
        weapons.ModeChangeRequested = false;
        movement.SecondShipGraphic = Graphics[1];
        if (combat.TwoPlayers)
        {
            Update(1, combat.Galaga ? (byte)0 : Devices[0], Graphics[0], ref OldX[0], ref OldY[0], presentation[0]);
            Update(2, combat.Galaga ? (byte)0 : Devices[1], Graphics[1], ref OldX[1], ref OldY[1], presentation[1]);
        }
        else Update(1, 0, Graphics[0], ref OldX[0], ref OldY[0], presentation[0]);

        var center = unchecked((ushort)(combat.TwoPlayers ? (players[0].X + players[1].X) / 2 : players[0].X));
        MapX3Offset = unchecked((ushort)(int)MathF.Floor((260f - (center - 36f)) / (260f - 36f) * (24f * 3f) - 1f));
        backgrounds.Third.X = (ushort)(MapX3Offset % 24);
        backgrounds.Third.HorizontalTileOffset = 1 - MapX3Offset / 24;
        MapX2Offset = MapX3Offset * 2 / 3;
        backgrounds.Second.X = (ushort)(MapX2Offset % 24);
        backgrounds.Second.HorizontalTileOffset = 1 - MapX2Offset / 24;
        OldMapXOffset = MapXOffset;
        MapXOffset = (ushort)(MapX2Offset / 2);
        backgrounds.First.X = (ushort)(MapXOffset % 24);
        backgrounds.First.HorizontalTileOffset = 1 - MapXOffset / 24;
        if (level.ThirdAtFirstX)
        {
            MapX3Offset = MapXOffset;
            backgrounds.Third.X = backgrounds.First.X;
            backgrounds.Third.HorizontalTileOffset = backgrounds.First.HorizontalTileOffset - 1;
        }
    }

    public void Update(byte playerNumber, byte device, ushort graphic, ref ushort oldX, ref ushort oldY,
        PlayerFramePresentation presentation)
    {
        if (playerNumber is < 1 or > 2) throw new ArgumentOutOfRangeException(nameof(playerNumber));
        if (players.Count != 2) throw new InvalidOperationException("Player frames require both player states.");
        if (input.NetworkGame && Network is null) throw new InvalidOperationException("Network player frames require a network session.");
        if (input.NetworkGame && input.LocalPlayer is < 1 or > 2) throw new InvalidDataException("Local network player must be one or two.");
        var player = players[playerNumber - 1];
        if (playerNumber == 2 || !combat.TwoPlayers)
        {
            var rear = player.Items.Weapons[1].Id;
            if (rear >= items.Weapons.Count) throw new InvalidDataException("Player rear weapon exceeds loaded item definitions.");
            if (player.WeaponMode > items.Weapons[rear].OpNum) player.WeaponMode = 1;
        }
        if (input.NetworkGame && input.LocalPlayer == playerNumber)
        {
            Network!.PrepareState();
            Network.OutgoingState.Slice(4, 10).Clear();
        }

        while (combat.UpdateDeath(player, playerNumber, Episode, input.NetworkGame, level, shots.MultiPosition)) { }
        if (!player.IsAlive)
        {
            effects.FollowX = effects.FollowY = 0;
            return;
        }

        var state = new PlayerMovementState();
        if (!level.EndLevel)
        {
            oldX = unchecked((ushort)player.X); oldY = unchecked((ushort)player.Y);
            Array.Clear(Buttons);
            input.Collect(player, input.NetworkGame ? (byte)0 : device, playerNumber, ref oldX, ref oldY,
                state, Buttons, input.NetworkGame && input.LocalPlayer == playerNumber ? Network!.OutgoingState : [],
                input.PlayDemo ? Replay : null, DemoRecording);
            movement.MoveAllowed = true;
            if (input.NetworkGame && !Network!.StateIsReset)
            {
                var remote = playerNumber != input.LocalPlayer;
                var packet = remote ? Network.IncomingState : Network.OutgoingStates[Network.DelayFrames];
                if (packet is null) throw new InvalidOperationException("Delayed player input is unavailable.");
                var data = packet.Data.Span;
                if (data.Length < (remote ? 18 : 14)) throw new InvalidDataException("Delayed player input packet is truncated.");
                if (remote && input.LocalPlayer == 2)
                    enemies.Difficulty = unchecked((sbyte)BinaryPrimitives.ReadUInt16BigEndian(data[16..]));
                var bits = BinaryPrimitives.ReadUInt16BigEndian(data[12..]);
                for (var i = 0; i < 4; i++) Buttons[i] = (bits & (1 << i)) != 0;
                player.X += BinaryPrimitives.ReadInt16BigEndian(data[4..]);
                player.Y += BinaryPrimitives.ReadInt16BigEndian(data[6..]);
                state.AccelerationX = BinaryPrimitives.ReadInt16BigEndian(data[8..]);
                state.AccelerationY = BinaryPrimitives.ReadInt16BigEndian(data[10..]);
            }
            movement.UpdateLinking(player, playerNumber, oldX, oldY, state, Buttons, weapons, level.SuperTyrian);
        }

        OldX[playerNumber - 1] = oldX; OldY[playerNumber - 1] = oldY;
        movement.DrawLevelEnd(player, graphic, presentation.DrawTrail);
        if (input.PlayDemo) presentation.DrawDemoText();
        movement.UpdateMovement(player, playerNumber, graphic, oldX, oldY, state, Buttons);
        movement.DrawShip(player, playerNumber, graphic, oldX, oldY, input.NetworkGame, Background2,
            SuperWild, MapX2Offset, ShadowDistance, effects, presentation.DrawShip);
        weapons.UpdateAttacks(player, playerNumber, graphic, oldX, oldY, Buttons, special.ArcadeMode,
            enemies.Difficulty, level.EndLevel, movement, special, presentation.Game, presentation.Hud,
            presentation.DrawModeButtons, presentation.DrawIndicator, presentation.DrawCharge, presentation.DrawOptions,
            (OldX[0], OldY[0]));
        weapons.DrawSidekicks(player, playerNumber, level.EndLevel, presentation.DrawSidekick);

        bool Replay() => input.ReplayDemo(DemoPlayback ?? throw new InvalidOperationException("Demo playback requires an input stream."), players, Buttons);
    }
}
