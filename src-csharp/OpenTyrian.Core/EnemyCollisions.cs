/*
 * OpenTyrian: A modern cross-platform port of Tyrian
 * Copyright (C) 2007-2013 The OpenTyrian Development Team
 * Distributed under the GNU General Public License, version 2 or later.
 * See COPYING for details.
 */

namespace OpenTyrian.Core;

public sealed partial class EnemySystem
{
    public void DestroyOnCollision(int index, byte linkNumber, SceneEffects effects)
    {
        ValidateCollisionIndex(index);
        var link = linkNumber == 0 ? 255 : linkNumber;
        for (var i = 0; i < 100; i++)
        {
            var enemy = Enemies[i];
            if (enemy.Availability == 1) continue;
            var otherLink = enemy.LinkNumber;
            if (i != index && (link == 255 || !(link == otherLink || link - 100 == otherLink ||
                otherLink > 40 && otherLink / 20 == link / 20 && otherLink <= link))) continue;
            var x = enemy.X + enemy.MapOffset;
            enemy.LinkNumber = 0; enemy.Availability = 1;
            if (enemy.Type >= items.Enemies.Count) throw new InvalidDataException("Collision enemy type exceeds loaded definitions.");
            if (items.Enemies[enemy.Type].ESize == 1)
            {
                effects.SetupLarge(enemy.Ground, enemy.ExplosionNumber, unchecked((short)x), enemy.Y);
                effects.SoundQueue[6] = 9;
            }
            else
            {
                effects.SetupExplosion(x, enemy.Y, 0, 1, false, false);
                effects.SoundQueue[5] = 4;
            }
        }
        Enemies[index].Availability = 1;
    }

    public void DamageOnCollision(PlayerState player, int index, byte damageRate, PlayerCombat combat)
    {
        ValidateCollisionIndex(index);
        var enemy = Enemies[index];
        var damage = Math.Min(enemy.Armor, damageRate);
        combat.Damage((byte)damage, player);
        if (enemy.Armor > 0)
        {
            player.XVelocity = unchecked(player.XVelocity + enemy.XSpeed * enemy.Armor / 2);
            player.YVelocity = unchecked(player.YVelocity + enemy.YSpeed * enemy.Armor / 2);
        }
        var armor = enemy.Armor == 255 ? 30000 : enemy.Armor;
        if (armor > damage)
        {
            if (enemy.Armor != 255) enemy.Armor -= (byte)damage;
            combat.Effects.SoundQueue[5] = 3;
        }
        else DestroyOnCollision(index, enemy.LinkNumber, combat.Effects);
    }

    public void CollectArmorPickup(PlayerState player, int index, short value, PlayerCombat combat)
    {
        ValidateCollisionIndex(index);
        if (value <= 20000) throw new ArgumentOutOfRangeException(nameof(value), "Armor pickups must have a value above 20000.");
        var amount = (uint)(value - 20000);
        if (combat.Linked)
        {
            foreach (var owner in combat.Players)
                owner.Armor = Math.Min(unchecked(owner.Armor + amount / 2), 28u);
        }
        else player.Armor = Math.Min(unchecked(player.Armor + amount), 28u);
        Enemies[index].Availability = 1;
        combat.DrawArmor(); combat.Effects.SoundQueue[7] = 29;
    }

    private static void ValidateCollisionIndex(int index)
    {
        if ((uint)index >= 100) throw new ArgumentOutOfRangeException(nameof(index), "Player collisions use the first 100 enemies.");
    }
}
