namespace OpenTyrian.Core;

public enum DestructUnit
{
    None = -1,
    Tank,
    Nuke,
    Dirt,
    Satellite,
    Magnet,
    Laser,
    Jumper,
    Heli
}

public static class DestructUnitNames
{
    public const int UnitCount = 8;

    public static DestructUnit GetUnitByName(string unitName)
    {
        ArgumentNullException.ThrowIfNull(unitName);
        return unitName switch
        {
            "tank" => DestructUnit.Tank,
            "nuke" => DestructUnit.Nuke,
            "dirt" => DestructUnit.Dirt,
            "satellite" => DestructUnit.Satellite,
            "magnet" => DestructUnit.Magnet,
            "laser" => DestructUnit.Laser,
            "jumper" => DestructUnit.Jumper,
            "heli" => DestructUnit.Heli,
            _ => DestructUnit.None
        };
    }
}
