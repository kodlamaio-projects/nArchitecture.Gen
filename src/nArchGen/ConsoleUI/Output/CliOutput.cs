using System.Text.Encodings.Web;
using System.Text.Json;
using Spectre.Console;

namespace ConsoleUI.Output;

/// <summary>
/// Decides where output goes. In <c>--json</c> mode stdout carries only the <see cref="CliResult"/> document; progress,
/// messages and errors are written to stderr.
/// </summary>
public static class CliOutput
{
    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static bool IsJson { get; private set; }

    /// <summary>The command named on the command line, such as <c>generate crud</c>; used when reporting a failed run.</summary>
    public static string CommandName { get; private set; } = "nArchGen";

    public static IAnsiConsole ErrorConsole { get; private set; } = AnsiConsole.Console;

    public static void Configure(string[] args)
    {
        IsJson = args.Contains("--json");
        CommandName = getCommandName(args);

        ErrorConsole = AnsiConsole.Create(new AnsiConsoleSettings { Out = new AnsiConsoleOutput(Console.Error) });
        // Redirected output is read by scripts and agents: do not wrap messages and file paths at the default 80 columns.
        if (Console.IsErrorRedirected)
            ErrorConsole.Profile.Width = short.MaxValue;

        if (IsJson)
            AnsiConsole.Console = ErrorConsole;
        else if (Console.IsOutputRedirected)
            AnsiConsole.Profile.Width = short.MaxValue;
    }

    /// <summary>Writes the JSON document in <c>--json</c> mode, otherwise the warnings for a human reader.</summary>
    public static void WriteResult(CliResult result)
    {
        if (IsJson)
        {
            WriteJson(result);
            return;
        }

        foreach (string warning in result.Warnings)
            AnsiConsole.MarkupLine($":warning: [yellow]{warning.EscapeMarkup()}[/]");
    }

    /// <summary>
    /// Runs <paramref name="action"/> under a spinner on a terminal. Without one (redirected output, CI) the spinner is
    /// skipped, since Spectre would print its last status without a line break.
    /// </summary>
    public static async Task RunWithStatusAsync(string status, Func<Action<string>, Task> action)
    {
        if (!AnsiConsole.Profile.Capabilities.Interactive || !AnsiConsole.Profile.Capabilities.Ansi)
        {
            await action(_ => { });
            return;
        }

        await AnsiConsole
            .Status()
            .Spinner(Spinner.Known.Dots2)
            .SpinnerStyle(style: Style.Parse(text: "blue"))
            .StartAsync(status, ctx => action(currentStatus => ctx.Status(currentStatus)));
    }

    /// <summary>Writes a file path on its own line, in full; <see cref="TextPath"/> would shorten long paths to the console width.</summary>
    public static void WriteFilePath(string filePath)
    {
        string directory = Path.GetDirectoryName(filePath) is { Length: > 0 } name ? name + Path.DirectorySeparatorChar : string.Empty;
        AnsiConsole.MarkupLine($"[yellow]{directory.EscapeMarkup()}[/][blue]{Path.GetFileName(filePath).EscapeMarkup()}[/]");
    }

    public static void WriteJson(CliResult result) => Console.Out.WriteLine(JsonSerializer.Serialize(result, _jsonOptions));

    private static string getCommandName(string[] args)
    {
        string[] words = args.TakeWhile(arg => !arg.StartsWith('-')).ToArray();
        return words switch
        {
            ["generate", string subcommand, ..] => $"generate {subcommand}",
            [string command, ..] => command,
            _ => "nArchGen",
        };
    }
}
