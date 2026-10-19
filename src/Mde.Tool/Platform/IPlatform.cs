namespace Mde.Tool.Platform;

public interface IPlatform
{
    string GetUserConfigurationDirectory(string applicationName);

    bool IsWindows();

    bool IsInteractiveTerminal();

    Task<PlatformProcessResult> RunProcessAsync(
        PlatformProcessStartInfo startInfo,
        CancellationToken cancellationToken = default);

    IReadOnlyList<IdeInstallation> DetectInstalledIdes();
}
