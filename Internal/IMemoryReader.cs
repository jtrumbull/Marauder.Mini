using System.Diagnostics;
using System.Text;

namespace Marauder.Mini.Internal;

/// <summary>
/// Represents the result of a memory operation, containing status and error information.
/// </summary>
public readonly record struct MemoryOperationResult
{
    /// <summary>
    /// Indicates whether the memory operation was successful.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Error message if the operation failed, null otherwise.
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// Win32 error code if applicable, 0 otherwise.
    /// </summary>
    public int ErrorCode { get; init; }

    public static MemoryOperationResult Succeeded => new() { Success = true };
    public static MemoryOperationResult Failed(string error, int errorCode = 0) => 
        new() { Success = false, Error = error, ErrorCode = errorCode };
}

/// <summary>
/// Provides a high-level interface for reading process memory.
/// </summary>
public interface IMemoryReader : IDisposable
{
    /// <summary>
    /// Gets the process this memory reader is attached to.
    /// </summary>
    Process TargetProcess { get; }

    /// <summary>
    /// Gets whether the memory reader is currently attached to a process.
    /// </summary>
    bool IsAttached { get; }

    /// <summary>
    /// Attaches the memory reader to a specific process.
    /// </summary>
    /// <param name="processId">The ID of the process to attach to.</param>
    /// <returns>A result indicating success or failure of the operation.</returns>
    Task<MemoryOperationResult> AttachAsync(int processId);

    /// <summary>
    /// Reads a value of type T from the specified memory address.
    /// </summary>
    /// <typeparam name="T">The type of value to read.</typeparam>
    /// <param name="address">The memory address to read from.</param>
    /// <returns>The value read from memory and operation result.</returns>
    Task<(T? Value, MemoryOperationResult Result)> ReadAsync<T>(nint address) where T : unmanaged;

    /// <summary>
    /// Reads an array of bytes from the specified memory address.
    /// </summary>
    /// <param name="address">The memory address to read from.</param>
    /// <param name="size">Number of bytes to read.</param>
    /// <returns>The bytes read from memory and operation result.</returns>
    Task<(byte[]? Bytes, MemoryOperationResult Result)> ReadBytesAsync(nint address, int size);

    /// <summary>
    /// Reads a null-terminated string from the specified memory address.
    /// </summary>
    /// <param name="address">The memory address to read from.</param>
    /// <param name="maxLength">Maximum length of the string to read.</param>
    /// <param name="encoding">The encoding to use (default: UTF8).</param>
    /// <returns>The string read from memory and operation result.</returns>
    Task<(string? Text, MemoryOperationResult Result)> ReadStringAsync(
        nint address, 
        int maxLength = 1024, 
        Encoding? encoding = null);

    /// <summary>
    /// Reads a value from a pointer chain, following multiple offsets.
    /// </summary>
    /// <typeparam name="T">The type of value to read.</typeparam>
    /// <param name="baseAddress">The base address to start from.</param>
    /// <param name="offsets">The chain of offsets to follow.</param>
    /// <returns>The value read from the final address and operation result.</returns>
    Task<(T? Value, MemoryOperationResult Result)> ReadChainAsync<T>(nint baseAddress, params int[] offsets) 
        where T : unmanaged;
}