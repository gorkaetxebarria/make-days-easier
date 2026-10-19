namespace Mde.Tool.Platform;

public sealed record PlatformProcessResult(
    int ExitCode,
    string StandardOutput,
    string StandardError);
