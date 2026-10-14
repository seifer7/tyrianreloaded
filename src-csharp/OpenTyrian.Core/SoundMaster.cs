/*
 * OpenTyrian: A modern cross-platform port of Tyrian
 * Copyright (C) 2007-2013 The OpenTyrian Development Team
 * Distributed under the GNU General Public License, version 2 or later.
 * See COPYING for details.
 */

namespace OpenTyrian.Core;

public enum SoundId : byte
{
    None = 0,
    Weapon1 = 1,
    Weapon2 = 2,
    EnemyHit = 3,
    Explosion4 = 4,
    Weapon5 = 5,
    Weapon6 = 6,
    Weapon7 = 7,
    Select = 8,
    Explosion8 = 8,
    Explosion9 = 9,
    Weapon10 = 10,
    Explosion11 = 11,
    Explosion12 = 12,
    Weapon13 = 13,
    Weapon14 = 14,
    Weapon15 = 15,
    Spring = 16,
    Warning = 17,
    Item = 18,
    HullHit = 19,
    MachineGun = 20,
    SoulOfZinglon = 21,
    Explosion22 = 22,
    Clink = 23,
    Click = 24,
    Weapon25 = 25,
    Weapon26 = 26,
    ShieldHit = 27,
    Cursor = 28,
    Powerup = 29,
    ClearedPlatform = 30,
    Boss = 31,
    Enemies = 32,
    GoodLuck = 33,
    LevelEnd = 34,
    Danger = 35,
    Spikes = 36,
    DataCube = 37,
    Accelerate = 38
}

public static class SoundMaster
{
    public const int SoundEffectCount = 29;
    public const int VoiceCount = 9;
    public const int SoundCount = SoundEffectCount + VoiceCount;

    public static IReadOnlyList<string> Titles { get; } = Array.AsReadOnly(new[]
    {
        "SCALEDN2", "F2", "TEMP10", "EXPLSM", "PASS3", "TEMP2", "BYPASS1", "EXP1RT", "EXPLLOW", "TEMP13",
        "EXPRETAP", "MT2BOOM", "TEMP3", "LAZB", "LAZGUN2", "SPRING", "WARNING", "ITEM", "HIT2", "MACHNGUN",
        "HYPERD2", "EXPLHUG", "CLINK1", "CLICK", "SCALEDN1", "TEMP11", "TEMP16", "SMALL1", "POWERUP",
        "VOICE1", "VOICE2", "VOICE3", "VOICE4", "VOICE5", "VOICE6", "VOICE7", "VOICE8", "VOICE9"
    });

    public static ReadOnlySpan<byte> WindowTextSamples =>
    [
        (byte)SoundId.Danger, (byte)SoundId.Boss, (byte)SoundId.Enemies,
        (byte)SoundId.ClearedPlatform, (byte)SoundId.Danger, (byte)SoundId.Spikes,
        (byte)SoundId.Accelerate, (byte)SoundId.Danger, (byte)SoundId.Enemies
    ];
}
