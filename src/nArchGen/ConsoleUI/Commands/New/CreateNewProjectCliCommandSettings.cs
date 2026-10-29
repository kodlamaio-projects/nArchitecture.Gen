using System.ComponentModel;
using ConsoleUI.Exceptions;
using Spectre.Console;
using Spectre.Console.Cli;

namespace ConsoleUI.Commands.New;

public partial class CreateNewProjectCliCommand
{
    public class Settings : InteractiveCliCommandSettings
    {
        [CommandArgument(position: 0, template: "[ProjectName]")]
        public string? ProjectName { get; set; }

        [CommandOption("--security")]
        public bool IsThereSecurityMechanism { get; set; }

        [CommandOption("--starter-path")]
        [Description("Local checkout of the nArchitecture starter to copy instead of downloading the tagged release.")]
        public string? StarterPath { get; set; }

        public void CheckProjectNameArgument()
        {
            if (ProjectName == null)
            {
                EnsureInteractive("Missing <ProjectName> argument.");
                ProjectName = AnsiConsole.Ask<string>("What's project name?");
            }

            if (string.IsNullOrWhiteSpace(ProjectName))
                throw new UsageException("Project name cannot be empty.");
        }

        public void CheckIsThereSecurityMechanismArgument()
        {
            if (IsThereSecurityMechanism)
                return;
            // Without a terminal to ask, the project is created without security.
            if (!IsInteractive)
                return;
            IsThereSecurityMechanism = AnsiConsole.Confirm(
                prompt: "Do you want to add security mechanism to your project?",
                defaultValue: false
            );
        }
    }
}
