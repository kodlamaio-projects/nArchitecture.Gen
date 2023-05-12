using Spectre.Console;
using Spectre.Console.Cli;

namespace ConsoleUI.Commands.New;

public partial class CreateNewProjectCliCommand
{
    public class Settings : CommandSettings
    {
        [CommandArgument(position: 0, template: "[ProjectName]")]
        public string? ProjectName { get; set; }

        [CommandOption("--no-security")]
        public bool IsThereSecurityMechanism { get; set; }

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
            IsThereSecurityMechanism = AnsiConsole.Confirm(
                prompt: "Do you want to add security mechanism to your project?",
                defaultValue: true
            );
        }
    }
}
