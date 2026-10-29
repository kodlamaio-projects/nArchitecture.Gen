using ConsoleUI.Exceptions;
using Spectre.Console;
using Spectre.Console.Cli;

namespace ConsoleUI.Commands.Generate.Query;

public partial class GenerateQueryCliCommand
{
    public class Settings : GenerateCliCommandSettings
    {
        [CommandArgument(position: 0, template: "[QueryName]")]
        public string? QueryName { get; set; }

        [CommandArgument(position: 1, template: "[FeatureName]")]
        public string? FeatureName { get; set; }

        [CommandOption("-c|--caching")]
        public bool IsCachingUsed { get; set; }

        [CommandOption("-l|--logging")]
        public bool IsLoggingUsed { get; set; }

        [CommandOption("-s|--secured")]
        public bool IsSecuredOperationUsed { get; set; }

        public void CheckQueryName()
        {
            if (QueryName is not null)
            {
                AnsiConsole.MarkupLine($"[green]Query[/] name is [blue]{QueryName}[/].");
                return;
            }

            EnsureInteractive("Missing <QueryName> argument.");
            QueryName = AnsiConsole.Prompt(new TextPrompt<string>("[blue]What is [green]new query name[/]?[/]"));
        }

        public void CheckFeatureName()
        {
            string featuresPath = $"{ProjectPath}/Application/Features";
            string[] features = Directory.Exists(featuresPath)
                ? Directory.GetDirectories(featuresPath).Select(path => Path.GetFileName(path)).ToArray()
                : [];

            if (FeatureName is not null)
            {
                if (!features.Contains(FeatureName))
                    throw new UsageException(
                        $"Feature \"{FeatureName}\" not found in \"{featuresPath}\". Available: {FormatChoices(features)}."
                    );
                AnsiConsole.MarkupLine($"[green]Feature[/] name that the query will be in is [blue]{FeatureName}[/].");
                return;
            }

            if (features.Length == 0)
                throw new UsageException($"No feature found in \"{featuresPath}\".");
            EnsureInteractive($"Missing <FeatureName> argument. Available: {FormatChoices(features)}.");

            FeatureName = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("[blue]Which is [green]feature name[/] that the command will be in?[/]")
                    .PageSize(10)
                    .AddChoices(features)
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
