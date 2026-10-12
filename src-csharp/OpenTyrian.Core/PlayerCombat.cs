/*
 * OpenTyrian: A modern cross-platform port of Tyrian
 * Copyright (C) 2007-2009 The OpenTyrian Development Team
 * Distributed under the GNU General Public License, version 2 or later.
 * See COPYING for details.
 */

namespace OpenTyrian.Core;

public sealed class PlayerCombat
{
    private readonly IReadOnlyList<PlayerState> _players;
    private readonly SceneEffects _effects;
    private readonly IndexedSurface _hud;
    internal IReadOnlyList<PlayerState> Players => _players;
    internal SceneEffects Effects => _effects;
    public bool TwoPlayers { get; set; }
    public bool Galaga { get; set; }
    public bool Cheating { get; set; }
    public bool LevelTimer { get; set; }
    public byte LevelEnd { get; set; }
    public ushort MusicVolume { get; set; }
    public ushort TemporaryVolume { get; set; }

    public PlayerCombat(IReadOnlyList<PlayerState> players, SceneEffects effects, IndexedSurface hud)
    {
        if (players.Count != 2) throw new ArgumentException("Combat requires the native two-player state.", nameof(players));
        _players = players;
        _effects = effects;
        _hud = hud;
    }

    public byte Damage(byte amount, PlayerState player)
    {
        var damage = (byte)0;
        _effects.SoundQueue[7] = 27;
        if (player.Shield < amount)
        {
            damage = amount;
            amount = unchecked((byte)(amount - player.Shield));
            player.Shield = 0;
            if (amount > 0)
            {
                if (player.Armor < amount)
                {
                    player.Armor = 0;
                    if (player.IsAlive && !Cheating)
                    {
                        LevelTimer = false;
                        player.IsAlive = false;
                        player.ExplodingTicks = 60;
                        LevelEnd = 40;
                        TemporaryVolume = MusicVolume;
                        _effects.SoundQueue[1] = 22;
                    }
                }
                else
                {
                    player.Armor -= amount;
                    _effects.SoundQueue[7] = 19;
                }
            }
        }
        else
        {
            player.Shield -= amount;
            foreach (var (x, y, type) in ShieldExplosions)
                _effects.SetupExplosion(unchecked(player.X + x), unchecked(player.Y + y), 0, type, false, !TwoPlayers);
        }
        WipeBars();
        DrawShield();
        DrawArmor();
        return damage;
    }

    public void WipeBars()
    {
        if (!TwoPlayers || Galaga)
        {
            _hud.FillRectangle(270, 137, 278, unchecked((int)(194u - _players[0].Shield * 2)), 0);
            _hud.FillRectangle(307, 137, 315, unchecked((int)(194u - _players[0].Armor * 2)), 0);
        }
        else
        {
            _hud.FillRectangle(270, 16, 278, 60, 0);
            _hud.FillRectangle(270, 150, 278, 194, 0);
            _hud.FillRectangle(307, 16, 315, 60, 0);
            _hud.FillRectangle(307, 150, 315, 194, 0);
        }
    }

    public void DrawShield()
    {
        if (TwoPlayers && !Galaga)
        {
            for (var i = 0; i < _players.Count; i++)
                DrawBar(_hud, 270, (short)(60 + 134 * i), RoundedGauge(_players[i].Shield), 144);
        }
        else
        {
            var player = _players[0];
            DrawBar(_hud, 270, 194, unchecked((short)player.Shield), 144);
            if (player.Shield != player.ShieldMax)
            {
                var y = unchecked((int)(193u - player.ShieldMax * 2));
                _hud.Rectangle(270, y, 278, y, 68);
            }
        }
    }

    public void DrawArmor()
    {
        foreach (var player in _players) player.Armor = Math.Min(player.Armor, 28);
        if (TwoPlayers && !Galaga)
        {
            for (var i = 0; i < _players.Count; i++)
                DrawBar(_hud, 307, (short)(60 + 134 * i), RoundedGauge(_players[i].Armor), 224);
        }
        else DrawBar(_hud, 307, 194, (short)_players[0].Armor, 224);
    }

    public ushort PortConfigurations(IReadOnlyList<WeaponItem> weapons)
        => weapons[_players[TwoPlayers ? 1 : 0].Items.Weapons[(int)WeaponPort.Rear].Id].OpNum;

    public static void DrawBar(IndexedSurface surface, short x, short y, short count, short color)
    {
        if (count >= 255) throw new ArgumentOutOfRangeException(nameof(count), "Native byte-loop gauges at 255 or above do not terminate.");
        var wait = 2;
        color = unchecked((short)(color + 2));
        for (var i = 0; i <= count; i++)
        {
            surface.Rectangle(x, y - 1, x + 8, y, color);
            if (wait > 0) wait--;
            else
            {
                color = unchecked((short)(color + 1));
                wait = 1;
            }
            y = unchecked((short)(y - 2));
        }
    }

    private static short RoundedGauge(uint value)
    {
        var scaled = MathF.Round(value * 0.8f, MidpointRounding.AwayFromZero);
        if (scaled > short.MaxValue) throw new InvalidDataException("Native gauge floating conversion exceeds a signed 16-bit value.");
        return (short)scaled;
    }

    private static readonly (int X, int Y, uint Type)[] ShieldExplosions =
    [
        (-17,-12,14), (-5,-12,15), (7,-12,16), (19,-12,17),
        (-17,2,18), (19,2,19), (-17,16,20), (-5,16,21), (7,16,22)
    ];
}
