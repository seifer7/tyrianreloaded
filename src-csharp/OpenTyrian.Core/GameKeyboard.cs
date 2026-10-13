/*
 * OpenTyrian: A modern cross-platform port of Tyrian
 * Copyright (C) 2007-2013 The OpenTyrian Development Team
 * Distributed under the GNU General Public License, version 2 or later.
 * See COPYING for details.
 */

namespace OpenTyrian.Core;

public interface IGameKeyboardHost
{
    void CheckGamma();
    ushort PortConfigurations();
    void InitializeShips();
    void DrawBars();
    void DrawOptions();
    void Help(CancellationToken cancellation);
    void TextWindow();
    uint Ticks { get; }
    void SetSpeed();
    void PlaySong(uint song);
}

public sealed class GameKeyboard(LegacyGameState game, ItemDatabase items, InputState input,
    PlayerCombat combat, LevelEvents level, BackgroundRenderer backgrounds, NetworkRequests requests,
    GameRuntimeSettings settings, PlayerShots shots, ushort[] graphics, EditedShips edited, IGameKeyboardHost host)
{
    public bool NetworkGame { get; set; }
    public bool ConstantPlay { get; set; }
    public bool[] EditedPlayer { get; } = new bool[2];
    public bool SkipPresentation { get; set; }
    public bool Debug { get; set; }
    public float DebugHistory { get; private set; }
    public uint DebugHistoryCount { get; private set; }
    public uint LastDebugTime { get; private set; }
    public bool SuperPause { get; set; }

    public void RecordDebugFrame(uint ticks, Action<float> draw)
    {
        DebugHistory += Math.Abs(unchecked((int)ticks - (int)LastDebugTime));
        DebugHistoryCount = unchecked(DebugHistoryCount + 1);
        draw(1000f / MathF.Round(DebugHistory / DebugHistoryCount, MidpointRounding.AwayFromZero));
        LastDebugTime = ticks;
    }

    public void Run(CancellationToken cancellation = default)
    {
        host.CheckGamma();
        if (!NetworkGame && !game.SuperTyrian && edited.Available)
        {
            if (Held(43)) Edit(0);
            if (Held(57)) Edit(1);
        }
        if (Held(58))
        {
            if (NetworkGame) requests.Help = true;
            else { host.Help(cancellation); SkipPresentation = true; }
        }
        if (Held(59) && Held(61) && Held(63) && Held(64) && Held(66) && Held(49) && Held(56))
        {
            if (NetworkGame) requests.NortShip = true;
            else
            {
                var owner = game.Players[0];
                owner.Items.Ship = Known(KnownItem.ShipNortship);
                owner.Items.Special = Known(KnownItem.SpecialAstralZone);
                owner.Items.Weapons[0].Id = Known(KnownItem.WeaponFrontNortshipSuperPulse);
                owner.Items.Weapons[1].Id = Known(KnownItem.WeaponRearNortshipSpreader);
                graphics[0] = 1;
            }
        }
        if (!NetworkGame && !game.TwoPlayerMode && !game.SuperTyrian && game.SuperArcadeMode == 0)
        {
            if (Held(59) && Held(60) && Held(63)) { combat.Cheating = !combat.Cheating; input.KeysActive[59] = 0; }
            if (Held(59) && Held(60) && (Held(61) || Held(62)))
            {
                foreach (var player in game.Players) player.Armor = 0;
                combat.Cheating = !combat.Cheating; host.TextWindow();
            }
            if (ConstantPlay && Held(6)) { combat.Cheating = !combat.Cheating; input.KeysActive[6] = 0; }
        }
        if (game.SuperTyrian) combat.Cheating = false;
        if (Held(67) && Held(42))
        {
            input.KeysActive[67] = 0; Debug = !Debug;
            DebugHistory = 1; DebugHistoryCount = 1; LastDebugTime = host.Ticks;
        }
        if (Held(59) && Held(63) && (Held(64) || Held(65)) && !Held(66) && !game.SuperTyrian && game.SuperArcadeMode == 0)
        {
            if (NetworkGame) requests.SkipLevel = true;
            else { combat.LevelTimer = true; level.TimerCountdown = 0; level.EndLevel = true; combat.LevelEnd = 40; }
        }
        requests.PausePressed |= Held(19); requests.InGameMenuPressed |= Held(41);
        if (Held(42))
        {
            if (Held(83)) SuperPause = !SuperPause;
            if (Held(69) && Held(71))
            {
                for (var key = 31; key <= 38; key++)
                    if (Held(key)) backgrounds.Smoothies[key - 31] = !backgrounds.Smoothies[key - 31];
                if (Held(39)) backgrounds.Smoothies[8] = !backgrounds.Smoothies[8];
            }
            else if (Held(45))
            {
                if (backgrounds.LevelFilter == -99) backgrounds.LevelFilter = 0;
                else
                {
                    backgrounds.LevelFilter = unchecked((sbyte)(backgrounds.LevelFilter + 1));
                    if (backgrounds.LevelFilter == 16) backgrounds.LevelFilter = -99;
                }
            }
            else if (Held(30))
            {
                settings.FastPlay = unchecked((byte)(settings.FastPlay + 1));
                if (settings.FastPlay > 2) settings.FastPlay = 0;
                input.KeysActive[30] = 0; host.SetSpeed();
            }
            if (Held(71)) host.PlaySong(MersenneTwister.NextUInt32() % 41);
        }

        bool Held(int scan) => input.KeysActive[scan] != 0;
        byte Known(KnownItem item) => checked((byte)items.KnownIndex(item));
        void Edit(int index)
        {
            var player = game.Players[index];
            for (var scan = 30; scan <= 39; scan++)
            {
                if (!Held(scan)) continue;
                var number = scan - 30 + 1;
                var offset = (number - 1) * 15;
                var data = edited.Records.Span;
                player.Items.Ship = (byte)(90 + number);
                player.Items.Weapons[0].Id = data[offset + 1];
                player.Items.Weapons[1].Id = data[offset + 2];
                player.Items.Special = data[offset + 3];
                player.Items.Sidekicks[0] = data[offset + 4]; player.Items.Sidekicks[1] = data[offset + 5];
                player.Items.Generator = data[offset + 6]; player.Items.Shield = data[offset + 8];
                Array.Clear(shots.MultiPosition);
                if (player.WeaponMode > host.PortConfigurations()) player.WeaponMode = 1;
                var armor = unchecked((ushort)player.Armor);
                host.InitializeShips();
                if (player.Armor > armor && EditedPlayer[index]) player.Armor = armor;
                else EditedPlayer[index] = true;
                host.DrawBars(); host.DrawOptions(); input.KeysActive[scan] = 0;
            }
        }
    }
}

public sealed class GameKeyboardHost(GameGamma gamma, ReadOnlyMemory<PaletteColor> picturePalette,
    PlayerCombat combat, ItemDatabase items, PlayerShipInfo ships, GameHelp help, ReadOnlyMemory<byte> continueText, GameTextWindow textWindow,
    ReadOnlyMemory<byte> cheatingText, GameRuntimeSettings settings, DelayTimer timer,
    InputState input, Action drawOptions, Action<uint> playSong) : IGameKeyboardHost
{
    public void CheckGamma() => gamma.Check(picturePalette.Span);
    public ushort PortConfigurations() => combat.PortConfigurations(items.Weapons);
    public void InitializeShips() => ships.Initialize();
    public void DrawBars() { combat.WipeBars(); combat.DrawArmor(); combat.DrawShield(); }
    public void DrawOptions() => drawOptions();
    public void Help(CancellationToken cancellation) => help.Run(combat.TwoPlayers, continueText, cancellation);
    public void TextWindow() => textWindow.Show(cheatingText);
    public uint Ticks => input.Ticks;
    public void SetSpeed() => settings.SetGameSpeed(timer);
    public void PlaySong(uint song) => playSong(song);
}
