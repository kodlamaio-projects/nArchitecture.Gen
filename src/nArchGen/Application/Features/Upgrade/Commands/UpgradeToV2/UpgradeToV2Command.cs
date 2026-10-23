using System.Runtime.CompilerServices;
using Application.Features.Upgrade.Steps;
using Core.CodeGen.CommandLine;
using Core.CrossCuttingConcerns.Exceptions;
using NArchitecture.Core.Mediator;

namespace Application.Features.Upgrade.Commands.UpgradeToV2;

public class UpgradeToV2Command : IStreamRequest<UpgradedProjectResponse>
{
    public required UpgradeOptions Options { get; init; }
    public bool Force { get; init; }

    public class UpgradeToV2CommandHandler : IStreamRequestHandler<UpgradeToV2Command, UpgradedProjectResponse>
    {
        public const string ReportFileName = "MIGRATION-REPORT.md";

        public static readonly string[] StepIds = ["project-files", "mediator", "automapper", "tests", "web-api", "format", "verify"];

        public async IAsyncEnumerable<UpgradedProjectResponse> Handle(
            UpgradeToV2Command request,
            [EnumeratorCancellation] CancellationToken cancellationToken
        )
        {
            UpgradeOptions options = request.Options;
            UpgradedProjectResponse response = new();

            if (!options.DryRun && !request.Force)
            {
                ProcessResult status = await ProcessRunner.RunAsync("git", "status --porcelain", options.SolutionPath, cancellationToken);
                if (status.ExitCode == 0 && !string.IsNullOrWhiteSpace(status.Output))
                    throw new BusinessException("The git working tree is not clean. Commit or stash your changes first, or pass --force.");
            }

            response.CurrentStatusMessage = "Loading solution...";
            yield return response;
            UpgradeContext context = new()
            {
                Options = options,
                Files = await UpgradeFileSet.LoadAsync(options.SolutionPath),
                Report = new UpgradeReport { DryRun = options.DryRun },
            };
            response.LastOperationMessage = $"Loaded {context.Files.Files.Count()} files.";

            IUpgradeStep[] steps =
            [
                new AnalyzeStep(),
                new ProjectFilesStep(),
                new MediatorStep(),
                new AutoMapperStep(),
                new TestsStep(),
                new WebApiStep(),
            ];
            foreach (IUpgradeStep step in steps)
            {
                if (options.IsSkipped(step.Id))
                {
                    context.Report.SkippedSteps.Add($"{step.Id} ({step.Title})");
                    continue;
                }
                response.CurrentStatusMessage = $"{step.Title}...";
                yield return response;
                await step.ApplyAsync(context, cancellationToken);
                response.LastOperationMessage = $"{step.Title}: done.";
            }

            List<string> changed = context.Files.ChangedFiles.ToList();
            if (options.DryRun)
            {
                response.LastOperationMessage = $"Dry run: {changed.Count} file(s) would change.";
            }
            else
            {
                response.CurrentStatusMessage = "Writing files...";
                yield return response;
                await context.Files.FlushAsync();
                response.LastOperationMessage = $"{changed.Count} file(s) written.";

                if (!options.IsSkipped("format"))
                {
                    FormatStep format = new();
                    response.CurrentStatusMessage = $"{format.Title}...";
                    yield return response;
                    await format.ApplyAsync(context, cancellationToken);
                    response.LastOperationMessage = $"{format.Title}: done.";
                }

                if (!options.NoBuild && !options.IsSkipped("verify"))
                {
                    VerifyStep verify = new();
                    response.CurrentStatusMessage = $"{verify.Title}...";
                    yield return response;
                    await verify.ApplyAsync(context, cancellationToken);
                    response.BuildSucceeded = context.Report.BuildSummary?.Contains("succeeded") == true;
                    response.LastOperationMessage = $"{verify.Title}: done.";
                }
                else
                    context.Report.SkippedSteps.Add("verify (build)");
            }

            string reportPath = Path.Combine(options.SolutionPath, ReportFileName);
            await File.WriteAllTextAsync(reportPath, context.Report.Render(options.SolutionPath), cancellationToken);

            response.CurrentStatusMessage = "Completed.";
            response.ChangedFilePathsResult = changed;
            response.ReportFilePath = reportPath;
            response.ManualWorkCount = context.Report.ManualWork.Count;
            response.OutputMessage =
                context.Report.ManualWork.Count == 0
                    ? $":check_mark_button: No manual work left. Report: {reportPath}"
                    : $":warning: {context.Report.ManualWork.Count} item(s) need manual work. Report: {reportPath}";
            yield return response;
        }
    }
}
