using Marauder.Mini.Models;
using Microsoft.Extensions.Logging;

namespace Marauder.Mini.Services;

public interface IGameClientService
{
    Task<GameClient> Start(CancellationToken token);
}

public class GameClientService(ILogger<GameClientService> logger) : IGameClientService
{
    public event EventHandler Resized = default!;
    public event EventHandler Minimized = default!;
    public event EventHandler Restored = default!;
    public event EventHandler Exited = default!;

    private bool _running;
    private Thread _thread = default!;
    private GameClient _client = default!;
    private readonly ILogger<GameClientService> _logger = logger;
    private Mutex _mutex = new();

    /// <summary>
    /// Start game client
    /// </summary>
    /// <param name="token"></param>
    /// <returns></returns>
    public async Task<GameClient> Start(CancellationToken token)
    {
        if (_running) return _client;

        var client = await CreateGameClientAsync();
        var thread = new Thread(() => Worker(token));

        _logger.LogInformation("Starting the game client thread");
        
        thread.Start();

        _client = client;
        _thread = thread;

        return client;
    }

    private static async Task<GameClient> CreateGameClientAsync()
    {
        var client = new GameClient();
        client.OpenProcess();
        await client.MoveToPrimaryScreen();
        return client;
    }

    async void Worker(CancellationToken token)
    {
        _mutex.WaitOne();

        try
        {
            var index = 0;
            var running = _running = true;
            
            while(running)
            {
                token.ThrowIfCancellationRequested();
                // Thread work here

                await _client.UpdateAsync();
                // CheckForExit()
                // CheckForResize()
                // CheckForMinimized()
                // CheckForResize()

                Thread.Sleep(100);
                index++;
                if (!_running || token.IsCancellationRequested || index == 10)
                    running = false;
            }

            _logger.LogInformation("Game client thread has exited");
        }
        finally
        {

        }
    }
}