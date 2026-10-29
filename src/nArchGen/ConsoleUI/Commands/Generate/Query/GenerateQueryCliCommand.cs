using Application.Features.Generate.Commands.Query;
using ConsoleUI.Output;
using Domain.ValueObjects;
using NArchitecture.Core.Mediator;
using Spectre.Console;
using Spectre.Console.Cli;

namespace ConsoleUI.Commands.Generate.Query;

public partial class GenerateQueryCliCommand : AsyncCommand<GenerateQueryCliCommand.Settings>
{
    private readonly IMediator _mediator;

    public GenerateQueryCliCommand(IMediator mediator)
    {
        _mediator = mediator ?? throw new ArgumentNullException(paramName: nameof(mediator));
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        settings.CheckQueryName();
        settings.CheckProjectName();
        settings.CheckFeatureName();
        settings.CheckMechanismOptions();

        GenerateQueryCommand generateQueryCommand = new()
        {
            QueryName = settings.QueryName!,
            FeatureName = settings.FeatureName!,
            ProjectPath = settings.ProjectPath,
            Force = settings.Force,
            QueryTemplateData = new QueryTemplateData
            {
                QueryName = settings.QueryName!,
                FeatureName = settings.FeatureName!,
                IsCachingUsed = settings.IsCachingUsed,
                IsLoggingUsed = settings.IsLoggingUsed,
                IsSecuredOperationUsed = settings.IsSecuredOperationUsed,
            },
        };

        IAsyncEnumerable<GeneratedQueryResponse> resultsStream = _mediator.CreateStream(request: generateQueryCommand, cancellationToken);

        GeneratedQueryResponse? last = null;

        await CliOutput.RunWithStatusAsync(
            status: "Generating...",
            action: async setStatus =>
            {
                await foreach (GeneratedQueryResponse result in resultsStream)
                {
                    last = result;
                    setStatus(result.CurrentStatusMessage);

                    if (result.OutputMessage is not null)
                        AnsiConsole.MarkupLine(result.OutputMessage);

                    if (result.LastOperationMessage is not null)
                        AnsiConsole.MarkupLine($":check_mark_button: {result.LastOperationMessage}");

                    if (result.NewFilePathsResult is not null)
                    {
                        AnsiConsole.MarkupLine(":new_button: [green]Generated files:[/]");
                        foreach (string filePath in result.NewFilePathsResult)
                            CliOutput.WriteFilePath(filePath);
                    }

                    if (result.UpdatedFilePathsResult is not null)
                    {
                        AnsiConsole.MarkupLine(":up_button: [green]Updated files:[/]");
                        foreach (string filePath in result.UpdatedFilePathsResult)
                            CliOutput.WriteFilePath(filePath);
                    }
                }
            }
        );

        CliOutput.WriteResult(
            new CliResult
            {
                Command = "generate query",
                Created = CliResult.FullPaths(last?.NewFilePathsResult),
                Updated = CliResult.FullPaths(last?.UpdatedFilePathsResult),
                Warnings = last?.Warnings.ToArray() ?? [],
            }
        );

        return CliExitCodes.Success;
    }
}
