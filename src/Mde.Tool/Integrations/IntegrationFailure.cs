namespace Mde.Tool.Integrations;

public sealed record IntegrationFailure(
    string Code,
    string Message,
    string? Details = null);
