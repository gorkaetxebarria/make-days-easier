namespace Mde.Tool.Integrations;

public sealed record ProcessLaunchRequest(
    string FileName,
    IReadOnlyList<string> Arguments,
    string? WorkingDirectory = null,
    bool RedirectStandardOutput = true,
    bool RedirectStandardError = true);
