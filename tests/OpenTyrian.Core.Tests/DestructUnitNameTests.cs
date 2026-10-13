using OpenTyrian.Core;

namespace OpenTyrian.Core.Tests;

internal static class DestructUnitNameTests
{
    public static void Run()
    {
        var names = new[]
        {
            "tank", "nuke", "dirt", "satellite", "magnet", "laser", "jumper", "heli"
        };
        for (var i = 0; i < names.Length; i++)
            Check(DestructUnitNames.GetUnitByName(names[i]) == (DestructUnit)i,
                $"Destruct unit name '{names[i]}' maps to the wrong enum value.");

        Check(DestructUnitNames.GetUnitByName("Tank") == DestructUnit.None &&
            DestructUnitNames.GetUnitByName("tank ") == DestructUnit.None &&
            DestructUnitNames.GetUnitByName("") == DestructUnit.None,
            "Unknown destruct unit names must remain unmatched.");
        try
        {
            DestructUnitNames.GetUnitByName(null!);
            throw new InvalidOperationException("Null destruct unit name was accepted.");
        }
        catch (ArgumentNullException)
        {
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
