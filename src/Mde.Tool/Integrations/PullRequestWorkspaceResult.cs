namespace Mde.Tool.Integrations;

public sealed record PullRequestWorkspaceResult
{
    private PullRequestWorkspaceResult(
        AdoPullRequestDetails? pullRequest,
        string? repositoryPath,
        string? checkedOutBranch,
        IReadOnlyList<string> warnings,
        IntegrationFailure? failure)
    {
        PullRequest = pullRequest;
        RepositoryPath = repositoryPath;
        CheckedOutBranch = checkedOutBranch;
        Warnings = warnings;
        Failure = failure;
    }

    public bool Success => Failure is null;

    public AdoPullRequestDetails? PullRequest { get; }

    public string? RepositoryPath { get; }

    public string? CheckedOutBranch { get; }

    public IReadOnlyList<string> Warnings { get; }

    public IntegrationFailure? Failure { get; }

    public static PullRequestWorkspaceResult Succeeded(
        AdoPullRequestDetails pullRequest,
        string repositoryPath,
        string checkedOutBranch,
        IReadOnlyList<string> warnings) =>
        new(pullRequest, repositoryPath, checkedOutBranch, warnings, null);

    public static PullRequestWorkspaceResult Failed(string code, string message, string? details = null) =>
        new(null, null, null, [], new IntegrationFailure(code, message, details));
}
