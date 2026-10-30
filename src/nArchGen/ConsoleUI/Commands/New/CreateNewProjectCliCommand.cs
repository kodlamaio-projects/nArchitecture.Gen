using Application.Features.Create.Commands.New;
using ConsoleUI.Output;
using NArchitecture.Core.Mediator;
using Spectre.Console;
using Spectre.Console.Cli;

namespace ConsoleUI.Commands.New;

public partial class CreateNewProjectCliCommand : AsyncCommand<CreateNewProjectCliCommand.Settings>
{
    private readonly IMediator _mediator;

    public CreateNewProjectCliCommand(IMediator mediator)
    {
        _mediator = mediator ?? throw new ArgumentNullException(paramName: nameof(mediator));
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        settings.CheckProjectNameArgument();
        settings.CheckIsThereSecurityMechanismArgument();

        CreateNewProjectCommand request = new(
            projectName: settings.ProjectName!,
            isThereSecurityMechanism: settings.IsThereSecurityMechanism,
            starterPath: settings.StarterPath
        );

        IAsyncEnumerable<CreatedNewProjectResponse> resultsStream = _mediator.CreateStream(request, cancellationToken);

        CreatedNewProjectResponse? last = null;

        await CliOutput.RunWithStatusAsync(
            status: "Creating...",
            action: async setStatus =>
            {
                await foreach (CreatedNewProjectResponse result in resultsStream)
                {
                    last = result;
                    setStatus(result.CurrentStatusMessage);

                    if (result.LastOperationMessage is not null)
                        AnsiConsole.MarkupLine($":check_mark_button: {result.LastOperationMessage}");

                    if (result.NewFilePathsResult is not null)
                    {
                        AnsiConsole.MarkupLine(":new_button: [green]Generated files:[/]");
                        foreach (string filePath in result.NewFilePathsResult)
                            CliOutput.WriteFilePath(filePath);
                    }

                    if (result.OutputMessage is not null)
                        AnsiConsole.MarkupLine(result.OutputMessage);
                }
            }
        );

        CliOutput.WriteResult(
            new CliResult
            {
                Command = "new",
                Created = CliResult.FullPaths(last?.NewFilePathsResult),
                Warnings = last?.Warnings.ToArray() ?? [],
                Details = new Dictionary<string, object?> { ["projectPath"] = Path.GetFullPath(settings.ProjectName!) },
            }
        );

        if (!CliOutput.IsJson)
            AnsiConsole.MarkupLine(
                $":robot: To set up AI coding agents for the project, run [blue]nArchGen ai init[/] in [blue]{settings.ProjectName!.EscapeMarkup()}[/]."
            );

        return CliExitCodes.Success;
    }
}
