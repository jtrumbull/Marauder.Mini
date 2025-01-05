using System.Diagnostics;
using Marauder.Mini.Enums;

namespace Marauder.Mini.Internal;

/// <summary>
/// Specialized memory scanner for Diablo 2 Resurrected memory structures.
/// Provides functionality to read game state, player location, and session information.
/// Based on patterns and offsets from hectorgimenez/d2go utilities.
/// </summary>
public sealed class D2RMemoryScanner : BaseMemoryScanner
{
    // Process identification
    private const string ProcessName = "D2R";
    private const string ModuleName = "D2R.exe";

    // Memory signatures for key game structures
    private static readonly byte[] PlayerPattern = 
        { 0x48, 0x8B, 0x05, 0x00, 0x00, 0x00, 0x00, 0x48, 0x8B, 0x88, 0x00, 0x00, 0x00, 0x00 };
    private static readonly string PlayerMask = "xxx????xxx????";

    private static readonly byte[] UnitTablePattern = 
        { 0x48, 0x8B, 0x05, 0x00, 0x00, 0x00, 0x00, 0x48, 0x8B, 0x0C, 0xC8 };
    private static readonly string UnitTableMask = "xxx????xxxx";

    private static readonly byte[] GameInfoPattern = 
        { 0x48, 0x8B, 0x05, 0x00, 0x00, 0x00, 0x00, 0x48, 0x85, 0xC0, 0x74, 0x00, 0x48, 0x8B, 0x40, 0x00 };
    private static readonly string GameInfoMask = "xxx????xxxx?xxx?";

    // Memory structure offsets
    private const int PLAYER_UNIT_ID_OFFSET = 0x08;
    private const int UNIT_ENTRY_SIZE = 0x10;
    private const int UNIT_LEVEL_OFFSET = 0x0C;
    private const int GAME_DIFFICULTY_OFFSET = 0x38;
    private const int GAME_NAME_OFFSET = 0x128;
    private const int GAME_PASSWORD_OFFSET = 0x140;
    private const int GAME_DESCRIPTION_OFFSET = 0x158;

    private nint _moduleBase;
    private readonly Dictionary<string, nint> _cachedOffsets = [];

    public D2RMemoryScanner(IMemoryReader memoryReader) : base(memoryReader) { }

    /// <summary>
    /// Creates a new scanner instance and attaches it to the D2R process.
    /// Handles process location, memory attachment, and initial setup.
    /// </summary>
    public static async Task<(D2RMemoryScanner? Scanner, MemoryOperationResult Result)> CreateAsync()
    {
        await Task.Yield(); // Allow for potential context switch

        try
        {
            // Create and attach the memory reader
            var (reader, readerResult) = await Internal.MemoryReader.CreateAsync(ProcessName);
            if (!readerResult.Success || reader == null)
                return (null, readerResult);

            // Initialize the scanner
            var scanner = new D2RMemoryScanner(reader);
            var initResult = await scanner.InitializeAsync();
            
            return initResult.Success 
                ? (scanner, initResult)
                : (null, initResult);
        }
        catch (Exception ex)
        {
            return (null, MemoryOperationResult.Failed($"Failed to create scanner: {ex.Message}"));
        }
    }

    /// <summary>
    /// Initializes the scanner by locating the game module and storing its base address.
    /// </summary>
    private async Task<MemoryOperationResult> InitializeAsync()
    {
        await Task.Yield(); // Allow for potential context switch

        try
        {
            // Find the main game module
            var module = MemoryReader.TargetProcess.Modules.Cast<ProcessModule>()
                .FirstOrDefault(m => m.ModuleName == ModuleName);

            if (module == null)
                return MemoryOperationResult.Failed("D2R.exe module not found");

            _moduleBase = module.BaseAddress;
            return MemoryOperationResult.Succeeded;
        }
        catch (Exception ex)
        {
            return MemoryOperationResult.Failed($"Failed to initialize scanner: {ex.Message}");
        }
    }

    /// <summary>
    /// Locates the player unit structure in memory.
    /// Uses pattern scanning to find the reference and calculates the final offset.
    /// </summary>
    public async Task<(nint Offset, MemoryOperationResult Result)> GetPlayerOffsetAsync()
    {
        await Task.Yield(); // Allow for potential context switch

        // Check cache first
        if (_cachedOffsets.TryGetValue("Player", out var offset))
            return (offset, MemoryOperationResult.Succeeded);

        // Scan for the player pattern
        var (addresses, result) = await ScanForPatternAsync(PlayerPattern, PlayerMask);
        if (!result.Success || addresses == null || !addresses.Any())
            return (0, MemoryOperationResult.Failed("Player pattern not found"));

        try
        {
            // Calculate the actual offset from the pattern
            var (relativeOffset, readResult) = await MemoryReader.ReadAsync<int>(addresses.First() + 3);
            if (!readResult.Success || relativeOffset == null)
                return (0, readResult);

            // Store in cache and return
            offset = addresses.First() + relativeOffset.Value + 7;
            _cachedOffsets["Player"] = offset;
            return (offset, MemoryOperationResult.Succeeded);
        }
        catch (Exception ex)
        {
            return (0, MemoryOperationResult.Failed($"Failed to get player offset: {ex.Message}"));
        }
    }

    /// <summary>
    /// Retrieves the player's current level/area.
    /// Follows the pointer chain: Player -> Unit -> Level ID
    /// </summary>
    public async Task<(Level? CurrentLevel, MemoryOperationResult Result)> GetPlayerLevelAsync()
    {
        await Task.Yield(); // Allow for potential context switch

        // Get player structure location
        var (playerOffset, offsetResult) = await GetPlayerOffsetAsync();
        if (!offsetResult.Success)
            return (null, offsetResult);

        try
        {
            // Read player pointer
            var (playerPtr, readResult) = await MemoryReader.ReadAsync<nint>(playerOffset);
            if (!readResult.Success || playerPtr == null)
                return (null, readResult);

            // Get player's unit ID
            var (unitId, unitResult) = await MemoryReader.ReadAsync<int>(playerPtr.Value + PLAYER_UNIT_ID_OFFSET);
            if (!unitResult.Success || unitId == null)
                return (null, unitResult);

            // Lookup level ID from unit table
            var (levelId, levelResult) = await GetUnitLevelAsync(unitId.Value);
            if (!levelResult.Success)
                return (null, levelResult);

            return ((Level)levelId, MemoryOperationResult.Succeeded);
        }
        catch (Exception ex)
        {
            return (null, MemoryOperationResult.Failed($"Failed to get player level: {ex.Message}"));
        }
    }

    /// <summary>
    /// Gets a unit's current level/area ID from the unit table.
    /// </summary>
    private async Task<(int LevelId, MemoryOperationResult Result)> GetUnitLevelAsync(int unitId)
    {
        await Task.Yield(); // Allow for potential context switch

        var (tableOffset, offsetResult) = await GetUnitTableOffsetAsync();
        if (!offsetResult.Success)
            return (0, offsetResult);

        try
        {
            // Read unit table base pointer
            var (tablePtr, tableResult) = await MemoryReader.ReadAsync<nint>(tableOffset);
            if (!tableResult.Success || tablePtr == null)
                return (0, tableResult);

            // Calculate unit entry address and read level ID
            var unitPtr = tablePtr.Value + (unitId * UNIT_ENTRY_SIZE);
            var (levelId, levelResult) = await MemoryReader.ReadAsync<int>(unitPtr + UNIT_LEVEL_OFFSET);
            if (!levelResult.Success || levelId == null)
                return (0, levelResult);

            return (levelId.Value, MemoryOperationResult.Succeeded);
        }
        catch (Exception ex)
        {
            return (0, MemoryOperationResult.Failed($"Failed to get unit level: {ex.Message}"));
        }
    }

    /// <summary>
    /// Locates the unit table in memory.
    /// </summary>
    private async Task<(nint Offset, MemoryOperationResult Result)> GetUnitTableOffsetAsync()
    {
        await Task.Yield(); // Allow for potential context switch

        // Check cache first
        if (_cachedOffsets.TryGetValue("UnitTable", out var offset))
            return (offset, MemoryOperationResult.Succeeded);

        // Scan for the unit table pattern
        var (addresses, result) = await ScanForPatternAsync(UnitTablePattern, UnitTableMask);
        if (!result.Success || addresses == null || !addresses.Any())
            return (0, MemoryOperationResult.Failed("Unit table pattern not found"));

        try
        {
            // Calculate the actual offset from the pattern
            var (relativeOffset, readResult) = await MemoryReader.ReadAsync<int>(addresses.First() + 3);
            if (!readResult.Success || relativeOffset == null)
                return (0, readResult);

            // Store in cache and return
            offset = addresses.First() + relativeOffset.Value + 7;
            _cachedOffsets["UnitTable"] = offset;
            return (offset, MemoryOperationResult.Succeeded);
        }
        catch (Exception ex)
        {
            return (0, MemoryOperationResult.Failed($"Failed to get unit table offset: {ex.Message}"));
        }
    }

    /// <summary>
    /// Retrieves current game session information.
    /// Includes difficulty, game name, password, and description.
    /// </summary>
    public async Task<(GameInfo? Info, MemoryOperationResult Result)> GetGameInfoAsync()
    {
        await Task.Yield(); // Allow for potential context switch

        var (offset, offsetResult) = await GetGameInfoOffsetAsync();
        if (!offsetResult.Success)
            return (null, offsetResult);

        try
        {
            // Read game info structure pointer
            var (infoPtr, ptrResult) = await MemoryReader.ReadAsync<nint>(offset);
            if (!ptrResult.Success || infoPtr == null)
                return (null, ptrResult);

            var info = new GameInfo();

            // Read game difficulty
            var (difficultyId, diffResult) = await MemoryReader.ReadAsync<int>(infoPtr.Value + GAME_DIFFICULTY_OFFSET);
            if (diffResult.Success && difficultyId != null)
                info.Difficulty = (Difficulty)difficultyId;

            // Read game name (24 chars max)
            var (gameName, nameResult) = await MemoryReader.ReadStringAsync(infoPtr.Value + GAME_NAME_OFFSET, 24);
            if (nameResult.Success)
                info.GameName = gameName;

            // Read game password (24 chars max)
            var (password, passResult) = await MemoryReader.ReadStringAsync(infoPtr.Value + GAME_PASSWORD_OFFSET, 24);
            if (passResult.Success)
                info.GamePassword = password;

            // Read game description (255 chars max)
            var (description, descResult) = await MemoryReader.ReadStringAsync(infoPtr.Value + GAME_DESCRIPTION_OFFSET, 255);
            if (descResult.Success)
                info.GameDescription = description;

            return (info, MemoryOperationResult.Succeeded);
        }
        catch (Exception ex)
        {
            return (null, MemoryOperationResult.Failed($"Failed to get game info: {ex.Message}"));
        }
    }

    /// <summary>
    /// Locates the game info structure in memory.
    /// </summary>
    private async Task<(nint Offset, MemoryOperationResult Result)> GetGameInfoOffsetAsync()
    {
        await Task.Yield(); // Allow for potential context switch

        // Check cache first
        if (_cachedOffsets.TryGetValue("GameInfo", out var offset))
            return (offset, MemoryOperationResult.Succeeded);

        // Scan for the game info pattern
        var (addresses, result) = await ScanForPatternAsync(GameInfoPattern, GameInfoMask);
        if (!result.Success || addresses == null || !addresses.Any())
            return (0, MemoryOperationResult.Failed("Game info pattern not found"));

        try
        {
            // Calculate the actual offset from the pattern
            var (relativeOffset, readResult) = await MemoryReader.ReadAsync<int>(addresses.First() + 3);
            if (!readResult.Success || relativeOffset == null)
                return (0, readResult);

            // Store in cache and return
            offset = addresses.First() + relativeOffset.Value + 7;
            _cachedOffsets["GameInfo"] = offset;
            return (offset, MemoryOperationResult.Succeeded);
        }
        catch (Exception ex)
        {
            return (0, MemoryOperationResult.Failed($"Failed to get game info offset: {ex.Message}"));
        }
    }
}

/// <summary>
/// Represents current game session information including difficulty level and session details.
/// </summary>
public class GameInfo
{
    public Difficulty Difficulty { get; set; }
    public string? GameName { get; set; }
    public string? GamePassword { get; set; }
    public string? GameDescription { get; set; }
}