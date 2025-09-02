using Application.Features.Generate.Commands.DynamicQuery;
using Core.CodeGen.Code.CSharp;
using Core.CodeGen.Code.CSharp.ValueObjects;
using Core.CrossCuttingConcerns.Helpers;
using Domain.ValueObjects;
using MediatR;
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

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        settings.CheckProjectName();
        settings.CheckEntityArgument();
        settings.CheckMechanismOptions();

        string entityPath = PlatformHelper.SecuredPathJoin(settings.ProjectPath, "Domain", "Entities", $"{settings.EntityName}.cs");
        ICollection<PropertyInfo> entityProperties = await CSharpCodeReader.ReadClassPropertiesAsync(entityPath, settings.ProjectPath);
        string entityIdType = (await CSharpCodeReader.ReadBaseClassGenericArgumentsAsync(entityPath)).First();

        GenerateDynamicQueryCommand generateDynamicQueryCommand =
            new()
            {
                DynamicQueryTemplateData = new DynamicQueryTemplateData
                {
                    Entity = new Entity
                    {
                        Name = settings.EntityName!,
                        IdType = entityIdType,
                        Properties = entityProperties.Where(property => property.AccessModifier == "public").ToArray()
                    },
                    IsCachingUsed = settings.IsCachingUsed,
                    IsLoggingUsed = settings.IsLoggingUsed,
                    IsSecuredOperationUsed = settings.IsSecuredOperationUsed
                },
                ProjectPath = settings.ProjectPath
            };

        IAsyncEnumerable<GeneratedDynamicQueryResponse> resultsStream = _mediator.CreateStream(request: generateDynamicQueryCommand);

        await AnsiConsole
            .Status()
            .Spinner(Spinner.Known.Dots2)
            .SpinnerStyle(style: Style.Parse(text: "blue"))
            .StartAsync(
                status: "Generating Dynamic Query...",
                action: async ctx =>
                {
                    await foreach (GeneratedDynamicQueryResponse result in resultsStream)
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
