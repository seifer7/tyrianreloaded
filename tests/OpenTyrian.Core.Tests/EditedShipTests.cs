using System.Buffers.Binary;
using System.Diagnostics;
using System.Text;
using OpenTyrian.Core;

namespace OpenTyrian.Core.Tests;

internal static class EditedShipTests
{
    public static void Run(string? executable = null)
    {
        for (var variant = 0; variant < 256; variant++)
        {
            var plain = Enumerable.Range(0, EditedShips.PayloadSize).Select(i => (byte)(variant + i * 37)).ToArray();
            var encrypted = Encrypt(plain);
            if (!EditedShips.Decrypt(encrypted).AsSpan().SequenceEqual(plain))
                throw new InvalidOperationException("Edited-ship decryption differs.");
            for (var i = 0; i < 4; i++)
            {
                encrypted[150 + i] ^= 1; Throws<InvalidDataException>(() => EditedShips.Decrypt(encrypted)); encrypted[150 + i] ^= 1;
            }
        }
        var root = Path.Combine(Path.GetTempPath(), $"opentyrian-edited-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, "newsh$.shp");
        try
        {
            var empty = new EditedShips();
            if (empty.Load(root) || empty.Available) throw new InvalidOperationException("Missing optional edited ships became available.");
            if (executable is not null) Compare(executable, path, "archive:0,0\n", 0);
            var loaded = new EditedShips();
            int[] sizes = [0, 3, 663, 65535];
            for (var mode = 0; mode < 24; mode++)
            {
                var size = sizes[mode % 4];
                var records = Enumerable.Range(0, 150).Select(i => (byte)(mode * 17 + i * 11)).ToArray();
                var shapes = Enumerable.Range(0, size).Select(i => (byte)(mode + i * 19)).ToArray();
                if (size == 663)
                {
                    for (var i = 0; i < 330; i++) BinaryPrimitives.WriteUInt16LittleEndian(shapes.AsSpan(i * 2), 660);
                    shapes[660] = 0x10; shapes[661] = 0xAE; shapes[662] = 0x0F;
                }
                File.WriteAllBytes(path, [.. shapes, .. Encrypt(records), .. new byte[(mode & 4) != 0 ? 65536 : 0]]);
                if (size == 0)
                {
                    Throws<InvalidDataException>(() => loaded.Load(root));
                    if (executable is not null) Compare(executable, path, "", 1);
                    continue;
                }
                if (!loaded.Load(root) || !loaded.Available || loaded.ShapeSize != size || loaded.Shapes.Size != size ||
                    !loaded.Records.Span.SequenceEqual(records))
                    throw new InvalidOperationException("Edited-ship archive loading/ushort size differs.");
                if (size == 663)
                {
                    var surface = new IndexedSurface(320, 200, 327, TextWriter.Null);
                    loaded.Shapes.Blit(surface, 10, 20, 5);
                    if (surface.Pixels.Span[20 * surface.Pitch + 10] != 0xAE)
                        throw new InvalidOperationException("Edited ship graphics were not integrated into the sprite decoder.");
                }
                if (executable is not null)
                {
                    uint hash = 2166136261;
                    foreach (var b in shapes) hash = unchecked((hash ^ b) * 16777619);
                    Compare(executable, path, $"archive:1,{size}\nshapes:{hash}\nrecords,{string.Join(',', records)}\n", 0);
                }
            }
            var before = loaded.Records.ToArray(); var beforeShapes = loaded.Shapes;
            for (var check = 0; check < 4; check++)
            {
                var encrypted = Encrypt(before); encrypted[150 + check] ^= 1; File.WriteAllBytes(path, [0, .. encrypted]);
                Throws<InvalidDataException>(() => loaded.Load(root));
                if (!loaded.Records.Span.SequenceEqual(before) || !ReferenceEquals(loaded.Shapes, beforeShapes))
                    throw new InvalidOperationException("Failed edited-ship load published partial state.");
                if (executable is not null) Compare(executable, path, "", 255);
            }
            File.WriteAllBytes(path, new byte[153]); Throws<InvalidDataException>(() => loaded.Load(root));
            Throws<InvalidDataException>(() => EditedShips.Decrypt(new byte[153]));
            File.Delete(path);
            if (loaded.Load(root) || !loaded.Available || !loaded.Records.Span.SequenceEqual(before))
                throw new InvalidOperationException("Missing optional reload discarded prior edited ships.");
        }
        finally { File.Delete(path); Directory.Delete(root); }
    }

    internal static byte[] Encrypt(ReadOnlySpan<byte> plain)
    {
        byte[] key = [58, 23, 16, 192, 254, 82, 113, 147, 62, 99];
        var encrypted = new byte[154];
        byte add = 0, subtract = 0, multiply = 1, xor = 0;
        for (var i = 0; i < plain.Length; i++)
        {
            encrypted[i] = (byte)(plain[i] ^ key[(i + 1) % 10] ^ (i == 0 ? 0 : encrypted[i - 1]));
            add = unchecked((byte)(add + plain[i])); subtract = unchecked((byte)(subtract - plain[i]));
            multiply = unchecked((byte)(multiply * plain[i] + 1)); xor ^= plain[i];
        }
        encrypted[150] = add; encrypted[151] = subtract; encrypted[152] = multiply; encrypted[153] = xor;
        return encrypted;
    }

    private static void Compare(string executable, string path, string expected, int exit)
    {
        var start = new ProcessStartInfo(executable) { RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add(path);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Cannot start edited-ship reference.");
        var output = process.StandardOutput.ReadToEndAsync(); var errors = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(30000)) { process.Kill(entireProcessTree: true); throw new TimeoutException("Edited-ship reference timed out."); }
        var actual = output.GetAwaiter().GetResult().Replace("\r\n", "\n", StringComparison.Ordinal);
        if (process.ExitCode != exit || actual != expected)
            throw new InvalidOperationException($"Edited-ship native archive differs: {process.ExitCode}/{exit}, {actual}; {errors.GetAwaiter().GetResult()}");
    }

    private static void Throws<T>(Action action) where T : Exception
    {
        try { action(); } catch (T) { return; }
        throw new InvalidOperationException($"Expected {typeof(T).Name}.");
    }
}
