using System.Net;
using System.Net.Http;
using Mde.Tool.Integrations;

namespace Mde.Tool.Tests;

public sealed class ExternalIntegrationTests {
    [Fact]
    public async Task Azure_cli_token_failure_never_echoes_access_token() {
        const string token = "definitely-not-a-real-token";
        var launcher = new RecordingProcessLauncher(new ProcessLaunchResult(1, token, "not signed in"));
        var result = await new AzureCliTokenProvider(launcher).GetAccessTokenAsync();

        Assert.False(result.Success);
        Assert.Equal("azure-cli-token-command-failed", result.Failure!.Code);
        Assert.DoesNotContain(token, result.Failure.Details ?? string.Empty);
        Assert.Equal("az", Assert.Single(launcher.Requests).FileName);
        Assert.Contains("--resource", launcher.Requests[0].Arguments);
    }

    [Fact]
    public async Task Ado_auth_failure_uses_fake_http_and_returns_safe_guidance() {
        var handler = new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized) {
            Content = new StringContent("expired credential")
        });
        using var http = new HttpClient(handler);
        var client = new AdoPullRequestClient(http, new FixedTokenProvider());

        var result = await client.GetPullRequestAsync(new AdoPullRequestLookupRequest("contoso", 42));

        Assert.False(result.Success);
        Assert.Equal("ado-auth-failed", result.Failure!.Code);
        Assert.Equal("Azure DevOps rejected the access token.", result.Failure.Message);
        Assert.Equal(1, handler.Requests);
    }

    private sealed class RecordingProcessLauncher(ProcessLaunchResult result) : IProcessLauncher {
        public List<ProcessLaunchRequest> Requests { get; } = [];
        public Task<ProcessLaunchResult> RunAsync(ProcessLaunchRequest request, CancellationToken cancellationToken = default) {
            Requests.Add(request);
            return Task.FromResult(result);
        }
    }

    private sealed class FixedTokenProvider : IAzureCliTokenProvider {
        public Task<AzureCliTokenResult> GetAccessTokenAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(AzureCliTokenResult.Succeeded("fake-token"));
    }

    private sealed class StubHttpHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler {
        public int Requests { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
            Requests++;
            return Task.FromResult(responder(request));
        }
    }
}
