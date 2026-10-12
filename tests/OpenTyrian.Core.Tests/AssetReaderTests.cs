using System.Buffers.Binary;
using OpenTyrian.Core;

namespace OpenTyrian.Core.Tests;

internal static class AssetReaderTests
{
    public static void Run()
    {
        byte[] key = [204, 129, 63, 255, 71, 19, 25, 62, 1, 99];
        foreach (var length in new[] { 0, 1, 10, 11, 255 })
        {
            var plain = Enumerable.Range(0, length).Select(index => (byte)(index + 1)).ToArray();
            var cipher = new byte[length];
            for (var index = 0; index < length; index++)
            {
                cipher[index] = (byte)(plain[index] ^ key[index % key.Length] ^ (index == 0 ? 0 : cipher[index - 1]));
            }

            using var stream = new MemoryStream(new byte[] { (byte)length }.Concat(cipher).ToArray());
            var destination = new byte[length + 1];
            EncryptedText.ReadPascalString(stream, destination);
            Check(destination.AsSpan(0, length).SequenceEqual(plain), "Decrypted text differs.");
            Check(destination[^1] == 0, "Missing terminator.");
            stream.Position = 0;
            EncryptedText.SkipPascalString(stream);
            Check(stream.Position == stream.Length, "Skip did not consume the string.");
            stream.Position = 0;
            EncryptedText.ReadPascalString(stream, Array.Empty<byte>());
            Check(stream.Position == stream.Length, "Empty destination did not consume the string.");
            stream.Position = 0;
            EncryptedText.ReadPascalString(stream, new byte[1]);
            Check(stream.Position == stream.Length, "Truncation did not consume the string.");
        }

        byte[] compressed = [0, 2, 7, 0x80, 0, 0];
        var file = new byte[0xB00 + 8 + 2 + 4 + compressed.Length];
        "LPF "u8.CopyTo(file);
        PutWord(file, 6, 1);
        BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(8), 2);
        file[0x100] = 3;
        file[0x101] = 2;
        file[0x102] = 1;
        PutWord(file, 0x502, 1);
        PutWord(file, 0x504, (ushort)(compressed.Length + 4));
        PutWord(file, 0xB02, 1);
        PutWord(file, 0xB04, (ushort)(compressed.Length + 4));
        PutWord(file, 0xB08, (ushort)(compressed.Length + 4));
        compressed.CopyTo(file, 0xB0E);
        using var source = new MemoryStream(file);
        using (var animation = new AnimationFile(source, leaveOpen: true))
        {
            Check(animation.RecordCount == 2, "Record count differs.");
            Check(animation.Palette[0] == new PaletteColor(1, 2, 3), "BGR palette differs.");
            Check(animation.FindPage(0) == 0 && animation.FindPage(1) == -1, "Page lookup differs.");
            animation.LoadPage(0);
            animation.LoadPage(0);
            var frame = new byte[AnimationDecoder.FrameSize];
            Check(animation.RenderFrame(0, frame) == 0 && frame[0] == 7 && frame[1] == 7 && frame[2] == 0, "Frame differs.");
        }

        Check(source.CanRead, "leaveOpen did not retain the stream.");
        var invalid = new MemoryStream(new byte[10]);
        ExpectInvalid(() => new AnimationFile(invalid));
        Check(!invalid.CanRead, "Invalid stream was not disposed.");
        file[0] = 0;
        ExpectInvalid(() => new AnimationFile(new MemoryStream(file)));
        file[0] = (byte)'L';
        PutWord(file, 0xB08, 3);
        using var badPage = new AnimationFile(new MemoryStream(file));
        ExpectInvalid(() => badPage.LoadPage(0));
    }

    private static void PutWord(byte[] file, int offset, ushort value) => BinaryPrimitives.WriteUInt16LittleEndian(file.AsSpan(offset), value);

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static void ExpectInvalid(Action action)
    {
        try
        {
            action();
        }
        catch (InvalidDataException)
        {
            return;
        }

        throw new InvalidOperationException("Expected invalid asset failure.");
    }
}
