using Application.Features.Generate.Commands.Crud;
using Core.CodeGen.Code.CSharp;
using Core.CodeGen.Code.CSharp.ValueObjects;
using Core.CrossCuttingConcerns.Helpers;
using Domain.ValueObjects;
using NArchitecture.Core.Mediator;
using Spectre.Console.Cli;

namespace ConsoleUI.Commands.Generate.Crud;

public partial class GenerateCrudCliCommand : AsyncCommand<GenerateCrudCliCommand.Settings>
{
    private readonly IMediator _mediator;

    public GenerateCrudCliCommand(IMediator mediator)
    {
        _mediator = mediator ?? throw new ArgumentNullException(paramName: nameof(mediator));
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        settings.CheckProjectName();
        settings.CheckEntityArgument();
        settings.CheckDbContextArgument();
        settings.CheckMechanismOptions();

        string entityPath = PlatformHelper.SecuredPathJoin(settings.ProjectPath, "Domain", "Entities", $"{settings.EntityName}.cs");
        ICollection<PropertyInfo> entityProperties = await CSharpCodeReader.ReadClassPropertiesAsync(entityPath, settings.ProjectPath);
        string entityIdType = (await CSharpCodeReader.ReadBaseClassGenericArgumentsAsync(entityPath)).First();
        GenerateCrudCommand generateCrudCommand = new()
        {
            CrudTemplateData = new CrudTemplateData
            {
                Entity = new Entity
                {
                    Name = settings.EntityName!,
                    IdType = entityIdType,
                    Properties = entityProperties.Where(property => property.AccessModifier == "public").ToArray(),
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
                OperationClaimTypeString = (settings.OperationClaimType ?? OperationClaimType.Numeric).ToString(),
            },
            ProjectPath = settings.ProjectPath,
            Force = settings.Force,
            DbContextName = settings.DbContextName!,
        };

        return await GenerateCommandRunner.RunAsync(
            command: "generate crud",
            status: "Generating...",
            settings,
            createStream: () => _mediator.CreateStream(request: generateCrudCommand, cancellationToken)
        );
    }
}
