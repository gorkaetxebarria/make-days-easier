namespace Mde.Tool.Configuration;

public interface IUserConfigurationStore
{
    Task<UserConfiguration> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(UserConfiguration configuration, CancellationToken cancellationToken = default);
}
