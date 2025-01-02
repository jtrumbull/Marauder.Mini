using System.Runtime.InteropServices;

namespace Marauder.Mini;

public static class Win32
{
    public const int PROCESS_QUERY_INFORMATION = 0x0400;
    public const int MEM_COMMIT = 0x00001000;
    public const int PROCESS_VM_OPERATION = 0x0008;
    public const int PROCESS_VM_READ = 0x0010;
    public const int PROCESS_VM_WRITE = 0x0020;
    public const int SYNCHRONIZE = 0x00100000;

    [DllImport("kernel32.dll")]
    public static extern bool ReadProcessMemory(nint hProcess, nint lpBaseAddress, byte[] lpBuffer, int dwSize, ref int lpNumberOfBytesRead);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool WriteProcessMemory(int hProcess, nint lpBaseAddress, byte[] lpBuffer, int nSize, ref int lpNumberOfBytesWritten);

    [DllImport("kernel32.dll")]
    public static extern nint OpenProcess(int dwDesiredAccess, bool bInheritHandle, int dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool CloseHandle(nint handle);

    [DllImport("kernel32.dll")]
    static extern void GetSystemInfo(out SystemInfo lpSystemInfo);
}

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