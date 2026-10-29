using Application.Features.Generate.Commands.Command;
using Domain.ValueObjects;
using NArchitecture.Core.Mediator;
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

        return await GenerateCommandRunner.RunAsync(
            command: "generate command",
            status: "Generating...",
            settings,
            createStream: () => _mediator.CreateStream(request: generateCommandCommand, cancellationToken)
        );
    }
}
