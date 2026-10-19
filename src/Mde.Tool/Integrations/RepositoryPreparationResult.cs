namespace Mde.Tool.Integrations;

public sealed record RepositoryPreparationResult
{
    private RepositoryPreparationResult(
        string? repositoryPath,
        string? checkedOutBranch,
        IntegrationFailure? failure)
    {
        RepositoryPath = repositoryPath;
        CheckedOutBranch = checkedOutBranch;
        Failure = failure;
    }

    public bool Success => Failure is null;

    public string? RepositoryPath { get; }

    public string? CheckedOutBranch { get; }

    public IntegrationFailure? Failure { get; }

    public static RepositoryPreparationResult Succeeded(string repositoryPath, string checkedOutBranch) =>
        new(repositoryPath, checkedOutBranch, null);

    public static RepositoryPreparationResult Failed(string code, string message, string? details = null) =>
        new(null, null, new IntegrationFailure(code, message, details));
}
