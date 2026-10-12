/*
 * OpenTyrian: A modern cross-platform port of Tyrian
 * Copyright (C) 2007-2009 The OpenTyrian Development Team
 * Distributed under the GNU General Public License, version 2 or later.
 * See COPYING for details.
 */

namespace OpenTyrian.Core;

public interface IDelayPlatform
{
    uint Ticks { get; }
    void Delay(uint milliseconds);
    void ServiceEvents(bool clearNew);
    void PollJoysticks();
    bool NewKey { get; set; }
    bool MouseDown { get; }
    bool JoystickDown { get; }
}

public sealed class DelayTimer(IDelayPlatform platform)
{
    private const float PitPeriod = (12.0f / 14318180.0f) * 1000.0f;
    private float _period = 0x4300 * PitPeriod;
    private uint _target;
    private uint _target2;

    public void SetSpeed(ushort speed) => _period = speed * PitPeriod;

    public void SetDelay(int delay) => _target = Target(delay);
    public void SetDelay2(int delay) => _target2 = Target(delay);

    private uint Target(int delay)
    {
        // C converts the complete floating-point expression, including ticks,
        // to uint32, rather than truncating the duration before addition.
        var target = (float)platform.Ticks + delay * _period;
        if (!float.IsFinite(target) || target < 0 || target >= 4294967296.0)
        {
            throw new ArgumentOutOfRangeException(nameof(delay), "Delay produces a target outside native defined uint32 conversion bounds.");
        }

        return (uint)target;
    }

    public uint GetDelayTicks() => Remaining(_target);
    public uint GetDelayTicks2() => Remaining(_target2);

    private uint Remaining(uint target)
    {
        var delay = unchecked((int)(target - platform.Ticks));
        return delay > 0 ? (uint)delay : 0;
    }

    public void Wait()
    {
        var delay = GetDelayTicks();
        if (delay > 0)
        {
            platform.Delay(delay);
        }
    }

    public void ServiceWait() => WaitWithService(false);
    public void WaitOrInput() => WaitWithService(true);

    private void WaitWithService(bool input)
    {
        while (true)
        {
            platform.ServiceEvents(false);
            if (input)
            {
                platform.PollJoysticks();
                if (platform.NewKey || platform.MouseDown || platform.JoystickDown)
                {
                    platform.NewKey = false;
                    return;
                }
            }

            var delay = GetDelayTicks();
            if (delay == 0)
            {
                return;
            }

            platform.Delay(Math.Min(delay, 10));
        }
    }

    public static void ChangeVolume(ref ushort music, int musicDelta, ref ushort sample, int sampleDelta,
        Action playClink, Action<byte, byte> setVolume)
    {
        var musicValue = unchecked(music + musicDelta);
        var sampleValue = unchecked(sample + sampleDelta);
        if (musicDelta != 0)
        {
            if (musicValue > 255 || musicValue < 0)
            {
                musicValue = Math.Clamp(musicValue, 0, 255);
                playClink();
            }
        }

        if (sampleDelta != 0)
        {
            if (sampleValue > 255 || sampleValue < 0)
            {
                sampleValue = Math.Clamp(sampleValue, 0, 255);
                playClink();
            }
        }

        music = unchecked((ushort)musicValue);
        sample = unchecked((ushort)sampleValue);
        setVolume(unchecked((byte)music), unchecked((byte)sample));
    }
}
