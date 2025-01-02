using System.CommandLine;

namespace Marauder.Mini.Commands;

public class RunCommand : Command
{
    public RunCommand(ChantCommand chant) : base("run", "Run a bot command")
    {
        AddCommand(chant);
    }
}