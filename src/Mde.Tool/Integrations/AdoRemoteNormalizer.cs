using System.Text.RegularExpressions;

namespace Mde.Tool.Integrations;

public static partial class AdoRemoteNormalizer
{
    public static bool AreEquivalent(string expectedRemoteUrl, string actualRemoteUrl)
    {
        return TryNormalize(expectedRemoteUrl, out var expectedIdentity)
               && TryNormalize(actualRemoteUrl, out var actualIdentity)
               && string.Equals(expectedIdentity.Organization, actualIdentity.Organization, StringComparison.OrdinalIgnoreCase)
               && string.Equals(expectedIdentity.Project, actualIdentity.Project, StringComparison.OrdinalIgnoreCase)
               && string.Equals(expectedIdentity.Repository, actualIdentity.Repository, StringComparison.OrdinalIgnoreCase);
    }

    public static bool TryNormalize(string remoteUrl, out AdoRemoteIdentity identity)
    {
        identity = default!;
        if (string.IsNullOrWhiteSpace(remoteUrl))
        {
            return false;
        }

        var trimmed = remoteUrl.Trim();

        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var absoluteUri))
        {
            return TryNormalizeUri(absoluteUri, out identity);
        }

        var scpMatch = ScpPattern().Match(trimmed);
        if (!scpMatch.Success)
        {
            return false;
        }

        var host = scpMatch.Groups["host"].Value;
        var path = scpMatch.Groups["path"].Value.Trim('/');
        return TryNormalizeScp(host, path, out identity);
    }

    private static bool TryNormalizeUri(Uri uri, out AdoRemoteIdentity identity)
    {
        identity = default!;
        var host = uri.Host.ToLowerInvariant();
        var segments = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (host == "dev.azure.com")
        {
            return TryNormalizeDevAzurePath(segments, out identity);
        }

        if (host == "ssh.dev.azure.com")
        {
            return segments.Length >= 4
                   && string.Equals(segments[0], "v3", StringComparison.OrdinalIgnoreCase)
                   && TryBuildIdentity(segments[1], segments[2], segments[3], out identity);
        }

        if (host.EndsWith(".visualstudio.com", StringComparison.OrdinalIgnoreCase))
        {
            var organization = host[..^".visualstudio.com".Length];
            return TryNormalizeVisualStudioPath(organization, segments, out identity);
        }

        return false;
    }

    private static bool TryNormalizeScp(string host, string path, out AdoRemoteIdentity identity)
    {
        identity = default!;
        if (!string.Equals(host, "ssh.dev.azure.com", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments.Length >= 4
               && string.Equals(segments[0], "v3", StringComparison.OrdinalIgnoreCase)
               && TryBuildIdentity(segments[1], segments[2], segments[3], out identity);
    }

    private static bool TryNormalizeDevAzurePath(string[] segments, out AdoRemoteIdentity identity)
    {
        identity = default!;
        return segments.Length >= 4
               && string.Equals(segments[2], "_git", StringComparison.OrdinalIgnoreCase)
               && TryBuildIdentity(segments[0], segments[1], segments[3], out identity);
    }

    private static bool TryNormalizeVisualStudioPath(
        string organization,
        string[] segments,
        out AdoRemoteIdentity identity)
    {
        identity = default!;
        if (segments.Length >= 4
            && string.Equals(segments[0], "DefaultCollection", StringComparison.OrdinalIgnoreCase)
            && string.Equals(segments[2], "_git", StringComparison.OrdinalIgnoreCase))
        {
            return TryBuildIdentity(organization, segments[1], segments[3], out identity);
        }

        return segments.Length >= 3
               && string.Equals(segments[1], "_git", StringComparison.OrdinalIgnoreCase)
               && TryBuildIdentity(organization, segments[0], segments[2], out identity);
    }

    private static bool TryBuildIdentity(
        string organization,
        string project,
        string repository,
        out AdoRemoteIdentity identity)
    {
        identity = default!;

        organization = Uri.UnescapeDataString(organization);
        project = Uri.UnescapeDataString(project);
        repository = Uri.UnescapeDataString(repository);
        if (string.IsNullOrWhiteSpace(organization)
            || string.IsNullOrWhiteSpace(project)
            || string.IsNullOrWhiteSpace(repository))
        {
            return false;
        }

        identity = new AdoRemoteIdentity(
            organization.Trim(),
            project.Trim(),
            repository.Trim());
        return true;
    }

    [GeneratedRegex("^(?<user>[^@]+)@(?<host>[^:]+):(?<path>.+)$", RegexOptions.Compiled)]
    private static partial Regex ScpPattern();
}
