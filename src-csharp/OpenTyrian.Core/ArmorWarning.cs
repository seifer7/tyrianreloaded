/*
 * OpenTyrian: A modern cross-platform port of Tyrian
 * Copyright (C) 2007-2013 The OpenTyrian Development Team
 * Distributed under the GNU General Public License, version 2 or later.
 * See COPYING for details.
 */

namespace OpenTyrian.Core;

public interface IArmorWarningHost
{
    short CreateShip();
    void ConfigureShip(short ship, int dropId, ushort backgroundMove);
    void Fill(int left, int top, int right, int bottom, byte color);
    void Label(byte brightness);
}

public sealed class ArmorWarning(IReadOnlyList<PlayerState> players, PlayerCombat combat,
    WarningBars warning, SceneEffects effects, IArmorWarningHost host)
{
    public ushort ShipDelay { get; set; }

    public void Draw(bool networkGame, byte playerNumber, ushort thirdMove)
    {
        if (networkGame && playerNumber is < 1 or > 2)
            throw new InvalidDataException("Local network player must be one or two.");
        var first = players[0].IsAlive && players[0].Armor < 6;
        var second = combat.TwoPlayers && !combat.Galaga && players[1].IsAlive && players[1].Armor < 6;
        if (!first && !second) return;
        var armor = first ? players[0].Armor : players[1].Armor;
        if (ShipDelay > 0) ShipDelay--;
        else
        {
            var ship = host.CreateShip();
            if (ship > 0) host.ConfigureShip(ship, (int)(561 + MersenneTwister.NextUInt32() % 3), thirdMove);
            ShipDelay = 500;
        }
        if (!(first && (!networkGame || playerNumber == 1)) &&
            !(combat.TwoPlayers && players[1].IsAlive && players[1].Armor < 6 && (!networkGame || playerNumber == 2))) return;
        var delay = (byte)(armor * 4 + 8);
        if (warning.SoundDelay > delay) warning.SoundDelay = delay;
        if (warning.SoundDelay > 1) warning.SoundDelay--;
        else { effects.SoundQueue[7] = 17; warning.SoundDelay = delay; }
        warning.Color = unchecked((byte)(warning.Color + warning.ColorChange));
        var maximum = 113 + (14 - armor * 2);
        if (warning.Color > maximum)
        {
            warning.ColorChange = unchecked((sbyte)-warning.ColorChange);
            warning.Color = (byte)maximum;
        }
        else if (warning.Color < 113) warning.ColorChange = unchecked((sbyte)-warning.ColorChange);
        host.Fill(24, 181, 138, 183, warning.Color);
        host.Fill(175, 181, 287, 183, warning.Color);
        host.Fill(24, 0, 287, 3, warning.Color);
        host.Label((byte)(warning.Color % 16 / 2));
    }
}

public sealed class ArmorWarningHost(EnemySystem enemies, ItemDatabase items, IndexedSurface primary,
    FontRenderer font) : IArmorWarningHost
{
    public short CreateShip() => enemies.Create(50, items.KnownIndex(KnownItem.EnemyArmorShip));
    public void ConfigureShip(short ship, int dropId, ushort backgroundMove)
    {
        if (!items.TryFind(ItemType.Enemy, dropId, out var drop))
            throw new InvalidDataException($"Armor rescue drop enemy ID '{dropId}' is missing.");
        var enemy = enemies.Enemies[ship - 1];
        enemy.EnemyDie = checked((ushort)drop);
        enemy.YSpeed = unchecked((sbyte)(enemy.YSpeed - backgroundMove));
        enemy.Armor = 4;
    }
    public void Fill(int left, int top, int right, int bottom, byte color)
        => primary.FillRectangle(left, top, right, bottom, color);
    public void Label(byte brightness) => font.OutText(primary, 140, 178, "WARNING"u8, 7, brightness);
}
