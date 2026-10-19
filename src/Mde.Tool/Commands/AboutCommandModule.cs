using System.CommandLine;
using Mde.Tool.Configuration;
using Spectre.Console;

namespace Mde.Tool.Commands;

public sealed class AboutCommandModule(
    IAnsiConsole console,
    IUserConfigurationStore configurationStore) : ICommandModule
{
    public Command CreateCommand()
    {
        var command = new Command("about", "Show the current mde configuration.");

        command.SetAction(async (_, cancellationToken) =>
        {
            var configuration = await configurationStore.LoadAsync(cancellationToken);
            console.MarkupLine("[bold]mde[/] is ready.");
            console.MarkupLine($"Azure DevOps organization: [grey]{Markup.Escape(configuration.AzureDevOpsOrganization ?? "not configured")}[/]");
            console.MarkupLine($"Preferred IDE: [grey]{Markup.Escape(configuration.PreferredIde?.ToString() ?? "not configured")}[/]");
        });

        return command;
    }
}
