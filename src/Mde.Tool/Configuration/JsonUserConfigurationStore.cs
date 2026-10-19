using System.Text.Json;
using Mde.Tool.Platform;

namespace Mde.Tool.Configuration;

public sealed class JsonUserConfigurationStore(IPlatform platform) : IUserConfigurationStore
{
    private const string ApplicationName = "mde";
    private const string FileName = "config.json";
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public async Task<UserConfiguration> LoadAsync(CancellationToken cancellationToken = default)
    {
        var path = GetConfigurationPath();

        if (!File.Exists(path))
        {
            return new UserConfiguration();
        }

        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<UserConfiguration>(
                   stream,
                   SerializerOptions,
                   cancellationToken)
               ?? new UserConfiguration();
    }

    public async Task SaveAsync(UserConfiguration configuration, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var directory = platform.GetUserConfigurationDirectory(ApplicationName);
        Directory.CreateDirectory(directory);

        await using var stream = File.Create(Path.Combine(directory, FileName));
        await JsonSerializer.SerializeAsync(stream, configuration, SerializerOptions, cancellationToken);
    }

    private string GetConfigurationPath() =>
        Path.Combine(platform.GetUserConfigurationDirectory(ApplicationName), FileName);
}
