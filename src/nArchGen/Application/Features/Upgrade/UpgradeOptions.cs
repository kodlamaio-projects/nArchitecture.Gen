namespace Application.Features.Upgrade;

public sealed class UpgradeOptions
{
    /// <summary>Solution root; the folder that holds the <c>.sln</c>, <c>src/</c> and <c>tests/</c>.</summary>
    public required string SolutionPath { get; init; }

    /// <summary>Project folder name under <c>src/</c>.</summary>
    public required string ProjectName { get; init; }

    public bool DryRun { get; init; }

    /// <summary>Keep xunit 2 / VSTest instead of moving the test projects to xunit.v3 + Microsoft Testing Platform.</summary>
    public bool KeepTestFramework { get; init; }

    public bool NoBuild { get; init; }

    public IReadOnlySet<string> SkippedSteps { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    public string ProjectPath => Path.Combine(SolutionPath, "src", ProjectName);

    public bool IsSkipped(string stepId) => SkippedSteps.Contains(stepId);
}
