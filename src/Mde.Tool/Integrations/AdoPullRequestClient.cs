using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Mde.Tool.Integrations;

public sealed class AdoPullRequestClient(
    HttpClient httpClient,
    IAzureCliTokenProvider tokenProvider) : IAdoPullRequestClient
{
    public async Task<AdoPullRequestLookupResult> GetPullRequestAsync(
        AdoPullRequestLookupRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Organization))
        {
            return AdoPullRequestLookupResult.Failed(
                "ado-organization-required",
                "Azure DevOps organization is required.");
        }

        var tokenResult = await tokenProvider.GetAccessTokenAsync(cancellationToken);
        if (!tokenResult.Success)
        {
            return AdoPullRequestLookupResult.Failed(
                tokenResult.Failure!.Code,
                tokenResult.Failure.Message,
                tokenResult.Failure.Details);
        }

        using var pullRequestResponse = await SendAsync(
            BuildOrgPullRequestUri(request.Organization, request.PullRequestId),
            tokenResult.AccessToken!,
            cancellationToken);
        if (!pullRequestResponse.Success)
        {
            return pullRequestResponse.Failure!;
        }

        await using var pullRequestDocument = await pullRequestResponse.Response!.Content
            .ReadAsStreamAsync(cancellationToken);
        using var pullRequestJson = await JsonDocument.ParseAsync(pullRequestDocument, cancellationToken: cancellationToken);

        if (!TryBuildPullRequestSnapshot(
                request.PullRequestId,
                pullRequestJson.RootElement,
                out var snapshot,
                out var parseFailure))
        {
            return parseFailure!;
        }

        var changedFilesResult = await GetChangedFilesAsync(
            request.Organization,
            request.PullRequestId,
            snapshot.RepositoryId,
            snapshot.ProjectId,
            tokenResult.AccessToken!,
            cancellationToken);
        if (!changedFilesResult.Success)
        {
            return AdoPullRequestLookupResult.Failed(
                changedFilesResult.Failure!.Code,
                changedFilesResult.Failure.Message,
                changedFilesResult.Failure.Details);
        }

        return AdoPullRequestLookupResult.Succeeded(
            new AdoPullRequestDetails(
                request.PullRequestId,
                snapshot.Title,
                snapshot.AuthorDisplayName,
                snapshot.TargetBranch,
                snapshot.TargetRef,
                snapshot.SourceBranch,
                snapshot.SourceRef,
                snapshot.SourceCommit,
                snapshot.RepositoryName,
                snapshot.RepositoryId,
                snapshot.ProjectName,
                snapshot.ProjectId,
                snapshot.CloneUrl,
                snapshot.RemoteUrl,
                changedFilesResult.ChangedFiles!));
    }

    private async Task<ChangedFilesResult> GetChangedFilesAsync(
        string organization,
        int pullRequestId,
        string repositoryId,
        string projectId,
        string accessToken,
        CancellationToken cancellationToken)
    {
        using var iterationsResponse = await SendAsync(
            BuildIterationsUri(organization, projectId, repositoryId, pullRequestId),
            accessToken,
            cancellationToken);
        if (!iterationsResponse.Success)
        {
            return ChangedFilesResult.FromFailure(iterationsResponse.Failure!);
        }

        await using var iterationsContent = await iterationsResponse.Response!.Content
            .ReadAsStreamAsync(cancellationToken);
        using var iterationsJson = await JsonDocument.ParseAsync(iterationsContent, cancellationToken: cancellationToken);

        var lastIterationId = GetLastIterationId(iterationsJson.RootElement);
        if (!lastIterationId.HasValue)
        {
            return ChangedFilesResult.Succeeded([]);
        }

        using var changesResponse = await SendAsync(
            BuildChangesUri(organization, projectId, repositoryId, pullRequestId, lastIterationId.Value),
            accessToken,
            cancellationToken);
        if (!changesResponse.Success)
        {
            return ChangedFilesResult.FromFailure(changesResponse.Failure!);
        }

        await using var changesContent = await changesResponse.Response!.Content.ReadAsStreamAsync(cancellationToken);
        using var changesJson = await JsonDocument.ParseAsync(changesContent, cancellationToken: cancellationToken);

        var changedFiles = ParseChangedFiles(changesJson.RootElement);
        return ChangedFilesResult.Succeeded(changedFiles);
    }

    private async Task<HttpResponseEnvelope> SendAsync(
        Uri uri,
        string accessToken,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        HttpResponseMessage response;
        try
        {
            response = await httpClient.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            return HttpResponseEnvelope.FromFailure(
                AdoPullRequestLookupResult.Failed(
                    "ado-request-failed",
                    "Azure DevOps request failed.",
                    exception.Message));
        }

        if (response.IsSuccessStatusCode)
        {
            return HttpResponseEnvelope.FromSuccess(response);
        }

        var details = await response.Content.ReadAsStringAsync(cancellationToken);
        var failure = response.StatusCode switch
        {
            HttpStatusCode.NotFound => AdoPullRequestLookupResult.Failed(
                "ado-pr-not-found",
                "Pull request was not found.",
                TrimDetails(details)),
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => AdoPullRequestLookupResult.Failed(
                "ado-auth-failed",
                "Azure DevOps rejected the access token.",
                TrimDetails(details)),
            _ => AdoPullRequestLookupResult.Failed(
                "ado-unexpected-status",
                $"Azure DevOps request failed with status {(int)response.StatusCode}.",
                TrimDetails(details))
        };
        response.Dispose();
        return HttpResponseEnvelope.FromFailure(failure);
    }

    private static Uri BuildOrgPullRequestUri(string organization, int pullRequestId) =>
        new($"https://dev.azure.com/{Uri.EscapeDataString(organization)}/_apis/git/pullrequests/{pullRequestId}?api-version=7.1");

    private static Uri BuildIterationsUri(string organization, string projectId, string repositoryId, int pullRequestId) =>
        new($"https://dev.azure.com/{Uri.EscapeDataString(organization)}/{Uri.EscapeDataString(projectId)}/_apis/git/repositories/{Uri.EscapeDataString(repositoryId)}/pullRequests/{pullRequestId}/iterations?api-version=7.1");

    private static Uri BuildChangesUri(string organization, string projectId, string repositoryId, int pullRequestId, int iterationId) =>
        new($"https://dev.azure.com/{Uri.EscapeDataString(organization)}/{Uri.EscapeDataString(projectId)}/_apis/git/repositories/{Uri.EscapeDataString(repositoryId)}/pullRequests/{pullRequestId}/iterations/{iterationId}/changes?api-version=7.1&$top=2000");

    private static bool TryBuildPullRequestSnapshot(
        int pullRequestId,
        JsonElement pullRequest,
        out PullRequestSnapshot snapshot,
        out AdoPullRequestLookupResult? failure)
    {
        snapshot = default!;
        failure = null;

        if (!TryReadString(pullRequest, "title", out var title)
            || !TryReadString(pullRequest, "sourceRefName", out var sourceRef)
            || !TryReadString(pullRequest, "targetRefName", out var targetRef))
        {
            failure = AdoPullRequestLookupResult.Failed(
                "ado-pr-shape-invalid",
                "Pull request payload did not include required fields.");
            return false;
        }

        var sourceCommit = pullRequest.TryGetProperty("lastMergeSourceCommit", out var sourceCommitObject)
                           && TryReadString(sourceCommitObject, "commitId", out var commit)
            ? commit
            : string.Empty;

        var authorDisplayName = pullRequest.TryGetProperty("createdBy", out var author)
                                 && TryReadString(author, "displayName", out var createdByName)
            ? createdByName
            : string.Empty;

        if (!pullRequest.TryGetProperty("repository", out var repository)
            || !TryReadString(repository, "id", out var repositoryId)
            || !TryReadString(repository, "name", out var repositoryName)
            || !TryReadString(repository, "remoteUrl", out var remoteUrl))
        {
            failure = AdoPullRequestLookupResult.Failed(
                "ado-pr-repository-shape-invalid",
                "Pull request payload did not include repository identity.");
            return false;
        }

        if (!repository.TryGetProperty("project", out var project)
            || !TryReadString(project, "id", out var projectId)
            || !TryReadString(project, "name", out var projectName))
        {
            failure = AdoPullRequestLookupResult.Failed(
                "ado-pr-project-shape-invalid",
                "Pull request payload did not include project identity.");
            return false;
        }

        snapshot = new PullRequestSnapshot(
            pullRequestId,
            title,
            authorDisplayName,
            ShortBranch(targetRef),
            targetRef,
            ShortBranch(sourceRef),
            sourceRef,
            sourceCommit,
            repositoryName,
            repositoryId,
            projectName,
            projectId,
            remoteUrl,
            remoteUrl);
        return true;
    }

    private static int? GetLastIterationId(JsonElement root)
    {
        if (!root.TryGetProperty("value", out var iterations)
            || iterations.ValueKind is not JsonValueKind.Array)
        {
            return null;
        }

        var max = (int?)null;
        foreach (var iteration in iterations.EnumerateArray())
        {
            if (iteration.TryGetProperty("id", out var idElement)
                && idElement.TryGetInt32(out var iterationId)
                && (!max.HasValue || iterationId > max.Value))
            {
                max = iterationId;
            }
        }

        return max;
    }

    private static IReadOnlyList<string> ParseChangedFiles(JsonElement root)
    {
        if (!root.TryGetProperty("changes", out var changes)
            || changes.ValueKind is not JsonValueKind.Array)
        {
            return [];
        }

        var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var change in changes.EnumerateArray())
        {
            if (!change.TryGetProperty("item", out var item))
            {
                continue;
            }

            if (TryReadString(item, "path", out var path))
            {
                files.Add(path);
            }
        }

        return files.Order(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static bool TryReadString(JsonElement element, string propertyName, out string value)
    {
        value = string.Empty;
        if (!element.TryGetProperty(propertyName, out var property)
            || property.ValueKind is not JsonValueKind.String)
        {
            return false;
        }

        value = property.GetString() ?? string.Empty;
        return !string.IsNullOrWhiteSpace(value);
    }

    private static string ShortBranch(string referenceName) =>
        referenceName.StartsWith("refs/heads/", StringComparison.OrdinalIgnoreCase)
            ? referenceName["refs/heads/".Length..]
            : referenceName;

    private static string? TrimDetails(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed record PullRequestSnapshot(
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
        string RemoteUrl);

    private sealed record HttpResponseEnvelope(
        bool Success,
        HttpResponseMessage? Response,
        AdoPullRequestLookupResult? Failure) : IDisposable
    {
        public static HttpResponseEnvelope FromSuccess(HttpResponseMessage response) =>
            new(true, response, null);

        public static HttpResponseEnvelope FromFailure(AdoPullRequestLookupResult failure) =>
            new(false, null, failure);

        public void Dispose()
        {
            Response?.Dispose();
        }
    }

    private sealed record ChangedFilesResult(
        IReadOnlyList<string>? ChangedFiles,
        IntegrationFailure? Failure)
    {
        public bool Success => Failure is null;

        public static ChangedFilesResult Succeeded(IReadOnlyList<string> changedFiles) =>
            new(changedFiles, null);

        public static ChangedFilesResult FromFailure(AdoPullRequestLookupResult failure) =>
            new(
                null,
                failure.Failure
                ?? new IntegrationFailure("ado-unexpected-failure", "Azure DevOps request failed."));
    }
}
