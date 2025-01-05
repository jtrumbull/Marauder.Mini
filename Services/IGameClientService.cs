using Marauder.Mini.Models;

namespace Marauder.Mini.Services;

public interface IGameClientService
{
    // public event EventHandler Exited;

    Task<GameClient> StartAsync(CancellationToken token);
    Task StopAsync();
}