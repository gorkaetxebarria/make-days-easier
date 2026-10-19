namespace Mde.Tool.Integrations;

public sealed class GitClient(IProcessLauncher processLauncher) : IGitClient
{
    public async Task<GitCommandResult<string>> GetRepositoryRootAsync(
        string workingDirectory,
        CancellationToken cancellationToken = default)
    {
        var result = await RunGitAsync(
            ["-C", workingDirectory, "rev-parse", "--show-toplevel"],
            workingDirectory,
            cancellationToken);

        if (!result.Success)
        {
            return GitCommandResult<string>.Failed(
                result.Failure!.Code,
                result.Failure.Message,
                result.Failure.Details);
        }

        return GitCommandResult<string>.Succeeded(result.Value!);
    }

    public async Task<GitCommandResult<string>> GetRemoteUrlAsync(
        string repositoryPath,
        string remoteName = "origin",
        CancellationToken cancellationToken = default)
    {
        var result = await RunGitAsync(
            ["-C", repositoryPath, "remote", "get-url", remoteName],
            repositoryPath,
            cancellationToken);

        if (!result.Success)
        {
            return GitCommandResult<string>.Failed(
                result.Failure!.Code,
                result.Failure.Message,
                result.Failure.Details);
        }

        return GitCommandResult<string>.Succeeded(result.Value!);
    }

    public async Task<GitCommandResult<bool>> IsWorkingTreeDirtyAsync(
        string repositoryPath,
        CancellationToken cancellationToken = default)
    {
        var result = await RunGitAsync(
            ["-C", repositoryPath, "status", "--porcelain", "--untracked-files=normal"],
            repositoryPath,
            cancellationToken);

        if (!result.Success)
        {
            return GitCommandResult<bool>.Failed(
                result.Failure!.Code,
                result.Failure.Message,
                result.Failure.Details);
        }

        return GitCommandResult<bool>.Succeeded(!string.IsNullOrWhiteSpace(result.Value));
    }

    public async Task<GitCommandResult> CloneIntoDirectoryAsync(
        string remoteUrl,
        string destinationDirectory,
        CancellationToken cancellationToken = default)
    {
        var result = await RunGitAsync(
            ["clone", remoteUrl, "."],
            destinationDirectory,
            cancellationToken);

        return result.Success
            ? GitCommandResult.Succeeded()
            : GitCommandResult.Failed(
                result.Failure!.Code,
                result.Failure.Message,
                result.Failure.Details);
    }

    public async Task<GitCommandResult> FetchAsync(
        string repositoryPath,
        string remoteName,
        string sourceRef,
        CancellationToken cancellationToken = default)
    {
        var result = await RunGitAsync(
            ["-C", repositoryPath, "fetch", remoteName, sourceRef],
            repositoryPath,
            cancellationToken);

        return result.Success
            ? GitCommandResult.Succeeded()
            : GitCommandResult.Failed(
                result.Failure!.Code,
                result.Failure.Message,
                result.Failure.Details);
    }

    public async Task<GitCommandResult> CheckoutTrackingBranchAsync(
        string repositoryPath,
        string remoteName,
        string sourceRef,
        string localBranchName,
        CancellationToken cancellationToken = default)
    {
        var remoteBranchName = ToRemoteBranchName(sourceRef);

        var branchExistsResult = await RunGitExitCodeOnlyAsync(
            ["-C", repositoryPath, "show-ref", "--verify", "--quiet", $"refs/heads/{localBranchName}"],
            repositoryPath,
            cancellationToken);
        if (!branchExistsResult.Success)
        {
            return GitCommandResult.Failed(
                branchExistsResult.Failure!.Code,
                branchExistsResult.Failure.Message,
                branchExistsResult.Failure.Details);
        }

        var checkoutArguments = branchExistsResult.Value == 0
            ? new[] { "-C", repositoryPath, "checkout", localBranchName }
            : new[] { "-C", repositoryPath, "checkout", "--track", "-b", localBranchName, $"{remoteName}/{remoteBranchName}" };

        var checkoutResult = await RunGitAsync(
            checkoutArguments,
            repositoryPath,
            cancellationToken);

        return checkoutResult.Success
            ? GitCommandResult.Succeeded()
            : GitCommandResult.Failed(
                checkoutResult.Failure!.Code,
                checkoutResult.Failure.Message,
                checkoutResult.Failure.Details);
    }

    private async Task<GitCommandResult<string>> RunGitAsync(
        IReadOnlyList<string> arguments,
        string workingDirectory,
        CancellationToken cancellationToken)
    {
        ProcessLaunchResult result;
        try
        {
            result = await processLauncher.RunAsync(
                new ProcessLaunchRequest(
                    "git",
                    arguments,
                    workingDirectory),
                cancellationToken);
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return GitCommandResult<string>.Failed(
                "git-launch-failed",
                "Unable to execute git.",
                exception.Message);
        }

        if (result.ExitCode != 0)
        {
            return GitCommandResult<string>.Failed(
                "git-command-failed",
                $"Git command failed with exit code {result.ExitCode}.",
                string.IsNullOrWhiteSpace(result.StandardError)
                    ? result.StandardOutput.Trim()
                    : result.StandardError.Trim());
        }

        return GitCommandResult<string>.Succeeded(result.StandardOutput.Trim());
    }

    private async Task<GitCommandResult<int>> RunGitExitCodeOnlyAsync(
        IReadOnlyList<string> arguments,
        string workingDirectory,
        CancellationToken cancellationToken)
    {
        ProcessLaunchResult result;
        try
        {
            result = await processLauncher.RunAsync(
                new ProcessLaunchRequest(
                    "git",
                    arguments,
                    workingDirectory),
                cancellationToken);
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return GitCommandResult<int>.Failed(
                "git-launch-failed",
                "Unable to execute git.",
                exception.Message);
        }

        return result.ExitCode switch
        {
            0 => GitCommandResult<int>.Succeeded(0),
            1 => GitCommandResult<int>.Succeeded(1),
            _ => GitCommandResult<int>.Failed(
                "git-command-failed",
                $"Git command failed with exit code {result.ExitCode}.",
                string.IsNullOrWhiteSpace(result.StandardError)
                    ? result.StandardOutput.Trim()
                    : result.StandardError.Trim())
        };
    }

    private static string ToRemoteBranchName(string sourceRef) =>
        sourceRef.StartsWith("refs/heads/", StringComparison.OrdinalIgnoreCase)
            ? sourceRef["refs/heads/".Length..]
            : sourceRef;
}
