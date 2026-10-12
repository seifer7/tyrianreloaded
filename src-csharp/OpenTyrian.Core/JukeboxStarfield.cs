/*
 * OpenTyrian: A modern cross-platform port of Tyrian
 * Copyright (C) 2007-2009 The OpenTyrian Development Team
 * Distributed under the GNU General Public License, version 2 or later.
 * See COPYING for details.
 */

namespace OpenTyrian.Core;

public sealed class JukeboxStar
{
    public short X { get; set; }
    public short Y { get; set; }
    public short Z { get; set; }
    public short LastX { get; set; }
    public short LastY { get; set; }
}

public sealed class JukeboxStarfield
{
    private bool _initialized;
    public JukeboxStar[] Stars { get; } = Enumerable.Range(0, 1000).Select(_ => new JukeboxStar()).ToArray();
    public int TemporaryX { get; private set; }
    public int TemporaryY { get; private set; }
    public bool Running { get; private set; }
    public byte Setup { get; private set; }
    public ushort Steps { get; private set; }
    public ushort Phase2 { get; set; }
    public sbyte Phase2Increment { get; set; }
    public float Phase { get; set; }
    public float PhaseIncrement { get; set; }
    public float PhaseAcceleration { get; set; }
    public ushort ChangeTime { get; private set; }
    public bool AutomaticChanges { get; set; }
    public short Speed { get; set; }
    public sbyte SpeedChange { get; set; }
    public byte Color { get; set; }

    public void Initialize()
    {
        if (_initialized) return;
        _initialized = true;
        Reset();
        ChangeSetup(2);
        AutomaticChanges = true;
        for (var i = 0; i < Stars.Length; i++)
        {
            Stars[i].X = unchecked((short)((int)(MersenneTwister.NextUInt32() % 64000) - 32000));
            Stars[i].Y = unchecked((short)((int)(MersenneTwister.NextUInt32() % 40000) - 20000));
            Stars[i].Z = (short)(i + 1);
        }
    }

    public void Reset()
    {
        Phase2 = 1;
        Phase2Increment = 1;
        PhaseIncrement = 0.1f;
        PhaseAcceleration = 0.0001f;
        Phase = 0;
        Color = 32;
        Speed = 2;
        SpeedChange = 0;
    }

    public void ChangeSetup(byte setup)
    {
        if (setup > 14) throw new ArgumentOutOfRangeException(nameof(setup));
        Steps = 0;
        ChangeTime = (ushort)(MersenneTwister.NextUInt32() % 1000);
        Setup = setup > 0 ? setup : (byte)(MersenneTwister.NextUInt32() % 15);
        if (Setup == 1 || PhaseIncrement > 2.2f) PhaseIncrement = 0.1f;
    }

    public (int X, int Y) NewStar()
    {
        if (Setup == 0)
        {
            TemporaryX = (int)(MersenneTwister.NextUInt32() % 64000) - 32000;
            TemporaryY = (int)(MersenneTwister.NextUInt32() % 40000) - 20000;
            return (TemporaryX, TemporaryY);
        }
        Phase += PhaseIncrement;
        switch (Setup)
        {
            case 1:
                TemporaryX = (int)(MathF.Sin(Phase / 30) * 20000);
                TemporaryY = (int)(MersenneTwister.NextUInt32() % 40000) - 20000;
                break;
            case 2:
                TemporaryX = (int)(MathF.Cos(Phase) * 20000);
                TemporaryY = (int)(MathF.Sin(Phase) * 20000);
                break;
            case 3:
                TemporaryX = (int)(MathF.Cos(Phase * 15) * 100) * ((int)(Phase / 6) % 200);
                TemporaryY = (int)(MathF.Sin(Phase * 15) * 100) * ((int)(Phase / 6) % 200);
                break;
            case 4:
            case 5:
                TemporaryX = (int)(MathF.Sin(Phase / (Setup == 4 ? 60 : 2)) * 20000);
                TemporaryY = (int)(MathF.Cos(Phase) * (int)(MathF.Sin(Phase / 200) * 300) * 100);
                break;
            case 6:
            case 8:
                TemporaryX = (int)(MathF.Sin(Phase / (Setup == 6 ? 1 : 2)) * 40000);
                TemporaryY = (int)(MathF.Cos(Phase) * 20000);
                break;
            case 7:
                TemporaryX = (int)(MersenneTwister.NextUInt32() % 65535);
                TemporaryY = MersenneTwister.NextUInt32() % 2 == 0 ?
                    (int)(MathF.Cos(Phase / 80) * 10000) + 15000 : 50000 - (int)(MathF.Cos(Phase / 80) * 13000);
                break;
            case 9:
            case 10:
            case 11:
                AdvancePhase2();
                var divisor = Setup == 9 ? 10.0f : Setup == 10 ? 5.0f : 1000.0f;
                var phaseDivisor = Setup == 9 ? 500 : Setup == 10 ? 100 : 2;
                TemporaryX = (int)(MathF.Cos(MathF.Sin(Phase2 / divisor) + Phase / phaseDivisor) * 32000);
                TemporaryY = (int)(MathF.Sin(MathF.Cos(Phase2 / divisor) + Phase / phaseDivisor) * 30000);
                break;
            case 12:
                if (Phase != 0)
                {
                    AdvancePhase2();
                    TemporaryX = (int)(MathF.Cos(MathF.Sin(Phase2 / 2.0f) / (MathF.Sqrt(MathF.Abs(Phase)) / 10.0f + 1) + Phase2 / 100.0f) * 32000);
                    TemporaryY = (int)(MathF.Sin(MathF.Cos(Phase2 / 2.0f) / (MathF.Sqrt(MathF.Abs(Phase)) / 10.0f + 1) + Phase2 / 100.0f) * 30000);
                }
                break;
            case 13:
                if (Phase != 0)
                {
                    AdvancePhase2();
                    TemporaryX = (int)(MathF.Cos(MathF.Sin(Phase2 / 10.0f) / 2 + Phase / 20) * 32000);
                    TemporaryY = (int)(MathF.Sin(MathF.Sin(Phase2 / 11.0f) / 2 + Phase / 20) * 30000);
                }
                break;
            case 14:
                Phase2 = unchecked((ushort)(Phase2 + Phase2Increment));
                TemporaryX = (int)((MathF.Sin(Phase) + MathF.Cos(Phase2 / 1000.0f) * 3) * 12000);
                TemporaryY = (int)(MathF.Cos(Phase) * 10000) + Phase2;
                break;
        }
        return (TemporaryX, TemporaryY);
    }

    private void AdvancePhase2()
    {
        Phase2 = unchecked((ushort)(Phase2 + Phase2Increment));
        if (Phase2 is 0 or ushort.MaxValue) Phase2Increment = unchecked((sbyte)-Phase2Increment);
    }

    public void Update(IndexedSurface screen, InputState input)
    {
        if (screen.Width != 320 || screen.Height != 200 || screen.Pitch != 320)
            throw new ArgumentException("Jukebox stars require a contiguous 320x200 indexed screen.");
        Speed = unchecked((short)(Speed + SpeedChange));
        foreach (var star in Stars)
        {
            Draw(star.LastX + star.LastY * 320, 0, clear: true);
            if (star.Z == 0) throw new InvalidDataException("Jukebox star depth cannot be zero.");
            TemporaryX = star.X / star.Z + 160;
            TemporaryY = star.Y / star.Z + 100;
            var depth = unchecked((short)(star.Z - Speed));
            if (depth <= 0 || TemporaryY == 0 || TemporaryY > 198 || TemporaryX > 318 || TemporaryX < 1)
            {
                star.Z = 500;
                NewStar();
                star.X = unchecked((short)TemporaryX);
                star.Y = unchecked((short)TemporaryY);
            }
            else
            {
                star.LastX = unchecked((short)TemporaryX);
                star.LastY = unchecked((short)TemporaryY);
                star.Z = depth;
                Draw(TemporaryX + TemporaryY * 320, unchecked((byte)(Color + ((depth >> 4) & 31))), clear: false);
            }
        }
        if (input.NewKey) HandleKey(input);
        if (AutomaticChanges)
        {
            Steps = unchecked((ushort)(Steps + 1));
            if (Steps > ChangeTime) ChangeSetup(0);
        }
        if (MersenneTwister.NextUInt32() % 1000 == 1)
            PhaseAcceleration = (float)(MersenneTwister.NextFloatInclusive() * 0.01f - 0.005f);
        PhaseIncrement += PhaseAcceleration;

        void Draw(int offset, byte color, bool clear)
        {
            if (offset < 640 || offset >= 64000 - 640) return;
            var pixels = screen.Pixels.Span;
            pixels[offset] = color;
            color = clear ? (byte)0 : unchecked((byte)(color + 72));
            pixels[offset - 1] = pixels[offset + 1] = pixels[offset - 320] = pixels[offset + 320] = color;
            color = clear ? (byte)0 : unchecked((byte)(color + 72));
            pixels[offset - 2] = pixels[offset + 2] = pixels[offset - 640] = pixels[offset + 640] = color;
        }
    }

    private void HandleKey(InputState input)
    {
        var modifiers = input.LastModifiers;
        const int shift = 3, other = 0xFC0;
        char key = '\0';
        if ((modifiers & (shift | other)) == 0)
            key = input.LastScancode switch
            {
                6 => 'C',
                19 => 'P',
                22 => 'S',
                27 => 'X',
                41 => (char)27,
                >= 30 and <= 38 => (char)('1' + input.LastScancode - 30),
                39 => '0',
                >= 89 and <= 97 => (char)('1' + input.LastScancode - 89),
                98 => '0',
                45 or 86 => '-',
                87 => '+',
                47 => '[',
                48 => ']',
                53 => '`',
                _ => '\0'
            };
        else if ((modifiers & shift) != 0 && (modifiers & other) == 0)
            key = input.LastScancode switch
            {
                6 => 'C',
                19 => 'P',
                22 => 'S',
                27 => 'X',
                30 => '!',
                31 => '@',
                32 => '#',
                33 => '$',
                46 => '+',
                47 => '{',
                48 => '}',
                _ => '\0'
            };
        switch (key)
        {
            case '+': Speed = unchecked((short)(Speed + 1)); SpeedChange = 0; break;
            case '-': Speed = unchecked((short)(Speed - 1)); SpeedChange = 0; break;
            case >= '1' and <= '9': ChangeSetup((byte)(key - '0')); break;
            case '0': ChangeSetup(10); break;
            case '!': ChangeSetup(11); break;
            case '@': ChangeSetup(12); break;
            case '#': ChangeSetup(13); break;
            case '$': ChangeSetup(14); break;
            case 'C': Reset(); break;
            case 'S': PhaseAcceleration = MersenneTwister.NextFloatInclusive() * 0.01f - 0.005f; break;
            case 'X':
            case (char)27: Running = false; break;
            case '[': Color = unchecked((byte)(Color - 1)); break;
            case ']': Color = unchecked((byte)(Color + 1)); break;
            case '{': Color = unchecked((byte)(Color - 72)); break;
            case '}': Color = unchecked((byte)(Color + 72)); break;
            case '`': AutomaticChanges = !AutomaticChanges; break;
            case 'P': input.WaitNoInput(true, false, false); input.WaitInput(true, false, false); break;
        }
    }
}
