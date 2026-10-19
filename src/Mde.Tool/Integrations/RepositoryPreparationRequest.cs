namespace Mde.Tool.Integrations;

public sealed record RepositoryPreparationRequest(
    string RepositoryName,
    string CloneUrl,
    string SourceRef);
