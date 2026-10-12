using OpenTyrian.Core;

namespace OpenTyrian.Core.Tests;

internal static class FontTests
{
    public static void Run()
    {
        var shapes = new MainShapeTables();
        using var encoded = new MemoryStream();
        BinaryFile.WriteUInt16(encoded, 127);
        for (var index = 0; index < 127; index++)
        {
            var exists = index != 1; // B is intentionally absent.
            BinaryFile.WriteBoolean(encoded, exists);
            if (exists)
            {
                BinaryFile.WriteUInt16(encoded, 2);
                BinaryFile.WriteUInt16(encoded, 1);
                BinaryFile.WriteUInt16(encoded, 2);
                BinaryFile.WriteExactly(encoded, new byte[] { 0xA8, 254 });
            }
        }

        foreach (var table in shapes.Tables.Take(3))
        {
            encoded.Position = 0;
            table.Load(encoded);
        }

        var renderer = new FontRenderer(shapes);
        using (var helpWarnings = new StringWriter())
        {
            var helpSurface = new IndexedSurface(40, 20, 42, helpWarnings);
            var help = new HelpTextRenderer(renderer) { Color = 3, Brightness = 3 };
            help.DrawBox(helpSurface, 1, 1, "AA AA"u8, 3);
            Check(helpSurface.Pixels.Span[43] == 0x3B && helpSurface.Pixels.Span[46] == 0x3B &&
                helpSurface.Pixels.Span[337] == 0x3B && helpSurface.Pixels.Span[340] == 0x3B,
                "Wrapped help lines did not render at their native vertical spacing.");
        }
        Check(renderer.TextWidth("A B~&A\0ignored"u8, Font.Small) == 13, "Font width or NUL handling differs.");
        Check(renderer.Center("AA"u8, Font.Small) == 157, "Font center differs.");
        Check(FontRenderer.SpriteIndex(128) == 86 && FontRenderer.SpriteIndex(168) == 126 &&
            FontRenderer.SpriteIndex(169) == -1 && FontRenderer.SpriteIndex((byte)'~') == -1, "Font byte mapping differs.");
        using var warnings = new StringWriter();
        var surface = new IndexedSurface(40, 10, 42, warnings);
        renderer.DrawHue(surface, 1, 1, "A~A~A"u8, Font.Small, FontAlignment.Left, 3, 0);
        Check(surface.Pixels.Span[43] == 0x38 && surface.Pixels.Span[46] == 0x3C && surface.Pixels.Span[49] == 0x38, "Font highlighting differs.");
        surface.Pixels.Span.Fill(0xAE);
        renderer.DrawBlend(surface, 10, 1, "A~A"u8, Font.Normal, FontAlignment.Right, 3, 0);
        Check(surface.Pixels.Span[46] == 0x3B && surface.Pixels.Span[49] == 0x3B, "Font blend/alignment differs.");
        renderer.DrawDark(surface, 10, 2, "AB A"u8, Font.Large, FontAlignment.Centered, false);
        Check(surface.Pixels.Span[88] == 0xA7 && surface.Pixels.Span[97] == 0xA7, "Font missing-glyph advance differs.");
        surface.Pixels.Span.Fill(0xAE);
        renderer.DrawShadow(surface, 10, 3, "A"u8, Font.Small, FontAlignment.Left, 5, 0, true, 1);
        Check(surface.Pixels.Span[136] == 0x58 && surface.Pixels.Span[179] == 0, "Font drop shadow differs.");
        renderer.DrawFullShadow(surface, 20, 5, "A"u8, Font.Small, FontAlignment.Left, 5, 0, true, 1);
        foreach (var position in new[] { 4 * 42 + 20, 5 * 42 + 19, 5 * 42 + 21, 6 * 42 + 20 })
        {
            Check(surface.Pixels.Span[position] == 0, "Font full-shadow placement differs.");
        }

        Check(surface.Pixels.Span[5 * 42 + 20] == 0x58, "Font full-shadow foreground differs.");
        surface.Pixels.Span.Fill(0xAE);
        renderer.DrawString(surface, 1, 1, "A~A"u8, Font.Large);
        Check(surface.Pixels.Span[43] == 0xF5 && surface.Pixels.Span[46] == 0xF7 &&
            surface.Pixels.Span[129] == 0xA7, "Legacy string highlighting/shadow differs.");
        renderer.OutText(surface, 1, 2, "A~A"u8, 3, 1);
        Check(surface.Pixels.Span[85] == 0x39 && surface.Pixels.Span[88] == 0x3D, "Legacy tiny text highlight differs.");
        renderer.OutText(surface, 1, 3, "A"u8, 3, -1);
        Check(surface.Pixels.Span[127] == 0, "Negative-brightness legacy text is not black.");
        renderer.OutTextAdjust(surface, 1, 4, "A"u8, 3, 10, Font.Normal, true);
        Check(surface.Pixels.Span[169] == 0x3F, "Adjusted legacy text did not saturate.");
        renderer.OutTextModify(surface, 10, 4, "A"u8, 3, 0, Font.Normal);
        Check(surface.Pixels.Span[178] == 0x3B, "Modified legacy text differs.");
        renderer.OutTextAndDarken(surface, 20, 4, "A"u8, 2, 10, Font.Normal);
        Check(surface.Pixels.Span[188] == 0x32 && surface.Pixels.Span[231] == 0xA7, "Legacy unchecked brightness/shadow differs.");
        renderer.ShadeText(surface, 30, 4, "A"u8, 3, 0, TextShade.Full);
        Check(surface.Pixels.Span[198] == 0x38 && surface.Pixels.Span[197] == 0 &&
            surface.Pixels.Span[199] == 0, "Legacy full shade differs.");
        renderer.ShadeText(surface, 30, 7, "A"u8, 3, 0, TextShade.Partial);
        Check(surface.Pixels.Span[324] == 0x38 && surface.Pixels.Span[367] == 0, "Legacy partial shade differs.");
        renderer.ShadeText(surface, 10, 7, "A"u8, 3, 0, TextShade.Blend);
        Check(surface.Pixels.Span[304] == 0x3B, "Legacy blend shade differs.");
        renderer.ShadeText(surface, 15, 7, "A"u8, 3, 0, TextShade.Darken);
        Check(surface.Pixels.Span[352] == 0x38 && surface.Pixels.Span[395] == 0xA7, "Legacy darken shade differs.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
