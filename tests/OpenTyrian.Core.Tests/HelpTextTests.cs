using OpenTyrian.Core;

namespace OpenTyrian.Core.Tests;

internal static class HelpTextTests
{
    public static void Run()
    {
        var data = new HelpTextData();
        byte[][][] groups =
        [
            data.Help, data.PlanetNames, data.MiscText, data.MiscTextB, data.MenuEntries[6], data.MenuText,
            data.Outputs, data.TopicNames, data.MainMenuHelp, data.MenuEntries[1][..7], data.MenuEntries[2][..9],
            data.MenuEntries[3][..8], data.InGameText, data.DetailLevels, data.GameSpeeds, data.EpisodeNames,
            data.DifficultyNames, data.GameplayNames, data.MenuEntries[10][..6], data.InputDevices,
            data.NetworkText, data.MenuEntries[11][..4], data.HighScoreDifficultyNames, data.MenuEntries[12][..6],
            data.MenuEntries[13][..7], data.JoystickButtonNames, data.SuperShips, data.SpecialNames,
            data.DestructHelp, data.WeaponNames, data.DestructModeNames,
            data.ShipInfo.SelectMany(pair => pair).ToArray(), data.MenuEntries[14][..5]
        ];
        using var stream = new MemoryStream();
        BinaryFile.WriteInt32(stream, 123456);
        byte[] key = [204, 129, 63, 255, 71, 19, 25, 62, 1, 99];
        var counter = 0;
        foreach (var group in groups)
        {
            stream.Write(new byte[] { 2, 0, 0 });
            foreach (var entry in group)
            {
                var length = entry.Length - 1;
                var encoded = new byte[length + 1];
                encoded[0] = (byte)length;
                for (var index = 0; index < length; index++)
                {
                    var plaintext = (byte)((counter + index) % 255 + 1);
                    encoded[index + 1] = (byte)(plaintext ^ key[index % 10] ^ (index == 0 ? 0 : encoded[index]));
                }

                stream.Write(encoded);
                counter++;
            }

            if (!ReferenceEquals(group, groups[^1]))
            {
                stream.Write(new byte[] { 1, 0 });
            }
        }

        var expectedPosition = stream.Position;
        stream.WriteByte(42);
        stream.Position = 0;
        data.Load(stream);
        Check(data.EpisodeOneDataOffset == 123456 && stream.Position == expectedPosition, "Help text offset or delimiters differ.");
        counter = 0;
        foreach (var group in groups)
        {
            foreach (var entry in group)
            {
                Check(entry[^1] == 0, "Help string is not NUL terminated.");
                for (var index = 0; index < entry.Length - 1; index++)
                {
                    Check(entry[index] == (byte)((counter + index) % 255 + 1), "Help text group order or decryption differs.");
                }

                counter++;
            }
        }

        Check(data.KeyNames.All(entry => entry.All(value => value == 0)) &&
            data.MenuEntries[0].All(entry => entry.All(value => value == 0)), "Help loader populated native-unused fields.");
        Check(HelpTextData.MenuHelp.Length == 154 && HelpTextData.MenuHelp[1] == 34 &&
            HelpTextData.MenuHelp[143] == 4, "Menu help mapping differs.");
        using var truncated = new MemoryStream(stream.ToArray()[..20]);
        try
        {
            data.Load(truncated);
            throw new InvalidOperationException("Truncated help archive accepted.");
        }
        catch (EndOfStreamException)
        {
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
