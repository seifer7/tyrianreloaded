/*
 * OpenTyrian: A modern cross-platform port of Tyrian
 * Copyright (C) 2007-2009 The OpenTyrian Development Team
 * Distributed under the GNU General Public License, version 2 or later.
 * See COPYING for details.
 */

namespace OpenTyrian.Core;

public abstract class GameItem
{
    public string Id { get; internal set; } = "";
}

public abstract class NamedItem : GameItem
{
    public string Name { get; internal set; } = "";
    public ushort ItemGraphic { get; internal set; }
}

public abstract class ShopItem : NamedItem
{
    public int Cost { get; internal set; }
    public byte Probability { get; internal set; }
}

public sealed class ShotItem : GameItem
{
    public ushort Drain { get; internal set; }
    public byte ShotRepeat { get; internal set; }
    public byte Multi { get; internal set; }
    public ushort WeapAni { get; internal set; }
    public byte Max { get; internal set; }
    public byte Tx { get; internal set; }
    public byte Ty { get; internal set; }
    public byte Aim { get; internal set; }
    public byte[] Attack { get; } = new byte[8];
    public byte[] Del { get; } = new byte[8];
    public byte[] IceTicks { get; } = new byte[8];
    public byte[] Infinite { get; } = new byte[8];
    public string[] ChainIds { get; } = Enumerable.Repeat("", 8).ToArray();
    public int[] Chain { get; } = new int[8];
    public sbyte[] Sx { get; } = new sbyte[8];
    public sbyte[] Sy { get; } = new sbyte[8];
    public sbyte[] Bx { get; } = new sbyte[8];
    public sbyte[] By { get; } = new sbyte[8];
    public ushort[] Sg { get; } = new ushort[8];
    public sbyte Acceleration { get; internal set; }
    public sbyte AccelerationX { get; internal set; }
    public byte CircleSize { get; internal set; }
    public byte Sound { get; internal set; }
    public byte Trail { get; internal set; }
    public byte ShipBlastFilter { get; internal set; }
}

public sealed class ShipItem : ShopItem
{
    public ushort ShipGraphic { get; internal set; }
    public byte Ani { get; internal set; }
    public sbyte Spd { get; internal set; }
    public byte Dmg { get; internal set; }
    public byte BigShipGraphic { get; internal set; }
    public byte BigShipGraphX { get; internal set; }
    public byte BigShipGraphY { get; internal set; }
}

public sealed class WeaponItem : ShopItem
{
    public byte OpNum { get; internal set; }
    public ushort[][] Op { get; } = [new ushort[11], new ushort[11]];
    public ushort PowerUse { get; internal set; }
    public ushort Position { get; internal set; }
    public byte BigShipGraphic { get; internal set; }
    public byte BigShipGraphFrntX { get; internal set; }
    public byte BigShipGraphFrntY { get; internal set; }
    public byte BigShipGraphRearX { get; internal set; }
    public byte BigShipGraphRearY { get; internal set; }
    public string LinkGunWeaponId { get; internal set; } = "";
    public int LinkGunWeapon { get; internal set; }
    public string[] ChargeGunWeaponIds { get; } = Enumerable.Repeat("", 5).ToArray();
    public int[] ChargeGunWeapons { get; } = new int[5];
}

public sealed class GeneratorItem : ShopItem
{
    public byte Power { get; internal set; }
    public sbyte Speed { get; internal set; }
    public byte BigShipGraphic { get; internal set; }
    public byte BigShipGraphX { get; internal set; }
    public byte BigShipGraphY { get; internal set; }
}

public sealed class ShieldItem : ShopItem
{
    public byte Tpwr { get; internal set; }
    public byte Mpwr { get; internal set; }
}

public sealed class SidekickItem : ShopItem
{
    public byte Pwr { get; internal set; }
    public byte Tr { get; internal set; }
    public byte Option { get; internal set; }
    public sbyte OpSpd { get; internal set; }
    public byte Ani { get; internal set; }
    public ushort[][] Gr { get; } = Enumerable.Range(0, 5).Select(_ => new ushort[20]).ToArray();
    public int WPort { get; internal set; }
    public string WPortId { get; internal set; } = "";
    public ushort Wpnum { get; internal set; }
    public byte Ammo { get; internal set; }
    public bool Stop { get; internal set; }
    public byte IconGr { get; internal set; }
    public byte Position { get; internal set; }
}

public sealed class SpecialItem : NamedItem
{
    public byte Pwr { get; internal set; }
    public byte Stype { get; internal set; }
    public ushort Wpn { get; internal set; }
}

public sealed class EnemyItem : GameItem
{
    public byte Ani { get; internal set; }
    public byte[] Tur { get; } = new byte[3];
    public byte[] Freq { get; } = new byte[3];
    public sbyte XMove { get; internal set; }
    public sbyte YMove { get; internal set; }
    public sbyte XAccel { get; internal set; }
    public sbyte YAccel { get; internal set; }
    public sbyte XcAccel { get; internal set; }
    public sbyte YcAccel { get; internal set; }
    public short StartX { get; internal set; }
    public short StartY { get; internal set; }
    public sbyte StartXc { get; internal set; }
    public sbyte StartYc { get; internal set; }
    public byte Armor { get; internal set; }
    public byte ESize { get; internal set; }
    public ushort[] EGraphic { get; } = new ushort[20];
    public byte ExplosionType { get; internal set; }
    public byte Animate { get; internal set; }
    public byte ShapeBank { get; internal set; }
    public sbyte XRev { get; internal set; }
    public sbyte YRev { get; internal set; }
    public ushort Dgr { get; internal set; }
    public sbyte DLevel { get; internal set; }
    public sbyte DAni { get; internal set; }
    public byte ELaunchFreq { get; internal set; }
    public string ELaunchTypeId { get; internal set; } = "";
    public int ELaunchType { get; internal set; }
    public ushort ELaunchTypePos { get; internal set; }
    public byte ELaunchSpecial { get; internal set; }
    public short Value { get; internal set; }
    public string EEnemyDieId { get; internal set; } = "";
    public int EEnemyDie { get; internal set; }
    public byte[] EShotWait { get; } = new byte[3];
    public bool EnemyGround { get; internal set; }
}

public enum KnownItem
{
    GeneratorAdvancedMr12, ShieldAdvancedIntegrityField, ShieldGencoreHighEnergy,
    ShipNortship, ShipSilverShip, ShipStalker, ShipStalker21126, ShipSuperCarrot, ShipUspTalon,
    Shot147, Shot148, SidekickCompanionShipQuicksilver, SpecialAstralZone,
    WeaponFrontAtomicRailgun, WeaponFrontBananaBlast, WeaponFrontHotdog,
    WeaponFrontNortshipSuperPulse, WeaponFrontPulseCannon, WeaponFrontMiscellaneous,
    WeaponRearBananaBlast, WeaponRearHotdog, WeaponRearNortshipSpreader, WeaponRearVulcanCannon,
    EnemyArmorShip, EnemyFrontPowerup, EnemyRearPowerup
}
