namespace Marauder.Mini.Models;

public enum UnitType
{
    Unknown,
    Item,
    Player,
    Object,
}

public class Unit
{
    public uint Id;
}

public class ItemUnit : Unit
{
    public UnitType Type = UnitType.Item;
}

public class ObjectUnit : Unit
{
    public UnitType Type = UnitType.Object;
}

public class UnitTable
{
    public IEnumerable<ItemUnit> ReadItems()
    {
        return [];
    }

    public IEnumerable<PlayerUnit> ReadPlayers()
    {
        return [];
    }

    public IEnumerable<ObjectUnit> ReadObjects()
    {
        return [];
    }
}