using Application.Features.Generate.Commands.Command;
using ConsoleUI.Output;
using Domain.ValueObjects;
using NArchitecture.Core.Mediator;
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

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        settings.CheckProjectName();
        settings.CheckCommandName();
        settings.CheckFeatureName();
        settings.CheckMechanismOptions();
        settings.CheckEndPointMethod();

        GenerateCommandCommand generateCommandCommand = new()
        {
            CommandName = settings.CommandName!,
            FeatureName = settings.FeatureName!,
            ProjectPath = settings.ProjectPath,
            Force = settings.Force,
            CommandTemplateData = new CommandTemplateData
            {
                CommandName = settings.CommandName!,
                FeatureName = settings.FeatureName!,
                IsCachingUsed = settings.IsCachingUsed,
                IsLoggingUsed = settings.IsLoggingUsed,
                IsTransactionUsed = settings.IsTransactionUsed,
                IsSecuredOperationUsed = settings.IsSecuredOperationUsed,
                EndPointMethod = settings.EndPointMethod!,
            },
        };

        IAsyncEnumerable<GeneratedCommandResponse> resultsStream = _mediator.CreateStream(
            request: generateCommandCommand,
            cancellationToken
        );

        GeneratedCommandResponse? last = null;

        await CliOutput.RunWithStatusAsync(
            status: "Generating...",
            action: async setStatus =>
            {
                await foreach (GeneratedCommandResponse result in resultsStream)
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
                Command = "generate command",
                Created = CliResult.FullPaths(last?.NewFilePathsResult),
                Updated = CliResult.FullPaths(last?.UpdatedFilePathsResult),
                Warnings = last?.Warnings.ToArray() ?? [],
            }
        );

        return CliExitCodes.Success;
    }
}
