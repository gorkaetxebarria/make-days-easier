namespace Mde.Tool.Integrations;

public interface IAzureCliTokenProvider
{
    Task<AzureCliTokenResult> GetAccessTokenAsync(CancellationToken cancellationToken = default);
}
