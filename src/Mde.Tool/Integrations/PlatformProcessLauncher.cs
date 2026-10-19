using Mde.Tool.Platform;

namespace Mde.Tool.Integrations;

public sealed class PlatformProcessLauncher(IPlatform platform) : IProcessLauncher
{
    public async Task<ProcessLaunchResult> RunAsync(
        ProcessLaunchRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = await platform.RunProcessAsync(
            new PlatformProcessStartInfo(
                request.FileName,
                request.Arguments,
                request.WorkingDirectory,
                request.RedirectStandardOutput,
                request.RedirectStandardError),
            cancellationToken);

        return new ProcessLaunchResult(
            result.ExitCode,
            result.StandardOutput,
            result.StandardError);
    }
}
