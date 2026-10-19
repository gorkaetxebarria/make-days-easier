using System.CommandLine;

namespace Mde.Tool.Commands;

public sealed class CommandApplication
{
    private readonly IReadOnlyCollection<ICommandModule> modules;

    public CommandApplication(IEnumerable<ICommandModule> modules)
    {
        this.modules = modules.ToArray();
    }

    public Task<int> InvokeAsync(string[] arguments)
    {
        var rootCommand = new RootCommand("Make daily developer workflows easier.");

        foreach (var module in modules)
        {
            rootCommand.Subcommands.Add(module.CreateCommand());
        }

        return rootCommand.Parse(arguments).InvokeAsync();
    }
}
