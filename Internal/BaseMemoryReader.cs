using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace Marauder.Mini.Internal;

/// <summary>
/// Base implementation of IMemoryReader that uses Win32 APIs to read process memory.
/// </summary>
public class BaseMemoryReader : IMemoryReader
{
    private readonly IntPtr INVALID_HANDLE_VALUE = new(-1);
    private IntPtr _processHandle = IntPtr.Zero;
    private Process? _targetProcess;
    private bool _disposed;

    public Process TargetProcess => _targetProcess ?? throw new InvalidOperationException("Not attached to a process");
    public bool IsAttached => _processHandle != IntPtr.Zero && _processHandle != INVALID_HANDLE_VALUE;

    public async Task<MemoryOperationResult> AttachAsync(int processId)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(BaseMemoryReader));

        await Task.Yield(); // Allow for potential context switch

        try
        {
            // Detach from any existing process first
            if (IsAttached)
            {
                CloseHandle(_processHandle);
                _processHandle = IntPtr.Zero;
                _targetProcess = null;
            }

            // Get process handle with required access rights
            _processHandle = OpenProcess(
                ProcessAccessFlags.VirtualMemoryRead | 
                ProcessAccessFlags.QueryInformation,
                false,
                processId);

            if (_processHandle == IntPtr.Zero || _processHandle == INVALID_HANDLE_VALUE)
            {
                return MemoryOperationResult.Failed(
                    $"Failed to open process {processId}",
                    Marshal.GetLastWin32Error());
            }

            _targetProcess = Process.GetProcessById(processId);
            return MemoryOperationResult.Succeeded;
        }
        catch (Exception ex)
        {
            return MemoryOperationResult.Failed(
                $"Failed to attach to process: {ex.Message}");
        }
    }

    public async Task<(T? Value, MemoryOperationResult Result)> ReadAsync<T>(nint address) where T : unmanaged
    {
        if (!IsAttached)
            return (default, MemoryOperationResult.Failed("Not attached to a process"));

        await Task.Yield(); // Allow for potential context switch

        try
        {
            int size = Marshal.SizeOf<T>();
            var (bytes, result) = await ReadBytesAsync(address, size);
            
            if (!result.Success || bytes == null)
                return (default, result);

            GCHandle handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            try
            {
                T value = Marshal.PtrToStructure<T>(handle.AddrOfPinnedObject());
                return (value, MemoryOperationResult.Succeeded);
            }
            finally
            {
                handle.Free();
            }
        }
        catch (Exception ex)
        {
            return (default, MemoryOperationResult.Failed(
                $"Failed to read value: {ex.Message}"));
        }
    }

    public async Task<(byte[]? Bytes, MemoryOperationResult Result)> ReadBytesAsync(nint address, int size)
    {
        if (!IsAttached)
            return (null, MemoryOperationResult.Failed("Not attached to a process"));

        if (size <= 0)
            return (null, MemoryOperationResult.Failed("Invalid size specified"));

        await Task.Yield(); // Allow for potential context switch

        var buffer = new byte[size];
        var bytesRead = 0;

        try
        {
            if (!ReadProcessMemory(
                _processHandle,
                address,
                buffer,
                size,
                ref bytesRead))
            {
                return (null, MemoryOperationResult.Failed(
                    "Failed to read process memory",
                    Marshal.GetLastWin32Error()));
            }

            if (bytesRead != size)
            {
                return (null, MemoryOperationResult.Failed(
                    $"Partial read: {bytesRead} of {size} bytes"));
            }

            return (buffer, MemoryOperationResult.Succeeded);
        }
        catch (Exception ex)
        {
            return (null, MemoryOperationResult.Failed(
                $"Failed to read bytes: {ex.Message}"));
        }
    }

    public async Task<(string? Text, MemoryOperationResult Result)> ReadStringAsync(
        nint address,
        int maxLength = 1024,
        Encoding? encoding = null)
    {
        if (!IsAttached)
            return (null, MemoryOperationResult.Failed("Not attached to a process"));

        await Task.Yield(); // Allow for potential context switch

        encoding ??= Encoding.UTF8;
        var (bytes, result) = await ReadBytesAsync(address, maxLength);

        if (!result.Success || bytes == null)
            return (null, result);

        try
        {
            // Find null terminator
            int length = 0;
            while (length < bytes.Length && bytes[length] != 0)
                length++;

            if (length == 0)
                return (string.Empty, MemoryOperationResult.Succeeded);

            string text = encoding.GetString(bytes, 0, length);
            return (text, MemoryOperationResult.Succeeded);
        }
        catch (Exception ex)
        {
            return (null, MemoryOperationResult.Failed(
                $"Failed to decode string: {ex.Message}"));
        }
    }

    public async Task<(T? Value, MemoryOperationResult Result)> ReadChainAsync<T>(
        nint baseAddress,
        params int[] offsets) where T : unmanaged
    {
        if (!IsAttached)
            return (default, MemoryOperationResult.Failed("Not attached to a process"));

        if (offsets == null || offsets.Length == 0)
            return await ReadAsync<T>(baseAddress);

        await Task.Yield(); // Allow for potential context switch

        try
        {
            nint currentAddress = baseAddress;

            // Follow the pointer chain except for the last offset
            for (int i = 0; i < offsets.Length - 1; i++)
            {
                var (addr, result) = await ReadAsync<nint>(currentAddress + offsets[i]);
                
                if (!result.Success)
                    return (default, result);

                currentAddress = addr ?? default;
                
                if (currentAddress == IntPtr.Zero)
                    return (default, MemoryOperationResult.Failed("Null pointer in chain"));
            }

            // Read the final value
            return await ReadAsync<T>(currentAddress + offsets[^1]);
        }
        catch (Exception ex)
        {
            return (default, MemoryOperationResult.Failed(
                $"Failed to read pointer chain: {ex.Message}"));
        }
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                // Dispose managed state
                _targetProcess?.Dispose();
            }

            // Free unmanaged resources
            if (_processHandle != IntPtr.Zero && _processHandle != INVALID_HANDLE_VALUE)
            {
                CloseHandle(_processHandle);
                _processHandle = IntPtr.Zero;
            }

            _disposed = true;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    ~BaseMemoryReader()
    {
        Dispose(false);
    }

    #region Win32 API Imports

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(
        ProcessAccessFlags processAccess,
        bool bInheritHandle,
        int processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool ReadProcessMemory(
        IntPtr hProcess,
        nint lpBaseAddress,
        [Out] byte[] lpBuffer,
        int nSize,
        ref int lpNumberOfBytesRead);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);

    [Flags]
    private enum ProcessAccessFlags : uint
    {
        VirtualMemoryRead = 0x0010,
        QueryInformation = 0x0400
    }

    #endregion
}