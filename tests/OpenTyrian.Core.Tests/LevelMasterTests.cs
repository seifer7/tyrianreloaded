using OpenTyrian.Core;

namespace OpenTyrian.Core.Tests;

internal static class LevelMasterTests
{
    public static void Run()
    {
        if (LevelMaster.EventMaximum != 2500 || LevelMaster.WeaponCount != 780 ||
            LevelMaster.PortCount != 42 || LevelMaster.ArmorCount != 4 ||
            LevelMaster.PowerCount != 6 || LevelMaster.EngineCount != 6 ||
            LevelMaster.OptionCount != 30 || LevelMaster.ShipCount != 13 ||
            LevelMaster.ShieldCount != 10 || LevelMaster.SpecialCount != 46 ||
            LevelMaster.EnemyCount != 850)
            throw new InvalidOperationException("Level master capacities differ from the native constants.");

        ReadOnlySpan<byte> expected =
        [
            (byte)'2', (byte)'4', (byte)'7', (byte)'8', (byte)'A', (byte)'B', (byte)'C', (byte)'D',
            (byte)'E', (byte)'F', (byte)'G', (byte)'H', (byte)'I', (byte)'J', (byte)'K', (byte)'L',
            (byte)'M', (byte)'N', (byte)'O', (byte)'P', (byte)'Q', (byte)'R', (byte)'S', (byte)'T',
            (byte)'U', (byte)'5', (byte)'#', (byte)'V', (byte)'0', (byte)'@', (byte)'3', (byte)'^',
            (byte)'5', (byte)'9'
        ];
        if (!LevelMaster.ShapeFiles.SequenceEqual(expected))
            throw new InvalidOperationException("Level shape-file table differs from the native table.");
    }
}
