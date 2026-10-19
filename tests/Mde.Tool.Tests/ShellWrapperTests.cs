using System.Diagnostics;

namespace Mde.Tool.Tests;

public sealed class ShellWrapperTests {
    [Theory]
    [InlineData("bash")]
    [InlineData("zsh")]
    public async Task Shell_wrapper_changes_directory_only_after_valid_success_json(string shell) {
        if (OperatingSystem.IsWindows() || !CommandExists(shell)) {
            return;
        }

        var scratch = Path.Combine(Directory.GetCurrentDirectory(), "artifacts", "test-scratch", Guid.NewGuid().ToString("N"));
        var target = Path.Combine(scratch, "target");
        Directory.CreateDirectory(target);
        try {
            var mde = Path.Combine(scratch, "mde");
            await File.WriteAllTextAsync(mde, $"#!/bin/sh\nprintf '%s\\n' '{{\"schemaVersion\":1,\"repositoryPath\":\"{target}\"}}'\n");
            MakeExecutable(mde);
            var script = Path.Combine(RepositoryRoot(), "scripts", "shell", "mde-pr.sh");
            var success = await RunShellAsync(shell, scratch, $"PATH='{scratch}':$PATH; . '{script}'; mde-pr 42; pwd");
            Assert.True(success.ExitCode == 0, success.StandardError);
            Assert.True(target == success.StandardOutput.Trim(), success.StandardError);

            await File.WriteAllTextAsync(mde, "#!/bin/sh\necho failure >&2\nexit 7\n");
            MakeExecutable(mde);
            var failure = await RunShellAsync(shell, scratch, $"PATH='{scratch}':$PATH; . '{script}'; before=$(pwd); mde-pr 42; result_code=$?; test \"$before\" = \"$(pwd)\"; test \"$result_code\" -eq 7");
            Assert.True(failure.ExitCode == 0, failure.StandardError);
        }
        finally {
            if (Directory.Exists(scratch)) Directory.Delete(scratch, true);
        }
    }

    [Fact]
    public async Task PowerShell_wrapper_has_a_cross_platform_json_and_failure_guard_contract() {
        var script = await File.ReadAllTextAsync(Path.Combine(RepositoryRoot(), "scripts", "shell", "mde-pr.ps1"));

        Assert.Contains("& mde pr $Id --ide $Ide --output json", script);
        Assert.Contains("ConvertFrom-Json -ErrorAction Stop", script);
        Assert.Contains("if ($LASTEXITCODE -ne 0)", script);
        Assert.Contains("Set-Location -LiteralPath $path", script);
        Assert.Contains("location not changed", script);
    }

    private static bool CommandExists(string command) {
        try {
            using var process = Process.Start(new ProcessStartInfo("sh", $"-c \"command -v {command}\"") {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            });
            process!.WaitForExit();
            return process.ExitCode == 0;
        }
        catch { return false; }
    }

    private static async Task<(int ExitCode, string StandardOutput, string StandardError)> RunShellAsync(string shell, string directory, string command) {
        var startInfo = new ProcessStartInfo(shell) {
            WorkingDirectory = directory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        startInfo.ArgumentList.Add("-c");
        startInfo.ArgumentList.Add(command);
        using var process = Process.Start(startInfo)!;
        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return (process.ExitCode, output, error);
    }

    private static void MakeExecutable(string file) {
        if (!OperatingSystem.IsWindows()) {
            File.SetUnixFileMode(file,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
        }
    }

    private static string RepositoryRoot() {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Mde.slnx"))) {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Could not find repository root.");
    }
}
