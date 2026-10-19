namespace Mde.Tool.Integrations;

public sealed record AdoPullRequestLookupRequest(
    string Organization,
    int PullRequestId);
