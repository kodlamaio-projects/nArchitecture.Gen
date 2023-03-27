using Application.Features.Generate.Commands.Command;
using Domain.ValueObjects;
using MediatR;
using Spectre.Console;
using Spectre.Console.Cli;

namespace ConsoleUI.Commands.Generate.Command;

public partial class GenerateCommandCliCommand : AsyncCommand<GenerateCommandCliCommand.Settings>
{
    private readonly IMediator _mediator;

    public GenerateCommandCliCommand(IMediator mediator)
    {
        _mediator = mediator ?? throw new ArgumentNullException(paramName: nameof(mediator));
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        settings.CheckProjectName();
        settings.CheckCommandName();
        settings.CheckFeatureName();
        settings.CheckMechanismOptions();
        settings.CheckEndPointMethod();

        GenerateCommandCommand generateCommandCommand =
            new()
            {
                CommandName = settings.CommandName!,
                FeatureName = settings.FeatureName!,
                ProjectPath = settings.ProjectPath,
                CommandTemplateData = new CommandTemplateData
                {
                    CommandName = settings.CommandName!,
                    FeatureName = settings.FeatureName!,
                    IsCachingUsed = settings.IsCachingUsed,
                    IsLoggingUsed = settings.IsLoggingUsed,
                    IsTransactionUsed = settings.IsTransactionUsed,
                    IsSecuredOperationUsed = settings.IsSecuredOperationUsed,
                    EndPointMethod = settings.EndPointMethod!
                }
            };

        IAsyncEnumerable<GeneratedCommandResponse> resultsStream = _mediator.CreateStream(
            request: generateCommandCommand
        );

        await AnsiConsole
            .Status()
            .Spinner(Spinner.Known.Dots2)
            .SpinnerStyle(style: Style.Parse(text: "blue"))
            .StartAsync(
                status: "Generating...",
                action: async ctx =>
                {
                    await foreach (GeneratedCommandResponse result in resultsStream)
                    {
                        ctx.Status(result.CurrentStatusMessage);

                        if (result.OutputMessage is not null)
                            AnsiConsole.MarkupLine(result.OutputMessage);

                        if (result.LastOperationMessage is not null)
                            AnsiConsole.MarkupLine(
                                $":check_mark_button: {result.LastOperationMessage}"
                            );

                        if (result.NewFilePathsResult is not null)
                        {
                            AnsiConsole.MarkupLine(":new_button: [green]Generated files:[/]");
                            foreach (string filePath in result.NewFilePathsResult)
                                AnsiConsole.Write(
                                    new TextPath(filePath)
                                        .StemColor(Color.Yellow)
                                        .LeafColor(Color.Blue)
                                );
                        }

                        if (result.UpdatedFilePathsResult is not null)
                        {
                            AnsiConsole.MarkupLine(":up_button: [green]Updated files:[/]");
                            foreach (string filePath in result.UpdatedFilePathsResult)
                                AnsiConsole.Write(
                                    new TextPath(filePath)
                                        .StemColor(Color.Yellow)
                                        .LeafColor(Color.Blue)
                                );
                        }
                    }
                }
            );

        return 0;
    }
}
