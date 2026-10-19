namespace Mde.Tool.Integrations;

public sealed record AdoRemoteIdentity(
    string Organization,
    string Project,
    string Repository);
