using Mde.Tool.Configuration;

namespace Mde.Tool.Tests;

internal sealed class MemoryConfigurationStore(UserConfiguration configuration) : IUserConfigurationStore {
    public UserConfiguration Configuration { get; private set; } = configuration;

    public Task<UserConfiguration> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult(Configuration);

    public Task SaveAsync(UserConfiguration configuration, CancellationToken cancellationToken = default) {
        Configuration = configuration;
        return Task.CompletedTask;
    }
}
