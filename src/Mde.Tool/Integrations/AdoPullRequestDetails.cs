namespace Mde.Tool.Integrations;

public sealed record AdoPullRequestDetails(
    int PullRequestId,
    string Title,
    string AuthorDisplayName,
    string TargetBranch,
    string TargetRef,
    string SourceBranch,
    string SourceRef,
    string SourceCommit,
    string RepositoryName,
    string RepositoryId,
    string ProjectName,
    string ProjectId,
    string CloneUrl,
    string RemoteUrl,
    IReadOnlyList<string> ChangedFiles);
