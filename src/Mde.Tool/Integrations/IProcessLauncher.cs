namespace Mde.Tool.Integrations;

public interface IProcessLauncher
{
    Task<ProcessLaunchResult> RunAsync(
        ProcessLaunchRequest request,
        CancellationToken cancellationToken = default);
}
