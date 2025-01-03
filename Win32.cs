using System.Runtime.InteropServices;
using Marauder.Mini.Structs;

namespace Marauder.Mini;

public static class Win32
{
    // Constants for ReadProcessMemory
    public const int PROCESS_QUERY_INFORMATION = 0x0400;
    public const int MEM_COMMIT = 0x00001000;
    public const int PROCESS_VM_OPERATION = 0x0008;
    public const int PROCESS_VM_READ = 0x0010;
    public const int PROCESS_VM_WRITE = 0x0020;
    public const int SYNCHRONIZE = 0x00100000;
    // Constants for SetWindowPos
    public static readonly nint HWND_TOP = nint.Zero;
    public const uint SWP_NOZORDER = 0x0004;
    public const uint SWP_NOACTIVATE = 0x0010;
    // Constants for ShowWindowAsync
    public const int SW_SHOWNORMAL = 1;
    public const int SW_SHOW = 5;
    public const int SW_RESTORE = 9;

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

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetWindowRect(nint hWnd, out Structs.Rectangle lpRect);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);

    [DllImport("user32.dll")]
    public static extern bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hWnd);
}
