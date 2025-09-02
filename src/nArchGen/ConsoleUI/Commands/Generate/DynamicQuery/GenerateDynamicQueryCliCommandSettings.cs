using Core.CodeGen.Code;
using Core.CrossCuttingConcerns.Exceptions;
using Core.CrossCuttingConcerns.Helpers;
using Spectre.Console;
using Spectre.Console.Cli;

namespace ConsoleUI.Commands.Generate.DynamicQuery;

public partial class GenerateDynamicQueryCliCommand
{
    public class Settings : CommandSettings
    {
        [CommandArgument(position: 0, template: "[EntityName]")]
        public string? EntityName { get; set; }

        [CommandOption("-p|--project")]
        public string? ProjectName { get; set; }

        [CommandOption("-c|--caching")]
        public bool IsCachingUsed { get; set; }

        [CommandOption("-l|--logging")]
        public bool IsLoggingUsed { get; set; }

        [CommandOption("-s|--secured")]
        public bool IsSecuredOperationUsed { get; set; }

        public string ProjectPath =>
            ProjectName != null
                ? PlatformHelper.SecuredPathJoin(Environment.CurrentDirectory, "src", ProjectName.ToCamelCase())
                : Environment.CurrentDirectory;

        public void CheckProjectName()
        {
            if (ProjectName != null)
            {
                if (!Directory.Exists(ProjectPath))
                    throw new BusinessException($"Project not found in \"{ProjectPath}\".");
                AnsiConsole.MarkupLine($"Selected [green]project[/] is [blue]{ProjectName}[/].");
                return;
            }

            string[] layerFolders = { "Application", "Domain", "Persistence", "WebAPI" };
            if (layerFolders.All(folder => Directory.Exists($"{Environment.CurrentDirectory}/{folder}")))
                return;

            string[] projects = Directory
                .GetDirectories($"{Environment.CurrentDirectory}/src")
                .Select(Path.GetFileName)
                .Where(project => project != "corePackages")
                .ToArray()!;
            if (projects.Length == 0)
                throw new BusinessException("No projects found in src");
            if (projects.Length == 1)
            {
                ProjectName = projects.First();
                AnsiConsole.MarkupLine($"Selected [green]project[/] is [blue]{ProjectName}[/].");
                return;
            }

            ProjectName = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("What's your [green]project[/] in [blue]src[/] folder?")
                    .PageSize(10)
                    .AddChoices(projects)
            );
        }

        public void CheckEntityArgument()
        {
            if (EntityName is not null)
            {
                AnsiConsole.MarkupLine($"Selected [green]entity[/] is [blue]{EntityName}[/].");
                return;
            }

            string[] entities = Directory
                .GetFiles(path: PlatformHelper.SecuredPathJoin(ProjectPath, "Domain", "Entities"))
                .Select(Path.GetFileNameWithoutExtension)
                .ToArray()!;
            if (entities.Length == 0)
                throw new BusinessException($"No entities found in \"{ProjectPath}\\Domain\\Entities\"");

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

            if (mechanismsToPrompt.Count == 0)
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
