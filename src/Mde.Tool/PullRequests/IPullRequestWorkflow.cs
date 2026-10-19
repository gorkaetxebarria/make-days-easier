namespace Mde.Tool.PullRequests;

public interface IPullRequestWorkflow
{
    Task OpenAsync(PullRequestRequest request, CancellationToken cancellationToken = default);
}
