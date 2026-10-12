/*
 * Derived from the Mersenne Twister implementation by Makoto Matsumoto,
 * Takuji Nishimura, and Eric Landry (Copyright 1997-2004).
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *
 * 1. Redistributions of source code must retain the above copyright notice,
 *    this list of conditions, and the following disclaimer.
 * 2. Redistributions in binary form must reproduce the above copyright notice,
 *    this list of conditions, and the following disclaimer in the
 *    documentation and/or other materials provided with the distribution.
 * 3. The names of its contributors may not be used to endorse or promote
 *    products derived from this software without specific prior written
 *    permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
 * AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
 * IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
 * ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE
 * LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR
 * CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF
 * SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS
 * INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN
 * CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
 * ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
 * POSSIBILITY OF SUCH DAMAGE.
 */

namespace OpenTyrian.Core;

public static class MersenneTwister
{
    private const int StateLength = 624;
    private const int MiddleOffset = 397;
    private const uint MatrixA = 0x9908b0df;
    private const uint UpperMask = 0x80000000;
    private const uint LowerMask = 0x7fffffff;
    private static readonly uint[] State = new uint[StateLength];
    private static int _index;
    private static bool _initialized;

    public static void Seed(ulong seed)
    {
        State[0] = unchecked((uint)seed);
        for (var i = 1; i < StateLength; i++)
        {
            State[i] = unchecked(1812433253u * (State[i - 1] ^ (State[i - 1] >> 30)) + (uint)i);
        }

        _index = 0;
        _initialized = true;
    }

    public static uint NextUInt32()
    {
        if (!_initialized)
        {
            Seed(5489);
        }

        var nextIndex = (_index + 1) % StateLength;
        var middleIndex = (_index + MiddleOffset) % StateLength;
        var next = State[nextIndex];
        var mixed = (State[_index] & UpperMask) | (next & LowerMask);
        var value = State[middleIndex] ^ (mixed >> 1) ^ ((next & 1) == 0 ? 0u : MatrixA);
        State[_index] = value;
        _index = nextIndex;

        value ^= value >> 11;
        value ^= (value << 7) & 0x9d2c5680;
        value ^= (value << 15) & 0xefc60000;
        value ^= value >> 18;
        return value;
    }

    public static float NextFloatInclusive()
    {
        return (float)NextUInt32() / uint.MaxValue;
    }

    public static float NextFloatExclusive()
    {
        return (float)NextUInt32() / ((float)uint.MaxValue + 1.0f);
    }
}
