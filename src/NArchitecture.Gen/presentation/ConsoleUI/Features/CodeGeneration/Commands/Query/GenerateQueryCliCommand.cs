using NArchitecture.Gen.Application.Features.CodeGeneration.Commands.Query;
using NArchitecture.Gen.Domain.Features.CodeGeneration.ValueObjects;
using NArchitecture.Core.Mediator.Abstractions;
using Spectre.Console;
using Spectre.Console.Cli;

namespace NArchitecture.Gen.ConsoleUI.Features.CodeGeneration.Commands.Query;

public partial class GenerateQueryCliCommand : AsyncCommand<GenerateQueryCliCommand.Settings>
{
    private readonly IMediator _mediator;

    public GenerateQueryCliCommand(IMediator mediator)
    {
        _mediator = mediator ?? throw new ArgumentNullException(paramName: nameof(mediator));
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        settings.CheckQueryName();
        settings.CheckProjectName();
        settings.CheckFeatureName();
        settings.CheckMechanismOptions();

        GenerateQueryCommand generateQueryCommand =
            new()
            {
                QueryName = settings.QueryName!,
                FeatureName = settings.FeatureName!,
                ProjectPath = settings.ProjectPath,
                QueryTemplateData = new QueryTemplateData
                {
                    QueryName = settings.QueryName!,
                    FeatureName = settings.FeatureName!,
                    IsCachingUsed = settings.IsCachingUsed,
                    IsLoggingUsed = settings.IsLoggingUsed,
                    IsSecuredOperationUsed = settings.IsSecuredOperationUsed
                }
            };

        IAsyncEnumerable<GeneratedQueryResponse> resultsStream = _mediator.SendStreamAsync(generateQueryCommand);

        await AnsiConsole
            .Status()
            .Spinner(Spinner.Known.Dots2)
            .SpinnerStyle(style: Style.Parse(text: "blue"))
            .StartAsync(
                status: "Generating...",
                action: async ctx =>
                {
                    await foreach (GeneratedQueryResponse result in resultsStream)
                    {
                        ctx.Status(result.CurrentStatusMessage);

                        if (result.OutputMessage is not null)
                            AnsiConsole.MarkupLine(result.OutputMessage);

                        if (result.LastOperationMessage is not null)
                            AnsiConsole.MarkupLine($":check_mark_button: {result.LastOperationMessage}");

                        if (result.NewFilePathsResult is not null)
                        {
                            AnsiConsole.MarkupLine(":new_button: [green]Generated files:[/]");
                            foreach (string filePath in result.NewFilePathsResult)
                                AnsiConsole.Write(new TextPath(filePath).StemColor(Color.Yellow).LeafColor(Color.Blue));
                        }

                        if (result.UpdatedFilePathsResult is not null)
                        {
                            AnsiConsole.MarkupLine(":up_button: [green]Updated files:[/]");
                            foreach (string filePath in result.UpdatedFilePathsResult)
                                AnsiConsole.Write(new TextPath(filePath).StemColor(Color.Yellow).LeafColor(Color.Blue));
                        }
                    }
                }
            );

        return 0;
    }
}
