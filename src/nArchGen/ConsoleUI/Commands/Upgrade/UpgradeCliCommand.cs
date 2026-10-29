using Application.Features.Upgrade;
using Application.Features.Upgrade.Commands.UpgradeToV2;
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

        await AnsiConsole
            .Status()
            .Spinner(Spinner.Known.Dots2)
            .SpinnerStyle(style: Style.Parse(text: "blue"))
            .StartAsync(
                status: "Upgrading...",
                action: async ctx =>
                {
                    await foreach (UpgradedProjectResponse result in resultsStream)
                    {
                        ctx.Status(result.CurrentStatusMessage);
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
                AnsiConsole.Write(new TextPath(filePath).StemColor(Color.Yellow).LeafColor(Color.Blue));
        }
        if (last?.OutputMessage is not null)
            AnsiConsole.MarkupLine(last.OutputMessage.EscapeMarkup().Replace(":check_mark_button:", "✅").Replace(":warning:", "⚠️"));

        return last?.BuildSucceeded == false ? CliExitCodes.Failure : CliExitCodes.Success;
    }
}
