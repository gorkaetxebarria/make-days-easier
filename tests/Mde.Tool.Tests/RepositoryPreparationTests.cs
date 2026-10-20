using Mde.Tool.Integrations;

namespace Mde.Tool.Tests;

public sealed class RepositoryPreparationTests {
    private const string CloneUrl = "https://dev.azure.com/contoso/project/_git/repo";
    private const string Cwd = "/work";
    private static readonly string Repository = Path.Combine(Cwd, "repo");

    [Theory]
    [InlineData("https://dev.azure.com/CONTOSO/project/_git/repo")]
    [InlineData("ssh://git@ssh.dev.azure.com/v3/contoso/project/repo")]
    [InlineData("git@ssh.dev.azure.com:v3/contoso/project/repo")]
    public async Task Existing_repository_is_found_for_equivalent_HTTPS_SSH_and_scp_remotes(string remote) {
        var git = LocalRepository(remote);
        var result = await new PullRequestRepositoryPreparer(git, new FakeFileSystem(Cwd))
            .PrepareAsync(new RepositoryPreparationRequest("repo", CloneUrl, "refs/heads/feature/work"));

        Assert.True(result.Success);
        Assert.Equal(Cwd, result.RepositoryPath);
        Assert.Equal("feature/work", result.CheckedOutBranch);
        Assert.DoesNotContain(git.Calls, call => call.StartsWith("clone:", StringComparison.Ordinal));
        Assert.Contains($"fetch:{Cwd}:origin:refs/heads/feature/work", git.Calls);
        Assert.Contains($"checkout:{Cwd}:origin:refs/heads/feature/work:feature/work", git.Calls);
    }

    [Fact]
    public async Task Missing_repository_clones_only_into_current_directory_then_fetches_and_tracks_source_branch() {
        var git = new FakeGitClient();
        var result = await new PullRequestRepositoryPreparer(git, new FakeFileSystem(Cwd))
            .PrepareAsync(new RepositoryPreparationRequest("repo", CloneUrl, "refs/heads/feature/work"));

        Assert.True(result.Success);
        Assert.Equal(Cwd, result.RepositoryPath);
        Assert.Contains($"clone:{CloneUrl}:{Cwd}", git.Calls);
        Assert.Contains($"dirty:{Cwd}", git.Calls);
        Assert.Contains($"fetch:{Cwd}:origin:refs/heads/feature/work", git.Calls);
        Assert.Contains($"checkout:{Cwd}:origin:refs/heads/feature/work:feature/work", git.Calls);
    }

    [Fact]
    public async Task Same_named_folder_with_non_matching_remote_fails_without_mutating_it() {
        var git = new FakeGitClient();
        git.Roots[Repository] = GitCommandResult<string>.Succeeded(Repository);
        git.Remotes[Repository] = GitCommandResult<string>.Succeeded("https://dev.azure.com/contoso/other/_git/repo");
        var result = await new PullRequestRepositoryPreparer(git, new FakeFileSystem(Cwd, Repository))
            .PrepareAsync(new RepositoryPreparationRequest("repo", CloneUrl, "refs/heads/feature/work"));

        Assert.False(result.Success);
        Assert.Equal("repo-folder-collision", result.Failure!.Code);
        Assert.Equal(Repository, result.Failure.Details);
        Assert.DoesNotContain(git.Calls, call => call.StartsWith("clone:", StringComparison.Ordinal)
                                           || call.StartsWith("dirty:", StringComparison.Ordinal)
                                           || call.StartsWith("fetch:", StringComparison.Ordinal)
                                           || call.StartsWith("checkout:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Dirty_repository_stops_before_fetch_or_checkout_without_discarding_changes() {
        var git = LocalRepository(CloneUrl);
        git.DirtyResult = GitCommandResult<bool>.Succeeded(true);
        var result = await new PullRequestRepositoryPreparer(git, new FakeFileSystem(Cwd))
            .PrepareAsync(new RepositoryPreparationRequest("repo", CloneUrl, "refs/heads/feature/work"));

        Assert.False(result.Success);
        Assert.Equal("repo-dirty-worktree", result.Failure!.Code);
        Assert.Contains("Commit or stash", result.Failure.Message);
        Assert.DoesNotContain(git.Calls, call => call.StartsWith("fetch:", StringComparison.Ordinal)
                                           || call.StartsWith("checkout:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Dirty_repository_is_stashed_then_checked_out_when_user_confirms() {
        var git = LocalRepository(CloneUrl);
        git.DirtyResult = GitCommandResult<bool>.Succeeded(true);
        var result = await new PullRequestRepositoryPreparer(git, new FakeFileSystem(Cwd))
            .PrepareAsync(new RepositoryPreparationRequest("repo", CloneUrl, "refs/heads/feature/work", _ => Task.FromResult(true)));

        Assert.True(result.Success);
        var stash = git.Calls.FindIndex(c => c.StartsWith("stash:", StringComparison.Ordinal));
        var checkout = git.Calls.FindIndex(c => c.StartsWith("checkout:", StringComparison.Ordinal));
        Assert.True(stash >= 0 && stash < checkout);
    }

    [Fact]
    public async Task Dirty_repository_stops_when_user_declines_stash() {
        var git = LocalRepository(CloneUrl);
        git.DirtyResult = GitCommandResult<bool>.Succeeded(true);
        var result = await new PullRequestRepositoryPreparer(git, new FakeFileSystem(Cwd))
            .PrepareAsync(new RepositoryPreparationRequest("repo", CloneUrl, "refs/heads/feature/work", _ => Task.FromResult(false)));

        Assert.False(result.Success);
        Assert.Equal("repo-dirty-worktree", result.Failure!.Code);
        Assert.DoesNotContain(git.Calls, call => call.StartsWith("stash:", StringComparison.Ordinal)
                                           || call.StartsWith("checkout:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Git_fetch_failure_does_not_attempt_checkout() {
        var git = LocalRepository(CloneUrl);
        git.FetchResult = GitCommandResult.Failed("git-command-failed", "network denied", "fatal: auth");
        var result = await new PullRequestRepositoryPreparer(git, new FakeFileSystem(Cwd))
            .PrepareAsync(new RepositoryPreparationRequest("repo", CloneUrl, "refs/heads/feature/work"));

        Assert.False(result.Success);
        Assert.Equal("git-command-failed", result.Failure!.Code);
        Assert.Equal("Failed to fetch the pull request source branch.", result.Failure.Message);
        Assert.DoesNotContain(git.Calls, call => call.StartsWith("checkout:", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("https://example.com/repo")]
    [InlineData("not-a-remote")]
    public async Task Unsupported_clone_urls_fail_before_git_operations(string cloneUrl) {
        var git = new FakeGitClient();
        var result = await new PullRequestRepositoryPreparer(git, new FakeFileSystem(Cwd))
            .PrepareAsync(new RepositoryPreparationRequest("repo", cloneUrl, "refs/heads/feature/work"));

        Assert.False(result.Success);
        Assert.Equal("ado-remote-invalid", result.Failure!.Code);
        Assert.Empty(git.Calls);
    }

    private static FakeGitClient LocalRepository(string remote) {
        var git = new FakeGitClient();
        git.Roots[Cwd] = GitCommandResult<string>.Succeeded(Cwd);
        git.Remotes[Cwd] = GitCommandResult<string>.Succeeded(remote);
        return git;
    }
}
