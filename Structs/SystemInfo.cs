namespace Marauder.Mini.Structs;

public struct SystemInfo
{
    public ushort processorArchitecture;
    public ushort reserved;
    public uint pageSize;
    public nint minimumApplicationAddress;
    public nint maximumApplicationAddress;
    public nint activeProcessorMask;
    public uint numberOfProcessors;
    public uint processorType;
    public uint allocationGranularity;
    public ushort processorLevel;
    public ushort processorRevision;
}