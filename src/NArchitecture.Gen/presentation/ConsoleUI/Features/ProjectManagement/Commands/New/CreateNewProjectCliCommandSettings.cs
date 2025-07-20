using NArchitecture.Gen.Domain.Features.TemplateManagement.DomainServices;
using NArchitecture.Gen.Domain.Features.TemplateManagement.ValueObjects;
using Spectre.Console;
using Spectre.Console.Cli;

namespace NArchitecture.Gen.ConsoleUI.Features.ProjectManagement.Commands.New;

public partial class CreateNewProjectCliCommand
{
    public class Settings : CommandSettings
    {
        [CommandArgument(position: 0, template: "[ProjectName]")]
        public string? ProjectName { get; set; }

        [CommandOption("--template|-t")]
        public string? TemplateId { get; set; }

        public void CheckProjectNameArgument()
        {
            if (ProjectName != null)
                return;

            ProjectName = AnsiConsole.Ask<string>("What's project name?");

            if (string.IsNullOrWhiteSpace(ProjectName))
                throw new ArgumentNullException(nameof(ProjectName));
        }

        public async Task CheckTemplateSelectionAsync(ITemplateService templateService)
        {
            if (!string.IsNullOrEmpty(TemplateId))
                return;

            List<ProjectTemplate> availableTemplates = await templateService.GetAvailableTemplatesAsync();
            
            if (availableTemplates.Count == 1)
            {
                TemplateId = availableTemplates.First().Id;
                return;
            }

            // Display available templates in a table
            var table = new Table();
            table.AddColumn("ID");
            table.AddColumn("Name");
            table.AddColumn("Description");
            table.AddColumn("Default");

            foreach (ProjectTemplate template in availableTemplates)
            {
                table.AddRow(
                    template.Id,
                    template.Name,
                    template.Description
                );
            }

            AnsiConsole.Write(table);

            // Create selection prompt
            var selectionPrompt = new SelectionPrompt<string>()
                .Title("Select a project template:")
                .PageSize(10)
                .MoreChoicesText("[grey](Move up and down to reveal more templates)[/]");

            foreach (ProjectTemplate template in availableTemplates)
            {
                selectionPrompt.AddChoice(template.Id);
                selectionPrompt.UseConverter(id =>
                {
                    ProjectTemplate? t = availableTemplates.FirstOrDefault(x => x.Id == id);
                    return t?.Name ?? id;
                });
            }

            TemplateId = AnsiConsole.Prompt(selectionPrompt);
        }
    }
}
