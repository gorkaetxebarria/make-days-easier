using Mde.Tool.Platform;

namespace Mde.Tool.Configuration;

public sealed record UserConfiguration(
    string? AzureDevOpsOrganization = null,
    PreferredIde? PreferredIde = null);
