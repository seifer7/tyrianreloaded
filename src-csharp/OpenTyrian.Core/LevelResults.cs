/*
 * OpenTyrian: A modern cross-platform port of Tyrian
 * Copyright (C) 2007-2013 The OpenTyrian Development Team
 * Distributed under the GNU General Public License, version 2 or later.
 * See COPYING for details.
 */

using System.Globalization;
using System.Text;

namespace OpenTyrian.Core;

public interface ILevelResultsHost
{
    void WipeKey();
    void SetWhite();
    void PlaySample(byte sample);
    void PlaySong(uint song);
    void Glow(int x, int y, ReadOnlyMemory<byte> text, CancellationToken cancellation);
    void Cube(ushort x, ushort y);
    void CubeHue(ushort x, ushort y, sbyte brightness);
    void Present();
    void SetDelay(int frames);
    bool AnyButton();
    void KeepAlive();
    void Wait();
    void WaitJoystickRelease(CancellationToken cancellation);
    void FadeBlack(int steps);
    void Clear();
}

public sealed class LevelResults(LegacyGameState game, LegacySaveArchive archive, PlayerProgression progression,
    EnemySystem enemies, PlayerCombat combat, LevelEvents level, PlayerPickups pickups, TextGlow glow,
    IReadOnlyList<byte[]> text, ILevelResultsHost host)
{
    public bool ConstantPlay { get; set; }
    // Native unlock indices 100..103 overlap the regenerated save checksum bytes.
    public byte[] ChecksumScratch { get; } = new byte[4];

    public void Run(CancellationToken cancellation = default)
    {
        if (!ConstantPlay)
        {
            var special = game.Players[0].Items.Special;
            if (special < 21) Unlock(81 + special);
            foreach (var player in game.Players)
            {
                foreach (var weapon in player.Items.Weapons) Unlock(weapon.Id);
                foreach (var sidekick in player.Items.Sidekicks) Unlock(51 + sidekick);
            }
        }
        progression.AdjustDifficulty();
        game.Players[0].LastItems = game.Players[0].Items.Copy();
        game.LastLevelName = CString(game.LevelName).ToArray();
        host.WipeKey();
        glow.FrameCount = 4; glow.Font = Font.Normal;
        host.SetWhite();
        if (!combat.LevelTimer || level.TimerCountdown > 0 || game.Episode != 4) host.PlaySample(34);
        else host.PlaySong(21);
        if (pickups.BonusLevel) Show(20, 20, Text(16));
        else Show(20, 20, Join(Text(PlayerRules.AllAlive(game.Players[0], game.Players[1], game.TwoPlayerMode) ? 26 : 61), game.LevelName));
        if (game.TwoPlayerMode)
            for (var i = 0; i < 2; i++) Show(30, 50 + 20 * i, Join(Text(40 + i), Cash(i)));
        else Show(30, 50, Join(Text(27), Cash(0)));
        var percentage = enemies.TotalEnemies == 0 ? (byte)0 : unchecked((byte)(enemies.KilledEnemies * 100 / enemies.TotalEnemies));
        Show(40, 90, Join(Text(62), Encoding.ASCII.GetBytes(percentage.ToString(CultureInfo.InvariantCulture) + "%")));
        if (!ConstantPlay) archive.EditorLevel = unchecked((ushort)(archive.EditorLevel + percentage / 5));
        if (!game.OnePlayerAction && !game.TwoPlayerMode)
        {
            Show(30, 120, Text(3));
            if (enemies.CubeCount > 0)
            {
                if (enemies.CubeCount > 4) enemies.CubeCount = 4;
                if (glow.FrameCount != 0) glow.FrameCount = 1;
                for (var cube = 1; cube <= enemies.CubeCount; cube++)
                {
                    cancellation.ThrowIfCancellationRequested();
                    host.KeepAlive(); host.PlaySample(18);
                    var x = (ushort)(20 + 30 * cube);
                    host.Cube(x, 135); host.Present();
                    for (var brightness = -15; brightness <= 10; brightness++) CubeFrame(x, (sbyte)brightness);
                    for (var brightness = 10; brightness >= 0; brightness--) CubeFrame(x, (sbyte)brightness);
                }
            }
            else Show(50, 135, Text(14));
        }
        var animated = glow.FrameCount != 0;
        if (animated) glow.FrameCount = 6;
        Show(90, game.TwoPlayerMode ? 150 : 160, Text(4));
        if (!ConstantPlay)
        {
            do
            {
                cancellation.ThrowIfCancellationRequested();
                host.SetDelay(1); host.KeepAlive(); host.Wait();
            } while (!(host.AnyButton() || glow.FrameCount == 0 && animated));
        }
        host.WaitJoystickRelease(cancellation);
        host.FadeBlack(15); host.Clear();

        void Show(int x, int y, ReadOnlyMemory<byte> message)
        {
            cancellation.ThrowIfCancellationRequested();
            host.Glow(x, y, message, cancellation);
        }
        void CubeFrame(ushort x, sbyte brightness)
        {
            cancellation.ThrowIfCancellationRequested();
            host.SetDelay(unchecked((int)glow.FrameCount)); host.CubeHue(x, 135, brightness);
            if (host.AnyButton()) glow.FrameCount = 0;
            host.Present(); host.Wait();
        }
    }

    private void Unlock(int index)
    {
        if ((uint)index < archive.ItemAvailability.Length) archive.ItemAvailability[index] = 1;
        else if ((uint)(index - archive.ItemAvailability.Length) < ChecksumScratch.Length)
            ChecksumScratch[index - archive.ItemAvailability.Length] = 1;
        else throw new InvalidDataException("Level-result item unlock exceeds the native save buffer.");
    }
    private ReadOnlyMemory<byte> Text(int index) => (uint)index < text.Count ? text[index] : throw new InvalidDataException("Level-result text is missing.");
    private byte[] Cash(int index) => Encoding.ASCII.GetBytes(game.Players[index].Cash.ToString(CultureInfo.InvariantCulture));
    private static ReadOnlySpan<byte> CString(ReadOnlySpan<byte> value)
    {
        var end = value.IndexOf((byte)0); return end < 0 ? value : value[..end];
    }
    private static byte[] Join(ReadOnlyMemory<byte> left, ReadOnlyMemory<byte> right)
    {
        var a = CString(left.Span); var b = CString(right.Span);
        var result = new byte[a.Length + 1 + b.Length];
        a.CopyTo(result); result[a.Length] = (byte)' '; b.CopyTo(result.AsSpan(a.Length + 1));
        return result;
    }
}

public sealed class LevelResultsHost(IndexedSurface primary, IndexedSurface clearSurface, PaletteSystem palette,
    TextGlow glow, IGameHudPresentation hud, SpriteTable options, InputState input, DelayTimer timer,
    Action<byte> playSample, Action<uint> playSong, Action present, Action keepAlive) : ILevelResultsHost
{
    public void WipeKey() => input.ClearKeyboard();
    public void SetWhite() => palette.SetSolid(new PaletteColor(255, 255, 255), 254, 254);
    public void PlaySample(byte sample) => playSample(sample);
    public void PlaySong(uint song) => playSong(song);
    public void Glow(int x, int y, ReadOnlyMemory<byte> text, CancellationToken cancellation) => glow.Draw(x, y, text, cancellation);
    public void Cube(ushort x, ushort y) => GameHud.DrawCube(hud, x, y, 9, 0);
    public void CubeHue(ushort x, ushort y, sbyte brightness) => options.Blit(primary, x, y, 25, SpriteEffect.Hue, 9, brightness);
    public void Present() => present();
    public void SetDelay(int frames) => timer.SetDelay(frames);
    public bool AnyButton() => input.AnyButton();
    public void KeepAlive() => keepAlive();
    public void Wait() => timer.Wait();
    public void WaitJoystickRelease(CancellationToken cancellation) => input.WaitNoInput(false, false, true, cancellation);
    public void FadeBlack(int steps) => palette.FadeBlack(steps, () => timer.SetDelay(1), present, timer.Wait);
    public void Clear() => clearSurface.Pixels.Span.Clear();
}
