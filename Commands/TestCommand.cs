using System.CommandLine;
using System.Diagnostics;
using Marauder.Mini.Enums;
using Marauder.Mini.Models;
using Marauder.Mini.Services;
using Microsoft.Extensions.Logging;

namespace Marauder.Mini.Commands;

public class TestCommand : Command
{
    public TestCommand(
        IGameClientService gameClientService,
        ILogger<TestCommand> logger) : base("test", "Display debug information")
    {
        this.SetHandler(async () => {

            try
            {
                logger.LogInformation("Running test command");

                using var cts = new CancellationTokenSource();
                using var client = await gameClientService.StartAsync(cts.Token);

                logger.LogDebug($"Process-> ID: 0x{client.GetProcessId().ToString("X")}");
                logger.LogDebug($"Process-> Handle: 0x{client.GetProcessHandle().ToString("X")}");
                logger.LogDebug($"Module-> Name: {client.GetModuleName()}");
                logger.LogDebug($"Module-> Base address: 0x{client.GetBaseAddress().ToString("X")}");
                logger.LogDebug($"Module-> Memory size: {client.GetModuleMemorySize()}");

                var screens = Screen.AllScreens;

                // Find and report offsets

                var gameReader = new GameReader(client.GetProcessHandle(), client.GetBaseAddress(), client.GetModuleMemorySize());

                gameReader.Memory.CalculateOffsets();

                Logger.Stat("Offsets-> GameData", $"0x{gameReader.Memory.Offsets.GameData?.ToString("X")}");
                Logger.Stat("Offsets-> UnitTable", $"0x{gameReader.Memory.Offsets.UnitTable?.ToString("X")}");
                Logger.Stat("Offsets-> UI", $"0x{gameReader.Memory.Offsets.UI?.ToString("X")}");
                Logger.Stat("Offsets-> Hover", $"0x{gameReader.Memory.Offsets.Hover?.ToString("X")}");
                Logger.Stat("Offsets-> Expansion", $"0x{gameReader.Memory.Offsets.Expansion?.ToString("X")}");

                // Read player units (only works if in game)

                var playerUnits = gameReader.ReadPlayerUnits();

                var player = playerUnits.Where(e => e.IsMainPlayer).FirstOrDefault();

                if (player != null)
                {
                    Console.WriteLine("Player unit:");
                    Logger.Stat("  Unit-> Id", player.Id);
                    Logger.Stat("  Unit-> Name", player.Name);
                    Logger.Stat("  Unit-> Address", $"0x{player.Address}");
                    Logger.Stat("  Unit-> IsMainPlayer", player.IsMainPlayer);
                    Logger.Stat("  Unit-> IsCorpse", player.IsCorpse);
                    Logger.Stat("  Unit-> AreaId", $"{player.AreaId} ({(Area)player.AreaId})");
                    Logger.Stat("  Unit-> Position", $"{player.Position.X}, {player.Position.Y}");
                }
                else
                {
                    throw new PlayerNotFoundException();
                }
            }
            catch(ProcessNotFoundException ex)
            {
                logger.LogError(ex, $"{ex.Message}. Make sure the game is running");
            }
            catch(ProcessModuleNotFoundException ex)
            {
                logger.LogError(ex, $"{ex.Message}. Make sure the game is running");
            }
            catch(PlayerNotFoundException)
            {
                logger.LogError("Player unit not found. Make sure the selected hero is in a game");
            }
            catch(Exception ex)
            {
                logger.LogError(ex, $"An unhandled error was caught: {ex}");
                throw;
            }
        });
    }

    public class PlayerNotFoundException() : Exception();
}