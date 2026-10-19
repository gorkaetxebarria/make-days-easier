using System.Diagnostics;

namespace Mde.Tool.Platform;

public sealed class DefaultPlatform : IPlatform
{
    public string GetUserConfigurationDirectory(string applicationName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationName);

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            applicationName);
    }

    public bool IsInteractiveTerminal()
    {
        return !Console.IsInputRedirected
            && !Console.IsOutputRedirected
            && !Console.IsErrorRedirected;
    }

    public bool IsWindows() => OperatingSystem.IsWindows();

    public async Task<PlatformProcessResult> RunProcessAsync(
        PlatformProcessStartInfo startInfo,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(startInfo);

        var processStartInfo = new ProcessStartInfo(startInfo.FileName)
        {
            RedirectStandardOutput = startInfo.RedirectStandardOutput,
            RedirectStandardError = startInfo.RedirectStandardError,
            UseShellExecute = false,
            WorkingDirectory = startInfo.WorkingDirectory ?? Environment.CurrentDirectory
        };

        foreach (var argument in startInfo.Arguments)
        {
            processStartInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(processStartInfo)
            ?? throw new InvalidOperationException($"Unable to start '{startInfo.FileName}'.");

        var standardOutputTask = processStartInfo.RedirectStandardOutput
            ? process.StandardOutput.ReadToEndAsync(cancellationToken)
            : Task.FromResult(string.Empty);
        var standardErrorTask = processStartInfo.RedirectStandardError
            ? process.StandardError.ReadToEndAsync(cancellationToken)
            : Task.FromResult(string.Empty);

        await process.WaitForExitAsync(cancellationToken);

        return new PlatformProcessResult(
            process.ExitCode,
            await standardOutputTask,
            await standardErrorTask);
    }

    public IReadOnlyList<IdeInstallation> DetectInstalledIdes() => [];
}
