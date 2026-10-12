/*
 * OpenTyrian: A modern cross-platform port of Tyrian
 * Copyright (C) 2007-2013 The OpenTyrian Development Team
 * Distributed under the GNU General Public License, version 2 or later.
 * See COPYING for details.
 */

namespace OpenTyrian.Core;

public sealed class PlayerSpecialWeapons(ItemDatabase items, PlayerShots shots, PlayerWeapons weapons,
    PlayerCombat combat, EnemySystem enemies, SceneEffects effects, LevelEvents level, BackgroundRenderer backgrounds)
{
    public byte NextWait { get; set; }
    public byte Wait { get; set; }
    public byte ZinglonDuration { get; set; }
    public byte AstralDuration { get; set; }
    public ushort FlareDuration { get; set; }
    public bool FlareStart { get; set; }
    public bool LinkToPlayer { get; set; }
    public bool Spray { get; set; }
    public bool FireButtonHeld { get; set; }
    public sbyte Filter { get; set; }
    public sbyte Frequency { get; set; }
    public sbyte ColorChange { get; set; }
    public ushort Weapon { get; set; }
    public byte PowerParameter { get; set; }
    public byte ArcadeMode { get; set; }

    public void Complete(byte playerNumber, byte specialIndex, ushort mouseX, ushort mouseY, Action drawOptions)
    {
        if (playerNumber is < 1 or > 2) throw new ArgumentOutOfRangeException(nameof(playerNumber));
        var special = items.Specials[specialIndex];
        var players = shots.Players;
        NextWait = 0;
        switch (special.Stype)
        {
            case 1:
                Create(10, players[playerNumber - 1].X, players[playerNumber - 1].Y, special.Wpn, playerNumber);
                shots.Repeat[8] = shots.Repeat[10];
                break;
            case 2:
                foreach (var shot in enemies.EnemyShots.Where(shot => !shot.Available))
                {
                    shot.XVelocity = unchecked((short)(shot.XVelocity - Math.Sign(players[0].X - shot.X)));
                    shot.YVelocity = unchecked((short)(shot.YVelocity - Math.Sign(players[0].Y - shot.Y)));
                }
                break;
            case 3:
                Zinglon();
                break;
            case 4:
                foreach (var enemy in enemies.Enemies.Take(100).Where(enemy => enemy.Availability != 1 && enemy.ScoreItem && enemy.Value != 0))
                {
                    enemy.XSpeed = unchecked((sbyte)(enemy.XSpeed + Math.Sign(players[0].X - enemy.X)));
                    enemy.YSpeed = unchecked((sbyte)(enemy.YSpeed + Math.Sign(players[0].Y - enemy.Y)));
                }
                break;
            case >= 5 and <= 11:
            case 16:
                if (FlareDuration == 0) FlareStart = true;
                Weapon = special.Wpn; LinkToPlayer = Spray = false;
                var power = players[0].Items.Weapons[0].Power;
                switch (special.Stype)
                {
                    case 5: Filter = 7; Frequency = 2; FlareDuration = 50; break;
                    case 6: Filter = 1; Frequency = 7; FlareDuration = (ushort)(200 + 25 * power); break;
                    case 7: Filter = 3; Frequency = 3; FlareDuration = (ushort)(50 + 10 * power); Zinglon(); break;
                    case 8: Filter = -99; Frequency = 7; FlareDuration = (ushort)(10 + power); break;
                    case 9:
                        Filter = -99; Frequency = 8; FlareDuration = (ushort)(8 + 2 * power);
                        LinkToPlayer = true; NextWait = special.Pwr; break;
                    case 10:
                        Filter = -99; Frequency = 8; FlareDuration = (ushort)(14 + 4 * power);
                        LinkToPlayer = true; break;
                    case 11:
                        Filter = -99; Frequency = unchecked((sbyte)special.Pwr); FlareDuration = (ushort)(10 + 10 * power);
                        AstralDuration = unchecked((byte)(20 + 10 * power)); break;
                    case 16:
                        Filter = -99; Frequency = 8; FlareDuration = (ushort)(PowerParameter * 16 + 8);
                        LinkToPlayer = Spray = true; break;
                }
                break;
            case 12:
                players[playerNumber - 1].InvulnerableTicks = (uint)(PowerParameter * 10);
                if (ArcadeMode is > 0 and <= 7)
                {
                    shots.Repeat[8] = 250;
                    Create(10, players[0].X, players[0].Y, 707, 1);
                    players[0].InvulnerableTicks = 100;
                }
                break;
            case 13:
            case 14:
                var target = players[special.Stype == 13 ? 0 : 1];
                target.Armor = unchecked(target.Armor + (uint)(PowerParameter / 4 + 1));
                effects.SoundQueue[3] = 29;
                break;
            case 17:
                effects.SoundQueue[3] = 29;
                var slot = players[0].Items.Sidekicks[0] == special.Wpn ? 1 : 0;
                players[0].Items.Sidekicks[slot] = unchecked((byte)special.Wpn);
                // Native code resets slots 0/1 here, not the firing bays 2/3.
                shots.MultiPosition[slot] = 0;
                drawOptions();
                break;
            case 18:
                players[0].Items.Sidekicks[1] = unchecked((byte)special.Wpn);
                drawOptions();
                effects.SoundQueue[4] = 29;
                shots.MultiPosition[1] = 0;
                break;
        }

        void Create(uint bay, int x, int y, ushort shot, byte owner)
            => shots.Create(0, bay, unchecked((ushort)x), unchecked((ushort)y), mouseX, mouseY, shot, owner);
        void Zinglon() { ZinglonDuration = 50; shots.Repeat[8] = 100; effects.SoundQueue[7] = 21; }
    }

    public void Update(byte playerNumber, ushort mouseX, ushort mouseY, ReadOnlySpan<bool> buttons,
        IndexedSurface surface, IndexedSurface hud, SpriteSheet indicators, SpriteTable icons)
        => Update(playerNumber, mouseX, mouseY, buttons, surface,
            (x, y, frame) => indicators.Blit(surface, x, y, frame), () => weapons.DrawOptions(hud, icons));

    public void Update(byte playerNumber, ushort mouseX, ushort mouseY, ReadOnlySpan<bool> buttons,
        IndexedSurface surface, Action<int, int, int> drawIndicator, Action drawOptions)
    {
        if (playerNumber is < 1 or > 2) throw new ArgumentOutOfRangeException(nameof(playerNumber));
        if (buttons.Length < 4) throw new ArgumentException("Special weapons require four buttons.", nameof(buttons));
        var players = shots.Players;
        var player = players[playerNumber - 1];
        if (players[0].Items.Special > 0)
            drawIndicator(47, 4, shots.Repeat[8] == 0 && Wait == 0 && FlareDuration < 2 && ZinglonDuration < 2 ? 94 : 93);
        if (shots.Repeat[8] > 0) shots.Repeat[8]--;
        if (Wait > 0) Wait--;
        var executed = weapons.ExecutedSpecial[playerNumber - 1];
        if (executed > 0 && shots.Repeat[8] == 0 && FlareDuration == 0)
        {
            PowerParameter = items.Specials[executed].Pwr;
            var affordable = true;
            if (PowerParameter > 0)
            {
                if (PowerParameter < 98)
                {
                    if (player.Shield >= PowerParameter) player.Shield -= PowerParameter;
                    else affordable = false;
                }
                else if (PowerParameter == 98)
                {
                    if (player.Shield < 4) affordable = false;
                    PowerParameter = unchecked((byte)player.Shield); player.Shield = 0;
                }
                else if (PowerParameter == 99)
                {
                    PowerParameter = unchecked((byte)(player.Shield / 2)); player.Shield = PowerParameter;
                }
                else
                {
                    PowerParameter -= 100;
                    if (player.Armor > PowerParameter) player.Armor -= PowerParameter;
                    else affordable = false;
                }
            }
            shots.MultiPosition[8] = shots.MultiPosition[10] = 0;
            if (affordable) Complete(playerNumber, executed, mouseX, mouseY, drawOptions);
            weapons.ExecutedSpecial[playerNumber - 1] = 0;
            combat.WipeBars(); combat.DrawShield(); combat.DrawArmor();
        }
        if (playerNumber == 1 && players[0].Items.Special > 0)
        {
            if (ArcadeMode > 0 && (buttons[1] || buttons[2])) FireButtonHeld = false;
            if (!buttons[0] && !(ArcadeMode != 0 && (buttons[1] || buttons[2]))) FireButtonHeld = false;
            else if (shots.Repeat[8] == 0 && !FireButtonHeld && FlareDuration == 0 && Wait == 0)
            {
                FireButtonHeld = true;
                Complete(playerNumber, players[0].Items.Special, mouseX, mouseY, drawOptions);
            }
        }
        if (AstralDuration > 0) AstralDuration--;
        shots.Availability[PlayerShots.Capacity - 1] = 0;
        if (FlareDuration > 1)
        {
            if (Filter != -99)
            {
                if (backgrounds.LevelFilter == -99 && backgrounds.Brightness == -99) level.FilterActive = false;
                if (!level.FilterActive)
                {
                    backgrounds.LevelFilter = Filter;
                    if (Filter == 7) backgrounds.Brightness = 0;
                    level.FilterActive = true;
                }
                ColorChange = MersenneTwister.NextUInt32() % 2 == 0 ? (sbyte)-1 : (sbyte)1;
                if (backgrounds.LevelFilter == 7)
                {
                    if (backgrounds.Brightness < -6) ColorChange = 1;
                    if (backgrounds.Brightness > 6) ColorChange = -1;
                    backgrounds.Brightness = unchecked((sbyte)(backgrounds.Brightness + ColorChange));
                }
            }
            if ((int)(MersenneTwister.NextUInt32() % 6) < Frequency)
            {
                var id = (short)PlayerShots.Capacity;
                if (LinkToPlayer)
                {
                    if (shots.Repeat[8] == 0)
                        id = shots.Create(0, 8, unchecked((ushort)players[0].X), unchecked((ushort)players[0].Y),
                            mouseX, mouseY, Weapon, playerNumber);
                }
                else
                {
                    var y = (ushort)(MersenneTwister.NextUInt32() % 180);
                    var x = (ushort)(MersenneTwister.NextUInt32() % 280);
                    id = shots.Create(0, 8, x, y, mouseX, mouseY, Weapon, playerNumber);
                }
                if (Spray && id != PlayerShots.Capacity)
                {
                    var shot = shots.Shots[id];
                    shot.XM = unchecked((short)(MersenneTwister.NextUInt32() % 5 - 2));
                    shot.YM = unchecked((short)(MersenneTwister.NextUInt32() % 5 - 2));
                    if (shot.YM == 0) shot.YM++;
                }
            }
            FlareDuration--;
            if (FlareDuration == 1) Wait = NextWait;
        }
        else if (FlareStart)
        {
            FlareStart = false; shots.Repeat[8] = LinkToPlayer ? (byte)15 : (byte)200; FlareDuration = 0;
            if (backgrounds.LevelFilter == Filter)
            { backgrounds.LevelFilter = backgrounds.Brightness = -99; level.FilterActive = false; }
        }
        if (ZinglonDuration > 1)
        {
            var radius = 25 - Math.Abs(ZinglonDuration - 25);
            surface.BrightenRectangle(players[0].X + 7 - radius, 0, players[0].X + 7 + radius, 184);
            surface.BrightenRectangle(players[0].X + 5 - radius, 0, players[0].X + 9 + radius, 184);
            ZinglonDuration--;
            if (ZinglonDuration % 5 == 0) shots.Availability[PlayerShots.Capacity - 1] = 1;
        }
    }
}
