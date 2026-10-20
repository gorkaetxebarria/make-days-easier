namespace Mde.Tool.Integrations;

public interface IGitClient
{
    Task<GitCommandResult<string>> GetRepositoryRootAsync(
        string workingDirectory,
        CancellationToken cancellationToken = default);

    Task<GitCommandResult<string>> GetRemoteUrlAsync(
        string repositoryPath,
        string remoteName = "origin",
        CancellationToken cancellationToken = default);

    Task<GitCommandResult<bool>> IsWorkingTreeDirtyAsync(
        string repositoryPath,
        CancellationToken cancellationToken = default);

    Task<GitCommandResult> StashAsync(
        string repositoryPath,
        CancellationToken cancellationToken = default);

    Task<GitCommandResult> CloneIntoDirectoryAsync(
        string remoteUrl,
        string destinationDirectory,
        CancellationToken cancellationToken = default);

    Task<GitCommandResult> FetchAsync(
        string repositoryPath,
        string remoteName,
        string sourceRef,
        CancellationToken cancellationToken = default);

    Task<GitCommandResult> CheckoutTrackingBranchAsync(
        string repositoryPath,
        string remoteName,
        string sourceRef,
        string localBranchName,
        CancellationToken cancellationToken = default);
}
