using OpenTyrian.Core;

namespace OpenTyrian.Core.Tests;

internal static class AnimationDecoderTests
{
    public static void Run()
    {
        var frame = Enumerable.Repeat((byte)0xAA, AnimationDecoder.FrameSize).ToArray();
        byte[] compressed =
        [
            0, 3, 7,
            2, 4, 5,
            0x82,
            0x80, 3, 0,
            0x80, 2, 0x80, 8, 9,
            0x80, 4, 0xC0, 6,
            0x80, 0, 0
        ];
        Equal(0, AnimationDecoder.Decode(compressed, frame));
        byte[] prefix = [7, 7, 7, 4, 5, 0xAA, 0xAA, 0xAA, 0xAA, 0xAA, 8, 9, 6, 6, 6, 6];
        if (!frame.AsSpan(0, prefix.Length).SequenceEqual(prefix) || frame[prefix.Length..].Any(value => value != 0xAA))
        {
            throw new InvalidOperationException("Run/skip/copy decoding or preserved pixels differ.");
        }

        Equal(-1, AnimationDecoder.Decode(Array.Empty<byte>(), frame));
        Equal(-1, AnimationDecoder.Decode(new byte[] { 2, 1 }, frame));
        Equal(-1, AnimationDecoder.Decode(new byte[] { 0, 5 }, frame));
        Equal(-1, AnimationDecoder.Decode(new byte[] { 0x80, 0xFF, 0x7F, 0x80, 0xFF, 0x7F, 0x80, 0, 0 }, frame));
        Equal(0, AnimationDecoder.Decode(new byte[] { 0x80 }, frame));
        Equal(0, AnimationDecoder.Decode(new byte[] { 0x80, 0 }, frame));

        var fill = new List<byte>();
        for (var i = 0; i < 4; i++)
        {
            fill.AddRange(new byte[] { 0x80, 0x80, 0xFE, 0x55 });
        }

        fill.AddRange(new byte[] { 0x80, 0, 0 });
        Equal(0, AnimationDecoder.Decode(fill.ToArray(), frame));
        if (frame.Any(value => value != 0x55))
        {
            throw new InvalidOperationException("Full-frame fill differs.");
        }
    }

    private static void Equal(int expected, int actual)
    {
        if (expected != actual)
        {
            throw new InvalidOperationException($"Expected animation status {expected}, got {actual}.");
        }
    }
}
