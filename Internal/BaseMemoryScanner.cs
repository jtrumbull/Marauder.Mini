using System.Runtime.InteropServices;
using System.Text;
using static Marauder.Mini.Internal.IMemoryScanner;

namespace Marauder.Mini.Internal;

/// <summary>
/// Base implementation of IMemoryScanner that provides memory scanning functionality.
/// </summary>
public class BaseMemoryScanner : IMemoryScanner
{
    protected const int BUFFER_SIZE = 4096;
    private bool _disposed;

    public IMemoryReader MemoryReader { get; }

    public BaseMemoryScanner(IMemoryReader memoryReader)
    {
        MemoryReader = memoryReader;
    }

    public async Task<(IReadOnlyCollection<MemoryRegion>? Regions, MemoryOperationResult Result)> GetMemoryRegionsAsync(
        MemoryProtection protection = MemoryProtection.ReadWrite)
    {
        if (!MemoryReader.IsAttached)
            return (null, MemoryOperationResult.Failed("Not attached to a process"));

        await Task.Yield();

        try
        {
            var regions = new List<MemoryRegion>();
            var systemInfo = new SYSTEM_INFO();
            GetSystemInfo(ref systemInfo);

            var address = systemInfo.minimumApplicationAddress;
            var endAddress = systemInfo.maximumApplicationAddress;

            while ((long)address < (long)endAddress)
            {
                var memInfo = new MEMORY_BASIC_INFORMATION();
                var result = VirtualQueryEx(
                    MemoryReader.TargetProcess.Handle,
                    address,
                    ref memInfo,
                    (uint)Marshal.SizeOf<MEMORY_BASIC_INFORMATION>());

                if (result == 0)
                    break;

                var regionProtection = (MemoryProtection)memInfo.Protect;
                if ((regionProtection & protection) == protection &&
                    memInfo.State == MemoryState.MEM_COMMIT)
                {
                    regions.Add(new MemoryRegion(
                        (nint)memInfo.BaseAddress,
                        (int)memInfo.RegionSize,
                        regionProtection));
                }

                address = (nint)((long)memInfo.BaseAddress + (long)memInfo.RegionSize);
            }

            return (regions, MemoryOperationResult.Succeeded);
        }
        catch (Exception ex)
        {
            return (null, MemoryOperationResult.Failed($"Failed to get memory regions: {ex.Message}"));
        }
    }

    public async Task<(IReadOnlyCollection<nint>? Addresses, MemoryOperationResult Result)> ScanForValueAsync<T>(
        T value,
        IEnumerable<MemoryRegion>? regions = null) where T : unmanaged
    {
        if (!MemoryReader.IsAttached)
            return (null, MemoryOperationResult.Failed("Not attached to a process"));

        try
        {
            regions ??= (await GetMemoryRegionsAsync()).Regions;
            if (regions == null)
                return (null, MemoryOperationResult.Failed("Failed to get memory regions"));

            var matches = new List<nint>();
            var valueBytes = new byte[Marshal.SizeOf<T>()];
            GCHandle handle = GCHandle.Alloc(value, GCHandleType.Pinned);

            try
            {
                Marshal.Copy(handle.AddrOfPinnedObject(), valueBytes, 0, valueBytes.Length);
            }
            finally
            {
                handle.Free();
            }

            foreach (var region in regions)
            {
                var (addresses, result) = await ScanRegionForBytesAsync(region, valueBytes);
                if (result.Success && addresses != null)
                    matches.AddRange(addresses);
            }

            return (matches, MemoryOperationResult.Succeeded);
        }
        catch (Exception ex)
        {
            return (null, MemoryOperationResult.Failed($"Failed to scan for value: {ex.Message}"));
        }
    }

    public async Task<(IReadOnlyCollection<nint>? Addresses, MemoryOperationResult Result)> ScanForPatternAsync(
        byte[] pattern,
        string mask,
        IEnumerable<MemoryRegion>? regions = null)
    {
        if (!MemoryReader.IsAttached)
            return (null, MemoryOperationResult.Failed("Not attached to a process"));

        if (pattern.Length != mask.Length)
            return (null, MemoryOperationResult.Failed("Pattern and mask lengths do not match"));

        try
        {
            regions ??= (await GetMemoryRegionsAsync()).Regions;
            if (regions == null)
                return (null, MemoryOperationResult.Failed("Failed to get memory regions"));

            var matches = new List<nint>();

            foreach (var region in regions)
            {
                var (regionBytes, readResult) = await MemoryReader.ReadBytesAsync(region.BaseAddress, region.Size);
                if (!readResult.Success || regionBytes == null)
                    continue;

                for (int i = 0; i <= regionBytes.Length - pattern.Length; i++)
                {
                    bool found = true;
                    for (int j = 0; j < pattern.Length; j++)
                    {
                        if (mask[j] == 'x' && pattern[j] != regionBytes[i + j])
                        {
                            found = false;
                            break;
                        }
                    }

                    if (found)
                        matches.Add(region.BaseAddress + i);
                }
            }

            return (matches, MemoryOperationResult.Succeeded);
        }
        catch (Exception ex)
        {
            return (null, MemoryOperationResult.Failed($"Failed to scan for pattern: {ex.Message}"));
        }
    }

    public async Task<(IReadOnlyCollection<nint>? Addresses, MemoryOperationResult Result)> ScanForStringAsync(
        string text,
        Encoding? encoding = null,
        IEnumerable<MemoryRegion>? regions = null)
    {
        encoding ??= Encoding.UTF8;
        var bytes = encoding.GetBytes(text);
        var mask = new string('x', bytes.Length);
        return await ScanForPatternAsync(bytes, mask, regions);
    }

    public async Task<(IReadOnlyCollection<nint>? Addresses, MemoryOperationResult Result)> DifferentialScanAsync(
        ComparisonOperation comparison,
        IReadOnlyCollection<nint> previousAddresses)
    {
        if (!MemoryReader.IsAttached)
            return (null, MemoryOperationResult.Failed("Not attached to a process"));

        if (previousAddresses.Count == 0)
            return (new List<nint>(), MemoryOperationResult.Succeeded);

        try
        {
            var matches = new List<nint>();

            foreach (var address in previousAddresses)
            {
                var (currentValue, currentResult) = await MemoryReader.ReadAsync<long>(address);
                var (previousValue, previousResult) = await MemoryReader.ReadAsync<long>(address);

                if (!currentResult.Success || !previousResult.Success)
                    continue;

                bool match = comparison switch
                {
                    ComparisonOperation.Equal => currentValue == previousValue,
                    ComparisonOperation.NotEqual => currentValue != previousValue,
                    ComparisonOperation.Greater => currentValue > previousValue,
                    ComparisonOperation.Less => currentValue < previousValue,
                    ComparisonOperation.GreaterOrEqual => currentValue >= previousValue,
                    ComparisonOperation.LessOrEqual => currentValue <= previousValue,
                    ComparisonOperation.Changed => currentValue != previousValue,
                    ComparisonOperation.Unchanged => currentValue == previousValue,
                    ComparisonOperation.Increased => currentValue > previousValue,
                    ComparisonOperation.Decreased => currentValue < previousValue,
                    _ => throw new ArgumentException($"Unknown comparison operation: {comparison}")
                };

                if (match)
                    matches.Add(address);
            }

            return (matches, MemoryOperationResult.Succeeded);
        }
        catch (Exception ex)
        {
            return (null, MemoryOperationResult.Failed($"Failed to perform differential scan: {ex.Message}"));
        }
    }

    protected async Task<(IReadOnlyCollection<nint>? Addresses, MemoryOperationResult Result)> ScanRegionForBytesAsync(
        MemoryRegion region,
        byte[] pattern)
    {
        var matches = new List<nint>();
        var buffer = new byte[BUFFER_SIZE];

        for (int offset = 0; offset < region.Size; offset += BUFFER_SIZE)
        {
            int bytesToRead = Math.Min(BUFFER_SIZE, region.Size - offset);
            var (bytes, result) = await MemoryReader.ReadBytesAsync(region.BaseAddress + offset, bytesToRead);

            if (!result.Success || bytes == null)
                continue;

            for (int i = 0; i <= bytes.Length - pattern.Length; i++)
            {
                bool found = true;
                for (int j = 0; j < pattern.Length; j++)
                {
                    if (pattern[j] != bytes[i + j])
                    {
                        found = false;
                        break;
                    }
                }

                if (found)
                    matches.Add(region.BaseAddress + offset + i);
            }
        }

        return (matches, MemoryOperationResult.Succeeded);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            _disposed = true;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    #region Native Structures

    [StructLayout(LayoutKind.Sequential)]
    private struct SYSTEM_INFO
    {
        public ushort processorArchitecture;
        ushort reserved;
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

    [StructLayout(LayoutKind.Sequential)]
    private struct MEMORY_BASIC_INFORMATION
    {
        public nint BaseAddress;
        public nint AllocationBase;
        public uint AllocationProtect;
        public nint RegionSize;
        public MemoryState State;
        public uint Protect;
        public uint Type;
    }

    private enum MemoryState : uint
    {
        MEM_COMMIT = 0x1000,
        MEM_RESERVE = 0x2000,
        MEM_FREE = 0x10000
    }

    #endregion

    #region Native Imports

    [DllImport("kernel32.dll")]
    private static extern void GetSystemInfo(ref SYSTEM_INFO lpSystemInfo);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern int VirtualQueryEx(
        IntPtr hProcess,
        nint lpAddress,
        ref MEMORY_BASIC_INFORMATION lpBuffer,
        uint dwLength);

    #endregion
}