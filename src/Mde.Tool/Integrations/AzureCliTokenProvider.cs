namespace Mde.Tool.Integrations;

public sealed class AzureCliTokenProvider(IProcessLauncher processLauncher) : IAzureCliTokenProvider
{
    private const string AzureDevOpsResource = "499b84ac-1321-427f-aa17-267ca6975798";

    public async Task<AzureCliTokenResult> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        ProcessLaunchResult result;

        try
        {
            result = await processLauncher.RunAsync(
                new ProcessLaunchRequest(
                    "az",
                    [
                        "account",
                        "get-access-token",
                        "--resource",
                        AzureDevOpsResource,
                        "--query",
                        "accessToken",
                        "--output",
                        "tsv",
                        "--only-show-errors"
                    ]),
                cancellationToken);
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return AzureCliTokenResult.Failed(
                "azure-cli-unavailable",
                "Unable to execute Azure CLI.",
                exception.Message);
        }

        if (result.ExitCode != 0)
        {
            return AzureCliTokenResult.Failed(
                "azure-cli-token-command-failed",
                "Azure CLI could not acquire an Azure DevOps access token.",
                SanitizeError(result.StandardError));
        }

        var token = result.StandardOutput.Trim();
        if (string.IsNullOrWhiteSpace(token))
        {
            return AzureCliTokenResult.Failed(
                "azure-cli-empty-token",
                "Azure CLI returned an empty access token.");
        }

        return AzureCliTokenResult.Succeeded(token);
    }

    private static string? SanitizeError(string standardError) =>
        string.IsNullOrWhiteSpace(standardError) ? null : standardError.Trim();
}
