using OpenTyrian.Core;

namespace OpenTyrian.Core.Tests;

internal static class LegacyConfigurationTypesTests
{
    public static void Run()
    {
        Check(Enum.GetValues<GameDifficulty>().Select(value => (byte)value)
            .SequenceEqual(Enumerable.Range(0, 11).Select(value => (byte)value)),
            "Managed game difficulty values do not match config.h.");
        Check(Enum.GetValues<KeySetting>().Select(value => (byte)value)
            .SequenceEqual(Enumerable.Range(0, 8).Select(value => (byte)value)),
            "Managed keyboard-setting values do not match config.h.");
        Check(Enum.GetValues<PlayerShotSlot>().Select(value => (byte)value)
            .SequenceEqual(Enumerable.Range(0, 11).Select(value => (byte)value)),
            "Managed player-shot slots do not match config.h.");

        var configuration = new GameConfiguration(new TestKeyboardNames());
        configuration.SetKey(KeySetting.Fire, 44);
        configuration.SetKey(KeySetting.RightSidekick, 226);
        Check(configuration.GetKey(KeySetting.Fire) == 44 &&
            configuration.GetKey(KeySetting.RightSidekick) == 226,
            "Typed keyboard settings do not address the native slots.");

        try
        {
            configuration.GetKey((KeySetting)byte.MaxValue);
            throw new InvalidOperationException("Invalid keyboard-setting ordinal was accepted.");
        }
        catch (ArgumentOutOfRangeException) { }

        try
        {
            configuration.SetKey(KeySetting.Up, 512);
            throw new InvalidOperationException("Out-of-range SDL scancode was accepted.");
        }
        catch (ArgumentOutOfRangeException) { }
    }

    private sealed class TestKeyboardNames : IKeyboardNames
    {
        public int FromName(string name) => 0;
        public string Name(int scancode) => "";
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
