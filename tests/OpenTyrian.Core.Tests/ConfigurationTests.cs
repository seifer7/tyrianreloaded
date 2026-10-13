using OpenTyrian.Core;

namespace OpenTyrian.Core.Tests;

internal static class ConfigurationTests
{
    public static readonly string[] Fixtures =
    [
        "",
        "section video\nitem scaler scale2x\nlist names first\nlist names second\n",
        "item orphan ignored\nsection '' ''\nitem '' ''\nsection video\nitem x 1\nitem x 2\nsection video\n",
        "section 'quoted type' \"name\"\r\nitem 'escape' 'a\\tb\\nc\\rd\\\\e\\'f\\x80'\r\n",
        "invalid ignored\nsection#bad\nsection okay\nitem missing\nitem x 'bad\\q'\nitem x 'fine' ignored\nlist x next\n",
        "section cfg\nlist x a\nlist y b\nlist x c\nitem y final\n",
        "section cfg\nitem x 'a\\x00b'\nitem unicode '\\xff'\n",
        "section cfg\nitem x \"quoted\\\"value\"\nitem y unquoted#bad\nitem z 'unclosed\n",
        "section cfg\nitem x 010\nitem y 0x10\nitem z -1\n",
        "section a name\rsection b\r\nitem x val\nsection a name\n"
    ];

    public static void Run()
    {
        foreach (var fixture in Fixtures)
        {
            using var input = new StringReader(fixture);
            var configuration = Configuration.Parse(input);
            using var output = new StringWriter();
            configuration.Write(output);
            using var reparsedInput = new StringReader(output.ToString());
            var reparsed = Configuration.Parse(reparsedInput);
            using var repeated = new StringWriter();
            reparsed.Write(repeated);
            Equal(output.ToString(), repeated.ToString());
        }

        var config = new Configuration();
        var section = config.AddSection("video");
        Equal(section, config.FindOrAddSection("video"));
        config.AddSection("video");
        Equal(2, config.FindSections("video").Count());
        Equal<ConfigSection?>(null, config.FindSection("Video"));
        var option = section.SetOption("key", "first");
        Equal(false, option.IsList);
        Equal(1, option.Count);
        option.AddValue("second");
        Equal(true, option.IsList);
        Equal<string?>(null, option.Value);
        option.RemoveValue(0);
        Equal(true, option.IsList);
        Equal<string?>(null, option.Value);
        Equal("second", option.Values[0]);
        Equal<ConfigOption?>(null, option.RemoveValue(9));
        option.RemoveValue(0);
        Equal(0, option.Count);
        option.SetValue("");
        Equal("", option.Value);
        Equal("existing", section.GetOrSetString("new", "existing"));
        Equal("existing", section.GetOrSetString("new", "other"));
        foreach (var style in Enum.GetValues<ConfigBoolStyle>())
        {
            section.SetBoolean("bool", true, style);
            Equal(true, section.TryGetBoolean("bool", out var result));
            Equal(true, result);
        }

        section.SetOption("bool", "TRUE");
        Equal(false, section.TryGetBoolean("bool", out _));
        Equal(false, section.GetOrSetBoolean("bool", false, ConfigBoolStyle.NoYes));
        Equal("no", section.GetOption("bool")?.Value);
        foreach (var (text, expected) in new (string, int)[] { ("010", 8), ("0x10", 16), ("-0x10", -16), (" +20", 20), ("-2147483648", int.MinValue) })
        {
            section.SetOption("int", text);
            Equal(true, section.TryGetInt32("int", out var result));
            Equal(expected, result);
        }

        foreach (var text in new[] { "08", "10 ", "", "abc", "+", "0x" })
        {
            section.SetOption("int", text);
            Equal(false, section.TryGetInt32("int", out _));
        }

        section.SetOption("uint", "-1");
        Equal(true, section.TryGetUInt32("uint", out var unsigned));
        Equal(uint.MaxValue, unsigned);
        section.SetOption("uint", "010");
        Equal(true, section.TryGetUInt32("uint", out unsigned));
        Equal(10u, unsigned);
        section.SetInt32("int", int.MinValue);
        Equal(int.MinValue, section.GetOrSetInt32("int", 0));
        section.SetUInt32("uint", uint.MaxValue);
        Equal(uint.MaxValue, section.GetOrSetUInt32("uint", 0));
        section.SetOption("nul", "prefix\0suffix");
        Equal("prefix", section.GetOption("nul")?.Value);
        config.Clear();
        Equal(0, config.Sections.Count);

        using var errors = new StringWriter();
        try
        {
            Configuration.ThrowOutOfMemory(errors);
            throw new InvalidOperationException("Configuration OOM handler returned.");
        }
        catch (OutOfMemoryException exception)
        {
            Equal("out of memory", exception.Message);
        }
        Equal("out of memory" + Environment.NewLine, errors.ToString());
    }

    private static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException($"Expected {expected}, got {actual}.");
        }
    }
}
