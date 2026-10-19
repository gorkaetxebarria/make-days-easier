namespace Mde.Tool.Integrations;

public sealed record AdoPullRequestLookupResult
{
    private AdoPullRequestLookupResult(
        AdoPullRequestDetails? pullRequest,
        IntegrationFailure? failure)
    {
        PullRequest = pullRequest;
        Failure = failure;
    }

    public bool Success => Failure is null;

    public AdoPullRequestDetails? PullRequest { get; }

    public IntegrationFailure? Failure { get; }

    public static AdoPullRequestLookupResult Succeeded(AdoPullRequestDetails pullRequest)
    {
        ArgumentNullException.ThrowIfNull(pullRequest);
        return new AdoPullRequestLookupResult(pullRequest, null);
    }

    public static AdoPullRequestLookupResult Failed(string code, string message, string? details = null) =>
        new(null, new IntegrationFailure(code, message, details));
}
