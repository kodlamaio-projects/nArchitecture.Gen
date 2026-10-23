using System.Text.RegularExpressions;

namespace Application.Features.Upgrade;

/// <summary>
/// Resolves simple, single-level MSBuild property references (<c>$(Name)</c>) against the <c>&lt;Name&gt;value&lt;/Name&gt;</c>
/// declarations found in one file's text, so a version comparison does not mistake
/// <c>Version="$(NArchitectureCoreVersion)"</c> for a literal that fails to start with "2.". It does not expand a
/// property embedded inside a larger string, follow conditions, or chase a property that itself references another one -
/// that covers every version-property pattern seen in these repos (one property, defined and used in the same file).
/// </summary>
public sealed partial class MsBuildPropertyIndex
{
    private readonly Dictionary<string, string> _values = new(StringComparer.OrdinalIgnoreCase);

    public static MsBuildPropertyIndex Build(string fileContent)
    {
        MsBuildPropertyIndex index = new();
        foreach (Match match in PropertyRegex().Matches(fileContent))
        {
            string value = match.Groups[2].Value.Trim();
            if (!value.StartsWith("$(", StringComparison.Ordinal))
                index._values[match.Groups[1].Value] = value;
        }
        return index;
    }

    /// <summary>Returns the literal value a pure <c>$(Name)</c> reference resolves to, or the input unchanged otherwise.</summary>
    public string Resolve(string value)
    {
        Match match = PureReferenceRegex().Match(value.Trim());
        return match.Success && _values.TryGetValue(match.Groups[1].Value, out string? resolved) ? resolved : value;
    }

    [GeneratedRegex(@"<([A-Za-z_]\w*)>\s*([^<>]*?)\s*</\1>")]
    private static partial Regex PropertyRegex();

    [GeneratedRegex(@"^\$\(([A-Za-z_]\w*)\)$")]
    private static partial Regex PureReferenceRegex();
}
