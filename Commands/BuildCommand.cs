using System.CommandLine;
using Marauder.Mini.Commands.Build;

namespace Marauder.Mini.Commands;

public class BuildCommand : Command
{
    public BuildCommand(BuildEnumsCommand buildEnums) : base("build", "Build scripts")
    {
        AddCommand(buildEnums);
    }
}