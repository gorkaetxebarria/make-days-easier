namespace Mde.Tool.Integrations;

public sealed class PullRequestWorkspaceService(
    IAdoPullRequestClient pullRequestClient,
    PullRequestRepositoryPreparer repositoryPreparer,
    IIdeLauncher ideLauncher)
{
    public async Task<PullRequestWorkspaceResult> PrepareAsync(
        PullRequestWorkspaceRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        request.Log?.Invoke($"Reading pull request {request.PullRequestId} from '{request.Organization}'...");
        var pullRequestResult = await pullRequestClient.GetPullRequestAsync(
            new AdoPullRequestLookupRequest(request.Organization, request.PullRequestId),
            cancellationToken);
        if (!pullRequestResult.Success)
        {
            return PullRequestWorkspaceResult.Failed(
                pullRequestResult.Failure!.Code,
                pullRequestResult.Failure.Message,
                pullRequestResult.Failure.Details);
        }

        var pullRequest = pullRequestResult.PullRequest!;
        request.Log?.Invoke($"Found '{pullRequest.RepositoryName}' (source {pullRequest.SourceRef}, target {pullRequest.TargetBranch}).");
        var repositoryResult = await repositoryPreparer.PrepareAsync(
            new RepositoryPreparationRequest(
                pullRequest.RepositoryName,
                pullRequest.CloneUrl,
                pullRequest.SourceRef,
                request.ConfirmStashAsync,
                request.Log),
            cancellationToken);
        if (!repositoryResult.Success)
        {
            return PullRequestWorkspaceResult.Failed(
                repositoryResult.Failure!.Code,
                repositoryResult.Failure.Message,
                repositoryResult.Failure.Details);
        }

        var warnings = new List<string>();
        if (request.LaunchIde)
        {
            request.Log?.Invoke("Launching IDE...");
            var ideResult = await ideLauncher.LaunchAsync(
                new IdeLaunchRequest(
                    repositoryResult.RepositoryPath!,
                    request.PreferredIde,
                    request.SolutionPath),
                cancellationToken);
            if (!ideResult.Launched && !string.IsNullOrWhiteSpace(ideResult.Warning))
            {
                warnings.Add(ideResult.Warning);
            }
        }

        return PullRequestWorkspaceResult.Succeeded(
            pullRequest,
            repositoryResult.RepositoryPath!,
            repositoryResult.CheckedOutBranch!,
            warnings);
    }
}
