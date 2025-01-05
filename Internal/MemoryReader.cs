using System.Text;
using System.Runtime.CompilerServices;
using System.Diagnostics;

namespace Marauder.Mini.Internal;

/// <summary>
/// Final implementation of memory reader with additional utility methods and syntactic sugar.
/// </summary>
public sealed class MemoryReader : BaseMemoryReader
{
    /// <summary>
    /// Creates a reader and attaches it to a process.
    /// </summary>
    public static async Task<(MemoryReader? Reader, MemoryOperationResult Result)> CreateAsync(int processId)
    {
        var reader = new MemoryReader();
        var result = await reader.AttachAsync(processId);
        
        return result.Success 
            ? (reader, result) 
            : (null, result);
    }

    /// <summary>
    /// Creates a reader and attaches it to a process by name.
    /// </summary>
    public static async Task<(MemoryReader? Reader, MemoryOperationResult Result)> CreateAsync(string processName)
    {
        try
        {
            var processes = Process.GetProcessesByName(processName);
            if (processes.Length == 0)
                return (null, MemoryOperationResult.Failed($"Process '{processName}' not found"));
            
            if (processes.Length > 1)
                return (null, MemoryOperationResult.Failed($"Multiple processes named '{processName}' found"));
            
            return await CreateAsync(processes[0].Id);
        }
        catch (Exception ex)
        {
            return (null, MemoryOperationResult.Failed($"Failed to create reader: {ex.Message}"));
        }
    }

    /// <summary>
    /// Read operation with implicit error handling through ConfigureAwait.
    /// </summary>
    public MemoryOperation<T> Read<T>(nint address) where T : unmanaged =>
        new(this, address);

    /// <summary>
    /// String read operation with implicit error handling through ConfigureAwait.
    /// </summary>
    public MemoryStringOperation ReadString(nint address, int maxLength = 1024, Encoding? encoding = null) =>
        new(this, address, maxLength, encoding);

    /// <summary>
    /// Read a chain of pointers with implicit error handling through ConfigureAwait.
    /// </summary>
    public MemoryChainOperation<T> ReadChain<T>(nint baseAddress, params int[] offsets) where T : unmanaged =>
        new(this, baseAddress, offsets);

    /// <summary>
    /// Creates a pointer to a specific type at the given address.
    /// </summary>
    public MemoryPointer<T> CreatePointer<T>(nint address) where T : unmanaged =>
        new(this, address);

    /// <summary>
    /// Creates a string pointer at the given address.
    /// </summary>
    public MemoryStringPointer CreateStringPointer(nint address, int maxLength = 1024, Encoding? encoding = null) =>
        new(this, address, maxLength, encoding);
}

#region Syntactic Sugar Types

/// <summary>
/// Represents a memory read operation that can be configured and awaited.
/// </summary>
public readonly struct MemoryOperation<T> where T : unmanaged
{
    private readonly IMemoryReader _reader;
    private readonly nint _address;

    internal MemoryOperation(IMemoryReader reader, nint address)
    {
        _reader = reader;
        _address = address;
    }

    public TaskAwaiter<T> GetAwaiter()
    {
        return _reader.ReadAsync<T>(_address)
            .ContinueWith(t => 
            {
                if (!t.Result.Result.Success)
                    throw new MemoryReadException(t.Result.Result.Error ?? "Unknown error");
                return t.Result.Value!.Value;
            }).GetAwaiter();
    }
}

/// <summary>
/// Represents a memory string read operation that can be configured and awaited.
/// </summary>
public readonly struct MemoryStringOperation
{
    private readonly IMemoryReader _reader;
    private readonly nint _address;
    private readonly int _maxLength;
    private readonly Encoding? _encoding;

    internal MemoryStringOperation(IMemoryReader reader, nint address, int maxLength, Encoding? encoding)
    {
        _reader = reader;
        _address = address;
        _maxLength = maxLength;
        _encoding = encoding;
    }

    public TaskAwaiter<string> GetAwaiter()
    {
        return _reader.ReadStringAsync(_address, _maxLength, _encoding)
            .ContinueWith(t => 
            {
                if (!t.Result.Result.Success)
                    throw new MemoryReadException(t.Result.Result.Error ?? "Unknown error");
                return t.Result.Text!;
            }).GetAwaiter();
    }
}

/// <summary>
/// Represents a memory pointer chain read operation that can be configured and awaited.
/// </summary>
public readonly struct MemoryChainOperation<T> where T : unmanaged
{
    private readonly IMemoryReader _reader;
    private readonly nint _baseAddress;
    private readonly int[] _offsets;

    internal MemoryChainOperation(IMemoryReader reader, nint baseAddress, int[] offsets)
    {
        _reader = reader;
        _baseAddress = baseAddress;
        _offsets = offsets;
    }

    public TaskAwaiter<T> GetAwaiter()
    {
        return _reader.ReadChainAsync<T>(_baseAddress, _offsets)
            .ContinueWith(t => 
            {
                if (!t.Result.Result.Success)
                    throw new MemoryReadException(t.Result.Result.Error ?? "Unknown error");
                return t.Result.Value!.Value;
            }).GetAwaiter();
    }
}

/// <summary>
/// Represents a pointer to a value in memory that can be repeatedly read.
/// </summary>
public sealed class MemoryPointer<T> where T : unmanaged
{
    private readonly IMemoryReader _reader;
    private readonly nint _address;

    internal MemoryPointer(IMemoryReader reader, nint address)
    {
        _reader = reader;
        _address = address;
    }

    public async Task<T> ReadAsync()
    {
        var (value, result) = await _reader.ReadAsync<T>(_address);
        if (!result.Success)
            throw new MemoryReadException(result.Error ?? "Unknown error");
        return value!.Value;
    }

    public MemoryOperation<T> Read() => new(_reader, _address);
}

/// <summary>
/// Represents a pointer to a string in memory that can be repeatedly read.
/// </summary>
public sealed class MemoryStringPointer
{
    private readonly IMemoryReader _reader;
    private readonly nint _address;
    private readonly int _maxLength;
    private readonly Encoding? _encoding;

    internal MemoryStringPointer(IMemoryReader reader, nint address, int maxLength, Encoding? encoding)
    {
        _reader = reader;
        _address = address;
        _maxLength = maxLength;
        _encoding = encoding;
    }

    public async Task<string> ReadAsync()
    {
        var (text, result) = await _reader.ReadStringAsync(_address, _maxLength, _encoding);
        if (!result.Success)
            throw new MemoryReadException(result.Error ?? "Unknown error");
        return text!;
    }

    public MemoryStringOperation Read() => new(_reader, _address, _maxLength, _encoding);
}

/// <summary>
/// Exception thrown when a memory read operation fails.
/// </summary>
public class MemoryReadException : Exception
{
    public MemoryReadException(string message) : base(message) { }
}

#endregion

/// <summary>
/// Extension methods for MemoryReader.
/// </summary>
public static class MemoryReaderExtensions
{
    /// <summary>
    /// Reads a value and throws an exception if the read fails.
    /// </summary>
    public static async Task<T> ReadOrThrowAsync<T>(this IMemoryReader reader, nint address) where T : unmanaged
    {
        var (value, result) = await reader.ReadAsync<T>(address);
        if (!result.Success)
            throw new MemoryReadException(result.Error ?? "Unknown error");
        return value!.Value;
    }

    /// <summary>
    /// Reads a string and throws an exception if the read fails.
    /// </summary>
    public static async Task<string> ReadStringOrThrowAsync(
        this IMemoryReader reader,
        nint address,
        int maxLength = 1024,
        Encoding? encoding = null)
    {
        var (text, result) = await reader.ReadStringAsync(address, maxLength, encoding);
        if (!result.Success)
            throw new MemoryReadException(result.Error ?? "Unknown error");
        return text!;
    }

    /// <summary>
    /// Reads a pointer chain and throws an exception if the read fails.
    /// </summary>
    public static async Task<T> ReadChainOrThrowAsync<T>(
        this IMemoryReader reader,
        nint baseAddress,
        params int[] offsets) where T : unmanaged
    {
        var (value, result) = await reader.ReadChainAsync<T>(baseAddress, offsets);
        if (!result.Success)
            throw new MemoryReadException(result.Error ?? "Unknown error");
        return value!.Value;
    }
}