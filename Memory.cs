using System.Text;

namespace Marauder.Mini;

/// <summary>
/// Memory reader class
/// </summary>
public class MemoryReader
{
    public MemoryOffsets Offsets = new();
    public MemoryScanner Scanner = new();

    protected readonly nint  _handle;
    protected readonly nint  _baseAddress;
    protected readonly nint  _baseSize;

    public nint BaseAddress => _baseAddress;

    public MemoryReader(nint handle, nint baseAddress, nint baseSize)
    {
        _handle = handle;
        _baseAddress = baseAddress;
        _baseSize = baseSize;
    }

    public (byte[], int) ReadMemory(nint address, int bytes)
    {
        int bytesRead = 0;
        byte[] buffer = new byte[bytes];
        Win32.ReadProcessMemory(_handle, address, buffer, bytes, ref bytesRead);
        return (buffer, bytesRead);
    }

    public byte[] ReadBytes(nint address, int bytes)
    {
        var (buffer, bytesRead) = ReadMemory(address, bytes);
        return buffer;
    }

    public string ReadString(nint address, int maxLength)
    {
        var (buffer, bytesRead) = ReadMemory(address, maxLength);
        int stringLength = Array.IndexOf(buffer, (byte)0);
        stringLength = stringLength < 0 ? buffer.Length : stringLength;
        return Encoding.UTF8.GetString(buffer, 0, stringLength);
    }

    public short ReadInt16(nint address) => BitConverter.ToInt16(ReadBytes(address, 2));
    public int ReadInt32(nint address) => BitConverter.ToInt32(ReadBytes(address, 4));
    public long ReadInt64(nint address) => BitConverter.ToInt64(ReadBytes(address, 8));
    public byte ReadUInt8(nint address) => ReadBytes(address, 1)[0];
    public ushort ReadUInt16(nint address) => BitConverter.ToUInt16(ReadBytes(address, 2));
    public uint ReadUInt32(nint address) => BitConverter.ToUInt32(ReadBytes(address, 4));
    public ulong ReadUInt64(nint address) => BitConverter.ToUInt64(ReadBytes(address, 8));

    /// <summary>
    /// Calculate memory offsets
    /// </summary>
    /// <exception cref="Exception"></exception>
    public void CalculateOffsets()
    {
        var (memory, bytesRead) = ReadMemory(_baseAddress, (int)_baseSize);

        // Game data

        byte[] pattern = [0x44, 0x88, 0x25, 0x00, 0x00, 0x00, 0x00, 0x66, 0x44, 0x89, 0x25, 0x00, 0x00, 0x00, 0x00];
        string mask = "xxx????xxxx????";

        var match = FindPattern(memory, _baseAddress, pattern, mask) 
            ?? throw new Exception("Game data offset pattern not found");

        uint gameDataOffsetInt = ReadUInt32(match + 0x3);
        
        Offsets.GameData = match - _baseAddress - 0x121 + (nint)gameDataOffsetInt;

        // Unit table

        pattern = [0x48, 0x03, 0xC7, 0x49, 0x8B, 0x8C, 0xC6];
        mask = "xxxxxxx";

        match = FindPattern(memory, _baseAddress, pattern, mask) 
            ?? throw new Exception("Unit table offset pattern not found");
        
        Offsets.UnitTable = (nint)ReadUInt32(match + 0x07);

        // UI

        pattern = [0x40, 0x84, 0xed, 0x0f, 0x94, 0x05];
        mask = "xxxxxx";

        match = FindPattern(memory, _baseAddress, pattern, mask) 
            ?? throw new Exception("UI offset pattern not found");

        Offsets.UI = (nint)ReadUInt32(match + 0x06);

        // Hover

        pattern = [0xC6, 0x84, 0xC2, 0x00, 0x00, 0x00, 0x00, 0x00, 0x48, 0x8B, 0x74];
        mask = "xxx?????xxx";

        match = FindPattern(memory, _baseAddress, pattern, mask) 
            ?? throw new Exception("Hover offset pattern not found");

        Offsets.Hover = (nint)ReadUInt32(match + 0x03) - 1;

        // Expansion

        pattern = [0x48, 0x8B, 0x05, 0x00, 0x00, 0x00, 0x00, 0x48, 0x8B, 0xD9, 0xF3, 0x0F, 0x10, 0x50, 0x00];
        mask = "xxx????xxxxxxx?";

        match = FindPattern(memory, _baseAddress, pattern, mask) 
            ?? throw new Exception("Expansion offset pattern not found");
        
        var expansionInt = ReadUInt32(match + 0x03);
        
        Offsets.Expansion = (nint)match - _baseAddress + 0x07 + (nint)expansionInt;

        // Roster offset
        
        pattern = [0x48, 0x8B, 0x05, 0x00, 0x00, 0x00, 0x00, 0x48, 0x8B, 0xD9, 0xF3, 0x0F, 0x10, 0x50, 0x00];
        mask = "xxx????xxxxxxx?";

        match = FindPattern(memory, _baseAddress, pattern, mask) 
            ?? throw new Exception("Roster offset pattern not found");
        
        var rosterInt = ReadUInt32(match - 0x03);
        
        Offsets.Roster = (nint)match - _baseAddress + 0x01 + (nint)rosterInt;
    }

    /// <summary>
    /// Find pattern
    /// </summary>
    /// <param name="memory"></param>
    /// <param name="baseAddress"></param>
    /// <param name="pattern"></param>
    /// <param name="mask"></param>
    /// <returns></returns>
    private static nint? FindPattern(byte[] memory, nint baseAddress, byte[] pattern, string mask)
    {
        nint? found = null;
        for (var i = 0; i < memory.Length - pattern.Length; i++)
        {
            bool match = true;
            for (int j = 0; j < pattern.Length; j++)
            {
                if (mask[j] != '?' && memory[i + j] != pattern[j])
                {
                    match = false;
                    break;
                }
            }
            if (match)
            {
                found = nint.Add(baseAddress, i);
                break;
            }
        }
        return found;
    }

    public string ReadSelectedCharacter()
    {
        var selectedCharacter = "";
        return selectedCharacter;
    }
}

/// <summary>
/// Memory offsets model
/// </summary>
public class MemoryOffsets
{
    public nint? GameData;
    public nint? UnitTable;
    public nint? UI;
    public nint? Hover;
    public nint? Expansion;
    public nint? Roster;
}

/// <summary>
/// Memory scanner class
/// </summary>
public class MemoryScanner
{

}