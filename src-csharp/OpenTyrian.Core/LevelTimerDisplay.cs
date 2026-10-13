/*
 * OpenTyrian: A modern cross-platform port of Tyrian
 * Copyright (C) 2007-2013 The OpenTyrian Development Team
 * Distributed under the GNU General Public License, version 2 or later.
 * See COPYING for details.
 */

using System.Globalization;
using System.Text;

namespace OpenTyrian.Core;

public interface ILevelTimerDisplayHost
{
    uint Ticks { get; }
    void DebugText(int x, int y, string text, byte hue, int brightness);
    void Notice(int brightness);
    void TimerLabel(int brightness);
    void TimerValue(string value);
}

public sealed class LevelTimerDisplay(GameKeyboard keyboard, PlayerState player, BackgroundRenderer backgrounds,
    GameRuntimeSettings settings, LevelEvents level, PlayerCombat combat, PlayerPickups pickups, SceneEffects effects,
    ILevelTimerDisplayHost host)
{
    public byte Flash { get; set; }
    public sbyte FlashChange { get; set; } = 1;
    public uint DebugTime { get; private set; }

    public void Draw(int enemiesOnScreen)
    {
        DebugTime = host.Ticks;
        if (keyboard.Debug)
        {
            host.DebugText(30, 70, "SM = " + string.Concat(backgrounds.Smoothies.Select(value => value ? '1' : '0')), 4, 0);
            host.DebugText(30, 80, "Memory left = -1", 4, 0);
            host.DebugText(30, 90, $"Enemies onscreen = {enemiesOnScreen.ToString(CultureInfo.InvariantCulture)}", 6, 0);
            keyboard.RecordDebugFrame(DebugTime, fps =>
            {
                var fpsText = float.IsPositiveInfinity(fps) ? "inf" : ((double)fps).ToString("F3", CultureInfo.InvariantCulture);
                var text = string.Create(CultureInfo.InvariantCulture,
                    $"X:{(level.MapX - 1) * 12 + player.X} Y:{level.Location.ToString(CultureInfo.InvariantCulture).PadRight(5)}  {fpsText} FPS  {player.XVelocity} {player.YVelocity} {player.X} {player.Y}");
                host.DebugText(45, 175, text, 15, 3);
            });
        }
        if (pickups.DisplayTime > 0)
        {
            pickups.DisplayTime--;
            host.Notice(Flash - 8);
            Flash = unchecked((byte)(Flash + FlashChange));
            if (Flash > 4 || Flash == 0) FlashChange = unchecked((sbyte)-FlashChange);
        }
        settings.AdvancePentiumFrame();
        if (combat.LevelTimer && level.TimerCountdown > 0)
        {
            level.TimerCountdown--;
            if (level.TimerCountdown == 0) level.Jump(level.TimerJump);
            if (level.TimerCountdown > 200)
            {
                if (level.TimerCountdown % 100 == 0) effects.SoundQueue[7] = 17;
                if (level.TimerCountdown % 10 == 0) effects.SoundQueue[6] = 24;
            }
            else if (level.TimerCountdown % 20 == 0) effects.SoundQueue[7] = 17;
            host.TimerLabel(level.TimerCountdown % 20 / 3);
            host.TimerValue(((double)(level.TimerCountdown / 100f)).ToString("F1", CultureInfo.InvariantCulture));
        }
    }
}

public sealed class LevelTimerDisplayHost(IndexedSurface surface, FontRenderer font, HelpTextData text,
    Func<uint> ticks) : ILevelTimerDisplayHost
{
    public uint Ticks => ticks();
    public void DebugText(int x, int y, string value, byte hue, int brightness) =>
        font.OutText(surface, x, y, Encoding.Latin1.GetBytes(value), hue, brightness);
    public void Notice(int brightness) => font.OutTextAndDarken(surface, 90, 10, text.MiscText[59], 15, unchecked((uint)brightness), Font.Large);
    public void TimerLabel(int brightness) => font.ShadeText(surface, 140, 6, text.MiscText[66], 7, brightness, TextShade.Full);
    public void TimerValue(string value) => font.DrawString(surface, 100, 2, Encoding.Latin1.GetBytes(value), Font.Normal);
}
