using Mde.Tool.Configuration;

namespace Mde.Tool.Integrations;

public sealed record PullRequestWorkspaceRequest(
    string Organization,
    int PullRequestId,
    PreferredIde? PreferredIde = null,
    string? SolutionPath = null,
    bool LaunchIde = false);
