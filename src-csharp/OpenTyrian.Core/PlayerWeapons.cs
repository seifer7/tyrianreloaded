/*
 * OpenTyrian: A modern cross-platform port of Tyrian
 * Copyright (C) 2007-2009 The OpenTyrian Development Team
 * Distributed under the GNU General Public License, version 2 or later.
 * See COPYING for details.
 */

namespace OpenTyrian.Core;

public sealed class PlayerWeapons(PlayerShots shots, ItemDatabase items, PlayerCombat combat)
{
    public byte ChargeWait { get; set; }
    public byte ChargeLevel { get; set; }
    public byte ChargeMaximum { get; set; } = 5;
    public byte ChargeGraphic { get; set; }
    public byte ChargeGraphicWait { get; set; }
    public bool ModeChangeRequested { get; set; }
    public bool ModeChangeReady { get; set; } = true;
    public bool PortConfigChange { get; set; }

    public void DrawOptions(IndexedSurface hud, SpriteTable icons)
        => DrawOptions(hud, (x, y, frame) => icons.Blit(hud, x, y, frame));

    public void DrawOptions(IndexedSurface hud, Action<int, int, int> drawIcon)
    {
        var player = shots.Players[combat.TwoPlayers ? 1 : 0];
        for (var i = 0; i < 2; i++)
        {
            var option = items.Sidekicks[player.Items.Sidekicks[i]];
            var sidekick = player.Sidekicks[i];
            sidekick.Ammo = sidekick.AmmoMax = option.Ammo;
            sidekick.AmmoRefillTicks = sidekick.AmmoRefillTicksMax = unchecked((uint)((105 - sidekick.Ammo) * 4));
            sidekick.Style = option.Tr;
            sidekick.AnimationEnabled = option.Option == 1; sidekick.AnimationFrame = 0;
            sidekick.Charge = 0; sidekick.ChargeTicks = 20;
            var y = (combat.TwoPlayers ? 108 : 64) + i * 18;
            hud.FillRectangle(284, y, 312, y + 15, 0);
            if (option.IconGr > 0) drawIcon(284, y, option.IconGr - 1);
            hud.SegmentedGauge(284, y + 13, 112, 2, 2, (uint)Math.Max(1, sidekick.AmmoMax / 10), (uint)sidekick.Ammo);
        }
        DrawOptionLevel(hud);
    }

    public void DrawOptionLevel(IndexedSurface hud)
    {
        if (!combat.TwoPlayers) return;
        for (var i = 1; i <= 3; i++)
            hud.FillRectangle(268, 127 + (i - 1) * 6, 269, 130 + (i - 1) * 6,
                (byte)(193 + (shots.Players[1].Items.SidekickLevel - 100 == i ? 11 : 0)));
    }
    public byte[][] SpecialCodeProgress { get; } = [new byte[21], new byte[21]];
    public byte[] ExecutedSpecial { get; } = new byte[2];
    public void UpdateAttacks(PlayerState player, byte playerNumber, ushort graphic, ushort mouseX, ushort mouseY,
        ReadOnlySpan<bool> buttons, byte arcadeMode, sbyte difficulty, bool endLevel, PlayerMovement movement,
        PlayerSpecialWeapons special, IndexedSurface game, IndexedSurface hud,
        Action drawModeButtons, Action<int, int, int> drawIndicator, Action<int, int, int> drawCharge, Action drawOptions,
        (ushort X, ushort Y)? specialMouse = null)
    {
        Validate(playerNumber, buttons);
        if (!movement.MoveAllowed || !player.IsAlive || endLevel) return;
        player.DeltaXShotMove = player.X - player.LastXShotMove;
        player.DeltaYShotMove = player.Y - player.LastYShotMove;
        ChangeMode(player, buttons, arcadeMode, drawModeButtons);
        if (!combat.Galaga) special.Update(playerNumber, specialMouse?.X ?? mouseX, specialMouse?.Y ?? mouseY,
            buttons, game, drawIndicator, drawOptions);
        FireMain(player, playerNumber, mouseX, mouseY, buttons);
        UpdateCharge(player, playerNumber, graphic, mouseX, mouseY, buttons, difficulty, hud, drawCharge);
        FireSuperbomb(player, playerNumber, mouseX, mouseY, buttons);
        movement.UpdateSidekickPositions(player, buttons);
        FireSidekicks(player, playerNumber, mouseX, mouseY, buttons, hud);
    }

    public void RecognizeSpecial(byte playerNumber, short x, short y, short oldX, short oldY,
        ReadOnlySpan<bool> buttons, bool superTyrian)
    {
        Validate(playerNumber, buttons);
        var ship = shots.Players[playerNumber - 1].Items.Ship;
        if (playerNumber == 2 && ship < 15) ship = 0;
        if (ship >= 15) return;
        if (!superTyrian && ship >= LegacyGameTables.PlayerSpecialShipComboCount)
            throw new InvalidDataException("Player ship exceeds native special-command tables.");
        var count = (oldY > y ? 1 : 0) + (oldY < y ? 1 : 0) + (x < oldX ? 1 : 0) + (x > oldX ? 1 : 0);
        var direction = (oldY > y ? 1 : 0) + (oldY < y ? 2 : 0) + (x < oldX ? 3 : 0) + (x > oldX ? 4 : 0);
        if (direction == 0)
        {
            if (!buttons[0]) { direction = 9; count = 1; }
            else { count = 0; direction = 99; }
        }
        if (count != 1) return;
        direction += buttons[0] ? 4 : 0;
        var progress = SpecialCodeProgress[playerNumber - 1];
        for (var i = 0; i < (superTyrian ? LegacyGameTables.SuperTyrianSpecialCount : 3); i++)
        {
            var combo = superTyrian
                ? LegacyGameTables.SuperTyrianSpecialCombo(i)
                : LegacyGameTables.PlayerSpecialShipCombo(ship, i);
            if (combo == 0) { progress[i] = 0; continue; }
            var keyCombo = combo - 1;
            if (keyCombo >= LegacyGameTables.KeyboardComboCount)
                throw new InvalidDataException("Special-command mapping exceeds native keyboard tables.");
            if (progress[i] >= LegacyGameTables.KeyboardComboLength - 1)
                throw new InvalidDataException("Special-command progress exceeds native key tables.");
            var next = LegacyGameTables.KeyboardComboStep(keyCombo, progress[i]);
            if (next == direction)
            {
                progress[i]++;
                next = LegacyGameTables.KeyboardComboStep(keyCombo, progress[i]);
                if (next is > 100 and <= 146)
                { progress[i] = 0; ExecutedSpecial[playerNumber - 1] = (byte)(next - 100); }
            }
            else if (direction != 9 && (next - 1) % 4 != (direction - 1) % 4 &&
                (progress[i] == 0 || LegacyGameTables.KeyboardComboStep(keyCombo, progress[i] - 1) != direction))
                progress[i] = 0;
        }
    }

    public void ChangeMode(PlayerState player, ReadOnlySpan<bool> buttons, byte arcadeMode, Action drawButtons)
    {
        if (buttons.Length < 4) throw new ArgumentException("Player firing requires four buttons.", nameof(buttons));
        if (!buttons[3]) return;
        ModeChangeRequested = true;
        if (!ModeChangeReady) return;
        shots.MultiPosition[1] = 0;
        if (arcadeMode is >= 1 and <= 7)
        {
            shots.MultiPosition[8] = shots.MultiPosition[10] = 0;
            var primary = Special(LegacyGameTables.SuperArcadeSpecialWeapon(arcadeMode - 1, 0));
            if (shots.Players[0].Items.Special == primary)
            {
                shots.Players[0].Items.Special = Special(LegacyGameTables.SuperArcadeSpecialWeapon(arcadeMode - 1, 1));
                player.WeaponMode = 2;
            }
            else
            {
                shots.Players[0].Items.Special = primary;
                player.WeaponMode = 1;
            }
        }
        else
        {
            player.WeaponMode = unchecked(player.WeaponMode + 1);
            if (player.WeaponMode > combat.PortConfigurations(items.Weapons)) player.WeaponMode = 1;
        }
        drawButtons();
        ModeChangeReady = false;

        byte Special(byte id)
        {
            var index = items.Index(ItemType.Special, id);
            if (index < 0) throw new InvalidDataException($"Arcade special weapon {id} is missing.");
            return checked((byte)index);
        }
    }

    public void DrawModeButtons(Action<int, int, int> draw)
    {
        if (combat.TwoPlayers) return;
        var first = shots.Players[0].WeaponMode == 1;
        draw(285, 44, first ? 18 : 19);
        draw(302, 44, first ? 19 : 18);
    }

    public void DrawModeButtons(IndexedSurface hud, SpriteTable icons)
        => DrawModeButtons((x, y, frame) => icons.Blit(hud, x, y, frame));

    public void FireMain(PlayerState player, byte playerNumber, ushort mouseX, ushort mouseY, ReadOnlySpan<bool> buttons)
    {
        Validate(playerNumber, buttons);
        if (combat.Linked && playerNumber == 2) return;
        var first = combat.TwoPlayers ? playerNumber - 1 : 0;
        var last = combat.TwoPlayers ? playerNumber : 2;
        for (var port = first; port < last; port++)
        {
            var weapon = player.Items.Weapons[port];
            if (weapon.Id == 0) continue;
            if (shots.Repeat[port] > 0) shots.Repeat[port]--;
            else if (buttons[0])
            {
                var power = combat.Galaga ? 0 : weapon.Power - 1;
                var mode = port == 1 ? unchecked((int)player.WeaponMode - 1) : 0;
                if (power is < 0 or >= 11 || mode is < 0 or >= 2)
                    throw new InvalidDataException("Player weapon power or mode exceeds native firing tables.");
                shots.CreateTyped(weapon.Id, port == 0 ? PlayerShotSlot.Front : PlayerShotSlot.Rear,
                    unchecked((ushort)player.X), unchecked((ushort)player.Y),
                    mouseX, mouseY, items.Weapons[weapon.Id].Op[mode][power], playerNumber);
            }
        }
    }

    public void FireSuperbomb(PlayerState player, byte playerNumber, ushort mouseX, ushort mouseY, ReadOnlySpan<bool> buttons)
    {
        var ownerNumber = playerNumber == 0 ? (byte)1 : playerNumber;
        Validate(ownerNumber, buttons);
        var owner = shots.Players[ownerNumber - 1];
        if (owner.Superbombs == 0) return;
        var bay = ownerNumber == 1 ? PlayerShotSlot.PlayerOneSuperbomb : PlayerShotSlot.PlayerTwoSuperbomb;
        if (shots.Repeat[(int)bay] > 0) shots.Repeat[(int)bay]--;
        else if (buttons[2] || buttons[1])
        {
            owner.Superbombs--;
            shots.MultiPosition[(int)bay] = 0;
            // The original uses these literal indices rather than ID lookup.
            shots.CreateTyped(16, bay, unchecked((ushort)player.X), unchecked((ushort)player.Y),
                mouseX, mouseY, 535, playerNumber);
        }
    }

    public void UpdateCharge(PlayerState player, byte playerNumber, ushort graphic, ushort mouseX, ushort mouseY,
        ReadOnlySpan<bool> buttons, sbyte difficulty, IndexedSurface hud, Action<int, int, int> draw)
    {
        Validate(playerNumber, buttons);
        if (playerNumber != 2) return;
        if (ChargeLevel > 5) throw new InvalidDataException("Charge weapon exceeds its five native shot levels.");
        if (!combat.Linked) draw(player.X + (graphic == 0 ? 1 : 0) + 1, player.Y - 13,
            77 + ChargeLevel + ChargeGraphic * 19);
        if (ChargeGraphicWait > 0) ChargeGraphicWait--;
        else
        {
            ChargeGraphic = unchecked((byte)(ChargeGraphic + 1));
            if (ChargeGraphic == 4) ChargeGraphic = 0;
            ChargeGraphicWait = 3;
        }
        if (ChargeLevel > 0) Bar(193);
        if (ChargeWait > 0) ChargeWait--;
        else
        {
            if (ChargeLevel < ChargeMaximum) ChargeLevel++;
            ChargeWait = unchecked((byte)(28 - player.Items.Weapons[1].Power * 2));
            if (difficulty > 3) ChargeWait = unchecked((byte)(ChargeWait - 5));
        }
        if (ChargeLevel > 5) throw new InvalidDataException("Charge maximum exceeds its five native shot levels.");
        if (ChargeLevel > 0) Bar(204);
        const PlayerShotSlot bay = PlayerShotSlot.PlayerTwoCharge;
        if (shots.Repeat[(int)bay] > 0) shots.Repeat[(int)bay]--;
        else if (buttons[0] && (!combat.Linked || ChargeLevel > 0))
        {
            shots.MultiPosition[(int)bay] = 0;
            var ownerWeapon = shots.Players[1].Items.Weapons[1].Id;
            shots.CreateTyped(checked((ushort)items.KnownIndex(KnownItem.WeaponFrontMiscellaneous)), bay,
                unchecked((ushort)player.X), unchecked((ushort)player.Y), mouseX, mouseY,
                checked((ushort)items.Weapons[ownerWeapon].ChargeGunWeapons[ChargeLevel > 0 ? ChargeLevel - 1 : 0]), playerNumber);
            if (ChargeLevel > 0) Bar(193);
            ChargeLevel = 0;
            ChargeWait = unchecked((byte)(30 - player.Items.Weapons[1].Power * 2));
        }

        void Bar(byte color) => hud.FillRectangle(269, 107 + (ChargeLevel - 1) * 3, 275, 108 + (ChargeLevel - 1) * 3, color);
    }

    public void FireSidekicks(PlayerState player, byte playerNumber, ushort mouseX, ushort mouseY,
        ReadOnlySpan<bool> buttons, IndexedSurface hud)
    {
        Validate(playerNumber, buttons);
        if (playerNumber != 2 && combat.TwoPlayers) return;
        for (var i = 0; i < 2; i++)
        {
            var bay = i == 0 ? PlayerShotSlot.LeftSidekick : PlayerShotSlot.RightSidekick;
            var option = items.Sidekicks[player.Items.Sidekicks[i]];
            if (option.WPort == 0) continue;
            if (shots.Repeat[(int)bay] > 0) { shots.Repeat[(int)bay]--; continue; }
            var sidekick = player.Sidekicks[i];
            var maximum = sidekick.AmmoMax;
            var y = (combat.TwoPlayers ? 108 : 64) + i * 18 + 13;
            if (maximum > 0)
            {
                if (sidekick.Ammo < 0) throw new InvalidDataException("Sidekick ammunition cannot be negative.");
                if (sidekick.AmmoRefillTicks > 0) sidekick.AmmoRefillTicks--;
                else
                {
                    sidekick.AmmoRefillTicks = sidekick.AmmoRefillTicksMax;
                    if (sidekick.Ammo < maximum) sidekick.Ammo++;
                    Gauge();
                }
                if (buttons[1 + i] && sidekick.Ammo > 0)
                {
                    Fire();
                    sidekick.Ammo--;
                    ResetCharge();
                    hud.FillRectangle(284, y, 312, y + 2, 0);
                    Gauge();
                }
            }
            else if (buttons[0] || buttons[1 + i])
            {
                Fire();
                ResetCharge();
            }

            void Fire() => shots.CreateTyped(checked((ushort)option.WPort), bay, unchecked((ushort)sidekick.X),
                unchecked((ushort)sidekick.Y), mouseX, mouseY, unchecked((ushort)(option.Wpnum + sidekick.Charge)), playerNumber);
            void ResetCharge()
            {
                if (sidekick.Charge > 0) { shots.MultiPosition[(int)bay] = 0; sidekick.Charge = 0; }
                sidekick.ChargeTicks = 20; sidekick.AnimationEnabled = true;
            }
            void Gauge()
            {
                hud.SegmentedGauge(284, y, 112, 2, 2, (uint)Math.Max(1, maximum / 10), (uint)sidekick.Ammo);
            }
        }
    }

    public void DrawSidekicks(PlayerState player, byte playerNumber, bool endLevel,
        Action<int, int, int, bool> draw)
    {
        if (playerNumber is < 1 or > 2) throw new ArgumentOutOfRangeException(nameof(playerNumber));
        if ((playerNumber != 2 && combat.TwoPlayers) || endLevel) return;
        for (var i = 0; i < 2; i++)
        {
            var option = items.Sidekicks[player.Items.Sidekicks[i]];
            var sidekick = player.Sidekicks[i];
            if (option.Option > 0)
            {
                if (sidekick.AnimationEnabled)
                {
                    sidekick.AnimationFrame = unchecked(sidekick.AnimationFrame + 1);
                    if (sidekick.AnimationFrame >= option.Ani)
                    {
                        sidekick.AnimationFrame = 0;
                        sidekick.AnimationEnabled = option.Option == 1;
                    }
                }
                if (sidekick.Charge >= 5 || sidekick.AnimationFrame >= 20)
                    throw new InvalidDataException("Sidekick charge or animation frame exceeds native sprite tables.");
                var graphic = option.Gr[sidekick.Charge][sidekick.AnimationFrame];
                var large = sidekick.Style is 1 or 2;
                draw(sidekick.X - (large ? 6 : 0), sidekick.Y, graphic, large);
            }
            sidekick.ChargeTicks = unchecked(sidekick.ChargeTicks - 1);
            if (sidekick.ChargeTicks == 0)
            {
                if (sidekick.Charge < option.Pwr) sidekick.Charge++;
                sidekick.ChargeTicks = 20;
            }
        }
    }

    private static void Validate(byte playerNumber, ReadOnlySpan<bool> buttons)
    {
        if (playerNumber is < 1 or > 2) throw new ArgumentOutOfRangeException(nameof(playerNumber));
        if (buttons.Length < 4) throw new ArgumentException("Player firing requires four buttons.", nameof(buttons));
    }
}
