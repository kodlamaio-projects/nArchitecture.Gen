using System.Text;
using System.Text.RegularExpressions;
using Core.CodeGen.CommandLine;

namespace Application.Features.Upgrade.Steps;

/// <summary>Restores and builds the upgraded solution so the report can say where it stands.</summary>
public sealed partial class VerifyStep : IUpgradeStep
{
    public string Id => "verify";
    public string Title => "Building the upgraded solution";

    public async Task ApplyAsync(UpgradeContext context, CancellationToken cancellationToken)
    {
        ProcessResult restore = await ProcessRunner.RunAsync("dotnet", "restore", context.SolutionPath, cancellationToken);
        ProcessResult build =
            restore.ExitCode == 0
                ? await ProcessRunner.RunAsync("dotnet", "build --no-restore -c Release", context.SolutionPath, cancellationToken)
                : restore;

        List<string> errors = ErrorRegex().Matches(build.Output).Select(m => m.Value.Trim()).Distinct().ToList();

        StringBuilder summary = new();
        if (build.ExitCode == 0)
            summary.AppendLine("`dotnet build -c Release` succeeded.");
        else
        {
            summary.AppendLine(
                $"`dotnet build -c Release` failed with {errors.Count} distinct error(s). First {Math.Min(errors.Count, 20)}:"
            );
            summary.AppendLine();
            foreach (string error in errors.Take(20))
                summary.AppendLine($"- `{shorten(error, context.SolutionPath)}`");
            summary.AppendLine();
            summary.AppendLine("Resolve the manual work items above, then build again.");
        }
        context.Report.BuildSummary = summary.ToString();
        context.Report.Add(
            Title,
            build.ExitCode == 0 ? "build succeeded" : $"build failed ({errors.Count} error(s)), see the Build section"
        );
    }

    private static string shorten(string error, string solutionPath) =>
        error.Replace(solutionPath + Path.DirectorySeparatorChar, string.Empty).Replace(solutionPath, string.Empty);

    [GeneratedRegex(@"^.*\berror\s+(CS|NU|MSB|RMG)\d+.*$", RegexOptions.Multiline)]
    private static partial Regex ErrorRegex();
}
