using Mde.Tool.Integrations;

namespace Mde.Tool.Tests;

internal sealed class FakeAdoClient(AdoPullRequestLookupResult result) : IAdoPullRequestClient {
    public List<AdoPullRequestLookupRequest> Requests { get; } = [];

    public Task<AdoPullRequestLookupResult> GetPullRequestAsync(AdoPullRequestLookupRequest request, CancellationToken cancellationToken = default) {
        Requests.Add(request);
        return Task.FromResult(result);
    }
}
