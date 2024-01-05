using Core.CodeGen.Code;
using Core.CodeGen.File;
using Core.CrossCuttingConcerns.Exceptions;
using Core.CrossCuttingConcerns.Helpers;
using Domain.Constants;
using Spectre.Console;
using Spectre.Console.Cli;

namespace ConsoleUI.Commands.Generate.Crud;

public partial class GenerateCrudCliCommand
{
    public class Settings : CommandSettings
    {
        [CommandArgument(position: 0, template: "[EntityName]")]
        public string? EntityName { get; set; }

        [CommandArgument(position: 1, template: "[DBContextName]")]
        public string? DbContextName { get; set; }

        [CommandOption("-p|--project")]
        public string? ProjectName { get; set; }

        [CommandOption("-c|--caching")]
        public bool IsCachingUsed { get; set; }

        [CommandOption("-l|--logging")]
        public bool IsLoggingUsed { get; set; }

        [CommandOption("-t|--transaction")]
        public bool IsTransactionUsed { get; set; }

        [CommandOption("-s|--secured")]
        public bool IsSecuredOperationUsed { get; set; }

        public string ProjectPath =>
            ProjectName != null
                ? PlatformHelper.SecuredPathJoin(
                    Environment.CurrentDirectory,
                    "src",
                    ProjectName.ToCamelCase()
                )
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
            if (
                layerFolders.All(
                    folder => Directory.Exists($"{Environment.CurrentDirectory}/{folder}")
                )
            )
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
                throw new BusinessException(
                    $"No entities found in \"{ProjectPath}\\Domain\\Entities\""
                );

            EntityName = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("What's your [green]entity[/]?")
                    .PageSize(10)
                    .AddChoices(entities)
            );
        }

        public void CheckDbContextArgument()
        {
            if (DbContextName is not null)
            {
                AnsiConsole.MarkupLine(
                    $"Selected [green]DbContext[/] is [blue]{DbContextName}[/]."
                );
                return;
            }

            string[] dbContexts = Directory
                .GetFiles(
                    path: PlatformHelper.SecuredPathJoin(ProjectPath, "Persistence", "Contexts")
                )
                .Select(Path.GetFileNameWithoutExtension)
                .ToArray()!;
            if (dbContexts.Length == 0)
                throw new BusinessException(
                    $"No DbContexts found in \"{ProjectPath}\\Persistence\\Contexts\""
                );

            DbContextName = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("What's your [green]DbContext[/]?")
                    .PageSize(5)
                    .AddChoices(dbContexts)
            );
        }

        public void CheckMechanismOptions()
        {
            List<string> mechanismsToPrompt = new();

            if (IsCachingUsed)
                AnsiConsole.MarkupLine("[green]Caching[/] is used.");
            else
                mechanismsToPrompt.Add("Caching");
            if (IsLoggingUsed)
                AnsiConsole.MarkupLine("[green]Logging[/] is used.");
            else
                mechanismsToPrompt.Add("Logging");
            if (IsTransactionUsed)
                AnsiConsole.MarkupLine("[green]Transaction[/] is used.");
            else
                mechanismsToPrompt.Add("Transaction");
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
                    .InstructionsText(
                        "[grey](Press [blue]<space>[/] to toggle a mechanism, "
                            + "[green]<enter>[/] to accept)[/]"
                    )
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
                        case "Transaction":
                            IsTransactionUsed = true;
                            break;
                        case "Secured Operation":
                            IsSecuredOperationUsed = true;
                            break;
                    }
                });
        }
    }
}
