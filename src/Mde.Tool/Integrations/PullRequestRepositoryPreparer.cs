namespace Mde.Tool.Integrations;

public sealed class PullRequestRepositoryPreparer(
    IGitClient gitClient,
    IFileSystem fileSystem)
{
    public async Task<RepositoryPreparationResult> PrepareAsync(
        RepositoryPreparationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!AdoRemoteNormalizer.TryNormalize(request.CloneUrl, out _))
        {
            return RepositoryPreparationResult.Failed(
                "ado-remote-invalid",
                "Pull request clone URL is not a supported Azure DevOps remote URL.");
        }

        var currentDirectory = fileSystem.GetCurrentDirectory();
        request.Log?.Invoke($"Looking for a local clone in '{currentDirectory}'...");

        var currentDirectoryMatch = await TryMatchRepositoryAsync(
            currentDirectory,
            request.CloneUrl,
            cancellationToken);
        if (!currentDirectoryMatch.Success)
        {
            return RepositoryPreparationResult.Failed(
                currentDirectoryMatch.Failure!.Code,
                currentDirectoryMatch.Failure.Message,
                currentDirectoryMatch.Failure.Details);
        }

        var repositoryPath = currentDirectoryMatch.Value;
        var sameNamedFolderPath = fileSystem.Combine(currentDirectory, request.RepositoryName);
        if (repositoryPath is null && fileSystem.DirectoryExists(sameNamedFolderPath))
        {
            var subfolderMatch = await TryMatchRepositoryAsync(
                sameNamedFolderPath,
                request.CloneUrl,
                cancellationToken);
            if (!subfolderMatch.Success)
            {
                return RepositoryPreparationResult.Failed(
                    subfolderMatch.Failure!.Code,
                    subfolderMatch.Failure.Message,
                    subfolderMatch.Failure.Details);
            }

            if (subfolderMatch.Value is null)
            {
                return RepositoryPreparationResult.Failed(
                    "repo-folder-collision",
                    $"Existing folder '{request.RepositoryName}' does not match the expected Azure DevOps remote.",
                    sameNamedFolderPath);
            }

            repositoryPath = subfolderMatch.Value;
        }

        if (repositoryPath is not null)
        {
            request.Log?.Invoke($"Using existing clone at '{repositoryPath}'.");
        }

        if (repositoryPath is null)
        {
            request.Log?.Invoke($"No local clone found; cloning into '{currentDirectory}'...");
            var cloneResult = await gitClient.CloneIntoDirectoryAsync(
                request.CloneUrl,
                currentDirectory,
                cancellationToken);
            if (!cloneResult.Success)
            {
                return RepositoryPreparationResult.Failed(
                    cloneResult.Failure!.Code,
                    "Failed to clone the pull request repository into the current directory.",
                    cloneResult.Failure.Details);
            }

            repositoryPath = currentDirectory;
        }

        request.Log?.Invoke("Checking the working tree for uncommitted changes...");
        var dirtyCheck = await gitClient.IsWorkingTreeDirtyAsync(repositoryPath, cancellationToken);
        if (!dirtyCheck.Success)
        {
            return RepositoryPreparationResult.Failed(
                dirtyCheck.Failure!.Code,
                dirtyCheck.Failure.Message,
                dirtyCheck.Failure.Details);
        }

        if (dirtyCheck.Value)
        {
            if (request.ConfirmStashAsync is null || !await request.ConfirmStashAsync(cancellationToken))
            {
                return RepositoryPreparationResult.Failed(
                    "repo-dirty-worktree",
                    "Repository has uncommitted changes. Commit or stash them before running this command.");
            }

            request.Log?.Invoke("Stashing uncommitted changes...");
            var stashResult = await gitClient.StashAsync(repositoryPath, cancellationToken);
            if (!stashResult.Success)
            {
                return RepositoryPreparationResult.Failed(
                    stashResult.Failure!.Code,
                    "Failed to stash the uncommitted changes.",
                    stashResult.Failure.Details);
            }
        }

        request.Log?.Invoke($"Fetching {request.SourceRef} from origin...");
        var fetchResult = await gitClient.FetchAsync(
            repositoryPath,
            "origin",
            request.SourceRef,
            cancellationToken);
        if (!fetchResult.Success)
        {
            return RepositoryPreparationResult.Failed(
                fetchResult.Failure!.Code,
                "Failed to fetch the pull request source branch.",
                fetchResult.Failure.Details);
        }

        var localBranchName = ToLocalBranchName(request.SourceRef);
        request.Log?.Invoke($"Checking out '{localBranchName}'...");
        var checkoutResult = await gitClient.CheckoutTrackingBranchAsync(
            repositoryPath,
            "origin",
            request.SourceRef,
            localBranchName,
            cancellationToken);
        if (!checkoutResult.Success)
        {
            return RepositoryPreparationResult.Failed(
                checkoutResult.Failure!.Code,
                "Failed to switch to the pull request source branch.",
                checkoutResult.Failure.Details);
        }

        return RepositoryPreparationResult.Succeeded(repositoryPath, localBranchName);
    }

    private async Task<GitCommandResult<string?>> TryMatchRepositoryAsync(
        string directoryPath,
        string expectedRemoteUrl,
        CancellationToken cancellationToken)
    {
        var rootResult = await gitClient.GetRepositoryRootAsync(directoryPath, cancellationToken);
        if (!rootResult.Success)
        {
            if (rootResult.Failure?.Code == "git-command-failed")
            {
                return GitCommandResult<string?>.Succeeded(null);
            }

            return GitCommandResult<string?>.Failed(
                rootResult.Failure!.Code,
                rootResult.Failure.Message,
                rootResult.Failure.Details);
        }

        var remoteResult = await gitClient.GetRemoteUrlAsync(rootResult.Value!, cancellationToken: cancellationToken);
        if (!remoteResult.Success)
        {
            return GitCommandResult<string?>.Failed(
                remoteResult.Failure!.Code,
                remoteResult.Failure.Message,
                remoteResult.Failure.Details);
        }

        return AdoRemoteNormalizer.AreEquivalent(expectedRemoteUrl, remoteResult.Value!)
            ? GitCommandResult<string?>.Succeeded(rootResult.Value!)
            : GitCommandResult<string?>.Succeeded(null);
    }

    private static string ToLocalBranchName(string sourceRef) =>
        sourceRef.StartsWith("refs/heads/", StringComparison.OrdinalIgnoreCase)
            ? sourceRef["refs/heads/".Length..]
            : sourceRef;
}
