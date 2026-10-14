using System.Security.Cryptography;
using OpenTyrian.Core;

namespace OpenTyrian.Core.Tests;

internal static class JukeboxPaletteTests
{
    public static void Run()
    {
        var colors = JukeboxPalette.Colors.Span;
        Check(colors.Length == PaletteSystem.ColorCount, "The jukebox palette must contain 256 colors.");
        Check(colors[0] == new PaletteColor(0, 0, 0) &&
            colors[1] == new PaletteColor(0, 0, 168) &&
            colors[15] == new PaletteColor(252, 252, 252) &&
            colors[244] == new PaletteColor(44, 64, 64) &&
            colors[255] == new PaletteColor(0, 0, 0),
            "Known native jukebox palette entries differ.");

        var rgb = new byte[colors.Length * 3];
        for (var i = 0; i < colors.Length; i++)
        {
            rgb[i * 3] = colors[i].Red;
            rgb[i * 3 + 1] = colors[i].Green;
            rgb[i * 3 + 2] = colors[i].Blue;
        }
        Check(Convert.ToHexString(SHA256.HashData(rgb)) ==
            "0EA5935AA95332CC00D4E55DCA21CFDF416524838D6B383BF42B52FB742EE9D7",
            "The native VGA jukebox palette table differs.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
