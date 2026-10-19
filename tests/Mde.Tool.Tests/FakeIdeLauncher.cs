using Mde.Tool.Integrations;

namespace Mde.Tool.Tests;

internal sealed class FakeIdeLauncher(IdeLaunchResult result) : IIdeLauncher {
    public List<IdeLaunchRequest> Requests { get; } = [];

    public Task<IdeLaunchResult> LaunchAsync(IdeLaunchRequest request, CancellationToken cancellationToken = default) {
        Requests.Add(request);
        return Task.FromResult(result);
    }
}
