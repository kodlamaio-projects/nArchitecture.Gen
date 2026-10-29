using System.ComponentModel;
using ConsoleUI.Exceptions;
using Spectre.Console;
using Spectre.Console.Cli;

namespace ConsoleUI.Commands;

public abstract class InteractiveCliCommandSettings : CommandSettings
{
    [CommandOption("--no-interactive")]
    [Description("Never prompt. A missing required value fails with exit code 2. Implied when input is redirected or CI is set.")]
    public bool NoInteractive { get; set; }

    [CommandOption("--json")]
    [Description("Write only a JSON result to stdout (progress goes to stderr). Implies --no-interactive.")]
    public bool Json { get; set; }

    public bool IsInteractive =>
        !NoInteractive
        && !Json
        && AnsiConsole.Profile.Capabilities.Interactive
        && !Console.IsInputRedirected
        && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CI"));

    /// <summary>Fails when a required value is missing and cannot be prompted for.</summary>
    protected void EnsureInteractive(string missingValueMessage)
    {
        if (!IsInteractive)
            throw new UsageException(missingValueMessage);
    }

    protected static string FormatChoices(IEnumerable<string?> choices) => string.Join(", ", choices);
}
