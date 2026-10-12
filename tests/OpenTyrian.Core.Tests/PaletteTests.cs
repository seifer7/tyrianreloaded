using OpenTyrian.Core;

namespace OpenTyrian.Core.Tests;

internal static class PaletteTests
{
    public static void Run()
    {
        var encoded = new byte[23 * 256 * 3];
        for (var index = 0; index < encoded.Length; index += 3)
        {
            encoded[index] = 63;
            encoded[index + 1] = 32;
            encoded[index + 2] = 1;
        }

        using var stream = new MemoryStream(encoded);
        var palettes = PaletteSystem.Load(stream);
        Check(palettes.Length == 23 && palettes[0][0] == new PaletteColor(255, 130, 4), "VGA palette expansion differs.");
        var system = new PaletteSystem(color => (uint)((color.Red << 16) | (color.Green << 8) | color.Blue));
        system.SetPalette(palettes[0], 0, 255);
        Check(system.Rgb.Span[0] == 0xFF8204, "Mapped palette differs.");
        Check(PaletteSystem.RgbToYuv(0, 0, 0) == 0x008080, "Black YUV differs.");
        Check(PaletteSystem.RgbToYuv(255, 255, 255) == 0xBF8080, "White YUV differs.");
        Check(PaletteSystem.RgbToYuv(255, 0, 0) == 0x3FBF60, "Signed-shift red YUV differs.");
        var differences = system.InitializeSolidFade(new PaletteColor(0, 255, 10), 4, 7);
        var original = system.Active.Span[0];
        for (var remaining = 7; remaining > 0; remaining--)
        {
            system.StepFade(differences, remaining, 4, 7);
        }

        Check(system.Active.Span[4] == new PaletteColor(0, 255, 10), "Fade did not reach exact target.");
        Check(system.Active.Span[0] == original, "Fade changed unrelated colors.");
        var actions = new List<string>();
        system.FadeBlack(2, () => actions.Add("delay"), () => actions.Add("present"), () => actions.Add("wait"));
        Check(string.Join(',', actions) == "delay,present,wait,delay,present,wait", "Fade presentation ordering differs.");
        Check(system.Active.Span.ToArray().All(color => color == new PaletteColor(0, 0, 0)), "Black fade differs.");
        system.FadeWhite(3, () => { }, () => { }, () => { });
        Check(system.Active.Span.ToArray().All(color => color == new PaletteColor(255, 255, 255)), "White fade differs.");
        system.SetSolid(new PaletteColor(3, 5, 7), 1, 2);
        Check(system.Active.Span[1] == new PaletteColor(3, 5, 7), "Solid palette differs.");
        system.FadePalette(palettes[1], 3, 0, 255, () => { }, () => { }, () => { });
        Check(system.Active.Span.SequenceEqual(palettes[1]), "Palette fade differs.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
