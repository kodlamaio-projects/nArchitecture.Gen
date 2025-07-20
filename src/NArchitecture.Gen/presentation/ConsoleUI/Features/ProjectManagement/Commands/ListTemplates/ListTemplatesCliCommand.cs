using NArchitecture.Gen.Domain.Features.TemplateManagement.DomainServices;
using NArchitecture.Gen.Domain.Features.TemplateManagement.ValueObjects;
using Spectre.Console;
using Spectre.Console.Cli;

namespace NArchitecture.Gen.ConsoleUI.Features.ProjectManagement.Commands.ListTemplates;

public class ListTemplatesCliCommand : AsyncCommand
{
    private readonly ITemplateService _templateService;

    public ListTemplatesCliCommand(ITemplateService templateService)
    {
        _templateService = templateService ?? throw new ArgumentNullException(nameof(templateService));
    }

    public override async Task<int> ExecuteAsync(CommandContext context)
    {
        try
        {
            List<ProjectTemplate> templates = await _templateService.GetAvailableTemplatesAsync();
            TemplateConfiguration config = await _templateService.GetTemplateConfigurationAsync();

            AnsiConsole.MarkupLine("[bold blue]Available Project Templates[/]");
            AnsiConsole.WriteLine();

            var table = new Table();
            table.AddColumn("ID");
            table.AddColumn("Name");
            table.AddColumn("Description");
            table.AddColumn("Repository");
            table.AddColumn("Mode");
            table.AddColumn("Version/Branch");
            table.AddColumn("Default");

            foreach (ProjectTemplate template in templates)
            {
                string versionInfo = template.InstallationMode == TemplateInstallationMode.Release
                    ? $"v{template.ReleaseVersion}"
                    : $"branch: {template.BranchName}";

                if (config.Settings.IsDebugMode)
                {
                    versionInfo += " [yellow](debug mode)[/]";
                }

                table.AddRow(
                    template.Id,
                    template.Name,
                    template.Description,
                    template.RepositoryUrl,
                    template.InstallationMode.ToString(),
                    versionInfo
                );
            }

            AnsiConsole.Write(table);

            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[dim]Configuration Version: {config.Version}[/]");
            AnsiConsole.MarkupLine($"[dim]Debug Mode: {(config.Settings.IsDebugMode ? "Enabled" : "Disabled")}[/]");
            AnsiConsole.MarkupLine($"[dim]Default Template: {config.Settings.DefaultTemplateId}[/]");

            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error: {ex.Message}[/]");
            return 1;
        }
    }
}