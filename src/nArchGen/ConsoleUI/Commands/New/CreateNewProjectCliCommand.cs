using Application.Features.Create.Commands.New;
using MediatR;
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

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        settings.CheckProjectNameArgument();
        settings.CheckIsThereSecurityMechanismArgument();

        CreateNewProjectCommand request =
            new(
                projectName: settings.ProjectName!,
                isThereSecurityMechanism: settings.IsThereSecurityMechanism
            );

        IAsyncEnumerable<CreatedNewProjectResponse> resultsStream = _mediator.CreateStream(request);

        await AnsiConsole
            .Status()
            .Spinner(Spinner.Known.Dots2)
            .SpinnerStyle(style: Style.Parse(text: "blue"))
            .StartAsync(
                status: "Creating...",
                action: async ctx =>
                {
                    await foreach (CreatedNewProjectResponse result in resultsStream)
                    {
                        ctx.Status(result.CurrentStatusMessage);

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

                        if (result.OutputMessage is not null)
                            AnsiConsole.MarkupLine(result.OutputMessage);
                    }
                }
            );

        return 0;
    }
}
