using ConsoleUI.Exceptions;
using Core.CrossCuttingConcerns.Helpers;
using Domain.ValueObjects;
using Spectre.Console;
using Spectre.Console.Cli;

namespace ConsoleUI.Commands.Generate.Crud;

public partial class GenerateCrudCliCommand
{
    public class Settings : GenerateCliCommandSettings
    {
        [CommandArgument(position: 0, template: "[EntityName]")]
        public string? EntityName { get; set; }

        [CommandArgument(position: 1, template: "[DBContextName]")]
        public string? DbContextName { get; set; }

        [CommandOption("-c|--caching")]
        public bool IsCachingUsed { get; set; }

        [CommandOption("-l|--logging")]
        public bool IsLoggingUsed { get; set; }

        [CommandOption("-t|--transaction")]
        public bool IsTransactionUsed { get; set; }

        [CommandOption("-s|--secured")]
        public bool IsSecuredOperationUsed { get; set; }

        [CommandOption("-d|--dynamic-query")]
        public bool IsDynamicQueryUsed { get; set; }

        [CommandOption("-m|--menu-role")]
        public bool IsMenuRoleIncluded { get; set; }

        [CommandOption("--operation-claim-path")]
        public string? CustomOperationClaimPath { get; set; }

        [CommandOption("--operation-claim-type")]
        public OperationClaimType? OperationClaimType { get; set; }

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

        public void CheckDbContextArgument()
        {
            string contextsPath = PlatformHelper.SecuredPathJoin(ProjectPath, "Persistence", "Contexts");
            string[] dbContexts = Directory.Exists(contextsPath)
                ? Directory.GetFiles(contextsPath).Select(path => Path.GetFileNameWithoutExtension(path)).ToArray()
                : [];

            if (DbContextName is not null)
            {
                if (!dbContexts.Contains(DbContextName))
                    throw new UsageException(
                        $"DbContext \"{DbContextName}\" not found in \"{contextsPath}\". Available: {FormatChoices(dbContexts)}."
                    );
                AnsiConsole.MarkupLine($"Selected [green]DbContext[/] is [blue]{DbContextName}[/].");
                return;
            }

            if (dbContexts.Length == 0)
                throw new UsageException($"No DbContexts found in \"{contextsPath}\".");
            EnsureInteractive($"Missing <DBContextName> argument. Available: {FormatChoices(dbContexts)}.");

            DbContextName = AnsiConsole.Prompt(
                new SelectionPrompt<string>().Title("What's your [green]DbContext[/]?").PageSize(5).AddChoices(dbContexts)
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
            if (IsTransactionUsed)
                AnsiConsole.MarkupLine("[green]Transaction[/] is used.");
            else
                mechanismsToPrompt.Add("Transaction");
            if (IsSecuredOperationUsed)
                AnsiConsole.MarkupLine("[green]SecuredOperation[/] is used.");
            else
                mechanismsToPrompt.Add("Secured Operation");
            if (IsDynamicQueryUsed)
                AnsiConsole.MarkupLine("[green]Dynamic Query[/] is used.");
            else
                mechanismsToPrompt.Add("Dynamic Query");
            if (IsMenuRoleIncluded)
                AnsiConsole.MarkupLine("[green]Menu Role[/] will be included.");
            else
                mechanismsToPrompt.Add("Menu Role");

            // Without a terminal to ask, mechanisms that were not passed as flags stay off.
            if (mechanismsToPrompt.Count == 0 || !IsInteractive)
            {
                CheckCustomOperationClaimPath();
                CheckOperationClaimType();
                return;
            }

            List<string> selectedMechanisms = AnsiConsole.Prompt(
                new MultiSelectionPrompt<string>()
                    .Title("What [green]mechanisms[/] do you want to use?")
                    .NotRequired()
                    .PageSize(6)
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
                        case "Transaction":
                            IsTransactionUsed = true;
                            break;
                        case "Secured Operation":
                            IsSecuredOperationUsed = true;
                            break;
                        case "Dynamic Query":
                            IsDynamicQueryUsed = true;
                            break;
                        case "Menu Role":
                            IsMenuRoleIncluded = true;
                            break;
                    }
                });

            CheckCustomOperationClaimPath();
            CheckOperationClaimType();
        }

        public void CheckCustomOperationClaimPath()
        {
            if (CustomOperationClaimPath is not null)
            {
                AnsiConsole.MarkupLine($"Custom Operation Claim Path: [blue]{CustomOperationClaimPath}[/].");
                return;
            }

            // Without a terminal to ask, the default configuration path is used.
            if (!IsInteractive)
                return;

            bool useCustomPath = AnsiConsole.Confirm("Do you want to use a [green]custom path[/] for Operation Claims Configuration?");

            if (useCustomPath)
            {
                CustomOperationClaimPath = AnsiConsole.Ask<string>("Enter the [green]custom path[/] for OperationClaimConfiguration.cs:");
                AnsiConsole.MarkupLine($"Custom Operation Claim Path: [blue]{CustomOperationClaimPath}[/].");
            }
        }

        public void CheckOperationClaimType()
        {
            // Without a terminal to ask, the default numeric claim ids are used.
            if (!OperationClaimType.HasValue && !IsInteractive)
                OperationClaimType = Domain.ValueObjects.OperationClaimType.Numeric;

            if (OperationClaimType.HasValue)
            {
                AnsiConsole.MarkupLine($"Operation Claim Type: [blue]{OperationClaimType.Value}[/].");
                return;
            }

            OperationClaimType = AnsiConsole.Prompt(
                new SelectionPrompt<OperationClaimType>()
                    .Title("What's your [green]Operation Claim type[/]?")
                    .PageSize(3)
                    .AddChoices(Domain.ValueObjects.OperationClaimType.Numeric, Domain.ValueObjects.OperationClaimType.Guid)
            );

            AnsiConsole.MarkupLine($"Operation Claim Type: [blue]{OperationClaimType.Value}[/].");
        }
    }
}
