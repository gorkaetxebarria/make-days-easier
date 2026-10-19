namespace Mde.Tool.Integrations;

public sealed record AzureCliTokenResult
{
    private AzureCliTokenResult(string? accessToken, IntegrationFailure? failure)
    {
        AccessToken = accessToken;
        Failure = failure;
    }

    public bool Success => Failure is null;

    public string? AccessToken { get; }

    public IntegrationFailure? Failure { get; }

    public static AzureCliTokenResult Succeeded(string accessToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accessToken);
        return new AzureCliTokenResult(accessToken, null);
    }

    public static AzureCliTokenResult Failed(string code, string message, string? details = null) =>
        new(null, new IntegrationFailure(code, message, details));
}
