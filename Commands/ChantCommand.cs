using System.CommandLine;
using Microsoft.Extensions.Logging;

namespace Marauder.Mini.Commands;

public class ChantCommand : Command
{
    public ChantCommand(ILogger<ChantCommand> logger) : base("chant", "Run a chant game")
    {
        this.SetHandler(() =>
        {
            logger.LogError("Chant command not implemented");
        });
    }
}