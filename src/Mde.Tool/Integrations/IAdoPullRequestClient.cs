namespace Mde.Tool.Integrations;

public interface IAdoPullRequestClient
{
    Task<AdoPullRequestLookupResult> GetPullRequestAsync(
        AdoPullRequestLookupRequest request,
        CancellationToken cancellationToken = default);
}
