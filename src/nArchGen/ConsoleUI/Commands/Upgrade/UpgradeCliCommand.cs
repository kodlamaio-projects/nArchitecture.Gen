using Application.Features.Upgrade;
using Application.Features.Upgrade.Commands.UpgradeToV2;
using ConsoleUI.Output;
using NArchitecture.Core.Mediator;
using Spectre.Console;
using Spectre.Console.Cli;

namespace ConsoleUI.Commands.Upgrade;

public partial class UpgradeCliCommand : AsyncCommand<UpgradeCliCommand.Settings>
{
    private readonly IMediator _mediator;

    public UpgradeCliCommand(IMediator mediator)
    {
        _mediator = mediator ?? throw new ArgumentNullException(paramName: nameof(mediator));
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        settings.CheckProjectName();

        UpgradeToV2Command request = new()
        {
            Options = new UpgradeOptions
            {
                SolutionPath = settings.SolutionPath,
                ProjectName = settings.ProjectName!,
                DryRun = settings.DryRun,
                KeepTestFramework = settings.KeepTestFramework,
                NoBuild = settings.NoBuild,
                SkippedSteps = settings.SkippedSteps,
            },
            Force = settings.Force,
        };

        IAsyncEnumerable<UpgradedProjectResponse> resultsStream = _mediator.CreateStream(request, cancellationToken);
        UpgradedProjectResponse? last = null;

        await CliOutput.RunWithStatusAsync(
            status: "Upgrading...",
            action: async setStatus =>
            {
                await foreach (UpgradedProjectResponse result in resultsStream)
                {
                    setStatus(result.CurrentStatusMessage);
                    if (result.LastOperationMessage is not null)
                        AnsiConsole.MarkupLine($":check_mark_button: {result.LastOperationMessage.EscapeMarkup()}");
                    last = result;
                }
            }
        );

        if (last?.ChangedFilePathsResult is { Count: > 0 } changed)
        {
            AnsiConsole.MarkupLine(settings.DryRun ? ":memo: [yellow]Files that would change:[/]" : ":memo: [green]Changed files:[/]");
            foreach (string filePath in changed.Order())
                CliOutput.WriteFilePath(filePath);
        }
        if (last?.OutputMessage is not null)
            AnsiConsole.MarkupLine(last.OutputMessage.EscapeMarkup().Replace(":check_mark_button:", "✅").Replace(":warning:", "⚠️"));

        bool buildFailed = last?.BuildSucceeded == false;
        int exitCode = buildFailed ? CliExitCodes.Failure : CliExitCodes.Success;
        CliOutput.WriteResult(
            new CliResult
            {
                Command = "upgrade",
                ExitCode = exitCode,
                Updated = settings.DryRun ? [] : CliResult.FullPaths(last?.ChangedFilePathsResult),
                Warnings = last?.Warnings.ToArray() ?? [],
                Error = buildFailed ? new CliError("failure", "The upgraded solution does not build; see the report.", []) : null,
                Details = new Dictionary<string, object?>
                {
                    ["dryRun"] = settings.DryRun,
                    // With --dry-run nothing is written; these are the files the upgrade would change.
                    ["changesPlanned"] = settings.DryRun ? CliResult.FullPaths(last?.ChangedFilePathsResult) : [],
                    ["buildSucceeded"] = last?.BuildSucceeded,
                    ["manualWorkCount"] = last?.ManualWorkCount ?? 0,
                    ["reportFile"] = last?.ReportFilePath is null ? null : Path.GetFullPath(last.ReportFilePath),
                },
            }
        );

        return exitCode;
    }
}
