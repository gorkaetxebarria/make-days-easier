using Mde.Tool.Integrations;

namespace Mde.Tool.Tests;

internal sealed class FakeGitClient : IGitClient {
    public Dictionary<string, GitCommandResult<string>> Roots { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, GitCommandResult<string>> Remotes { get; } = new(StringComparer.Ordinal);
    public GitCommandResult<bool> DirtyResult { get; set; } = GitCommandResult<bool>.Succeeded(false);
    public GitCommandResult CloneResult { get; set; } = GitCommandResult.Succeeded();
    public GitCommandResult FetchResult { get; set; } = GitCommandResult.Succeeded();
    public GitCommandResult CheckoutResult { get; set; } = GitCommandResult.Succeeded();
    public List<string> Calls { get; } = [];

    public Task<GitCommandResult<string>> GetRepositoryRootAsync(string workingDirectory, CancellationToken cancellationToken = default) {
        Calls.Add($"root:{workingDirectory}");
        return Task.FromResult(Roots.TryGetValue(workingDirectory, out var result)
            ? result : GitCommandResult<string>.Failed("git-command-failed", "not a repository"));
    }

    public Task<GitCommandResult<string>> GetRemoteUrlAsync(string repositoryPath, string remoteName = "origin", CancellationToken cancellationToken = default) {
        Calls.Add($"remote:{repositoryPath}");
        return Task.FromResult(Remotes[repositoryPath]);
    }

    public Task<GitCommandResult<bool>> IsWorkingTreeDirtyAsync(string repositoryPath, CancellationToken cancellationToken = default) {
        Calls.Add($"dirty:{repositoryPath}");
        return Task.FromResult(DirtyResult);
    }

    public Task<GitCommandResult> CloneIntoDirectoryAsync(string remoteUrl, string destinationDirectory, CancellationToken cancellationToken = default) {
        Calls.Add($"clone:{remoteUrl}:{destinationDirectory}");
        return Task.FromResult(CloneResult);
    }

    public Task<GitCommandResult> FetchAsync(string repositoryPath, string remoteName, string sourceRef, CancellationToken cancellationToken = default) {
        Calls.Add($"fetch:{repositoryPath}:{remoteName}:{sourceRef}");
        return Task.FromResult(FetchResult);
    }

    public Task<GitCommandResult> CheckoutTrackingBranchAsync(string repositoryPath, string remoteName, string sourceRef, string localBranchName, CancellationToken cancellationToken = default) {
        Calls.Add($"checkout:{repositoryPath}:{remoteName}:{sourceRef}:{localBranchName}");
        return Task.FromResult(CheckoutResult);
    }
}
