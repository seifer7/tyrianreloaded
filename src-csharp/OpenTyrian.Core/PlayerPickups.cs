/*
 * OpenTyrian: A modern cross-platform port of Tyrian
 * Copyright (C) 2007-2013 The OpenTyrian Development Team
 * Distributed under the GNU General Public License, version 2 or later.
 * See COPYING for details.
 */

namespace OpenTyrian.Core;

public sealed class PlayerPickups(IReadOnlyList<PlayerState> players, EnemySystem enemies, PlayerCombat combat,
    PlayerShots shots, ItemDatabase items, LevelEvents level, SceneEffects effects,
    IReadOnlyList<string> text, IReadOnlyList<string> secondaryText)
{
    public bool NetworkGame { get; set; }
    public byte LocalPlayer { get; set; } = 1;
    public string LocalName { get; set; } = "";
    public string OpponentName { get; set; } = "";
    public byte ArcadeMode { get; set; }
    public bool BonusLevel { get; set; }
    public ushort NextLevel { get; set; }
    public ushort DisplayTime { get; set; }
    public byte DamageRate { get => level.DamageRate; set => level.DamageRate = value; }
    private static readonly int[][] ArcadeWeapons =
    [
        [9, 31, 32, 33, 34], [19, 8, 22, 41, 34], [27, 5, 20, 42, 31],
        [15, 3, 28, 22, 12], [23, 35, 25, 14, 6], [2, 5, 21, 4, 7], [40, 38, 37, 41, 36]
    ];
    private static readonly byte[] ArcadeSpecials =
        [17, 17, 18, 0, 0, 0, 10, 0, 0, 0, 0, 0, 44, 0, 10, 0, 19, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 45, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0];
    private static readonly byte[][][] Options =
    [
        [[0, 0], [0, 0], [0, 0]], [[1, 1], [16, 16], [30, 30]], [[2, 2], [29, 29], [29, 20]],
        [[3, 3], [21, 21], [12, 0]], [[4, 4], [18, 18], [16, 23]], [[0, 0], [0, 0], [0, 0]],
        [[6, 6], [29, 16], [0, 22]], [[7, 7], [19, 19], [19, 28]], [[0, 0], [0, 0], [0, 0]],
        [[0, 0], [0, 0], [0, 0]], [[10, 10], [21, 21], [21, 27]], [[0, 0], [0, 0], [0, 0]],
        [[0, 0], [0, 0], [0, 0]], [[13, 13], [17, 17], [13, 26]], [[0, 0], [0, 0], [0, 0]],
        [[15, 15], [15, 16], [15, 16]]
    ];

    public string GetName(byte number)
    {
        if (number is < 1 or > 2) throw new ArgumentOutOfRangeException(nameof(number));
        var local = CString(LocalName); var opponent = CString(OpponentName);
        if (number == LocalPlayer && local.Length != 0) return local;
        if (opponent.Length != 0) return opponent;
        return Text(47 + number);
    }

    public string DefaultName(byte number)
    {
        if (number is < 1 or > 2) throw new ArgumentOutOfRangeException(nameof(number));
        return Text(47 + number);
    }

    public void CollectBonusLevel(int index, short value, Action<uint> playSong)
    {
        Validate(index);
        if (BonusLevel) return;
        if (value <= 10000) throw new ArgumentOutOfRangeException(nameof(value));
        playSong(30); BonusLevel = true; NextLevel = (ushort)(value - 10000);
        enemies.Enemies[index].Availability = 1; DisplayTime = 150;
    }

    public void CollectScore(PlayerState player, byte number, int index, short value, int screenX,
        ushort mouseX, ushort mouseY, Action<string> showText)
    {
        Validate(index, number);
        enemies.Enemies[index].Availability = 1; effects.SoundQueue[7] = 18;
        switch (value)
        {
            case 1: enemies.CubeCount = unchecked((ushort)(enemies.CubeCount + 1)); effects.SoundQueue[3] = 37; break;
            case -1:
                showText(UpgradeText(1, 44));
                PlayerRules.PowerUpWeapon(players[0], WeaponPort.Front, shots.MultiPosition); effects.SoundQueue[7] = 29; break;
            case -2:
                showText(UpgradeText(2, 45));
                PlayerRules.PowerUpWeapon(players[combat.TwoPlayers ? 1 : 0], WeaponPort.Rear, shots.MultiPosition);
                effects.SoundQueue[7] = 29; break;
            case -3:
                if (index >= shots.Availability.Length)
                    throw new InvalidDataException("Native score-item projectile clear exceeds player shot capacity.");
                shots.MultiPosition[4] = 0;
                shots.Create(0, 4, unchecked((ushort)player.X), unchecked((ushort)player.Y), mouseX, mouseY, 104, number);
                shots.Availability[index] = 0; break;
            case -4: if (players[number - 1].Superbombs < 10) players[number - 1].Superbombs++; break;
            case -5:
                players[0].Items.Weapons[0].Id = checked((byte)items.KnownIndex(KnownItem.WeaponFrontHotdog));
                players[0].Items.Weapons[1].Id = players[1].Items.Weapons[1].Id = checked((byte)items.KnownIndex(KnownItem.WeaponRearHotdog));
                players[0].LastItems = players[0].Items.Copy();
                foreach (var owner in players) owner.WeaponMode = 1;
                Array.Clear(shots.MultiPosition); break;
            default:
                if (combat.Linked)
                {
                    // COUNTOF promotes the native division to unsigned int before cash addition.
                    var reward = unchecked((uint)value) / 2;
                    foreach (var owner in players) owner.Cash = unchecked(owner.Cash + reward);
                }
                else player.Cash = unchecked(player.Cash + (ulong)(long)value);
                break;
        }
        var enemy = enemies.Enemies[index];
        effects.SetupExplosion(screenX, enemy.Y, 0, enemy.ExplosionNumber, true, false);
    }

    public void CollectArcade(PlayerState player, byte number, int index, short value,
        Action<string> showText, Action drawOptions)
    {
        Validate(index, number);
        var enemy = enemies.Enemies[index];
        if (value == 30000)
        {
            player.Cash = unchecked(player.Cash + 100);
            if (!combat.Galaga) PlayerRules.GotPurpleBall(player, shots.MultiPosition);
            else
            {
                if (combat.TwoPlayers) player.Cash = unchecked(player.Cash + 2400);
                combat.TwoPlayers = combat.Linked = true;
                players[1].Items.Weapons[1].Power = 1; players[1].Armor = 10; players[1].IsAlive = true;
            }
            enemy.Availability = 1; effects.SoundQueue[7] = 29;
        }
        else if (ArcadeMode != 0 && value > 30000)
        {
            if (ArcadeMode > ArcadeWeapons.Length || value > 30005)
                throw new InvalidDataException("Arcade pickup exceeds native weapon tables.");
            shots.MultiPosition[0] = 0; shots.Repeat[0] = 10;
            var weapon = Index(ItemType.WeaponFront, ArcadeWeapons[ArcadeMode - 1][value - 30001]);
            if (weapon == players[0].Items.Weapons[0].Id)
            {
                player.Cash = unchecked(player.Cash + 1000);
                PlayerRules.PowerUpWeapon(player, WeaponPort.Front, shots.MultiPosition);
            }
            else PlayerRules.GotPurpleBall(player, shots.MultiPosition);
            players[0].Items.Weapons[0].Id = weapon;
            player.Cash = unchecked(player.Cash + 200); effects.SoundQueue[7] = 29; enemy.Availability = 1;
        }
        else if (value > 32100)
        {
            if (number != 1) return;
            player.Cash = unchecked(player.Cash + 250);
            players[0].Items.Special = Index(ItemType.Special, value - 32100);
            shots.MultiPosition[8] = shots.MultiPosition[10] = 0; shots.Repeat[8] = 10; shots.Repeat[10] = 0;
            showText(PickupText(1, RawName(ItemType.Special, value - 32100)));
            effects.SoundQueue[7] = 29; enemy.Availability = 1;
        }
        else if (value > 32000)
        {
            var id = value - 32000;
            if (number == 2)
            {
                enemy.Availability = 1; showText(PickupText(2, RawName(ItemType.SidekickLeft, id)));
                var owner = players[1].Items;
                if (id != owner.SidekickSeries) { owner.SidekickSeries = (byte)id; owner.SidekickLevel = 101; }
                else if (owner.SidekickLevel < 103) owner.SidekickLevel++;
                var upgrade = owner.SidekickLevel - 101;
                if (owner.SidekickSeries >= Options.Length || (uint)upgrade >= 3)
                    throw new InvalidDataException("Sidekick pickup exceeds native upgrade tables.");
                for (var i = 0; i < 2; i++) owner.Sidekicks[i] = Index(i == 0 ? ItemType.SidekickLeft : ItemType.SidekickRight,
                    Options[owner.SidekickSeries][upgrade][i]);
                shots.MultiPosition[2] = shots.MultiPosition[3] = 0; drawOptions(); effects.SoundQueue[7] = 29;
            }
            else if (level.OnePlayerAction)
            {
                enemy.Availability = 1; showText($"{Text(63)} {RawName(ItemType.SidekickLeft, id)}");
                players[0].Items.Sidekicks[0] = Index(ItemType.SidekickLeft, id);
                players[0].Items.Sidekicks[1] = Index(ItemType.SidekickRight, id);
                shots.MultiPosition[2] = shots.MultiPosition[3] = 0; drawOptions(); effects.SoundQueue[7] = 29;
            }
            if (enemy.Availability == 1) player.Cash = unchecked(player.Cash + 250);
        }
        else if (value > 31000)
        {
            player.Cash = unchecked(player.Cash + 250);
            if (number == 2)
            {
                showText(PickupText(2, RawName(ItemType.WeaponRear, value - 31000)));
                players[1].Items.Weapons[1].Id = Index(ItemType.WeaponRear, value - 31000);
                shots.MultiPosition[1] = 0; enemy.Availability = 1; effects.SoundQueue[7] = 29;
            }
            else if (level.OnePlayerAction)
            {
                showText($"{Text(63)} {RawName(ItemType.WeaponRear, value - 31000)}");
                players[0].Items.Weapons[1].Id = Index(ItemType.WeaponRear, value - 31000);
                shots.MultiPosition[1] = 0; enemy.Availability = 1; effects.SoundQueue[7] = 29;
                if (players[0].Items.Weapons[1].Power == 0) players[0].Items.Weapons[1].Power = 1;
            }
        }
        else if (value > 30000)
        {
            var id = value - 30000;
            if (number == 1 && combat.TwoPlayers)
            {
                showText(PickupText(1, RawName(ItemType.WeaponFront, id)));
                players[0].Items.Weapons[0].Id = Index(ItemType.WeaponFront, id);
                shots.MultiPosition[0] = 0; enemy.Availability = 1; effects.SoundQueue[7] = 29;
            }
            else if (level.OnePlayerAction)
            {
                showText($"{Text(63)} {RawName(ItemType.WeaponFront, id)}");
                players[0].Items.Weapons[0].Id = Index(ItemType.WeaponFront, id);
                shots.MultiPosition[0] = 0; enemy.Availability = 1; effects.SoundQueue[7] = 29;
            }
            if (enemy.Availability == 1)
            {
                if (id > ArcadeSpecials.Length) throw new InvalidDataException("Arcade pickup exceeds native special table.");
                players[0].Items.Special = Index(ItemType.Special, ArcadeSpecials[id - 1]);
                if (players[0].Items.Special > 0)
                { shots.MultiPosition[8] = shots.MultiPosition[10] = shots.Repeat[8] = shots.Repeat[10] = 0; }
                player.Cash = unchecked(player.Cash + 250);
            }
        }
    }

    public void Collide(PlayerState player, byte number, ushort mouseX, ushort mouseY,
        Action<string> showText, Action drawOptions, Action<uint> playSong)
    {
        if (number is < 1 or > 2) throw new ArgumentOutOfRangeException(nameof(number));
        for (var i = 0; i < 100; i++)
        {
            var enemy = enemies.Enemies[i];
            if (enemy.Availability == 1) continue;
            var x = enemy.X + enemy.MapOffset;
            if (Math.Abs(player.X - x) >= 12 || Math.Abs(player.Y - enemy.Y) >= 14) continue;
            var value = enemy.Value;
            if (value > 29999) CollectArcade(player, number, i, value, showText, drawOptions);
            else if (value > 20000) enemies.CollectArmorPickup(player, i, value, combat);
            else if (value > 10000 && enemy.Availability == 2) CollectBonusLevel(i, value, playSong);
            else if (enemy.ScoreItem) CollectScore(player, number, i, value, x, mouseX, mouseY, showText);
            else if (player.InvulnerableTicks == 0 && enemy.Availability == 0 && !enemy.Ground)
                enemies.DamageOnCollision(player, i, DamageRate, combat);
        }
    }

    private string UpgradeText(byte number, int index) => NetworkGame ? $"{GetName(number)} {Secondary(3)} {Text(index)}" :
        combat.TwoPlayers ? $"{Text(number == 1 ? 42 : 43)} {Text(index)}" : Text(index);
    private string PickupText(byte number, string name) => NetworkGame ? $"{GetName(number)} {Secondary(3)} {name}" :
        $"{Text(number == 2 ? 43 : combat.TwoPlayers ? 42 : 63)} {name}";
    private string Text(int index) => index < text.Count ? CString(text[index]) : throw new InvalidDataException("Pickup text is missing.");
    private string Secondary(int index) => index < secondaryText.Count ? CString(secondaryText[index]) : throw new InvalidDataException("Pickup text is missing.");
    private static string CString(string value) { var end = value.IndexOf('\0'); return end < 0 ? value : value[..end]; }
    private byte Index(ItemType type, int id) => items.TryFind(type, id, out var index) && index <= byte.MaxValue
        ? (byte)index : throw new InvalidDataException($"Pickup {type} item {id} is missing or exceeds byte indices.");
    private string RawName(ItemType type, int index) => (uint)index < items.Items(type).Count && items.Items(type)[index] is NamedItem named
        ? CString(named.Name) : throw new InvalidDataException("Pickup display name exceeds native raw item table.");
    private static void Validate(int index, byte number = 1)
    {
        if ((uint)index >= 100) throw new ArgumentOutOfRangeException(nameof(index));
        if (number is < 1 or > 2) throw new ArgumentOutOfRangeException(nameof(number));
    }
}
