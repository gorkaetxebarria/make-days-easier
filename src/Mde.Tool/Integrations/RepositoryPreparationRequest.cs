namespace Mde.Tool.Integrations;

public sealed record RepositoryPreparationRequest(
    string RepositoryName,
    string CloneUrl,
    string SourceRef,
    Func<CancellationToken, Task<bool>>? ConfirmStashAsync = null,
    Action<string>? Log = null);
