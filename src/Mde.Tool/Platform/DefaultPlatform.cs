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

        var fileName = startInfo.FileName;
        var launcherArguments = new List<string>();
        if (OperatingSystem.IsWindows() && ResolveWindowsScript(fileName) is { } script)
        {
            // Process.Start cannot launch .cmd/.bat shims (e.g. az.cmd) without a shell.
            launcherArguments.AddRange(["/c", script]);
            fileName = Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe";
        }

        var processStartInfo = new ProcessStartInfo(fileName)
        {
            RedirectStandardOutput = startInfo.RedirectStandardOutput,
            RedirectStandardError = startInfo.RedirectStandardError,
            UseShellExecute = false,
            WorkingDirectory = startInfo.WorkingDirectory ?? Environment.CurrentDirectory
        };

        foreach (var argument in launcherArguments.Concat(startInfo.Arguments))
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

    private static string? ResolveWindowsScript(string fileName)
    {
        if (Path.HasExtension(fileName))
        {
            return fileName.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase)
                || fileName.EndsWith(".bat", StringComparison.OrdinalIgnoreCase)
                ? fileName
                : null;
        }

        var directories = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
        foreach (var directory in directories)
        {
            if (File.Exists(Path.Combine(directory, fileName + ".exe")))
            {
                return null;
            }

            foreach (var extension in new[] { ".cmd", ".bat" })
            {
                var candidate = Path.Combine(directory, fileName + extension);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        return null;
    }
}
