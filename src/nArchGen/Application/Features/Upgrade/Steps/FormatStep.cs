using Core.CodeGen.CommandLine;

namespace Application.Features.Upgrade.Steps;

/// <summary>Runs the solution's own csharpier (from its tool manifest) over the rewritten files when it is available.</summary>
public sealed class FormatStep : IUpgradeStep
{
    public string Id => "format";
    public string Title => "Formatting with csharpier";

    public async Task ApplyAsync(UpgradeContext context, CancellationToken cancellationToken)
    {
        string manifest = Path.Combine(context.SolutionPath, ".config", "dotnet-tools.json");
        if (!File.Exists(manifest) || !File.ReadAllText(manifest).Contains("\"csharpier\""))
        {
            context.Report.SkippedSteps.Add("format (no csharpier in .config/dotnet-tools.json)");
            return;
        }

        ProcessResult restore = await ProcessRunner.RunAsync("dotnet", "tool restore", context.SolutionPath, cancellationToken);
        ProcessResult format =
            restore.ExitCode == 0
                ? await ProcessRunner.RunAsync("dotnet", "csharpier format .", context.SolutionPath, cancellationToken)
                : await ProcessRunner.RunAsync("dotnet", "csharpier format .", context.SolutionPath, cancellationToken);

        if (format.ExitCode == 0)
            context.Report.Add(Title, "`dotnet csharpier format .` applied");
        else
            context.Report.SkippedSteps.Add(
                "format (`dotnet tool restore` / `dotnet csharpier format .` failed; run them yourself once the tools are installed)"
            );
    }
}
