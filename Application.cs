using System.CommandLine;
using Marauder.Mini.Commands;
using Marauder.Mini.Models;
using Microsoft.Extensions.Logging;

namespace Marauder.Mini;

public class Application : RootCommand
{
    private readonly EventId Cancel = new();
    private readonly EventId Error = new();
    private readonly EventId Finish = new();
    private readonly CancellationTokenSource _tokenSource;
    private readonly ILogger<Application> _logger;

    public Application(
        RunCommand run,
        TestCommand test,
        CancellationTokenSource tokenSource,
        ILogger<Application> logger) : base("Maurading and such")
    {
        _tokenSource = tokenSource;
        _logger = logger;

        AddCommand(run);
        AddCommand(test);
    }

    public async Task<int> RunAsync(string[] args)
    {
        try
        {
            await this.InvokeAsync(args);
            _logger.LogTrace(Finish, "Finished");
            return 0x00;
        }
        catch(ProcessModuleNotFoundException)
        {
            _logger.LogError(Error, "An unhandled exception was caught in the main thread");
            return 0x01;
        }
        catch(Exception ex)
        {
            _logger.LogError(Error, "An unhandled exception was caught in the main thread");
            return 0x01;
        }
    }

    public async Task<int> CancelAsync()
    {
        _logger.LogTrace(Cancel, "Canceling");
        await _tokenSource.CancelAsync();
        return 0x01;
    }
}