using NArchitecture.Gen.Application.Features.ProjectManagement.Commands.New;
using NArchitecture.Gen.Domain.Features.TemplateManagement.DomainServices;
using NArchitecture.Gen.Domain.Features.TemplateManagement.ValueObjects;
using NArchitecture.Core.Mediator.Abstractions;
using Spectre.Console;
using Spectre.Console.Cli;

namespace NArchitecture.Gen.ConsoleUI.Features.ProjectManagement.Commands.New;

public partial class CreateNewProjectCliCommand : AsyncCommand<CreateNewProjectCliCommand.Settings>
{
    private readonly IMediator _mediator;
    private readonly ITemplateService _templateService;

    public CreateNewProjectCliCommand(IMediator mediator, ITemplateService templateService)
    {
        _mediator = mediator ?? throw new ArgumentNullException(paramName: nameof(mediator));
        _templateService = templateService ?? throw new ArgumentNullException(paramName: nameof(templateService));
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        settings.CheckProjectNameArgument();
        await settings.CheckTemplateSelectionAsync(_templateService);

        CreateNewProjectCommand request =
            new(projectName: settings.ProjectName!, templateId: settings.TemplateId);

        IAsyncEnumerable<CreatedNewProjectResponse> resultsStream = _mediator.SendStreamAsync(request);

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
                            AnsiConsole.MarkupLine($":check_mark_button: {result.LastOperationMessage}");

                        if (result.NewFilePathsResult is not null)
                        {
                            AnsiConsole.MarkupLine(":new_button: [green]Generated files:[/]");
                            foreach (string filePath in result.NewFilePathsResult)
                                AnsiConsole.Write(new TextPath(filePath).StemColor(Color.Yellow).LeafColor(Color.Blue));
                        }

                        if (result.OutputMessage is not null)
                            AnsiConsole.MarkupLine(result.OutputMessage);
                    }
                }
            );

        return 0;
    }
}
