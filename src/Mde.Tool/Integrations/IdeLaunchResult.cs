namespace Mde.Tool.Integrations;

public sealed record IdeLaunchResult(bool Launched, string? Warning)
{
    public static IdeLaunchResult Success() => new(true, null);

    public static IdeLaunchResult Warn(string warning) => new(false, warning);
}
