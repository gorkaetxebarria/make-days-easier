namespace Mde.Tool.Integrations;

public sealed record ProcessLaunchResult(
    int ExitCode,
    string StandardOutput,
    string StandardError);
