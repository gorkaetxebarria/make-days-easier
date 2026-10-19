namespace Mde.Tool.Integrations;

public class GitCommandResult
{
    protected GitCommandResult(IntegrationFailure? failure)
    {
        Failure = failure;
    }

    public bool Success => Failure is null;

    public IntegrationFailure? Failure { get; }

    public static GitCommandResult Succeeded() => new(null);

    public static GitCommandResult Failed(string code, string message, string? details = null) =>
        new(new IntegrationFailure(code, message, details));
}

public sealed class GitCommandResult<T> : GitCommandResult
{
    private GitCommandResult(T? value, IntegrationFailure? failure)
        : base(failure)
    {
        Value = value;
    }

    public T? Value { get; }

    public static GitCommandResult<T> Succeeded(T value) => new(value, null);

    public static new GitCommandResult<T> Failed(string code, string message, string? details = null) =>
        new(default, new IntegrationFailure(code, message, details));
}
