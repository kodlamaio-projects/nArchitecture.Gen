using System.ComponentModel;
using ConsoleUI.Exceptions;
using Domain.ValueObjects;
using Spectre.Console;
using Spectre.Console.Cli;

namespace ConsoleUI.Commands.Ai.Init;

public partial class AiInitCliCommand
{
    public class Settings : InteractiveCliCommandSettings
    {
        [CommandOption("--tools")]
        [Description("Comma separated AI tools to write a file for, next to AGENTS.md: claude, codex, copilot, cursor, custom.")]
        public string? Tools { get; set; }

        [CommandOption("--custom-file")]
        [Description("File name for the custom tool, relative to the solution root. Written as .md; implies --tools custom.")]
        public string? CustomFile { get; set; }

        [CommandOption("--skills")]
        [Description("Also write skills (add-entity, add-command, add-query, add-business-rule) and file-scoped rules for the tools.")]
        public bool IncludeSkills { get; set; }

        [CommandOption("--force")]
        [Description("Overwrite files that already exist. Without it nothing is written when any of them exists (exit code 3).")]
        public bool Force { get; set; }

        [CommandOption("--dry-run")]
        [Description("Write nothing; list the files that would be created and show a diff of the ones that would be overwritten.")]
        public bool DryRun { get; set; }

        public string SolutionPath => Environment.CurrentDirectory;

        public List<AiTool> SelectedTools { get; } = [];

        public void CheckSolutionRoot()
        {
            string srcPath = Path.Combine(SolutionPath, "src");
            bool hasProject =
                Directory.Exists(srcPath) && Directory.GetDirectories(srcPath).Any(path => Path.GetFileName(path) != "corePackages");
            if (!hasProject)
                throw new UsageException("No project found here. Run nArchGen ai init from the solution root (the folder with src/).");
        }

        public void CheckToolsOption()
        {
            if (Tools is not null)
                SelectedTools.AddRange(parseTools(Tools));
            if (CustomFile is not null && !SelectedTools.Contains(AiTool.Custom))
                SelectedTools.Add(AiTool.Custom);

            // Without a terminal to ask, only AGENTS.md is written unless tools were passed.
            if (Tools is null && CustomFile is null && IsInteractive)
                SelectedTools.AddRange(
                    AnsiConsole.Prompt(
                        new MultiSelectionPrompt<AiTool>()
                            .Title("Which [green]AI tools[/] do you want a file for? [grey](AGENTS.md is always written)[/]")
                            .NotRequired()
                            .PageSize(5)
                            .InstructionsText("[grey](Press [blue]<space>[/] to toggle a tool, [green]<enter>[/] to accept)[/]")
                            .AddChoices(Enum.GetValues<AiTool>())
                    )
                );

            if (SelectedTools.Count > 0)
                AnsiConsole.MarkupLine($"Selected [green]AI tools[/]: [blue]{string.Join(", ", SelectedTools)}[/].");
        }

        public void CheckSkillsOption()
        {
            bool hasSkillTool = SelectedTools.Any(tool => tool != AiTool.Custom);
            if (IncludeSkills)
            {
                if (!hasSkillTool)
                    throw new UsageException("--skills needs at least one of claude, codex, copilot, cursor in --tools.");
                AnsiConsole.MarkupLine("[green]Skills[/] and [green]file-scoped rules[/] will be written.");
                return;
            }

            // Without a terminal to ask, skills are written only with --skills.
            if (!hasSkillTool || !IsInteractive)
                return;

            IncludeSkills = AnsiConsole.Confirm(
                $"Add [green]skills[/] ({string.Join(", ", AiInstructions.Skills)}) and [green]file-scoped rules[/] for these tools?"
            );
        }

        public void CheckCustomFileOption()
        {
            if (!SelectedTools.Contains(AiTool.Custom))
                return;

            if (CustomFile is null)
            {
                EnsureInteractive("Missing --custom-file for the custom AI tool.");
                CustomFile = AnsiConsole.Ask<string>("What's the [green]file name[/] for your AI tool? [grey](without extension)[/]");
            }

            // The warning about a replaced extension is reported with the result.
            string customPath = AiToolFiles.NormalizeCustomPath(CustomFile, out _);
            if (string.IsNullOrWhiteSpace(Path.GetFileNameWithoutExtension(customPath)))
                throw new UsageException("The custom AI file name cannot be empty.");
            if (
                Path.IsPathRooted(customPath)
                || customPath.Split('/').Contains("..")
                || customPath.Split('/').Any(segment => segment.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            )
                throw new UsageException($"\"{CustomFile}\" is not a valid file name inside the solution.");

            string[] reservedPaths = [AiToolFiles.Agents, .. SelectedTools.Select(AiToolFiles.PathOf).OfType<string>()];
            if (reservedPaths.Contains(customPath, StringComparer.OrdinalIgnoreCase))
                throw new UsageException($"\"{customPath}\" is already written by nArchGen; choose another custom file name.");

            AnsiConsole.MarkupLine($"Custom AI file: [blue]{customPath.EscapeMarkup()}[/].");
        }

        private static IEnumerable<AiTool> parseTools(string tools)
        {
            foreach (string name in tools.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (!name.All(char.IsLetter) || !Enum.TryParse(name, ignoreCase: true, out AiTool tool))
                    throw new UsageException(
                        $"Unknown AI tool \"{name}\". Available: {FormatChoices(Enum.GetNames<AiTool>().Select(n => n.ToLowerInvariant()))}."
                    );
                yield return tool;
            }
        }
    }
}
