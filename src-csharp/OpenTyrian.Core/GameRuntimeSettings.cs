/*
 * OpenTyrian: A modern cross-platform port of Tyrian
 * Copyright (C) 2007-2009 The OpenTyrian Development Team
 * Distributed under the GNU General Public License, version 2 or later.
 * See COPYING for details.
 */

namespace OpenTyrian.Core;

public sealed class GameRuntimeSettings
{
    public byte ProcessorType { get; set; }
    public byte GameSpeed { get; set; }
    public byte FastPlay { get; set; }
    public bool Wild { get; private set; }
    public bool SuperWild { get; private set; }
    public bool SmoothScroll { get; private set; }
    public bool ExplosionTransparent { get; private set; }
    public bool FiltrationAvailable { get; private set; }
    public bool Background2 { get; private set; }
    public bool DisplayScore { get; private set; }
    public bool PentiumMode { get; private set; }
    public uint FrameCountMax { get; private set; }

    public void InitializeProcessor()
    {
        Wild = SuperWild = FiltrationAvailable = false;
        SmoothScroll = ExplosionTransparent = Background2 = DisplayScore = true;
        switch (ProcessorType)
        {
            case 1: Background2 = DisplayScore = ExplosionTransparent = false; break;
            case 3 or 5: SmoothScroll = false; break;
            case 4: Wild = FiltrationAvailable = true; break;
            case 6: Wild = SuperWild = FiltrationAvailable = true; break;
        }
        FastPlay = GameSpeed switch
        {
            1 => 3,
            2 => 4,
            3 => 5,
            4 => 0,
            5 => 1,
            _ => FastPlay
        };
    }

    public void SetGameSpeed(DelayTimer timer)
    {
        ArgumentNullException.ThrowIfNull(timer);
        if (FastPlay > 5) throw new InvalidDataException("Native fast-play mode must be in the range 0..5.");
        PentiumMode = FastPlay == 5;
        SmoothScroll = FastPlay != 2;
        FrameCountMax = FastPlay switch { 3 => 4, 4 => 3, _ => 2 };
        ushort speed = FastPlay switch { 1 => 0x3000, 2 => 0x2000, 3 => 0x5300, _ => 0x4300 };
        timer.SetSpeed(speed);
        timer.SetDelay((int)FrameCountMax);
    }
}
