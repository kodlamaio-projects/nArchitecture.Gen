using System.ComponentModel;
using Core.CodeGen.Code;
using Core.CrossCuttingConcerns.Exceptions;
using Core.CrossCuttingConcerns.Helpers;
using Spectre.Console;
using Spectre.Console.Cli;

namespace ConsoleUI.Commands.Upgrade;

public partial class UpgradeCliCommand
{
    public class Settings : CommandSettings
    {
        [CommandOption("-p|--project")]
        [Description("Project folder under src/ (auto-detected when there is only one).")]
        public string? ProjectName { get; set; }

        [CommandOption("--dry-run")]
        [Description("Analyze and write MIGRATION-REPORT.md without changing any file.")]
        public bool DryRun { get; set; }

        [CommandOption("--keep-test-framework")]
        [Description("Keep xunit 2 / VSTest instead of moving tests to xunit.v3 + Microsoft Testing Platform.")]
        public bool KeepTestFramework { get; set; }

        [CommandOption("--no-build")]
        [Description("Do not restore and build after applying the changes.")]
        public bool NoBuild { get; set; }

        [CommandOption("--skip")]
        [Description("Comma separated step ids to skip: project-files, mediator, automapper, tests, web-api, format, verify.")]
        public string? Skip { get; set; }

        [CommandOption("--force")]
        [Description("Run even when the git working tree has uncommitted changes.")]
        public bool Force { get; set; }

        public string SolutionPath => Environment.CurrentDirectory;

        public string ProjectPath => PlatformHelper.SecuredPathJoin(SolutionPath, "src", ProjectName!);

        public IReadOnlySet<string> SkippedSteps =>
            (Skip ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

        public void CheckProjectName()
        {
            if (ProjectName != null)
            {
                if (!Directory.Exists(ProjectPath))
                    throw new BusinessException($"Project not found in \"{ProjectPath}\".");
                AnsiConsole.MarkupLine($"Selected [green]project[/] is [blue]{ProjectName}[/].");
                return;
            }

            string srcPath = PlatformHelper.SecuredPathJoin(SolutionPath, "src");
            if (!Directory.Exists(srcPath))
                throw new BusinessException("Run `nArchGen upgrade` from the solution root; no src folder found here.");

            string[] projects = Directory.GetDirectories(srcPath).Select(Path.GetFileName).Where(p => p != "corePackages").ToArray()!;
            if (projects.Length == 0)
                throw new BusinessException("No projects found in src");
            if (projects.Length == 1 || !AnsiConsole.Profile.Capabilities.Interactive)
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
    }
}
