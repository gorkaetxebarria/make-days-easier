using Mde.Tool.Configuration;
using Mde.Tool.Platform;

namespace Mde.Tool.Integrations;

public sealed class IdeLauncher(
    IProcessLauncher processLauncher,
    IPlatform platform) : IIdeLauncher
{
    public async Task<IdeLaunchResult> LaunchAsync(
        IdeLaunchRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.PreferredIde == PreferredIde.VisualStudio)
        {
            if (!platform.IsWindows())
            {
                return IdeLaunchResult.Warn("Visual Studio launch is only supported on Windows.");
            }

            if (string.IsNullOrWhiteSpace(request.SolutionPath))
            {
                return IdeLaunchResult.Warn("Visual Studio launch requires a solution path.");
            }

            return await TryLaunchAsync(
                "devenv",
                [request.SolutionPath],
                request.WorkingDirectory,
                "visual-studio-launch-failed",
                cancellationToken);
        }

        return await TryLaunchAsync(
            request.PreferredIde == PreferredIde.VisualStudioCodeInsiders ? "code-insiders" : "code",
            [request.WorkingDirectory],
            request.WorkingDirectory,
            "vscode-launch-failed",
            cancellationToken);
    }

    private async Task<IdeLaunchResult> TryLaunchAsync(
        string executable,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        string warningCode,
        CancellationToken cancellationToken)
    {
        ProcessLaunchResult result;
        try
        {
            result = await processLauncher.RunAsync(
                new ProcessLaunchRequest(
                    executable,
                    arguments,
                    workingDirectory),
                cancellationToken);
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return IdeLaunchResult.Warn($"{warningCode}: {exception.Message}");
        }

        if (result.ExitCode != 0)
        {
            var details = string.IsNullOrWhiteSpace(result.StandardError)
                ? result.StandardOutput.Trim()
                : result.StandardError.Trim();
            return IdeLaunchResult.Warn($"{warningCode}: exit code {result.ExitCode}{(string.IsNullOrWhiteSpace(details) ? string.Empty : $" ({details})")}");
        }

        return IdeLaunchResult.Success();
    }
}
