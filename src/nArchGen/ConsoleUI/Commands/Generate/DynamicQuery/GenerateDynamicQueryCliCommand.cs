using Application.Features.Generate.Commands.DynamicQuery;
using Core.CodeGen.Code.CSharp;
using Core.CodeGen.Code.CSharp.ValueObjects;
using Core.CrossCuttingConcerns.Helpers;
using Domain.ValueObjects;
using NArchitecture.Core.Mediator;
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

        return await GenerateCommandRunner.RunAsync(
            command: "generate dynamic-query",
            status: "Generating Dynamic Query...",
            settings.DryRun,
            createStream: () => _mediator.CreateStream(request: generateDynamicQueryCommand, cancellationToken)
        );
    }
}
