namespace Application.Features.Upgrade;

public sealed class UpgradeContext
{
    public required UpgradeOptions Options { get; init; }
    public required UpgradeFileSet Files { get; init; }
    public required UpgradeReport Report { get; init; }

    public string SolutionPath => Options.SolutionPath;
    public string ProjectPath => Options.ProjectPath;

    public IEnumerable<string> ProjectCSharpFiles =>
        Files.CSharpFiles.Where(f => f.StartsWith(ProjectPath, StringComparison.OrdinalIgnoreCase));

    public IEnumerable<string> TestCSharpFiles =>
        Files.CSharpFiles.Where(f => f.StartsWith(Path.Combine(SolutionPath, "tests"), StringComparison.OrdinalIgnoreCase));

    public IEnumerable<string> ProjectAndTestCSharpFiles => ProjectCSharpFiles.Concat(TestCSharpFiles);
}

/// <summary>One unit of the upgrade. Steps mutate <see cref="UpgradeContext.Files"/> and describe themselves in the report.</summary>
public interface IUpgradeStep
{
    /// <summary>Stable id used by <c>--skip</c>.</summary>
    string Id { get; }

    string Title { get; }

    Task ApplyAsync(UpgradeContext context, CancellationToken cancellationToken);
}
