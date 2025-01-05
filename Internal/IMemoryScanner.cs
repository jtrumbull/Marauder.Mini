using System.Text;

namespace Marauder.Mini.Internal;

/// <summary>
/// Defines memory protection flags.
/// </summary>
[Flags]
public enum MemoryProtection
{
    None = 0,
    Read = 1,
    Write = 2,
    Execute = 4,
    ReadWrite = Read | Write,
    ReadExecute = Read | Execute,
    ReadWriteExecute = Read | Write | Execute
}

/// <summary>
/// Defines comparison operations for differential scanning.
/// </summary>
public enum ComparisonOperation
{
    Equal,
    NotEqual,
    Greater,
    Less,
    GreaterOrEqual,
    LessOrEqual,
    Changed,
    Unchanged,
    Increased,
    Decreased
}

/// <summary>
/// Provides functionality for scanning process memory for specific patterns or values.
/// </summary>
public interface IMemoryScanner : IDisposable
{
    /// <summary>
    /// Gets the memory reader used by this scanner.
    /// </summary>
    IMemoryReader MemoryReader { get; }

    /// <summary>
    /// Represents a memory region to be scanned.
    /// </summary>
    public readonly record struct MemoryRegion(nint BaseAddress, int Size, MemoryProtection Protection);

    /// <summary>
    /// Gets all memory regions in the target process that match the specified protection flags.
    /// </summary>
    /// <param name="protection">Memory protection flags to filter by.</param>
    /// <returns>Collection of memory regions and operation result.</returns>
    Task<(IReadOnlyCollection<MemoryRegion>? Regions, MemoryOperationResult Result)> GetMemoryRegionsAsync(
        MemoryProtection protection = MemoryProtection.ReadWrite);

    /// <summary>
    /// Scans memory for a specific value of type T.
    /// </summary>
    /// <typeparam name="T">The type of value to scan for.</typeparam>
    /// <param name="value">The value to find.</param>
    /// <param name="regions">Optional specific regions to scan. If null, scans all readable regions.</param>
    /// <returns>Collection of addresses where the value was found and operation result.</returns>
    Task<(IReadOnlyCollection<nint>? Addresses, MemoryOperationResult Result)> ScanForValueAsync<T>(
        T value,
        IEnumerable<MemoryRegion>? regions = null) where T : unmanaged;

    /// <summary>
    /// Scans memory for a specific byte pattern with optional wildcards.
    /// </summary>
    /// <param name="pattern">The byte pattern to search for.</param>
    /// <param name="mask">The mask string where 'x' means match and '?' means wildcard.</param>
    /// <param name="regions">Optional specific regions to scan. If null, scans all readable regions.</param>
    /// <returns>Collection of addresses where the pattern was found and operation result.</returns>
    Task<(IReadOnlyCollection<nint>? Addresses, MemoryOperationResult Result)> ScanForPatternAsync(
        byte[] pattern,
        string mask,
        IEnumerable<MemoryRegion>? regions = null);

    /// <summary>
    /// Scans memory for a string using the specified encoding.
    /// </summary>
    /// <param name="text">The string to search for.</param>
    /// <param name="encoding">The encoding to use (default: UTF8).</param>
    /// <param name="regions">Optional specific regions to scan. If null, scans all readable regions.</param>
    /// <returns>Collection of addresses where the string was found and operation result.</returns>
    Task<(IReadOnlyCollection<nint>? Addresses, MemoryOperationResult Result)> ScanForStringAsync(
        string text,
        Encoding? encoding = null,
        IEnumerable<MemoryRegion>? regions = null);

    /// <summary>
    /// Performs a differential scan comparing with previous scan results.
    /// </summary>
    /// <param name="comparison">The comparison operation to perform.</param>
    /// <param name="previousAddresses">Addresses from the previous scan.</param>
    /// <returns>Collection of addresses that match the comparison and operation result.</returns>
    Task<(IReadOnlyCollection<nint>? Addresses, MemoryOperationResult Result)> DifferentialScanAsync(
        ComparisonOperation comparison,
        IReadOnlyCollection<nint> previousAddresses);
}