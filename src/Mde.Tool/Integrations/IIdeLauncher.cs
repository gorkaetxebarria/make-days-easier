namespace Mde.Tool.Integrations;

public interface IIdeLauncher
{
    Task<IdeLaunchResult> LaunchAsync(
        IdeLaunchRequest request,
        CancellationToken cancellationToken = default);
}
