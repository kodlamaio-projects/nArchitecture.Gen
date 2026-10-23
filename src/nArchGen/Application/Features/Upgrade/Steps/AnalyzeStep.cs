using System.Text.RegularExpressions;
using Core.CrossCuttingConcerns.Exceptions;

namespace Application.Features.Upgrade.Steps;

/// <summary>Reads the current state of the solution into the report and refuses to run on something that is not a 1.x project.</summary>
public sealed partial class AnalyzeStep : IUpgradeStep
{
    public string Id => "analyze";
    public string Title => "Analyzing solution";

    public Task ApplyAsync(UpgradeContext context, CancellationToken cancellationToken)
    {
        List<string> csprojFiles = context.Files.FilesWithExtension(".csproj").ToList();
        if (csprojFiles.Count == 0)
            throw new BusinessException($"No .csproj found under \"{context.SolutionPath}\".");

        HashSet<string> frameworks = [];
        HashSet<string> coreVersions = [];
        bool hasMediatR = false,
            hasAutoMapper = false,
            usesCpm = context.Files.Exists(Path.Combine(context.SolutionPath, "Directory.Packages.props"));

        IEnumerable<string> packageFiles = csprojFiles.Concat(context.Files.FilesWithExtension(".props"));
        foreach (string file in packageFiles)
        {
            string text = context.Files.Read(file);
            MsBuildPropertyIndex properties = MsBuildPropertyIndex.Build(text);
            foreach (Match match in TargetFrameworkRegex().Matches(text))
                frameworks.Add(match.Groups[1].Value);
            foreach (Match match in PackageRegex().Matches(text))
            {
                string id = match.Groups[1].Value;
                // A version written as a property reference (e.g. Version="$(NArchitectureCoreVersion)") is resolved
                // against the same file before use; an unresolved reference is treated like a missing version, same
                // as a plain `<PackageReference Include="..." />` under central package management.
                string? version = match.Groups[2].Success ? properties.Resolve(match.Groups[2].Value) : null;
                if (version?.StartsWith('$') == true)
                    version = null;
                if (id.StartsWith("NArchitecture.Core.", StringComparison.OrdinalIgnoreCase) && version is not null)
                    coreVersions.Add(version);
                if (id.Equals("MediatR", StringComparison.OrdinalIgnoreCase))
                    hasMediatR = true;
                if (id.Equals("AutoMapper", StringComparison.OrdinalIgnoreCase))
                    hasAutoMapper = true;
            }
        }

        int profileCount = 0,
            createMapCount = 0,
            mapCallCount = 0,
            notificationCount = 0,
            mediatRUsings = 0;
        foreach (string file in context.ProjectAndTestCSharpFiles)
        {
            string text = context.Files.Read(file);
            if (text.Contains(": Profile") || text.Contains(":Profile"))
                profileCount++;
            createMapCount += CreateMapRegex().Matches(text).Count;
            mapCallCount += MapCallRegex().Matches(text).Count;
            notificationCount += NotificationRegex().Matches(text).Count;
            if (text.Contains("using MediatR;"))
                mediatRUsings++;
        }

        UpgradeReport report = context.Report;
        report.Analysis["Solution"] = context.SolutionPath;
        report.Analysis["Project"] = context.Options.ProjectName;
        report.Analysis["Target frameworks"] = frameworks.Count == 0 ? "-" : string.Join(", ", frameworks.Order());
        report.Analysis["NArchitecture.Core versions"] = coreVersions.Count == 0 ? "-" : string.Join(", ", coreVersions.Order());
        report.Analysis["Central package management"] = usesCpm ? "yes" : "no";
        report.Analysis["MediatR referenced"] = hasMediatR ? "yes" : "no";
        report.Analysis["AutoMapper referenced"] = hasAutoMapper ? "yes" : "no";
        report.Analysis["Files using MediatR"] = mediatRUsings.ToString();
        report.Analysis["AutoMapper profiles / CreateMap calls / Map calls"] = $"{profileCount} / {createMapCount} / {mapCallCount}";
        report.Analysis["INotification / Publish usages"] = notificationCount.ToString();
        report.Analysis["Test projects"] = context
            .Files.FilesWithExtension(".csproj")
            .Count(f => f.StartsWith(Path.Combine(context.SolutionPath, "tests"), StringComparison.OrdinalIgnoreCase))
            .ToString();

        bool alreadyOn2 =
            !hasMediatR
            && !hasAutoMapper
            && frameworks.All(f => f == PackageCatalog.TargetFramework)
            && coreVersions.All(v => v.StartsWith("2.", StringComparison.Ordinal));
        if (alreadyOn2 && frameworks.Count > 0)
            throw new BusinessException("This solution already targets nArchitecture 2.0 / net10.0; nothing to upgrade.");

        return Task.CompletedTask;
    }

    [GeneratedRegex(@"<TargetFramework>\s*([^<\s]+)\s*</TargetFramework>")]
    private static partial Regex TargetFrameworkRegex();

    [GeneratedRegex(@"<Package(?:Reference|Version)\s+Include=""([^""]+)""(?:\s+Version=""([^""]+)"")?")]
    private static partial Regex PackageRegex();

    [GeneratedRegex(@"CreateMap\s*<")]
    private static partial Regex CreateMapRegex();

    [GeneratedRegex(@"\.Map\s*(?:<[^;]*?>)?\s*\(")]
    private static partial Regex MapCallRegex();

    [GeneratedRegex(@"\bINotification\b|\bINotificationHandler\b|\bIPublisher\b|\.Publish\s*\(")]
    private static partial Regex NotificationRegex();
}
