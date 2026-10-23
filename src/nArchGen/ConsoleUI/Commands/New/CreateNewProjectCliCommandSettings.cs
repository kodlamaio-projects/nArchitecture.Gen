using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;

namespace ConsoleUI.Commands.New;

public partial class CreateNewProjectCliCommand
{
    public class Settings : CommandSettings
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
            if (ProjectName != null)
                return;

            ProjectName = AnsiConsole.Ask<string>("What's project name?");

            if (string.IsNullOrWhiteSpace(ProjectName))
                throw new ArgumentNullException(nameof(ProjectName));
        }

        public void CheckIsThereSecurityMechanismArgument()
        {
            if (IsThereSecurityMechanism)
                return;
            // Non-interactive terminals (CI, scripts) cannot answer the confirm; the project is created without security.
            if (!AnsiConsole.Profile.Capabilities.Interactive)
                return;
            IsThereSecurityMechanism = AnsiConsole.Confirm(
                prompt: "Do you want to add security mechanism to your project?",
                defaultValue: false
            );
        }
    }
}
