using System.CommandLine;
using Mde.Tool.Configuration;
using Mde.Tool.Platform;
using Spectre.Console;

namespace Mde.Tool.Commands;

public sealed class InitCommandModule(
    IAnsiConsole console,
    IPlatform platform,
    IUserConfigurationStore configurationStore) : ICommandModule
{
    public Command CreateCommand()
    {
        var command = new Command("init", "Check local Azure CLI readiness and optionally save the default Azure DevOps organization and preferred IDE.");
        var organizationOption = new Option<string?>("--organization")
        {
            Description = "Default Azure DevOps organization name (not a URL) used by 'mde pr'. Stored as a non-secret setting."
        };
        var ideOption = new Option<string?>("--ide")
        {
            Description = "Preferred IDE to save: code, insiders, vs, rider, or none to clear it. When omitted in an interactive terminal, init asks; otherwise the saved value is left unchanged."
        };
        ideOption.AcceptOnlyFromAmong(IdeChoices.Keys.ToArray());
        command.Options.Add(organizationOption);
        command.Options.Add(ideOption);

        command.SetAction(async (parseResult, cancellationToken) =>
            await ExecuteAsync(parseResult.GetValue(organizationOption), parseResult.GetValue(ideOption), cancellationToken));

        return command;
    }

    private async Task<int> ExecuteAsync(string? organization, string? ide, CancellationToken cancellationToken)
    {
        if (organization is not null)
        {
            var normalized = organization.Trim();
            if (!OrganizationName.IsValid(normalized))
            {
                console.MarkupLine("[red]Invalid organization name.[/]");
                console.MarkupLine("Use only the organization name (letters, digits, '.', '_' or '-'), not a URL or token.");
                return 1;
            }

            var configuration = await configurationStore.LoadAsync(cancellationToken);
            await configurationStore.SaveAsync(configuration with { AzureDevOpsOrganization = normalized }, cancellationToken);
            console.MarkupLine($"Default Azure DevOps organization saved: [grey]{Markup.Escape(normalized)}[/]");
        }

        await SavePreferredIdeAsync(ide, cancellationToken);

        var cliAvailability = await TryRunAzureCliCommandAsync(
            ["version", "--output", "none", "--only-show-errors"],
            "checking Azure CLI availability",
            "Install Azure CLI, then run 'mde init' again.",
            cancellationToken);
        if (!cliAvailability.Success)
        {
            WriteGitCredentialManagerNotice();
            return 1;
        }

        var signedInStatus = await TryRunAzureCliCommandAsync(
            ["account", "show", "--output", "none", "--only-show-errors"],
            "checking Azure CLI sign-in readiness",
            "Run 'az login' in an interactive terminal, then run 'mde init' again.",
            cancellationToken);
        if (signedInStatus.Success)
        {
            console.MarkupLine("[green]Azure CLI is available and signed in.[/]");
            WriteGitCredentialManagerNotice();
            return 0;
        }

        if (!platform.IsInteractiveTerminal())
        {
            console.MarkupLine("[yellow]Azure CLI sign-in is required, but this session is not interactive.[/]");
            console.MarkupLine("Open a local terminal and run [grey]az login[/], then rerun [grey]mde init[/].");
            WriteGitCredentialManagerNotice();
            return 1;
        }

        var shouldStartLogin = console.Confirm(
            "Azure CLI sign-in is required. Start [grey]az login[/] now?",
            false);

        if (!shouldStartLogin)
        {
            console.MarkupLine("[yellow]Initialization not completed.[/]");
            console.MarkupLine("Sign in later with [grey]az login[/], then rerun [grey]mde init[/].");
            WriteGitCredentialManagerNotice();
            return 1;
        }

        var login = await TryRunAzureCliCommandAsync(
            ["login"],
            "starting Azure CLI sign-in",
            "Retry [grey]az login[/] in your terminal and then rerun [grey]mde init[/].",
            cancellationToken,
            redirectOutput: false,
            redirectError: false);

        if (!login.Success)
        {
            WriteGitCredentialManagerNotice();
            return 1;
        }

        console.MarkupLine("[green]Azure CLI sign-in completed. Initialization succeeded.[/]");
        WriteGitCredentialManagerNotice();
        return 0;
    }

    private static readonly Dictionary<string, PreferredIde?> IdeChoices = new()
    {
        ["code"] = PreferredIde.VisualStudioCode,
        ["insiders"] = PreferredIde.VisualStudioCodeInsiders,
        ["vs"] = PreferredIde.VisualStudio,
        ["rider"] = PreferredIde.Rider,
        ["none"] = null
    };

    private const string SkipChoice = "Other / skip";

    private async Task SavePreferredIdeAsync(string? ide, CancellationToken cancellationToken)
    {
        if (ide is null)
        {
            if (!platform.IsInteractiveTerminal())
            {
                return;
            }

            var labels = new Dictionary<string, string>
            {
                ["Visual Studio Code"] = "code",
                ["Visual Studio Code Insiders"] = "insiders",
                ["Visual Studio"] = "vs",
                ["JetBrains Rider"] = "rider"
            };
            var selected = console.Prompt(
                new SelectionPrompt<string>()
                    .Title("Which IDE do you prefer? (saved for later workflows)")
                    .AddChoices(labels.Keys.Append(SkipChoice)));
            if (!labels.TryGetValue(selected, out ide))
            {
                console.MarkupLine("[grey]Preferred IDE left unchanged.[/]");
                return;
            }
        }

        var preferred = IdeChoices[ide];
        var configuration = await configurationStore.LoadAsync(cancellationToken);
        await configurationStore.SaveAsync(configuration with { PreferredIde = preferred }, cancellationToken);
        console.MarkupLine(preferred is null
            ? "Preferred IDE cleared."
            : $"Preferred IDE saved: [grey]{Markup.Escape(preferred.Value.ToString())}[/]");
    }

    private async Task<CommandExecutionResult> TryRunAzureCliCommandAsync(
        IReadOnlyList<string> arguments,
        string operationDescription,
        string remediation,
        CancellationToken cancellationToken,
        bool redirectOutput = true,
        bool redirectError = true)
    {
        try
        {
            var result = await platform.RunProcessAsync(
                new PlatformProcessStartInfo(
                    "az",
                    arguments,
                    RedirectStandardOutput: redirectOutput,
                    RedirectStandardError: redirectError),
                cancellationToken);

            if (result.ExitCode == 0)
            {
                return CommandExecutionResult.Succeeded;
            }

            console.MarkupLine($"[red]Failed while {Markup.Escape(operationDescription)} (exit code {result.ExitCode}).[/]");
            console.MarkupLine(Markup.Escape(remediation));
            return CommandExecutionResult.Failed;
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            console.MarkupLine($"[red]Failed while {Markup.Escape(operationDescription)}.[/]");
            console.MarkupLine(Markup.Escape(remediation));
            return CommandExecutionResult.Failed;
        }
    }

    private void WriteGitCredentialManagerNotice()
    {
        console.MarkupLine("[grey]Note: Git authentication is managed separately by Git Credential Manager.[/]");
    }

    private readonly record struct CommandExecutionResult(bool Success)
    {
        public static readonly CommandExecutionResult Succeeded = new(true);
        public static readonly CommandExecutionResult Failed = new(false);
    }
}
