/*
 * OpenTyrian: A modern cross-platform port of Tyrian
 * Copyright (C) 2007-2009 The OpenTyrian Development Team
 * Distributed under the GNU General Public License, version 2 or later.
 * See COPYING for details.
 */

namespace OpenTyrian.Core;

public sealed class PlayerMovementState
{
    public short MouseX { get; set; }
    public short MouseY { get; set; }
    public short AccelerationX { get; set; }
    public short AccelerationY { get; set; }
    public bool AnalogLinkGun { get; set; }
    public float LinkGunAngle { get; set; }
}

public sealed class PlayerMovement(IReadOnlyList<PlayerState> players, PlayerCombat combat, LevelEvents level,
    PlayerShots shots, ItemDatabase items)
{
    public bool MoveAllowed { get; set; }
    public float LinkGunDirection { get; set; }
    public ushort SecondShipGraphic { get; set; }
    public sbyte LevelEndWarp { get; set; }
    public float SatelliteRotation { get; set; }
    public bool AttachmentLinked { get; set; }
    public bool AttachmentReturn { get; set; }
    public short AttachmentMove { get; set; }
    public sbyte ConstantLastX { get; set; }

    public void DrawShip(PlayerState player, byte playerNumber, ushort graphic, ushort mouseX, ushort mouseY,
        bool networkGame, bool background2, bool superWild, int mapX2Offset, uint shadowDistance,
        SceneEffects effects, IndexedSurface surface, SpriteSheet sheet)
        => DrawShip(player, playerNumber, graphic, mouseX, mouseY, networkGame, background2, superWild,
            mapX2Offset, shadowDistance, effects, (x, y, frame, large, effect) =>
            {
                if (large) sheet.Blit2x2(surface, x, y, frame, effect);
                else sheet.Blit(surface, x, y, frame, effect);
            });

    public void DrawShip(PlayerState player, byte playerNumber, ushort graphic, ushort mouseX, ushort mouseY,
        bool networkGame, bool background2, bool superWild, int mapX2Offset, uint shadowDistance,
        SceneEffects effects, Action<int, int, int, bool, SpriteEffect> draw)
    {
        if (playerNumber is < 1 or > 2) throw new ArgumentOutOfRangeException(nameof(playerNumber));
        if (level.EndLevel) return;
        if (player.X > 256) { player.X = 256; ConstantLastX = unchecked((sbyte)-ConstantLastX); }
        if (player.X < 40) { player.X = 40; ConstantLastX = unchecked((sbyte)-ConstantLastX); }
        player.Y = Math.Clamp(player.Y, 10, networkGame && playerNumber == 1 ? 154 : 160);
        var banking = Math.Clamp(player.XVelocity / 2 + (player.X - mouseX) / 6, -2, 2);
        var sprite = banking * 2 + graphic;
        effects.FollowX = unchecked((short)(player.X - player.LastXExplosionFollow));
        effects.FollowY = unchecked((short)(player.Y - player.LastYExplosionFollow));
        if (effects.FollowY < 0) effects.FollowY = 0;
        player.LastXExplosionFollow = player.X;
        player.LastYExplosionFollow = player.Y;
        if (background2)
        {
            Ship(mapX2Offset - 30, unchecked((int)shadowDistance), SpriteEffect.Darken);
            if (superWild && graphic != 1)
            {
                if (graphic == 0)
                {
                    draw(player.X - 16 - mapX2Offset + 30, unchecked(player.Y - 7 + (int)shadowDistance), sprite + 13, true, SpriteEffect.Darken);
                    draw(player.X + 6 - mapX2Offset + 30, unchecked(player.Y - 7 + (int)shadowDistance), sprite + 51, true, SpriteEffect.Darken);
                }
                else draw(player.X - 4 - mapX2Offset + 30, unchecked(player.Y - 7 + (int)shadowDistance), sprite, true, SpriteEffect.Darken);
            }
        }
        if (player.InvulnerableTicks > 0)
        {
            player.InvulnerableTicks--;
            Ship(0, 0, SpriteEffect.Blend);
        }
        else
        {
            Ship(0, 0, SpriteEffect.Copy);
            if (graphic == 1 && banking != 0)
            {
                var left = banking > 0;
                draw(player.X + (left ? -17 : 19), player.Y + 7,
                    left ? 38 + banking : 57 - banking, false, SpriteEffect.Copy);
                if (shots.Repeat[9] > 0) shots.Repeat[9]--;
                else
                {
                    // Native supported toolchains evaluate the Y argument's RNG call first.
                    var y = unchecked((ushort)(player.Y + MersenneTwister.NextUInt32() % 8 - 4));
                    var x = unchecked((ushort)(player.X + (left ? -7 : 9) + MersenneTwister.NextUInt32() % 8 - 4));
                    shots.CreateTyped(0, PlayerShotSlot.NortSparks, x, y, mouseX, mouseY, 671, 1);
                    shots.Repeat[9] = (byte)(Math.Abs(banking) - 1);
                }
            }
        }
        if (playerNumber == 2 && graphic == 0)
        {
            if (player.Sidekicks[0].Style == 0)
            { player.Sidekicks[0].X = player.X - 14 + banking * 2; player.Sidekicks[0].Y = player.Y; }
            if (player.Sidekicks[1].Style == 0)
            { player.Sidekicks[1].X = player.X + 17 + banking * 2; player.Sidekicks[1].Y = player.Y; }
        }

        void Ship(int offsetX, int offsetY, SpriteEffect effect)
        {
            var y = unchecked(player.Y - 7 + offsetY);
            if (graphic is 0 or 1)
            {
                draw(player.X - 17 - offsetX, y, graphic == 0 ? sprite + 13 : 220, true, effect);
                draw(player.X + 7 - offsetX, y, graphic == 0 ? sprite + 51 : 222, true, effect);
            }
            else draw(player.X - 5 - offsetX, y, sprite, true, effect);
        }
    }

    public void UpdateLinking(PlayerState player, byte playerNumber, ushort oldX, ushort oldY,
        PlayerMovementState movement, ReadOnlySpan<bool> buttons, PlayerWeapons weapons, bool superTyrian)
    {
        Validate(playerNumber, buttons);
        weapons.RecognizeSpecial(playerNumber, unchecked((short)player.X), unchecked((short)player.Y),
            unchecked((short)oldX), unchecked((short)oldY), buttons, superTyrian);
        UpdateLinkingCore(player, playerNumber, oldX, oldY, movement, buttons);
    }

    public void UpdateLinking(PlayerState player, byte playerNumber, ushort oldX, ushort oldY,
        PlayerMovementState movement, ReadOnlySpan<bool> buttons, Action<byte, int, int, ushort, ushort> specialCodes)
    {
        Validate(playerNumber, buttons);
        specialCodes(playerNumber, player.X, player.Y, oldX, oldY);
        UpdateLinkingCore(player, playerNumber, oldX, oldY, movement, buttons);
    }

    private void UpdateLinkingCore(PlayerState player, byte playerNumber, ushort oldX, ushort oldY,
        PlayerMovementState movement, ReadOnlySpan<bool> buttons)
    {
        if (!MoveAllowed) return;
        if (combat.TwoPlayers && !combat.Linked && player.X == oldX && player.Y == oldY &&
            Math.Abs(players[0].X - players[1].X) < 8 && Math.Abs(players[0].Y - players[1].Y) < 8 &&
            players[0].IsAlive && players[1].IsAlive && !combat.Galaga)
            combat.Linked = true;
        if (playerNumber == 1 && (buttons[2] || buttons[1]) && !combat.Galaga) combat.Linked = false;
        if (combat.TwoPlayers && combat.Linked && playerNumber == 2 && (player.X != oldX || player.Y != oldY))
        {
            if (buttons[0])
            {
                if (movement.AnalogLinkGun) LinkGunDirection = movement.LinkGunAngle;
                else
                {
                    var target = Math.Abs(player.X - oldX) > Math.Abs(player.Y - oldY)
                        ? (float)(player.X - oldX > 0 ? Math.PI / 2 : Math.PI * 1.5)
                        : (float)(player.Y - oldY > 0 ? 0 : Math.PI);
                    if (MathF.Abs(LinkGunDirection - target) < 0.3f) LinkGunDirection = target;
                    else if (LinkGunDirection < target && LinkGunDirection - target > -3.24f) LinkGunDirection += 0.2f;
                    else if (LinkGunDirection - target < Math.PI) LinkGunDirection -= 0.2f;
                    else LinkGunDirection += 0.2f;
                }
                if (LinkGunDirection >= 2 * Math.PI) LinkGunDirection = (float)(LinkGunDirection - 2 * Math.PI);
                else if (LinkGunDirection < 0) LinkGunDirection = (float)(LinkGunDirection + 2 * Math.PI);
            }
            else if (!combat.Galaga) combat.Linked = false;
        }
    }

    public void DrawLevelEnd(PlayerState player, ushort graphic, Action<int, int, int> draw)
    {
        if (combat.LevelEnd > 0 && PlayerRules.AllDead(players[0], players[1], combat.TwoPlayers))
            level.ReallyEndLevel = true;
        if (!player.IsAlive || !level.EndLevel) return;
        if (combat.LevelEnd == 0) { level.ReallyEndLevel = true; return; }
        player.Y -= LevelEndWarp;
        if (player.Y < -200) level.ReallyEndLevel = true;
        var spacing = 1;
        var y = player.Y;
        var count = Math.Min(Math.Abs(41 - combat.LevelEnd), 20);
        for (var i = 0; i < count; i++) { y += spacing; spacing++; }
        for (var i = 1; i < count; i++)
        {
            y -= spacing; spacing--;
            if (y is <= 0 or >= 170) continue;
            if (graphic is 0 or 1)
            {
                draw(player.X - 17, y - 7, graphic == 0 ? 13 : 220);
                draw(player.X + 7, y - 7, graphic == 0 ? 51 : 222);
            }
            else draw(player.X - 5, y - 7, graphic);
        }
    }

    public void UpdateMovement(PlayerState player, byte playerNumber, ushort graphic, ushort oldX, ushort oldY,
        PlayerMovementState movement, ReadOnlySpan<bool> buttons)
    {
        Validate(playerNumber, buttons);
        if (!player.IsAlive || level.EndLevel) return;
        if (!combat.Linked || playerNumber < 2)
        {
            if (!combat.TwoPlayers || SecondShipGraphic != 0)
            {
                if (player.Sidekicks[0].Style == 0) { player.Sidekicks[0].X = oldX - 14; player.Sidekicks[0].Y = oldY; }
                if (player.Sidekicks[1].Style == 0) { player.Sidekicks[1].X = oldX + 16; player.Sidekicks[1].Y = oldY; }
            }
            if (player.XFrictionTicks > 0) player.XFrictionTicks--;
            else { player.XFrictionTicks = 1; player.XVelocity -= Math.Sign(player.XVelocity); }
            if (player.YFrictionTicks > 0) player.YFrictionTicks--;
            else { player.YFrictionTicks = 2; player.YVelocity -= Math.Sign(player.YVelocity); }
            player.XVelocity = Math.Clamp(player.XVelocity + movement.AccelerationX, -4, 4);
            player.YVelocity = Math.Clamp(player.YVelocity + movement.AccelerationY, -4, 4);
            player.X += player.XVelocity; player.Y += player.YVelocity;
            if (player.X != oldX || player.Y != oldY)
            {
                Array.Copy(player.OldX, 1, player.OldX, 0, player.OldX.Length - 1);
                Array.Copy(player.OldY, 1, player.OldY, 0, player.OldY.Length - 1);
                player.OldX[^1] = player.X; player.OldY[^1] = player.Y;
            }
        }
        else
        {
            player.X = players[0].X - (graphic == 0 ? 1 : 0); player.Y = players[0].Y + 8;
            player.XVelocity = players[0].XVelocity; player.YVelocity = 4;
            const PlayerShotSlot miscellaneous = PlayerShotSlot.Miscellaneous, rear = PlayerShotSlot.Rear;
            foreach (var (angle, item) in new[] { (LinkGunDirection + 0.2f, KnownItem.Shot148),
                (LinkGunDirection - 0.2f, KnownItem.Shot148), (LinkGunDirection, KnownItem.Shot147) })
            {
                shots.MultiPosition[(int)miscellaneous] = 0;
                shots.CreateTyped(0, miscellaneous, unchecked((ushort)(player.X + 1 + Round(MathF.Sin(angle) * 26))),
                    unchecked((ushort)(player.Y + Round(MathF.Cos(angle) * 26))), oldX, oldY,
                    checked((ushort)items.KnownIndex(item)), playerNumber);
            }
            if (shots.Repeat[(int)rear] > 0) shots.Repeat[(int)rear]--;
            else if (buttons[0])
            {
                shots.MultiPosition[(int)rear] = 0;
                var weapon = player.Items.Weapons[1].Id;
                var id = shots.CreateTyped(0, rear, unchecked((ushort)(player.X + 1 + Round(MathF.Sin(LinkGunDirection) * 20))),
                    unchecked((ushort)(player.Y + Round(MathF.Cos(LinkGunDirection) * 20))), oldX, oldY,
                    checked((ushort)items.Weapons[weapon].LinkGunWeapon), playerNumber);
                shots.SetDirection(id, weapon, LinkGunDirection);
            }
        }
    }

    public void UpdateSidekickPositions(PlayerState player, ReadOnlySpan<bool> buttons)
    {
        if (buttons.Length < 4) throw new ArgumentException("Player movement requires four buttons.", nameof(buttons));
        var left = player.Sidekicks[0]; var right = player.Sidekicks[1];
        if (left.Style == 4 && right.Style == 4) SatelliteRotation += 0.2f;
        else if (left.Style == 4 || right.Style == 4) SatelliteRotation += 0.15f;
        switch (left.Style)
        {
            case 1: case 3: left.X = player.OldX[9]; left.Y = player.OldY[9]; break;
            case 2: left.X = player.X; left.Y = Math.Max(10, player.Y - 20); break;
            case 4:
                left.X = player.X + Round(MathF.Sin(SatelliteRotation) * 20);
                left.Y = player.Y + Round(MathF.Cos(SatelliteRotation) * 20); break;
        }
        switch (right.Style)
        {
            case 4:
                right.X = player.X - Round(MathF.Sin(SatelliteRotation) * 20);
                right.Y = player.Y - Round(MathF.Cos(SatelliteRotation) * 20); break;
            case 1: case 3: right.X = player.OldX[0]; right.Y = player.OldY[0]; break;
            case 2:
                if (!AttachmentLinked)
                {
                    right.Y += AttachmentMove / 2;
                    var returning = AttachmentReturn ? 1 : 0;
                    if (AttachmentMove >= -2)
                    {
                        var step = AttachmentReturn ? 2 : 0;
                        if (right.Y > player.Y - 20 + 5) { step = 2; AttachmentMove -= (short)(1 + returning); }
                        else if (right.Y > player.Y - 20)
                        {
                            step = 3;
                            AttachmentMove += (short)(AttachmentMove > 0 ? -1 : 1);
                        }
                        else if (right.Y > player.Y - 20 - 5) { step = 2; AttachmentMove++; }
                        else if (AttachmentMove < 2 + returning * 4) AttachmentMove += (short)(1 + returning);
                        if (AttachmentReturn) step *= 2;
                        if (Math.Abs(right.X - player.X) < step) step = 1;
                        if (right.X > player.X) right.X -= step;
                        else if (right.X < player.X) right.X += step;
                        if (Math.Abs(right.Y - (player.Y - 20)) + Math.Abs(right.X - player.X) < 8)
                        {
                            AttachmentLinked = true; combat.Effects.SoundQueue[2] = 23;
                        }
                        if (buttons[2]) AttachmentReturn = true;
                    }
                    else
                    {
                        AttachmentMove += (short)(1 + returning);
                        combat.Effects.SetupExplosion(right.X + 1, right.Y + 10, 0, 0, false, false);
                    }
                }
                else
                {
                    right.X = player.X; right.Y = player.Y - 20;
                    if (buttons[2])
                    {
                        AttachmentLinked = AttachmentReturn = false; AttachmentMove = -20;
                        combat.Effects.SoundQueue[3] = 26;
                    }
                }
                if (right.Y < 10) right.Y = 10;
                break;
        }
    }

    private static int Round(float value) => (int)MathF.Round(value, MidpointRounding.AwayFromZero);
    private static void Validate(byte number, ReadOnlySpan<bool> buttons)
    {
        if (number is < 1 or > 2) throw new ArgumentOutOfRangeException(nameof(number));
        if (buttons.Length < 4) throw new ArgumentException("Player movement requires four buttons.", nameof(buttons));
    }
}
