using System.CommandLine;
using System.Text.Json;
using Mde.Tool.Configuration;
using Mde.Tool.Integrations;
using Mde.Tool.Platform;
using Spectre.Console;

namespace Mde.Tool.Commands;

public sealed class PullRequestCommandModule(
    IAnsiConsole console,
    IAnsiConsole diagnosticConsole,
    IUserConfigurationStore configurationStore,
    IPlatform platform,
    PullRequestWorkspaceService workspaceService,
    IIdeLauncher ideLauncher) : ICommandModule
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private const string CodeIde = "code";
    private const string VisualStudioIde = "vs";
    private const string TextOutput = "text";
    private const string JsonOutput = "json";

    public Command CreateCommand()
    {
        var command = new Command(
            "pr",
            "Prepare an Azure DevOps pull request locally: clone into the current directory if needed, check out its source branch, and open an IDE.");

        var idArgument = new Argument<int>("id")
        {
            Description = "Azure DevOps pull request id."
        };
        idArgument.Validators.Add(result =>
        {
            if (result.GetValueOrDefault<int>() <= 0)
            {
                result.AddError("The pull request id must be a positive integer.");
            }
        });

        var ideOption = new Option<string>("--ide")
        {
            Description = "IDE to open after preparing the repository: 'code' (VS Code) or 'vs' (Visual Studio, Windows only; falls back to VS Code when no .sln/.slnx exists).",
            DefaultValueFactory = _ => CodeIde
        };
        ideOption.AcceptOnlyFromAmong(CodeIde, VisualStudioIde);

        var outputOption = new Option<string>("--output")
        {
            Description = "Output format: 'text' (default, human readable) or 'json' (stdout contains only {\"schemaVersion\":1,\"repositoryPath\":\"...\"}; diagnostics go to stderr).",
            DefaultValueFactory = _ => TextOutput
        };
        outputOption.AcceptOnlyFromAmong(TextOutput, JsonOutput);

        command.Arguments.Add(idArgument);
        command.Options.Add(ideOption);
        command.Options.Add(outputOption);

        command.SetAction(async (parseResult, cancellationToken) =>
            await ExecuteAsync(
                parseResult.GetValue(idArgument),
                parseResult.GetValue(ideOption) ?? CodeIde,
                string.Equals(parseResult.GetValue(outputOption), JsonOutput, StringComparison.Ordinal),
                cancellationToken));

        return command;
    }

    private async Task<int> ExecuteAsync(int id, string ide, bool json, CancellationToken cancellationToken)
    {
        var ui = json ? diagnosticConsole : console;

        try
        {
            if (ide == VisualStudioIde && !platform.IsWindows())
            {
                ui.MarkupLine("[red]--ide vs is only supported on Windows.[/] Use [grey]--ide code[/].");
                return 1;
            }

            var configuration = await configurationStore.LoadAsync(cancellationToken);
            var organization = configuration.AzureDevOpsOrganization;
            if (string.IsNullOrWhiteSpace(organization))
            {
                ui.MarkupLine("[red]No Azure DevOps organization is configured.[/]");
                ui.MarkupLine("Run [grey]mde init --organization <org>[/] first.");
                return 1;
            }

            var result = await workspaceService.PrepareAsync(
                new PullRequestWorkspaceRequest(organization, id),
                cancellationToken);
            if (!result.Success)
            {
                var failure = result.Failure!;
                ui.MarkupLine($"[red]{Markup.Escape(failure.Message)}[/]");
                if (!string.IsNullOrWhiteSpace(failure.Details))
                {
                    ui.MarkupLine(Markup.Escape(failure.Details));
                }

                return 1;
            }

            var repositoryPath = Path.GetFullPath(result.RepositoryPath!);
            var pullRequest = result.PullRequest!;

            WriteSummary(ui, pullRequest, result.CheckedOutBranch!);
            foreach (var warning in result.Warnings)
            {
                ui.MarkupLine($"[yellow]Warning:[/] {Markup.Escape(warning)}");
            }

            await LaunchIdeAsync(ui, ide, repositoryPath, cancellationToken);

            if (json)
            {
                Console.Out.Write(JsonSerializer.Serialize(new PullRequestJsonOutput(1, repositoryPath), JsonOptions));
                Console.Out.WriteLine();
            }
            else
            {
                // Written raw so Spectre never wraps or styles the final path line.
                Console.Out.WriteLine(repositoryPath);
            }

            return 0;
        }
        catch (OperationCanceledException)
        {
            ui.MarkupLine("[yellow]Cancelled.[/]");
            return 1;
        }
        catch (Exception exception)
        {
            ui.MarkupLine($"[red]Unexpected error: {Markup.Escape(exception.Message)}[/]");
            return 1;
        }
    }

    private async Task LaunchIdeAsync(IAnsiConsole ui, string ide, string repositoryPath, CancellationToken cancellationToken)
    {
        var request = new IdeLaunchRequest(repositoryPath);
        var label = "VS Code";

        if (ide == VisualStudioIde)
        {
            var solution = FindSolution(repositoryPath);
            if (solution is null)
            {
                ui.MarkupLine("[yellow]No .sln or .slnx file found in the repository; falling back to VS Code.[/]");
            }
            else
            {
                request = new IdeLaunchRequest(repositoryPath, PreferredIde.VisualStudio, solution);
                label = "Visual Studio";
            }
        }

        IdeLaunchResult launch;
        try
        {
            launch = await ideLauncher.LaunchAsync(request, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            launch = IdeLaunchResult.Warn(exception.Message);
        }

        if (!launch.Launched)
        {
            ui.MarkupLine($"[yellow]Warning:[/] could not open {label}: {Markup.Escape(launch.Warning ?? "unknown error")}. The repository is ready.");
        }
    }

    // Looks in the repository root, then one level down.
    private static string? FindSolution(string repositoryPath)
    {
        try
        {
            foreach (var directory in new[] { repositoryPath }.Concat(Directory.EnumerateDirectories(repositoryPath).Where(d => !Path.GetFileName(d).StartsWith('.'))))
            {
                var match = Directory.EnumerateFiles(directory)
                    .Where(f => f.EndsWith(".sln", StringComparison.OrdinalIgnoreCase)
                        || f.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase))
                    .Order(StringComparer.OrdinalIgnoreCase)
                    .FirstOrDefault();
                if (match is not null)
                {
                    return match;
                }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }

        return null;
    }

    private static void WriteSummary(IAnsiConsole ui, AdoPullRequestDetails pullRequest, string branch)
    {
        ui.MarkupLine($"[bold]PR #{pullRequest.PullRequestId}:[/] {Markup.Escape(pullRequest.Title)}");
        ui.MarkupLine($"Author: {Markup.Escape(pullRequest.AuthorDisplayName)}");
        ui.MarkupLine($"Target branch: {Markup.Escape(pullRequest.TargetBranch)}");
        ui.MarkupLine($"Checked out: {Markup.Escape(branch)}");
        ui.MarkupLine($"Changed files ({pullRequest.ChangedFiles.Count}):");
        foreach (var file in pullRequest.ChangedFiles)
        {
            ui.MarkupLine($"  [grey]{Markup.Escape(file)}[/]");
        }
    }

    private sealed record PullRequestJsonOutput(int SchemaVersion, string RepositoryPath);
}
