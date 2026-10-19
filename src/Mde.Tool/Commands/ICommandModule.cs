using System.CommandLine;

namespace Mde.Tool.Commands;

public interface ICommandModule
{
    Command CreateCommand();
}
