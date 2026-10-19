namespace Mde.Tool.Platform;

public sealed record PlatformProcessStartInfo(
    string FileName,
    IReadOnlyList<string> Arguments,
    string? WorkingDirectory = null,
    bool RedirectStandardOutput = true,
    bool RedirectStandardError = true);
