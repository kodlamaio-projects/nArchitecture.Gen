using Application.Features.Generate.Commands.DynamicQuery;
using ConsoleUI.Output;
using Core.CodeGen.Code.CSharp;
using Core.CodeGen.Code.CSharp.ValueObjects;
using Core.CrossCuttingConcerns.Helpers;
using Domain.ValueObjects;
using NArchitecture.Core.Mediator;
using Spectre.Console;
using Spectre.Console.Cli;

namespace ConsoleUI.Commands.Generate.DynamicQuery;

public partial class GenerateDynamicQueryCliCommand : AsyncCommand<GenerateDynamicQueryCliCommand.Settings>
{
    private readonly IMediator _mediator;

    public GenerateDynamicQueryCliCommand(IMediator mediator)
    {
        _mediator = mediator ?? throw new ArgumentNullException(paramName: nameof(mediator));
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        settings.CheckProjectName();
        settings.CheckEntityArgument();
        settings.CheckMechanismOptions();

        string entityPath = PlatformHelper.SecuredPathJoin(settings.ProjectPath, "Domain", "Entities", $"{settings.EntityName}.cs");
        ICollection<PropertyInfo> entityProperties = await CSharpCodeReader.ReadClassPropertiesAsync(entityPath, settings.ProjectPath);
        string entityIdType = (await CSharpCodeReader.ReadBaseClassGenericArgumentsAsync(entityPath)).First();

        GenerateDynamicQueryCommand generateDynamicQueryCommand = new()
        {
            DynamicQueryTemplateData = new DynamicQueryTemplateData
            {
                Entity = new Entity
                {
                    Name = settings.EntityName!,
                    IdType = entityIdType,
                    Properties = entityProperties.Where(property => property.AccessModifier == "public").ToArray(),
                },
                IsCachingUsed = settings.IsCachingUsed,
                IsLoggingUsed = settings.IsLoggingUsed,
                IsSecuredOperationUsed = settings.IsSecuredOperationUsed,
            },
            ProjectPath = settings.ProjectPath,
            Force = settings.Force,
        };

        IAsyncEnumerable<GeneratedDynamicQueryResponse> resultsStream = _mediator.CreateStream(
            request: generateDynamicQueryCommand,
            cancellationToken
        );

        GeneratedDynamicQueryResponse? last = null;

        await CliOutput.RunWithStatusAsync(
            status: "Generating Dynamic Query...",
            action: async setStatus =>
            {
                await foreach (GeneratedDynamicQueryResponse result in resultsStream)
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
                Command = "generate dynamic-query",
                Created = CliResult.FullPaths(last?.NewFilePathsResult),
                Updated = CliResult.FullPaths(last?.UpdatedFilePathsResult),
                Warnings = last?.Warnings.ToArray() ?? [],
            }
        );

        return CliExitCodes.Success;
    }
}
