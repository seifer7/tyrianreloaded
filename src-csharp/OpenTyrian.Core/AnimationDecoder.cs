/*
 * OpenTyrian: A modern cross-platform port of Tyrian
 * Copyright (C) 2007-2009 The OpenTyrian Development Team
 * Distributed under the GNU General Public License, version 2 or later.
 * See COPYING for details.
 */

namespace OpenTyrian.Core;

public static class AnimationDecoder
{
    public const int FrameSize = 320 * 200;

    public static int Decode(Memory<byte> compressed, Memory<byte> frame)
    {
        if (frame.Length != FrameSize)
        {
            throw new ArgumentException("Animation frames must contain exactly 320 x 200 indexed pixels.", nameof(frame));
        }

        var input = new SizeBuffer(compressed, (uint)compressed.Length);
        var output = new SizeBuffer(frame, FrameSize);
        while (true)
        {
            var opcode = input.ReadByte();
            if (input.Error || output.Error)
            {
                return -1;
            }

            if (opcode == 0x80)
            {
                opcode = input.ReadWord();
                // Native playback tests stop before checking a failed word read.
                if (opcode == 0)
                {
                    return 0;
                }

                if ((opcode & 0x8000) == 0)
                {
                    output.Seek(opcode, SeekOrigin.Current);
                }
                else
                {
                    opcode &= ~0x8000u;
                    if ((opcode & 0x4000) != 0)
                    {
                        var count = opcode & ~0x4000u;
                        var value = input.ReadByte();
                        output.Fill((int)value, count);
                    }
                    else
                    {
                        output.CopyFrom(input, opcode);
                    }
                }
            }
            else if ((opcode & 0x80) != 0)
            {
                output.Seek(opcode & ~0x80u, SeekOrigin.Current);
            }
            else if (opcode == 0)
            {
                var count = input.ReadByte();
                var value = input.ReadByte();
                output.Fill((int)value, count);
            }
            else
            {
                output.CopyFrom(input, opcode);
            }
        }
    }
}
