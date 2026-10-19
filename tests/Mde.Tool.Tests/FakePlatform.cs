using Mde.Tool.Platform;

namespace Mde.Tool.Tests;

internal sealed class FakePlatform : IPlatform {
    public string ConfigurationDirectory { get; init; } = Path.Combine(Directory.GetCurrentDirectory(), "artifacts", "test-config");
    public bool Windows { get; init; }
    public bool Interactive { get; init; }
    public Queue<PlatformProcessResult> ProcessResults { get; } = new();
    public List<PlatformProcessStartInfo> ProcessCalls { get; } = [];

    public string GetUserConfigurationDirectory(string applicationName) => ConfigurationDirectory;
    public bool IsWindows() => Windows;
    public bool IsInteractiveTerminal() => Interactive;
    public IReadOnlyList<IdeInstallation> DetectInstalledIdes() => [];

    public Task<PlatformProcessResult> RunProcessAsync(PlatformProcessStartInfo startInfo, CancellationToken cancellationToken = default) {
        ProcessCalls.Add(startInfo);
        return Task.FromResult(ProcessResults.Count > 0
            ? ProcessResults.Dequeue()
            : new PlatformProcessResult(0, string.Empty, string.Empty));
    }
}
