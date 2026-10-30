using Application.Features.Ai.Commands.Init;
using ConsoleUI.Commands.Generate;
using NArchitecture.Core.Mediator;
using Spectre.Console.Cli;

namespace ConsoleUI.Commands.Ai.Init;

public partial class AiInitCliCommand : AsyncCommand<AiInitCliCommand.Settings>
{
    private readonly IMediator _mediator;

    public AiInitCliCommand(IMediator mediator)
    {
        _mediator = mediator ?? throw new ArgumentNullException(paramName: nameof(mediator));
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        settings.CheckSolutionRoot();
        settings.CheckToolsOption();
        settings.CheckCustomFileOption();
        settings.CheckSkillsOption();

        InitAiCommand initAiCommand = new()
        {
            SolutionPath = settings.SolutionPath,
            Tools = settings.SelectedTools,
            CustomPath = settings.CustomFile,
            IncludeSkills = settings.IncludeSkills,
            Force = settings.Force,
        };

        return await GenerateCommandRunner.RunAsync(
            command: "ai init",
            status: "Writing...",
            settings.DryRun,
            createStream: () => _mediator.CreateStream(request: initAiCommand, cancellationToken)
        );
    }
}
