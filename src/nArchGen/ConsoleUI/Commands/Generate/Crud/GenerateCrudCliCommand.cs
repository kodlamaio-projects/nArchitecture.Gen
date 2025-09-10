﻿using Application.Features.Generate.Commands.Crud;
using Core.CodeGen.Code.CSharp;
using Core.CodeGen.Code.CSharp.ValueObjects;
using Core.CrossCuttingConcerns.Helpers;
using Domain.ValueObjects;
using MediatR;
using Spectre.Console;
using Spectre.Console.Cli;

namespace ConsoleUI.Commands.Generate.Crud;

public partial class GenerateCrudCliCommand : AsyncCommand<GenerateCrudCliCommand.Settings>
{
    private readonly IMediator _mediator;

    public GenerateCrudCliCommand(IMediator mediator)
    {
        _mediator = mediator ?? throw new ArgumentNullException(paramName: nameof(mediator));
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        settings.CheckProjectName();
        settings.CheckEntityArgument();
        settings.CheckDbContextArgument();
        settings.CheckMechanismOptions();

        string entityPath = PlatformHelper.SecuredPathJoin(settings.ProjectPath, "Domain", "Entities", $"{settings.EntityName}.cs");
        ICollection<PropertyInfo> entityProperties = await CSharpCodeReader.ReadClassPropertiesAsync(entityPath, settings.ProjectPath);
        string entityIdType = (await CSharpCodeReader.ReadBaseClassGenericArgumentsAsync(entityPath)).First();
        GenerateCrudCommand generateCrudCommand =
            new()
            {
                CrudTemplateData = new CrudTemplateData
                {
                    Entity = new Entity
                    {
                        Name = settings.EntityName!,
                        IdType = entityIdType,
                        Properties = entityProperties.Where(property => property.AccessModifier == "public").ToArray()
                    },
                    IsCachingUsed = settings.IsCachingUsed,
                    IsLoggingUsed = settings.IsLoggingUsed,
                    IsTransactionUsed = settings.IsTransactionUsed,
                    IsSecuredOperationUsed = settings.IsSecuredOperationUsed,
                    IsDynamicQueryUsed = settings.IsDynamicQueryUsed,
                    IsMenuRoleIncluded = settings.IsMenuRoleIncluded,
                    CustomOperationClaimPath = settings.CustomOperationClaimPath,
                    DbContextName = settings.DbContextName!,
                    OperationClaimType = settings.OperationClaimType ?? OperationClaimType.Numeric,
                    OperationClaimTypeString = (settings.OperationClaimType ?? OperationClaimType.Numeric).ToString()
                },
                ProjectPath = settings.ProjectPath,
                DbContextName = settings.DbContextName!
            };

        IAsyncEnumerable<GeneratedCrudResponse> resultsStream = _mediator.CreateStream(request: generateCrudCommand);

        await AnsiConsole
            .Status()
            .Spinner(Spinner.Known.Dots2)
            .SpinnerStyle(style: Style.Parse(text: "blue"))
            .StartAsync(
                status: "Generating...",
                action: async ctx =>
                {
                    await foreach (GeneratedCrudResponse result in resultsStream)
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
