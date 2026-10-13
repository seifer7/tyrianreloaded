/*
 * OpenTyrian: A modern cross-platform port of Tyrian
 * Copyright (C) 2007-2009 The OpenTyrian Development Team
 * Distributed under the GNU General Public License, version 2 or later.
 * See COPYING for details.
 */

using System.Globalization;

namespace OpenTyrian.Core;

public sealed class ItemDatabase
{
    private readonly Dictionary<string, int>[] _indices;
    public IReadOnlyList<ShotItem> Shots { get; }
    public IReadOnlyList<ShipItem> Ships { get; }
    public IReadOnlyList<WeaponItem> Weapons { get; }
    public IReadOnlyList<GeneratorItem> Generators { get; }
    public IReadOnlyList<ShieldItem> Shields { get; }
    public IReadOnlyList<SidekickItem> Sidekicks { get; }
    public IReadOnlyList<SpecialItem> Specials { get; }
    public IReadOnlyList<EnemyItem> Enemies { get; }
    public IReadOnlyList<int> KnownIndices { get; }

    private ItemDatabase(ItemDefinitions definitions)
    {
        definitions.ReserveZeroSlots();
        _indices = Enumerable.Range(0, (int)ItemType.Count).Select(type =>
            definitions.Records((ItemType)type).Select((record, index) => (record.Id, index))
                .ToDictionary(pair => pair.Id, pair => pair.index, StringComparer.Ordinal)).ToArray();
        Shots = Create<ShotItem>(definitions, ItemType.Shot);
        Ships = Create<ShipItem>(definitions, ItemType.Ship);
        Weapons = Create<WeaponItem>(definitions, ItemType.WeaponFront);
        Generators = Create<GeneratorItem>(definitions, ItemType.Generator);
        Shields = Create<ShieldItem>(definitions, ItemType.Shield);
        Sidekicks = Create<SidekickItem>(definitions, ItemType.SidekickLeft);
        Specials = Create<SpecialItem>(definitions, ItemType.Special);
        Enemies = Create<EnemyItem>(definitions, ItemType.Enemy);
        foreach (var type in new[] { ItemType.Shot, ItemType.Ship, ItemType.WeaponFront, ItemType.Generator,
            ItemType.Shield, ItemType.SidekickLeft, ItemType.Special, ItemType.Enemy })
        {
            var records = definitions.Records(type);
            for (var i = 0; i < records.Count; i++)
            {
                foreach (var field in records[i].Fields)
                {
                    Apply(Items(type)[i], records[i], field);
                }
            }
        }

        ResolveReferences(definitions);
        KnownIndices = Array.AsReadOnly(KnownReferences.Select(reference =>
            RequireIndex(reference.Type, reference.Id, "data", 0)).ToArray());
    }

    public static ItemDatabase Load(string workingDirectory)
    {
        var definitions = new ItemDefinitions();
        definitions.LoadAll(workingDirectory);
        return new ItemDatabase(definitions);
    }

    public static ItemDatabase FromDefinitions(ItemDefinitions definitions) => new(definitions);

    public IReadOnlyList<GameItem> Items(ItemType type) => type switch
    {
        ItemType.Shot => Shots,
        ItemType.Ship => Ships,
        ItemType.WeaponFront or ItemType.WeaponRear => Weapons,
        ItemType.Generator => Generators,
        ItemType.Shield => Shields,
        ItemType.SidekickLeft or ItemType.SidekickRight => Sidekicks,
        ItemType.Special => Specials,
        ItemType.Enemy => Enemies,
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };

    public bool TryFind(ItemType type, string id, out int index)
    {
        if (type < ItemType.Shot || type >= ItemType.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(type));
        }

        return _indices[(int)type].TryGetValue(id, out index);
    }

    public int Index(ItemType type, string id) => TryFind(type, id, out var index) ? index : -1;
    public int Index(ItemType type, int id) => Index(type, id.ToString(CultureInfo.InvariantCulture));
    public bool TryFind(ItemType type, int id, out int index) => TryFind(type, id.ToString(CultureInfo.InvariantCulture), out index);
    public int KnownIndex(KnownItem item) => KnownIndices[(int)item];

    public bool AddRandomShopItem(ItemType type, Span<byte> available, int index)
    {
        if (type is not (ItemType.Ship or ItemType.WeaponFront or ItemType.WeaponRear or ItemType.Generator
            or ItemType.Shield or ItemType.SidekickLeft or ItemType.SidekickRight))
        {
            return false;
        }

        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, available.Length);
        var candidates = new List<byte>();
        var items = Items(type);
        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            if (item.Id == "0") continue;
            if (item.Id.Length == 0) break;
            if (item is WeaponItem weapon &&
                (type == ItemType.WeaponRear ? weapon.Position != 1 : weapon.Position == 1)) continue;
            if (item is SidekickItem sidekick &&
                (type == ItemType.SidekickLeft ? sidekick.Position == 2 : sidekick.Position == 1)) continue;
            if (Contains(available, i)) continue;
            var probability = ((ShopItem)item).Probability;
            if (MersenneTwister.NextUInt32() % 100 >= probability) continue;
            candidates.Add(unchecked((byte)i));
        }

        if (candidates.Count == 0) return false;
        available[index] = candidates[(int)(MersenneTwister.NextUInt32() % (uint)candidates.Count)];
        return true;
    }

    public static bool Contains(ReadOnlySpan<byte> values, int value)
    {
        foreach (var element in values)
        {
            if (element == value) return true;
        }

        return false;
    }

    private static IReadOnlyList<T> Create<T>(ItemDefinitions definitions, ItemType type) where T : GameItem, new()
    {
        var records = definitions.Records(type);
        if (records.Count == 0) throw ItemDefinitions.Error("data", 0, $"no objects for type {(int)type}");
        return Array.AsReadOnly(records.Select(record => new T { Id = record.Id }).ToArray());
    }

    private int RequireIndex(ItemType type, string id, string path, int line)
        => TryFind(type, id, out var index) ? index : throw ItemDefinitions.Error(path, line,
            $"required extracted item ID '{id}' (type {(int)type}) is missing");

    private void ResolveReferences(ItemDefinitions definitions)
    {
        foreach (var type in new[] { ItemType.Shot, ItemType.WeaponFront, ItemType.SidekickLeft, ItemType.Enemy })
        {
            var records = definitions.Records(type);
            for (var i = 0; i < records.Count; i++)
            {
                var record = records[i];
                int Resolve(ItemType target, string id) => id.Length == 0 || id == "0" ? 0 :
                    RequireIndex(target, id, record.Path, record.Line);
                switch (Items(type)[i])
                {
                    case ShotItem shot:
                        for (var j = 0; j < shot.Chain.Length; j++)
                            shot.Chain[j] = Resolve(ItemType.Shot, shot.ChainIds[j]);
                        break;
                    case WeaponItem weapon:
                        weapon.LinkGunWeapon = Resolve(ItemType.Shot, weapon.LinkGunWeaponId);
                        for (var j = 0; j < weapon.ChargeGunWeapons.Length; j++)
                            weapon.ChargeGunWeapons[j] = Resolve(ItemType.Shot, weapon.ChargeGunWeaponIds[j]);
                        break;
                    case SidekickItem sidekick:
                        sidekick.WPort = Resolve(ItemType.WeaponFront, sidekick.WPortId);
                        break;
                    case EnemyItem enemy:
                        enemy.EEnemyDie = Resolve(ItemType.Enemy, enemy.EEnemyDieId);
                        enemy.ELaunchType = Resolve(ItemType.Enemy, enemy.ELaunchTypeId);
                        break;
                }
            }
        }
    }

    private static int Number(string value) => int.Parse(ItemDefinitions.Tokens(value).First(),
        NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);

    private static void Fill<T>(T[] destination, string value, Func<int, T> convert)
    {
        var index = 0; var reader = new TextTokenReader(System.Text.Encoding.UTF8.GetBytes(value));
        while (index < destination.Length && reader.TryReadInt32(out var number))
            destination[index++] = convert(number);
    }

    private static void FillReferences(string[] destination, string value)
    {
        var index = 0;
        foreach (var token in ReferenceTokens(value, destination.Length))
            destination[index++] = token;
    }

    private static IEnumerable<string> ReferenceTokens(string value, int capacity)
    {
        var reader = new TextTokenReader(System.Text.Encoding.UTF8.GetBytes(value));
        for (var i = 0; i < capacity && reader.TryReadToken(11, out var token); i++)
            yield return System.Text.Encoding.UTF8.GetString(token.Span);
    }

    private void Apply(GameItem item, ItemRecord record, ItemField field)
    {
        var key = field.Key;
        var value = field.Value;
        int Reference(ItemType type, string id) => RequireIndex(type, id, record.Path, field.Line);
        if (key == "id") { item.Id = value; return; }
        if (item is NamedItem named)
        {
            if (key == "name") { named.Name = value; return; }
            if (key == "itemgraphic") { named.ItemGraphic = unchecked((ushort)Number(value)); return; }
        }
        if (item is ShopItem shop)
        {
            if (key == "cost") { shop.Cost = Number(value); return; }
            if (key == "probability") { shop.Probability = unchecked((byte)Number(value)); return; }
        }

        // C assigns into fixed-width fields after parsing signed 32-bit integers.
        unchecked
        {
            switch (item)
            {
                case ShipItem ship:
                    switch (key)
                    {
                        case "shipgraphic": ship.ShipGraphic = (ushort)Number(value); break;
                        case "ani": ship.Ani = (byte)Number(value); break;
                        case "spd": ship.Spd = (sbyte)Number(value); break;
                        case "dmg": ship.Dmg = (byte)Number(value); break;
                        case "bigshipgraphic": ship.BigShipGraphic = (byte)Number(value); break;
                        case "bigshipgraphx": ship.BigShipGraphX = (byte)Number(value); break;
                        case "bigshipgraphy": ship.BigShipGraphY = (byte)Number(value); break;
                        default: throw Unsupported();
                    }
                    break;
                case WeaponItem weapon:
                    switch (key)
                    {
                        case "opnum": weapon.OpNum = (byte)Number(value); break;
                        case "op0":
                        case "op1":
                            var j = 0;
                            foreach (var token in ReferenceTokens(value, 11))
                                weapon.Op[key[^1] - '0'][j++] = (ushort)Reference(ItemType.Shot, token);
                            break;
                        case "poweruse": weapon.PowerUse = (ushort)Number(value); break;
                        case "position": weapon.Position = (ushort)Number(value); break;
                        case "bigshipgraphic": weapon.BigShipGraphic = (byte)Number(value); break;
                        case "bigshipgraphfrntx": weapon.BigShipGraphFrntX = (byte)Number(value); break;
                        case "bigshipgraphfrnty": weapon.BigShipGraphFrntY = (byte)Number(value); break;
                        case "bigshipgraphrearx": weapon.BigShipGraphRearX = (byte)Number(value); break;
                        case "bigshipgraphreary": weapon.BigShipGraphRearY = (byte)Number(value); break;
                        case "linkgunweapon": weapon.LinkGunWeaponId = value[..Math.Min(value.Length, 10)]; break;
                        case "chargegunweapons": FillReferences(weapon.ChargeGunWeaponIds, value); break;
                        default: throw Unsupported();
                    }
                    break;
                case GeneratorItem generator:
                    switch (key)
                    {
                        case "power": generator.Power = (byte)Number(value); break;
                        case "speed": generator.Speed = (sbyte)Number(value); break;
                        case "bigshipgraphic": generator.BigShipGraphic = (byte)Number(value); break;
                        case "bigshipgraphx": generator.BigShipGraphX = (byte)Number(value); break;
                        case "bigshipgraphy": generator.BigShipGraphY = (byte)Number(value); break;
                        default: throw Unsupported();
                    }
                    break;
                case ShieldItem shield:
                    switch (key)
                    {
                        case "tpwr": shield.Tpwr = (byte)Number(value); break;
                        case "mpwr": shield.Mpwr = (byte)Number(value); break;
                        default: throw Unsupported();
                    }
                    break;
                case SidekickItem sidekick:
                    switch (key)
                    {
                        case "pwr": sidekick.Pwr = (byte)Number(value); break;
                        case "tr": sidekick.Tr = (byte)Number(value); break;
                        case "option": sidekick.Option = (byte)Number(value); break;
                        case "opspd": sidekick.OpSpd = (sbyte)Number(value); break;
                        case "ani": sidekick.Ani = (byte)Number(value); break;
                        case "gr0":
                        case "gr1":
                        case "gr2":
                        case "gr3":
                        case "gr4":
                            Fill(sidekick.Gr[key[^1] - '0'], value, n => (ushort)n); break;
                        case "wport": sidekick.WPortId = value[..Math.Min(value.Length, 10)]; break;
                        case "wpnum": sidekick.Wpnum = (ushort)Reference(ItemType.Shot, value); break;
                        case "ammo": sidekick.Ammo = (byte)Number(value); break;
                        case "stop": sidekick.Stop = Number(value) != 0; break;
                        case "icongr": sidekick.IconGr = (byte)Number(value); break;
                        case "position": sidekick.Position = (byte)Number(value); break;
                        default: throw Unsupported();
                    }
                    break;
                case SpecialItem special:
                    switch (key)
                    {
                        case "pwr": special.Pwr = (byte)Number(value); break;
                        case "stype": special.Stype = (byte)Number(value); break;
                        case "wpn": special.Wpn = (ushort)Reference(ItemType.Shot, value); break;
                        default: throw Unsupported();
                    }
                    break;
                case ShotItem shot:
                    switch (key)
                    {
                        case "drain": shot.Drain = (ushort)Number(value); break;
                        case "shotrepeat": shot.ShotRepeat = (byte)Number(value); break;
                        case "multi": shot.Multi = (byte)Number(value); break;
                        case "weapani": shot.WeapAni = (ushort)Number(value); break;
                        case "max": shot.Max = (byte)Number(value); break;
                        case "tx": shot.Tx = (byte)Number(value); break;
                        case "ty": shot.Ty = (byte)Number(value); break;
                        case "aim": shot.Aim = (byte)Number(value); break;
                        case "attack": Fill(shot.Attack, value, n => (byte)n); break;
                        case "chain": FillReferences(shot.ChainIds, value); break;
                        case "iceticks": Fill(shot.IceTicks, value, n => (byte)n); break;
                        case "infinite": Fill(shot.Infinite, value, n => (byte)n); break;
                        case "del": Fill(shot.Del, value, n => (byte)n); break;
                        case "sx": Fill(shot.Sx, value, n => (sbyte)n); break;
                        case "sy": Fill(shot.Sy, value, n => (sbyte)n); break;
                        case "bx": Fill(shot.Bx, value, n => (sbyte)n); break;
                        case "by": Fill(shot.By, value, n => (sbyte)n); break;
                        case "sg": Fill(shot.Sg, value, n => (ushort)n); break;
                        case "acceleration": shot.Acceleration = (sbyte)Number(value); break;
                        case "accelerationx": shot.AccelerationX = (sbyte)Number(value); break;
                        case "circlesize": shot.CircleSize = (byte)Number(value); break;
                        case "sound": shot.Sound = (byte)Number(value); break;
                        case "trail": shot.Trail = (byte)Number(value); break;
                        case "shipblastfilter": shot.ShipBlastFilter = (byte)Number(value); break;
                        default: throw Unsupported();
                    }
                    break;
                case EnemyItem enemy:
                    switch (key)
                    {
                        case "ani": enemy.Ani = (byte)Number(value); break;
                        case "tur": Fill(enemy.Tur, value, n => (byte)n); break;
                        case "freq": Fill(enemy.Freq, value, n => (byte)n); break;
                        case "xmove": enemy.XMove = (sbyte)Number(value); break;
                        case "ymove": enemy.YMove = (sbyte)Number(value); break;
                        case "xaccel": enemy.XAccel = (sbyte)Number(value); break;
                        case "yaccel": enemy.YAccel = (sbyte)Number(value); break;
                        case "xcaccel": enemy.XcAccel = (sbyte)Number(value); break;
                        case "ycaccel": enemy.YcAccel = (sbyte)Number(value); break;
                        case "startx": enemy.StartX = (short)Number(value); break;
                        case "starty": enemy.StartY = (short)Number(value); break;
                        case "startxc": enemy.StartXc = (sbyte)Number(value); break;
                        case "startyc": enemy.StartYc = (sbyte)Number(value); break;
                        case "armor": enemy.Armor = (byte)Number(value); break;
                        case "esize": enemy.ESize = (byte)Number(value); break;
                        case "egraphic": Fill(enemy.EGraphic, value, n => (ushort)n); break;
                        case "explosiontype": enemy.ExplosionType = (byte)Number(value); break;
                        case "animate": enemy.Animate = (byte)Number(value); break;
                        case "shapebank": enemy.ShapeBank = (byte)Number(value); break;
                        case "xrev": enemy.XRev = (sbyte)Number(value); break;
                        case "yrev": enemy.YRev = (sbyte)Number(value); break;
                        case "dgr": enemy.Dgr = (ushort)Number(value); break;
                        case "dlevel": enemy.DLevel = (sbyte)Number(value); break;
                        case "dani": enemy.DAni = (sbyte)Number(value); break;
                        case "elaunchfreq": enemy.ELaunchFreq = (byte)Number(value); break;
                        case "elaunchtype": enemy.ELaunchTypeId = value[..Math.Min(value.Length, 30)]; break;
                        case "elaunchtypepos": enemy.ELaunchTypePos = (ushort)Number(value); break;
                        case "elaunchspecial": enemy.ELaunchSpecial = (byte)Number(value); break;
                        case "value": enemy.Value = (short)Number(value); break;
                        case "eenemydie": enemy.EEnemyDieId = value[..Math.Min(value.Length, 10)]; break;
                        case "eshotwait": Fill(enemy.EShotWait, value, n => (byte)n); break;
                        case "enemyground": enemy.EnemyGround = Number(value) == 1; break;
                        default: throw Unsupported();
                    }
                    break;
                default: throw Unsupported();
            }
        }

        InvalidDataException Unsupported() => ItemDefinitions.Error(record.Path, field.Line, $"unknown field '{key}'");
    }

    private static readonly (ItemType Type, string Id)[] KnownReferences =
    [
        (ItemType.Generator, "2"), (ItemType.Shield, "2"), (ItemType.Shield, "4"),
        (ItemType.Ship, "12"), (ItemType.Ship, "11"), (ItemType.Ship, "8"), (ItemType.Ship, "13"),
        (ItemType.Ship, "2"), (ItemType.Ship, "1"), (ItemType.Shot, "147"), (ItemType.Shot, "148"),
        (ItemType.SidekickRight, "24"), (ItemType.Special, "13"),
        (ItemType.WeaponFront, "39"), (ItemType.WeaponFront, "23"), (ItemType.WeaponFront, "25"),
        (ItemType.WeaponFront, "36"), (ItemType.WeaponFront, "1"), (ItemType.WeaponFront, "16"),
        (ItemType.WeaponRear, "24"), (ItemType.WeaponRear, "26"), (ItemType.WeaponRear, "37"),
        (ItemType.WeaponRear, "15"), (ItemType.Enemy, "560"), (ItemType.Enemy, "533"), (ItemType.Enemy, "534")
    ];
}
