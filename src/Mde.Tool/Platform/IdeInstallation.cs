using Mde.Tool.Configuration;

namespace Mde.Tool.Platform;

public sealed record IdeInstallation(
    PreferredIde Ide,
    string DisplayName,
    string ExecutablePath);
