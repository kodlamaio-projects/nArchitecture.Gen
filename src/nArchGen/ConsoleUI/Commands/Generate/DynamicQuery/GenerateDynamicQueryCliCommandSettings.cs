using ConsoleUI.Exceptions;
using Core.CrossCuttingConcerns.Helpers;
using Spectre.Console;
using Spectre.Console.Cli;

namespace ConsoleUI.Commands.Generate.DynamicQuery;

public partial class GenerateDynamicQueryCliCommand
{
    public class Settings : GenerateCliCommandSettings
    {
        [CommandArgument(position: 0, template: "[EntityName]")]
        public string? EntityName { get; set; }

        [CommandOption("-c|--caching")]
        public bool IsCachingUsed { get; set; }

        [CommandOption("-l|--logging")]
        public bool IsLoggingUsed { get; set; }

        [CommandOption("-s|--secured")]
        public bool IsSecuredOperationUsed { get; set; }

        public void CheckEntityArgument()
        {
            string entitiesPath = PlatformHelper.SecuredPathJoin(ProjectPath, "Domain", "Entities");
            string[] entities = Directory.Exists(entitiesPath)
                ? Directory.GetFiles(entitiesPath).Select(path => Path.GetFileNameWithoutExtension(path)).ToArray()
                : [];

            if (EntityName is not null)
            {
                if (!entities.Contains(EntityName))
                    throw new UsageException(
                        $"Entity \"{EntityName}\" not found in \"{entitiesPath}\". Available: {FormatChoices(entities)}."
                    );
                AnsiConsole.MarkupLine($"Selected [green]entity[/] is [blue]{EntityName}[/].");
                return;
            }

            if (entities.Length == 0)
                throw new UsageException($"No entities found in \"{entitiesPath}\".");
            EnsureInteractive($"Missing <EntityName> argument. Available: {FormatChoices(entities)}.");

            EntityName = AnsiConsole.Prompt(
                new SelectionPrompt<string>().Title("What's your [green]entity[/]?").PageSize(10).AddChoices(entities)
            );
        }

        public void CheckMechanismOptions()
        {
            List<string> mechanismsToPrompt = [];

            if (IsCachingUsed)
                AnsiConsole.MarkupLine("[green]Caching[/] is used.");
            else
                mechanismsToPrompt.Add("Caching");
            if (IsLoggingUsed)
                AnsiConsole.MarkupLine("[green]Logging[/] is used.");
            else
                mechanismsToPrompt.Add("Logging");
            if (IsSecuredOperationUsed)
                AnsiConsole.MarkupLine("[green]SecuredOperation[/] is used.");
            else
                mechanismsToPrompt.Add("Secured Operation");

            // Without a terminal to ask, mechanisms that were not passed as flags stay off.
            if (mechanismsToPrompt.Count == 0 || !IsInteractive)
                return;

            List<string> selectedMechanisms = AnsiConsole.Prompt(
                new MultiSelectionPrompt<string>()
                    .Title("What [green]mechanisms[/] do you want to use?")
                    .NotRequired()
                    .PageSize(5)
                    .MoreChoicesText("[grey](Move up and down to reveal more mechanisms)[/]")
                    .InstructionsText("[grey](Press [blue]<space>[/] to toggle a mechanism, " + "[green]<enter>[/] to accept)[/]")
                    .AddChoices(mechanismsToPrompt)
            );

            selectedMechanisms
                .ToList()
                .ForEach(mechanism =>
                {
                    switch (mechanism)
                    {
                        case "Caching":
                            IsCachingUsed = true;
                            break;
                        case "Logging":
                            IsLoggingUsed = true;
                            break;
                        case "Secured Operation":
                            IsSecuredOperationUsed = true;
                            break;
                    }
                });
        }
    }
}
