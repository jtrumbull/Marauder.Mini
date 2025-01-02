namespace Marauder.Mini;

public class Unit;
public class PlayerUnit : Unit
{
    public uint Id { get; set; }
    public string Name { get; set; } = default!;
    public nint Address { get; set; }
    public bool IsMainPlayer { get; set; }
    public bool IsCorpse { get; set; }
    public uint AreaId { get; set; }
    public Position Position { get; set; } = default!;
}

public class GameReader(nint handle, nint baseAddress, nint baseSize)
{
    public MemoryReader Memory { get; set; } = new(handle, baseAddress, baseSize);

    /// <summary>
    /// Read player units from unit table
    /// </summary>
    /// <returns>Collection of all player units</returns>
    public IEnumerable<PlayerUnit> ReadPlayerUnits()
    {
        IEnumerable<PlayerUnit> units = [];

        if (Memory.Offsets.UnitTable == null)
        {
            throw new Exception("Attempted to access unit table offset before it was calculated");
        }

        if (Memory.Offsets.Expansion == null)
        {
            throw new Exception("Attempted to access expansion offset before it was calculated");
        }
        
        for (var i = 0; i < 128; i++)
        {
            nint unitOffset = Memory.Offsets.UnitTable.Value + (i * 8);
            nint unitAddress = (nint)Memory.ReadUInt64(Memory.BaseAddress + unitOffset);

            if (unitAddress == 0) continue;

            nint unitNameAddress = (nint)Memory.ReadUInt64(unitAddress + 0x10);
            nint unitPathAddress = (nint)Memory.ReadUInt64(unitAddress + 0x38);
            nint unitInventoryAddress = (nint)Memory.ReadUInt64(unitAddress + 0x90);
            nint unitRoom1Address = (nint)Memory.ReadUInt64(unitPathAddress + 0x20);
            nint unitRoom2Address = (nint)Memory.ReadUInt64(unitRoom1Address + 0x18);
            nint unitLevelAddress = (nint)Memory.ReadUInt64(unitRoom2Address + 0x90);
            nint expansionAddress = (nint)Memory.ReadUInt64(Memory.BaseAddress + Memory.Offsets.Expansion.Value);

            uint unitId = Memory.ReadUInt32(unitAddress + 0x08);
            uint areaId = Memory.ReadUInt32(unitLevelAddress + 0x1F8);

            string unitName = Memory.ReadString(unitNameAddress, 32 /* TODO: Better default? */);
            
            ushort x = Memory.ReadUInt16(unitPathAddress + 0x02);
            ushort y = Memory.ReadUInt16(unitPathAddress + 0x06);
            bool isExpansion = Memory.ReadUInt8(expansionAddress + 0x5C) > 0;
            bool isMainPlayer = Memory.ReadUInt16(unitInventoryAddress + 0x30) > 0;
            bool isCorpse = Memory.ReadUInt8(unitAddress + 0x1AE) == 1 && unitInventoryAddress > 0 && x > 0 && y > 0;
            if (isExpansion)
            {
                isMainPlayer = Memory.ReadUInt16(unitInventoryAddress + 0x70) > 0;
            }
            Position position = new((int)x, (int)y);

            units = units.Append(new PlayerUnit
            {
                Id = unitId,
                Name = unitName,
                Address = unitAddress,
                IsMainPlayer = isMainPlayer,
                IsCorpse = isCorpse,
                AreaId = areaId,
                Position = position
            });
        }
        return units;
    }
}