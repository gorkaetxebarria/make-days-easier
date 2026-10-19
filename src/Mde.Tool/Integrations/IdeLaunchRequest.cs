using Mde.Tool.Configuration;

namespace Mde.Tool.Integrations;

public sealed record IdeLaunchRequest(
    string WorkingDirectory,
    PreferredIde? PreferredIde = null,
    string? SolutionPath = null);
