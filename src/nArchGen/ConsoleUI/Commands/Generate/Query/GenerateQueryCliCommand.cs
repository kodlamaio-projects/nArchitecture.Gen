using Application.Features.Generate.Commands.Query;
using Domain.ValueObjects;
using NArchitecture.Core.Mediator;
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

        return await GenerateCommandRunner.RunAsync(
            command: "generate query",
            status: "Generating...",
            settings,
            createStream: () => _mediator.CreateStream(request: generateQueryCommand, cancellationToken)
        );
    }
}
