using System.ComponentModel;
using ConsoleUI.Exceptions;
using Core.CodeGen.Code;
using Core.CrossCuttingConcerns.Helpers;
using Spectre.Console;
using Spectre.Console.Cli;

namespace ConsoleUI.Commands.Generate;

public abstract class GenerateCliCommandSettings : InteractiveCliCommandSettings
{
    [CommandOption("-p|--project")]
    [Description("Project folder under src/. Auto-detected when run inside a project or when src/ has a single project.")]
    public string? ProjectName { get; set; }

    [CommandOption("--force")]
    [Description("Overwrite files that already exist. Without it nothing is written when any generated file exists (exit code 3).")]
    public bool Force { get; set; }

    [CommandOption("--dry-run")]
    [Description("Run every step without writing anything; list the files that would be created and show a diff of the updated ones.")]
    public bool DryRun { get; set; }

    public string ProjectPath =>
        ProjectName != null
            ? PlatformHelper.SecuredPathJoin(Environment.CurrentDirectory, "src", ProjectName.ToCamelCase())
            : Environment.CurrentDirectory;

    public void CheckProjectName()
    {
        if (ProjectName != null)
        {
            if (!Directory.Exists(ProjectPath))
                throw new UsageException($"Project not found in \"{ProjectPath}\".");
            AnsiConsole.MarkupLine($"Selected [green]project[/] is [blue]{ProjectName}[/].");
            return;
        }

        string[] layerFolders = { "Application", "Domain", "Persistence", "WebAPI" };
        if (layerFolders.All(folder => Directory.Exists($"{Environment.CurrentDirectory}/{folder}")))
            return;

        string srcPath = $"{Environment.CurrentDirectory}/src";
        if (!Directory.Exists(srcPath))
            throw new UsageException(
                "No project found here. Run nArchGen from the solution root (the folder with src/) or from a project folder."
            );

        string[] projects = Directory
            .GetDirectories(srcPath)
            .Select(path => Path.GetFileName(path))
            .Where(project => project != "corePackages")
            .ToArray();
        if (projects.Length == 0)
            throw new UsageException("No projects found in src");
        if (projects.Length == 1)
        {
            ProjectName = projects.First();
            AnsiConsole.MarkupLine($"Selected [green]project[/] is [blue]{ProjectName}[/].");
            return;
        }

        EnsureInteractive($"Multiple projects found in src; pass one with --project. Available: {FormatChoices(projects)}.");
        ProjectName = AnsiConsole.Prompt(
            new SelectionPrompt<string>().Title("What's your [green]project[/] in [blue]src[/] folder?").PageSize(10).AddChoices(projects)
        );
    }
}
