using OpenTyrian.Core;

namespace OpenTyrian.Core.Tests;

internal static class BinaryFileTests
{
    public static void Run()
    {
        using var stream = new MemoryStream();
        BinaryFile.WriteBoolean(stream, false);
        BinaryFile.WriteBoolean(stream, true);
        BinaryFile.WriteUInt8(stream, new byte[] { 0x12, 0xFE });
        BinaryFile.WriteInt8(stream, new sbyte[] { -128, -1 });
        BinaryFile.WriteUInt16(stream, 0x1234);
        BinaryFile.WriteInt16(stream, -2);
        BinaryFile.WriteUInt32(stream, 0x89ABCDEF);
        BinaryFile.WriteInt32(stream, int.MinValue);
        Equal(2, BinaryFile.WriteBytes(stream, new byte[] { 0x55, 0x66 }));
        byte[] expected =
        [
            0, 1, 0x12, 0xFE, 0x80, 0xFF, 0x34, 0x12, 0xFE, 0xFF,
            0xEF, 0xCD, 0xAB, 0x89, 0, 0, 0, 0x80, 0x55, 0x66
        ];
        BytesEqual(expected, stream.ToArray());
        Equal(20L, BinaryFile.Length(stream));
        Equal(20L, stream.Position);
        stream.Position = 0;
        Equal(false, BinaryFile.ReadBoolean(stream));
        Equal(true, BinaryFile.ReadBoolean(stream));
        var bytes = new byte[2];
        BinaryFile.ReadUInt8(stream, bytes);
        BytesEqual(new byte[] { 0x12, 0xFE }, bytes);
        var signedBytes = new sbyte[2];
        BinaryFile.ReadInt8(stream, signedBytes);
        Equal((sbyte)-128, signedBytes[0]);
        Equal((sbyte)-1, signedBytes[1]);
        var words = new ushort[1];
        BinaryFile.ReadUInt16(stream, words);
        Equal((ushort)0x1234, words[0]);
        var signedWords = new short[1];
        BinaryFile.ReadInt16(stream, signedWords);
        Equal((short)-2, signedWords[0]);
        var integers = new uint[1];
        BinaryFile.ReadUInt32(stream, integers);
        Equal(0x89ABCDEFu, integers[0]);
        var signedIntegers = new int[1];
        BinaryFile.ReadInt32(stream, signedIntegers);
        Equal(int.MinValue, signedIntegers[0]);
        var partial = new byte[5];
        Equal(2, BinaryFile.ReadBytes(stream, partial));
        Equal(0, BinaryFile.ReadBytes(stream, partial));
        try
        {
            BinaryFile.ReadExactly(stream, bytes);
            throw new InvalidOperationException("Expected truncated-file failure.");
        }
        catch (EndOfStreamException exception)
        {
            Equal("error: An unexpected problem occurred while reading from a file.", exception.Message);
        }

        using var nonCanonicalBool = new MemoryStream(new byte[] { 0x80 });
        Equal(true, BinaryFile.ReadBoolean(nonCanonicalBool));

        using var pcx = new MemoryStream();
        BinaryFile.WritePcxHeader(pcx, 320, 200);
        var header = new byte[128];
        header[0] = 10;
        header[1] = 5;
        header[2] = 1;
        header[3] = 8;
        header[8] = 0x3F;
        header[9] = 1;
        header[10] = 199;
        header[12] = 0x40;
        header[13] = 1;
        header[14] = 200;
        header[65] = 1;
        header[66] = 0x40;
        header[67] = 1;
        header[68] = 1;
        BytesEqual(header, pcx.ToArray());

        using var fragmented = new FragmentedStream(new byte[] { 1, 2, 3, 4 });
        BinaryFile.ReadUInt32(fragmented, integers);
        Equal(0x04030201u, integers[0]);
        using var nonWritable = new MemoryStream(new byte[1], writable: false);
        try
        {
            BinaryFile.WriteBoolean(nonWritable, true);
            throw new InvalidOperationException("Expected write failure.");
        }
        catch (NotSupportedException)
        {
        }
    }

    private static void BytesEqual(byte[] expected, byte[] actual)
    {
        if (!expected.AsSpan().SequenceEqual(actual))
        {
            throw new InvalidOperationException("Binary file bytes differ.");
        }
    }

    private static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException($"Expected {expected}, got {actual}.");
        }
    }

    private sealed class FragmentedStream(byte[] data) : MemoryStream(data)
    {
        public override int Read(Span<byte> buffer) => base.Read(buffer[..Math.Min(buffer.Length, 1)]);
    }
}
