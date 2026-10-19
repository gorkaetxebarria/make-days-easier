using System.Text.RegularExpressions;

namespace Mde.Tool.Commands;

internal static partial class OrganizationName
{
    public static bool IsValid(string value) => Pattern().IsMatch(value);

    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9._-]{0,49}$")]
    private static partial Regex Pattern();
}
